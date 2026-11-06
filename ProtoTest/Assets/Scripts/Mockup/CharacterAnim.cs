using UnityEngine;

namespace Mockup
{
    /// <summary>
    /// walk / carry 클립만 있음 (5-6장). 이동 중이면 재생, 멈추면 두 발 접지 프레임에서 정지시켜 idle 로 쓴다.
    /// 접지 프레임(normalized time)은 빌더가 발 본 높이로 계산해 넣는다.
    /// </summary>
    public class CharacterAnim : MonoBehaviour
    {
        public Animator animator;
        [Range(0f, 1f)] public float walkIdleTime;
        [Range(0f, 1f)] public float carryIdleTime;

        string state;
        bool moving;

        void OnEnable() => state = null;

        public void Set(bool isMoving, bool carrying)
        {
            if (animator == null) return;
            string s = carrying ? "Carry" : "Walk";
            if (s == state && isMoving == moving) return;
            state = s;
            moving = isMoving;
            if (isMoving)
            {
                animator.speed = 1f;
                animator.Play(s, 0, 0f);
            }
            else
            {
                animator.Play(s, 0, carrying ? carryIdleTime : walkIdleTime);
                animator.speed = 0f;
            }
        }
    }
}
