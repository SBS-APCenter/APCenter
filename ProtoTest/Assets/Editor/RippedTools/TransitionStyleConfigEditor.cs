using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RippedTools
{
    /// <summary>Creating, populating and playing TransitionStyleConfig assets.</summary>
    public static class TransitionConfigTools
    {
        public const string ConfigFolder = "Assets/RippedTools/Resources/" + TransitionStyleConfig.ResourcesFolder;
        const string PlayKey = "RippedTools.PlayConfig";

        static readonly string[] SkippedProps = { "_Stencil", "_ColorMask", "_UseUIAlphaClip", "_UIMaskSoftness", "_texcoord" };

        public static IEnumerable<TransitionStyleConfig> AllConfigs() =>
            AssetDatabase.FindAssets("t:" + nameof(TransitionStyleConfig))
                .Select(g => AssetDatabase.LoadAssetAtPath<TransitionStyleConfig>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null);

        /// <summary>Creates a populated config for every transition style that has none yet. Existing configs are never touched.</summary>
        [MenuItem("Tools/Ripped/Create Transition Configs")]
        public static void CreateMissing()
        {
            EnsureFolder(ConfigFolder);
            var covered = new HashSet<GameObject>(AllConfigs().Select(c => c.stylePrefab).Where(p => p != null));
            int created = 0;
            foreach (var prefab in UIAnimTools.TransitionStyles())
            {
                if (covered.Contains(prefab))
                    continue;
                var cfg = ScriptableObject.CreateInstance<TransitionStyleConfig>();
                cfg.stylePrefab = prefab;
                Populate(cfg, false);
                AssetDatabase.CreateAsset(cfg, AssetDatabase.GenerateUniqueAssetPath($"{ConfigFolder}/{prefab.name}.asset"));
                created++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TransitionConfigTools] created {created} configs in {ConfigFolder}");
        }

        /// <summary>Re-reads every config's prefab (new colors, sprites, material properties) while keeping the values already set.</summary>
        [MenuItem("Tools/Ripped/Repopulate All Transition Configs")]
        public static void RepopulateAll()
        {
            int count = 0;
            foreach (var cfg in AllConfigs())
            {
                Populate(cfg, true);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TransitionConfigTools] repopulated {count} configs");
        }

        static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                var parent = string.Join("/", parts.Take(i));
                var path = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }

        /// <summary>
        /// Fills the lists that mirror the prefab (element paths, palette, sprites, material properties) and the summary.
        /// Merges by key, so values the user already entered survive; entries that no longer occur get usage 0.
        /// </summary>
        public static void Populate(TransitionStyleConfig cfg, bool recordUndo)
        {
            var prefab = cfg.stylePrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[TransitionConfigTools] {cfg.name}: Style Prefab is empty");
                return;
            }
            if (recordUndo)
                Undo.RecordObject(cfg, "Populate Transition Config");
            var root = prefab.transform;
            cfg.elementPaths = prefab.GetComponentsInChildren<Transform>(true).Where(t => t != root)
                .Select(t => TransitionConfigRuntime.PathOf(t, root)).ToList();

            var graphics = prefab.GetComponentsInChildren<Graphic>(true);
            var clips = UIAnimViewer.ClipsOf(prefab);

            foreach (var p in cfg.color.palette) p.usage = 0;
            foreach (var group in graphics.GroupBy(g => Opaque(g.color)).OrderByDescending(g => g.Count()))
            {
                var entry = cfg.color.palette.FirstOrDefault(p => Opaque(p.from).Equals(group.Key));
                if (entry == null)
                    cfg.color.palette.Add(entry = new PaletteEntry { from = group.Key, to = group.Key });
                entry.usage = group.Count();
            }

            var sprites = new Dictionary<Sprite, int>();
            foreach (var img in graphics.OfType<Image>().Where(i => i.sprite != null))
                sprites[img.sprite] = sprites.TryGetValue(img.sprite, out var n) ? n + 1 : 1;
            foreach (var (_, clip) in clips)
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, b))
                        if (key.value is Sprite s && !sprites.ContainsKey(s))
                            sprites[s] = 0;
            foreach (var sw in cfg.resources.spriteSwaps) sw.usage = 0;
            foreach (var kv in sprites.OrderByDescending(kv => kv.Value))
            {
                var swap = cfg.resources.spriteSwaps.FirstOrDefault(s => s.original == kv.Key);
                if (swap == null)
                    cfg.resources.spriteSwaps.Add(swap = new SpriteSwap { original = kv.Key });
                swap.usage = kv.Value;
            }

            var materials = graphics.Where(g => g.material != null && g.material != g.defaultMaterial).Select(g => g.material).Distinct();
            foreach (var mat in materials)
            {
                var mo = cfg.resources.materials.FirstOrDefault(m => m.material == mat);
                if (mo == null)
                    cfg.resources.materials.Add(mo = new MaterialOverride { material = mat });
                PopulateProps(mo);
            }

            cfg.summary = Summary(prefab, graphics, clips, sprites.Count, cfg.resources.materials.Count);
            EditorUtility.SetDirty(cfg);
        }

        static Color Opaque(Color c)
        {
            Color32 q = c;
            q.a = 255;
            return q;
        }

        static void PopulateProps(MaterialOverride mo)
        {
            var mat = mo.material;
            var shader = mat.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                var name = shader.GetPropertyName(i);
                var flags = shader.GetPropertyFlags(i);
                if ((flags & (ShaderPropertyFlags.HideInInspector | ShaderPropertyFlags.PerRendererData)) != 0 || SkippedProps.Any(s => name.StartsWith(s, StringComparison.Ordinal)))
                    continue;
                var shaderType = shader.GetPropertyType(i);
                var type = shaderType == ShaderPropertyType.Color ? MaterialPropType.Color
                         : shaderType == ShaderPropertyType.Vector ? MaterialPropType.Vector
                         : shaderType == ShaderPropertyType.Texture ? MaterialPropType.Texture
                         : MaterialPropType.Float;
                var prop = mo.props.FirstOrDefault(p => p.name == name);
                bool fresh = prop == null;
                if (fresh)
                    mo.props.Add(prop = new MaterialProp { name = name });
                prop.type = type;
                prop.range = shaderType == ShaderPropertyType.Range ? shader.GetPropertyRangeLimits(i) : Vector2.zero;
                switch (type)
                {
                    case MaterialPropType.Float: prop.originalFloat = mat.GetFloat(name); if (fresh) prop.floatValue = prop.originalFloat; break;
                    case MaterialPropType.Color: prop.originalColor = mat.GetColor(name); if (fresh) prop.colorValue = prop.originalColor; break;
                    case MaterialPropType.Vector: prop.originalVector = mat.GetVector(name); if (fresh) prop.vectorValue = prop.originalVector; break;
                }
            }
        }

        static string Summary(GameObject prefab, Graphic[] graphics, List<(Animator animator, AnimationClip clip)> clips, int spriteCount, int materialCount)
        {
            string Clips(Func<AnimationClip, bool> pick) =>
                string.Join(", ", clips.Where(c => pick(c.clip)).Select(c => $"{c.clip.name} ({c.clip.length:0.00}s)"));
            var tweens = prefab.GetComponentsInChildren<DG.Tweening.DOTweenAnimation>(true);
            var tweenKinds = string.Join(", ", tweens.GroupBy(TransitionConfigRuntime.KindOf).Select(g => $"{g.Key} {g.Count()}"));
            var texts = graphics.Count(g => g is Text || g.GetType().Name.Contains("TextMeshPro"));
            return $"In: {Clips(UIAnimViewer.IsIn)}\nOut: {Clips(UIAnimViewer.IsOut)}\n" +
                   $"Graphic {graphics.Length} (텍스트 {texts}) · 스프라이트 {spriteCount} · 머티리얼 {materialCount} · " +
                   $"파티클 {prefab.GetComponentsInChildren<ParticleSystem>(true).Length} · CanvasGroup {prefab.GetComponentsInChildren<CanvasGroup>(true).Length}\n" +
                   $"DOTween {tweens.Length}{(tweens.Length > 0 ? $": {tweenKinds}" : "")}";
        }

        /// <summary>Plays the config in the viewer, opening the viewer scene and entering play mode first if needed.</summary>
        public static void Play(TransitionStyleConfig cfg)
        {
            if (EditorApplication.isPlaying)
            {
                var viewer = Object.FindObjectOfType<UIAnimViewer>();
                if (viewer != null) viewer.PlayStyle(cfg);
                else Debug.LogWarning("[TransitionConfigTools] no UIAnimViewer in the playing scene");
                return;
            }
            if (Object.FindObjectOfType<UIAnimViewer>() == null)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(UIAnimTools.ScenePath);
            }
            SessionState.SetString(PlayKey, AssetDatabase.GetAssetPath(cfg));
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void HookPlayRequest()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode)
                    return;
                var path = SessionState.GetString(PlayKey, "");
                if (path == "")
                    return;
                SessionState.EraseString(PlayKey);
                var cfg = AssetDatabase.LoadAssetAtPath<TransitionStyleConfig>(path);
                var viewer = Object.FindObjectOfType<UIAnimViewer>();
                if (cfg != null && viewer != null)
                    viewer.PlayStyle(cfg);
            };
        }
    }

    [CustomEditor(typeof(TransitionStyleConfig))]
    public class TransitionStyleConfigEditor : Editor
    {
        TransitionPlan preview;
        int previewDirty = -1;

        static readonly GUIContent PopulateLabel = new GUIContent("Populate from Prefab",
            "프리팹을 읽어 요소 경로 목록(▼ 드롭다운), 팔레트(사용된 색), 스프라이트 목록, 머티리얼 프로퍼티 목록, 상단 요약을 채웁니다.\n" +
            "이미 입력한 값(Use, 새 색, 교체 스프라이트, 새 프로퍼티 값)은 그대로 두고 새 항목만 추가합니다.");
        static readonly GUIContent PlayLabel = new GUIContent("▶ Play This Style",
            "뷰어 씬에서 이 설정으로 이 스타일을 재생합니다. Play 모드가 아니면 뷰어 씬을 열고 Play를 시작한 뒤 재생합니다.\n" +
            "Play 중 즉시 반영: Color, Shape, Elements(표시·색·스프라이트·형태), 머티리얼 값.\n" +
            "다시 재생해야 반영: Timing, Elements의 타이밍, Tweens, Particles, Scene A/B 배경.");
        static readonly GUIContent ResetLabel = new GUIContent("Reset ▾",
            "선택한 섹션을 원본 값으로 되돌립니다(Ctrl+Z로 실행 취소 가능). 목록 섹션은 되돌린 뒤 Populate로 다시 채웁니다.");
        static readonly GUIContent TimelineTip = new GUIContent("",
            "재생 타임라인 미리보기. 회색 = Pre/Hold/Post 대기, 파랑 = In, 주황 = Out, 빨간 선 = Scene A→B 교체.\n" +
            "뷰어의 Speed 슬라이더는 반영되지 않은 1배속 기준입니다.");

        public override void OnInspectorGUI()
        {
            var cfg = (TransitionStyleConfig)target;
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TransitionStyleConfig.stylePrefab)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TransitionStyleConfig.useConfig)));

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(cfg.stylePrefab == null))
                {
                    if (GUILayout.Button(PopulateLabel))
                    {
                        serializedObject.ApplyModifiedProperties();
                        TransitionConfigTools.Populate(cfg, true);
                        serializedObject.Update();
                    }
                    if (GUILayout.Button(PlayLabel))
                    {
                        serializedObject.ApplyModifiedProperties();
                        TransitionConfigTools.Play(cfg);
                    }
                }
                var resetRect = GUILayoutUtility.GetRect(ResetLabel, EditorStyles.miniPullDown, GUILayout.Width(70));
                if (EditorGUI.DropdownButton(resetRect, ResetLabel, FocusType.Passive))
                    ResetMenu(cfg).DropDown(resetRect);
            }

            if (!string.IsNullOrEmpty(cfg.summary))
                EditorGUILayout.HelpBox(cfg.summary, MessageType.None);
            else if (cfg.stylePrefab != null)
                EditorGUILayout.HelpBox("[Populate from Prefab]을 누르면 요소 목록과 요약이 채워집니다.", MessageType.Info);

            DrawTimeline(cfg);
            EditorGUILayout.Space(4);
            DrawPropertiesExcluding(serializedObject, "m_Script", nameof(TransitionStyleConfig.stylePrefab), nameof(TransitionStyleConfig.useConfig));
            serializedObject.ApplyModifiedProperties();
        }

        GenericMenu ResetMenu(TransitionStyleConfig cfg)
        {
            var menu = new GenericMenu();
            void Item(string label, Action reset, bool repopulate)
            {
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    Undo.RecordObject(cfg, "Reset " + label);
                    reset();
                    if (repopulate) TransitionConfigTools.Populate(cfg, false);
                    EditorUtility.SetDirty(cfg);
                });
            }
            Item("전체", () =>
            {
                cfg.timing = new TimingSettings(); cfg.color = new ColorSettings(); cfg.resources = new ResourceSettings();
                cfg.shape = new ShapeSettings(); cfg.tweens = new TweenSettings(); cfg.particles = new ParticleSettings();
                cfg.elements = new List<ElementOverride>(); cfg.debug = new DebugSettings();
            }, true);
            menu.AddSeparator("");
            Item("Timing", () => cfg.timing = new TimingSettings(), false);
            Item("Color", () => cfg.color = new ColorSettings(), true);
            Item("Resources", () => cfg.resources = new ResourceSettings(), true);
            Item("Shape", () => cfg.shape = new ShapeSettings(), false);
            Item("Tweens", () => cfg.tweens = new TweenSettings(), false);
            Item("Particles", () => cfg.particles = new ParticleSettings(), false);
            Item("Elements", () => cfg.elements = new List<ElementOverride>(), false);
            Item("Debug", () => cfg.debug = new DebugSettings(), false);
            return menu;
        }

        void DrawTimeline(TransitionStyleConfig cfg)
        {
            if (cfg.stylePrefab == null)
                return;
            int dirty = EditorUtility.GetDirtyCount(cfg);
            if (preview == null || dirty != previewDirty)
            {
                preview = TransitionPlan.Build(cfg.stylePrefab, cfg, 0.4f, false);
                previewDirty = dirty;
            }
            var p = preview;
            var r = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
            float total = Mathf.Max(p.end, 0.01f);
            float X(float t) => r.x + r.width * Mathf.Clamp01(t / total);
            EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(X(p.inPhase.start), r.y, X(p.inPhase.End) - X(p.inPhase.start), r.height), new Color(0.25f, 0.55f, 1f));
            EditorGUI.DrawRect(new Rect(X(p.outPhase.start), r.y, X(p.outPhase.End) - X(p.outPhase.start), r.height), new Color(1f, 0.6f, 0.2f));
            EditorGUI.DrawRect(new Rect(X(p.swapTime) - 1, r.y - 2, 2, r.height + 4), Color.red);
            GUI.Label(r, TimelineTip);
            EditorGUILayout.LabelField(
                $"In {p.inPhase.start:0.00}–{p.inPhase.End:0.00}s (×{p.inPhase.speed:0.##})   " +
                $"Out {p.outPhase.start:0.00}–{p.outPhase.End:0.00}s (×{p.outPhase.speed:0.##})   " +
                $"A→B {p.swapTime:0.00}s   총 {p.end:0.00}s", EditorStyles.miniLabel);
        }
    }

    static class DrawerUtil
    {
        public static GUIContent Tip(Type type, string field, string text = "")
        {
            var tip = type.GetField(field)?.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? "";
            return new GUIContent(text, tip);
        }
    }

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    class ReadOnlyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(position, property, label, true);
        }
    }

    /// <summary>A text field plus a ▼ button listing the style prefab's hierarchy (filled by Populate).</summary>
    [CustomPropertyDrawer(typeof(ElementPathAttribute))]
    class ElementPathDrawer : PropertyDrawer
    {
        static readonly GUIContent Button = new GUIContent("▼", "프리팹 계층에서 요소 경로를 고릅니다. 목록이 비어 있으면 [Populate from Prefab]을 먼저 누르세요.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var field = new Rect(position.x, position.y, position.width - 26, position.height);
            var button = new Rect(position.xMax - 24, position.y, 24, position.height);
            EditorGUI.PropertyField(field, property, label);
            if (GUI.Button(button, Button, EditorStyles.miniButton))
                Menu(property).DropDown(button);
        }

        static GenericMenu Menu(SerializedProperty property)
        {
            var so = property.serializedObject;
            var propertyPath = property.propertyPath;
            var cfg = so.targetObject as TransitionStyleConfig;
            var paths = cfg != null ? cfg.elementPaths : new List<string>();
            var menu = new GenericMenu();
            void Set(string value)
            {
                so.Update();
                so.FindProperty(propertyPath).stringValue = value;
                so.ApplyModifiedProperties();
            }
            menu.AddItem(new GUIContent("* (모든 요소)"), property.stringValue == "*", () => Set("*"));
            menu.AddSeparator("");
            if (paths.Count == 0)
                menu.AddDisabledItem(new GUIContent("(Populate from Prefab을 먼저 누르세요)"));
            var parents = new HashSet<string>(paths.Where(p => p.Contains('/')).Select(p => p.Substring(0, p.LastIndexOf('/'))));
            foreach (var path in paths)
            {
                var item = parents.Contains(path) ? $"{path}/= {path.Substring(path.LastIndexOf('/') + 1)}" : path;
                var value = path;
                menu.AddItem(new GUIContent(item), property.stringValue == value, () => Set(value));
            }
            return menu;
        }
    }

    [CustomPropertyDrawer(typeof(PaletteEntry))]
    class PaletteEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            var t = typeof(PaletteEntry);
            float x = r.x, w = (r.width - 18 - 20 - 54) / 2;
            EditorGUI.PropertyField(new Rect(x, r.y, 18, r.height), p.FindPropertyRelative("use"), DrawerUtil.Tip(t, "use")); x += 18;
            EditorGUI.PropertyField(new Rect(x, r.y, w, r.height), p.FindPropertyRelative("from"), DrawerUtil.Tip(t, "from")); x += w;
            EditorGUI.LabelField(new Rect(x, r.y, 20, r.height), new GUIContent(" →")); x += 20;
            EditorGUI.PropertyField(new Rect(x, r.y, w, r.height), p.FindPropertyRelative("to"), DrawerUtil.Tip(t, "to")); x += w;
            EditorGUI.LabelField(new Rect(x + 4, r.y, 50, r.height), DrawerUtil.Tip(t, "usage", $"×{p.FindPropertyRelative("usage").intValue}"), EditorStyles.miniLabel);
        }
    }

    [CustomPropertyDrawer(typeof(SpriteSwap))]
    class SpriteSwapDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            var t = typeof(SpriteSwap);
            float w = (r.width - 20 - 40) / 2, x = r.x;
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.PropertyField(new Rect(x, r.y, w, r.height), p.FindPropertyRelative("original"), GUIContent.none);
            GUI.Label(new Rect(x, r.y, w, r.height), DrawerUtil.Tip(t, "original")); x += w;
            EditorGUI.LabelField(new Rect(x, r.y, 20, r.height), new GUIContent(" →")); x += 20;
            EditorGUI.PropertyField(new Rect(x, r.y, w, r.height), p.FindPropertyRelative("replacement"), DrawerUtil.Tip(t, "replacement")); x += w;
            EditorGUI.LabelField(new Rect(x + 4, r.y, 36, r.height), DrawerUtil.Tip(t, "usage", $"×{p.FindPropertyRelative("usage").intValue}"), EditorStyles.miniLabel);
        }
    }

    [CustomPropertyDrawer(typeof(MaterialProp))]
    class MaterialPropDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
        {
            var t = typeof(MaterialProp);
            var type = (MaterialPropType)p.FindPropertyRelative("type").enumValueIndex;
            var name = p.FindPropertyRelative("name").stringValue;
            var range = p.FindPropertyRelative("range").vector2Value;
            string original;
            switch (type)
            {
                case MaterialPropType.Float: original = p.FindPropertyRelative("originalFloat").floatValue.ToString("0.###"); break;
                case MaterialPropType.Color: original = p.FindPropertyRelative("originalColor").colorValue.ToString(); break;
                case MaterialPropType.Vector: original = p.FindPropertyRelative("originalVector").vector4Value.ToString(); break;
                default: original = "(원본 텍스처)"; break;
            }
            float x = r.x, nameWidth = Mathf.Min(170, r.width * 0.4f);
            EditorGUI.PropertyField(new Rect(x, r.y, 18, r.height), p.FindPropertyRelative("use"), DrawerUtil.Tip(t, "use")); x += 18;
            EditorGUI.LabelField(new Rect(x, r.y, nameWidth, r.height), new GUIContent(name, $"{type} 프로퍼티 {name}\n원본 값: {original}")); x += nameWidth;
            var value = new Rect(x, r.y, r.xMax - x, r.height);
            switch (type)
            {
                case MaterialPropType.Float:
                    var f = p.FindPropertyRelative("floatValue");
                    if (range != Vector2.zero) EditorGUI.Slider(value, f, range.x, range.y, GUIContent.none);
                    else EditorGUI.PropertyField(value, f, GUIContent.none);
                    break;
                case MaterialPropType.Color: EditorGUI.PropertyField(value, p.FindPropertyRelative("colorValue"), GUIContent.none); break;
                case MaterialPropType.Vector:
                    var v = p.FindPropertyRelative("vectorValue");
                    v.vector4Value = EditorGUI.Vector4Field(value, GUIContent.none, v.vector4Value);
                    break;
                default: EditorGUI.PropertyField(value, p.FindPropertyRelative("textureValue"), GUIContent.none); break;
            }
        }
    }
}
