using UnityEngine;
using UnityEngine.UI;

namespace Mockup
{
    /// <summary>
    /// 클립에 물린 종이가 윗변(힌지)을 축으로 넘어가는 모습을 원근 투영한 메시로 그린다 (Overlay 캔버스에서도 3D처럼).
    /// RectTransform 피벗 = 윗변 중앙(0.5, 1) 이어야 함. 앞면 = 페이지 스냅샷 텍스처, 뒷면 = 단색 종이.
    /// angle: 0 = 보드 위에 평평 / 90 = 화면 쪽으로 수직 / 180 = 클립 위로 넘어가 위로 뻗음 / 270 = 보드 뒤로 완전히 내려감.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PageFlipGraphic : MaskableGraphic
    {
        public Texture frontTexture;
        public Rect frontUV = new Rect(0f, 0f, 1f, 1f);
        public Color backColor = new Color(0.92f, 0.91f, 0.87f);
        [Tooltip("시점 거리 = 종이 높이 × 이 값. 작을수록 원근이 강함")]
        public float perspective = 3f;
        [Range(4, 64)] public int segments = 24;
        [Range(0f, 1f)] public float minBrightness = 0.72f;

        float angle;
        bool frontVisible = true;

        public float Angle
        {
            get => angle;
            set
            {
                angle = value;
                bool front = Mathf.Cos(angle * Mathf.Deg2Rad) >= 0f; // 법선이 화면을 향하면 앞면
                if (front != frontVisible)
                {
                    frontVisible = front;
                    SetMaterialDirty(); // 텍스처 교체
                }
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture => frontVisible && frontTexture != null ? frontTexture : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float H = r.height;
            if (H <= 0f || r.width <= 0f) return;

            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float D = H * Mathf.Max(1.2f, perspective);
            float hingeY = r.yMax;
            float cx = r.center.x;

            // 기울어질수록 어두워짐 (빛이 정면에서 온다고 가정)
            float light = Mathf.Lerp(minBrightness, 1f, Mathf.Abs(cos));
            Color baseCol = frontVisible ? color : backColor * color;
            Color32 c = new Color(baseCol.r * light, baseCol.g * light, baseCol.b * light, baseCol.a);

            int n = Mathf.Max(1, segments);
            for (int j = 0; j <= n; j++)
            {
                float t = (float)j / n;            // 0 = 힌지(윗변), 1 = 아랫변
                float dy = -H * t;                  // 힌지 기준 아래쪽(음수)
                float yr = dy * cos;                // X축 회전 후 화면상 높이
                float z = -dy * sin;                // 화면 쪽(+)으로 다가온 거리
                float s = D / Mathf.Max(D * 0.15f, D - z); // 원근 배율
                float y = hingeY + yr * s;
                float xl = cx + (r.xMin - cx) * s;
                float xr = cx + (r.xMax - cx) * s;

                float v = frontUV.yMax - frontUV.height * t;
                vh.AddVert(new Vector3(xl, y), c, new Vector2(frontUV.xMin, v));
                vh.AddVert(new Vector3(xr, y), c, new Vector2(frontUV.xMax, v));
                if (j > 0)
                {
                    int i = j * 2;
                    vh.AddTriangle(i - 2, i - 1, i + 1);
                    vh.AddTriangle(i - 2, i + 1, i);
                }
            }
        }
    }
}
