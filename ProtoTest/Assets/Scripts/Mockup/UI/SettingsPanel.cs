using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>설정 버튼 → 설정 창 [게임 종료] → 확인 창 (5-5장). 에디터에서는 플레이 모드 종료.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        public Button openButton;
        public GameObject panel;
        public Button quitButton;
        public Button closeButton;
        public GameObject confirmPanel;
        public Button confirmYes;
        public Button confirmNo;

        void Awake()
        {
            openButton.onClick.AddListener(() => panel.SetActive(true));
            closeButton.onClick.AddListener(() => panel.SetActive(false));
            quitButton.onClick.AddListener(() => confirmPanel.SetActive(true));
            confirmNo.onClick.AddListener(() => confirmPanel.SetActive(false));
            confirmYes.onClick.AddListener(Quit);
        }

        MapPanel map;

        void Start() => map = FindAnyObjectByType<MapPanel>(FindObjectsInactive.Include); // 씬에 배치된 지도 참조

        void Update()
        {
            // ESC: 켜져 있는 UI 를 위에서부터 하나 닫음 (확인 창 → 설정 → 지도 → 사무실). 아무것도 없을 때만 설정 열기
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            var gm = GameManager.I;
            if (confirmPanel.activeSelf) confirmPanel.SetActive(false);
            else if (panel.activeSelf) panel.SetActive(false);
            else if (map != null && map.panel.activeSelf) map.panel.SetActive(false);
            else if (gm != null && gm.OfficeOpen) gm.officeUI.Close(); // 튜토리얼 중엔 Close 가 무시됨
            else panel.SetActive(true);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
