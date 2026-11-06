using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    [Serializable]
    public class UpgradeRowRefs
    {
        public TMP_Text titleText;
        public TMP_Text currentText;
        public TMP_Text nextText;
        public TMP_Text costText;
        public Button button;
    }

    /// <summary>플레이어 업그레이드 탭: 들 수 있는 개수 / 이동속도 / 작업속도 (최대 5단계).</summary>
    public class UpgradeTab : MonoBehaviour
    {
        public UpgradeRowRefs[] rows; // 0 개수, 1 이동속도, 2 작업 1회 시간
        static readonly string[] Titles = { "들 수 있는 택배", "이동속도", "작업 1회 시간" };

        void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int k = i;
                rows[i].button.onClick.AddListener(() => GameManager.I.Upgrade(k));
            }
        }

        static string Fmt(int kind, float v) => kind == 0 ? $"{v:0}개" : kind == 1 ? $"{v:0.0}" : $"{v:0.0}초";

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                var steps = gm.Steps(i);
                int lv = gm.UpgradeLevel(i);
                bool max = lv + 1 >= steps.Count;
                var r = rows[i];
                r.titleText.text = $"{Titles[i]}\n<size=70%>{lv + 1}단계 / {steps.Count}단계</size>";
                r.currentText.text = "현재\n" + Fmt(i, steps[lv].value);
                r.nextText.text = max ? "다음\nMAX" : "다음\n" + Fmt(i, steps[lv + 1].value);
                r.costText.text = max ? "" : "비용\n" + Names.Money(steps[lv + 1].cost);
                r.button.interactable = gm.CanUpgrade(i);
            }
        }
    }
}
