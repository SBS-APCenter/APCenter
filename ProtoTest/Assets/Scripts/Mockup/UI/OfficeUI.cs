using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>
    /// 사무실 클립보드 UI (5-1장). 열려 있는 동안 게임 시간 정지(timeScale 0).
    /// 우측 포스트잇 탭: [계약] / [알바관리] / [플레이어 업그레이드].
    /// </summary>
    public class OfficeUI : MonoBehaviour
    {
        public GameObject root;
        public GameObject[] pages;
        public Button[] tabButtons;
        public RectTransform[] tabRects;
        public Button closeButton;
        public float tabSelectedX = 0f;   // 선택된 탭: 종이 밖으로 더 튀어나옴
        public float tabIdleX = -24f;

        int current;

        public bool IsOpen => root != null && root.activeSelf;

        void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int k = i;
                tabButtons[i].onClick.AddListener(() => Show(k));
            }
            closeButton.onClick.AddListener(Close);
        }

        public void Open(int tab)
        {
            root.SetActive(true);
            Time.timeScale = 0f;
            Show(tab >= 0 ? tab : current);
        }

        public void Close()
        {
            if (!GameManager.I.tutorialDone) return; // 튜토리얼(계약 2건) 끝날 때까지 닫기 불가
            root.SetActive(false);
            Time.timeScale = GameManager.I.timeSpeed;
        }

        public void Show(int i)
        {
            current = i;
            for (int p = 0; p < pages.Length; p++) pages[p].SetActive(p == i);
            for (int t = 0; t < tabRects.Length; t++)
            {
                var pos = tabRects[t].anchoredPosition;
                pos.x = t == i ? tabSelectedX : tabIdleX; // 선택된 탭이 바깥으로 튀어나옴
                tabRects[t].anchoredPosition = pos;
            }
        }

        void Update()
        {
            if (IsOpen) closeButton.interactable = GameManager.I.tutorialDone;
        }
    }
}
