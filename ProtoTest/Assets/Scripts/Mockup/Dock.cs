using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>하차도크: 트럭에서 내린 택배가 쌓이는 곳.</summary>
    public class Dock : MonoBehaviour
    {
        public GameObject[] boxes;
        public TMP_Text countText;
        [System.NonSerialized] public List<string> parcels = new List<string>();

        void LateUpdate()
        {
            for (int i = 0; i < boxes.Length; i++) boxes[i].SetActive(i < parcels.Count);
            var gm = GameManager.I;
            if (countText != null && gm != null)
                countText.text = parcels.Count >= gm.config.dockCapacity
                    ? $"하차도크 <color=#D03030>{parcels.Count}/{gm.config.dockCapacity} 가득</color>"
                    : $"하차도크 {parcels.Count}/{gm.config.dockCapacity}";
        }
    }
}
