using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>
    /// 화면 전환 연출. 씬 리로드를 transition In(화면 덮기) → 로드 → Out(화면 열기) 으로 감싼다.
    /// TransitionCanvas 에 붙이면 Awake 에서 루트 오버레이 캔버스로 분리하고 DontDestroyOnLoad 처리한다.
    /// 새 씬에도 TransitionCanvas 가 들어 있지만 이미 Instance 가 있으면 스스로 파괴된다.
    /// </summary>
    public class ScreenTransition : MonoBehaviour
    {
        public static ScreenTransition Instance { get; private set; }
        public static bool Busy => Instance != null && Instance.busy;

        [Tooltip("비워두면 자식에서 Animator 를 자동으로 찾는다.")]
        public Animator animator;
        public string inState = "style47_in";
        public string outState = "style47_out";
        public int sortingOrder = 1000;

        bool busy;
        GameObject blocker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (animator == null) animator = GetComponentInChildren<Animator>(true);

            // 게임 캔버스와 같은 해상도 기준을 쓰도록 스케일러를 복사한 뒤 루트로 분리한다.
            var scaler = GetComponent<CanvasScaler>();
            var src = transform.parent != null ? transform.parent.GetComponentInParent<CanvasScaler>() : null;
            if (scaler != null && src != null)
            {
                scaler.uiScaleMode = src.uiScaleMode;
                scaler.referenceResolution = src.referenceResolution;
                scaler.screenMatchMode = src.screenMatchMode;
                scaler.matchWidthOrHeight = src.matchWidthOrHeight;
            }
            transform.SetParent(null, false);
            transform.localScale = Vector3.one;

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            blocker = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)blocker.transform;
            rt.SetParent(transform, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = blocker.GetComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true;
            blocker.SetActive(false);

            if (animator != null)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.gameObject.SetActive(false);
            }
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>In 연출 → 씬 로드 → Out 연출 순서로 씬을 다시 불러온다.</summary>
        public void ReloadScene(int buildIndex)
        {
            if (busy) return;
            StartCoroutine(Run(buildIndex));
        }

        IEnumerator Run(int buildIndex)
        {
            busy = true;
            blocker.SetActive(true);

            // 1) transition In: 화면을 덮는다
            animator.gameObject.SetActive(true);
            animator.Play(inState, 0, 0f);

            // 로딩은 In 연출과 동시에 시작해 두고(활성화만 보류), 연출이 끝나면 곧바로 활성화한다.
            var op = SceneManager.LoadSceneAsync(buildIndex);
            op.allowSceneActivation = false;
            float inEnd = Time.realtimeSinceStartup + LengthOf(inState, 1f);
            while (Time.realtimeSinceStartup < inEnd || op.progress < 0.9f) yield return null;

            // 2) 화면이 덮인 상태에서 게임 초기화(씬 활성화)
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;   // 새 씬의 Start 가 도는 프레임
            yield return null;

            // 3) transition Out: 초기화된 화면을 보여준다
            animator.Play(outState, 0, 0f);
            yield return new WaitForSecondsRealtime(LengthOf(outState, 0.45f));

            animator.gameObject.SetActive(false);
            blocker.SetActive(false);
            busy = false;
        }

        float LengthOf(string clipName, float fallback)
        {
            var ctrl = animator.runtimeAnimatorController;
            var clip = ctrl == null ? null : ctrl.animationClips.FirstOrDefault(c => c.name == clipName);
            return clip != null ? clip.length : fallback;
        }
    }
}
