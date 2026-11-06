using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    [System.Serializable]
    public class OrderCardRefs
    {
        public GameObject root;
        public Image bg;
        public TMP_Text idText;
        public Image[] icons;      // 물품 이모지 (최대 6종)
        public TMP_Text[] counts;
    }

    /// <summary>상단 HUD (레벨 / 돈 / 경험치) + 주문서 카드 + 들고 있는 택배 표시.</summary>
    public class HUD : MonoBehaviour
    {
        public TMP_Text levelText;
        public TMP_Text moneyText;
        public TMP_Text expText;
        public Image expFill;
        public GameObject nextRegionBanner;
        public TMP_Text orderTitle;
        public OrderCardRefs[] orderCards;
        public TMP_Text carryText;

        public static readonly Color OrderNormal = new Color(1f, 0.99f, 0.93f), OrderReady = new Color(0.8f, 1f, 0.8f);

        readonly StringBuilder sb = new StringBuilder();

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            var cfg = gm.config;

            levelText.text = $"Lv.{gm.level}";
            moneyText.text = gm.money < 0 ? $"<color=#ff5050>{Names.Money(gm.money)}</color>" : Names.Money(gm.money);
            bool max = gm.level >= cfg.maxLevel;
            expText.text = max ? "MAX" : $"{gm.exp} / {gm.ExpToNext}";
            expFill.fillAmount = max ? 1f : gm.exp / (float)gm.ExpToNext;
            bool banner = gm.level >= cfg.nextRegionLevel;
            if (nextRegionBanner.activeSelf != banner) nextRegionBanner.SetActive(banner);

            orderTitle.text = $"주문서 {gm.orders.Count}/{cfg.orderMax}";
            for (int i = 0; i < orderCards.Length; i++)
            {
                var c = orderCards[i];
                bool on = i < gm.orders.Count;
                if (c.root.activeSelf != on) c.root.SetActive(on);
                if (!on) continue;
                var o = gm.orders[i];
                bool ready = false;
                foreach (var t in gm.tables) if (t.cell.unlocked && gm.Packable(t, o)) ready = true;
                c.bg.color = ready ? OrderReady : OrderNormal;
                c.idText.text = ready ? $"#{o.id} <color=#2a8a2a>포장 가능</color>" : $"#{o.id}";
                for (int k = 0; k < c.icons.Length; k++)
                {
                    bool has = k < o.lines.Count;
                    if (c.icons[k].gameObject.activeSelf != has) c.icons[k].gameObject.SetActive(has);
                    if (c.counts[k].gameObject.activeSelf != has) c.counts[k].gameObject.SetActive(has);
                    if (!has) continue;
                    var def = cfg.FindItem(o.lines[k].item);
                    c.icons[k].sprite = def != null && def.icon != null ? def.icon : cfg.unknownIcon;
                    c.counts[k].text = "×" + o.lines[k].count;
                }
            }

            var p = gm.player;
            sb.Clear();
            sb.Append($"들고 있음 {p.carried.Count}/{p.Capacity}");
            if (p.carried.Count > 0)
            {
                sb.Append(p.carryKind == CarryKind.Truck ? "  (하차: 도크에 놓기)  " : p.carryKind == CarryKind.Raw ? "  (분류용)  "
                    : p.carryKind == CarryKind.Packed ? $"  (포장 택배 {p.carried.Count}개: 트럭에 싣기)  " : "  (운반용·검수 완료)  ");
                foreach (var it in cfg.companies)
                {
                    int a = p.CountCarried(it.itemA.name), b = p.CountCarried(it.itemB.name);
                    if (a > 0) sb.Append($"{it.itemA.name}{a} ");
                    if (b > 0) sb.Append($"{it.itemB.name}{b} ");
                }
            }
            if (p.PadLocked) sb.Append("\n<color=#ffd060>다음 작업: 발판에서 내려왔다 다시 올라오세요</color>");
            carryText.text = sb.ToString();
        }
    }
}
