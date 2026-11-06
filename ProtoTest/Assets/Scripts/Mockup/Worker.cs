using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>
    /// 알바 1명 (씬에 풀로 미리 배치, 고용 시 활성). 배치 구역의 작업 발판으로 이동해 자동 작업.
    /// 일하는 동안 스트레스 증가, 최대치면 휴식 후 0 (4-5장).
    /// </summary>
    public class Worker : Actor
    {
        public int slot;
        public GameObject[] models;     // 0 = Macaron, 1 = rabbit
        public CharacterAnim[] anims;
        public TMP_Text nameText;

        [System.NonSerialized] public bool hired;
        [System.NonSerialized] public WorkerData data;
        [System.NonSerialized] public WorkRole role;
        [System.NonSerialized] public float stress;
        [System.NonSerialized] public bool resting;
        float restTimer;
        WorkPad target;

        GameConfig Cfg => GameManager.I.config;
        public override int Capacity => Mathf.Max(1, data.strength);
        public override float MoveSpeed => Cfg.workerSpeedBase + Cfg.workerSpeedPerAgility * data.agility;
        public override float WorkTime => Mathf.Max(Cfg.workerWorkTimeMin, Cfg.workerWorkTimeBase - Cfg.workerWorkTimePerInt * data.intelligence);
        public override WorkRole Role => role;
        public int Wage => data.StatSum * Cfg.wagePerStat;

        public void Hire(WorkerData d, Vector3 pos)
        {
            data = d;
            hired = true;
            role = WorkRole.Waiting;
            stress = 0f;
            resting = false;
            target = null;
            ClearCarry();
            for (int i = 0; i < models.Length; i++) models[i].SetActive(i == d.modelIndex);
            anim = anims[d.modelIndex];
            transform.position = pos;
            gameObject.SetActive(true);
        }

        public void Fire()
        {
            Release();
            hired = false;
            ClearCarry();
            gameObject.SetActive(false);
        }

        public void SetRole(WorkRole r)
        {
            if (r == role) return;
            Release();
            GameManager.I.ReturnCarried(this);
            role = r;
        }

        void Release()
        {
            if (target != null && target.occupant == this) target.occupant = null;
            target = null;
        }

        bool Valid(WorkPad p) => p != null && p.Usable && (p.occupant == null || p.occupant == this) && p.CanWork(this);

        void Update() => Tick(Time.deltaTime);

        /// <summary>AI 1스텝. 테스트에서 직접 호출 가능.</summary>
        public void Tick(float dt)
        {
            if (!hired || dt <= 0f) return;
            var gm = GameManager.I;
            IsWorking = false;

            if (resting)
            {
                IsMoving = false;
                restTimer -= dt;
                if (restTimer <= 0f)
                {
                    resting = false;
                    stress = 0f;
                }
                return;
            }

            if (role == WorkRole.Waiting)
            {
                Release();
                MoveTo(gm.WaitSpot(this), dt);
                return;
            }

            if (!Valid(target))
            {
                Release();
                target = gm.FindTargetPad(this);
                if (target != null) target.occupant = this;
            }
            if (target == null)
            {
                IsMoving = false; // 할 일 없음: 제자리 대기
                return;
            }

            if (MoveTo(target.transform.position, dt))
            {
                TickWork(dt, target);
                if (!IsWorking) Release();
            }

            stress += Cfg.stressPerSecond * dt;
            if (stress >= Cfg.stressMax)
            {
                stress = Cfg.stressMax;
                resting = true;
                restTimer = Cfg.restSeconds;
                Release();
            }
        }

        /// <summary>목표 지점으로 직선 이동. 도착하면 true.</summary>
        bool MoveTo(Vector3 pos, float dt)
        {
            Vector3 d = pos - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.1f)
            {
                IsMoving = false;
                return true;
            }
            IsMoving = true;
            float step = MoveSpeed * dt;
            transform.position += d / dist * Mathf.Min(step, dist);
            Face(d);
            return false;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (nameText != null && data != null)
                nameText.text = resting ? $"{data.name}\n<color=#ff6060>휴식 중</color>" : $"{data.name} ({Names.Role(role)})";
        }
    }
}
