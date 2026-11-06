using UnityEngine;

namespace Mockup
{
    /// <summary>
    /// 작업 발판 (4-7장). 점선 원형. 위에 서 있으면 작업이 자동 진행된다.
    /// 플레이어는 모든 작업, 알바는 배치 구역(역할)의 작업만 수행한다.
    /// </summary>
    public class WorkPad : MonoBehaviour
    {
        public PadType type;
        public float radius = 0.9f;
        public Shelf shelf;
        public Dock dock;
        public UnloadTruck truck;
        public PackingTable table;
        public ShipTruck shipTruck;

        [System.NonSerialized] public Worker occupant;

        public bool Usable => gameObject.activeInHierarchy;

        static bool Allow(Actor a, WorkRole r) => a.Role == WorkRole.Player || a.Role == r;

        /// <summary>이 발판에서 지금 할 수 있는 작업. 없으면 Waiting.</summary>
        public WorkRole Pick(Actor a)
        {
            var gm = GameManager.I;
            switch (type)
            {
                case PadType.Unload: // 트럭에서 1개씩 들기
                    if (Allow(a, WorkRole.Unload) && truck.present && truck.cargo.Count > 0 && a.Room > 0
                        && (a.carryKind == CarryKind.None || a.carryKind == CarryKind.Truck)) return WorkRole.Unload;
                    break;
                case PadType.DockPickup: // 하차한 택배 놓기 / 분류할 택배 들기
                {
                    bool dockFull = dock.parcels.Count >= gm.config.dockCapacity;
                    if (a.Role == WorkRole.Player)
                    {
                        // 밟은 순간 들고 있었으면 놓기만 (트럭 택배·분류용 택배 모두), 빈손이었으면 들기만
                        if (a.EnteredCarrying)
                            return a.carried.Count > 0 && !dockFull && (a.carryKind == CarryKind.Truck || a.carryKind == CarryKind.Raw) ? WorkRole.Unload : WorkRole.Waiting;
                        if (a.carried.Count == 0 || a.carryKind == CarryKind.Raw)
                            return a.Room > 0 && gm.HasSortable(dock, a) ? WorkRole.Sort : WorkRole.Waiting;
                        break;
                    }
                    if (Allow(a, WorkRole.Unload) && a.carryKind == CarryKind.Truck && a.carried.Count > 0 && !dockFull) return WorkRole.Unload;
                    if (Allow(a, WorkRole.Sort) && (a.carryKind == CarryKind.None || a.carryKind == CarryKind.Raw) && a.Room > 0 && gm.HasSortable(dock, a)) return WorkRole.Sort;
                    break;
                }
                case PadType.Shelf:
                    if (Allow(a, WorkRole.Sort) && a.carryKind == CarryKind.Raw && shelf.HasLabel && shelf.Space > 0 && a.carried.Contains(shelf.label)) return WorkRole.Sort;
                    if (Allow(a, WorkRole.Transport) && (a.carryKind == CarryKind.None || a.carryKind == CarryKind.Checked) && a.Room > 0 && shelf.checkedCount > 0 && gm.Needed(shelf.label) > 0) return WorkRole.Transport;
                    break;
                case PadType.Inspect: // 선반 왼쪽 검수 전용 발판
                    if (Allow(a, WorkRole.Inspect) && shelf.uncheckedCount > 0) return WorkRole.Inspect;
                    break;
                case PadType.PackTable:
                    if (Allow(a, WorkRole.Transport) && a.carryKind == CarryKind.Checked && a.carried.Count > 0) return WorkRole.Transport; // 공유 풀: 아무 포장대나
                    if (Allow(a, WorkRole.Pack) && gm.FindPackable(table) != null) return WorkRole.Pack;
                    break;
                case PadType.PackedPickup: // 포장 완료 택배 1개씩 들기
                    if (Allow(a, WorkRole.Ship) && (a.carryKind == CarryKind.None || a.carryKind == CarryKind.Packed) && a.Room > 0 && table.packed.Count > 0) return WorkRole.Ship;
                    break;
                case PadType.Ship: // 트럭에 1개씩 싣기
                    if (Allow(a, WorkRole.Ship) && a.carryKind == CarryKind.Packed && a.carried.Count > 0 && shipTruck.Docked && shipTruck.Space > 0) return WorkRole.Ship;
                    break;
            }
            return WorkRole.Waiting;
        }

