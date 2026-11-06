using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>선반 (AI_Shelf 프리팹). 라벨 / 용량 / 검수 전·후 수량.</summary>
    public class Shelf : MonoBehaviour
    {
        public int index;
        public TMP_Text labelText;
        public SpriteRenderer iconRenderer; // 라벨 물품 이모지

        [System.NonSerialized] public string label;
        [System.NonSerialized] public int capacity = 10;
        [System.NonSerialized] public int uncheckedCount;
        [System.NonSerialized] public int checkedCount;

        public bool HasLabel => !string.IsNullOrEmpty(label);
        public int Total => uncheckedCount + checkedCount;
        public int Space => capacity - Total;

        public void Clear()
        {
            label = null;
            uncheckedCount = 0;
            checkedCount = 0;
        }

        void LateUpdate()
        {
            if (iconRenderer != null)
            {
                var gm = GameManager.I;
                var def = HasLabel && gm != null ? gm.config.FindItem(label) : null;
                var sprite = HasLabel ? (def != null ? def.icon : null) : (gm != null ? gm.config.emptyShelfIcon : null); // 빈 선반 = 🚫
                bool show = sprite != null;
                if (iconRenderer.gameObject.activeSelf != show) iconRenderer.gameObject.SetActive(show);
                if (show) iconRenderer.sprite = sprite;
            }
            // 선반 위 택배 박스: 미검수 = 갈색, 검수 완료 = 초록 (든 만큼 사라짐)
            if (boxes != null)
                for (int i = 0; i < boxes.Length; i++)
                {
                    bool on = i < Total;
                    if (boxes[i].activeSelf != on) boxes[i].SetActive(on);
                    if (on && boxRenderers != null && i < boxRenderers.Length)
                    {
                        var m = i < checkedCount ? checkedMaterial : uncheckedMaterial;
                        if (m != null && boxRenderers[i].sharedMaterial != m) boxRenderers[i].sharedMaterial = m;
                    }
                }
            if (labelText == null) return;
            labelText.text = $"{Total}/{capacity}\n<size=80%>미검수 {uncheckedCount} · 검수 {checkedCount}</size>";
        }

        [Header("선반 위 박스 (미리 배치, 용량 최대치만큼)")]
        public GameObject[] boxes;
        public Renderer[] boxRenderers;
        public Material uncheckedMaterial;
        public Material checkedMaterial;
    }
}
