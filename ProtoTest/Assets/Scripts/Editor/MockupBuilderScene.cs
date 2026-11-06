using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mockup.EditorTools
{
    public static partial class MockupBuilder
    {
        /// <summary>
        /// 맵 배치 (2-2장 초기 7칸 + 해금 3칸). 3레벨 하차 = 왼쪽 하차구역(행2 열1)의 왼쪽,
        /// 7레벨 상차 = 오른쪽 상차구역(행2 열3)의 오른쪽, 10레벨 반품 = 7레벨 상차구역 아래.
        /// </summary>
        static readonly (int col, int row, ZoneType type, int level)[] Layout =
        {
            (0, 0, ZoneType.Office, 0),
            (1, 0, ZoneType.Ship, 0),
            (3, 1, ZoneType.Ship, 7),     // 7레벨: 오른쪽 상차구역의 오른쪽
            (0, 1, ZoneType.Unload, 0),
            (1, 1, ZoneType.Storage, 0),
            (2, 1, ZoneType.Ship, 0),
            (-1, 1, ZoneType.Unload, 3),  // 3레벨: 왼쪽 하차구역의 왼쪽
            (1, 2, ZoneType.Unload, 0),
            (2, 2, ZoneType.Storage, 0),
            (3, 2, ZoneType.Return, 10),  // 10레벨: 해금 상차구역(7레벨) 아래
        };

        static Vector3 CellPos(int col, int row) => new Vector3(col * S, 0f, -row * S);

        static readonly Dictionary<ZoneType, string> ZoneNames = new Dictionary<ZoneType, string>
        {
            { ZoneType.Office, "사무실" }, { ZoneType.Storage, "적재구역" }, { ZoneType.Unload, "하차구역" },
            { ZoneType.Ship, "상차구역" }, { ZoneType.Return, "반품구역" },
        };

        /// <summary>트럭이 붙는 바깥쪽 방향 (맵 가장자리).</summary>
        static Vector3 OuterDir(int col, int row, ZoneType type)
        {
            if (type == ZoneType.Unload) return Vector3.back;   // 하차 트럭: 구역 아래쪽
            if (type == ZoneType.Ship) return Vector3.forward;  // 상차 트럭: 구역 위쪽
            return Vector3.forward;
        }

        const float DriveDistance = 50f; // 트럭이 화면 밖에서 들어오는 거리

        /// <summary>트럭 모델 정면(local +x)을 dir 방향으로 하는 yaw.</summary>
        static float YawFacing(Vector3 dir) => Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;

        /// <summary>트럭이 구역 1/3 깊이까지 들어옴: 칸 중심에서 트럭 뒤끝까지 거리 (가장자리 방향).</summary>
        /// <summary>짐칸만 구역 안: 트럭 프리팹 메시에서 잰 짐칸 앞끝 / 뒤끝 (local x, 정면 +x). BuildScene 시작 시 채움.</summary>
        static float truckCargoFront = 0.6f, truckRear = -3.1f, truckHeight = 3.5f;

        static void BuildScene(GameConfig cfg)
        {
            TruckMetrics(out truckCargoFront, out truckRear, out truckHeight);

            // 조명 / 카메라 (쿼터뷰 고정, 5-2장)
            var light = Go("Directional Light", null, Vector3.zero).AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var rig = Go("CameraRig", null, Vector3.zero);
            rig.transform.rotation = Quaternion.Euler(0f, CamYaw, 0f);
            var camGo = Go("Main Camera", rig.transform, Quaternion.Euler(50f, 0f, 0f) * new Vector3(0f, 0f, -40f));
            camGo.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
            camGo.AddComponent<AudioListener>();
            var rigC = rig.AddComponent<CameraRig>();
            rigC.cam = cam;
            rigC.yaw = CamYaw;

            Cube("Ground", null, new Vector3(S, -0.35f, -S * 1.5f), new Vector3(S * 7f, 0.2f, S * 7f), mats["Ground"]);

            var map = Go("Map", null, Vector3.zero).transform;
            var cells = new List<ZoneCell>();
            var shelves = new List<Shelf>();
            var bays = new List<UnloadBay>();
            var tables = new List<PackingTable>();
            var shipTrucks = new List<ShipTruck>();
            var pads = new List<WorkPad>();
            var unlocks = new List<UnlockPad>();
            var waitSpots = new List<Transform>();
            Transform officeEntry = null;
            int storageIndex = 0;

            foreach (var (col, row, type, level) in Layout)
            {
                var zc = MakeCell(map, col, row, type, level);
                cells.Add(zc);
                var content = zc.contentRoot.transform;
                var d = OuterDir(col, row, type);
                var t = new Vector3(d.z, 0f, -d.x); // 가장자리 방향(접선)
                float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg; // 트럭 길이 방향(local x) = d

                switch (type)
                {
                    case ZoneType.Office:
                        officeEntry = BuildOffice(content, waitSpots);
                        break;
                    case ZoneType.Storage:
                        BuildStorage(content, storageIndex++, shelves, pads);
                        break;
                    case ZoneType.Unload:
                        bays.Add(BuildUnload(zc, content, d, t, yaw, pads));
                        break;
                    case ZoneType.Ship:
                        BuildShip(zc, content, d, t, yaw, tables, shipTrucks, pads);
                        break;
                    case ZoneType.Return:
                        Text3D(content, "Note", "반품구역\n(동작은 회의 후 추가)", 7f, new Vector3(0f, 1.5f, 0f), Color.black);
                        break;
                }

            }

            // 해금 발판: 잠긴 칸은 벽 너머라 못 들어감 → 이웃한 시작 칸 안쪽, 공유 벽 바로 앞에 둔다
            foreach (var zc in cells)
            {
                if (zc.unlockLevel == 0) continue;
                ZoneCell host = null;
                foreach (var n in cells)
                    if (n.unlockLevel == 0 && Mathf.Abs(n.grid.x - zc.grid.x) + Mathf.Abs(n.grid.y - zc.grid.y) == 1) { host = n; break; }
                var g = zc.grid - host.grid;
                var dir = new Vector3(g.x, 0f, -g.y);
                var up = Inst(unlockPrefab, zc.transform, Vector3.zero).GetComponent<UnlockPad>();
                up.transform.position = host.transform.position + dir * (S * 0.5f - 1.7f);
                up.name = $"UnlockPad_Lv{zc.unlockLevel}";
                up.cell = zc;
                up.text.text = $"{zc.displayName} 해금\n0 / {cfg.UnlockCost(zc.unlockLevel):N0}";
                up.gameObject.SetActive(false);
                unlocks.Add(up);
            }

            // 플레이어 (Basic)
            var playerGo = Go("Player", null, officeEntry.position);
            var player = playerGo.AddComponent<PlayerController>();
            player.anim = AddModel(playerGo.transform, "Basic", basicInfo);
            SetupActorParts(playerGo.transform, player, false);
            SetupBubble(player);
            player.cameraRig = rigC;
            rigC.target = playerGo.transform;
            rig.transform.position = officeEntry.position;

            // 알바 풀 (최대 10명, 미리 배치 후 비활성)
            var workersRoot = Go("Workers", null, Vector3.zero).transform;
            var workers = new Worker[10];
            for (int i = 0; i < workers.Length; i++)
            {
                var wgo = Inst(workerPrefab, workersRoot, waitSpots[i].position);
                wgo.name = $"Worker_{i}";
                workers[i] = wgo.GetComponent<Worker>();
                workers[i].slot = i;
                wgo.SetActive(false);
            }

            // GameManager
            var gmGo = Go("GameManager", null, Vector3.zero);
            var gm = gmGo.AddComponent<GameManager>();
            gm.config = cfg;
            gm.mainCamera = cam;
            gm.player = player;
            gm.workers = workers;
            gm.waitSpots = waitSpots.ToArray();
            gm.shelves = shelves.ToArray();
            gm.unloadBays = bays.ToArray();
            gm.tables = tables.ToArray();
            gm.shipTrucks = shipTrucks.ToArray();
            gm.pads = pads.ToArray();
            gm.cells = cells.ToArray();
            gm.unlockPads = unlocks.ToArray();
            gm.officeEntry = officeEntry;

            BuildUI(gm, player);

            foreach (var f in Object.FindObjectsByType<FaceCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                f.transform.rotation = CamRot;
        }

        static ZoneCell MakeCell(Transform map, int col, int row, ZoneType type, int level)
        {
            var go = Go($"Cell_R{row + 1}C{col + 1}_{type}" + (level > 0 ? $"_Lv{level}" : ""), map, CellPos(col, row));
            var zc = go.AddComponent<ZoneCell>();
            zc.type = type;
            zc.unlockLevel = level;
            zc.size = S;
            zc.displayName = ZoneNames[type];
            var m = mats[type.ToString()];
            var floor = Cube("Floor", go.transform, new Vector3(0f, -0.1f, 0f), new Vector3(S, 0.2f, S), level == 0 ? m : mats["Locked"]);
            zc.floor = floor.GetComponent<Renderer>();
            zc.unlockedMaterial = m;
            zc.lockedMaterial = mats["Locked"];
            var content = Go("Content", go.transform, Vector3.zero);
            zc.contentRoot = content;
            var name = Text3D(go.transform, "ZoneName", level == 0 ? zc.displayName : zc.displayName + "\n(잠김)", 12f, new Vector3(-S * 0.28f, 0.02f, S * 0.36f), new Color(0f, 0f, 0f, 0.55f), false);
            name.transform.localRotation = Quaternion.Euler(90f, CamYaw, 0f);
            zc.nameText = name;
            zc.grid = new Vector2Int(col, row);
            BuildWalls(zc, type);
            content.SetActive(level == 0);
            return zc;
        }

        const float WallH = 1.2f, WallT = 0.35f;

        /// <summary>칸 4변의 벽 (미리 배치, GameManager 가 이웃 해금 여부로 켜고 끔). 트럭이 들어오는 변(하차 아래 / 상차 위)은 벽 없음.</summary>
        static void BuildWalls(ZoneCell zc, ZoneType type)
        {
            string[] names = { "Wall_N", "Wall_E", "Wall_S", "Wall_W" };
            for (int d = 0; d < 4; d++)
            {
                if ((type == ZoneType.Unload && d == 2) || (type == ZoneType.Ship && d == 0)) continue; // walls[d] = null
                var w = Go(names[d], zc.transform, Vector3.zero);
                float e = S * 0.5f - WallT * 0.5f;
                Vector3 pos, scale;
                if (d == 0) { pos = new Vector3(0f, WallH * 0.5f, e); scale = new Vector3(S, WallH, WallT); }
                else if (d == 2) { pos = new Vector3(0f, WallH * 0.5f, -e); scale = new Vector3(S, WallH, WallT); }
                else if (d == 1) { pos = new Vector3(e, WallH * 0.5f, 0f); scale = new Vector3(WallT, WallH, S); }
                else { pos = new Vector3(-e, WallH * 0.5f, 0f); scale = new Vector3(WallT, WallH, S); }
                Cube("Seg", w.transform, pos, scale, mats["Wall"]);
                w.SetActive(false);
                zc.walls[d] = w;
            }
        }

        static WorkPad Pad(Transform parent, Vector3 worldPos, PadType type, string label, List<WorkPad> pads)
        {
            var p = Inst(padPrefab, parent, Vector3.zero).GetComponent<WorkPad>();
            p.transform.position = new Vector3(worldPos.x, 0f, worldPos.z);
            p.transform.rotation = Quaternion.identity;
            p.type = type;
            p.name = "Pad_" + type;
            p.GetComponentInChildren<TMPro.TMP_Text>().text = label;
            StylePad(p); // 점선 검정 + 최소 글씨 (label 인자는 무시됨)
            pads.Add(p);
            return p;
        }

        static Transform BuildOffice(Transform content, List<Transform> waitSpots)
        {
            Cube("OfficeBuilding", content, new Vector3(0f, 1.25f, 4.2f * K), new Vector3(6f * K, 2.5f, 2.6f), mats["Building"]);
            Text3D(content, "OfficeSign", "OFFICE", 9f, new Vector3(0f, 3.3f, 4.2f * K), Color.black);

            var entry = Go("OfficeEntry", content, new Vector3(0f, 0f, 1.6f * K)); // 시작 위치 (사무실은 구역 진입으로 열림)

            // 사무실 앞 작은 대기구역: 대기 알바가 겹치지 않게 설 자리 10개
            Cube("WaitingArea", content, new Vector3(0f, 0.01f, -3.4f * K), new Vector3(10.5f * K, 0.02f, 3.4f * K), mats["Waiting"]);
            Text3D(content, "WaitingLabel", "대기구역", 5f, new Vector3(-4.2f * K, 1.0f, -1.4f * K), Color.black);
            var spots = Go("WaitSpots", content, Vector3.zero).transform;
            for (int i = 0; i < 10; i++)
            {
                var s = Go($"Spot_{i}", spots, new Vector3((-4f + (i % 5) * 2f) * K, 0f, (i < 5 ? -2.7f : -4.2f) * K));
                waitSpots.Add(s.transform);
            }
            return entry.transform;
        }

        static void BuildStorage(Transform content, int cellIndex, List<Shelf> shelves, List<WorkPad> pads)
        {
            var spots = new[] { new Vector3(-2.8f, 0f, 2.6f) * K, new Vector3(2.8f, 0f, 2.6f) * K, new Vector3(-2.8f, 0f, -2.0f) * K, new Vector3(2.8f, 0f, -2.0f) * K };
            for (int i = 0; i < 4; i++)
            {
                int idx = cellIndex * 4 + i;
                var sh = Inst(shelfPrefab, content, spots[i]).GetComponent<Shelf>();
                sh.name = $"Shelf_{idx + 1}";
                sh.index = idx;
                sh.labelText.gameObject.SetActive(false); // 선반은 이모지만
                shelves.Add(sh);
                var pad = Pad(sh.transform, sh.transform.position + new Vector3(0f, 0f, -2.3f), PadType.Shelf, $"선반 {idx + 1}\n분류 · 운반", pads);
                pad.shelf = sh;
                var ins = Pad(sh.transform, sh.transform.position + new Vector3(-2.7f, 0f, -0.4f), PadType.Inspect, $"선반 {idx + 1}\n검수", pads); // 선반 왼쪽
                ins.shelf = sh;
            }
        }

        static UnloadBay BuildUnload(ZoneCell zc, Transform content, Vector3 d, Vector3 t, float yaw, List<WorkPad> pads)
        {
            var bay = content.gameObject.AddComponent<UnloadBay>();
            bay.cell = zc;
            Vector3 c = zc.transform.position;

            // 하차도크 (트럭 반대편 위쪽)
            var dockGo = Go("Dock", content, Vector3.zero);
            dockGo.transform.position = c - d * 3.5f;
            var dock = dockGo.AddComponent<Dock>();
            Cube("DockFloor", dockGo.transform, new Vector3(0f, 0.01f, 0f), new Vector3(3.2f, 0.02f, 3.2f), mats["Dock"]);
            dock.boxes = Boxes(dockGo.transform, 15, i => new Vector3(-1.0f + (i % 5) * 0.5f, 0.18f + (i / 10) * 0.37f, -0.5f + ((i / 5) % 2) * 0.6f));
            foreach (var b in dock.boxes) b.SetActive(false);
            dock.countText = Text3D(dockGo.transform, "CountText", "하차도크 0", 5.5f, new Vector3(0f, 2.0f, 0f), Color.black);
            bay.dock = dock;
            for (int k = -1; k <= 1; k += 2)
            {
                var p = Pad(dockGo.transform, dockGo.transform.position + d * 2.4f + t * (1.2f * k), PadType.DockPickup, "하차도크\n하차(놓기) · 분류(들기)", pads);
                p.dock = dock;
            }

            // 트럭 2대 (2번째는 8레벨 보상, 기획서 "하차 트럭 최대 칸 수 × 2대")
            bay.trucks = new UnloadTruck[2];
            for (int i = 0; i < 2; i++)
            {
                // 정면은 아래(-z). 아래에서 후진으로 들어와 하차, 전진으로 아래로 나감
                float off = (i == 0 ? -3f : 3f) * K;
                var tg = Inst(truckPrefab, content, Vector3.zero, YawFacing(Vector3.back));
                tg.name = $"UnloadTruck_{i + 1}";
                tg.transform.position = c + d * (S * 0.5f - truckCargoFront) + t * off;
                var ut = tg.AddComponent<UnloadTruck>();
                ut.view = tg.GetComponent<TruckView>();
                ut.view.driveOffset = Vector3.back * DriveDistance;
                var p = Pad(tg.transform, c + d * (S * 0.5f - truckCargoFront + truckRear - 1.2f) + t * off, PadType.Unload, "하차\n(트럭에서 들기)", pads);
                p.truck = ut;
                p.dock = dock;
                ut.pad = p;
                bay.trucks[i] = ut;
                if (i == 1) tg.SetActive(false);
            }
            return bay;
        }

        static void BuildShip(ZoneCell zc, Transform content, Vector3 d, Vector3 t, float yaw, List<PackingTable> tables, List<ShipTruck> trucks, List<WorkPad> pads)
        {
            Vector3 c = zc.transform.position;

            // 포장대 (왼쪽 아래)
            var table = Inst(tablePrefab, content, Vector3.zero, yaw + 90f).GetComponent<PackingTable>();
            table.name = "PackingTable";
            table.transform.position = c - d * 2.5f - t * 1.3f; // 포장 완료 테이블과 나란히
            table.cell = zc;
            tables.Add(table);
            for (int k = -1; k <= 1; k += 2)
            {
                var p = Pad(table.transform, table.transform.position - d * 2.6f + t * (1.0f * k), PadType.PackTable, "포장대\n운반(내려놓기) · 포장", pads);
                p.table = table;
            }

            // 포장 완료 택배 테이블 (오른쪽 아래): 포장된 택배가 쌓이고, 앞 발판에서 1개씩 든다
            var pt = Go("PackedTable", content, Vector3.zero);
            pt.transform.position = c - d * 2.5f + t * 1.4f;
            Cube("Top", pt.transform, new Vector3(0f, 0.9f, 0f), new Vector3(2.4f, 0.12f, 1.4f), mats["Table"]);
            for (int lx = -1; lx <= 1; lx += 2)
                for (int lz = -1; lz <= 1; lz += 2)
                    Cube("Leg", pt.transform, new Vector3(lx * 1.05f, 0.42f, lz * 0.55f), new Vector3(0.12f, 0.84f, 0.12f), mats["Table"]);
            table.packedBoxes = Boxes(pt.transform, 10, i => new Vector3(-0.8f + (i % 6 % 3) * 0.8f, 1.14f + (i / 6) * 0.37f, (i % 6 < 3 ? -0.3f : 0.3f)));
            foreach (var b in table.packedBoxes) b.SetActive(false);
            table.packedText = Text3D(pt.transform, "PackedText", "포장 완료 택배 0", 5f, new Vector3(0f, 2.6f, 0f), Color.black);
            var pp = Pad(pt.transform, pt.transform.position + t * 2.3f, PadType.PackedPickup, "포장 택배\n들기", pads);
            pp.table = table;

            // 트럭: 정면은 위(+z). 위에서 후진으로 구역 1/3 까지 들어와 상차, 전진으로 위로 나감
            var tg = Inst(truckPrefab, content, Vector3.zero, YawFacing(Vector3.forward));
            tg.name = "ShipTruck";
            tg.transform.position = c + d * (S * 0.5f - truckCargoFront);
            var st = tg.AddComponent<ShipTruck>();
            st.view = tg.GetComponent<TruckView>();
            st.view.driveOffset = Vector3.forward * DriveDistance;
            st.table = table;
            var sp = Pad(tg.transform, c + d * (S * 0.5f - truckCargoFront + truckRear - 1.2f), PadType.Ship, "상차\n(트럭에 싣기)", pads);
            sp.shipTruck = st;
            trucks.Add(st);

            // [출발] 버튼: 트럭 위 월드 캔버스 (상차 트럭에만 붙는 별도 컴포넌트)
            var cv = Go("DepartCanvas", tg.transform, Vector3.zero);
            cv.transform.position = tg.transform.position + Vector3.up * (truckHeight + 0.9f);
            cv.layer = 5;
            var canvas = cv.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            cv.AddComponent<GraphicRaycaster>();
            cv.AddComponent<FaceCamera>();
            var crt = (RectTransform)cv.transform;
            crt.sizeDelta = new Vector2(260f, 90f);
            crt.localScale = Vector3.one * 0.012f;
            st.departButton = Btn(cv.transform, "DepartButton", "출발", new Color(1f, 0.85f, 0.2f), 44f, out var lbl);
            Stretch((RectTransform)st.departButton.transform);
            st.departLabel = lbl;
        }
    }
}
