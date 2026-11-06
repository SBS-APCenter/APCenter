using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mockup.EditorTools
{
    /// <summary>
    /// 목업 씬/프리팹/설정 에셋을 에디터에서 한 번에 만든다 (메뉴: Mockup/Build Mockup Scene).
    /// 기획서 5-7장: 모든 오브젝트·UI·텍스트는 씬에 미리 배치 → 런타임 코드는 참조만 한다.
    /// 다시 실행하면 씬/프리팹을 새로 만든다. GameConfig 에셋은 이미 있으면 유지(수정값 보존).
    /// </summary>
    public static partial class MockupBuilder
    {
        const string Gen = "Assets/Mockup";
        const string PrefabDir = "Assets/Prefabs";
        const string ScenePath = "Assets/Scenes/Mockup.unity";
        const string FontPath = "Assets/Font/MALGUN SDF.asset";
        const float S = 18f;            // 칸 한 변
        const float K = S / 12f;        // 칸 안 배치 간격 배율 (기준 12)
        const float CharScale = 1.2f;
        const float ModelYaw = 0f;      // 캐릭터 모델 정면 보정

        static TMP_FontAsset font;
        const float CamYaw = 0f;        // 0 = 격자가 화면에 정사각형으로 보임
        static readonly Quaternion CamRot = Quaternion.Euler(50f, CamYaw, 0f);
        static Sprite ringSprite, squareSprite, circleSprite;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static GameObject parcelPrefab, truckPrefab, shelfPrefab, padPrefab, unlockPrefab, tablePrefab, workerPrefab;
        static Camera cam;

        struct CharInfo
        {
            public string walk;
            public RuntimeAnimatorController ctrl;
            public float walkIdle, carryIdle, ground;
        }

        static CharInfo basicInfo, macInfo, rabInfo;

        [MenuItem("Mockup/Build Mockup Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Mockup] 플레이 모드를 끄고 실행하세요.");
                return;
            }
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError("[Mockup] MALGUN SDF 폰트를 찾을 수 없음: " + FontPath);
                return;
            }
            // 1024 아틀라스 1장에 한글 ~115자만 들어감 → 다중 아틀라스 허용 (안 하면 □로 깨짐)
            if (!font.isMultiAtlasTexturesEnabled)
            {
                font.isMultiAtlasTexturesEnabled = true;
                EditorUtility.SetDirty(font);
            }

            Folder("Assets", "Mockup");
            foreach (var f in new[] { "Materials", "Textures", "Animators", "Data" }) Folder(Gen, f);
            Folder("Assets", "Prefabs");

            var config = MakeConfig();
            MakeTextures();
            MakeIcons(config);
            MakeMapIcons();
            MakeMaterials();
            basicInfo = PrepareChar("Basic", "Assets/Resources/Charactor/Basic/basics_walk.fbx", "Assets/Resources/Charactor/Basic/basics_carry.fbx");
            macInfo = PrepareChar("Macaron", "Assets/Resources/Charactor/Macaron/macaron_walk.fbx", "Assets/Resources/Charactor/Macaron/macaron_carry.fbx");
            rabInfo = PrepareChar("Rabbit", "Assets/Resources/Charactor/rabbit/rabbit_walk2.fbx", "Assets/Resources/Charactor/rabbit/rabbit_carry.fbx");
            MakePrefabs();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(config);
            var builtGm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            PatchCore4(builtGm); // 패치 4 내용 (택배 크기·선반 박스·설정·클립보드)
            PatchCore5(builtGm); // 패치 5 내용 (글씨 패널 · 3/4레벨 교환 · 능력치 5단계)
            PatchCore6(builtGm); // 패치 6 내용 (선반 이모지 · 발판 패널 제거 · 트럭 글씨 · 출발 버튼)
            PatchCore7(builtGm); // 패치 7 내용 (포장 발판 1개 · 줌 안내)
            PatchCore8(builtGm); // 패치 8 내용 (하차도크 박스 20칸)
            RecordPrefabOverrides();
            CheckFonts();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Mockup] 씬 생성 완료: " + ScenePath);
        }

        // ================= 에셋 =================

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static GameConfig MakeConfig()
        {
            string path = Gen + "/Data/GameConfig.asset";
            var c = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (c != null) return c;
            c = ScriptableObject.CreateInstance<GameConfig>();
            FillDefaults(c);
            AssetDatabase.CreateAsset(c, path);
            return c;
        }

        static ItemDef Item(string n, int price, int freq, Color col) => new ItemDef { name = n, price = price, frequency = freq, color = col };

        static void FillDefaults(GameConfig c)
        {
            c.zoneUnlocks = new List<ZoneUnlockDef>
            {
                new ZoneUnlockDef { level = 3, cost = 1000 },
                new ZoneUnlockDef { level = 7, cost = 3000 },
                new ZoneUnlockDef { level = 10, cost = 5000 },
            };
            c.shelfUpgrades = new List<ShelfUpgradeDef>
            {
                new ShelfUpgradeDef { level = 2, shelfA = 0, shelfB = 1 },
                new ShelfUpgradeDef { level = 4, shelfA = 2, shelfB = 3 },
                new ShelfUpgradeDef { level = 5, shelfA = 4, shelfB = 5 },
                new ShelfUpgradeDef { level = 6, shelfA = 6, shelfB = 7 },
            };
            c.carrySteps = Steps(new float[] { 4, 5, 6, 7, 8 }, new[] { 0, 500, 1000, 2000, 4000 }); // 1단계 4개 → 5단계 8개
            c.speedSteps = Steps(new[] { 6.0f, 6.6f, 7.2f, 7.8f, 8.4f, 9.0f }, new[] { 0, 300, 600, 1200, 2400, 4800 });
            c.workTimeSteps = Steps(new[] { 3.0f, 2.7f, 2.4f, 2.1f, 1.8f, 1.5f }, new[] { 0, 300, 600, 1200, 2400, 4800 });
            c.workerNames = new List<string> { "민수", "지훈", "서연", "하은", "도윤", "수아", "예준", "지우", "시우", "하린", "유진", "현우", "채원", "준서", "다은" };
            c.companies = new List<CompanyDef>
            {
                // 0, 1 = 튜토리얼 지정 회사 (신발 / 라면). 2번째 물품은 임시.
                new CompanyDef { name = "신발 회사", contractFee = 300, itemA = Item("신발", 100, 4, new Color(0.9f, 0.4f, 0.3f)), itemB = Item("슬리퍼", 60, 3, new Color(0.9f, 0.7f, 0.3f)) },
                new CompanyDef { name = "라면 회사", contractFee = 200, itemA = Item("라면", 50, 5, new Color(0.95f, 0.85f, 0.3f)), itemB = Item("과자", 40, 3, new Color(0.6f, 0.4f, 0.2f)) },
                new CompanyDef { name = "문구 회사", contractFee = 250, itemA = Item("볼펜", 30, 4, Color.blue), itemB = Item("노트", 40, 3, Color.cyan) },
                new CompanyDef { name = "화장품 회사", contractFee = 400, itemA = Item("립스틱", 120, 2, Color.magenta), itemB = Item("로션", 90, 3, Color.white) },
                new CompanyDef { name = "전자 회사", contractFee = 500, itemA = Item("이어폰", 150, 2, Color.gray), itemB = Item("충전기", 80, 3, Color.black) },
                new CompanyDef { name = "의류 회사", contractFee = 300, itemA = Item("티셔츠", 70, 4, Color.green), itemB = Item("양말", 30, 5, Color.yellow) },
                new CompanyDef { name = "생활용품 회사", contractFee = 250, itemA = Item("휴지", 40, 5, Color.white), itemB = Item("세제", 60, 3, Color.cyan) },
            };
        }

        static List<UpgradeStep> Steps(float[] v, int[] cost)
        {
            var l = new List<UpgradeStep>();
            for (int i = 0; i < v.Length; i++) l.Add(new UpgradeStep { value = v[i], cost = cost[i] });
            return l;
        }

        static Sprite MakeSprite(string name, int size, System.Func<float, float, float> alpha)
        {
            string path = $"{Gen}/Textures/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v))));
                }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spritePixelsPerUnit = size; // 스프라이트 1장 = 1 유닛
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void MakeTextures()
        {
            // 작업 발판: 점선 테두리 원형 (4-7장)
            ringSprite = MakeSprite("PadRing", 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                if (r > 0.97f) return 0f;
                if (r > 0.80f)
                {
                    float a = Mathf.Atan2(v, u) + Mathf.PI;
                    int seg = Mathf.FloorToInt(a / (2f * Mathf.PI) * 24f);
                    return seg % 2 == 0 ? 1f : 0f;
                }
                return 0.18f;
            });
            circleSprite = MakeSprite("Circle", 128, (u, v) => (0.98f - Mathf.Sqrt(u * u + v * v)) * 40f);
            squareSprite = MakeSprite("Square", 8, (u, v) => 1f);
        }

        static Material Mat(string name, Color c)
        {
            string path = $"{Gen}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static void MakeMaterials()
        {
            Mat("Office", Light(new Color(1f, 0.78f, 0.62f)));
            Mat("Storage", Light(new Color(1f, 0.9f, 0.45f)));
            Mat("Unload", Light(new Color(0.45f, 0.65f, 1f)));
            Mat("Ship", Light(new Color(0.5f, 0.85f, 0.5f)));
            Mat("Return", Light(new Color(0.7f, 0.38f, 0.3f)));
            Mat("Locked", new Color(0.62f, 0.62f, 0.64f));
            Mat("Ground", new Color(0.8f, 0.8f, 0.82f)); // 밝게: 바깥 트럭 위 검정 글씨가 보이게
            Mat("Parcel", new Color(0.78f, 0.58f, 0.34f));
            Mat("BarBg", new Color(0.15f, 0.15f, 0.15f));
            Mat("BarFill", new Color(0.3f, 0.9f, 0.35f));
            Mat("Waiting", new Color(0.88f, 0.62f, 0.48f));
            Mat("Dock", new Color(0.32f, 0.48f, 0.85f));
            Mat("Building", new Color(0.95f, 0.6f, 0.45f));
            Mat("Wall", new Color(0.62f, 0.64f, 0.7f));
            Mat("Table", new Color(0.6f, 0.45f, 0.3f));
        }

        // ================= 캐릭터 / 애니메이션 (5-6장) =================

        static void SetLoop(string path)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            bool changed = imp.clipAnimations.Length == 0;
            foreach (var c in clips)
                if (!c.loopTime) { c.loopTime = true; changed = true; }
            if (!changed) return;
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static AnimationClip Clip(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>두 발이 가장 낮게(둘 다 접지) 있는 프레임 = idle 프레임. 발끝 최저점 = 지면 보정값.</summary>
        static void Analyze(GameObject model, AnimationClip clip, out float idleNorm, out float ground)
        {
            idleNorm = 0f;
            ground = 0f;
            var inst = (GameObject)Object.Instantiate(model);
            var l = FindDeep(inst.transform, "Bip001 L Toe0");
            var r = FindDeep(inst.transform, "Bip001 R Toe0");
            if (l != null && r != null && clip != null)
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(clip.length * clip.frameRate));
                float best = float.MaxValue, minY = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    clip.SampleAnimation(inst, clip.length * i / n);
                    float ly = l.position.y, ry = r.position.y;
                    minY = Mathf.Min(minY, Mathf.Min(ly, ry));
                    float score = Mathf.Max(ly, ry) + Mathf.Abs(ly - ry) * 0.5f;
                    if (score < best) { best = score; idleNorm = i / (float)n; }
                }
                ground = -minY;
            }
            Object.DestroyImmediate(inst);
        }

        static CharInfo PrepareChar(string name, string walkPath, string carryPath)
        {
            SetLoop(walkPath);
            SetLoop(carryPath);
            var walkClip = Clip(walkPath);
            var carryClip = Clip(carryPath);
            string cp = $"{Gen}/Animators/{name}.controller";
            AssetDatabase.DeleteAsset(cp);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(cp);
            var sm = ctrl.layers[0].stateMachine;
            var ws = sm.AddState("Walk");
            ws.motion = walkClip;
            var cs = sm.AddState("Carry");
            cs.motion = carryClip;
            sm.defaultState = ws;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(walkPath);
            Analyze(model, walkClip, out float wi, out float g);
            Analyze(model, carryClip, out float ci, out _);
            Debug.Log($"[Mockup] {name}: walk idle {wi:0.00}, carry idle {ci:0.00}, ground {g:0.00}");
            return new CharInfo { walk = walkPath, ctrl = ctrl, walkIdle = wi, carryIdle = ci, ground = g };
        }

        static CharacterAnim AddModel(Transform parent, string name, CharInfo info)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(info.walk);
            var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            m.name = name;
            m.transform.localPosition = new Vector3(0f, info.ground * CharScale, 0f);
            m.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            m.transform.localScale = Vector3.one * CharScale;
            var an = m.GetComponent<Animator>();
            if (an == null) an = m.AddComponent<Animator>();
            an.runtimeAnimatorController = info.ctrl;
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ca = m.AddComponent<CharacterAnim>();
            ca.animator = an;
            ca.walkIdleTime = info.walkIdle;
            ca.carryIdleTime = info.carryIdle;
            return ca;
        }

        // ================= 공용 생성 헬퍼 (에디터 전용) =================

        static GameObject Go(string name, Transform parent, Vector3 localPos)
        {
            var g = new GameObject(name);
            if (parent != null) g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            return g;
        }

        static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            if (parent != null) g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static SpriteRenderer Flat(string name, Transform parent, Vector3 localPos, Sprite sp, float size, Color c)
        {
            var g = Go(name, parent, localPos);
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            g.transform.localScale = Vector3.one * size;
            var r = g.AddComponent<SpriteRenderer>();
            r.sprite = sp;
            r.color = c;
            return r;
        }

        static TextMeshPro Text3D(Transform parent, string name, string text, float size, Vector3 localPos, Color c, bool faceCamera = true)
        {
            var g = Go(name, parent, localPos);
            var t = g.AddComponent<TextMeshPro>();
            t.font = font;
            t.fontSharedMaterial = font.material;
            t.text = text;
            t.fontSize = size * 1.3f;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            // 밝은 바닥 위에서 읽히도록 월드 글씨는 검정, 외곽선 없음
            t.rectTransform.sizeDelta = new Vector2(8f, 3f);
            if (faceCamera)
            {
                g.AddComponent<FaceCamera>();
                g.transform.rotation = CamRot;
            }
            return t;
        }

        static GameObject Inst(GameObject prefab, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return g;
        }

        /// <summary>이미 있는 프리팹은 덮어쓰지 않는다 (사람이 고친 크기 등 보존). 새로 만들려면 프리팹을 지우고 빌드.</summary>
        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var p = existing != null ? existing : PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return p;
        }

        /// <summary>fbx 인스턴스 + 같은 폴더 BaseColor 텍스처로 만든 URP 머티리얼 (fbx 기본 머티리얼은 텍스처 없음).</summary>
        static GameObject Model(Transform parent, string fbxPath, string baseColorPath)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath), parent);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
            if (tex == null) return g;
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            var m = Mat(name, Color.white);
            m.SetTexture("_BaseMap", tex);
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
            }
            return g;
        }

        static GameObject[] Boxes(Transform parent, int count, System.Func<int, Vector3> pos)
        {
            var arr = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = Inst(parcelPrefab, parent, pos(i));
                arr[i].name = "Parcel_" + i;
            }
            return arr;
        }

        static void WorkBar(Transform overhead, Actor a, float y)
        {
            var bar = Go("WorkBar", overhead, new Vector3(0f, y, 0f));
            Cube("Bg", bar.transform, Vector3.zero, new Vector3(1.2f, 0.16f, 0.04f), mats["BarBg"]);
            var pivot = Go("FillPivot", bar.transform, new Vector3(-0.6f, 0f, -0.03f));
            Cube("Fill", pivot.transform, new Vector3(0.6f, 0f, 0f), new Vector3(1.2f, 0.12f, 0.04f), mats["BarFill"]);
            a.workBarRoot = bar.transform;
            a.workBarFill = pivot.transform;
            bar.SetActive(false);
        }

        // ================= 프리팹 (5-3장) =================

        static void MakePrefabs()
        {
            // 택배 박스
            var parcel = Cube("Parcel", null, Vector3.zero, Vector3.one * 0.35f, mats["Parcel"]);
            parcelPrefab = SavePrefab(parcel, "Parcel");

            // 트럭 (AI_Truck) — 하차/상차 공용
            var truck = Go("Truck", null, Vector3.zero);
            var tv = truck.AddComponent<TruckView>();
            // 주의: 기획서 경로 Structure/Prop/AI_Truck 의 fbx 는 AI_Work_Table 과 같은 메시(책상)라서
            // 실제 트럭 메시인 Structure/AI_Truck 을 사용한다.
            var tModel = Go("Model", truck.transform, Vector3.zero);
            tModel.transform.localScale = Vector3.one * 2.2f;
            Model(tModel.transform, "Assets/Resources/Structure/AI_Truck/AI_Truck.fbx", "Assets/Resources/Structure/AI_Truck/AI_Truck_BaseColor.png");
            tv.modelRoot = tModel;
            tv.countText = Text3D(truck.transform, "CountText", "0/10", 7f, new Vector3(0f, 3.4f, 0f), Color.black);
            truckPrefab = SavePrefab(truck, "Truck");

            // 선반 (AI_Shelf)
            var shelf = Go("Shelf", null, Vector3.zero);
            var sc = shelf.AddComponent<Shelf>();
            var sModel = Go("Model", shelf.transform, Vector3.zero);
            sModel.transform.localScale = Vector3.one * 1.9f; // 선반 크게
            Model(sModel.transform, "Assets/Resources/Structure/Prop/AI_Shelf/AI_Shelf.fbx", "Assets/Resources/Structure/Prop/AI_Shelf/AI_Shelf_BaseColor.png");
            sc.labelText = Text3D(shelf.transform, "LabelText", "선반", 7f, new Vector3(0f, 4.1f, 0f), Color.black);
            var shIcon = Flat("Icon", shelf.transform, new Vector3(0f, ShelfIconY, 0f), unknownIconSprite, 1.5f, Color.white);
            shIcon.gameObject.AddComponent<FaceCamera>();
            shIcon.transform.rotation = CamRot;
            shIcon.gameObject.SetActive(false);
            sc.iconRenderer = shIcon;
            shelfPrefab = SavePrefab(shelf, "Shelf");

            // 작업 발판 (점선 원형)
            var pad = Go("WorkPad", null, Vector3.zero);
            pad.AddComponent<WorkPad>();
            Flat("Ring", pad.transform, new Vector3(0f, 0.03f, 0f), ringSprite, 1.8f, new Color(1f, 1f, 1f, 0.95f));
            Text3D(pad.transform, "Label", "작업", 3.5f, new Vector3(0f, 1.0f, -0.9f), Color.black);
            padPrefab = SavePrefab(pad, "WorkPad");

            // 해금 발판 (사각형 + 게이지, 작업 발판과 다른 모양/색)
            var up = Go("UnlockPad", null, Vector3.zero);
            var upc = up.AddComponent<UnlockPad>();
            Flat("Base", up.transform, new Vector3(0f, 0.04f, 0f), squareSprite, 2.6f, new Color(1f, 0.55f, 0.1f, 0.85f));
            Flat("BarBg", up.transform, new Vector3(0f, 0.06f, -0.9f), squareSprite, 1f, new Color(0.1f, 0.1f, 0.1f, 1f)).transform.localScale = new Vector3(2.2f, 0.35f, 1f);
            var fp = Go("FillPivot", up.transform, new Vector3(-1.1f, 0.08f, -0.9f));
            Flat("Fill", fp.transform, new Vector3(1.1f, 0f, 0f), squareSprite, 1f, new Color(0.3f, 1f, 0.3f, 1f)).transform.localScale = new Vector3(2.2f, 0.28f, 1f);
            upc.fill = fp.transform;
            upc.text = Text3D(up.transform, "Text", "해금", 6f, new Vector3(0f, 1.2f, 0f), Color.black);
            unlockPrefab = SavePrefab(up, "UnlockPad");

            // 포장대 (AI_Work_Table)
            var table = Go("PackingTable", null, Vector3.zero);
            var pt = table.AddComponent<PackingTable>();
            var tbModel = Go("Model", table.transform, Vector3.zero);
            tbModel.transform.localScale = Vector3.one * 1.6f; // 포장대 크게
            Model(tbModel.transform, "Assets/Resources/Structure/Prop/AI_Work_Table/AI_Work_Table.fbx", "Assets/Resources/Structure/Prop/AI_Work_Table/AI_Work_Table_BaseColor.png");
            pt.infoText = Text3D(table.transform, "InfoText", "포장대", 5.5f, new Vector3(0f, 4.0f, 0f), Color.black);
            tablePrefab = SavePrefab(table, "PackingTable");

            // 알바 (Macaron / rabbit 둘 다 들고 있고 고용 시 하나만 켬)
            var wk = Go("Worker", null, Vector3.zero);
            var w = wk.AddComponent<Worker>();
            w.anims = new[] { AddModel(wk.transform, "Macaron", macInfo), AddModel(wk.transform, "Rabbit", rabInfo) };
            w.models = new[] { w.anims[0].gameObject, w.anims[1].gameObject };
            w.models[1].SetActive(false);
            w.anim = w.anims[0];
            SetupActorParts(wk.transform, w, true);
            workerPrefab = SavePrefab(wk, "Worker");
        }

        static void SetupActorParts(Transform root, Actor a, bool nameText)
        {
            var carry = Go("CarryRoot", root, new Vector3(0f, 0.95f, 0.5f));
            a.carryBoxes = Boxes(carry.transform, 10, i => new Vector3(0f, i * 0.37f, 0f));
            foreach (var b in a.carryBoxes) b.SetActive(false);
            var over = Go("Overhead", root, new Vector3(0f, 2.3f, 0f));
            a.overheadRoot = over.transform;
            WorkBar(over.transform, a, 0f);
            if (nameText && a is Worker w)
                w.nameText = Text3D(over.transform, "NameText", "알바", 4f, new Vector3(0f, 0.4f, 0f), Color.black, false);
        }

        /// <summary>플레이어 머리 위 말풍선: 들고 있는 물품 종류별 이모지(최대 4) + 개수. 미리 배치, 스프라이트만 교체.</summary>
        static void SetupBubble(Actor a)
        {
            var root = Go("Bubble", a.overheadRoot, new Vector3(0f, 1.5f, 0f));
            a.bubbleBg = Flat("Bg", root.transform, Vector3.zero, circleSprite, 1f, Color.white);
            a.bubbleBg.transform.localRotation = Quaternion.identity;
            a.bubbleBg.transform.localScale = new Vector3(4.2f, 1.6f, 1f);
            a.bubbleBg.sortingOrder = 0;
            var tail = Flat("Tail", root.transform, new Vector3(0f, -0.85f, 0f), circleSprite, 0.45f, Color.white);
            tail.transform.localRotation = Quaternion.identity;
            tail.sortingOrder = 0;
            a.bubbleIcons = new SpriteRenderer[4];
            a.bubbleCounts = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -1.5f + i * 1.0f;
                var ic = Flat($"Icon_{i}", root.transform, new Vector3(x - 0.15f, 0.1f, -0.01f), unknownIconSprite, 0.75f, Color.white);
                ic.transform.localRotation = Quaternion.identity;
                ic.sortingOrder = 1;
                a.bubbleIcons[i] = ic;
                var t = Text3D(root.transform, $"Count_{i}", "×1", 2.6f, new Vector3(x + 0.3f, -0.3f, -0.02f), Color.black, false);
                t.rectTransform.sizeDelta = new Vector2(1f, 0.6f);
                t.sortingOrder = 2;
                a.bubbleCounts[i] = t;
                ic.gameObject.SetActive(false);
            }
            root.SetActive(false);
            a.bubbleRoot = root;
        }

        static Color Light(Color c) => Color.Lerp(c, Color.white, ZoneLighten); // 구역색 연하게

        static Sprite unknownIconSprite;

        /// <summary>
        /// 물품 이모지: 헤드리스 Chrome 으로 렌더한 Segoe UI Emoji 시트(ItemIcons.png, 8×2 칸 128px)를 칸별 PNG 로 잘라 스프라이트로 만든다.
        /// (이 Unity 의 TMP 는 U+1F000 대 이모지를 폰트 아틀라스에 넣지 못함)
        /// </summary>
        static void MakeIcons(GameConfig cfg)
        {
            string sheetPath = $"{Gen}/Textures/ItemIcons.png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            if (imp == null) { Debug.LogWarning("[Mockup] ItemIcons.png 없음 → 이모지 생략"); return; }
            if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            Folder(Gen + "/Textures", "Icons");
            var sprites = new Sprite[16];
            for (int i = 0; i < 16; i++)
            {
                int cx = i % 8, cy = i / 8;
                var px = sheet.GetPixels(cx * 128, sheet.height - (cy + 1) * 128, 128, 128);
                var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                string p = $"{Gen}/Textures/Icons/Icon_{i:00}.png";
                File.WriteAllBytes(p, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(p);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.spritePixelsPerUnit = 128;
                ti.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }
            string[] order = { "신발", "슬리퍼", "라면", "과자", "볼펜", "노트", "립스틱", "로션", "이어폰", "충전기", "티셔츠", "양말", "휴지", "세제" };
            foreach (var c in cfg.companies)
                foreach (var it in new[] { c.itemA, c.itemB })
                {
                    int k = System.Array.IndexOf(order, it.name);
                    it.icon = k >= 0 ? sprites[k] : sprites[15];
                }
            cfg.unknownIcon = sprites[15];
            cfg.packedIcon = sprites[14];
            cfg.emptyShelfIcon = LoadEmptyShelfIcon();
            unknownIconSprite = sprites[15];
            EditorUtility.SetDirty(cfg);
        }

        // ================= 마무리 =================

        static void RecordPrefabOverrides()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                foreach (var c in go.GetComponents<Component>())
                    if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }
        }

        /// <summary>5-7장: 모든 TMP 텍스트는 MALGUN SDF. 기본 폰트로 남은 것이 있으면 교체하고 경고.</summary>
        static void CheckFonts()
        {
            int fixedCount = 0, total = 0;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                total++;
                if (t.font == font) continue;
                t.font = font;
                t.fontSharedMaterial = font.material;
                fixedCount++;
            }
            if (fixedCount > 0) Debug.LogWarning($"[Mockup] MALGUN SDF 가 아닌 텍스트 {fixedCount}개 교체");
            Debug.Log($"[Mockup] TMP 텍스트 {total}개 모두 MALGUN SDF");
        }

        static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
