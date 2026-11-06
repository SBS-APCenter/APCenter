using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine;

namespace RippedTools
{
    /// <summary>
    /// Per-style overrides for one transition prefab. Every value defaults to "same as the original", so a freshly
    /// created config plays the style unchanged. Configs under Resources/TransitionConfigs are picked up by UIAnimViewer.
    /// </summary>
    [CreateAssetMenu(menuName = "Ripped/Transition Style Config", fileName = "Style_Config")]
    public class TransitionStyleConfig : ScriptableObject
    {
        public const string ResourcesFolder = "TransitionConfigs";

        [Tooltip("이 설정을 적용할 전환 프리팹 (Assets/assetbundles/ui/transitionex/Style_N).\n" +
                 "뷰어는 재생할 프리팹과 이 필드가 같은 설정을 찾아 적용합니다.\n" +
                 "바꾼 뒤에는 [Populate from Prefab]을 눌러 요소 목록·색·스프라이트·머티리얼 목록을 다시 채우세요.")]
        public GameObject stylePrefab;

        [Tooltip("끄면 이 설정을 무시하고 원본 그대로 재생합니다.\n" +
                 "설정 값은 지워지지 않으므로, 켜고 끄며 원본과 비교할 때 씁니다.")]
        public bool useConfig = true;

        [Tooltip("자유 메모. 재생에는 영향이 없습니다. 분석 내용이나 실험 의도를 적어 두세요.")]
        [TextArea(2, 6)]
        public string notes = "";

        [Tooltip("In / Hold / Out 단계의 배속·길이·구간과 Scene A→B 교체 시점.\n" +
                 "Animator 클립에만 적용되며 DOTween은 [Tweens]에서 조절합니다.")]
        public TimingSettings timing = new TimingSettings();

        [Tooltip("스타일 전체의 색 보정: 팔레트 교체 → 색상/채도/명도 → Tint → 요소별 색 → 알파 배율 순서로 적용됩니다.\n" +
                 "애니메이션이 매 프레임 쓰는 색 위에 덧씌우므로 페이드 같은 연출 모양은 유지됩니다.")]
        public ColorSettings color = new ColorSettings();

        [Tooltip("스프라이트 교체, 머티리얼(셰이더) 프로퍼티 덮어쓰기, 전환 앞뒤 장면(Scene A/B) 배경 교체.")]
        public ResourceSettings resources = new ResourceSettings();

        [Tooltip("스타일 전체의 위치·크기·회전·반전. 요소 하나만 바꾸려면 [Elements]를 쓰세요.")]
        public ShapeSettings shape = new ShapeSettings();

        [Tooltip("프리팹 안 DOTweenAnimation(장식 링 회전, 반복 이동 등)의 배속·지연·이징을 덮어씁니다.\n" +
                 "재생을 시작할 때 한 번 적용되므로, Play 중 값을 바꾸면 다음 재생부터 반영됩니다.")]
        public TweenSettings tweens = new TweenSettings();

        [Tooltip("프리팹 안 ParticleSystem의 표시·속도·색·텍스처.\n" +
                 "참고: 원본의 UIParticle(Coffee) 스크립트가 없어서 파티클이 원본과 다르게 그려질 수 있습니다.")]
        public ParticleSettings particles = new ParticleSettings();

        [Tooltip("특정 요소(계층 경로)만 골라 표시/타이밍/색/스프라이트/위치를 바꿉니다.\n" +
                 "한 요소에 여러 항목이 일치하면 모두 적용됩니다(지연은 더하고, 배속·크기는 곱함).\n" +
                 "경로 칸 오른쪽 [▼]를 누르면 프리팹 계층에서 고를 수 있습니다.")]
        public List<ElementOverride> elements = new List<ElementOverride>();

        [Tooltip("뷰어 화면에 타임라인 막대와 요소 테두리를 그려 분석을 돕는 표시 옵션. 연출 자체는 바뀌지 않습니다.")]
        public DebugSettings debug = new DebugSettings();

