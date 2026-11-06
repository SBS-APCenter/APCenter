using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>
    /// 트럭 프리팹 공용 뷰 (AI_Truck). 적재 수량 표시 + 도착/출발 주행 연출.
    /// 트럭 정면은 driveOffset 방향(바깥)을 향해 주차. 도착 = 후진으로 들어옴, 출발 = 전진으로 나감.
    /// </summary>
    public class TruckView : MonoBehaviour
    {
        public GameObject modelRoot;
        public TMP_Text countText;
        public Vector3 driveOffset = new Vector3(0f, 0f, -45f); // 월드 기준: 하차 = 아래, 상차 = 위
        public float driveSeconds = 1.5f;

        Vector3 parkPos;
        Quaternion parkRot;
        bool shown;
        int phase; // 0 정지, 1 도착 중, 2 출발 중
        float t;

        void Awake()
        {
            parkPos = modelRoot.transform.position;
            parkRot = modelRoot.transform.rotation;
            modelRoot.SetActive(false);
        }

        public void Show(bool present, string text)
        {
            if (present && !shown)
            {
                shown = true;
                phase = 1;
                t = 0f;
                modelRoot.SetActive(true);
            }
            else if (!present && shown)
            {
                shown = false;
                phase = 2;
                t = 0f;
            }
            if (countText != null) countText.text = text;
        }

        void Update()
        {
            if (phase == 0) return;
            t += Time.deltaTime / Mathf.Max(0.01f, driveSeconds);
            float k = Mathf.Clamp01(t);
            var tr = modelRoot.transform;
            if (phase == 1)
            {
                tr.SetPositionAndRotation(Vector3.Lerp(parkPos + driveOffset, parkPos, 1f - (1f - k) * (1f - k)), parkRot);
            }
            else
            {
                tr.SetPositionAndRotation(Vector3.Lerp(parkPos, parkPos + driveOffset, k * k), parkRot);
            }
            if (k < 1f) return;
            if (phase == 2)
            {
                modelRoot.SetActive(false);
                tr.SetPositionAndRotation(parkPos, parkRot);
            }
            phase = 0;
        }
    }
}
