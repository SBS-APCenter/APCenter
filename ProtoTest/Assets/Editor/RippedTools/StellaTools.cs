using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RippedTools
{
    /// <summary>
    /// Batchmode helpers for the ripped Stella Sora subset.
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod RippedTools.StellaTools.Run
    ///   [-captureScene Assets/x.unity] [-capturePrefab Assets/x.prefab] -captureOut C:/out.png [-captureSize 1600x900]
    ///   [-captureCam x,y,z,rx,ry,rz]
    /// Run always makes sure a URP pipeline asset is assigned before capturing.
    /// </summary>
    public static class StellaTools
    {
        const string SettingsFolder = "Assets/Settings";

        [MenuItem("Tools/Ripped/Setup URP Pipeline")]
        public static void SetupPipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsFolder + "/URP_Asset.asset");
            if (asset == null)
            {
                Directory.CreateDirectory(SettingsFolder);
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, SettingsFolder + "/URP_Renderer.asset");
                asset = UniversalRenderPipelineAsset.Create(renderer);
                asset.supportsCameraDepthTexture = true;
                asset.supportsCameraOpaqueTexture = true;
                asset.shadowDistance = 80f;
                asset.supportsHDR = true;
                AssetDatabase.CreateAsset(asset, SettingsFolder + "/URP_Asset.asset");
                AssetDatabase.SaveAssets();
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StellaTools] URP asset assigned: {AssetDatabase.GetAssetPath(asset)}");
        }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name, string fallback)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }

            SetupPipeline();
            var scene = Arg("-captureScene", null);
            var prefab = Arg("-capturePrefab", null);
            var output = Arg("-captureOut", null);
            if (output == null)
                return;
            var size = Arg("-captureSize", "1600x900").Split('x');

            if (scene != null)
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            else
                CreateStudioScene();

            GameObject focus = null;
            if (prefab != null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                if (asset == null)
                    throw new FileNotFoundException("prefab not found", prefab);
                focus = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                focus.SetActive(true);
            }

            var bounds = RendererBounds(focus);
            Debug.Log($"[StellaTools] bounds center={bounds.center} size={bounds.size}");
            var cam = PrepareCamera(Arg("-captureCam", null), bounds, focus != null);
            Capture(cam, output, int.Parse(size[0]), int.Parse(size[1]));
        }

        static void CreateStudioScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
        }

        static Bounds RendererBounds(GameObject root)
        {
            var renderers = (root != null ? root.GetComponentsInChildren<Renderer>() : UnityEngine.Object.FindObjectsOfType<Renderer>())
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer))
                .Where(r => r.bounds.size.magnitude < 400f) // skip sky domes and far cards
                .ToArray();
            Debug.Log($"[StellaTools] renderers used for framing: {renderers.Length}");
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 10f);
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        static Camera PrepareCamera(string pose, Bounds b, bool portrait)
        {
            var cam = new GameObject("Capture Camera").AddComponent<Camera>();
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            cam.fieldOfView = portrait ? 30f : 50f;
            cam.farClipPlane = 2000f;
            cam.nearClipPlane = 0.05f;
            if (pose != null)
            {
                var v = pose.Split(',').Select(float.Parse).ToArray();
                cam.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(v[3], v[4], v[5]));
            }
            else if (portrait)
            {
                float dist = b.extents.y / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;
                cam.transform.position = b.center + new Vector3(0f, b.extents.y * 0.1f, dist);
                cam.transform.LookAt(b.center);
            }
            else
            {
                float r = Mathf.Max(b.extents.x, b.extents.z);
                cam.transform.position = b.center + new Vector3(-0.7f * r, 0.6f * r, -0.7f * r);
                cam.transform.LookAt(b.center);
            }
            Debug.Log($"[StellaTools] camera pos={cam.transform.position} rot={cam.transform.eulerAngles}");
            return cam;
        }

        static void Capture(Camera cam, string output, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllBytes(output, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            Debug.Log($"[StellaTools] wrote {output}");
        }
    }
}