        // Filled by "Populate from Prefab"; used by the element path dropdown and the inspector summary.
        [HideInInspector] public List<string> elementPaths = new List<string>();
        [HideInInspector] public string summary = "";

        public bool HasElementTiming()
        {
            foreach (var e in elements)
                if (e.IsSet && (e.delay != 0f || !Mathf.Approximately(e.speed, 1f)))
                    return true;
            return false;
        }
    }

    // ───────────────────────────── Timing ─────────────────────────────

    [Serializable]
    public class TimingSettings
    {
        [Tooltip("Scene A가 보인 뒤 In 연출이 시작될 때까지 기다리는 시간(초).\n기본 0.")]
        [Min(0f)] public float preDelay = 0f;

        [Tooltip("화면을 덮어 가는 In 단계(클립 이름이 *_in)의 재생 설정.")]
        public PhaseTiming inPhase = new PhaseTiming();

        [Tooltip("In이 끝난 뒤 Out이 시작될 때까지 화면을 덮은 채로 기다리는 시간(초).\n" +
                 "실제 게임에서는 이 사이에 다음 화면을 로딩합니다. 뷰어 기본값 0.4.")]
        [Min(0f)] public float hold = 0.4f;

        [Tooltip("Scene A→B 교체 시점(초). Out이 시작하는 순간을 기준으로 한 상대값입니다.\n" +
                 "  0  = Out 시작과 동시에 교체 (기본, 원본 뷰어와 같음)\n" +
                 "  음수 = 그만큼 일찍 교체. 예: -hold 값 = In이 끝나는 순간\n" +
                 "  양수 = Out이 시작한 뒤 교체 (덮개가 걷히는 중에 장면이 바뀜)\n" +
                 "전체 구간 밖의 값은 처음/끝으로 잘립니다. 인스펙터 상단 타이밍 막대의 빨간 선으로 확인하세요.")]
        public float swapOffset = 0f;

        [Tooltip("화면을 걷어 내는 Out 단계(클립 이름이 *_out)의 재생 설정.\n" +
                 "Out 클립이 없는 스타일은 In만 재생합니다.")]
        public PhaseTiming outPhase = new PhaseTiming();

        [Tooltip("Out이 끝난 뒤 마지막 프레임을 유지하다가 연출 오브젝트를 지우기까지의 시간(초).\n기본 0.")]
        [Min(0f)] public float postHold = 0f;
    }

    [Serializable]
    public class PhaseTiming
    {
        [Tooltip("끄면 이 단계를 재생하지 않습니다.\n" +
                 "  In을 끄면: In의 마지막 프레임(화면을 다 덮은 상태)에서 시작합니다.\n" +
                 "  Out을 끄면: Hold 뒤 Out 없이 바로 끝납니다.\n" +
                 "한 단계만 따로 분석할 때 씁니다.")]
        public bool play = true;

        [Tooltip("이 단계 Animator 클립의 배속. 1 = 원본, 0.5 = 절반 속도(2배 길게), 2 = 2배 빠르게.\n" +
                 "Target Duration이 0보다 크면 무시됩니다. 뷰어의 Speed 슬라이더와 곱해집니다.")]
        [Min(0.05f)] public float speed = 1f;

        [Tooltip("이 단계가 걸릴 목표 시간(초). 0이면 사용하지 않습니다.\n" +
                 "값을 넣으면 (트림한 구간 길이 ÷ 목표 시간)으로 배속을 자동 계산해 Speed를 대신합니다.")]
        [Min(0f)] public float targetDuration = 0f;

        [Tooltip("클립의 이 시간(초)부터 재생합니다. 앞부분을 건너뛸 때 씁니다. 0 = 처음부터.\n" +
                 "[Elements]의 지연·배속으로 클립이 늘어났다면 늘어난 클립 기준 시간입니다.")]
        [Min(0f)] public float trimStart = 0f;

