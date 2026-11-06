using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RippedTools
{
    /// <summary>
    /// Builds the UI animation viewer scene and renders frame strips for checking clips in batchmode.
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod RippedTools.UIAnimTools.Run [-stripOut C:/dir] [-stripCount 8]
    /// </summary>
    public static class UIAnimTools
    {
        internal const string ScenePath = "Assets/RippedTools/UIAnimViewer.unity";
        const string TransitionFolder = "Assets/assetbundles/ui/transitionex";
        static readonly Vector2 Reference = new Vector2(1920, 1080);

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name, string fallback)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }
            StellaTools.SetupPipeline();
            TransitionConfigTools.CreateMissing();
            BuildScene();
            var stripOut = Arg("-stripOut", null);
            if (stripOut != null)
                RenderStrips(stripOut, int.Parse(Arg("-stripCount", "8")), Arg("-stripFilter", null));
        }

        /// <summary>
        /// Copies only what the viewer scene needs into a new project folder: the scene's asset dependencies, the
        /// shader include files next to used shaders, the asmdefs owning used scripts, the tools and the URP settings.
        /// Unity.exe -batchmode -quit -projectPath ... -executeMethod RippedTools.UIAnimTools.ExportLite -liteOut C:/dest
        /// </summary>
        public static void ExportLite()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-liteOut");
            if (at < 0 || at + 1 >= args.Length)
                throw new ArgumentException("-liteOut <folder> is required");
            var dest = Path.GetFullPath(args[at + 1]);

            var files = new System.Collections.Generic.HashSet<string>(
                AssetDatabase.GetDependencies(ScenePath, true).Where(p => p.StartsWith("Assets/")));
            files.Add(ScenePath);
            foreach (var folder in new[] { "Assets/Editor/RippedTools", "Assets/RippedTools", "Assets/Settings" })
                if (Directory.Exists(folder))
                    files.UnionWith(Directory.GetFiles(folder).Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/')));
            foreach (var shader in files.Where(f => f.EndsWith(".shader")).ToList())
                files.UnionWith(Directory.GetFiles(Path.GetDirectoryName(shader))
                    .Where(f => f.EndsWith(".cginc") || f.EndsWith(".hlsl") || f.EndsWith(".cg"))
                    .Select(f => f.Replace('\\', '/')));
            foreach (var script in files.Where(f => f.EndsWith(".cs")).ToList())
                for (var dir = Path.GetDirectoryName(script); !string.IsNullOrEmpty(dir) && dir != "Assets"; dir = Path.GetDirectoryName(dir))
                {
                    var asmdef = Directory.GetFiles(dir, "*.asmdef").FirstOrDefault();
                    if (asmdef != null) { files.Add(asmdef.Replace('\\', '/')); break; }
                }

            long bytes = 0;
            var folders = new System.Collections.Generic.HashSet<string>();
            foreach (var f in files)
            {
                CopyWithMeta(f, dest);
                bytes += new FileInfo(f).Length;
                for (var dir = Path.GetDirectoryName(f).Replace('\\', '/'); dir != "Assets" && folders.Add(dir); dir = Path.GetDirectoryName(dir).Replace('\\', '/'))
                    if (File.Exists(dir + ".meta"))
                        File.Copy(dir + ".meta", Path.Combine(dest, dir + ".meta"), true);
            }
            foreach (var f in Directory.GetFiles("ProjectSettings"))
                CopyWithMeta(f.Replace('\\', '/'), dest);
            foreach (var f in new[] { "Packages/manifest.json", "Packages/packages-lock.json" })
                if (File.Exists(f))
                    CopyWithMeta(f, dest);
            Debug.Log($"[UIAnimTools] lite export: {files.Count} assets, {bytes / (1024f * 1024f):0.0} MB -> {dest}");
        }

        static void CopyWithMeta(string path, string dest)
        {
            var target = Path.Combine(dest, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(path, target, true);
            if (File.Exists(path + ".meta"))
                File.Copy(path + ".meta", target + ".meta", true);
        }

        /// <summary>
        /// Returns one "go to time t" sampler per DOTweenAnimation under root. Works with both the bundled stand-in
        /// (Prepare/Evaluate) and the real DOTween Pro (CreateTween + tween.Goto), resolved by reflection so this
        /// file compiles against either.
        /// </summary>
        static System.Collections.Generic.List<Action<float>> TweenSamplers(GameObject root)
        {
            var result = new System.Collections.Generic.List<Action<float>>();
            foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (c == null || c.GetType().FullName != "DG.Tweening.DOTweenAnimation")
                    continue;
                var type = c.GetType();
                if (type.GetField("isActive")?.GetValue(c) is bool active && !active)
                    continue; // switched off by a config
                var evaluate = type.GetMethod("Evaluate", new[] { typeof(float) });
                if (evaluate != null)
                {
                    type.GetMethod("Prepare")?.Invoke(c, null);
                    result.Add(t => evaluate.Invoke(c, new object[] { t }));
                    continue;
                }
                type.GetMethod("CreateTween")?.Invoke(c, new object[] { true, false });
                var tween = type.GetField("tween")?.GetValue(c);
                var gotoMethod = tween?.GetType().GetMethod("Goto", new[] { typeof(float), typeof(bool) })
                    ?? Type.GetType("DG.Tweening.TweenExtensions, DOTween")?.GetMethod("Goto");
                if (tween != null && gotoMethod != null)
                    result.Add(t => gotoMethod.Invoke(gotoMethod.IsStatic ? null : tween,
                        gotoMethod.IsStatic ? new object[] { tween, t, false } : new object[] { t, false }));
            }
            return result;
        }

        static int NaturalKey(string name)
        {
            var m = Regex.Match(name, @"(\d+)");
            return m.Success ? int.Parse(m.Value) : int.MaxValue;
        }

        internal static GameObject[] TransitionStyles() => AssetDatabase.FindAssets("t:Prefab", new[] { TransitionFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == TransitionFolder && Path.GetFileName(p).StartsWith("Style_", StringComparison.OrdinalIgnoreCase))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .OrderBy(g => NaturalKey(g.name))
            .ToArray();

        static GameObject[] AnimatedPanels() => AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/assetbundles/ui" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith(TransitionFolder))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(g => g.GetComponent<RectTransform>() != null && UIAnimViewer.ClipsOf(g).Count > 0)
            .OrderBy(g => g.name)
            .ToArray();

        [MenuItem("Tools/Ripped/Build UI Animation Viewer")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = CreateCamera();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;

            var sceneA = Backdrop(canvasGo.transform, "Scene A", new Color(0.18f, 0.32f, 0.55f));
            var sceneB = Backdrop(canvasGo.transform, "Scene B", new Color(0.55f, 0.25f, 0.30f));
            sceneB.SetActive(false);
            var panelLayer = Layer(canvasGo.transform, "Panel Layer");
            var transitionLayer = Layer(canvasGo.transform, "Transition Layer");

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var viewer = new GameObject("UI Anim Viewer").AddComponent<UIAnimViewer>();
            viewer.transitionStyles = TransitionStyles().ToList();
            viewer.panels = AnimatedPanels().ToList();
            viewer.transitionLayer = transitionLayer;
            viewer.panelLayer = panelLayer;
            viewer.sceneA = sceneA;
            viewer.sceneB = sceneB;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[UIAnimTools] saved {ScenePath}: {viewer.transitionStyles.Count} transition styles, {viewer.panels.Count} animated panels");
        }

        static Camera CreateCamera()
        {
            var cam = new GameObject("UI Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            return cam;
        }

        static RectTransform Layer(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static GameObject Backdrop(Transform parent, string label, Color color)
        {
            var rt = Layer(parent, label);
            rt.gameObject.AddComponent<Image>().color = color;
            var text = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            text.rectTransform.SetParent(rt, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            text.fontSize = 140;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1, 1, 1, 0.85f);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return rt.gameObject;
        }

        /// <summary>
        /// Writes one contact sheet per target. Transition styles are sampled along their TransitionPlan (12 evenly spaced
        /// moments from Scene A to Scene B, with the style's config applied); panels sample each clip separately.
        /// </summary>
        /// <param name="configFor">Config lookup override (tests); defaults to the configs in the config folder.</param>
        internal static void RenderStrips(string outDir, int count, string filter, Func<GameObject, TransitionStyleConfig> configFor = null)
        {
            Directory.CreateDirectory(outDir);
            var viewer = UnityEngine.Object.FindObjectOfType<UIAnimViewer>();
            var cam = UnityEngine.Object.FindObjectOfType<Camera>();
            var targets = viewer.transitionStyles.Concat(viewer.panels)
                .Where(g => filter == null || Regex.IsMatch(g.name, filter, RegexOptions.IgnoreCase))
                .Take(count);
            if (configFor == null)
            {
                var configs = TransitionConfigTools.AllConfigs().ToList();
                configFor = p => configs.FirstOrDefault(c => c.useConfig && c.stylePrefab == p);
            }
            const int w = 480, h = 270, cols = 6;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            foreach (var prefab in targets)
            {
                if (viewer.transitionStyles.Contains(prefab))
                {
                    RenderTransitionStrip(viewer, cam, rt, prefab, configFor(prefab), outDir, w, h, cols);
                    continue;
                }
                var layer = viewer.panelLayer;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layer);
                go.SetActive(true);
                UIAnimViewer.PrepareInstance(go);
                var clips = UIAnimViewer.ClipsOf(go);
                var tweens = TweenSamplers(go);
                var handlers = go.GetComponentsInChildren<UIMaterialAnimationHandler>(true);
                Debug.Log($"[UIAnimTools] {prefab.name}: " + string.Join(", ", clips.Select(c => $"{c.animator.name}/{c.clip.name} {c.clip.length:0.00}s loop={c.clip.isLooping}")));
                var frames = clips.SelectMany(c => Enumerable.Range(0, cols).Select(i => (c.animator, c.clip, t: c.clip.length * i / (cols - 1)))).ToList();
                int rows = Mathf.Max(1, Mathf.CeilToInt(frames.Count / (float)cols));
                var sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false);
                for (int i = 0; i < frames.Count; i++)
                {
                    var f = frames[i];
                    f.clip.SampleAnimation(f.animator.gameObject, f.t);
                    foreach (var sample in tweens)
                        sample(f.t);
                    foreach (var hd in handlers)
                        hd.ForceRefresh();
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    RenderTexture.active = rt;
                    var frame = new Texture2D(w, h, TextureFormat.RGB24, false);
                    frame.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    sheet.SetPixels(i % cols * w, (rows - 1 - i / cols) * h, w, h, frame.GetPixels());
                    UnityEngine.Object.DestroyImmediate(frame);
                }
                if (frames.Count == 0)
                {
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                }
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, prefab.name + ".png"), sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(go);
            }
            cam.targetTexture = null;
            rt.Release();
        }

        static void RenderTransitionStrip(UIAnimViewer viewer, Camera cam, RenderTexture rt, GameObject prefab, TransitionStyleConfig cfg,
                                          string outDir, int w, int h, int cols)
        {
            var holder = UIAnimViewer.CreateHolder(viewer.transitionLayer);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            go.SetActive(true);
            UIAnimViewer.PrepareInstance(go);
            var runtime = cfg != null ? TransitionConfigRuntime.Attach(holder, go.transform, cfg, 1f) : null;
            var plan = TransitionPlan.Build(go, cfg, viewer.holdSeconds);
            holder.SetActive(true);
            var tweens = TweenSamplers(go);
            var handlers = go.GetComponentsInChildren<UIMaterialAnimationHandler>(true);
            Debug.Log($"[UIAnimTools] {prefab.name}{(cfg != null ? $" (config {cfg.name})" : "")}: " +
                      $"in {plan.inPhase.start:0.00}-{plan.inPhase.End:0.00}s x{plan.inPhase.speed:0.##} [{string.Join(",", plan.inPhase.tracks.Select(t => $"{t.clip.name} {t.clip.length:0.00}s"))}], " +
                      $"out {plan.outPhase.start:0.00}-{plan.outPhase.End:0.00}s x{plan.outPhase.speed:0.##} [{string.Join(",", plan.outPhase.tracks.Select(t => $"{t.clip.name} {t.clip.length:0.00}s"))}], " +
                      $"swap {plan.swapTime:0.00}s, end {plan.end:0.00}s");

            int frames = cols * 2;
            var sheet = new Texture2D(w * cols, h * 2, TextureFormat.RGB24, false);
            for (int i = 0; i < frames; i++)
            {
                float t = plan.end * i / (frames - 1);
                viewer.sceneA.SetActive(t < plan.swapTime);
                viewer.sceneB.SetActive(t >= plan.swapTime);
                var phase = plan.ActiveAt(t);
                foreach (var (animator, clip) in phase.tracks)
                    clip.SampleAnimation(animator.gameObject, phase.Local(t));
                foreach (var sample in tweens)
                    sample(t);
                foreach (var hd in handlers)
                    hd.ForceRefresh();
                runtime?.Apply();
                Canvas.ForceUpdateCanvases();
                cam.Render();
                runtime?.Restore();
                RenderTexture.active = rt;
                var frame = new Texture2D(w, h, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                sheet.SetPixels(i % cols * w, (1 - i / cols) * h, w, h, frame.GetPixels());
                UnityEngine.Object.DestroyImmediate(frame);
            }
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(outDir, prefab.name + ".png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            runtime?.Cleanup();
            UnityEngine.Object.DestroyImmediate(holder);
            viewer.sceneA.SetActive(true);
            viewer.sceneB.SetActive(false);
        }
    }
}
