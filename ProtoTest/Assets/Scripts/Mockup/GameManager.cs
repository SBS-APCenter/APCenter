using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mockup
{
    public class Contract
    {
        public CompanyDef company;
        public int shelfA, shelfB;
    }

    /// <summary>
    /// 목업 규칙의 중심. 씬에 미리 배치된 오브젝트를 참조만 하고 값/활성 상태만 바꾼다 (5-7장).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Header("설정")]
        public GameConfig config;
        public Camera mainCamera;

        [Header("씬 참조")]
        public PlayerController player;
        public Worker[] workers;
        public Transform[] waitSpots;
        public Shelf[] shelves;
        public UnloadBay[] unloadBays;
        public PackingTable[] tables;
        public ShipTruck[] shipTrucks;
        public WorkPad[] pads;
        public ZoneCell[] cells;
        public UnlockPad[] unlockPads;
        public Transform officeEntry;
        public float officeEntryRadius = 1.2f;
        public OfficeUI officeUI;
        public LevelUpPopup levelUpPopup;

        // ---- 상태 ----
        [System.NonSerialized] public int money, exp, level = 1;
        [System.NonSerialized] public List<Contract> contracts = new List<Contract>();
        [System.NonSerialized] public CompanyDef[] offers = new CompanyDef[3];
        [System.NonSerialized] public WorkerData[] candidates = new WorkerData[3];
        [System.NonSerialized] public List<Order> orders = new List<Order>();
        [System.NonSerialized] public bool tutorialDone;
        [System.NonSerialized] public int carryLv, speedLv, workLv;
        [System.NonSerialized] public float dayTimer;
        [System.NonSerialized] public float timeSpeed = 1f; // 디버그 배속 (사무실 UI 닫을 때 이 값으로 복귀)
        float orderTimer;
        int nextOrderId = 1;
        bool playerInOffice;

        public bool OfficeOpen => officeUI != null && officeUI.IsOpen;
        public int PlayerCapacity => (int)config.carrySteps[carryLv].value;
        public float PlayerSpeed => config.speedSteps[speedLv].value;
        public float PlayerWorkTime => config.workTimeSteps[workLv].value;
        public int ExpToNext => config.expPerLevelFactor * level;
        public int MaxContracts => shelves.Length / 2;
        public float DayRemaining => Mathf.Max(0f, config.dayLengthSeconds - dayTimer);

        public PackingTable PrimaryTable
        {
            get
            {
                foreach (var t in tables) if (t.cell.unlocked) return t;
                return null;
            }
        }

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
        }

        void Start()
        {
            money = config.startMoney;
            PackingTable.ResetShared();
            foreach (var c in cells) c.SetUnlocked(c.unlockLevel == 0);
            foreach (var s in shelves) s.Clear();
            foreach (var b in unloadBays)
                foreach (var t in b.trucks) t.ResetState(config.truckFirstArrivalDelay);
            foreach (var t in shipTrucks) t.ResetState();
            foreach (var w in workers) w.gameObject.SetActive(false);

            // 튜토리얼: 지정 2개 회사 + 잠긴 1개 패널 (3장)
            offers[0] = config.companies[0];
            offers[1] = config.companies[1];
            offers[2] = config.companies.Count > 2 ? config.companies[2 + Random.Range(0, config.companies.Count - 2)] : null;
            RollCandidates();
            ApplyLevelState();

            player.transform.position = officeEntry.position;
            playerInOffice = true;
            officeUI.Open(0);
        }

        void Update()
        {
            Tick(Time.deltaTime); // 사무실 UI 열림 중엔 timeScale 0 → 모든 타이머 정지

            // 사무실 구역에 들어가면 바로 클립보드 UI (구역을 나갔다가 다시 들어와야 재오픈)
            bool inOffice = false;
            foreach (var c in cells) if (c.type == ZoneType.Office && c.Contains(player.transform.position, 0f)) inOffice = true;
            if (inOffice && !playerInOffice && !OfficeOpen) officeUI.Open(-1);
            playerInOffice = inOffice;
        }

        /// <summary>게임 타이머 진행 (트럭 / 주문 / 하루). 테스트에서 직접 호출 가능.</summary>
        public void Tick(float dt)
        {
            if (dt > 0f)
            {
                foreach (var b in unloadBays)
                {
                    if (!b.cell.unlocked) continue;
                    for (int i = 0; i < TrucksPerBay; i++) b.trucks[i].Tick(dt, this);
                }
                foreach (var t in shipTrucks) if (t.table.cell.unlocked) t.Tick(dt);

                if (contracts.Count > 0)
                {
                    orderTimer += dt;
                    if (orderTimer >= config.orderInterval)
                    {
                        orderTimer = 0f;
                        CreateOrder();
                    }
                }

                dayTimer += dt;
                if (dayTimer >= config.dayLengthSeconds)
                {
                    dayTimer = 0f;
                    OnDayPassed();
                }
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        int TrucksPerBay => level >= config.extraTruckLevel ? 2 : 1;

        void OnDayPassed()
        {
            int wages = 0;
            foreach (var w in workers) if (w.hired) wages += w.Wage * config.wageDayMultiplier;
            money -= wages; // 인건비 (임시: 시급 × 8)
            RollCandidates();
            RerollOffers();
        }

        // ================= 레벨 =================

        public void AddExp(int amount)
        {
            exp += amount;
            while (level < config.maxLevel && exp >= ExpToNext)
            {
                exp -= ExpToNext;
                level++;
                if (levelUpPopup != null) levelUpPopup.Show(level);
            }
            if (level >= config.maxLevel) exp = Mathf.Min(exp, ExpToNext);
            ApplyLevelState();
        }

        public void DebugLevelUp()
        {
            if (level >= config.maxLevel) return;
            level++;
            exp = 0;
            ApplyLevelState();
            if (levelUpPopup != null) levelUpPopup.Show(level);
        }

        /// <summary>현재 레벨 기준으로 보상 상태를 다시 맞춘다 (반복 호출해도 같은 결과).</summary>
        public void ApplyLevelState()
        {
            foreach (var s in shelves) s.capacity = config.shelfBaseCapacity;
            foreach (var u in config.shelfUpgrades)
            {
                if (level < u.level) continue;
                shelves[u.shelfA].capacity = config.shelfUpgradedCapacity;
                shelves[u.shelfB].capacity = config.shelfUpgradedCapacity;
            }
            foreach (var b in unloadBays)
                for (int i = 0; i < b.trucks.Length; i++)
                {
                    bool on = i < TrucksPerBay;
                    if (b.trucks[i].gameObject.activeSelf != on) b.trucks[i].gameObject.SetActive(on);
                }
            foreach (var p in unlockPads)
                p.gameObject.SetActive(!p.cell.unlocked && level >= p.cell.unlockLevel);
            UpdateWalls();
        }

        /// <summary>열린 칸의 가장자리 중 이웃도 열린 쪽은 벽을 없애 연결, 나머지는 벽 (트럭 진입구는 벽 오브젝트에 틈으로 있음).</summary>
        void UpdateWalls()
        {
            foreach (var c in cells)
                for (int d = 0; d < 4; d++)
                {
                    var w = c.walls[d];
                    if (w == null) continue;
                    var n = CellAt(c.grid + ZoneCell.Dirs[d]);
                    bool on = c.unlocked && (n == null || !n.unlocked);
                    if (w.activeSelf != on) w.SetActive(on);
                }
        }

        ZoneCell CellAt(Vector2Int g)
        {
            foreach (var c in cells) if (c.grid == g) return c;
            return null;
        }

        public void UnlockCell(ZoneCell cell)
        {
            if (cell.unlocked) return;
            cell.SetUnlocked(true);
            ApplyLevelState();
        }

        public bool IsWalkable(Vector3 p)
        {
            foreach (var c in cells)
                if (c.unlocked && c.Contains(p, -0.05f)) return true; // 음수 여백: 칸 경계에서 끊기지 않게 (잠긴 칸은 벽 너머)
            return false;
        }

        // ================= 계약 (3, 4-2, 5-1장) =================

        public List<ItemDef> ContractedItems()
        {
            var list = new List<ItemDef>();
            foreach (var c in contracts)
            {
                list.Add(c.company.itemA);
                list.Add(c.company.itemB);
            }
            return list;
        }

        public bool IsContracted(CompanyDef def)
        {
            foreach (var c in contracts) if (c.company == def) return true;
            return false;
        }

        int EmptyShelfCount()
        {
            int n = 0;
            foreach (var s in shelves) if (!s.HasLabel) n++;
            return n;
        }

        public bool OfferLocked(int i) => !tutorialDone && i >= 2;

        public bool CanContract(int i)
        {
            var def = offers[i];
            return def != null && !OfferLocked(i) && !IsContracted(def) && money >= def.contractFee
                   && contracts.Count < MaxContracts && EmptyShelfCount() >= 2;
        }

        public void Contract(int i)
        {
            if (!CanContract(i)) return;
            var def = offers[i];
            money -= def.contractFee;
            var c = new Contract { company = def, shelfA = -1, shelfB = -1 };
            foreach (var s in shelves)
            {
                if (s.HasLabel) continue;
                if (c.shelfA < 0) { c.shelfA = s.index; s.label = def.itemA.name; }
                else { c.shelfB = s.index; s.label = def.itemB.name; break; }
            }
            contracts.Add(c);
            if (!tutorialDone && IsContracted(config.companies[0]) && IsContracted(config.companies[1]))
                tutorialDone = true; // 튜토리얼은 계약까지만
        }

        /// <summary>계약 해지: 라벨 제거. 선반·도크·트럭·포장대·주문의 해당 물품은 소멸 (10장 25 임시 해석).</summary>
        public void CancelContract(int contractIndex)
        {
            if (contractIndex < 0 || contractIndex >= contracts.Count) return;
            var c = contracts[contractIndex];
            contracts.RemoveAt(contractIndex);
            var gone = new HashSet<string> { c.company.itemA.name, c.company.itemB.name };

            shelves[c.shelfA].Clear();
            shelves[c.shelfB].Clear();
            foreach (var b in unloadBays)
            {
                b.dock.parcels.RemoveAll(gone.Contains);
                foreach (var t in b.trucks) t.cargo.RemoveAll(gone.Contains);
            }
            foreach (var t in tables)
                foreach (var g in gone) t.staging.Remove(g);
            foreach (var a in AllActors())
            {
                a.carried.RemoveAll(gone.Contains);
                a.NormalizeCarry();
            }
            foreach (var o in orders) o.lines.RemoveAll(l => gone.Contains(l.item));
            orders.RemoveAll(o => o.lines.Count == 0);
        }

        public void RerollOffers()
        {
            if (!tutorialDone) return; // 튜토리얼 중 리롤 없음
            var pool = new List<CompanyDef>();
            foreach (var c in config.companies) if (!IsContracted(c)) pool.Add(c);
            for (int i = 0; i < 3; i++)
            {
                if (pool.Count == 0) { offers[i] = null; continue; }
                int k = Random.Range(0, pool.Count);
                offers[i] = pool[k];
                pool.RemoveAt(k);
            }
        }

        // ================= 알바 (4-5장) =================

        public void RollCandidates()
        {
            for (int i = 0; i < 3; i++)
            {
                int max = config.workerStatMax;
                candidates[i] = new WorkerData
                {
                    name = Rng.Pick(config.workerNames),
                    strength = Random.Range(1, max + 1),
                    agility = Random.Range(1, max + 1),
                    intelligence = Random.Range(1, max + 1),
                    modelIndex = Random.Range(0, 2),
                };
            }
        }

        public int CandidateWage(WorkerData d) => d.StatSum * config.wagePerStat;

        public bool CanHire(int i) => candidates[i] != null && FreeWorker() != null;

        Worker FreeWorker()
        {
            foreach (var w in workers) if (!w.hired) return w;
            return null;
        }

        public void Hire(int i)
        {
            if (!CanHire(i)) return;
            var w = FreeWorker();
            w.Hire(candidates[i], WaitSpotFor(w.slot));
            candidates[i] = null;
        }

        public void Fire(Worker w)
        {
            ReturnCarried(w);
            w.Fire();
        }

        public int CountRole(WorkRole r)
        {
            int n = 0;
            foreach (var w in workers) if (w.hired && w.role == r) n++;
            return n;
        }

        public bool AssignRole(Worker w, WorkRole r)
        {
            if (w.role == r) return true;
            if (r != WorkRole.Waiting && CountRole(r) >= config.zoneCapacity) return false;
            w.SetRole(r);
            return true;
        }

        Vector3 WaitSpotFor(int slot) => waitSpots[Mathf.Clamp(slot, 0, waitSpots.Length - 1)].position;

        /// <summary>대기 인원은 사무실 앞 대기구역에 겹치지 않게 (대기 중 순번 = 자리).</summary>
        public Vector3 WaitSpot(Worker me)
        {
            int idx = 0;
            foreach (var w in workers)
            {
                if (w == me) break;
                if (w.hired && w.role == WorkRole.Waiting) idx++;
            }
            return waitSpots[Mathf.Min(idx, waitSpots.Length - 1)].position;
        }

        /// <summary>역할 변경/해고 시 들고 있던 택배는 원래 자리로 돌려놓는다 (분실 방지).</summary>
        public void ReturnCarried(Actor a)
        {
            if (a.carried.Count == 0) return;
            if (a.carryKind == CarryKind.Raw || a.carryKind == CarryKind.Truck)
            {
                foreach (var b in unloadBays)
                    if (b.cell.unlocked) { b.dock.parcels.AddRange(a.carried); break; }
            }
            else if (a.carryKind == CarryKind.Packed)
            {
                if (PrimaryTable != null) PrimaryTable.packed.AddRange(a.packedCarried);
            }
            else if (PrimaryTable != null)
            {
                foreach (var item in a.carried) PrimaryTable.AddStaged(item, 1);
            }
            a.ClearCarry();
        }

        /// <summary>알바가 갈 발판. 같은 역할 내 우선순위가 높은 발판 중 가장 가까운 곳.</summary>
        public WorkPad FindTargetPad(Worker w)
        {
            WorkPad best = null;
            int bestPri = int.MaxValue;
            float bestDist = float.MaxValue;
            foreach (var p in pads)
            {
                if (!p.Usable || (p.occupant != null && p.occupant != w) || !p.CanWork(w)) continue;
                int pri = Priority(w, p);
                float d = (p.transform.position - w.transform.position).sqrMagnitude;
                if (pri < bestPri || (pri == bestPri && d < bestDist))
                {
                    best = p;
                    bestPri = pri;
                    bestDist = d;
                }
            }
            return best;
        }

        static int Priority(Worker w, WorkPad p)
        {
            switch (w.role)
            {
                case WorkRole.Sort: return p.type == PadType.DockPickup ? 0 : 1;            // 가득 들고 분류
                case WorkRole.Unload: return p.type == PadType.Unload ? 0 : 1;              // 트럭에서 가득 들고 도크로
                case WorkRole.Ship: return p.type == PadType.PackedPickup ? 0 : 1;          // 포장 택배 가득 들고 트럭으로
                case WorkRole.Transport: return p.type == PadType.Shelf ? 0 : 1;           // 필요한 만큼 모아서 운반
                default: return 0;
            }
        }

        IEnumerable<Actor> AllActors()
        {
            yield return player;
            foreach (var w in workers) if (w.hired) yield return w;
        }

        // ================= 작업 판정 =================

        public WorkPad FindPadAt(Vector3 pos)
        {
            WorkPad best = null;
            float bestD = float.MaxValue;
            foreach (var p in pads)
            {
                if (!p.Usable) continue;
                float d = (Flat(p.transform.position) - Flat(pos)).magnitude;
                if (d < p.radius && d < bestD)
                {
                    best = p;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>item 을 더 분류해 넣을 수 있는 선반 여유 (이미 들고 있는 것 제외).</summary>
        public int SortSpace(string item, Actor a)
        {
            int space = 0;
            foreach (var s in shelves) if (s.label == item) space += s.Space;
            return space - a.CountCarried(item);
        }

        public bool InOrders(string item)
        {
            foreach (var o in orders) if (o.Count(item) > 0) return true;
            return false;
        }

        /// <summary>목록에서 주문서에 있는 물품의 인덱스 (뒤에서부터). 없으면 마지막.</summary>
        public int PreferOrdered(List<string> list)
        {
            for (int i = list.Count - 1; i >= 0; i--) if (InOrders(list[i])) return i;
            return list.Count - 1;
        }

        public bool HasSortable(Dock dock, Actor a)
        {
            foreach (var item in dock.parcels) if (SortSpace(item, a) > 0) return true;
            return false;
        }

        /// <summary>주문서에 아직 모자란 수 = 주문 합 − 포장대에 놓인 수 − 운반 중인 수.</summary>
        public int Needed(string item)
        {
            int n = 0;
            foreach (var o in orders) n += o.Count(item);
            if (tables.Length > 0) n -= tables[0].Staged(item); // 공유 풀이라 한 번만
            foreach (var a in AllActors())
                if (a.carryKind == CarryKind.Checked) n -= a.CountCarried(item);
            return n;
        }

        // ================= 주문 / 포장 / 출고 (4-3, 4-4장) =================

        public void CreateOrder()
        {
            var items = ContractedItems();
            if (items.Count == 0 || orders.Count >= config.orderMax) return; // 가득 차면 생성 안 함 (패널티 없음, 10장 26)
            var order = new Order { id = nextOrderId++ };
            foreach (var it in items)
            {
                float p = 1f / (6 - Mathf.Clamp(it.frequency, 1, 5)); // 5 = 1/1 … 1 = 1/5
                if (Random.value < p) order.lines.Add(new OrderLine { item = it.name, count = RollCount() });
            }
            if (order.lines.Count == 0)
                order.lines.Add(new OrderLine { item = Rng.Pick(items).name, count = RollCount() });
            orders.Add(order);
        }

        int RollCount()
        {
            int n = Random.Range(config.orderItemMin, config.orderItemMax + 1);
            if (level >= config.orderBoostLevel) n = Mathf.CeilToInt(n * config.orderBoostMultiplier);
            return n;
        }

        public bool Packable(PackingTable t, Order o)
        {
            foreach (var l in o.lines) if (t.Staged(l.item) < l.count) return false;
            return true;
        }

        public Order FindPackable(PackingTable t)
        {
            foreach (var o in orders) if (Packable(t, o)) return o;
            return null;
        }

        public int OrderValue(Order o)
        {
            int v = 0;
            foreach (var l in o.lines)
            {
                var def = config.FindItem(l.item);
                if (def != null) v += def.price * l.count;
            }
            return v;
        }

        public void PackOrder(PackingTable t, Order o)
        {
            foreach (var l in o.lines) t.AddStaged(l.item, -l.count);
            orders.Remove(o);
            t.packed.Add(new PackedParcel { orderId = o.id, value = OrderValue(o) });
        }

        public void Depart(ShipTruck truck)
        {
            if (!truck.Docked || truck.loaded.Count == 0 || OfficeOpen) return;
            int sum = 0;
            foreach (var p in truck.loaded) sum += p.value;
            money += sum;
            AddExp(truck.loaded.Count * config.expPerParcel);
            truck.loaded.Clear();
            truck.awayTimer = config.shipTruckReturnSeconds;
        }

        // ================= 플레이어 업그레이드 (5-1장 ③) =================

        public List<UpgradeStep> Steps(int kind) => kind == 0 ? config.carrySteps : kind == 1 ? config.speedSteps : config.workTimeSteps;
        public int UpgradeLevel(int kind) => kind == 0 ? carryLv : kind == 1 ? speedLv : workLv;

        public bool CanUpgrade(int kind)
        {
            var steps = Steps(kind);
            int lv = UpgradeLevel(kind);
            return lv + 1 < steps.Count && money >= steps[lv + 1].cost;
        }

        public void Upgrade(int kind)
        {
            if (!CanUpgrade(kind)) return;
            int lv = UpgradeLevel(kind) + 1;
            money -= Steps(kind)[lv].cost;
            if (kind == 0) carryLv = lv;
            else if (kind == 1) speedLv = lv;
            else workLv = lv;
        }

        // ================= 디버그 (5장) =================

        public void DebugCallTrucks()
        {
            foreach (var b in unloadBays)
            {
                if (!b.cell.unlocked) continue;
                for (int i = 0; i < TrucksPerBay; i++)
                    if (!b.trucks[i].present) b.trucks[i].Arrive(this);
            }
        }

        public void SetSpeed(float speed)
        {
            timeSpeed = speed;
            if (!OfficeOpen) Time.timeScale = timeSpeed;
        }

        public void ResetGame()
        {
            if (ScreenTransition.Busy) return;
            Time.timeScale = 1f;
            int index = SceneManager.GetActiveScene().buildIndex;
            // transition In → 씬 리로드(초기화) → transition Out. 연출 오브젝트가 없으면 바로 리로드.
            if (ScreenTransition.Instance != null) ScreenTransition.Instance.ReloadScene(index);
            else SceneManager.LoadScene(index);
        }
    }
}
