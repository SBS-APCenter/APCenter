using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Mockup
{
    /// <summary>
    /// 다이나믹 조이스틱 (5-4장). 누른 위치에 반투명 원만 표시.
    /// 0.15초 이상 누르거나 10px 이상 드래그해야 시작 (짧은 터치는 일반 클릭). UI 위에서 시작한 입력은 무시.
    /// </summary>
    public class JoystickInput : MonoBehaviour
    {
        public RectTransform area;      // 캔버스 루트 RectTransform
        public RectTransform circle;    // 반투명 원 (씬에 미리 배치, 비활성)
        public RectTransform knob;

        public Vector2 Value { get; private set; }

        bool pressing, active, startedOverUI;
        Vector2 start;
        float pressTime;
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            if (gm.OfficeOpen)
            {
                Release();
                return;
            }

            bool down = false;
            Vector2 pos = Vector2.zero;
            int touches = 0;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches) if (t.press.isPressed) touches++;
                if (touches >= 2)
                {
                    Release(); // 두 손가락 = 카메라 드래그
                    return;
                }
                if (touches == 1)
                {
                    down = true;
                    pos = ts.primaryTouch.position.ReadValue();
                }
            }
            if (touches == 0 && Mouse.current != null)
            {
                down = Mouse.current.leftButton.isPressed;
                pos = Mouse.current.position.ReadValue();
            }

            if (down && !pressing)
            {
                pressing = true;
                active = false;
                start = pos;
                pressTime = 0f;
                startedOverUI = IsOverUI(pos);
            }
            else if (down)
            {
                pressTime += Time.unscaledDeltaTime;
                var cfg = gm.config;
                if (!startedOverUI && !active && (pressTime >= cfg.joystickHoldSeconds || (pos - start).magnitude >= cfg.joystickStartPixels))
                {
                    active = true;
                    circle.gameObject.SetActive(true);
                    circle.anchoredPosition = ToLocal(start);
                }
                if (active)
                {
                    float scale = area.lossyScale.x > 0f ? area.lossyScale.x : 1f;
                    float max = cfg.joystickMaxPixels * scale;
                    Vector2 d = Vector2.ClampMagnitude(pos - start, max);
                    Value = d / max;
                    if (knob != null) knob.anchoredPosition = d / scale;
                }
            }
            else if (pressing) Release();
        }

        void Release()
        {
            pressing = false;
            active = false;
            Value = Vector2.zero;
            if (circle != null && circle.gameObject.activeSelf) circle.gameObject.SetActive(false);
        }

        Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            return local;
        }

        bool IsOverUI(Vector2 screen)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
            return hits.Count > 0;
        }
    }
}
