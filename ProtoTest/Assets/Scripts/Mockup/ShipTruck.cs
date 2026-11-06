using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>상차 트럭 (트럭 프리팹 + 별도 컴포넌트). 최대 10개, 1개 이상이면 [출발] 활성. 자동 출발 없음 (4-4장).</summary>
    public class ShipTruck : MonoBehaviour
    {
        public TruckView view;
        public PackingTable table;
        public Button departButton;
        public TMP_Text departLabel;

        [System.NonSerialized] public List<PackedParcel> loaded = new List<PackedParcel>();
        [System.NonSerialized] public float awayTimer;

        public bool Docked => awayTimer <= 0f;
        public int Space => GameManager.I.config.shipTruckCapacity - loaded.Count;

        void Awake()
        {
            if (departButton != null) departButton.onClick.AddListener(() => GameManager.I.Depart(this));
        }

        public void ResetState()
        {
            loaded.Clear();
            awayTimer = 0f;
        }

        public void Tick(float dt)
        {
            if (awayTimer > 0f) awayTimer -= dt;
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            int cap = gm.config.shipTruckCapacity;
            if (view != null) view.Show(Docked, Docked ? $"상차 트럭 {loaded.Count}/{cap}" : $"배송 중… {Mathf.CeilToInt(awayTimer)}초");
            if (departButton != null)
            {
                if (departButton.gameObject.activeSelf != Docked) departButton.gameObject.SetActive(Docked);
                departButton.interactable = Docked && loaded.Count > 0 && !gm.OfficeOpen;
            }
        }
    }
}
