using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace RippedTools
{
    /// <summary>
    /// Plays ripped UI animations without the game's (missing) scripts.
    /// Transition styles run "scene A -> In -> hold -> swap to scene B -> Out" on a TransitionPlan, optionally tuned by a
    /// TransitionStyleConfig from Resources/TransitionConfigs; panels play any clip of any Animator.
    /// Clips are driven through PlayableGraphs, so controller state names do not need to be known.
    /// </summary>
    public class UIAnimViewer : MonoBehaviour
    {
        public List<GameObject> transitionStyles = new List<GameObject>();
        public List<GameObject> panels = new List<GameObject>();
        public RectTransform transitionLayer;
        public RectTransform panelLayer;
        public GameObject sceneA;
        public GameObject sceneB;
        [Tooltip("스타일 설정(TransitionStyleConfig)이 없을 때 In과 Out 사이에 기다리는 시간(초).")]
        public float holdSeconds = 0.4f;
        [Tooltip("켜면 Resources/TransitionConfigs의 스타일별 설정을 적용해서 재생합니다. 끄면 모든 스타일을 원본 그대로 재생합니다.")]
        public bool applyConfigs = true;

        enum Mode { Transitions, Panels }
        Mode mode;
        int styleIndex, panelIndex;
        float speed = 1f;
        bool busy, autoPlay, repeat;
        GameObject current;
        readonly List<PlayableGraph> graphs = new List<PlayableGraph>();
        readonly List<(PlayableGraph graph, AnimationClipPlayable playable)> tracks = new List<(PlayableGraph, AnimationClipPlayable)>();
        Vector2 scroll;
        string status = "";

        TransitionStyleConfig[] configs = new TransitionStyleConfig[0];
        TransitionStyleConfig forcedConfig, playingConfig;
        TransitionConfigRuntime runtime;
        TransitionPlan plan;
        float planTime;
        bool initialized;
        Backdrop backdropA, backdropB;
        static Texture2D white;

        /// <summary>Default look of a scene backdrop, so a config's backdrop override can be undone.</summary>
        class Backdrop
        {
            public Image image;
            public Graphic label;
            public Color color;
            public Sprite sprite;

            public Backdrop(GameObject go)
            {
                image = go.GetComponent<Image>();
                label = go.GetComponentsInChildren<Graphic>(true).FirstOrDefault(g => g.gameObject != go);
                if (image != null) { color = image.color; sprite = image.sprite; }
            }

            public void Apply(BackdropOverride o)
            {
                if (image == null) return;
                bool use = o != null && o.use;
                image.color = use ? o.color : color;
                image.sprite = use ? o.sprite : sprite;
                if (label != null) label.enabled = !use || o.showLabel;
            }
        }

        void Start() => EnsureInit();

        // Also called from PlayStyle, which the editor may invoke before Start has run.
        void EnsureInit()
        {
            if (initialized)
                return;
            initialized = true;
            configs = Resources.LoadAll<TransitionStyleConfig>(TransitionStyleConfig.ResourcesFolder);
            if (sceneA != null) backdropA = new Backdrop(sceneA);
            if (sceneB != null) backdropB = new Backdrop(sceneB);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
            if (Input.GetKeyDown(KeyCode.Space) && mode == Mode.Transitions && !busy) StartCoroutine(PlayTransition());
        }

        void Step(int delta)
        {
            if (mode == Mode.Transitions)
                styleIndex = (styleIndex + delta + transitionStyles.Count) % transitionStyles.Count;
            else
                ShowPanel((panelIndex + delta + panels.Count) % panels.Count);
        }

        /// <summary>The config to use for a style: the one sent by the inspector's Play button, else the first active match.</summary>
        public TransitionStyleConfig ConfigFor(GameObject style)
        {
            if (!applyConfigs)
                return null;
            if (forcedConfig != null && forcedConfig.stylePrefab == style)
                return forcedConfig.useConfig ? forcedConfig : null;
            return configs.FirstOrDefault(c => c != null && c.useConfig && c.stylePrefab == style);
        }

        /// <summary>Plays the config's style right away with that config (used by the config inspector).</summary>
        public void PlayStyle(TransitionStyleConfig cfg)
        {
            int index = cfg != null ? transitionStyles.IndexOf(cfg.stylePrefab) : -1;
            if (index < 0)
            {
                Debug.LogWarning($"[UIAnimViewer] {(cfg != null ? cfg.name : "null")}: style prefab is not in the viewer's list");
                return;
            }
            StopAllCoroutines();
            mode = Mode.Transitions;
            styleIndex = index;
            forcedConfig = cfg;
            StartCoroutine(PlayTransition());
        }

        /// <summary>
        /// Panel prefabs are root canvases in the game, so their RectTransform is saved as 0x0 and only sized by the
        /// Canvas. Nested under the viewer canvas they would collapse; stretch them over the parent instead.
        /// </summary>
        public static void FitToParent(GameObject root)
        {
            if (!(root.transform is RectTransform rt))
                return;
            if (root.GetComponent<Canvas>() == null && rt.sizeDelta != Vector2.zero)
                return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition3D = Vector3.zero;
        }

        /// <summary>Applies all load-time fixes to a freshly instantiated ripped UI prefab.</summary>
        public static void PrepareInstance(GameObject root)
        {
            FitToParent(root);
            PatchMissingCircleSprites(root);
        }

        /// <summary>
        /// An inactive full-screen container for one transition instance. Instantiating under it delays every Awake
        /// until it is activated, which gives a config the chance to edit DOTweenAnimation fields first.
        /// </summary>
        public static GameObject CreateHolder(Transform parent)
        {
            var holder = new GameObject("Transition", typeof(RectTransform));
            holder.SetActive(false);
            var rt = (RectTransform)holder.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return holder;
        }

        static Sprite circleSprite;

        /// <summary>
        /// Some circles referenced a shared sprite from a bundle that was never downloaded, so they rip as sprite-less
        /// squares. Images named "*circle*" with no sprite and the default material get a generated disc instead.
        /// </summary>
        public static void PatchMissingCircleSprites(GameObject root)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.sprite != null || img.material != img.defaultMaterial || img.name.IndexOf("circle", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (circleSprite == null)
                {
                    const int size = 256;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Generated Circle", hideFlags = HideFlags.DontSave };
                    var pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude;
                            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(size / 2f - d) * 255));
                        }
                    tex.SetPixels32(pixels);
                    tex.Apply();
                    circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                    circleSprite.hideFlags = HideFlags.DontSave;
                }
                img.sprite = circleSprite;
            }
        }

        public static List<(Animator animator, AnimationClip clip)> ClipsOf(GameObject root)
        {
            var result = new List<(Animator, AnimationClip)>();
            foreach (var a in root.GetComponentsInChildren<Animator>(true))
                if (a.runtimeAnimatorController != null)
                    foreach (var c in a.runtimeAnimatorController.animationClips.Distinct())
                        result.Add((a, c));
            return result;
        }

        // Transition clips are named like "style10_in" / "style10_out".
        public static bool IsIn(AnimationClip c) => System.Text.RegularExpressions.Regex.IsMatch(c.name, @"(^|_)in(\d*|_\w*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        public static bool IsOut(AnimationClip c) => System.Text.RegularExpressions.Regex.IsMatch(c.name, @"(^|_)out(\d*|_\w*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        IEnumerator PlayTransition()
        {
            EnsureInit();
            busy = true;
            Clear();
            var style = transitionStyles[styleIndex];
            var cfg = ConfigFor(style);
            forcedConfig = null;
            playingConfig = cfg;

            var holder = CreateHolder(transitionLayer);
            current = holder;
            var instance = Instantiate(style, holder.transform, false);
            PrepareInstance(instance);
            instance.SetActive(true);
            runtime = cfg != null ? TransitionConfigRuntime.Attach(holder, instance.transform, cfg, speed) : null;
            plan = TransitionPlan.Build(instance, cfg, holdSeconds);
            backdropA?.Apply(cfg != null ? cfg.resources.sceneA : null);
            backdropB?.Apply(cfg != null ? cfg.resources.sceneB : null);
            sceneA.SetActive(true);
            sceneB.SetActive(false);
            holder.SetActive(true);
            status = $"{style.name}{(cfg != null ? $" [config: {cfg.name}]" : "")}\n" +
                     $"in={string.Join(",", plan.inPhase.tracks.Select(x => x.clip.name))} out={string.Join(",", plan.outPhase.tracks.Select(x => x.clip.name))}";

            planTime = 0f;
            TransitionPlan.Phase shown = null;
            while (true)
            {
                if (planTime >= plan.swapTime && !sceneB.activeSelf)
                {
                    sceneA.SetActive(false);
                    sceneB.SetActive(true);
                }
                var phase = plan.ActiveAt(planTime);
                if (phase != shown)
                {
                    StopTracks();
                    foreach (var (animator, clip) in phase.tracks)
                        tracks.Add(CreateTrack(animator, clip));
                    shown = phase;
                }
                float local = phase.Local(planTime);
                foreach (var t in tracks)
                    t.playable.SetTime(local);
                if (planTime >= plan.end)
                    break;
                yield return null;
                planTime += Time.deltaTime * speed;
            }
            yield return null; // let the last pose render once
            Clear();
            busy = false;
            if (repeat || autoPlay)
            {
                if (!repeat)
                    styleIndex = (styleIndex + 1) % transitionStyles.Count;
                yield return new WaitForSeconds(0.3f);
                StartCoroutine(PlayTransition());
            }
        }

        /// <summary>A graph frozen at speed 0 whose time the transition coroutine sets every frame.</summary>
        (PlayableGraph, AnimationClipPlayable) CreateTrack(Animator animator, AnimationClip clip)
        {
            animator.enabled = true;
            var graph = PlayableGraph.Create(clip.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetSpeed(0);
            playable.SetDuration(double.MaxValue);
            AnimationPlayableOutput.Create(graph, "out", animator).SetSourcePlayable(playable);
            graph.Play();
            return (graph, playable);
        }

        PlayableGraph PlayClip(Animator animator, AnimationClip clip)
        {
            animator.enabled = true;
            var graph = PlayableGraph.Create(clip.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetSpeed(speed);
            playable.SetDuration(clip.isLooping ? double.MaxValue : clip.length);
            AnimationPlayableOutput.Create(graph, "out", animator).SetSourcePlayable(playable);
            graph.Play();
            return graph;
        }

        void ShowPanel(int index)
        {
            StopAllCoroutines();
            busy = false;
            Clear();
            panelIndex = index;
            sceneA.SetActive(true);
            sceneB.SetActive(false);
            current = Instantiate(panels[index], panelLayer, false);
            PrepareInstance(current);
            current.SetActive(true);
            status = panels[index].name;
        }

        void StopGraphs()
        {
            foreach (var g in graphs)
                if (g.IsValid()) g.Destroy();
            graphs.Clear();
        }

        void StopTracks()
        {
            foreach (var t in tracks)
                if (t.graph.IsValid()) t.graph.Destroy();
            tracks.Clear();
        }

        void Clear()
        {
            StopGraphs();
            StopTracks();
            if (current != null) Destroy(current);
            current = null;
            runtime = null;
            plan = null;
            playingConfig = null;
            backdropA?.Apply(null);
            backdropB?.Apply(null);
        }

        void OnDestroy()
        {
            StopGraphs();
            StopTracks();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(mode == Mode.Transitions, "Transitions", "Button")) mode = Mode.Transitions;
            if (GUILayout.Toggle(mode == Mode.Panels, "UI Panels", "Button") && mode != Mode.Panels) { mode = Mode.Panels; ShowPanel(panelIndex); }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Speed {speed:0.00}x");
            speed = GUILayout.HorizontalSlider(speed, 0.1f, 2f);
            GUILayout.Space(6);

            if (mode == Mode.Transitions && transitionStyles.Count > 0)
            {
                var cfg = ConfigFor(transitionStyles[styleIndex]);
                GUILayout.Label($"Style {styleIndex + 1}/{transitionStyles.Count}: {transitionStyles[styleIndex].name}");
                GUILayout.Label(cfg != null ? $"Config: {cfg.name}" : "Config: (none, original)");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("< Prev")) Step(-1);
                GUI.enabled = !busy;
                if (GUILayout.Button("Play (Space)")) StartCoroutine(PlayTransition());
                GUI.enabled = true;
                if (GUILayout.Button("Next >")) Step(1);
                GUILayout.EndHorizontal();
                autoPlay = GUILayout.Toggle(autoPlay, "Auto play all styles");
                repeat = GUILayout.Toggle(repeat, "Repeat current style");
                applyConfigs = GUILayout.Toggle(applyConfigs, "Apply style configs");
                scroll = GUILayout.BeginScrollView(scroll);
                for (int i = 0; i < transitionStyles.Count; i++)
                    if (GUILayout.Button((i == styleIndex ? "> " : "") + transitionStyles[i].name)) styleIndex = i;
                GUILayout.EndScrollView();
            }
            else if (mode == Mode.Panels && panels.Count > 0)
            {
                GUILayout.Label($"Panel {panelIndex + 1}/{panels.Count}: {panels[panelIndex].name}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("< Prev")) Step(-1);
                if (GUILayout.Button("Next >")) Step(1);
                GUILayout.EndHorizontal();
                scroll = GUILayout.BeginScrollView(scroll);
                if (current != null)
                    foreach (var (animator, clip) in ClipsOf(current))
                        if (GUILayout.Button($"{animator.name} / {clip.name} ({clip.length:0.00}s)"))
                        {
                            StopGraphs();
                            graphs.Add(PlayClip(animator, clip));
                        }
                GUILayout.Space(8);
                for (int i = 0; i < panels.Count; i++)
                    if (GUILayout.Button((i == panelIndex ? "> " : "") + panels[i].name)) ShowPanel(i);
                GUILayout.EndScrollView();
            }
            GUILayout.Label(status);
            GUILayout.EndArea();

            if (plan != null && (playingConfig == null || playingConfig.debug.showTimeline))
                DrawTimeline(new Rect(380, Screen.height - 46, Screen.width - 400, 18));
            if (runtime != null && playingConfig != null && playingConfig.debug.showBounds)
                DrawBounds(playingConfig.debug);
        }

        static void Fill(Rect r, Color c)
        {
            if (white == null) white = Texture2D.whiteTexture;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void DrawTimeline(Rect r)
        {
            float total = Mathf.Max(plan.end, 0.01f);
            float X(float t) => r.x + r.width * Mathf.Clamp01(t / total);
            Fill(r, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(X(plan.inPhase.start), r.y, X(plan.inPhase.End) - X(plan.inPhase.start), r.height), new Color(0.25f, 0.55f, 1f, 0.9f));
            Fill(new Rect(X(plan.outPhase.start), r.y, X(plan.outPhase.End) - X(plan.outPhase.start), r.height), new Color(1f, 0.6f, 0.2f, 0.9f));
            Fill(new Rect(X(plan.swapTime) - 1, r.y - 4, 2, r.height + 8), Color.red);
            Fill(new Rect(X(planTime) - 1, r.y - 6, 3, r.height + 12), Color.white);
            GUI.Label(new Rect(r.x, r.yMax + 2, r.width, 20),
                $"t={planTime:0.00}s / {plan.end:0.00}s   In {plan.inPhase.start:0.00}-{plan.inPhase.End:0.00}   Out {plan.outPhase.start:0.00}-{plan.outPhase.End:0.00}   A→B {plan.swapTime:0.00}");
        }

        void DrawBounds(DebugSettings dbg)
        {
            var canvas = transitionLayer.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            foreach (var (path, t) in runtime.Elements)
            {
                if (!(t is RectTransform rt) || !t.gameObject.activeInHierarchy || !ElementPathMatcher.Matches(dbg.boundsPath, false, path))
                    continue;
                rt.GetWorldCorners(corners);
                float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
                foreach (var c in corners)
                {
                    var s = RectTransformUtility.WorldToScreenPoint(cam, c);
                    xMin = Mathf.Min(xMin, s.x); xMax = Mathf.Max(xMax, s.x);
                    yMin = Mathf.Min(yMin, Screen.height - s.y); yMax = Mathf.Max(yMax, Screen.height - s.y);
                }
                var rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                Fill(new Rect(rect.x, rect.y, rect.width, 1), dbg.boundsColor);
                Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), dbg.boundsColor);
                Fill(new Rect(rect.x, rect.y, 1, rect.height), dbg.boundsColor);
                Fill(new Rect(rect.xMax - 1, rect.y, 1, rect.height), dbg.boundsColor);
                var old = GUI.contentColor;
                GUI.contentColor = dbg.boundsColor;
                GUI.Label(new Rect(rect.x + 2, rect.y + 1, 300, 20), t.name);
                GUI.contentColor = old;
            }
        }
    }
}
