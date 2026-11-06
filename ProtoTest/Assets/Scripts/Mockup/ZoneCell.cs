using TMPro;
using UnityEngine;

namespace Mockup
{
    /// <summary>맵 칸 1개. 잠김(회색) / 해금(구역 색). contentRoot 는 해금 시 켜진다.</summary>
    public class ZoneCell : MonoBehaviour
    {
        public ZoneType type;
        public int unlockLevel;          // 0 = 시작부터 열림
        public float size = 12f;
        public Renderer floor;
        public Material lockedMaterial;
        public Material unlockedMaterial;
        public GameObject contentRoot;
        public TMP_Text nameText;
        public string displayName;
        public Vector2Int grid;                          // (열, 행)
        public GameObject[] walls = new GameObject[4];   // 0 위(+z) 1 오른쪽(+x) 2 아래(-z) 3 왼쪽(-x)

        public static readonly Vector2Int[] Dirs = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) }; // 행은 아래로 증가

        [System.NonSerialized] public bool unlocked;

        public void SetUnlocked(bool value)
        {
            unlocked = value;
            if (floor != null) floor.sharedMaterial = value ? unlockedMaterial : lockedMaterial;
            if (contentRoot != null) contentRoot.SetActive(value);
            if (nameText != null) nameText.text = value ? displayName : displayName + "\n(잠김)";
        }

        public bool Contains(Vector3 p, float margin)
        {
            var c = transform.position;
            float h = size * 0.5f - margin;
            return Mathf.Abs(p.x - c.x) <= h && Mathf.Abs(p.z - c.z) <= h;
        }
    }
}
