using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        const float ZoneLighten = 0.7f; // 구역 바닥색: 흰색과 섞는 비율 (클수록 연함)

        static readonly (string name, Color baseColor)[] ZoneColors =
        {
            ("Office", new Color(1f, 0.78f, 0.62f)), ("Storage", new Color(1f, 0.9f, 0.45f)), ("Unload", new Color(0.45f, 0.65f, 1f)),
            ("Ship", new Color(0.5f, 0.85f, 0.5f)), ("Return", new Color(0.7f, 0.38f, 0.3f)),
        };

        /// <summary>레벨업 팝업 (화면 위쪽 가운데, 클릭 막지 않음). 미리 배치, 비활성.</summary>
        static LevelUpPopup BuildLevelPopup(RectTransform root)
        {
            var host = Stretch(UI("LevelUpPopup", root));
            var pop = host.gameObject.AddComponent<LevelUpPopup>();
            var r = Stretch(UI("Root", host));
            var box = Img(r, "Box", new Color(1f, 0.95f, 0.7f, 0.97f));
            box.raycastTarget = false;
            Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(640f, 230f));
            pop.titleText = Txt(RowTop(UI("TitleArea", box.transform), 14f, 60f), "Title", "레벨 2 달성!", 40f, new Color(0.75f, 0.35f, 0f), TextAlignmentOptions.Center);
            Stretch(pop.titleText.rectTransform);
            pop.titleText.fontStyle = FontStyles.Bold;
            pop.bodyText = Txt(Stretch(UI("BodyArea", box.transform), 24f, 14f, 24f, 80f), "Body", "", 26f, Ink, TextAlignmentOptions.Top);
            Stretch(pop.bodyText.rectTransform);
            pop.root = r.gameObject;
            pop.box = box.rectTransform;
            r.gameObject.SetActive(false);
            return pop;
        }

        [MenuItem("Mockup/Patch Current Scene 2 (사무실 구역 진입 · 레벨업 팝업 · 구역색)")]
        public static void PatchScene2()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }

            // 1) 구역 바닥색 더 연하게 (머티리얼 에셋 색만 변경)
            foreach (var (n, c) in ZoneColors)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Materials/{n}.mat");
                if (m == null) continue;
                m.SetColor("_BaseColor", Color.Lerp(c, Color.white, ZoneLighten));
                EditorUtility.SetDirty(m);
            }

            // 2) 사무실은 구역 진입으로 열림 → 입구 발판 표시 숨김 (스폰 위치용 OfficeEntry 는 유지)
            foreach (var name in new[] { "Mark", "Label" })
            {
                var t = gm.officeEntry.Find(name);
                if (t == null) continue;
                Undo.RecordObject(t.gameObject, "Mockup patch2");
                t.gameObject.SetActive(false);
            }

            // 3) 레벨업 팝업 (없을 때만)
            var canvas = GameObject.Find("Canvas");
            if (canvas != null && canvas.transform.Find("LevelUpPopup") == null)
            {
                var pop = BuildLevelPopup((RectTransform)canvas.transform);
                var office = canvas.transform.Find("OfficeUI");
                if (office != null) pop.transform.SetSiblingIndex(office.GetSiblingIndex() + 1); // 사무실 UI 위
                Undo.RegisterCreatedObjectUndo(pop.gameObject, "Mockup patch2");
                Undo.RecordObject(gm, "Mockup patch2");
                gm.levelUpPopup = pop;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log("[Mockup] 씬 패치 2 완료");
        }
    }
}
