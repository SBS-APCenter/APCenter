using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        const float TruckTextSize = 4.5f;  // 트럭 글씨 (두 대가 나란히 와도 패널이 안 겹치게)
        const float DepartAboveText = 1.6f; // [출발] 버튼: 트럭 글씨 위로 이만큼

        [MenuItem("Mockup/Patch Current Scene 6 (선반 이모지 · 발판 패널 · 트럭 글씨 · 출발 버튼)")]
        public static void PatchScene6()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }
            PatchCore6(gm);
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log("[Mockup] 씬 패치 6 완료");
        }

        static void PatchCore6(GameManager gm)
        {
            // 1) 선반: 이모지를 글씨 패널 왼쪽에 (같은 높이), 글씨는 살짝 오른쪽
            foreach (var s in gm.shelves)
            {
                if (s.labelText != null) SetRecorded(s.labelText.transform, new Vector3(0.7f, 4.0f, 0f));
                if (s.iconRenderer != null)
                {
                    SetRecorded(s.iconRenderer.transform, new Vector3(-1.9f, 4.0f, 0f));
                    Undo.RecordObject(s.iconRenderer.transform, "Mockup patch6");
                    s.iconRenderer.transform.localScale = Vector3.one * 1.1f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(s.iconRenderer.transform);
                }
            }

            // 2) 발판 글씨: 패널 제거 (발판 위 글씨는 짧아서 패널 불필요)
            foreach (var p in gm.pads)
                foreach (var tp in p.GetComponentsInChildren<TextPanel>(true))
                {
                    if (tp.panel != null) Undo.DestroyObjectImmediate(tp.panel.gameObject);
                    Undo.DestroyObjectImmediate(tp);
                }

            // 3) 트럭 글씨 줄이기 (인스턴스 오버라이드, 프리팹 원본은 그대로)
            float textY = 0f;
            foreach (var tv in Object.FindObjectsByType<TruckView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tv.countText == null) continue;
                Undo.RecordObject(tv.countText, "Mockup patch6");
                tv.countText.fontSize = TruckTextSize;
                PrefabUtility.RecordPrefabInstancePropertyModifications(tv.countText);
                textY = tv.countText.transform.localPosition.y;
            }

            // 4) [출발] 버튼: 트럭 글씨(+패널) 위로 올림
            foreach (var st in gm.shipTrucks)
            {
                if (st.departButton == null || st.view == null || st.view.countText == null) continue;
                var cv = st.departButton.transform.parent;
                Undo.RecordObject(cv, "Mockup patch6");
                var p = st.transform.position;
                float scale = st.transform.lossyScale.y; // 트럭 프리팹 스케일 반영 (글씨 실제 높이 기준)
                cv.position = new Vector3(p.x, st.view.countText.transform.position.y + DepartAboveText * scale, p.z);
            }
        }
    }
}
