using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GlorifiedBody.Characters;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The set of the death screen (Bruno, 2026-11-16: "like the batman arkham games, it is a full screen of the enemy doing the
    /// speech, animation and the hint"): the killer's model alone in the dark, far under the world, lit from the floor in front
    /// with two rims behind, a grey glow behind him, and a low camera that moves in slowly while he speaks. The camera draws into a texture that the death screen shows full
    /// screen. The model is the class's model from the character catalog (the killer id starts with its class: "soldier_rifle"),
    /// on the Idle of its animator on real time (the world is stopped), with a nod of the head while he speaks. A hazard (the
    /// halon, the short) has no model: the screen shows a card. PLACEHOLDER until the real takes exist. Numbers PROPOSED.
    /// </summary>
    public sealed class DeathStage : MonoBehaviour
    {
        public const int Layer = 29; // no name in the TagManager; nothing else uses it
        public static readonly Vector3 Origin = new Vector3(0f, -2000f, 0f);
        public const float StartDistance = 2.4f, EndDistance = 1.9f; // m from his chest, over the screen's time
        public const float FieldOfView = 30f;

        public RenderTexture Texture { get; private set; }
        public GameObject Figure { get; private set; }
        public string ClassId { get; private set; } = "";

        private Camera cam;
        private Transform head;
        private float height = 1.75f, speakUntil, seconds = 6f, startTime;

        /// <summary>The catalog class of a killer id: the longest class id that starts it ("power_armour_guns": power_armour). Empty: a hazard.</summary>
        public static CharacterEntry ClassOf(string killerId, CharacterCatalog catalog)
        {
            if (string.IsNullOrEmpty(killerId) || catalog == null) return null;
            CharacterEntry best = null;
            foreach (CharacterEntry e in catalog.entries)
            {
                if (e == null || e.prefab == null || e.role == CharacterRole.Player || string.IsNullOrEmpty(e.id)) continue;
                if (killerId != e.id && !killerId.StartsWith(e.id + "_")) continue;
                if (best == null || e.id.Length > best.id.Length) best = e;
            }
            return best;
        }

        /// <summary>Builds the set for the killer, or returns null when the killer has no model (a hazard).</summary>
        public static DeathStage Build(string killerId, float speakSeconds, float totalSeconds)
        {
            CharacterEntry entry = ClassOf(killerId, CharacterCatalog.Load());
            if (entry == null) return null;
            var root = new GameObject("[DeathStage]");
            root.transform.position = Origin;
            DeathStage stage = root.AddComponent<DeathStage>();
            stage.Setup(entry, speakSeconds, totalSeconds);
            return stage;
        }

        private void Setup(CharacterEntry entry, float speakSeconds, float totalSeconds)
        {
            ClassId = entry.id;
            height = entry.height;
            startTime = Time.unscaledTime;
            speakUntil = startTime + speakSeconds;
            seconds = Mathf.Max(0.1f, totalSeconds);

            Figure = Instantiate(entry.prefab, transform.position, Quaternion.identity, transform);
            Figure.name = "Killer_" + entry.id;
            // A model and its animator: no game code, no physics.
            foreach (MonoBehaviour b in Figure.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(b);
            foreach (Collider c in Figure.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (Rigidbody r in Figure.GetComponentsInChildren<Rigidbody>(true)) r.isKinematic = true;
            foreach (Transform t in Figure.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            Animator animator = Figure.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime; // the world is stopped
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.isHuman) head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            // Only the stage's lights reach him: a near-black ambient probe of his own, and no sun while the screen shows.
            // (Not a rendering layer of his own: URP drops every rendering layer with no name in the project settings, and
            // only "Default" has one, so the stage's lights reached nothing.)
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.012f, 0.012f, 0.014f));
            var probe = new System.Collections.Generic.List<SphericalHarmonicsL2> { sh };
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in Figure.GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(block);
                block.CopySHCoefficientArraysFrom(probe);
                r.SetPropertyBlock(block);
                r.lightProbeUsage = LightProbeUsage.CustomProvided;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            foreach (Light sun in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (sun.type == LightType.Directional && sun.enabled)
                {
                    sun.enabled = false;
                    suns.Add(sun);
                }

            // The Arkham look (Bruno, 2026-11-16: "a spotlight on the ground pointing up"): the footlight is the key, so his
            // legs take the most light, his face less, and the light goes up into his eye sockets. A weak fill keeps the
            // face, two cool rims draw his edge on the black.
            AddLight("Foot", new Vector3(0.15f, 0.05f, 1.1f), height * 0.8f, new Color(1f, 0.93f, 0.85f), 7f, 5f, 75f, true);
            AddLight("Fill", new Vector3(0.3f, height * 0.85f, 2.4f), height * 0.85f, new Color(0.8f, 0.85f, 0.95f), 0.5f, 5f, 40f, false);
            AddLight("RimLeft", new Vector3(-1.0f, height + 0.5f, -1.1f), height * 0.75f, new Color(0.75f, 0.82f, 1f), 3f, 4f, 45f, false);
            AddLight("RimRight", new Vector3(1.0f, height + 0.5f, -1.1f), height * 0.75f, new Color(0.75f, 0.82f, 1f), 3f, 4f, 45f, false);
            Backdrop();

            var camObj = new GameObject("DeathStage_Camera");
            camObj.transform.SetParent(transform, false);
            cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Black;
            cam.cullingMask = 1 << Layer;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 30f;
            Texture = new RenderTexture(Mathf.Max(640, Screen.width), Mathf.Max(360, Screen.height), 24) { name = "DeathStage" };
            cam.targetTexture = Texture;
            Grade(cam);
            Frame(0f);
        }

        private readonly System.Collections.Generic.List<Light> suns = new System.Collections.Generic.List<Light>();
        private static readonly Color Black = new Color(0.008f, 0.007f, 0.008f);

        private void AddLight(string name, Vector3 at, float aimHeight, Color colour, float intensity, float range, float angle, bool shadows)
        {
            var obj = new GameObject("DeathStage_" + name);
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = at;
            obj.transform.LookAt(transform.position + Vector3.up * aimHeight);
            Light l = obj.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.5f;
            l.color = colour;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Hard : LightShadows.None; // his nose and brows throw their shadows up
            l.cullingMask = 1 << Layer;
        }

        /// <summary>The grey glow behind him: an unlit quad with a soft round spot, black at its edge.</summary>
        private void Backdrop()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DeathStage_Glow" };
            var glow = new Color(0.17f, 0.17f, 0.18f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    tex.SetPixel(x, y, Color.Lerp(glow, Black, Mathf.SmoothStep(0f, 1f, d)));
                }
            tex.Apply();
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "DeathStage_Backdrop";
            Destroy(quad.GetComponent<Collider>());
            quad.layer = Layer;
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, height * 0.7f, -2.5f);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // a quad shows its face to -z: turn it to the camera
            quad.transform.localScale = new Vector3(3.2f, 3.6f, 1f) * Mathf.Max(1f, height / 1.75f);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            backdrop = new Material(shader) { mainTexture = tex };
            quad.GetComponent<Renderer>().sharedMaterial = backdrop;
        }

        private Material backdrop;
        private VolumeProfile grade;

        /// <summary>The grade: little colour, hard contrast, dark corners, and a glow on the rims.</summary>
        private void Grade(Camera c)
        {
            UniversalAdditionalCameraData data = c.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1 << Layer;
            data.renderShadows = true;
            grade = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments colour = grade.Add<ColorAdjustments>(true);
            colour.saturation.Override(-55f);
            colour.contrast.Override(25f);
            Vignette vignette = grade.Add<Vignette>(true);
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.5f);
            Bloom bloom = grade.Add<Bloom>(true);
            bloom.threshold.Override(0.7f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.6f);
            var obj = new GameObject("DeathStage_Grade") { layer = Layer };
            obj.transform.SetParent(transform, false);
            Volume volume = obj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.sharedProfile = grade;
        }

        /// <summary>
        /// The camera at a share of the screen's time: low, at his chest, looking up at his face, a little to one side, and
        /// moving in.
        /// </summary>
        private void Frame(float t)
        {
            if (cam == null) return;
            float scale = Mathf.Max(1f, height / 1.75f);
            float distance = Mathf.Lerp(StartDistance, EndDistance, Mathf.SmoothStep(0f, 1f, t)) * scale;
            Vector3 look = transform.position + Vector3.up * (height * 0.8f);
            Vector3 from = transform.position + Vector3.up * (height * 0.55f) + new Vector3(0.22f, 0f, 1f).normalized * distance;
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(look - from);
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            Frame(Mathf.Clamp01((now - startTime) / seconds));
            // The speech: a small nod and turn of the head over the animator's pose, while the line plays.
            if (head != null && now < speakUntil)
            {
                float a = now - startTime;
                head.localRotation *= Quaternion.Euler(Mathf.Sin(a * 7.3f) * 3.5f, Mathf.Sin(a * 2.9f) * 4f, 0f);
            }
        }

        private void OnDestroy()
        {
            if (cam != null) cam.targetTexture = null;
            foreach (Light sun in suns)
                if (sun != null) sun.enabled = true;
            if (backdrop != null)
            {
                Destroy(backdrop.mainTexture);
                Destroy(backdrop);
            }
            if (grade != null) Destroy(grade);
            if (Texture != null)
            {
                Texture.Release();
                Destroy(Texture);
            }
        }
    }
}
