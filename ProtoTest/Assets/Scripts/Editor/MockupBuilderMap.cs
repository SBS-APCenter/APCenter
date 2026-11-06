using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        static Sprite globeSprite, chainSprite, lockSprite;
        static Sprite[] regionColor, regionGray;
        static readonly string[] RegionNames = { "시내", "항구", "공항", "산업단지", "사막", "설산", "섬" };

        /// <summary>MapIcons.png (8×2 칸 128px, 헤드리스 Chrome 렌더) → 🌍 / ⛓️ / 🔒 / 지역 7개 (+ 회색 버전).</summary>
        static void MakeMapIcons()
        {
            string sheetPath = $"{Gen}/Textures/MapIcons.png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            if (imp == null) { Debug.LogWarning("[Mockup] MapIcons.png 없음"); return; }
            if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            Folder(Gen + "/Textures", "Map");

            Sprite Cut(int i, string name, bool gray)
            {
                int cx = i % 8, cy = i / 8;
                var px = sheet.GetPixels(cx * 128, sheet.height - (cy + 1) * 128, 128, 128);
                if (gray)
                    for (int k = 0; k < px.Length; k++)
                    {
                        float l = px[k].grayscale * 0.8f + 0.1f;
                        px[k] = new Color(l, l, l, px[k].a);
                    }
                var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                string p = $"{Gen}/Textures/Map/{name}.png";
                File.WriteAllBytes(p, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(p);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }

            globeSprite = Cut(0, "Globe", false);
            chainSprite = Cut(1, "Chain", false);
            lockSprite = Cut(2, "Lock", false);
            regionColor = new Sprite[7];
            regionGray = new Sprite[7];
            for (int i = 0; i < 7; i++)
            {
                regionColor[i] = Cut(3 + i, $"Region_{i + 1}", false);
                regionGray[i] = Cut(3 + i, $"Region_{i + 1}_Gray", true);
            }
        }

        /// <summary>우측 하단 🌍 버튼 + 지역 선택 지도 (미리 배치, 런타임은 스프라이트/활성만 바꿈).</summary>
        static void BuildMap(RectTransform root)
        {
            var host = Stretch(UI("MapUI", root));
            var mp = host.gameObject.AddComponent<MapPanel>();

            var btnImg = Img(host, "MapButton", Color.white, globeSprite);
            btnImg.type = Image.Type.Simple;
            btnImg.preserveAspect = true;
            Place(btnImg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(130f, 130f));
            mp.openButton = btnImg.gameObject.AddComponent<Button>();
            mp.openButton.targetGraphic = btnImg;

            var dim = Img(host, "MapPanel", new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);
            mp.panel = dim.gameObject;
            var board = Place(Img(dim.transform, "Board", new Color(0.72f, 0.88f, 0.98f)).rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 820f));
            TxtFill(RowTop(UI("TitleArea", board), 14f, 60f), "Title", "<b>지역 선택</b>", 40f, Ink, TextAlignmentOptions.Center, 0f);
            mp.closeButton = Btn(board, "CloseButton", "닫기", BtnRed, 26f);
            Place((RectTransform)mp.closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(130f, 52f));

            // 길 따라 지그재그 배치
            var pos = new[]
            {
                new Vector2(-520f, -180f), new Vector2(-340f, 60f), new Vector2(-150f, -150f), new Vector2(40f, 90f),
                new Vector2(220f, -160f), new Vector2(390f, 70f), new Vector2(540f, -150f),
            };
            for (int i = 0; i < pos.Length - 1; i++)
            {
                var a = pos[i]; var b = pos[i + 1];
                var line = Img(board, $"Path_{i}", new Color(1f, 1f, 1f, 0.8f));
                var rt = line.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = (a + b) * 0.5f + new Vector2(0f, -20f);
                rt.sizeDelta = new Vector2((b - a).magnitude, 14f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                line.raycastTarget = false;
            }

            mp.regions = new RegionRefs[7];
            for (int i = 0; i < 7; i++)
            {
                var node = Place(UI($"Region_{i + 1}", board), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos[i], new Vector2(170f, 210f));
                var icon = Img(node, "Icon", Color.white, i == 0 ? regionColor[i] : regionGray[i]);
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(150f, 150f));
                var btn = icon.gameObject.AddComponent<Button>();
                btn.targetGraphic = icon;

                var chains = UI("Chains", icon.transform);
                Stretch(chains);
                for (int k = -1; k <= 1; k += 2) // 사슬 두 줄 X 자로 감기
                {
                    var ch = Img(chains, k < 0 ? "ChainA" : "ChainB", Color.white, chainSprite);
                    ch.type = Image.Type.Simple;
                    ch.raycastTarget = false;
                    Place(ch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 175f));
                    ch.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 40f * k);
                }
                var lk = Img(chains, "Lock", Color.white, lockSprite);
                lk.type = Image.Type.Simple;
                lk.raycastTarget = false;
                Place(lk.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(72f, 72f));
                chains.gameObject.SetActive(i != 0);

                var name = Txt(Place(UI("NameArea", node), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(170f, 56f)), "Name", $"{i + 1}지역\n{RegionNames[i]}", 22f, Ink, TextAlignmentOptions.Center);
                Stretch(name.rectTransform);
                mp.regions[i] = new RegionRefs { button = btn, icon = icon, colorSprite = regionColor[i], graySprite = regionGray[i], chains = chains.gameObject, nameText = name };
            }

            mp.toastText = Txt(Place(UI("ToastArea", board), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(800f, 50f)), "Toast", "", 28f, new Color(0.7f, 0.1f, 0.1f), TextAlignmentOptions.Center);
            Stretch(mp.toastText.rectTransform);
            mp.toastText.gameObject.SetActive(false);
            dim.gameObject.SetActive(false);
        }

        // ================= 현재 씬 패치 (프리팹은 건드리지 않음) =================

        /// <summary>트럭 프리팹 메시에서 짐칸/운전석 경계(local x)와 뒤끝(local x 최소)을 잰다. 정면 = +x.</summary>
        static void TruckMetrics(out float cargoFront, out float rear, out float height)
        {
            var tp = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Truck.prefab");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(tp);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var mf = inst.GetComponent<TruckView>().modelRoot.GetComponentInChildren<MeshFilter>();
            var vs = mf.sharedMesh.vertices;
            float minX = float.MaxValue, maxX = float.MinValue;
            height = 0f;
            var w = new Vector3[vs.Length];
            for (int i = 0; i < vs.Length; i++)
            {
                w[i] = mf.transform.TransformPoint(vs[i]);
                minX = Mathf.Min(minX, w[i].x);
                maxX = Mathf.Max(maxX, w[i].x);
                height = Mathf.Max(height, w[i].y);
            }
            const int N = 20;
            var top = new float[N];
            foreach (var p in w)
            {
                int k = Mathf.Clamp((int)((p.x - minX) / (maxX - minX) * N), 0, N - 1);
                top[k] = Mathf.Max(top[k], p.y);
            }
            // 뒤쪽부터 높은 짐칸 구간이 끝나는(높이가 꺾이는) 지점 = 짐칸 앞끝
            float boxTop = top[N / 4];
            int split = N;
            for (int k = N / 4; k < N; k++)
                if (top[k] < boxTop - 0.15f) { split = k; break; }
            cargoFront = minX + (maxX - minX) * split / N;
            rear = minX;
            Object.DestroyImmediate(inst);
        }

        [MenuItem("Mockup/Patch Current Scene (트럭 위치 · 포장대 · 지도)")]
        public static void PatchScene()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요."); return; }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogError("[Mockup] Mockup 씬을 열고 실행하세요."); return; }
            float s = gm.cells[0].size;

            // 1) 트럭: 짐칸만 구역 안. 발판은 트럭 뒤끝 앞.
            TruckMetrics(out float cargoFront, out float rear, out float h);
            void PlaceTruck(Transform truck, Transform pad, Vector3 c, float dz)
            {
                Undo.RecordObject(truck, "Mockup patch");
                var p = truck.position;
                p.z = c.z + dz * (s * 0.5f - cargoFront);
                truck.position = p;
                if (pad != null)
                {
                    Undo.RecordObject(pad, "Mockup patch");
                    pad.position = new Vector3(p.x, 0f, p.z + dz * (rear - 1.2f));
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(truck);
            }
            foreach (var b in gm.unloadBays)
                foreach (var t in b.trucks)
                    PlaceTruck(t.transform, t.pad.transform, b.cell.transform.position, -1f);
            foreach (var st in gm.shipTrucks)
            {
                WorkPad sp = null;
                foreach (var p in gm.pads) if (p.type == PadType.Ship && p.shipTruck == st) sp = p;
                PlaceTruck(st.transform, sp != null ? sp.transform : null, st.table.cell.transform.position, 1f);
                if (st.departButton != null)
                {
                    var cv = st.departButton.transform.parent;
                    Undo.RecordObject(cv, "Mockup patch");
                    cv.position = new Vector3(st.transform.position.x, h + 0.9f, st.transform.position.z);
                }
            }

            // 2) 포장대 + 포장 완료 테이블 나란히 붙이기 (구역 가로 가운데)
            foreach (var t in gm.tables)
            {
                var packed = t.transform.parent.Find("PackedTable");
                if (packed == null) continue;
                var tb = new Bounds(t.transform.position, Vector3.zero);
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>()) if (r.GetComponent<TMP_Text>() == null) tb.Encapsulate(r.bounds);
                float thw = tb.extents.x, phw = 1.2f, cx = t.cell.transform.position.x;
                float left = cx - (thw * 2f + phw * 2f) * 0.5f;
                Undo.RecordObject(t.transform, "Mockup patch");
                Undo.RecordObject(packed, "Mockup patch");
                t.transform.position = new Vector3(left + thw + (t.transform.position.x - tb.center.x), t.transform.position.y, t.transform.position.z);
                packed.position = new Vector3(left + thw * 2f + phw, packed.position.y, packed.position.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.transform);
                foreach (var p in gm.pads)
                {
                    if (p.type == PadType.PackTable && p.table == t)
                    {
                        Undo.RecordObject(p.transform, "Mockup patch");
                        float side = p.transform.position.x < t.transform.position.x ? -1f : 1f;
                        p.transform.position = new Vector3(t.transform.position.x + side * 1.0f, 0f, p.transform.position.z);
                    }
                    if (p.type == PadType.PackedPickup && p.table == t)
                    {
                        Undo.RecordObject(p.transform, "Mockup patch");
                        p.transform.position = new Vector3(packed.position.x + phw + 1.1f, 0f, packed.position.z); // 테이블 오른쪽
                    }
                }
            }

            // 3) 우측 하단 지도 (없을 때만 추가)
            var canvas = GameObject.Find("Canvas");
            if (canvas != null && canvas.transform.Find("MapUI") == null)
            {
                MakeMapIcons();
                BuildMap((RectTransform)canvas.transform);
                var map = canvas.transform.Find("MapUI");
                var settings = canvas.transform.Find("Settings");
                if (settings != null) map.SetSiblingIndex(settings.GetSiblingIndex()); // 설정 창 아래
                Undo.RegisterCreatedObjectUndo(map.gameObject, "Mockup map");
            }

            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            Debug.Log($"[Mockup] 씬 패치 완료: 짐칸 앞끝 x={cargoFront:0.00}, 뒤끝 x={rear:0.00}, 높이 {h:0.00}");
        }
    }
}
