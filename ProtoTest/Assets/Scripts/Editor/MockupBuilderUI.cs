using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        static Sprite uiSprite;
        static readonly Color Dark = new Color(0.08f, 0.09f, 0.12f, 0.78f);
        static readonly Color Ink = new Color(0.15f, 0.13f, 0.1f);
        static readonly Color BtnYellow = new Color(1f, 0.86f, 0.35f);
        static readonly Color BtnBlue = new Color(0.55f, 0.75f, 1f);
        static readonly Color BtnRed = new Color(1f, 0.55f, 0.5f);
        static readonly Color PanelPaper = new Color(0.93f, 0.9f, 0.82f);

        // ================= UI 헬퍼 =================

        static RectTransform UI(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            g.transform.SetParent(parent, false);
            return (RectTransform)g.transform;
        }

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>부모 가로 x0~x1(비율), 세로 y0~y1(비율) 영역.</summary>
        static RectTransform Part(RectTransform rt, float x0, float x1, float y0 = 0f, float y1 = 1f, float pad = 6f)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        /// <summary>위쪽 기준 가로 꽉 찬 행 (y = 위에서부터 거리).</summary>
        static RectTransform RowTop(RectTransform rt, float y, float h, float padX = 20f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(-padX * 2f, h);
            return rt;
        }

        static Image Img(Transform parent, string name, Color c, Sprite sp = null)
        {
            var rt = UI(name, parent);
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = sp != null ? sp : uiSprite;
            i.type = Image.Type.Sliced;
            i.color = c;
            return i;
        }

        static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color c, TextAlignmentOptions al = TextAlignmentOptions.MidlineLeft)
        {
            var rt = UI(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSharedMaterial = font.material;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = al;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        static TextMeshProUGUI TxtFill(Transform parent, string name, string text, float size, Color c, TextAlignmentOptions al = TextAlignmentOptions.MidlineLeft, float pad = 8f)
        {
            var t = Txt(parent, name, text, size, c, al);
            Stretch(t.rectTransform, pad, pad, pad, pad);
            return t;
        }

        static Button Btn(Transform parent, string name, string label, Color c, float size, out TMP_Text lbl)
        {
            var img = Img(parent, name, c);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            b.colors = colors;
            lbl = TxtFill(img.transform, "Label", label, size, Ink, TextAlignmentOptions.Center, 4f);
            return b;
        }

        static Button Btn(Transform parent, string name, string label, Color c, float size) => Btn(parent, name, label, c, size, out _);

        static void LE(Component c, float h)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        static VerticalLayoutGroup VLG(GameObject g, float spacing, int pad)
        {
            var v = g.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return v;
        }

        static TMP_InputField Input(Transform parent, string name, string placeholder)
        {
            var bg = Img(parent, name, Color.white);
            var f = bg.gameObject.AddComponent<TMP_InputField>();
            var area = UI("TextArea", bg.transform);
            Stretch(area, 10f, 4f, 10f, 4f);
            area.gameObject.AddComponent<Image>();
            area.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var ph = TxtFill(area, "Placeholder", placeholder, 24f, new Color(0.5f, 0.5f, 0.5f), TextAlignmentOptions.MidlineLeft, 0f);
            ph.fontStyle = FontStyles.Italic;
            var tx = TxtFill(area, "Text", "", 24f, Color.black, TextAlignmentOptions.MidlineLeft, 0f);
            tx.textWrappingMode = TextWrappingModes.NoWrap;
            f.textViewport = area;
            f.textComponent = tx;
            f.placeholder = ph;
            f.fontAsset = font;
            f.targetGraphic = bg;
            f.contentType = TMP_InputField.ContentType.IntegerNumber;
            return f;
        }

        static GameObject Modal(Transform parent, string name, Vector2 size, out RectTransform box)
        {
            var dim = Img(parent, name, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);
            box = Place(Img(dim.transform, "Box", PanelPaper).rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        // ================= UI 구성 =================

        static void BuildUI(GameManager gm, PlayerController player)
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var cgo = new GameObject("Canvas") { layer = 5 };
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)cgo.transform;

            BuildHUD(root);
            player.joystick = BuildJoystick(root);
            gm.officeUI = BuildOffice(root);
            gm.levelUpPopup = BuildLevelPopup(root);
            BuildDebug(root, gm.config);
            BuildMap(root);
            BuildSettings(root);
        }

        static void BuildHUD(RectTransform root)
        {
            var rt = Stretch(UI("HUD", root));
            var hud = rt.gameObject.AddComponent<HUD>();

            // 레벨 + 경험치 바
            var lv = Place(Img(rt, "LevelBox", Dark).rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(470f, 90f));
            hud.levelText = Txt(Part(UI("LevelArea", lv), 0f, 0.28f), "LevelText", "Lv.1", 40f, Color.white, TextAlignmentOptions.Center);
            Stretch(hud.levelText.rectTransform);
            var expBg = Img(lv, "ExpBg", new Color(0.25f, 0.25f, 0.25f, 1f));
            Part(expBg.rectTransform, 0.3f, 0.97f, 0.28f, 0.72f, 4f);
            var fill = Img(expBg.transform, "ExpFill", new Color(0.4f, 0.85f, 1f), squareSprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform, 3f, 3f, 3f, 3f);
            hud.expFill = fill;
            hud.expText = TxtFill(expBg.transform, "ExpText", "0 / 100", 22f, Color.white, TextAlignmentOptions.Center, 0f);

            // 돈
            var money = Place(Img(rt, "MoneyBox", Dark).rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(380f, 80f));
            TxtFill(Part(UI("L", money), 0f, 0.3f), "MoneyLabel", "보유 돈", 26f, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Center, 0f);
            hud.moneyText = Txt(Part(UI("R", money), 0.3f, 1f), "MoneyText", "1,000", 40f, Color.white, TextAlignmentOptions.MidlineRight);
            Stretch(hud.moneyText.rectTransform, 0f, 0f, 14f, 0f);

            var banner = Img(rt, "NextRegionBanner", new Color(1f, 0.85f, 0.2f, 0.95f));
            Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(640f, 60f));
            TxtFill(banner.transform, "Text", "다음 지역 해금! (11레벨 달성 · 표시만)", 28f, Ink, TextAlignmentOptions.Center);
            banner.gameObject.SetActive(false);
            hud.nextRegionBanner = banner.gameObject;

            // 주문서 패널
            // 주문 수만큼 높이가 늘어나는 패널
            var op = Place(Img(rt, "OrderPanel", Dark).rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -125f), new Vector2(500f, 60f));
            VLG(op.gameObject, 4f, 8);
            op.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.orderTitle = Txt(op, "OrderTitle", "주문서 0/10", 30f, Color.white);
            LE(hud.orderTitle, 44f);
            // 정사각형 주문서 카드 (3열) — 물품 이모지 + 개수, 최대 6종
            var grid = UI("Cards", op).gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(150f, 150f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            hud.orderCards = new OrderCardRefs[10];
            for (int i = 0; i < 10; i++)
            {
                var bg = Img(grid.transform, $"Order_{i}", HUD.OrderNormal);
                var c = new OrderCardRefs { root = bg.gameObject, bg = bg, icons = new Image[6], counts = new TMP_Text[6] };
                c.idText = Txt(RowTop(UI("Head", bg.transform), 2f, 26f, 6f), "Id", "#0", 18f, Ink);
                Stretch(c.idText.rectTransform);
                for (int k = 0; k < 6; k++)
                {
                    float x0 = (k % 3) / 3f, y1 = k < 3 ? 0.82f : 0.41f;
                    var slot = Part(UI($"Slot_{k}", bg.transform), x0, x0 + 1f / 3f, y1 - 0.41f, y1, 2f);
                    var ic = Img(slot, "Icon", Color.white);
                    ic.type = Image.Type.Simple;
                    ic.preserveAspect = true;
                    ic.raycastTarget = false;
                    Part(ic.rectTransform, 0.05f, 0.95f, 0.32f, 1f, 0f);
                    c.icons[k] = ic;
                    c.counts[k] = Txt(Part(UI("CountArea", slot), 0f, 1f, 0f, 0.36f, 0f), "Count", "×1", 17f, Ink, TextAlignmentOptions.Center);
                    Stretch(c.counts[k].rectTransform);
                }
                hud.orderCards[i] = c;
                bg.gameObject.SetActive(false);
            }

            // 들고 있는 택배
            var cb = Place(Img(rt, "CarryBox", Dark).rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(900f, 90f));
            hud.carryText = TxtFill(cb, "CarryText", "들고 있음 0/4", 26f, Color.white, TextAlignmentOptions.Center);

            var help = Place(Img(rt, "HelpBox", new Color(0f, 0f, 0f, 0.45f)).rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(520f, 110f));
            TxtFill(help, "HelpText", "이동: WASD / 방향키 / 화면 누르고 드래그\n작업: 점선 원형 발판 위에 서 있기\n시점: 멈춘 상태에서 우클릭 드래그 (모바일 두 손가락)", 18f, Color.white);
        }

        static JoystickInput BuildJoystick(RectTransform root)
        {
            var go = Stretch(UI("Joystick", root));
            var j = go.gameObject.AddComponent<JoystickInput>();
            j.area = root;
            var circle = Img(go, "Circle", new Color(1f, 1f, 1f, 0.22f), circleSprite);
            circle.type = Image.Type.Simple;
            circle.raycastTarget = false;
            Place(circle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 180f));
            var knob = Img(circle.transform, "Knob", new Color(1f, 1f, 1f, 0.4f), circleSprite);
            knob.type = Image.Type.Simple;
            knob.raycastTarget = false;
            Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            j.circle = circle.rectTransform;
            j.knob = knob.rectTransform;
            circle.gameObject.SetActive(false);
            return j;
        }

        // ---------- 사무실 클립보드 ----------

        static OfficeUI BuildOffice(RectTransform canvasRoot)
        {
            var host = Stretch(UI("OfficeUI", canvasRoot));
            var office = host.gameObject.AddComponent<OfficeUI>();
            var rootImg = Img(host, "Root", new Color(0f, 0f, 0f, 0.55f));
            Stretch(rootImg.rectTransform);
            office.root = rootImg.gameObject;
            var root = rootImg.rectTransform;

            var board = Place(Img(root, "Clipboard", new Color(0.55f, 0.38f, 0.22f)).rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-200f, -10f), new Vector2(1260f, 990f));

            // 포스트잇 탭 (클립보드 우측)
            string[] tabNames = { "계약", "알바관리", "플레이어\n업그레이드" };
            Color[] tabColors = { new Color(1f, 0.92f, 0.45f), new Color(1f, 0.7f, 0.78f), new Color(0.65f, 0.85f, 1f) };
            office.tabButtons = new Button[3];
            office.tabRects = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var b = Btn(board, $"Tab_{i}", tabNames[i], tabColors[i], 28f);
                Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-24f, -90f - i * 135f), new Vector2(190f, 120f));
                office.tabButtons[i] = b;
                office.tabRects[i] = (RectTransform)b.transform;
            }

            var paper = Img(board, "Paper", new Color(0.99f, 0.98f, 0.94f));
            Stretch(paper.rectTransform, 26f, 26f, 26f, 60f);
            var clip = Img(board, "Clip", new Color(0.78f, 0.78f, 0.8f));
            Place(clip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(320f, 70f));

            office.closeButton = Btn(board, "CloseButton", "닫기", BtnRed, 26f);
            Place((RectTransform)office.closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -6f), new Vector2(140f, 48f));

            var pages = new GameObject[3];
            pages[0] = BuildContractPage(paper.rectTransform);
            var dragLayer = Stretch(UI("DragLayer", root));
            pages[1] = BuildWorkerPage(paper.rectTransform, dragLayer);
            pages[2] = BuildUpgradePage(paper.rectTransform);
            office.pages = pages;
            pages[1].SetActive(false);
            pages[2].SetActive(false);
            rootImg.gameObject.SetActive(false);
            return office;
        }

        static RectTransform Page(RectTransform paper, string name, string title)
        {
            var p = Stretch(UI(name, paper), 20f, 16f, 20f, 12f);
            var t = Txt(RowTop(UI("TitleArea", p), 0f, 56f, 0f), "Title", title, 40f, Ink);
            Stretch(t.rectTransform);
            t.fontStyle = FontStyles.Bold;
            return p;
        }

        static GameObject BuildContractPage(RectTransform paper)
        {
            var p = Page(paper, "ContractPage", "계약");
            var tab = p.gameObject.AddComponent<ContractTab>();

            tab.guideText = Txt(Place(UI("GuideArea", p), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-60f, 0f), new Vector2(620f, 56f)), "GuideText", "회사와 계약을 체결하세요", 34f, new Color(0.85f, 0.15f, 0.1f), TextAlignmentOptions.Center);
            Stretch(tab.guideText.rectTransform);
            tab.guideText.fontStyle = FontStyles.Bold;

            tab.rerollButton = Btn(p, "RerollButton", "리롤", BtnBlue, 28f);
            Place((RectTransform)tab.rerollButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(160f, 54f));
            tab.timerText = Txt(Place(UI("TimerArea", p), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, 0f), new Vector2(330f, 54f)), "TimerText", "자동 새로고침까지 05분 00초", 22f, Ink, TextAlignmentOptions.MidlineRight);
            Stretch(tab.timerText.rectTransform);

            tab.panels = new CompanyPanelRefs[3];
            for (int i = 0; i < 3; i++)
            {
                var bg = Img(p, $"CompanyPanel_{i}", PanelPaper);
                RowTop(bg.rectTransform, 72f + i * 152f, 142f, 0f);
                var r = new CompanyPanelRefs { root = bg.gameObject };
                r.nameText = TxtFill(Part(UI("NameArea", bg.transform), 0f, 0.2f), "Name", "회사", 32f, Ink, TextAlignmentOptions.Center, 0f);
                r.nameText.fontStyle = FontStyles.Bold;
                r.itemsText = TxtFill(Part(UI("ItemsArea", bg.transform), 0.2f, 0.66f), "Items", "", 24f, Ink, TextAlignmentOptions.MidlineLeft, 0f);
                r.feeText = TxtFill(Part(UI("FeeArea", bg.transform), 0.66f, 0.81f), "Fee", "", 26f, Ink, TextAlignmentOptions.Center, 0f);
                r.button = Btn(bg.transform, "ContractButton", "계약", BtnYellow, 32f, out r.buttonText);
                Part((RectTransform)r.button.transform, 0.82f, 0.99f, 0.12f, 0.88f, 6f);
                var dim = Img(bg.transform, "Dim", new Color(0f, 0f, 0f, 0.62f));
                Stretch(dim.rectTransform);
                TxtFill(dim.transform, "Text", "튜토리얼 중에는 지정된 회사만 계약할 수 있습니다", 26f, Color.white, TextAlignmentOptions.Center);
                dim.gameObject.SetActive(false);
                r.dim = dim.gameObject;
                tab.panels[i] = r;
            }

            tab.infoText = Txt(RowTop(UI("InfoArea", p), 536f, 46f, 0f), "InfoText", "계약 중 0/4", 28f, Ink);
            Stretch(tab.infoText.rectTransform);
            tab.infoText.fontStyle = FontStyles.Bold;

            tab.rows = new ContractRowRefs[4];
            for (int i = 0; i < 4; i++)
            {
                var bg = Img(p, $"ContractRow_{i}", new Color(0.86f, 0.93f, 0.86f));
                RowTop(bg.rectTransform, 588f + i * 62f, 56f, 0f);
                var r = new ContractRowRefs { root = bg.gameObject };
                r.text = TxtFill(Part(UI("TextArea", bg.transform), 0f, 0.84f), "Text", "", 24f, Ink, TextAlignmentOptions.MidlineLeft, 0f);
                r.cancelButton = Btn(bg.transform, "CancelButton", "해지", BtnRed, 24f);
                Part((RectTransform)r.cancelButton.transform, 0.85f, 0.99f, 0f, 1f, 5f);
                bg.gameObject.SetActive(false);
                tab.rows[i] = r;
            }
            return p.gameObject;
        }

        static GameObject BuildWorkerPage(RectTransform paper, RectTransform dragLayer)
        {
            var p = Page(paper, "WorkerPage", "알바관리");
            var tab = p.gameObject.AddComponent<WorkerTab>();
            tab.hireInfo = Txt(Place(UI("HireInfoArea", p), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, 0f), new Vector2(300f, 56f)), "HireInfo", "고용 0/10", 26f, Ink);
            Stretch(tab.hireInfo.rectTransform);

            // 스크롤 (위에서 아래로)
            var scrollRt = Stretch(UI("Scroll", p), 0f, 0f, 0f, 64f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            // RectMask2D 는 이 환경에서 자식이 청록색으로 깨져 보여서 스텐실 Mask 사용
            var viewport = Img(scrollRt, "Viewport", Color.white);
            Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = UI("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VLG(content.gameObject, 10f, 4).padding.right = 24;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var bar = Img(scrollRt, "Scrollbar", new Color(0f, 0f, 0f, 0.15f));
            bar.rectTransform.anchorMin = new Vector2(1f, 0f);
            bar.rectTransform.anchorMax = Vector2.one;
            bar.rectTransform.pivot = new Vector2(1f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(16f, 0f);
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            var handle = Img(bar.transform, "Handle", new Color(0.3f, 0.3f, 0.3f, 0.7f));
            Stretch(handle.rectTransform);
            sb.handleRect = handle.rectTransform;
            sb.targetGraphic = handle;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // ① 고용
            var hh = UI("HireHeader", content);
            LE(hh, 58f);
            TxtFill(hh, "Title", "<b>① 고용</b>", 30f, Ink, TextAlignmentOptions.MidlineLeft, 0f);
            tab.rerollButton = Btn(hh, "RerollButton", "리롤", BtnBlue, 26f);
            Place((RectTransform)tab.rerollButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(150f, 52f));
            tab.timerText = Txt(Place(UI("TimerArea", hh), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-160f, 0f), new Vector2(260f, 56f)), "TimerText", "", 20f, Ink, TextAlignmentOptions.MidlineRight);
            Stretch(tab.timerText.rectTransform);

            tab.candidates = new CandidateRefs[3];
            for (int i = 0; i < 3; i++)
            {
                var bg = Img(content, $"Candidate_{i}", PanelPaper);
                LE(bg, 150f);
                var r = new CandidateRefs { root = bg.gameObject };
                var photo = Img(bg.transform, "Photo", Color.white);
                Part(photo.rectTransform, 0.01f, 0.12f, 0.3f, 0.98f, 4f);
                r.photo = photo;
                r.photoLabel = TxtFill(photo.transform, "Label", "", 18f, Ink, TextAlignmentOptions.Center, 2f);
                r.nameText = TxtFill(Part(UI("NameArea", bg.transform), 0.01f, 0.12f, 0f, 0.3f, 2f), "Name", "", 22f, Ink, TextAlignmentOptions.Center, 0f);
                r.statsText = TxtFill(Part(UI("StatsArea", bg.transform), 0.15f, 0.7f), "Stats", "", 24f, Ink, TextAlignmentOptions.MidlineLeft, 0f);
                r.wageText = TxtFill(Part(UI("WageArea", bg.transform), 0.74f, 0.99f, 0.52f, 1f, 2f), "Wage", "", 22f, Ink, TextAlignmentOptions.Center, 0f);
                r.hireButton = Btn(bg.transform, "HireButton", "고용", BtnYellow, 28f, out r.hireLabel);
                Part((RectTransform)r.hireButton.transform, 0.76f, 0.97f, 0.06f, 0.5f, 4f);
                tab.candidates[i] = r;
            }

            // ② 배치
            var ah = UI("AssignHeader", content);
            LE(ah, 50f);
            TxtFill(ah, "Title", "<b>② 배치</b> — 이름표를 드래그해서 칸에 놓으세요 (구역당 최대 3명)", 26f, Ink, TextAlignmentOptions.MidlineLeft, 0f);

            var zonesRt = UI("Zones", content);
            LE(zonesRt, 520f);
            tab.zones = new ZoneDropSlot[7];
            for (int r = 0; r < 7; r++)
            {
                var bg = Img(zonesRt, $"Zone_{Names.Roles[r]}", r == 0 ? new Color(0.9f, 0.85f, 0.75f) : new Color(0.8f, 0.88f, 0.95f));
                if (r == 0) Part(bg.rectTransform, 0f, 1f, 0.7f, 1f, 4f);
                else
                {
                    int k = r - 1;
                    float x0 = (k % 3) / 3f, y1 = k < 3 ? 0.7f : 0.35f;
                    Part(bg.rectTransform, x0, x0 + 1f / 3f, y1 - 0.35f, y1, 4f);
                }
                var slot = bg.gameObject.AddComponent<ZoneDropSlot>();
                slot.role = (WorkRole)r;
                slot.title = Txt(RowTop(UI("TitleArea", bg.transform), 2f, 36f, 8f), "Title", Names.Roles[r], 24f, Ink);
                Stretch(slot.title.rectTransform);
                slot.title.fontStyle = FontStyles.Bold;
                var c = Stretch(UI("Content", bg.transform), 4f, 4f, 4f, 40f);
                var grid = c.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(r == 0 ? 120f : 118f, 40f);
                grid.spacing = new Vector2(6f, 6f);
                slot.content = c;
                tab.zones[r] = slot;
            }

            tab.tags = new NameTag[10];
            for (int i = 0; i < 10; i++)
            {
                var img = Img(tab.zones[0].content, $"NameTag_{i}", Color.white);
                var tag = img.gameObject.AddComponent<NameTag>();
                tag.slot = i;
                tag.background = img;
                tag.group = img.gameObject.AddComponent<CanvasGroup>();
                tag.dragLayer = dragLayer;
                tag.label = TxtFill(img.transform, "Label", "알바", 22f, Ink, TextAlignmentOptions.Center, 2f);
                img.gameObject.SetActive(false);
                tab.tags[i] = tag;
            }

            // ③ 상태
            var sh = UI("StatusHeader", content);
            LE(sh, 50f);
            TxtFill(sh, "Title", "<b>③ 알바 상태</b>", 30f, Ink, TextAlignmentOptions.MidlineLeft, 0f);
            tab.rows = new StatusRowRefs[10];
            for (int i = 0; i < 10; i++)
            {
                var bg = Img(content, $"Status_{i}", PanelPaper);
                LE(bg, 78f);
                var r = new StatusRowRefs { root = bg.gameObject };
                r.text = TxtFill(Part(UI("TextArea", bg.transform), 0f, 0.86f), "Text", "", 21f, Ink, TextAlignmentOptions.MidlineLeft, 0f);
                r.fireButton = Btn(bg.transform, "FireButton", "해고", BtnRed, 24f);
                Part((RectTransform)r.fireButton.transform, 0.87f, 0.99f, 0.1f, 0.9f, 4f);
                bg.gameObject.SetActive(false);
                tab.rows[i] = r;
            }
            return p.gameObject;
        }

        static GameObject BuildUpgradePage(RectTransform paper)
        {
            var p = Page(paper, "UpgradePage", "플레이어 업그레이드");
            var tab = p.gameObject.AddComponent<UpgradeTab>();
            tab.rows = new UpgradeRowRefs[3];
            for (int i = 0; i < 3; i++)
            {
                var bg = Img(p, $"Upgrade_{i}", PanelPaper);
                RowTop(bg.rectTransform, 80f + i * 200f, 180f, 0f);
                var r = new UpgradeRowRefs();
                r.titleText = TxtFill(Part(UI("TitleArea", bg.transform), 0f, 0.28f), "Title", "", 30f, Ink, TextAlignmentOptions.Center, 0f);
                r.currentText = TxtFill(Part(UI("CurArea", bg.transform), 0.28f, 0.44f), "Current", "", 28f, Ink, TextAlignmentOptions.Center, 0f);
                r.nextText = TxtFill(Part(UI("NextArea", bg.transform), 0.44f, 0.6f), "Next", "", 28f, new Color(0.1f, 0.45f, 0.1f), TextAlignmentOptions.Center, 0f);
                r.costText = TxtFill(Part(UI("CostArea", bg.transform), 0.6f, 0.78f), "Cost", "", 28f, Ink, TextAlignmentOptions.Center, 0f);
                r.button = Btn(bg.transform, "UpgradeButton", "업그레이드", BtnYellow, 28f);
                Part((RectTransform)r.button.transform, 0.8f, 0.99f, 0.2f, 0.8f, 6f);
                tab.rows[i] = r;
            }
            return p.gameObject;
        }

        // ---------- 디버그 / 설정 ----------

        static void BuildDebug(RectTransform root, GameConfig cfg)
        {
            var host = Place(UI("DebugPanel", root), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -105f), new Vector2(300f, 60f));
            var dp = host.gameObject.AddComponent<DebugPanel>();
            dp.toggleButton = Btn(host, "ToggleButton", "디버그 열기/닫기", new Color(0.8f, 0.8f, 0.8f), 22f);
            Stretch((RectTransform)dp.toggleButton.transform);

            var body = Img(host, "Body", Dark);
            var brt = body.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -6f);
            brt.sizeDelta = new Vector2(300f, 756f);
            VLG(body.gameObject, 6f, 8);
            dp.body = body.gameObject;

            Button B(string n, string l, Color c)
            {
                var b = Btn(body.transform, n, l, c, 22f);
                LE(b, 48f);
                return b;
            }
            dp.levelUpButton = B("LevelUp", "레벨업", BtnYellow);
            dp.addMoneyButton = B("AddMoney", $"돈 +{cfg.debugMoneySteps[0]:N0}", BtnYellow);
            dp.callTruckButton = B("CallTruck", "트럭 즉시 호출", BtnBlue);
            dp.createOrderButton = B("CreateOrder", "주문 즉시 생성", BtnBlue);
            // 배속 선택 1 / 2 / 3 / 4
            var speedRow = UI("SpeedRow", body.transform);
            LE(speedRow, 48f);
            var sg = speedRow.gameObject.AddComponent<GridLayoutGroup>();
            sg.cellSize = new Vector2(66f, 48f);
            sg.spacing = new Vector2(6f, 0f);
            int ns = cfg.debugSpeeds.Length;
            dp.speedButtons = new Button[ns];
            dp.speedImages = new Image[ns];
            for (int i = 0; i < ns; i++)
            {
                dp.speedButtons[i] = Btn(speedRow, $"Speed_{i}", $"{cfg.debugSpeeds[i]:0}배속", new Color(0.85f, 0.82f, 0.95f), 20f);
                dp.speedImages[i] = dp.speedButtons[i].GetComponent<Image>();
            }
            dp.resetButton = B("Reset", "게임 초기화", BtnRed);

            // 돈 수정 창 (작은 창)
            var mt = Txt(body.transform, "MoneyTitle", "돈 수정", 22f, Color.white, TextAlignmentOptions.Center);
            LE(mt, 34f);
            dp.moneyInput = Input(body.transform, "MoneyInput", "금액 입력");
            LE(dp.moneyInput, 48f);
            dp.applyMoneyButton = B("ApplyMoney", "적용", BtnYellow);

            var gridRt = UI("Steps", body.transform);
            LE(gridRt, 3 * 46f + 2 * 6f);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(138f, 46f);
            grid.spacing = new Vector2(6f, 6f);
            int n = cfg.debugMoneySteps.Length;
            dp.plusButtons = new Button[n];
            dp.minusButtons = new Button[n];
            for (int i = 0; i < n; i++)
            {
                dp.plusButtons[i] = Btn(gridRt, $"Plus_{i}", $"+{cfg.debugMoneySteps[i]:N0}", new Color(0.7f, 1f, 0.7f), 22f);
                dp.minusButtons[i] = Btn(gridRt, $"Minus_{i}", $"−{cfg.debugMoneySteps[i]:N0}", new Color(1f, 0.75f, 0.75f), 22f);
            }
        }

        static void BuildSettings(RectTransform root)
        {
            var host = Stretch(UI("Settings", root));
            var sp = host.gameObject.AddComponent<SettingsPanel>();
            sp.openButton = Btn(host, "SettingsButton", "설정", new Color(0.85f, 0.85f, 0.85f), 28f);
            Place((RectTransform)sp.openButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(150f, 70f));

            sp.panel = Modal(host, "SettingsPanel", new Vector2(520f, 380f), out var box);
            TxtFill(RowTop(UI("T", box), 20f, 60f), "Title", "설정", 40f, Ink, TextAlignmentOptions.Center, 0f);
            sp.quitButton = Btn(box, "QuitButton", "게임 종료", BtnRed, 32f);
            RowTop((RectTransform)sp.quitButton.transform, 120f, 90f, 60f);
            sp.closeButton = Btn(box, "CloseButton", "닫기", new Color(0.85f, 0.85f, 0.85f), 30f);
            RowTop((RectTransform)sp.closeButton.transform, 240f, 80f, 60f);

            sp.confirmPanel = Modal(host, "ConfirmPanel", new Vector2(600f, 320f), out var cbox);
            TxtFill(RowTop(UI("T", cbox), 30f, 120f), "Message", "게임을 종료할까요?", 36f, Ink, TextAlignmentOptions.Center, 0f);
            sp.confirmYes = Btn(cbox, "YesButton", "종료", BtnRed, 30f);
            Part((RectTransform)sp.confirmYes.transform, 0.08f, 0.46f, 0.1f, 0.38f, 0f);
            sp.confirmNo = Btn(cbox, "NoButton", "취소", new Color(0.85f, 0.85f, 0.85f), 30f);
            Part((RectTransform)sp.confirmNo.transform, 0.54f, 0.92f, 0.1f, 0.38f, 0f);
        }
    }
}
