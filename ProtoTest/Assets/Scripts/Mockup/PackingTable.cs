using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>
    /// 포장대. 운반된 물품(staging)은 모든 포장대가 공유하는 풀 → 어느 포장대에서든 포장 가능,
    /// 주문 물품이 포장대마다 나뉘어 막히지 않음. 포장 완료 택배(packed)는 포장대별.
    /// </summary>
    public class PackingTable : MonoBehaviour
    {
        public ZoneCell cell;
        public TMP_Text infoText;
        public GameObject[] packedBoxes;   // 옆 '포장 완료 테이블' 위에 쌓이는 박스
        public TMP_Text packedText;

        static readonly Dictionary<string, int> sharedStaging = new Dictionary<string, int>();
        public Dictionary<string, int> staging => sharedStaging;
        public static void ResetShared() => sharedStaging.Clear(); // 씬 재시작 시 (GameManager.Start)
        [System.NonSerialized] public List<PackedParcel> packed = new List<PackedParcel>();

        public int Staged(string item) => staging.TryGetValue(item, out var n) ? n : 0;

        public void AddStaged(string item, int n)
        {
            staging[item] = Staged(item) + n;
            if (staging[item] <= 0) staging.Remove(item);
        }

        void LateUpdate()
        {
            for (int i = 0; i < packedBoxes.Length; i++) packedBoxes[i].SetActive(i < packed.Count);
            if (packedText != null) packedText.text = $"포장 완료 택배 {packed.Count}";
            if (infoText == null) return;
            var sb = new System.Text.StringBuilder("포장대\n");
            if (staging.Count == 0) sb.Append("물품 없음");
            foreach (var kv in staging) sb.Append($"{kv.Key} {kv.Value}  ");
            sb.Append($"\n포장 완료 {packed.Count}");
            infoText.text = sb.ToString();
        }
    }
}
