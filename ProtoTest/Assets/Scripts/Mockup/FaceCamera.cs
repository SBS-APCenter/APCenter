using UnityEngine;

namespace Mockup
{
    /// <summary>월드 라벨(선반/트럭/도크 텍스트, 출발 버튼)이 항상 카메라를 향하게.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var gm = GameManager.I;
            if (gm != null && gm.mainCamera != null) transform.rotation = gm.mainCamera.transform.rotation;
        }
    }
}
