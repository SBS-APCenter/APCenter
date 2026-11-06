using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        const float ParcelSize = 0.45f;                       // 택배 크기 (기존 0.35)
        const float ParcelStep = ParcelSize + 0.01f;          // 쌓기 간격
        static readonly float[] ShelfBoards = { 0.71f, 1.52f, 2.33f, 3.09f }; // AI_Shelf×1.9 단 윗면 높이 (메시 측정)

        static void SetRecorded(Transform t, Vector3 localPos)
        {
            Undo.RecordObject(t, "Mockup patch4");
            t.localPosition = localPos;
            if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        static void StackCarry(Transform carryRoot)
        {
            int i = 0;
            foreach (Transform b in carryRoot) SetRecorded(b, new Vector3(0f, i++ * ParcelStep, 0f));
        }

        [MenuItem("Mockup/Patch Current Scene 4 (택배·선반 박스·업그레이드·설정·클립보드)")]
        public static void PatchScene4()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            PatchCore4(gm);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log("[Mockup] 씬 패치 4 완료");
        }

        static void PatchCore4(GameManager gm)
        {
            var cfg = gm.config;

            // 1) 택배 크기 (사용자 요청으로 Parcel 프리팹 스케일만 변경)
            var parcel = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Parcel.prefab");
            parcel.transform.localScale = Vector3.one * ParcelSize;
            PrefabUtility.SavePrefabAsset(parcel);

            // 들고 있는 박스 간격: 플레이어(씬) + 알바 프리팹
            StackCarry(gm.player.transform.Find("CarryRoot"));
            string wp = $"{PrefabDir}/Worker.prefab";
            var wroot = PrefabUtility.LoadPrefabContents(wp);
            int wi = 0;
            foreach (Transform b in wroot.transform.Find("CarryRoot")) b.localPosition = new Vector3(0f, wi++ * ParcelStep, 0f);
            PrefabUtility.SaveAsPrefabAsset(wroot, wp);
            PrefabUtility.UnloadPrefabContents(wroot);

            // 도크 / 포장 완료 테이블 박스 높이 간격
            foreach (var bay in gm.unloadBays)
                for (int i = 0; i < bay.dock.boxes.Length; i++)
                    SetRecorded(bay.dock.boxes[i].transform, new Vector3(-1.0f + (i % 5) * 0.5f, ParcelSize * 0.5f + (i / 10) * ParcelStep, -0.5f + ((i / 5) % 2) * 0.6f));
            foreach (var t in gm.tables)
                for (int i = 0; i < t.packedBoxes.Length; i++)
                    SetRecorded(t.packedBoxes[i].transform, new Vector3(-0.8f + (i % 6 % 3) * 0.8f, 0.96f + ParcelSize * 0.5f + (i / 6) * ParcelStep, i % 6 < 3 ? -0.3f : 0.3f));

            // 2) 선반: 박스 20칸 배치 (단마다 5개) + 용량/미검수/검수 텍스트 + 이모지 위로
            var rawMat = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Materials/Parcel.mat");
            var checkedMat = Mat("ParcelChecked", new Color(0.45f, 0.8f, 0.45f));
            foreach (var s in gm.shelves)
            {
                var old = s.transform.Find("Boxes");
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                var root = Go("Boxes", s.transform, Vector3.zero);
                Undo.RegisterCreatedObjectUndo(root, "Mockup patch4");
                int n = cfg.shelfUpgradedCapacity;
                var boxes = new GameObject[n];
                var rends = new Renderer[n];
                for (int i = 0; i < n; i++)
                {
                    int level = i / 5, col = i % 5;
                    float y = ShelfBoards[Mathf.Min(level, ShelfBoards.Length - 1)] + ParcelSize * 0.5f;
                    boxes[i] = Inst(parcelPrefab != null ? parcelPrefab : parcel, root.transform, new Vector3(-1.2f + col * 0.6f, y, 0f));
                    boxes[i].name = $"Parcel_{i}";
                    rends[i] = boxes[i].GetComponent<Renderer>();
                    boxes[i].SetActive(false);
                }
                Undo.RecordObject(s, "Mockup patch4");
                s.boxes = boxes;
                s.boxRenderers = rends;
                s.uncheckedMaterial = rawMat;
                s.checkedMaterial = checkedMat;
                PrefabUtility.RecordPrefabInstancePropertyModifications(s);

                if (s.labelText != null)
                {
                    Undo.RecordObject(s.labelText.gameObject, "Mockup patch4");
                    Undo.RecordObject(s.labelText, "Mockup patch4");
                    s.labelText.gameObject.SetActive(true);
                    s.labelText.fontSize = 6f;
                    SetRecorded(s.labelText.transform, new Vector3(0f, 4.0f, 0f));
                    PrefabUtility.RecordPrefabInstancePropertyModifications(s.labelText.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(s.labelText);
                }
                if (s.iconRenderer != null) SetRecorded(s.iconRenderer.transform, new Vector3(0f, 5.3f, 0f));
            }

            // 3) 플레이어 들 수 있는 택배: 1단계 4개 → 5단계 8개 (1단계당 +1)
            cfg.carrySteps = Steps(new float[] { 4, 5, 6, 7, 8 }, new[] { 0, 500, 1000, 2000, 4000 });
            EditorUtility.SetDirty(cfg);

            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            // 4) 설정 버튼: 글씨 대신 톱니바퀴, 정사각형
            var sb = canvas.transform.Find("Settings/SettingsButton");
            if (sb != null)
            {
                var gear = LoadSprite($"{Gen}/Textures/Icons/Gear.png");
                var img = sb.GetComponent<Image>();
                Undo.RecordObject(img, "Mockup patch4");
                img.sprite = gear;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;
                var rt = (RectTransform)sb;
                Undo.RecordObject(rt, "Mockup patch4");
                rt.sizeDelta = new Vector2(90f, 90f);
                var lbl = sb.Find("Label");
                if (lbl != null) { Undo.RecordObject(lbl.gameObject, "Mockup patch4"); lbl.gameObject.SetActive(false); }
            }

            // 5) 클립보드: 종이 여러 장 + 인덱스 포스트잇이 종이 사이에 끼워진 모양
            var board = canvas.transform.Find("OfficeUI/Root/Clipboard");
            var office = canvas.GetComponentInChildren<OfficeUI>(true);
            if (board != null && office != null)
            {
                var paper = (RectTransform)board.Find("Paper");
                var sheets = new List<RectTransform>();
                for (int k = 1; k <= 2; k++)
                {
                    var name = $"Sheet_{k}";
                    var sh = (RectTransform)board.Find(name);
                    if (sh == null)
                    {
                        var img = Img(board, name, k == 1 ? new Color(0.95f, 0.94f, 0.9f) : new Color(0.9f, 0.89f, 0.85f));
                        img.raycastTarget = false;
                        sh = img.rectTransform;
                        Undo.RegisterCreatedObjectUndo(sh.gameObject, "Mockup patch4");
                    }
                    sh.anchorMin = paper.anchorMin;
                    sh.anchorMax = paper.anchorMax;
                    sh.pivot = paper.pivot;
                    sh.offsetMin = paper.offsetMin + new Vector2(7f * k, -7f * k); // 오른쪽 아래로 살짝 밀린 뒷장
                    sh.offsetMax = paper.offsetMax + new Vector2(7f * k, -7f * k);
                    sh.localRotation = Quaternion.Euler(0f, 0f, k == 1 ? -0.6f : 0.8f);
                    sheets.Add(sh);
                }
                // 뒤 → 앞: 탭2, 종이2, 탭1, 종이1, 탭0, 맨 앞 종이(페이지)
                var order = new List<Transform> { office.tabRects[2], sheets[1], office.tabRects[1], sheets[0], office.tabRects[0], paper };
                for (int i = 0; i < order.Count; i++) order[i].SetSiblingIndex(i);
                Undo.RecordObject(office, "Mockup patch4");
                office.tabSelectedX = -36f; // 탭 왼쪽 끝은 종이 밑에 끼워짐
                office.tabIdleX = -66f;
                foreach (var tr in office.tabRects)
                {
                    Undo.RecordObject(tr, "Mockup patch4");
                    tr.anchoredPosition = new Vector2(office.tabIdleX, tr.anchoredPosition.y);
                    var lbl = (RectTransform)tr.Find("Label");
                    if (lbl != null) // 글자는 종이 밖으로 보이는 부분에만
                    {
                        Undo.RecordObject(lbl, "Mockup patch4");
                        lbl.offsetMin = new Vector2(-office.tabIdleX + 4f, lbl.offsetMin.y);
                        var t = lbl.GetComponent<TMP_Text>();
                        if (t != null)
                        {
                            Undo.RecordObject(t, "Mockup patch4");
                            t.enableAutoSizing = true; // 좁아진 탭 폭에 맞춰 글자 크기 자동
                            t.fontSizeMin = 14f;
                            t.fontSizeMax = 24f;
                            t.textWrappingMode = TextWrappingModes.NoWrap;
                        }
                    }
                }
            }
        }

        static Sprite LoadSprite(string p)
        {
            AssetDatabase.ImportAsset(p);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(p);
        }
    }
}
