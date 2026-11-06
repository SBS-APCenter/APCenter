using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>월드 글씨 뒤 반투명 패널. 미리 배치된 스프라이트 크기만 글자 범위에 맞춘다 (생성 없음).</summary>
    public class TextPanel : MonoBehaviour
    {
        public TMP_Text text;
        public SpriteRenderer panel;
        public Vector2 padding = new Vector2(0.25f, 0.12f);

        void LateUpdate()
        {
            bool show = !string.IsNullOrEmpty(text.text);
            if (panel.enabled != show) panel.enabled = show;
            if (!show) return;
            var b = text.textBounds;
            var t = panel.transform;
            t.localPosition = new Vector3(b.center.x, b.center.y, 0.02f); // 글자 바로 뒤
            t.localScale = new Vector3(b.size.x + padding.x * 2f, b.size.y + padding.y * 2f, 1f);
        }
    }
}