        [Tooltip("클립의 이 시간(초)에서 멈추고 그 프레임을 유지합니다. 0 = 끝까지.\n" +
                 "Trim Start보다 작거나 같으면 무시합니다. 연출 중간 상태를 정지 화면으로 볼 때 씁니다.")]
        [Min(0f)] public float trimEnd = 0f;
    }

    // ───────────────────────────── Color ─────────────────────────────

    [Serializable]
    public class ColorSettings
    {
        [Tooltip("모든 Graphic(Image, RawImage, Text 등) 색에 곱하는 색. 흰색 = 원본.\n알파도 곱해지므로 반투명하게 만들 때도 쓸 수 있습니다.")]
        public Color tint = Color.white;

        [Tooltip("색상환 회전 각도(도). 0 = 원본, 180 = 보색.\n무채색(흰/회/검)은 바뀌지 않습니다.")]
        [Range(-180f, 180f)] public float hueShift = 0f;

        [Tooltip("채도 배율. 1 = 원본, 0 = 흑백, 2 = 두 배(최대 1로 잘림).")]
        [Range(0f, 2f)] public float saturation = 1f;

        [Tooltip("명도(HSV의 V) 배율. 1 = 원본, 0 = 검정. 1보다 크면 밝아지지만 각 채널은 최대 1로 잘립니다.")]
        [Range(0f, 2f)] public float brightness = 1f;

        [Tooltip("모든 Graphic 알파 배율. 1 = 원본. 1보다 크게 하면 반투명 요소가 진해집니다(최대 1).\n가장 마지막에 적용됩니다.")]
        [Range(0f, 2f)] public float alphaScale = 1f;

        [Tooltip("끄면 Text / TextMeshPro 계열 텍스트는 위의 전체 색 보정과 팔레트에서 제외합니다.\n" +
                 "[Elements]의 요소별 색은 이 설정과 관계없이 적용됩니다.")]
        public bool includeText = true;

        [Tooltip("팔레트 일치 허용 오차. RGB 각 채널 차이가 이 값 이하이면 같은 색으로 봅니다(0~1 범위, 0.02 ≈ 5/255).\n" +
                 "애니메이션으로 색이 변하는 중간값은 일치하지 않을 수 있으니 필요하면 조금 올리세요.")]
        [Range(0f, 0.25f)] public float paletteTolerance = 0.02f;

        [Tooltip("원본 색 → 새 색 매핑 표. [Populate from Prefab]이 프리팹에 쓰인 색을 사용 횟수 순으로 채웁니다.\n" +
                 "Use를 켠 항목만 적용되고, 위에서부터 처음 일치한 항목 하나만 씁니다.\n" +
                 "알파는 원본(애니메이션) 값을 유지하고 RGB만 바꿉니다.")]
        public List<PaletteEntry> palette = new List<PaletteEntry>();

        public bool IsIdentity => tint == Color.white && hueShift == 0f && saturation == 1f && brightness == 1f && alphaScale == 1f;
    }

    [Serializable]
    public class PaletteEntry
    {
        [Tooltip("켜면 이 매핑을 적용합니다.")]
        public bool use;
        [Tooltip("찾을 원본 색(RGB). Populate가 프리팹에서 읽어 온 값입니다. 직접 추가할 수도 있습니다.")]
        public Color from = Color.white;
        [Tooltip("바꿀 색(RGB). 알파는 무시되고 원본 알파가 유지됩니다.")]
        public Color to = Color.white;
        [Tooltip("프리팹에서 이 색을 쓰는 Graphic 수(참고용, Populate가 채움).")]
        [ReadOnly] public int usage;
    }

    // ───────────────────────────── Resources ─────────────────────────────

    [Serializable]
    public class ResourceSettings
    {
        [Tooltip("원본 스프라이트 → 교체 스프라이트. 같은 스프라이트를 쓰는 모든 Image에 적용되며 클립의 스프라이트 키보다 우선합니다.\n" +
                 "교체할 이미지는 프로젝트(Assets/ 아래)에 Texture Type = Sprite로 임포트되어 있어야 합니다.\n" +
                 "Image 크기는 그대로이므로 비율이 다른 이미지는 늘어나 보일 수 있습니다.")]
        public List<SpriteSwap> spriteSwaps = new List<SpriteSwap>();

