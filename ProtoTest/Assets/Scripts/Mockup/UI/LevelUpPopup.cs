using System.Text;
using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>레벨업 시 해금/보상 내용을 잠깐 띄우는 팝업 (4-6장 레벨 보상). 씬에 미리 배치, 비활성 → Show.</summary>
    public class LevelUpPopup : MonoBehaviour
    {
        public GameObject root;
        public RectTransform box;
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public float showSeconds = 3f;

        float timer;

        public void Show(int level)
        {
            titleText.text = $"레벨 {level} 달성!";
            bodyText.text = Describe(level);
            root.SetActive(true);
            timer = showSeconds;
        }

        static string Describe(int level)
        {
            var gm = GameManager.I;
            var cfg = gm.config;
            var sb = new StringBuilder();
            foreach (var u in cfg.shelfUpgrades)
                if (u.level == level) sb.AppendLine($"선반 {u.shelfA + 1}·{u.shelfB + 1}번 용량 {cfg.shelfBaseCapacity} → {cfg.shelfUpgradedCapacity}");
            foreach (var c in gm.cells)
                if (c.unlockLevel == level) sb.AppendLine($"{c.displayName} 해금 가능 (해금 발판 등장)");
            if (level == cfg.extraTruckLevel) sb.AppendLine("하차구역 칸마다 입고 차량 +1대");
            if (level == cfg.orderBoostLevel) sb.AppendLine($"주문량 증가 (물품 개수 ×{cfg.orderBoostMultiplier:0.#})");
            if (level == cfg.nextRegionLevel) sb.AppendLine("다음 지역 해금! (지도에서 확인)");
            if (sb.Length == 0) sb.Append("새 해금 없음");
            return sb.ToString().TrimEnd();
        }

        void Update()
        {
            if (!root.activeSelf) return;
            timer -= Time.unscaledDeltaTime;
            float t = showSeconds - timer;
            if (box != null) box.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(t / 0.2f)); // 살짝 튀어나오는 연출
            if (timer <= 0f) root.SetActive(false);
        }
    }
}
