using System.Collections.Generic;
using UnityEngine;

namespace Mockup
{
    /// <summary>플레이어 / 알바 공통: 들고 있는 택배, 작업 발판 처리, 애니메이션.</summary>
    public abstract class Actor : MonoBehaviour
    {
        public CharacterAnim anim;
        public GameObject[] carryBoxes;
        public Transform overheadRoot;   // 카메라를 향하게 회전
        public Transform workBarRoot;
        public Transform workBarFill;

        [Header("말풍선 (들고 있는 물품 이모지)")]
        public GameObject bubbleRoot;
        public SpriteRenderer[] bubbleIcons;
        public TMPro.TMP_Text[] bubbleCounts;
        public SpriteRenderer bubbleBg;

        [System.NonSerialized] public List<string> carried = new List<string>();
        [System.NonSerialized] public CarryKind carryKind;
        [System.NonSerialized] public List<PackedParcel> packedCarried = new List<PackedParcel>(); // carryKind == Packed 일 때 carried 와 같은 순서

        public abstract int Capacity { get; }
        public abstract float MoveSpeed { get; }
        public abstract float WorkTime { get; }
        public abstract WorkRole Role { get; }

        public int Room => Mathf.Max(0, Capacity - carried.Count);
        public bool IsMoving { get; protected set; }
        public bool IsWorking { get; protected set; }

        protected WorkPad currentPad;
        protected float workTimer;

        public int CountCarried(string item)
        {
            int n = 0;
            foreach (var c in carried) if (c == item) n++;
            return n;
        }

        public void ClearCarry()
        {
            carried.Clear();
            packedCarried.Clear();
            carryKind = CarryKind.None;
        }

        public void NormalizeCarry()
        {
            if (carried.Count == 0) carryKind = CarryKind.None;
        }

        protected WorkRole currentAction;
        protected float actionDuration;

        /// <summary>옮기기 = transferInterval 마다 1개, 검수 = 작업시간 × inspectTimeScale, 포장 = 작업시간.</summary>
        float Duration(WorkRole r)
        {
            var cfg = GameManager.I.config;
            if (WorkPad.IsTransfer(r)) return cfg.transferInterval;
            return r == WorkRole.Inspect ? WorkTime * cfg.inspectTimeScale : WorkTime;
        }

        WorkRole lastActionOnPad;

        /// <summary>플레이어: 한 발판에서 동작이 끝난 뒤 다른 동작은 발판에서 내려왔다 다시 올라와야 시작.</summary>
        public bool PadLocked { get; private set; }

        /// <summary>현재 발판을 밟은 순간 무언가 들고 있었는지.</summary>
        public bool EnteredCarrying { get; private set; }

        protected void TickWork(float dt, WorkPad pad)
        {
            if (pad != currentPad)
            {
                currentPad = pad;
                lastActionOnPad = WorkRole.Waiting;
                PadLocked = false;
                EnteredCarrying = carried.Count > 0; // 발판을 밟은 순간 상태 (도크: 들고 있으면 놓기만, 빈손이면 들기만)
            }
            var action = pad != null ? pad.Pick(this) : WorkRole.Waiting;
            if (Role == WorkRole.Player && action != WorkRole.Waiting && lastActionOnPad != WorkRole.Waiting && action != lastActionOnPad)
                PadLocked = true;
            if (PadLocked) action = WorkRole.Waiting;
            if (action != WorkRole.Waiting) lastActionOnPad = action;
            if (action != currentAction)
            {
                currentAction = action;
                workTimer = 0f;
            }
            IsWorking = action != WorkRole.Waiting;
            if (!IsWorking)
            {
                workTimer = 0f;
                return;
            }
            actionDuration = Duration(action);
            workTimer += dt;
            if (workTimer >= actionDuration)
            {
                workTimer -= actionDuration;
                pad.DoWork(this);
                if (!pad.CanWork(this)) workTimer = 0f;
            }
        }

        protected void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        protected virtual void LateUpdate()
        {
            for (int i = 0; i < carryBoxes.Length; i++)
            {
                bool on = i < carried.Count;
                if (carryBoxes[i].activeSelf != on) carryBoxes[i].SetActive(on);
            }
            if (anim != null) anim.Set(IsMoving, carried.Count > 0);

            var gm = GameManager.I;
            if (overheadRoot != null && gm != null) overheadRoot.rotation = gm.mainCamera.transform.rotation;
            if (workBarRoot != null)
            {
                bool show = IsWorking && !WorkPad.IsTransfer(currentAction) && actionDuration > 0f; // 게이지는 검수/포장만
                if (workBarRoot.gameObject.activeSelf != show) workBarRoot.gameObject.SetActive(show);
                if (show) workBarFill.localScale = new Vector3(Mathf.Clamp01(workTimer / actionDuration), 1f, 1f);
            }
            UpdateBubble(gm);
        }

        static readonly List<string> distinct = new List<string>();
        static readonly Color BubbleTruck = new Color(0.8f, 0.9f, 1f), BubbleRaw = new Color(1f, 0.97f, 0.8f), BubbleChecked = new Color(0.85f, 1f, 0.85f), BubblePacked = new Color(1f, 0.88f, 0.75f);

        /// <summary>들고 있는 물품 종류별 이모지 + 개수. 배경색: 하차(파랑) / 분류(노랑) / 검수 완료(초록).</summary>
        void UpdateBubble(GameManager gm)
        {
            if (bubbleRoot == null || gm == null) return;
            bool show = carried.Count > 0;
            if (bubbleRoot.activeSelf != show) bubbleRoot.SetActive(show);
            if (!show) return;
            distinct.Clear();
            foreach (var c in carried) if (!distinct.Contains(c)) distinct.Add(c);
            for (int i = 0; i < bubbleIcons.Length; i++)
            {
                bool on = i < distinct.Count;
                if (bubbleIcons[i].gameObject.activeSelf != on) bubbleIcons[i].gameObject.SetActive(on);
                if (bubbleCounts[i].gameObject.activeSelf != on) bubbleCounts[i].gameObject.SetActive(on);
                if (!on) continue;
                var def = gm.config.FindItem(distinct[i]);
                bubbleIcons[i].sprite = distinct[i] == Names.Packed ? gm.config.packedIcon
                    : def != null && def.icon != null ? def.icon : gm.config.unknownIcon;
                bubbleCounts[i].text = "×" + CountCarried(distinct[i]);
            }
            if (bubbleBg != null)
                bubbleBg.color = carryKind == CarryKind.Truck ? BubbleTruck : carryKind == CarryKind.Raw ? BubbleRaw
                    : carryKind == CarryKind.Packed ? BubblePacked : BubbleChecked;
        }
    }
}