        [Tooltip("프리팹이 쓰는 머티리얼별 셰이더 프로퍼티 덮어쓰기(원형 와이프 반경·색, UV 스크롤 속도, 마스크 텍스처 등).\n" +
                 "재생할 때 머티리얼 복사본을 만들어 쓰므로 원본 .mat 파일은 바뀌지 않습니다. Play 중 값을 바꾸면 바로 반영됩니다.\n" +
                 "주의: UIMaterialAnimationHandler가 애니메이션으로 움직이는 프로퍼티는 애니메이션 값이 이깁니다.")]
        public List<MaterialOverride> materials = new List<MaterialOverride>();

        [Tooltip("전환 전 장면(Scene A) 배경 교체. 내 게임 화면 캡처 등을 넣고 전환을 테스트할 때 씁니다.")]
        public BackdropOverride sceneA = new BackdropOverride();

        [Tooltip("전환 후 장면(Scene B) 배경 교체.")]
        public BackdropOverride sceneB = new BackdropOverride();
    }

    [Serializable]
    public class SpriteSwap
    {
        [Tooltip("교체 대상 원본 스프라이트(Populate가 채움). 바꾸지 마세요.")]
        [ReadOnly] public Sprite original;
        [Tooltip("새 스프라이트. 비워 두면 원본을 그대로 씁니다.")]
        public Sprite replacement;
        [Tooltip("프리팹에서 이 스프라이트를 쓰는 Image 수(참고용). 0이면 클립의 스프라이트 교체 키로만 등장합니다.")]
        [ReadOnly] public int usage;
    }

    [Serializable]
    public class MaterialOverride
    {
        [Tooltip("대상 원본 머티리얼(Populate가 채움). 이 머티리얼을 쓰는 모든 Graphic에 적용됩니다.")]
        [ReadOnly] public Material material;
        [Tooltip("셰이더 프로퍼티 목록. Use를 켠 항목만 덮어씁니다. 왼쪽 값은 원본, 오른쪽이 새 값입니다.")]
        public List<MaterialProp> props = new List<MaterialProp>();
    }

    public enum MaterialPropType { Float, Color, Vector, Texture }

    [Serializable]
    public class MaterialProp
    {
        [Tooltip("켜면 이 프로퍼티를 새 값으로 덮어씁니다. 끄면 원본 값으로 돌아갑니다.")]
        public bool use;
        [Tooltip("셰이더 프로퍼티 이름(예: _Color0, _L_circle, _MaskTexture).")]
        [ReadOnly] public string name;
        [Tooltip("셰이더 프로퍼티 타입.")]
        [ReadOnly] public MaterialPropType type;
        [Tooltip("Range 프로퍼티의 최솟값·최댓값(슬라이더 범위). 둘 다 0이면 범위 없음.")]
        [ReadOnly] public Vector2 range;
        [Tooltip("새 값 (Float/Range 타입).")]
        public float floatValue;
        [Tooltip("새 값 (Color 타입). HDR 값도 그대로 씁니다.")]
        public Color colorValue = Color.white;
        [Tooltip("새 값 (Vector 타입). 셰이더마다 xyzw의 의미가 다릅니다(예: 타일링·오프셋).")]
        public Vector4 vectorValue;
        [Tooltip("새 값 (Texture 타입). 비워 두면 원본 텍스처를 유지합니다.")]
        public Texture textureValue;
        [Tooltip("Populate 시점의 원본 Float 값(참고용).")]
        [ReadOnly] public float originalFloat;
        [Tooltip("Populate 시점의 원본 Color 값(참고용).")]
        [ReadOnly] public Color originalColor;
        [Tooltip("Populate 시점의 원본 Vector 값(참고용).")]
        [ReadOnly] public Vector4 originalVector;
    }

