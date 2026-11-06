using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    [Serializable]
    public class CompanyPanelRefs
    {
        public GameObject root;
        public TMP_Text nameText;
        public TMP_Text itemsText;
        public TMP_Text feeText;
        public Button button;
        public TMP_Text buttonText;
        public GameObject dim;
    }

    [Serializable]
    public class ContractRowRefs
    {
        public GameObject root;
        public TMP_Text text;
        public Button cancelButton;
    }

    /// <summary>계약 탭: 회사 패널 3개(≡) + 계약 중 목록(최대 4) + 리롤.</summary>
    public class ContractTab : MonoBehaviour
    {
        public TMP_Text guideText;
        public TMP_Text infoText;
        public CompanyPanelRefs[] panels;
        public ContractRowRefs[] rows;
        public Button rerollButton;
        public TMP_Text timerText;

        void Awake()
        {
            for (int i = 0; i < panels.Length; i++)
            {
                int k = i;
                panels[i].button.onClick.AddListener(() => GameManager.I.Contract(k));
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int k = i;
                rows[i].cancelButton.onClick.AddListener(() => GameManager.I.CancelContract(k));
            }
            rerollButton.onClick.AddListener(() => GameManager.I.RerollOffers());
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;

            guideText.gameObject.SetActive(!gm.tutorialDone);
            infoText.text = $"계약 중 {gm.contracts.Count}/{gm.MaxContracts}";
            rerollButton.interactable = gm.tutorialDone;
            timerText.text = gm.tutorialDone ? "자동 새로고침까지 " + TimeText(gm.DayRemaining) : "튜토리얼 중 리롤 불가";

            for (int i = 0; i < panels.Length; i++)
            {
                var p = panels[i];
                var def = gm.offers[i];
                bool locked = gm.OfferLocked(i);
                p.dim.SetActive(locked || def == null);
                if (def == null)
                {
                    p.nameText.text = "(회사 없음)";
                    p.itemsText.text = "";
                    p.feeText.text = "";
                    p.button.interactable = false;
                    p.buttonText.text = "-";
                    continue;
                }
                p.nameText.text = def.name;
                p.itemsText.text = ItemLine(def.itemA, gm) + "\n" + ItemLine(def.itemB, gm);
                p.feeText.text = $"계약금\n{Names.Money(def.contractFee)}";
                bool done = gm.IsContracted(def);
                p.button.interactable = gm.CanContract(i);
                p.buttonText.text = locked ? "잠김" : done ? "계약됨" : "계약";
            }

            for (int i = 0; i < rows.Length; i++)
            {
                bool on = i < gm.contracts.Count;
                if (rows[i].root.activeSelf != on) rows[i].root.SetActive(on);
                if (!on) continue;
                var c = gm.contracts[i];
                rows[i].text.text = $"<b>{c.company.name}</b>   {c.company.itemA.name} · {c.company.itemB.name}   선반 {c.shelfA + 1} · {c.shelfB + 1}";
            }
        }

        static string ItemLine(ItemDef it, GameManager gm) =>
            $"{it.name}   가격 {Names.Money(it.price)}   주문빈도 {Names.Stars(it.frequency, 5)} ({it.frequency})";

        public static string TimeText(float sec)
        {
            int s = Mathf.CeilToInt(sec);
            return $"{s / 60:00}분 {s % 60:00}초";
        }
    }
}
