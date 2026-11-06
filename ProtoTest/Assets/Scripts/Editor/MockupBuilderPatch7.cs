using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        [MenuItem("Mockup/Patch Current Scene 7 (포장 발판 1개 · 줌 안내)")]
        public static void PatchScene7()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }
            int removed = PatchCore7(gm);
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log($"[Mockup] 씬 패치 7 완료: 포장 발판 {removed}개 제거");
        }

        static int PatchCore7(GameManager gm)
        {
            // 1) 포장대마다 포장 발판 1개 (포장대 앞 가운데). 알바는 발판 1개 = 1명이라 포장대마다 흩어짐
            var keep = new List<WorkPad>();
            int removed = 0;
            foreach (var t in gm.tables)
            {
                WorkPad first = null;
                foreach (var p in gm.pads)
                {
                    if (p == null || p.type != PadType.PackTable || p.table != t) continue;
                    if (first == null)
                    {
                        first = p;
                        Undo.RecordObject(p.transform, "Mockup patch7");
                        p.transform.position = new Vector3(t.transform.position.x, 0f, p.transform.position.z);
                    }
                    else
                    {
                        Undo.DestroyObjectImmediate(p.gameObject);
                        removed++;
                    }
                }
            }
            foreach (var p in gm.pads) if (p != null) keep.Add(p);
            Undo.RecordObject(gm, "Mockup patch7");
            gm.pads = keep.ToArray();

            // 2) 조작 안내에 줌 추가
            var help = GameObject.Find("Canvas/HUD/HelpBox/HelpText");
            if (help != null)
            {
                var t = help.GetComponent<TMP_Text>();
                Undo.RecordObject(t, "Mockup patch7");
                t.text = "이동: WASD / 방향키 / 화면 누르고 드래그\n작업: 점선 원형 발판 위에 서 있기\n시점: 멈춘 상태에서 우클릭 드래그 (모바일 두 손가락)\n줌: 마우스 휠 (모바일 두 손가락 벌리기·모으기)";
                var rt = (RectTransform)help.transform.parent;
                Undo.RecordObject(rt, "Mockup patch7");
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 135f);
            }
            return removed;
        }
    }
}