    [Serializable]
    public class BackdropOverride
    {
        [Tooltip("켜면 이 장면의 배경을 아래 값으로 바꿉니다. 끄면 뷰어 기본 배경(파랑 A / 빨강 B)을 씁니다.")]
        public bool use;
        [Tooltip("배경 색. 스프라이트를 넣었다면 스프라이트에 곱해지는 색입니다(흰색 = 스프라이트 원래 색).")]
        public Color color = Color.white;
        [Tooltip("배경 이미지. 화면 전체로 늘려 그립니다. 비워 두면 위의 색으로 채웁니다.")]
        public Sprite sprite;
        [Tooltip("\"Scene A\" / \"Scene B\" 글자를 표시할지 여부.")]
        public bool showLabel = true;
    }

    // ───────────────────────────── Shape ─────────────────────────────

    [Serializable]
    public class ShapeSettings
    {
        [Tooltip("스타일 전체 이동량(px, 기준 해상도 1920×1080). +X 오른쪽, +Y 위쪽.")]
        public Vector2 offset = Vector2.zero;
        [Tooltip("스타일 전체 크기 배율. 1 = 원본. 화면보다 작게 만들면 덮개 바깥이 보이니 분석용으로 쓰세요.")]
        [Min(0.01f)] public float scale = 1f;
        [Tooltip("스타일 전체 회전(도, 반시계 방향).")]
        [Range(-180f, 180f)] public float rotation = 0f;
        [Tooltip("좌우 반전. 와이프·슬라이드 방향을 거꾸로 할 때 씁니다.")]
        public bool flipX;
        [Tooltip("상하 반전.")]
        public bool flipY;
    }

    // ───────────────────────────── Tweens ─────────────────────────────

    public enum TweenKind
    {
        [Tooltip("모든 DOTween")] All,
        [Tooltip("무한 반복 회전(장식 링 등). loops = -1인 Rotate/LocalRotate")] LoopingRotation,
        [Tooltip("이동(Move, LocalMove, Punch/Shake Position)")] Move,
        [Tooltip("회전(Rotate, LocalRotate, Punch/Shake Rotation)")] Rotate,
        [Tooltip("크기(Scale, Punch/Shake Scale, UIWidthHeight)")] Scale,
        [Tooltip("색·투명도(Color, Fade)")] ColorFade,
        [Tooltip("그 밖의 타입(FillAmount, Text 등)")] Other
    }

    [Serializable]
    public class TweenSettings
    {
        [Tooltip("끄면 이 스타일의 DOTweenAnimation을 하나도 실행하지 않습니다(장식 회전 등이 멈춘 상태).\n" +
                 "Animator 클립 연출만 따로 볼 때 씁니다.")]
        public bool enabled = true;

        [Tooltip("모든 DOTween의 배속. 1 = 원본. duration과 delay를 이 값으로 나눕니다.\n뷰어의 Speed 슬라이더(재생 시작 시점 값)와 곱해집니다.")]
        [Min(0.05f)] public float speed = 1f;

        [Tooltip("경로·종류별 세부 규칙. 위에서부터 일치하는 규칙을 모두 적용합니다(배속은 곱하고 지연은 더함).\n" +
                 "[Populate from Prefab] 뒤 인스펙터 상단 요약에서 이 스타일의 트윈 구성을 확인할 수 있습니다.")]
        public List<TweenRule> rules = new List<TweenRule>();
    }

