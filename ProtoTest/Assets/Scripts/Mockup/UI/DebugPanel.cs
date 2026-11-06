using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>목업용 디버그 창: 레벨업 / 돈 추가 / 트럭 호출 / 주문 생성 / 배속(1~4) / 게임 초기화 + 돈 직접 수정 창.</summary>
    public class DebugPanel : MonoBehaviour
    {
        public Button toggleButton;
        public GameObject body;
        public Button levelUpButton;
        public Button addMoneyButton;
        public Button callTruckButton;
        public Button createOrderButton;
        public Button resetButton;
        public Button[] speedButtons;   // config.debugSpeeds 순서
        public Image[] speedImages;

        [Header("돈 수정 창")]
        public TMP_InputField moneyInput;
        public Button applyMoneyButton;
        public Button[] plusButtons;   // config.debugMoneySteps 순서
        public Button[] minusButtons;

        static readonly Color SpeedOn = new Color(0.65f, 0.5f, 1f), SpeedOff = new Color(0.85f, 0.82f, 0.95f);

        void Awake()
        {
            toggleButton.onClick.AddListener(() => body.SetActive(!body.activeSelf));
            levelUpButton.onClick.AddListener(() => GameManager.I.DebugLevelUp());
            addMoneyButton.onClick.AddListener(() => GameManager.I.money += GameManager.I.config.debugMoneySteps[0]);
            callTruckButton.onClick.AddListener(() => GameManager.I.DebugCallTrucks());
            createOrderButton.onClick.AddListener(() => GameManager.I.CreateOrder());
            resetButton.onClick.AddListener(() => GameManager.I.ResetGame());
            for (int i = 0; i < speedButtons.Length; i++)
            {
                int k = i;
                speedButtons[i].onClick.AddListener(() => GameManager.I.SetSpeed(GameManager.I.config.debugSpeeds[k]));
            }
            applyMoneyButton.onClick.AddListener(() =>
            {
                if (int.TryParse(moneyInput.text.Replace(",", ""), out int v)) GameManager.I.money = v;
            });
            for (int i = 0; i < plusButtons.Length; i++)
            {
                int k = i;
                plusButtons[i].onClick.AddListener(() => GameManager.I.money += GameManager.I.config.debugMoneySteps[k]);
                minusButtons[i].onClick.AddListener(() => GameManager.I.money -= GameManager.I.config.debugMoneySteps[k]);
            }
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            for (int i = 0; i < speedImages.Length; i++)
                speedImages[i].color = Mathf.Approximately(gm.config.debugSpeeds[i], gm.timeSpeed) ? SpeedOn : SpeedOff;
        }
    }
}
