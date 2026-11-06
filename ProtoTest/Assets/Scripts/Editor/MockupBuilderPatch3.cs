using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        static readonly Color PadRingColor = new Color(0f, 0f, 0f, 0.9f); // 점선 검정 (밝은 바닥에서 보이게)
        const float ShelfIconY = 4.4f; // 선반 이모지 높이 (선반 바로 위)

        /// <summary>발판 글씨는 최소한 (놓기 / 들기 정도).</summary>
        static string PadLabel(PadType t)
        {
            switch (t)
            {
                case PadType.Unload: return "들기";
                case PadType.DockPickup: return "놓기 · 들기";
                case PadType.Shelf: return "놓기 · 들기";
                case PadType.Inspect: return "검수";
                case PadType.PackTable: return "놓기 · 포장";
                case PadType.PackedPickup: return "들기";
                case PadType.Ship: return "놓기";
                default: return "";
            }
        }

        /// <summary>씬의 발판 인스턴스에 점선 색 / 글씨 적용 (프리팹 원본은 그대로, 인스턴스 오버라이드).</summary>
        static void StylePad(WorkPad p)
        {
            foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Undo.RecordObject(sr, "Mockup patch3");
                sr.color = PadRingColor;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
            }
            var label = p.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                Undo.RecordObject(label, "Mockup patch3");
                label.text = PadLabel(p.type);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }

        static Sprite LoadEmptyShelfIcon()
        {
            string p = $"{Gen}/Textures/Icons/EmptyShelf.png";
            AssetDatabase.ImportAsset(p);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            if (ti == null) return null;
            if (ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.spritePixelsPerUnit = 128;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }

        [MenuItem("Mockup/Patch Current Scene 3 (발판 점선 · 글씨 · 선반 이모지)")]
        public static void PatchScene3()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }

            foreach (var p in gm.pads) StylePad(p);

            // 선반: 텍스트 숨기고 이모지만 (빈 선반 = 🚫)
            foreach (var s in gm.shelves)
            {
                if (s.labelText == null) continue;
                Undo.RecordObject(s.labelText.gameObject, "Mockup patch3");
                s.labelText.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(s.labelText.gameObject);
                if (s.iconRenderer != null) // 텍스트 자리로 내림 (너무 높으면 위 구역 것처럼 보임)
                {
                    var t = s.iconRenderer.transform;
                    Undo.RecordObject(t, "Mockup patch3");
                    t.localPosition = new Vector3(t.localPosition.x, ShelfIconY, t.localPosition.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                }
            }
            gm.config.emptyShelfIcon = LoadEmptyShelfIcon();
            EditorUtility.SetDirty(gm.config);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log($"[Mockup] 씬 패치 3 완료: 발판 {gm.pads.Length}개, 선반 {gm.shelves.Length}개, 빈 선반 아이콘={(gm.config.emptyShelfIcon != null)}");
        }
    }
}
