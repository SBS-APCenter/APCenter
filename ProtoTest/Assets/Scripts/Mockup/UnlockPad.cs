using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>해금 발판 (2-4장). 올라서면 돈이 서서히 빠지며 게이지가 찬다. 내려오거나 돈이 없으면 멈춤(게이지 유지).</summary>
    public class UnlockPad : MonoBehaviour
    {
        public ZoneCell cell;
        public float radius = 1.3f;
        public Transform fill;
        public TMP_Text text;

        [System.NonSerialized] public int paid;
        float acc;

        public int Cost => GameManager.I.config.UnlockCost(cell.unlockLevel);

        void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            var gm = GameManager.I;
            if (gm == null || gm.OfficeOpen) return;
            int cost = Cost;
            var p = gm.player.transform.position;
            var c = transform.position;
            bool on = Mathf.Abs(p.x - c.x) < radius && Mathf.Abs(p.z - c.z) < radius;
            if (on && paid < cost && gm.money > 0)
            {
                acc += cost / Mathf.Max(0.1f, gm.config.unlockFillSeconds) * dt;
                int take = Mathf.Min(Mathf.FloorToInt(acc), gm.money, cost - paid);
                if (take > 0) { paid += take; gm.money -= take; acc -= take; }
            }
            else acc = 0f;

            if (paid >= cost) gm.UnlockCell(cell);
            Refresh();
        }

        public void Refresh()
        {
            int cost = Mathf.Max(1, Cost);
            float r = Mathf.Clamp01(paid / (float)cost);
            if (fill != null) fill.localScale = new Vector3(r, 1f, 1f);
            if (text != null) text.text = $"{cell.displayName} 해금\n{Names.Money(paid)} / {Names.Money(Cost)}";
        }
    }
}
