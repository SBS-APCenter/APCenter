using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    [Serializable]
    public class CandidateRefs
    {
        public GameObject root;
        public Image photo;
        public TMP_Text photoLabel;
        public TMP_Text nameText;
        public TMP_Text statsText;
        public TMP_Text wageText;
        public Button hireButton;
        public TMP_Text hireLabel;
    }

    [Serializable]
    public class StatusRowRefs
    {
        public GameObject root;
        public TMP_Text text;
        public Button fireButton;
    }

    /// <summary>알바관리 탭: 고용(후보 3) / 배치(드래그 앤 드롭) / 상태(해고).</summary>
    public class WorkerTab : MonoBehaviour
    {
        public CandidateRefs[] candidates;
        public Button rerollButton;
        public TMP_Text timerText;
        public TMP_Text hireInfo;
        public ZoneDropSlot[] zones;     // index = (int)WorkRole (대기 0 ~ 상차 6)
        public NameTag[] tags;           // index = worker slot
        public StatusRowRefs[] rows;     // index = worker slot
        public Color[] modelColors = { new Color(1f, 0.75f, 0.85f), new Color(0.85f, 0.85f, 1f) };
        public static readonly string[] ModelNames = { "Macaron", "rabbit" };

        void Awake()
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                int k = i;
                candidates[i].hireButton.onClick.AddListener(() => GameManager.I.Hire(k));
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int k = i;
                rows[i].fireButton.onClick.AddListener(() => GameManager.I.Fire(GameManager.I.workers[k]));
            }
            rerollButton.onClick.AddListener(() => GameManager.I.RollCandidates());
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            var cfg = gm.config;

            timerText.text = "자동 새로고침까지\n" + ContractTab.TimeText(gm.DayRemaining);
            int hired = 0;
            foreach (var w in gm.workers) if (w.hired) hired++;
            hireInfo.text = $"고용 {hired}/{gm.workers.Length}";

            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];
                var d = gm.candidates[i];
                if (d == null)
                {
                    c.nameText.text = "-";
                    c.statsText.text = "고용 완료";
                    c.wageText.text = "";
                    c.photoLabel.text = "";
                    c.photo.color = Color.gray;
                    c.hireButton.interactable = false;
                    c.hireLabel.text = "고용됨";
                    continue;
                }
                c.photo.color = modelColors[d.modelIndex];
                c.photoLabel.text = ModelNames[d.modelIndex];
                c.nameText.text = d.name;
                c.statsText.text = // <pos> 로 칸 시작 위치를 맞춤 (힘 / 민첩 / 지능 글자 폭이 달라도 정렬)
                    $"힘<pos=2.6em>{Names.ColorStars(d.strength, cfg.workerStatMax, Names.StrColor)} {d.strength}\n" +
                    $"민첩<pos=2.6em>{Names.ColorStars(d.agility, cfg.workerStatMax, Names.AgiColor)} {d.agility}\n" +
                    $"지능<pos=2.6em>{Names.ColorStars(d.intelligence, cfg.workerStatMax, Names.IntColor)} {d.intelligence}";
                c.wageText.text = $"시급\n{Names.Money(gm.CandidateWage(d))}";
                c.hireButton.interactable = gm.CanHire(i);
                c.hireLabel.text = "고용";
            }

            for (int r = 0; r < zones.Length; r++)
            {
                var role = (WorkRole)r;
                zones[r].title.text = role == WorkRole.Waiting
                    ? $"대기칸 ({gm.CountRole(role)})"
                    : $"{Names.Role(role)} ({gm.CountRole(role)}/{cfg.zoneCapacity})";
            }

            for (int i = 0; i < tags.Length; i++)
            {
                var w = gm.workers[i];
                var tag = tags[i];
                if (tag.gameObject.activeSelf != w.hired) tag.gameObject.SetActive(w.hired);
                if (!w.hired) continue;
                tag.label.text = w.data.name;
                tag.background.color = modelColors[w.data.modelIndex];
                var content = zones[(int)w.role].content;
                if (!tag.dragging && tag.transform.parent != content) tag.transform.SetParent(content, false);
            }

            for (int i = 0; i < rows.Length; i++)
            {
                var w = gm.workers[i];
                if (rows[i].root.activeSelf != w.hired) rows[i].root.SetActive(w.hired);
                if (!w.hired) continue;
                var d = w.data;
                int stressCells = Mathf.RoundToInt(w.stress / cfg.stressMax * 10f);
                rows[i].text.text =
                    $"<b>{d.name}</b> ({ModelNames[d.modelIndex]})   배치: {Names.Role(w.role)}   시급 {Names.Money(w.Wage)}" +
                    $"<pos=58%>스트레스<pos=68%>{Names.ColorStars(stressCells, 10, w.resting ? "#D03030" : Names.StressColor)} {Mathf.FloorToInt(w.stress)}" +
                    (w.resting ? " <color=#D03030><b>휴식</b></color>" : "") + "\n" +
                    $"<pos=0%>힘<pos=5%>{Names.ColorStars(d.strength, cfg.workerStatMax, Names.StrColor)} {d.strength}" +
                    $"<pos=24%>민첩<pos=30%>{Names.ColorStars(d.agility, cfg.workerStatMax, Names.AgiColor)} {d.agility}" +
                    $"<pos=49%>지능<pos=55%>{Names.ColorStars(d.intelligence, cfg.workerStatMax, Names.IntColor)} {d.intelligence}";
            }
        }
    }
}
