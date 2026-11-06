using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>알바 이름표. 드래그 앤 드롭으로 배치 구역 칸에 놓는다 (5-1장 ②).</summary>
    public class NameTag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int slot; // = Worker.slot
        public TMP_Text label;
        public Image background;
        public CanvasGroup group;
        public RectTransform dragLayer;

        [System.NonSerialized] public bool dragging;
        Transform home;

        public void OnBeginDrag(PointerEventData e)
        {
            dragging = true;
            home = transform.parent;
            transform.SetParent(dragLayer, true);
            group.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData e)
        {
            transform.position = e.position;
        }

        public void OnEndDrag(PointerEventData e)
        {
            dragging = false;
            group.blocksRaycasts = true;
            if (transform.parent == dragLayer) transform.SetParent(home, false); // 놓을 수 없는 곳이면 원위치
        }
    }
}
