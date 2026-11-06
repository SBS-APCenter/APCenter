using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mockup
{
    /// <summary>배치 구역 칸 (하차/분류/검수/운반/포장/상차/대기). 구역별 최대 인원 초과 시 거부.</summary>
    public class ZoneDropSlot : MonoBehaviour, IDropHandler
    {
        public WorkRole role;
        public RectTransform content;
        public TMP_Text title;

        public void OnDrop(PointerEventData e)
        {
            var tag = e.pointerDrag != null ? e.pointerDrag.GetComponent<NameTag>() : null;
            if (tag == null) return;
            var gm = GameManager.I;
            if (gm.AssignRole(gm.workers[tag.slot], role)) tag.transform.SetParent(content, false);
        }
    }
}
