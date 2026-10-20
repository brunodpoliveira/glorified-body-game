using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GlorifiedBody.DebugTools;
using Debug = UnityEngine.Debug;

namespace GlorifiedBody.Gore.Warmup
{
    /// <summary>
    /// Removes first-use stalls (first wave, first heavy gore kill, first death) by paying their cost during
    /// the level load:
    ///  1. ShaderVariantCollection.WarmUp on the recorded collections (shader compile + GPU program).
    ///     This step works in the editor only: in the development player the collections report 0 variants.
    ///  2. One off-screen render of every mesh/material pair in WarmupSet.drawPrefabs, with the scene lights,
    ///     so the real passes (forward, shadow caster, depth) build their pipeline states now.
    /// Runs on each gameplay scene load (a scene with GoreManager). Telemetry event: "warmup".
    /// Memory: the class keeps no reference to the warmed assets. After the run it calls
    /// Resources.UnloadUnusedAssets, so prefabs that the scene does not use (for example the boss in GoreTest)
    /// leave memory again. The compiled shader variants and pipeline states stay. Telemetry event: "warmup_unload"
    /// (graphics driver memory before the warm-up, after it, and after the unload; development builds and editor).
    /// </summary>
    public static class FirstUseWarmup
    {
        /// <summary>Set this environment variable to skip the warm-up (for before/after measurements).</summary>
        public const string DisableEnvVar = "GB_NO_WARMUP";
        private const float StageDepth = -2000f;
        private const int StageTextureSize = 64;

        // Instance ids, not objects: a strong reference would keep every warmed material, mesh and texture
        // in memory for the whole session.
        private static readonly HashSet<long> DrawnPairs = new();
        private static Mesh quadMesh;

        /// <summary>Last warm-up timings, for tests and the HUD.</summary>
        public static float LastShaderMs { get; private set; }
        public static float LastDrawMs { get; private set; }
        public static int LastDrawCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            DrawnPairs.Clear();
            quadMesh = null;
            LastShaderMs = LastDrawMs = 0f;
            LastDrawCount = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Run(SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Run(scene.name);

        private static void Run(string sceneName)
        {
            if (GoreManager.Instance == null)
            {
                return; // menus and empty scenes: nothing to warm
            }

            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(DisableEnvVar)))
            {
                RunTelemetry.Event("warmup", $"scene={sceneName};disabled={DisableEnvVar}");
                return; // A/B measurement: the "before" run
            }

            WarmupSet set = Resources.Load<WarmupSet>(WarmupSet.ResourcePath);
            if (set == null)
            {
                Debug.LogWarning($"[Warmup] Resources/{WarmupSet.ResourcePath} missing. Run GlorifiedBody.Editor.Gore.WarmupBuilder.Build.");
                return;
            }

            long gfxBefore = Profiler.GetAllocatedMemoryForGraphicsDriver();
            var watch = Stopwatch.StartNew();
            int variants = 0;
            int shaders = 0;
            foreach (ShaderVariantCollection collection in set.collections)
            {
                if (collection == null) continue;
                variants += collection.variantCount;
                shaders += collection.shaderCount;
                // In the Linux development player (OpenGL) every collection reports 0 variants, so WarmUp
                // does nothing there. Skip the call; the off-screen draw below warms the player.
                if (collection.variantCount > 0 && !collection.isWarmedUp) collection.WarmUp();
            }

            LastShaderMs = (float)watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            LastDrawCount = set.drawOffscreen ? DrawOffscreen(set.drawPrefabs) : 0;
            LastDrawMs = (float)watch.Elapsed.TotalMilliseconds;

            string detail = $"scene={sceneName};shaders={shaders};variants={variants};shader_ms={LastShaderMs:0};draws={LastDrawCount};draw_ms={LastDrawMs:0}";
            RunTelemetry.Event("warmup", detail);
            Debug.Log("[Warmup] " + detail);