    [Serializable]
    public class TweenRule
    {
        [Tooltip("대상 요소의 계층 경로. * = 아무 문자열(/ 포함), ? = 아무 한 글자.\n" +
                 "기본값 * 는 모든 요소. 비워 두면 이 규칙을 쓰지 않습니다.")]
        [ElementPath] public string path = "*";
        [Tooltip("켜면 경로와 일치하는 요소의 하위 요소에 붙은 트윈도 대상에 넣습니다.")]
        public bool includeChildren = true;
        [Tooltip("대상 트윈 종류. 장식 링 회전만 조절하려면 LoopingRotation을 고르세요.")]
        public TweenKind kind = TweenKind.All;
        [Tooltip("끄면 대상 트윈을 실행하지 않습니다.")]
        public bool enabled = true;
        [Tooltip("대상 트윈 배속. 1 = 원본, 0.5 = 절반 속도. 무한 회전이면 회전 속도 배율이 됩니다.")]
        [Min(0.05f)] public float speed = 1f;
        [Tooltip("시작 지연에 더할 시간(초, 배속 적용 후). 음수면 앞당기며 0 미만으로는 내려가지 않습니다.")]
        public float delayOffset = 0f;
        [Tooltip("켜면 대상 트윈의 이징을 아래 Ease로 바꿉니다.\n원본은 대부분 Custom Curve(INTERNAL_Custom)를 씁니다.")]
        public bool overrideEase;
        [Tooltip("새 이징. INTERNAL_Custom을 고르면 아래 Custom Curve를 씁니다.")]
        public Ease ease = Ease.OutQuad;
        [Tooltip("Ease가 INTERNAL_Custom일 때 쓰는 커브. 가로 0~1 = 진행도, 세로 0~1 = 시작값→끝값.")]
        public AnimationCurve customCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    // ───────────────────────────── Particles ─────────────────────────────

    [Serializable]
    public class ParticleSettings
    {
        [Tooltip("끄면 이 스타일의 파티클을 모두 숨깁니다.")]
        public bool enabled = true;
        [Tooltip("파티클 시뮬레이션 배속. 1 = 원본. 뷰어 Speed 슬라이더(재생 시작 시점 값)와 곱해집니다.\n재생 시작 시 한 번 적용됩니다.")]
        [Min(0.05f)] public float simulationSpeed = 1f;
        [Tooltip("파티클 시작 색(Start Color)에 곱하는 색. 흰색 = 원본. 재생 시작 시 한 번 적용됩니다.")]
        public Color startColorTint = Color.white;
        [Tooltip("파티클 텍스처 교체(_MainTex / _BaseMap). 비워 두면 원본. 재생 시작 시 한 번 적용됩니다.")]
        public Texture texture;
    }

    // ───────────────────────────── Elements ─────────────────────────────

    public enum ElementColorMode
    {
        [Tooltip("색을 바꾸지 않음")] None,
        [Tooltip("현재 색에 곱함(알파 포함)")] Multiply,
        [Tooltip("RGB만 교체하고 알파는 애니메이션 값을 유지")] ReplaceRGB,
        [Tooltip("RGBA 모두 교체(페이드 연출이 사라짐)")] ReplaceRGBA
    }

    [Serializable]
    public class ElementOverride
    {
        [Tooltip("대상 요소의 계층 경로(스타일 루트 기준, 예: \"Root/Ring/Circle_01\").\n" +
                 "* = 아무 문자열(/ 포함), ? = 아무 한 글자. 예: \"*Circle*\" = 이름에 Circle이 들어간 모든 요소.\n" +
                 "비워 두면 이 항목을 쓰지 않습니다. 오른쪽 [▼]로 계층에서 고를 수 있습니다.")]
        [ElementPath] public string path = "";

        [Tooltip("켜면 일치한 요소의 하위 요소 전체에도 적용합니다.\n끄면 그 요소 자신에만 적용합니다(색·스프라이트는 그 요소의 Graphic만).")]
        public bool includeChildren = true;

        [Header("표시")]
        [Tooltip("끄면 대상 요소를 숨깁니다(Graphic 알파 0, 파티클 렌더러 끔). 연출을 층별로 뜯어볼 때 씁니다.\n" +
                 "주의: Mask 요소만 숨기고 하위를 남기면(Include Children 끔) 마스크가 풀리거나 하위가 안 보일 수 있습니다.")]
        public bool visible = true;

