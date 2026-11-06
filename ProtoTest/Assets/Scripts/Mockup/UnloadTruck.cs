using System.Collections.Generic;
using UnityEngine;

namespace Mockup
{
    /// <summary>하차 트럭 1대. 체류 60초 / 전부 하차 시 즉시 회차 / 회차 후 15초 재방문 (4-1장).</summary>
    public class UnloadTruck : MonoBehaviour
    {
        public TruckView view;
        public WorkPad pad;

        [System.NonSerialized] public bool present;
        [System.NonSerialized] public float timer;
        [System.NonSerialized] public List<string> cargo = new List<string>();

        public void ResetState(float firstDelay)
        {
            present = false;
            timer = firstDelay;
            cargo.Clear();
        }

        public void Tick(float dt, GameManager gm)
        {
            if (!present)
            {
                if (gm.contracts.Count == 0) return;
                timer -= dt;
                if (timer <= 0f) Arrive(gm);
            }
            else
            {
                timer -= dt;
                if (cargo.Count == 0 || timer <= 0f) Leave(gm);
            }
        }

        public void Arrive(GameManager gm)
        {
            var items = gm.ContractedItems();
            if (items.Count == 0) return;
            cargo.Clear();
            for (int i = 0; i < gm.config.unloadTruckParcels; i++) cargo.Add(Rng.Pick(items).name);
            present = true;
            timer = gm.config.truckStaySeconds;
        }

        void Leave(GameManager gm)
        {
            cargo.Clear(); // 못 내린 택배는 그대로 싣고 회차
            present = false;
            timer = gm.config.truckReturnSeconds;
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (view == null || gm == null) return;
            view.Show(present, present
                ? $"하차 트럭 {cargo.Count}/{gm.config.unloadTruckParcels}\n회차까지 {Mathf.CeilToInt(timer)}초"
                : (gm.contracts.Count == 0 ? "계약 대기" : $"도착까지 {Mathf.CeilToInt(timer)}초"));
        }
    }
}
