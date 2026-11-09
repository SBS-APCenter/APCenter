using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>
    /// 사무실 클립보드 UI (5-1장). 열려 있는 동안 게임 시간 정지(timeScale 0).
    /// 우측 포스트잇 탭: [계약] / [알바관리] / [플레이어 업그레이드].
    /// 탭 전환 시 맨 앞 종이가 클립에 물린 채 윗변을 축으로 넘어가 보드 뒤로 사라지는 연출 (원근 메시, PageFlipGraphic).
    /// </summary>
    public class OfficeUI : MonoBehaviour
    {
        public GameObject root;
        public GameObject[] pages;
        public Button[] tabButtons;
        public RectTransform[] tabRects;
        public Button closeButton;
        public float tabSelectedX = 0f;   // 선택된 탭: 종이 밖으로 더 튀어나옴
        public float tabIdleX = -24f;

        [Header("Page Flip")]
        public float flipDuration = 0.75f;          // 전체 시간 (앞 70% 클립 위로 넘김, 뒤 30% 보드 뒤로 떨어짐)
        [Range(0f, 1f)] public float flipShadow = 0.35f;
        public float flipPerspective = 3f;          // 작을수록 원근 강함

        int current;

        RectTransform paperRt;   // 페이지들의 부모(Paper)
        RectTransform boardRt;   // 클립보드 판
        PageFlipGraphic flip;    // 넘어가는 종이 (런타임 생성)
        Image shade;             // 넘어가는 동안 아래 종이에 드리우는 그림자
        Sequence flipSeq;

        // 페이지 스냅샷 (숨김 카메라로 페이지를 텍스처에 찍음 → 화면 깜빡임 없음)
        Canvas rootCanvas;
        Camera snapCam;
        Canvas snapCanvas;
        CanvasScaler snapScaler;
        RectTransform snapHolder;
        RenderTexture snapRT;

        IrisTransition iris;

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsTransitioning => iris != null && iris.IsPlaying;

        /// <summary>사무실 진입 연출 (캐릭터 기준 아이리스 → 사무실 카드 → 화면 열리며 UI)</summary>
        public void OpenWithIris(int tab, Transform focus, Camera cam)
        {
            if (IsOpen || IsTransitioning) return;
            if (iris == null)
            {
                var canvas = GetComponentInParent<Canvas>(true).rootCanvas;
                var label = GetComponentInChildren<TMPro.TMP_Text>(true); // 한글 폰트 재사용
                iris = IrisTransition.Create(canvas, label != null ? label.font : null);
            }
            Time.timeScale = 0f; // 전환 시작과 동시에 게임 정지
            iris.Play(focus, cam, "사무실", () => Open(tab));
        }

        void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int k = i;
                tabButtons[i].onClick.AddListener(() => Show(k));
            }
            closeButton.onClick.AddListener(Close);
        }

        void OnDestroy()
        {
            DOTween.Kill(this);
            if (snapRT != null) { snapRT.Release(); Destroy(snapRT); }
            if (snapCam != null) Destroy(snapCam.gameObject);
            if (snapCanvas != null) Destroy(snapCanvas.gameObject);
        }

        public void Open(int tab)
        {
            root.SetActive(true);
            Time.timeScale = 0f;
            SetPage(tab >= 0 ? tab : current); // 열 때는 연출 없이 즉시
        }

        public void Close()
        {
            if (!GameManager.I.tutorialDone) return; // 튜토리얼(계약 2건) 끝날 때까지 닫기 불가
            FinishFlip();
            root.SetActive(false);
            Time.timeScale = GameManager.I.timeSpeed;
        }

        /// <summary>탭 전환. 열려 있으면 종이 넘김 연출, 아니면 즉시 전환.</summary>
        public void Show(int i)
        {
            if (!Application.isPlaying || !IsOpen || i == current) { SetPage(i); return; }
            FlipTo(i);
        }

        void SetPage(int i)
        {
            FinishFlip();
            current = i;
            for (int p = 0; p < pages.Length; p++) pages[p].SetActive(p == i);
            UpdateTabs(i);
        }

        void UpdateTabs(int i)
        {
            for (int t = 0; t < tabRects.Length; t++)
            {
                var pos = tabRects[t].anchoredPosition;
                pos.x = t == i ? tabSelectedX : tabIdleX; // 선택된 탭이 바깥으로 튀어나옴
                tabRects[t].anchoredPosition = pos;
            }
        }

        // ───────────── 종이 넘김 ─────────────

        void EnsureFlipObjects()
        {
            if (flip != null) return;

            paperRt = (RectTransform)pages[0].transform.parent;
            boardRt = (RectTransform)paperRt.parent;
            rootCanvas = GetComponentInParent<Canvas>(true).rootCanvas;
            var paperImg = paperRt.GetComponent<Image>();
            Color paperColor = paperImg != null ? paperImg.color : Color.white;

            // 넘어가는 종이: 피벗 = 윗변 중앙(클립 라인)
            var go = new GameObject("FlipSheet", typeof(RectTransform), typeof(CanvasRenderer), typeof(PageFlipGraphic));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(boardRt, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            flip = go.GetComponent<PageFlipGraphic>();
            flip.raycastTarget = false;
            flip.backColor = new Color(paperColor.r * 0.93f, paperColor.g * 0.92f, paperColor.b * 0.9f, 1f);
            go.SetActive(false);

            // 그림자 (Paper 안, 페이지 위)
            var sgo = new GameObject("FlipShade", typeof(RectTransform), typeof(Image));
            sgo.layer = gameObject.layer;
            var s = (RectTransform)sgo.transform;
            s.SetParent(paperRt, false);
            s.anchorMin = Vector2.zero; s.anchorMax = Vector2.one;
            s.offsetMin = s.offsetMax = Vector2.zero;
            shade = sgo.GetComponent<Image>();
            shade.color = Color.clear;
            shade.raycastTarget = false;
            sgo.SetActive(false);

            // 스냅샷 리그: 비활성 카메라 + 그 카메라 전용 Screen Space - Camera 캔버스 (씬 루트, 평소엔 꺼둠)
            var camGo = new GameObject("PageSnapshotCam");
            camGo.transform.position = new Vector3(0f, -5000f, 0f);
            snapCam = camGo.AddComponent<Camera>();
            snapCam.enabled = false;
            snapCam.orthographic = true;
            snapCam.clearFlags = CameraClearFlags.SolidColor;
            snapCam.backgroundColor = paperColor;
            snapCam.cullingMask = 1 << gameObject.layer;
            snapCam.nearClipPlane = 0.1f;
            snapCam.farClipPlane = 10f;

            var cgo = new GameObject("PageSnapshotCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.layer = gameObject.layer;
            snapCanvas = cgo.GetComponent<Canvas>();
            snapCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            snapCanvas.worldCamera = snapCam;
            snapCanvas.planeDistance = 1f;
            snapScaler = cgo.GetComponent<CanvasScaler>();
            snapScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var hgo = new GameObject("Holder", typeof(RectTransform));
            hgo.layer = gameObject.layer;
            snapHolder = (RectTransform)hgo.transform;
            snapHolder.SetParent(cgo.transform, false);
            snapHolder.anchorMin = Vector2.zero; snapHolder.anchorMax = Vector2.one;
            snapHolder.offsetMin = snapHolder.offsetMax = Vector2.zero;
            cgo.SetActive(false);
        }

        /// <summary>페이지를 Paper 크기 그대로 텍스처에 찍는다 (같은 프레임, 화면에는 안 보임)</summary>
        Texture Snapshot(GameObject page)
        {
            float sf = rootCanvas.scaleFactor;
            Vector2 size = paperRt.rect.size;
            int w = Mathf.Max(8, Mathf.CeilToInt(size.x * sf));
            int h = Mathf.Max(8, Mathf.CeilToInt(size.y * sf));
            if (snapRT == null || snapRT.width != w || snapRT.height != h)
            {
                if (snapRT != null) { snapRT.Release(); Destroy(snapRT); }
                snapRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "PageSnapshot" };
                snapRT.Create();
            }
            snapCam.targetTexture = snapRT;
            snapScaler.scaleFactor = sf; // 캔버스 단위 = Paper 단위 → 레이아웃 동일

            var t = page.transform;
            int sib = t.GetSiblingIndex();
            bool wasActive = page.activeSelf;
            snapCanvas.gameObject.SetActive(true);
            t.SetParent(snapHolder, false);
            page.SetActive(true);
            Canvas.ForceUpdateCanvases();
            snapCam.Render();
            t.SetParent(paperRt, false);
            t.SetSiblingIndex(sib);
            page.SetActive(wasActive);
            snapCanvas.gameObject.SetActive(false);
            return snapRT;
        }

        void FlipTo(int i)
        {
            FinishFlip();
            EnsureFlipObjects();

            int from = current;
            current = i;
            UpdateTabs(i);

            bool forward = i > from;
            var oldPage = pages[from];
            var newPage = pages[i];

            // 넘어가는 장 = 앞으로: 지금 장 / 뒤로: 돌아올 장. 아래에 깔린 장은 실제 UI 그대로.
            flip.frontTexture = Snapshot(forward ? oldPage : newPage);
            flip.perspective = flipPerspective;
            var under = forward ? newPage : oldPage;
            for (int p = 0; p < pages.Length; p++) pages[p].SetActive(pages[p] == under);

            // 넘어가는 종이를 Paper 윗변(클립 라인)에 맞춤
            var rt = flip.rectTransform;
            rt.SetParent(boardRt, false);
            rt.SetSiblingIndex(paperRt.GetSiblingIndex() + 1); // Paper 위, 클립 아래
            var c = new Vector3[4];
            paperRt.GetWorldCorners(c);
            rt.sizeDelta = paperRt.rect.size;
            rt.position = (c[1] + c[2]) * 0.5f;
            flip.gameObject.SetActive(true);

            shade.transform.SetAsLastSibling();
            shade.gameObject.SetActive(true);

            float up = flipDuration * 0.7f, drop = flipDuration * 0.3f;
            flipSeq = DOTween.Sequence().SetUpdate(true).SetTarget(this); // timeScale 0 에서도 재생

            if (forward)
            {
                // 들어올려 클립 위로 넘김 (꼭대기에서 살짝 멈칫) → 보드 뒤로 툭 떨어짐
                SetFlipAngle(0f);
                flipSeq.Append(DOTween.To(() => flip.Angle, SetFlipAngle, 180f, up).SetEase(Ease.InOutSine));
                flipSeq.Append(DOTween.To(() => flip.Angle, SetFlipAngle, 270f, drop).SetEase(Ease.InQuad));
                flipSeq.OnComplete(EndFlipVisuals);
            }
            else
            {
                // 보드 뒤에서 올라와 클립 위로 넘어온 뒤 → 앞으로 내려와 덮음
                SetFlipAngle(270f);
                flipSeq.Append(DOTween.To(() => flip.Angle, SetFlipAngle, 180f, drop).SetEase(Ease.OutQuad));
                flipSeq.Append(DOTween.To(() => flip.Angle, SetFlipAngle, 0f, up).SetEase(Ease.InOutSine));
                flipSeq.OnComplete(() =>
                {
                    oldPage.SetActive(false);
                    newPage.SetActive(true); // 스냅샷과 같은 모습 → 바꿔치기 티 안 남
                    EndFlipVisuals();
                });
            }
        }

        /// <summary>각도 적용 + 180° 넘으면 보드 뒤 레이어로, 아니면 보드 위(클립 아래)로</summary>
        void SetFlipAngle(float a)
        {
            flip.Angle = a;
            var rt = flip.rectTransform;
            bool behind = a > 180f;
            var parent = behind ? boardRt.parent : boardRt;
            if (rt.parent != parent)
            {
                rt.SetParent(parent, true);
                if (behind) rt.SetSiblingIndex(boardRt.GetSiblingIndex());          // 보드 바로 뒤
                else rt.SetSiblingIndex(paperRt.GetSiblingIndex() + 1);             // Paper 위, 클립 아래
            }
            // 덮고 있을수록 아래 장이 어두움
            float cover = Mathf.Clamp01(Mathf.Cos(Mathf.Min(a, 90f) * Mathf.Deg2Rad));
            shade.color = new Color(0f, 0f, 0f, flipShadow * cover);
        }

        void EndFlipVisuals()
        {
            var rt = flip.rectTransform;
            if (rt.parent != boardRt) rt.SetParent(boardRt, true);
            flip.gameObject.SetActive(false);
            shade.transform.SetAsLastSibling();
            shade.gameObject.SetActive(false);
        }

        /// <summary>진행 중인 플립이 있으면 즉시 끝 상태로 마무리 (연타/닫기 대비)</summary>
        void FinishFlip()
        {
            if (flipSeq != null && flipSeq.IsActive())
            {
                flipSeq.Complete();
            }
            flipSeq = null;
        }

        void Update()
        {
            if (IsOpen) closeButton.interactable = GameManager.I.tutorialDone;
        }
    }
}