        [Header("타이밍 (에디터 Play 전용)")]
        [Tooltip("대상 요소의 클립 키 전체를 이 시간(초)만큼 뒤로 미룹니다. 음수면 앞당깁니다.\n" +
                 "등장(활성화) 키도 함께 밀리므로 그 요소의 DOTween 시작도 같이 늦춰집니다.\n" +
                 "클립 복사본을 다시 만드는 기능이라 에디터에서만 동작하며, 원본 .anim은 바뀌지 않습니다. 다음 재생부터 반영됩니다.")]
        public float delay = 0f;

        [Tooltip("대상 요소의 클립 키 배속. 1 = 원본, 2 = 2배 빠르게. 그 요소에 붙은 DOTween의 배속에도 곱해집니다.\n" +
                 "에디터에서만 동작하며 다음 재생부터 반영됩니다.")]
        [Min(0.05f)] public float speed = 1f;

        [Header("색상")]
        [Tooltip("대상 요소 Graphic의 색 처리 방식. 전체 색 보정([Color]) 뒤에 적용됩니다.")]
        public ElementColorMode colorMode = ElementColorMode.None;
        [Tooltip("Color Mode에서 쓸 색.")]
        public Color color = Color.white;

        [Header("리소스")]
        [Tooltip("대상 요소 Image의 스프라이트를 이것으로 바꿉니다. 비워 두면 그대로.\n" +
                 "[Resources]의 Sprite Swaps보다 우선합니다.")]
        public Sprite sprite;

        [Header("형태")]
        [Tooltip("대상 요소에 더할 위치(px, 부모 기준). 애니메이션 위치 위에 더해지므로 이동 연출은 유지됩니다.")]
        public Vector2 offset = Vector2.zero;
        [Tooltip("대상 요소 크기 배율(X, Y). 애니메이션 크기에 곱해집니다. 1,1 = 원본. 음수면 반전.")]
        public Vector2 scale = Vector2.one;
        [Tooltip("대상 요소에 더할 회전(도, 반시계 방향). 애니메이션·DOTween 회전 위에 더해집니다.")]
        [Range(-180f, 180f)] public float rotation = 0f;

        public bool IsSet => !string.IsNullOrEmpty(path);
        public bool Matches(string elementPath) => IsSet && ElementPathMatcher.Matches(path, includeChildren, elementPath);
        public bool HasTransform => offset != Vector2.zero || scale != Vector2.one || rotation != 0f;
    }

    // ───────────────────────────── Debug ─────────────────────────────

    [Serializable]
    public class DebugSettings
    {
        [Tooltip("뷰어 하단에 Pre / In / Hold / Out 막대, 현재 재생 위치, A→B 교체 시점(빨간 선)을 그립니다.")]
        public bool showTimeline = true;
        [Tooltip("켜면 아래 경로와 일치하는 요소의 RectTransform 테두리와 이름을 화면에 그립니다.")]
        public bool showBounds;
        [Tooltip("테두리를 그릴 요소 경로(* / ? 와일드카드). 기본 * = 활성화된 모든 요소.")]
        [ElementPath] public string boundsPath = "*";
        [Tooltip("테두리 색.")]
        public Color boundsColor = new Color(0.2f, 1f, 0.4f, 0.9f);
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    /// <summary>Marks a string field as an element path so the inspector offers a hierarchy dropdown.</summary>
    public class ElementPathAttribute : PropertyAttribute { }

    /// <summary>Shows a field greyed out: values filled by Populate that should not be edited by hand.</summary>
    public class ReadOnlyAttribute : PropertyAttribute { }

    public static class ElementPathMatcher
    {
        static readonly Dictionary<string, Regex> cache = new Dictionary<string, Regex>();

        /// <summary>Glob match on hierarchy paths: * spans any characters including '/', ? is one character.</summary>
        public static bool Matches(string pattern, bool includeChildren, string path)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;
            var key = (includeChildren ? "1" : "0") + pattern;
            if (!cache.TryGetValue(key, out var regex))
            {
                var body = Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".");
                regex = new Regex(includeChildren ? $"^(?:{body})(?:/.*)?$" : $"^(?:{body})$", RegexOptions.IgnoreCase);
                cache[key] = regex;
            }
            return regex.IsMatch(path);
        }
    }
}