            // Release the set and the prefabs the scene does not reference. Shaders and PSOs stay warm.
            long gfxAfter = Profiler.GetAllocatedMemoryForGraphicsDriver();
            set = null;
            var unloadWatch = Stopwatch.StartNew();
            AsyncOperation unload = Resources.UnloadUnusedAssets();
            unload.completed += _ =>
            {
                long gfxUnloaded = Profiler.GetAllocatedMemoryForGraphicsDriver();
                string mem = $"scene={sceneName};gfx_mb_before={Mb(gfxBefore)};gfx_mb_after={Mb(gfxAfter)};gfx_mb_unloaded={Mb(gfxUnloaded)};unload_ms={unloadWatch.Elapsed.TotalMilliseconds:0}";
                RunTelemetry.Event("warmup_unload", mem);
                Debug.Log("[Warmup] " + mem);
            };
        }

        private static string Mb(long bytes) => (bytes / (1024f * 1024f)).ToString("0");

        /// <summary>Key of one mesh/material pair. Pure, for tests.</summary>
        public static long PairKey(int materialId, int meshId) => ((long)materialId << 32) | (uint)meshId;

        /// <summary>Renders each new mesh/material pair once with a temporary camera. Returns the pair count.</summary>
        public static int DrawOffscreen(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                return 0;
            }

            var stage = new GameObject("[WarmupStage]");
            stage.transform.position = new Vector3(0f, StageDepth, 0f);
            int count = 0;
            var renderers = new List<Renderer>(64);
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null) continue;
                renderers.Clear();
                prefab.GetComponentsInChildren(true, renderers);
                foreach (Renderer source in renderers)
                {
                    Mesh mesh = MeshOf(source);
                    if (mesh == null) continue;
                    Material[] materials = source.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        Material material = materials[m];
                        if (material == null) continue;
                        if (!DrawnPairs.Add(PairKey(material.GetInstanceID(), mesh.GetInstanceID()))) continue;
                        AddStageObject(stage.transform, mesh, Mathf.Min(m, mesh.subMeshCount - 1), material, source.shadowCastingMode);
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                RenderStage(stage.transform);
            }

            // Now, not at the end of the frame: the stage holds every warmed mesh and material, and the
            // UnloadUnusedAssets call right after the warm-up must not see it.
            Object.DestroyImmediate(stage);
            return count;
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            switch (renderer)
            {
                case SkinnedMeshRenderer skinned:
                    return skinned.sharedMesh;
                case MeshRenderer:
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    return filter != null ? filter.sharedMesh : null;
                case ParticleSystemRenderer particles:
                    return particles.renderMode == ParticleSystemRenderMode.Mesh && particles.mesh != null ? particles.mesh : Quad();
                default:
                    return Quad(); // line and trail renderers: the material matters, not the mesh
            }
        }

        private static void AddStageObject(Transform stage, Mesh mesh, int subMesh, Material material, ShadowCastingMode shadows)
        {
            var go = new GameObject("Warm_" + material.name);
            go.transform.SetParent(stage, false);
            go.transform.localPosition = new Vector3(0f, 0f, 4f);
            float size = Mathf.Max(0.001f, mesh.bounds.size.magnitude);
            go.transform.localScale = Vector3.one * (1f / size); // keep every mesh inside the view

            // Skinned meshes draw as static meshes here: URP skins on the GPU before the draw,
            // so the shader variant and pass are the same.
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // Submesh i uses material slot i. Fill the slots before it with the same material (no null slots).
            var list = new Material[subMesh + 1];
            for (int i = 0; i < list.Length; i++) list[i] = material;
            mr.sharedMaterials = list;
            mr.shadowCastingMode = shadows;
        }

        private static void RenderStage(Transform stage)
        {
            var rt = RenderTexture.GetTemporary(StageTextureSize, StageTextureSize, 24, RenderTextureFormat.Default);
            var camGo = new GameObject("WarmupCamera");
            camGo.transform.SetParent(stage, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            cam.fieldOfView = 60f;
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            // Same renderer data as the game camera, so the same passes and features run.
            UniversalAdditionalCameraData data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;

            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.Render();
            }

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
        }

        private static Mesh Quad()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "WarmupQuad" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            quadMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            quadMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return quadMesh;
        }
    }
}