        public bool CanWork(Actor a) => Pick(a) != WorkRole.Waiting;

        /// <summary>하차/들기/내려놓기/싣기 = 1개씩 짧은 간격. 검수/포장 = 게이지.</summary>
        public static bool IsTransfer(WorkRole r) => r != WorkRole.Inspect && r != WorkRole.Pack && r != WorkRole.Waiting;

        /// <summary>작업 1회 실행. 옮기는 작업은 1개씩, 검수는 선반의 미검수 전부.</summary>
        public void DoWork(Actor a)
        {
            var gm = GameManager.I;
            switch (Pick(a))
            {
                case WorkRole.Unload when type == PadType.Unload:
                {
                    int k = gm.PreferOrdered(truck.cargo); // 주문서에 있는 물품부터 내림
                    a.carried.Add(truck.cargo[k]);
                    truck.cargo.RemoveAt(k);
                    a.carryKind = CarryKind.Truck;
                    break;
                }
                case WorkRole.Unload: // 도크에 내려놓기
                    dock.parcels.Add(a.carried[a.carried.Count - 1]);
                    a.carried.RemoveAt(a.carried.Count - 1);
                    a.NormalizeCarry();
                    break;
                case WorkRole.Sort when type == PadType.DockPickup:
                {
                    int pick = -1;
                    for (int i = 0; i < dock.parcels.Count; i++) // 주문서 물품 우선, 없으면 넣을 수 있는 아무거나
                    {
                        string item = dock.parcels[i];
                        if (gm.SortSpace(item, a) <= 0) continue;
                        if (pick < 0) pick = i;
                        if (gm.InOrders(item)) { pick = i; break; }
                    }
                    if (pick >= 0)
                    {
                        a.carried.Add(dock.parcels[pick]);
                        dock.parcels.RemoveAt(pick);
                        a.carryKind = CarryKind.Raw;
                    }
                    break;
                }
                case WorkRole.Sort:
                    a.carried.Remove(shelf.label);
                    shelf.uncheckedCount++;
                    a.NormalizeCarry();
                    break;
                case WorkRole.Inspect:
                    shelf.checkedCount += shelf.uncheckedCount;
                    shelf.uncheckedCount = 0;
                    break;
                case WorkRole.Transport when type == PadType.Shelf:
                    shelf.checkedCount--;
                    a.carried.Add(shelf.label);
                    a.carryKind = CarryKind.Checked;
                    break;
                case WorkRole.Transport:
                    table.AddStaged(a.carried[a.carried.Count - 1], 1);
                    a.carried.RemoveAt(a.carried.Count - 1);
                    a.NormalizeCarry();
                    break;
                case WorkRole.Pack:
                {
                    var order = gm.FindPackable(table);
                    if (order != null) gm.PackOrder(table, order); // 주문서 1건 = 택배 1개
                    break;
                }
                case WorkRole.Ship when type == PadType.PackedPickup:
                    a.packedCarried.Add(table.packed[table.packed.Count - 1]);
                    table.packed.RemoveAt(table.packed.Count - 1);
                    a.carried.Add(Names.Packed);
                    a.carryKind = CarryKind.Packed;
                    break;
                case WorkRole.Ship:
                    shipTruck.loaded.Add(a.packedCarried[a.packedCarried.Count - 1]);
                    a.packedCarried.RemoveAt(a.packedCarried.Count - 1);
                    a.carried.RemoveAt(a.carried.Count - 1);
                    a.NormalizeCarry();
                    break;
            }
        }
    }
}
