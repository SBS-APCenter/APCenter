using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    [Serializable]
    public class RegionRefs
    {
        public Button button;
        public Image icon;
        public Sprite colorSprite;
        public Sprite graySprite;
        public GameObject chains;    // 미해금: 사슬 + 자물쇠
        public TMP_Text nameText;
    }

    /// <summary>
    /// 우측 하단 지구본 버튼 → 지역 선택 지도 (버거플리즈 스테이지 선택 느낌). 7지역, 이동 기능은 미구현.
    /// 1지역 = 현재, 2지역 = 11레벨 달성 시 해금 표시(4-6장), 나머지는 잠김(회색 + 사슬).
    /// </summary>
    public class MapPanel : MonoBehaviour
    {
        public Button openButton;
        public GameObject panel;
        public Button closeButton;
        public RegionRefs[] regions;
        public TMP_Text toastText;

        float toastTimer;

        void Awake()
        {
            openButton.onClick.AddListener(() => panel.SetActive(true));
            closeButton.onClick.AddListener(() => panel.SetActive(false));
            for (int i = 0; i < regions.Length; i++)
            {
                int k = i;
                regions[i].button.onClick.AddListener(() => OnRegion(k));
            }
        }

        bool Unlocked(int i)
        {
            var gm = GameManager.I;
            return i == 0 || (i == 1 && gm != null && gm.level >= gm.config.nextRegionLevel);
        }

        void OnRegion(int i)
        {
            if (i == 0) { panel.SetActive(false); return; }
            Toast(Unlocked(i) ? $"{i + 1}지역은 아직 준비 중입니다" : $"{i + 1}지역은 잠겨 있습니다");
        }

        void Toast(string msg)
        {
            toastText.text = msg;
            toastText.gameObject.SetActive(true);
            toastTimer = 1.5f;
        }

        void Update()
        {
            if (!panel.activeSelf) return;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                bool open = Unlocked(i);
                r.icon.sprite = open ? r.colorSprite : r.graySprite;
                if (r.chains.activeSelf == open) r.chains.SetActive(!open);
            }
            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f) toastText.gameObject.SetActive(false);
            }
        }
    }
}
