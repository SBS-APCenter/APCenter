using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Mockup
{
    /// <summary>알바 배치 구역 = 작업 종류 (4-2-1, 4-5장). Player 는 모든 작업 가능.</summary>
    public enum WorkRole { Waiting = 0, Unload = 1, Sort = 2, Inspect = 3, Transport = 4, Pack = 5, Ship = 6, Player = 7 }

    /// <summary>Truck = 트럭에서 막 내린 택배(도크에 놓아야 함), Raw = 도크에서 든 분류용, Checked = 검수 완료 운반용.</summary>
    public enum CarryKind { None, Raw, Checked, Truck, Packed }

    public enum ZoneType { Office, Storage, Unload, Ship, Return }

    /// <summary>PackedPickup = 포장 완료 택배 테이블 앞 (상차할 택배 들기).</summary>
    public enum PadType { Unload, DockPickup, Shelf, PackTable, Ship, Inspect, PackedPickup }

    public static class Names
    {
        public const string Packed = "택배"; // 손에 든 포장 완료 택배 표시명
        public static readonly string[] Roles = { "대기", "하차", "분류", "검수", "운반", "포장", "상차", "플레이어" };
        public static string Role(WorkRole r) => Roles[(int)r];
        public static string Money(int v) => v.ToString("N0");
        public const string StrColor = "#E04848", AgiColor = "#2FA65A", IntColor = "#4472E0", StressColor = "#F08A24";

        /// <summary>능력치 칸: 채운 칸 = 고유 색, 빈 칸 = 연회색.</summary>
        public static string ColorStars(int v, int max, string hex)
        {
            v = Mathf.Clamp(v, 0, max);
            return $"<color={hex}>{new string('■', v)}</color><color=#C8C8C8>{new string('■', max - v)}</color>";
        }

        public static string Stars(int v, int max)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < max; i++) sb.Append(i < v ? '■' : '□');
            return sb.ToString();
        }
    }

    public class OrderLine
    {
        public string item;
        public int count;
    }

    public class Order
    {
        public int id;
        public List<OrderLine> lines = new List<OrderLine>();

        public int Count(string item)
        {
            foreach (var l in lines) if (l.item == item) return l.count;
            return 0;
        }
    }

    public class PackedParcel
    {
        public int orderId;
        public int value;
    }

    public class WorkerData
    {
        public string name;
        public int strength, agility, intelligence;
        public int modelIndex; // 0 = Macaron, 1 = rabbit (10장 19: 랜덤 배정 임시)
        public int StatSum => strength + agility + intelligence;
    }

    /// <summary>간단 랜덤 헬퍼.</summary>
    public static class Rng
    {
        public static T Pick<T>(IList<T> list) => list[Random.Range(0, list.Count)];
    }
}
