using UnityEngine;
using UnityEngine.InputSystem;

namespace Mockup
{
    /// <summary>플레이어 (Basic). 키보드(WASD/방향키) + 다이나믹 조이스틱 이동, 발판 위 자동 작업.</summary>
    public class PlayerController : Actor
    {
        public JoystickInput joystick;
        public CameraRig cameraRig;

        GameManager GM => GameManager.I;
        public override int Capacity => GM.PlayerCapacity;
        public override float MoveSpeed => GM.PlayerSpeed;
        public override float WorkTime => GM.PlayerWorkTime;
        public override WorkRole Role => WorkRole.Player;

        void Update()
        {
            var gm = GM;
            if (gm == null) return;
            float dt = Time.deltaTime;

            Vector2 input = Vector2.zero;
            if (!gm.OfficeOpen)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                }
                if (input.sqrMagnitude < 0.01f && joystick != null) input = joystick.Value;
            }
            input = Vector2.ClampMagnitude(input, 1f);
            IsMoving = input.sqrMagnitude > 0.01f && dt > 0f;

            if (IsMoving)
            {
                var yaw = Quaternion.Euler(0f, cameraRig != null ? cameraRig.yaw : 0f, 0f);
                Vector3 dir = yaw * new Vector3(input.x, 0f, input.y);
                Vector3 step = dir * (MoveSpeed * dt);
                Vector3 p = transform.position;
                if (gm.IsWalkable(p + step)) p += step;
                else if (gm.IsWalkable(p + new Vector3(step.x, 0f, 0f))) p += new Vector3(step.x, 0f, 0f);
                else if (gm.IsWalkable(p + new Vector3(0f, 0f, step.z))) p += new Vector3(0f, 0f, step.z);
                transform.position = p;
                Face(dir);
            }

            TickWork(dt, gm.FindPadAt(transform.position));
        }
    }
}
