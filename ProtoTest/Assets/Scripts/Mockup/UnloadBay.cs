using UnityEngine;

namespace Mockup
{
    /// <summary>하차구역 칸 1개: 트럭 2대(2대째는 8레벨 보상) + 하차도크.</summary>
    public class UnloadBay : MonoBehaviour
    {
        public ZoneCell cell;
        public UnloadTruck[] trucks;
        public Dock dock;
    }
}
