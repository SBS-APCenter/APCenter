using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mockup
{
    [Serializable]
    public class ItemDef
    {
        public string name;
        public int price;
        [Range(1, 5)] public int frequency = 3;
        public Color color = Color.white;
        public Sprite icon; // 말풍선 이모지
    }

    [Serializable]
    public class CompanyDef
    {
        public string name;
        public int contractFee;
        public ItemDef itemA;
        public ItemDef itemB;
    }

    [Serializable]
    public class ZoneUnlockDef
    {
        public int level;
        public int cost;
    }

    [Serializable]
    public class ShelfUpgradeDef
    {
        public int level;
        public int shelfA;
        public int shelfB;
    }

    [Serializable]
    public class UpgradeStep
    {
        public float value;
        public int cost;
    }

    /// <summary>목업의 모든 임시 수치. 코드 밖 설정값 (기획서 6장).</summary>
    [CreateAssetMenu(menuName = "Mockup/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("경제 / 레벨")]
        public int startMoney = 1000;
        public int expPerParcel = 10;
        public int expPerLevelFactor = 100; // n -> n+1 : factor * n
        public int maxLevel = 11;
        public int nextRegionLevel = 11;

        [Header("구역 해금 (2-3, 6장)")]
        public List<ZoneUnlockDef> zoneUnlocks = new List<ZoneUnlockDef>();
        public float unlockFillSeconds = 3f;

        [Header("선반 (4-2장)")]
        public int shelfBaseCapacity = 10;
        public int shelfUpgradedCapacity = 20;
        public List<ShelfUpgradeDef> shelfUpgrades = new List<ShelfUpgradeDef>();

        [Header("하차 트럭 (4-1장)")]
        public int unloadTruckParcels = 10;
        public float truckStaySeconds = 60f;
        public float truckReturnSeconds = 15f;
        public float truckFirstArrivalDelay = 3f;
        public int extraTruckLevel = 8;
        public int dockCapacity = 20;   // 하차도크 최대 택배 수

        [Header("상차 트럭 (4-4장)")]
        public int shipTruckCapacity = 10;
        public float shipTruckReturnSeconds = 5f;

        [Header("주문 (4-3장)")]
        public float orderInterval = 30f;
        public int orderMax = 10;
        public int orderItemMin = 1;
        public int orderItemMax = 3;
        public int orderBoostLevel = 9;
        public float orderBoostMultiplier = 1.5f;

        [Header("인게임 하루")]
        public float dayLengthSeconds = 300f;

        [Header("플레이어 업그레이드 (5-1장 ③) index 0 = 기본")]
        public List<UpgradeStep> carrySteps = new List<UpgradeStep>();
        public List<UpgradeStep> speedSteps = new List<UpgradeStep>();
        public List<UpgradeStep> workTimeSteps = new List<UpgradeStep>();

        [Header("작업 발판")]
        public float transferInterval = 0.3f;   // 하차/들기/내려놓기/싣기: 1개씩 이 간격으로
        public float inspectTimeScale = 0.5f;   // 검수 게이지 = 작업 1회 시간 × 이 값 (기본 3초 → 1.5초)

        [Header("디버그 배속")]
        public float[] debugSpeeds = { 1f, 2f, 3f, 4f };

        [Header("말풍선")]
        public Sprite unknownIcon;
        public Sprite packedIcon; // 📦 포장 완료 택배
        public Sprite emptyShelfIcon; // 🚫 라벨 없는 선반

        [Header("알바 (4-5장)")]
        public int workerStatMax = 5;
        public float workerSpeedBase = 2f;
        public float workerSpeedPerAgility = 0.4f;
        public float workerWorkTimeBase = 3f;
        public float workerWorkTimePerInt = 0.4f;
        public float workerWorkTimeMin = 1f;
        public int wagePerStat = 10;
        public int wageDayMultiplier = 8;
        public float stressPerSecond = 1f;
        public float stressMax = 100f;
        public float restSeconds = 10f;
        public int zoneCapacity = 3;
        public List<string> workerNames = new List<string>();

        [Header("회사 (index 0,1 = 튜토리얼 지정 회사)")]
        public List<CompanyDef> companies = new List<CompanyDef>();

        [Header("디버그 / 입력")]
        public int[] debugMoneySteps = { 1000, 10000, 100000 };
        public float joystickHoldSeconds = 0.15f;
        public float joystickStartPixels = 10f;
        public float joystickMaxPixels = 90f;

        public int UnlockCost(int level)
        {
            foreach (var z in zoneUnlocks) if (z.level == level) return z.cost;
            return 0;
        }

        public ItemDef FindItem(string itemName)
        {
            foreach (var c in companies)
            {
                if (c.itemA.name == itemName) return c.itemA;
                if (c.itemB.name == itemName) return c.itemB;
            }
            return null;
        }
    }
}
