using UnityEngine;
using UnityEngine.InputSystem;

namespace Mockup
{
    /// <summary>
    /// 쿼터뷰 고정 각도 (5-2장). 이동 중엔 캐릭터 추적, 멈춰 있을 때 자유 시점 드래그.
    /// 모바일: 두 손가락 드래그 / PC: 우클릭 드래그(임시). 줌: 휠 / 두 손가락 벌리기·모으기. 사무실 UI 열림 중엔 비활성.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        public Camera cam;
        public float yaw = 45f;
        public float followSharpness = 10f;

        [Header("줌 (PC 휠 / 모바일 두 손가락 벌리기·모으기)")]
        public float minDistance = 20f;
        public float maxDistance = 90f;
        public float wheelStep = 0.12f;   // 휠 한 칸당 거리 비율

        [Header("오소그래픽 (카메라가 Orthographic이면 사용, 버거플리즈식 대각선 쿼터뷰)")]
        public float orthoYaw = 45f;
        public float orthoPitch = 35f;
        public float orthoCamDistance = 60f;  // 클리핑 방지용 거리 (오소에선 화면 크기와 무관)
        public float minOrthoSize = 6f;
        public float maxOrthoSize = 25f;

        bool freeView;
        bool panning;
        Vector3 lastGround;
        Vector3 camDir;
        float distance;
        float lastPinch = -1f;
        readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();

        void Awake()
        {
            if (cam.orthographic)
            {
                // 오소: 거리 대신 orthographicSize로 줌. yaw는 PlayerController 이동 방향에도 쓰임
                yaw = orthoYaw;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var rot = Quaternion.Euler(orthoPitch, 0f, 0f);
                cam.transform.localRotation = rot;
                camDir = rot * Vector3.back;
                cam.transform.localPosition = camDir * orthoCamDistance;
                distance = cam.orthographicSize;
                return;
            }
            var lp = cam.transform.localPosition;
            distance = lp.magnitude;
            camDir = lp / Mathf.Max(0.001f, distance);
        }

        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm == null || target == null) return;
            bool moving = gm.player.IsMoving;
            if (moving) freeView = false;

            if (!gm.OfficeOpen) HandleZoom();
            else lastPinch = -1f;

            if (!gm.OfficeOpen && !moving) HandlePan();
            else panning = false;

            if (!freeView)
            {
                float k = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, target.position, k);
            }
        }

        void HandlePan()
        {
            bool pressed = false;
            Vector2 pos = Vector2.zero;

            var ts = Touchscreen.current;
            if (ts != null)
            {
                int n = 0;
                Vector2 sum = Vector2.zero;
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    n++;
                    sum += t.position.ReadValue();
                }
                if (n >= 2)
                {
                    pressed = true;
                    pos = sum / n;
                }
            }
            if (!pressed && Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                pressed = true;
                pos = Mouse.current.position.ReadValue();
            }

            if (!pressed)
            {
                panning = false;
                return;
            }
            if (!ScreenToGround(pos, out var g)) return;
            if (!panning)
            {
                panning = true;
                lastGround = g;
                return;
            }
            transform.position += lastGround - g;
            freeView = true;
            ScreenToGround(pos, out lastGround);
        }

        void HandleZoom()
        {
            float d = distance;

            // PC: 마우스 휠 (UI 위에서는 무시 → 스크롤 목록과 충돌 방지)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && !OverUI(mouse.position.ReadValue()))
                    d *= wheel > 0f ? 1f - wheelStep : 1f + wheelStep;
            }

            // 모바일: 두 손가락 사이 거리 변화 (벌리면 확대, 모으면 축소)
            var ts = Touchscreen.current;
            Vector2 a = default, b = default;
            int n = 0;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    if (n == 0) a = t.position.ReadValue();
                    else if (n == 1) b = t.position.ReadValue();
                    n++;
                }
            if (n >= 2)
            {
                float pinch = (a - b).magnitude;
                if (lastPinch > 0f && pinch > 1f) d *= lastPinch / pinch;
                lastPinch = pinch;
            }
            else lastPinch = -1f;

            if (cam.orthographic)
            {
                distance = Mathf.Clamp(d, minOrthoSize, maxOrthoSize);
                cam.orthographicSize = distance;
                return;
            }
            distance = Mathf.Clamp(d, minDistance, maxDistance);
            cam.transform.localPosition = camDir * distance;
        }

        bool OverUI(Vector2 screen)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            hits.Clear();
            es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = screen }, hits);
            return hits.Count > 0;
        }

        bool ScreenToGround(Vector2 screen, out Vector3 world)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d))
            {
                world = ray.GetPoint(d);
                return true;
            }
            world = Vector3.zero;
            return false;
        }
    }
}
