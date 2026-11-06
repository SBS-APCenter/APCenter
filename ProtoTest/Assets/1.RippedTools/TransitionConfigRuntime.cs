using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace RippedTools
{
    /// <summary>
    /// Applies a TransitionStyleConfig to one instantiated transition style.
    /// Setup-time changes (materials, tweens, particles) happen in Attach while the holder is still inactive, so
    /// DOTweenAnimation picks them up when it builds its tween. Per-frame changes (colors, sprites, visibility,
    /// element transforms) are written in LateUpdate on top of what the Animator and DOTween produced, and undone at
    /// the start of the next frame so the animation never reads back its own modified output.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class TransitionConfigRuntime : MonoBehaviour
    {
        public TransitionStyleConfig config;
        public Transform root;

        readonly List<Graphic> graphics = new List<Graphic>();
        readonly List<ParticleSystemRenderer> particleRenderers = new List<ParticleSystemRenderer>();
        readonly List<Transform> transforms = new List<Transform>();
        readonly Dictionary<Transform, string> paths = new Dictionary<Transform, string>();
        readonly Dictionary<Material, Material> materialClones = new Dictionary<Material, Material>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Action> undo = new List<Action>();
        readonly List<ElementOverride> matches = new List<ElementOverride>();
        int materialHash;

        public IEnumerable<(string path, Transform transform)> Elements => transforms.Where(t => t != null).Select(t => (paths[t], t));

        public static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent)
                parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Call while the holder is inactive so no DOTweenAnimation has built its tween yet.</summary>
        public static TransitionConfigRuntime Attach(GameObject holder, Transform root, TransitionStyleConfig cfg, float globalSpeed)
        {
            var runtime = holder.AddComponent<TransitionConfigRuntime>();
            runtime.config = cfg;
            runtime.root = root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                runtime.transforms.Add(t);
                runtime.paths[t] = PathOf(t, root);
            }
            runtime.graphics.AddRange(root.GetComponentsInChildren<Graphic>(true));
            runtime.particleRenderers.AddRange(root.GetComponentsInChildren<ParticleSystemRenderer>(true));
            runtime.SetupMaterials();
            runtime.SetupTweens(globalSpeed);
            runtime.SetupParticles(globalSpeed);
            return runtime;
        }

        void Update() => Restore();
        void LateUpdate() => Apply();
        void OnDestroy() => Cleanup();

        /// <summary>Destroys the material copies made for this instance. Safe to call more than once.</summary>
        public void Cleanup()
        {
            foreach (var m in ownedMaterials)
                if (m != null)
                {
                    if (Application.isPlaying) Destroy(m);
                    else DestroyImmediate(m);
                }
            ownedMaterials.Clear();
            materialClones.Clear();
        }

        // ── setup-time ──

        void SetupMaterials()
        {
            foreach (var mo in config.resources.materials)
                if (mo.material != null && !materialClones.ContainsKey(mo.material))
                {
                    var clone = new Material(mo.material) { name = mo.material.name + " (config)", hideFlags = HideFlags.DontSave };
                    materialClones[mo.material] = clone;
                    ownedMaterials.Add(clone);
                }
            if (materialClones.Count == 0)
                return;
            foreach (var g in graphics)
                if (g != null && g.material != null && materialClones.TryGetValue(g.material, out var clone))
                    g.material = clone;
            materialHash = 0;
            UpdateMaterials();
        }

        void SetupTweens(float globalSpeed)
        {
            var settings = config.tweens;
            foreach (var tw in root.GetComponentsInChildren<DOTweenAnimation>(true))
            {
                if (!settings.enabled)
                {
                    tw.isActive = false;
                    continue;
                }
                var path = paths.TryGetValue(tw.transform, out var p) ? p : PathOf(tw.transform, root);
                float speed = settings.speed * Mathf.Max(globalSpeed, 0.01f);
                foreach (var e in config.elements)
                    if (e.Matches(path))
                        speed *= e.speed;
                float delayAdd = 0f;
                foreach (var rule in settings.rules)
                {
                    if (!ElementPathMatcher.Matches(rule.path, rule.includeChildren, path) || !KindMatches(rule.kind, tw))
                        continue;
                    if (!rule.enabled)
                        tw.isActive = false;
                    speed *= rule.speed;
                    delayAdd += rule.delayOffset;
                    if (rule.overrideEase)
                    {
                        tw.easeType = rule.ease;
                        if (rule.ease == Ease.INTERNAL_Custom && rule.customCurve != null)
                            tw.easeCurve = new AnimationCurve(rule.customCurve.keys);
                    }
                }
                speed = Mathf.Max(speed, 0.01f);
                tw.duration /= speed;
                tw.delay = Mathf.Max(0f, tw.delay / speed + delayAdd);
            }
        }

        public static TweenKind KindOf(DOTweenAnimation tw)
        {
            var type = tw.animationType.ToString();
            if ((type == "Rotate" || type == "LocalRotate") && tw.loops == -1) return TweenKind.LoopingRotation;
            switch (type)
            {
                case "Move": case "LocalMove": case "PunchPosition": case "ShakePosition": return TweenKind.Move;
                case "Rotate": case "LocalRotate": case "PunchRotation": case "ShakeRotation": return TweenKind.Rotate;
                case "Scale": case "PunchScale": case "ShakeScale": case "UIWidthHeight": return TweenKind.Scale;
                case "Color": case "Fade": return TweenKind.ColorFade;
                default: return TweenKind.Other;
            }
        }

        static bool KindMatches(TweenKind kind, DOTweenAnimation tw)
        {
            if (kind == TweenKind.All) return true;
            var actual = KindOf(tw);
            return actual == kind || (kind == TweenKind.Rotate && actual == TweenKind.LoopingRotation);
        }

        void SetupParticles(float globalSpeed)
        {
            var settings = config.particles;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.simulationSpeed *= Mathf.Max(settings.simulationSpeed, 0.01f) * Mathf.Max(globalSpeed, 0.01f);
                if (settings.startColorTint != Color.white)
                    main.startColor = Tint(main.startColor, settings.startColorTint);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (settings.texture != null && r != null && r.sharedMaterial != null)
                {
                    var m = new Material(r.sharedMaterial) { name = r.sharedMaterial.name + " (config)", hideFlags = HideFlags.DontSave };
                    foreach (var prop in new[] { "_MainTex", "_BaseMap" })
                        if (m.HasProperty(prop)) m.SetTexture(prop, settings.texture);
                    r.sharedMaterial = m;
                    ownedMaterials.Add(m);
                }
            }
        }

        static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g, Color t)
        {
            Gradient TintGradient(Gradient src) => src == null ? null : new Gradient
            {
                mode = src.mode,
                colorKeys = src.colorKeys.Select(k => new GradientColorKey(k.color * t, k.time)).ToArray(),
                alphaKeys = src.alphaKeys.Select(k => new GradientAlphaKey(k.alpha * t.a, k.time)).ToArray(),
            };
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color: g.color *= t; break;
                case ParticleSystemGradientMode.TwoColors: g.colorMin *= t; g.colorMax *= t; break;
                case ParticleSystemGradientMode.TwoGradients: g.gradientMin = TintGradient(g.gradientMin); g.gradientMax = TintGradient(g.gradientMax); break;
                default: g.gradient = TintGradient(g.gradient); break;
            }
            return g;
        }

        // ── per-frame ──

        /// <summary>Writes the per-frame overrides. Pair every call with Restore (done automatically in play mode).</summary>
        public void Apply()
        {
            Restore();
            if (config == null || root == null)
                return;
            ApplyShape();
            UpdateMaterials();

            foreach (var g in graphics)
            {
                if (g == null || !g.gameObject.activeInHierarchy)
                    continue;
                Collect(paths[g.transform]);
                if (matches.Exists(e => !e.visible))
                {
                    var cr = g.canvasRenderer;
                    float alpha = cr.GetAlpha();
                    if (alpha != 0f)
                    {
                        cr.SetAlpha(0f);
                        undo.Add(() => { if (cr != null) cr.SetAlpha(alpha); });
                    }
                    continue;
                }
                var c0 = g.color;
                var c1 = ColorOf(c0, IsText(g));
                if (c1 != c0)
                {
                    g.color = c1;
                    undo.Add(() => { if (g != null) g.color = c0; });
                }
                if (g is Image img)
                {
                    var s0 = img.sprite;
                    var s1 = SpriteFor(s0);
                    if (s1 != s0)
                    {
                        img.sprite = s1;
                        undo.Add(() => { if (img != null) img.sprite = s0; });
                    }
                }
            }

            foreach (var r in particleRenderers)
            {
                if (r == null || !r.enabled)
                    continue;
                Collect(paths[r.transform]);
                if (!config.particles.enabled || matches.Exists(e => !e.visible))
                {
                    r.enabled = false;
                    undo.Add(() => { if (r != null) r.enabled = true; });
                }
            }

            foreach (var t in transforms)
            {
                if (t == null || !t.gameObject.activeInHierarchy)
                    continue;
                Collect(paths[t]);
                var offset = Vector2.zero;
                var scale = Vector2.one;
                float rotation = 0f;
                bool any = false;
                foreach (var e in matches)
                    if (e.HasTransform)
                    {
                        offset += e.offset;
                        scale = Vector2.Scale(scale, e.scale);
                        rotation += e.rotation;
                        any = true;
                    }
                if (!any)
                    continue;
                var p0 = t is RectTransform rt0 ? (Vector3)rt0.anchoredPosition : t.localPosition;
                var s0 = t.localScale;
                var r0 = t.localRotation;
                if (t is RectTransform rt) rt.anchoredPosition += offset;
                else t.localPosition += (Vector3)offset;
                t.localScale = new Vector3(s0.x * scale.x, s0.y * scale.y, s0.z);
                t.localRotation = r0 * Quaternion.Euler(0f, 0f, rotation);
                undo.Add(() =>
                {
                    if (t == null) return;
                    if (t is RectTransform rtu) rtu.anchoredPosition = p0;
                    else t.localPosition = p0;
                    t.localScale = s0;
                    t.localRotation = r0;
                });
            }
        }

        /// <summary>Puts back every value Apply changed, newest first.</summary>
        public void Restore()
        {
            for (int i = undo.Count - 1; i >= 0; i--)
                undo[i]();
            undo.Clear();
        }

        void Collect(string path)
        {
            matches.Clear();
            foreach (var e in config.elements)
                if (e.Matches(path))
                    matches.Add(e);
        }

        static bool IsText(Graphic g) => g is Text || g.GetType().Name.IndexOf("TextMeshPro", StringComparison.Ordinal) >= 0;

        void ApplyShape()
        {
            if (!(transform is RectTransform holder))
                return;
            var s = config.shape;
            holder.anchoredPosition = s.offset;
            holder.localScale = new Vector3(s.scale * (s.flipX ? -1f : 1f), s.scale * (s.flipY ? -1f : 1f), 1f);
            holder.localRotation = Quaternion.Euler(0f, 0f, s.rotation);
        }

        Color ColorOf(Color c, bool isText)
        {
            var s = config.color;
            bool global = !isText || s.includeText;
            if (global)
            {
                foreach (var p in s.palette)
                    if (p.use && Mathf.Abs(c.r - p.from.r) <= s.paletteTolerance && Mathf.Abs(c.g - p.from.g) <= s.paletteTolerance && Mathf.Abs(c.b - p.from.b) <= s.paletteTolerance)
                    {
                        c = new Color(p.to.r, p.to.g, p.to.b, c.a);
                        break;
                    }
                if (s.hueShift != 0f || s.saturation != 1f || s.brightness != 1f)
                {
                    Color.RGBToHSV(c, out var h, out var sat, out var v);
                    h = Mathf.Repeat(h + s.hueShift / 360f, 1f);
                    var rgb = Color.HSVToRGB(h, Mathf.Clamp01(sat * s.saturation), v * s.brightness, false);
                    c = new Color(Mathf.Clamp01(rgb.r), Mathf.Clamp01(rgb.g), Mathf.Clamp01(rgb.b), c.a);
                }
                c *= s.tint;
            }
            foreach (var e in matches)
                switch (e.colorMode)
                {
                    case ElementColorMode.Multiply: c *= e.color; break;
                    case ElementColorMode.ReplaceRGB: c = new Color(e.color.r, e.color.g, e.color.b, c.a); break;
                    case ElementColorMode.ReplaceRGBA: c = e.color; break;
                }
            if (global)
                c.a = Mathf.Clamp01(c.a * s.alphaScale);
            return c;
        }

        Sprite SpriteFor(Sprite s)
        {
            for (int i = matches.Count - 1; i >= 0; i--)
                if (matches[i].sprite != null)
                    return matches[i].sprite;
            if (s != null)
                foreach (var swap in config.resources.spriteSwaps)
                    if (swap.original == s && swap.replacement != null)
                        return swap.replacement;
            return s;
        }

        /// <summary>Re-copies the original properties into each clone and applies the enabled overrides, only when they changed.</summary>
        void UpdateMaterials()
        {
            if (materialClones.Count == 0)
                return;
            int hash = 17;
            unchecked
            {
                foreach (var mo in config.resources.materials)
                    foreach (var p in mo.props)
                        if (p.use)
                            hash = hash * 31 + (p.name ?? "").GetHashCode() * 7 + p.floatValue.GetHashCode() + p.colorValue.GetHashCode()
                                   + p.vectorValue.GetHashCode() + (p.textureValue != null ? p.textureValue.GetInstanceID() : 0);
            }
            if (hash == materialHash)
                return;
            materialHash = hash;
            foreach (var mo in config.resources.materials)
            {
                if (mo.material == null || !materialClones.TryGetValue(mo.material, out var clone))
                    continue;
                clone.CopyPropertiesFromMaterial(mo.material);
                foreach (var p in mo.props)
                {
                    if (!p.use || string.IsNullOrEmpty(p.name) || !clone.HasProperty(p.name))
                        continue;
                    switch (p.type)
                    {
                        case MaterialPropType.Float: clone.SetFloat(p.name, p.floatValue); break;
                        case MaterialPropType.Color: clone.SetColor(p.name, p.colorValue); break;
                        case MaterialPropType.Vector: clone.SetVector(p.name, p.vectorValue); break;
                        case MaterialPropType.Texture: if (p.textureValue != null) clone.SetTexture(p.name, p.textureValue); break;
                    }
                }
            }
            // Graphics with a material modifier (Mask, UIMaterialAnimationHandler) render a copy of the clone; rebuild it.
            foreach (var g in graphics)
                if (g != null && g.material != null && ownedMaterials.Contains(g.material))
                    g.SetMaterialDirty();
        }
    }

    /// <summary>
    /// The timeline of one transition: Pre → In → Hold → Out → Post, plus the Scene A→B swap time.
    /// Times are in content seconds; the viewer advances them by deltaTime × Speed slider.
    /// </summary>
    public class TransitionPlan
    {
        public class Phase
        {
            public readonly List<(Animator animator, AnimationClip clip)> tracks = new List<(Animator, AnimationClip)>();
            public bool play = true;
            public float start, from, to, speed = 1f;

            public float Length => play && tracks.Count > 0 ? (to - from) / speed : 0f;
            public float End => start + Length;
            public float Local(float t) => play ? Mathf.Clamp(from + (t - start) * speed, from, to) : to;
        }

        public readonly Phase inPhase = new Phase(), outPhase = new Phase();
        public float swapTime, end;

        public Phase ActiveAt(float t) => outPhase.tracks.Count > 0 && outPhase.play && t >= outPhase.start ? outPhase : inPhase;

        /// <param name="root">An instance (or the prefab asset itself for previews).</param>
        /// <param name="retimeClips">Build retimed clip copies for element timing; false only measures the lengths.</param>
        public static TransitionPlan Build(GameObject root, TransitionStyleConfig cfg, float defaultHold, bool retimeClips = true)
        {
            var timing = cfg != null ? cfg.timing : new TimingSettings { hold = defaultHold };
            var clips = UIAnimViewer.ClipsOf(root);
            var ins = clips.Where(c => UIAnimViewer.IsIn(c.clip)).ToList();
            var outs = clips.Where(c => UIAnimViewer.IsOut(c.clip)).ToList();
            if (ins.Count == 0)
            {
                ins = clips;
                outs = new List<(Animator, AnimationClip)>();
            }
            var plan = new TransitionPlan();
            Fill(plan.inPhase, ins, timing.inPhase, root.transform, cfg, retimeClips);
            Fill(plan.outPhase, outs, timing.outPhase, root.transform, cfg, retimeClips);
            plan.inPhase.start = timing.preDelay;
            plan.outPhase.start = plan.inPhase.End + timing.hold;
            float outEnd = plan.outPhase.End;
            plan.swapTime = Mathf.Clamp(plan.outPhase.start + timing.swapOffset, 0f, outEnd);
            plan.end = outEnd + timing.postHold;
            return plan;
        }

        static void Fill(Phase phase, List<(Animator animator, AnimationClip clip)> source, PhaseTiming timing, Transform root,
                         TransitionStyleConfig cfg, bool retimeClips)
        {
            bool retime = cfg != null && cfg.HasElementTiming();
            float maxLength = 0f;
            foreach (var (animator, clip) in source)
            {
                var prefix = TransitionConfigRuntime.PathOf(animator.transform, root);
                float length = clip.length;
                var used = clip;
                if (retime && retimeClips)
                {
                    used = ClipRetimer.Retime(clip, prefix, cfg.elements);
                    length = used.length;
                }
                else if (retime)
                    length = ClipRetimer.RetimedLength(clip, prefix, cfg.elements);
                phase.tracks.Add((animator, used));
                maxLength = Mathf.Max(maxLength, length);
            }
            phase.play = timing.play;
            phase.from = Mathf.Clamp(timing.trimStart, 0f, maxLength);
            phase.to = timing.trimEnd > phase.from ? Mathf.Min(timing.trimEnd, maxLength) : maxLength;
            float span = phase.to - phase.from;
            phase.speed = Mathf.Max(timing.targetDuration > 0f && span > 0f ? span / timing.targetDuration : timing.speed, 0.01f);
        }
    }

    /// <summary>
    /// Shifts and scales the keys of the bindings that belong to matching elements, producing a new in-memory clip.
    /// Clip curves are only readable through UnityEditor.AnimationUtility, so outside the editor this is a no-op.
    /// </summary>
    public static class ClipRetimer
    {
        static (float delay, float speed, bool hit) TimingFor(string path, List<ElementOverride> elements)
        {
            float delay = 0f, speed = 1f;
            bool hit = false;
            foreach (var e in elements)
                if (e.Matches(path) && (e.delay != 0f || !Mathf.Approximately(e.speed, 1f)))
                {
                    delay += e.delay;
                    speed *= Mathf.Max(e.speed, 0.01f);
                    hit = true;
                }
            return (delay, speed, hit);
        }

        static string Join(string prefix, string bindingPath) =>
            string.IsNullOrEmpty(prefix) ? bindingPath : string.IsNullOrEmpty(bindingPath) ? prefix : prefix + "/" + bindingPath;

        public static AnimationClip Retime(AnimationClip src, string animatorPath, List<ElementOverride> elements)
        {
#if UNITY_EDITOR
            var dst = new AnimationClip { name = src.name, frameRate = src.frameRate, hideFlags = HideFlags.DontSave };
            UnityEditor.AnimationUtility.SetAnimationClipSettings(dst, UnityEditor.AnimationUtility.GetAnimationClipSettings(src));

            var bindings = UnityEditor.AnimationUtility.GetCurveBindings(src);
            var curves = new AnimationCurve[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                var curve = UnityEditor.AnimationUtility.GetEditorCurve(src, bindings[i]);
                var (delay, speed, hit) = TimingFor(Join(animatorPath, bindings[i].path), elements);
                curves[i] = hit ? Shift(curve, delay, speed) : curve;
            }
            UnityEditor.AnimationUtility.SetEditorCurves(dst, bindings, curves);

            var refBindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(src);
            var refCurves = new UnityEditor.ObjectReferenceKeyframe[refBindings.Length][];
            for (int i = 0; i < refBindings.Length; i++)
            {
                var keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(src, refBindings[i]);
                var (delay, speed, hit) = TimingFor(Join(animatorPath, refBindings[i].path), elements);
                if (hit)
                    for (int k = 0; k < keys.Length; k++)
                        keys[k].time = Mathf.Max(0f, delay + keys[k].time / speed);
                refCurves[i] = keys;
            }
            UnityEditor.AnimationUtility.SetObjectReferenceCurves(dst, refBindings, refCurves);
            return dst;
#else
            return src;
#endif
        }

        /// <summary>Length the retimed clip would have, without building it (for inspector previews).</summary>
        public static float RetimedLength(AnimationClip src, string animatorPath, List<ElementOverride> elements)
        {
#if UNITY_EDITOR
            float length = src.length;
            foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(src))
            {
                var (delay, speed, hit) = TimingFor(Join(animatorPath, b.path), elements);
                if (!hit) continue;
                var curve = UnityEditor.AnimationUtility.GetEditorCurve(src, b);
                if (curve != null && curve.length > 0)
                    length = Mathf.Max(length, delay + curve.keys[curve.length - 1].time / speed);
            }
            foreach (var b in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(src))
            {
                var (delay, speed, hit) = TimingFor(Join(animatorPath, b.path), elements);
                if (!hit) continue;
                var keys = UnityEditor.AnimationUtility.GetObjectReferenceCurve(src, b);
                if (keys.Length > 0)
                    length = Mathf.Max(length, delay + keys[keys.Length - 1].time / speed);
            }
            return length;
#else
            return src.length;
#endif
        }

        static AnimationCurve Shift(AnimationCurve curve, float delay, float speed)
        {
            var keys = curve.keys;
            for (int k = 0; k < keys.Length; k++)
            {
                keys[k].time = Mathf.Max(0f, delay + keys[k].time / speed);
                keys[k].inTangent *= speed;
                keys[k].outTangent *= speed;
            }
            return new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
        }
    }
}
