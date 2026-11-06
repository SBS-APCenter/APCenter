using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        [MenuItem("Mockup/Patch Current Scene 5 (글씨 패널 · 3/4레벨 교환 · 능력치 5단계)")]
        public static void PatchScene5()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }
            int panels = PatchCore5(gm);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log($"[Mockup] 씬 패치 5 완료: 글씨 패널 {panels}개 추가");
        }

        static int PatchCore5(GameManager gm)
        {
            var cfg = gm.config;

            // 1) 월드 글씨 뒤 반투명 패널 (바닥 구역 이름 / 말풍선 개수는 제외)
            var sq = AssetDatabase.LoadAssetAtPath<Sprite>($"{Gen}/Textures/Square.png");
            int added = 0;
            foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "ZoneName" || t.name.StartsWith("Count_") || t.GetComponent<TextPanel>() != null) continue;
                var p = new GameObject("Panel");
                p.transform.SetParent(t.transform, false);
                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = sq;
                sr.color = new Color(1f, 1f, 1f, 0.8f);
                sr.sortingOrder = -1; // 글자보다 먼저 그림
                var tp = t.gameObject.AddComponent<TextPanel>();
                tp.text = t;
                tp.panel = sr;
                Undo.RegisterCreatedObjectUndo(p, "Mockup patch5");
                added++;
            }

            // 2) 3레벨 ↔ 4레벨 효과 교환: 3레벨 = 선반 3·4번 확장, 4레벨 = 왼쪽 하차구역 해금
            foreach (var u in cfg.shelfUpgrades) if (u.shelfA == 2) u.level = 3;
            foreach (var z in cfg.zoneUnlocks) if (z.level == 3) z.level = 4;
            foreach (var c in gm.cells)
                if (c.type == ZoneType.Unload && c.unlockLevel == 3)
                {
                    Undo.RecordObject(c, "Mockup patch5");
                    c.unlockLevel = 4;
                    c.gameObject.name = c.gameObject.name.Replace("_Lv3", "_Lv4");
                    foreach (var up in gm.unlockPads) if (up.cell == c) up.gameObject.name = "UnlockPad_Lv4";
                }

            // 3) 플레이어 능력치 최대 5단계 (이동속도 / 작업속도도 들기 개수와 같이)
            cfg.speedSteps = Steps(new[] { 6.0f, 6.6f, 7.2f, 7.8f, 8.4f }, new[] { 0, 300, 600, 1200, 2400 });
            cfg.workTimeSteps = Steps(new[] { 3.0f, 2.7f, 2.4f, 2.1f, 1.8f }, new[] { 0, 300, 600, 1200, 2400 });
            EditorUtility.SetDirty(cfg);
            return added;
        }
    }
}
