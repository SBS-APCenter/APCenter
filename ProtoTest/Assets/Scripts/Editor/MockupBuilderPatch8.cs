using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        [MenuItem("Mockup/Patch Current Scene 8 (하차도크 박스 20칸)")]
        public static void PatchScene8()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }
            PatchCore8(gm);
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log("[Mockup] 씬 패치 8 완료");
        }

        /// <summary>하차도크 표시 박스를 한도(dockCapacity)만큼 미리 배치 (5×2 한 층, 위로 쌓기).</summary>
        static void PatchCore8(GameManager gm)
        {
            var parcel = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Parcel.prefab");
            int cap = gm.config.dockCapacity;
            foreach (var bay in gm.unloadBays)
            {
                var dock = bay.dock;
                if (dock.boxes.Length >= cap) continue;
                var arr = new GameObject[cap];
                for (int i = 0; i < cap; i++)
                {
                    if (i < dock.boxes.Length) { arr[i] = dock.boxes[i]; continue; }
                    arr[i] = Inst(parcel, dock.transform, new Vector3(-1.0f + (i % 5) * 0.5f, ParcelSize * 0.5f + (i / 10) * ParcelStep, -0.5f + ((i / 5) % 2) * 0.6f));
                    arr[i].name = $"Parcel_{i}";
                    arr[i].SetActive(false);
                    Undo.RegisterCreatedObjectUndo(arr[i], "Mockup patch8");
                }
                Undo.RecordObject(dock, "Mockup patch8");
                dock.boxes = arr;
            }
        }
    }
}
