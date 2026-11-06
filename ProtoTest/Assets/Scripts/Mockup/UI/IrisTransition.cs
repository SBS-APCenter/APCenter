using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>
    /// 아이리스(동그라미) 화면 전환. 캐릭터 기준으로 원이 오므라들다 → 캐릭터만 보일 때 멈칫 → 푝 닫힘
    /// → 장소 카드 → 원이 열리며 다음 화면. 오브젝트는 런타임 생성 (씬 배치 불필요), timeScale 0 에서도 재생.
    /// 구조: 투명한 원이 뚫린 Hole 이미지 + 그 바깥을 덮는 검은 사각형 4장 → Hole 크기 0 = 화면 전체 검정.
    /// </summary>
    public class IrisTransition : MonoBehaviour
    {
        [Header("타이밍 (초, unscaled)")]
        public float closeDuration = 0.6f;   // 화면 → 캐릭터 크기까지 오므라듦
        public float holdDuration = 0.3f;    // 멈칫
        public float snapDuration = 0.2f;    // 푝 닫힘
        public float cardInDuration = 0.35f;
        public float cardHold = 0.7f;
        public float cardOutDuration = 0.2f;
        public float openDuration = 0.5f;    // 화면 열림

        [Header("멈칫 크기")]
        public float focusScale = 1.8f;      // 화면상 캐릭터 크기 × 이 값 = 멈칫할 때 원 지름
        public float minFocusDiameter = 140f; // 캔버스 단위

        const float Big = 20000f;
        static readonly Color PaperColor = new Color(0.99f, 0.98f, 0.94f);
        static readonly Color BoardColor = new Color(0.55f, 0.38f, 0.22f);
        static readonly Color InkColor = new Color(0.2f, 0.16f, 0.12f);

        Canvas rootCanvas;
        RectTransform hole;
        RectTransform card;
        CanvasGroup cardGroup;
        TMP_Text cardTitle;
        Sequence seq;
        Transform focus;
        Camera cam;
        bool tracking;

        public bool IsPlaying => seq != null && seq.IsActive();

        public static IrisTransition Create(Canvas root, TMP_FontAsset font)
        {
            var go = new GameObject("IrisTransition", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.layer = root.gameObject.layer;
            var c = go.GetComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 1000; // 모든 UI 위

            var iris = go.AddComponent<IrisTransition>();
            iris.rootCanvas = root;
            iris.Build(rt, font);
            go.SetActive(false);
            return iris;
        }

        void OnDestroy()
        {
            DOTween.Kill(this);
        }

        // ───────────── 구성 ─────────────

        void Build(RectTransform rt, TMP_FontAsset font)
        {
            var holeImg = NewImage("Hole", rt, Color.black);
            hole = holeImg.rectTransform;
            hole.anchorMin = hole.anchorMax = hole.pivot = new Vector2(0.5f, 0.5f);
            holeImg.sprite = MakeHoleSprite();

            // 원 바깥을 덮는 4장 (Hole 의 자식이라 원을 따라 움직임)
            Cover("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(Big, Big));
            Cover("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(Big, Big));
            Cover("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(Big, 0f));
            Cover("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(Big, 0f));

            // 장소 카드 (클립보드 느낌: 갈색 테두리 + 종이)
            var border = NewImage("Card", rt, BoardColor);
            border.raycastTarget = false;
            card = border.rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(560f, 230f);
            cardGroup = border.gameObject.AddComponent<CanvasGroup>();
            cardGroup.blocksRaycasts = false;

            var paper = NewImage("Paper", card, PaperColor);
            paper.raycastTarget = false;
            var prt = paper.rectTransform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(12f, 12f); prt.offsetMax = new Vector2(-12f, -12f);

            NewText("Sub", prt, font, "OFFICE", 30f, BoardColor, new Vector2(0f, 52f), new Vector2(500f, 40f));
            cardTitle = NewText("Title", prt, font, "사무실", 84f, InkColor, new Vector2(0f, -14f), new Vector2(500f, 110f));
            card.gameObject.SetActive(false);
        }

        void Cover(string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
        {
            var r = NewImage(name, hole, Color.black).rectTransform;
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pivot;
            r.sizeDelta = size;
            r.anchoredPosition = Vector2.zero;
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true; // 전환 중 클릭 차단
            return img;
        }

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            return t;
        }

        /// <summary>가운데가 투명한 원, 바깥이 불투명한 텍스처 (가장자리 살짝 부드럽게)</summary>
        static Sprite MakeHoleSprite()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "IrisHole"
            };
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f, R = N * 0.5f - 2f, feather = 2.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = (byte)(Mathf.Clamp01((d - (R - feather)) / feather) * 255f);
                    px[y * N + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        // ───────────── 재생 ─────────────

        /// <param name="onCovered">화면이 완전히 덮여 있는 동안 호출 (여기서 다음 화면 열기)</param>
        public void Play(Transform focusTarget, Camera camera, string title, Action onCovered, Action onDone = null)
        {
            if (seq != null && seq.IsActive()) seq.Complete();

            focus = focusTarget;
            cam = camera != null ? camera : Camera.main;
            cardTitle.text = title;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            card.gameObject.SetActive(false);

            float sf = Mathf.Max(0.0001f, rootCanvas.scaleFactor);
            float full = new Vector2(Screen.width, Screen.height).magnitude / sf * 2.1f; // 원이 화면 어디서 시작해도 전부 보이게
            SampleFocus(out var center, out float focusD);
            hole.position = center;
            SetD(full);
            tracking = true;

            seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            seq.OnUpdate(() =>
            {
                if (tracking && focus != null) { SampleFocus(out var p, out _); hole.position = p; } // 카메라가 따라가는 중에도 캐릭터 고정
            });

            // 1) 캐릭터 쪽으로 쭉 오므라듦
            seq.Append(DOTween.To(GetD, SetD, focusD, closeDuration).SetEase(Ease.InOutCubic));
            // 2) 캐릭터만 보일 때 멈칫
            seq.AppendInterval(holdDuration);
            // 3) 푝 — InBack: 살짝 벌어졌다가 확 닫힘
            seq.Append(DOTween.To(GetD, SetD, 0f, snapDuration).SetEase(Ease.InBack, 2.2f));
            seq.AppendCallback(() =>
            {
                tracking = false;
                card.gameObject.SetActive(true);
                card.localScale = Vector3.one * 0.6f;
                cardGroup.alpha = 0f;
            });
            // 4) 장소 카드
            seq.AppendInterval(0.08f);
            seq.Append(card.DOScale(1f, cardInDuration).SetEase(Ease.OutBack));
            seq.Join(cardGroup.DOFade(1f, cardInDuration * 0.6f));
            seq.AppendCallback(() => onCovered?.Invoke()); // 검은 화면 뒤에서 다음 UI 준비
            seq.AppendInterval(cardHold);
            seq.Append(cardGroup.DOFade(0f, cardOutDuration));
            seq.Join(card.DOScale(1.08f, cardOutDuration).SetEase(Ease.InQuad));
            // 5) 화면 중앙에서 원이 열림
            seq.AppendCallback(() =>
            {
                card.gameObject.SetActive(false);
                hole.anchoredPosition = Vector2.zero;
            });
            seq.Append(DOTween.To(GetD, SetD, full, openDuration).SetEase(Ease.InCubic));
            seq.OnComplete(() =>
            {
                gameObject.SetActive(false);
                onDone?.Invoke();
            });
        }

        float GetD() => hole.sizeDelta.x;
        void SetD(float d) => hole.sizeDelta = new Vector2(d, d);

        /// <summary>캐릭터 화면 위치(오버레이 캔버스 world = 스크린 픽셀)와 "캐릭터만 보이는" 원 지름(캔버스 단위)</summary>
        void SampleFocus(out Vector3 center, out float diameter)
        {
            float sf = Mathf.Max(0.0001f, rootCanvas.scaleFactor);
            center = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            diameter = minFocusDiameter;
            if (focus == null || cam == null) return;

            var rends = focus.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(focus.position + Vector3.up, Vector3.one * 2f);
            bool any = false;
            foreach (var r in rends)
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }

            Vector3 c = cam.WorldToScreenPoint(b.center);
            Vector3 top = cam.WorldToScreenPoint(b.center + Vector3.up * b.extents.y);
            Vector3 bot = cam.WorldToScreenPoint(b.center - Vector3.up * b.extents.y);
            float side = Mathf.Max(b.extents.x, b.extents.z);
            Vector3 right = cam.WorldToScreenPoint(b.center + cam.transform.right * side);
            float px = Mathf.Max(Mathf.Abs(top.y - bot.y), Mathf.Abs(right.x - c.x) * 2f);

            center = new Vector3(c.x, c.y, 0f);
            diameter = Mathf.Max(minFocusDiameter, px * focusScale / sf);
        }
    }
}
