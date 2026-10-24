using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A short line over an enemy's head for a moment (a shock, a retreat). The words are PLACEHOLDERS: the writer
    /// (glorified-body-writer) owns the barks. A world text that faces the camera, white with a dark shadow.
    /// </summary>
    public sealed class Bark : MonoBehaviour
    {
        private TextMesh text;
        private TextMesh shadow;
        private float hideAt;

        public string Shown => text != null && text.gameObject.activeSelf ? text.text : "";
        public static int Count { get; private set; }

        /// <summary>Shows the line over the head of the object for the seconds.</summary>
        public static void Say(GameObject who, string line, float seconds = 1.6f)
        {
            if (who == null || string.IsNullOrEmpty(line)) return;
            Bark bark = who.GetComponent<Bark>() ?? who.AddComponent<Bark>();
            bark.Show(line, seconds);
            Count++;
        }

        private void Show(string line, float seconds)
        {
            if (text == null)
            {
                shadow = MakeText("BarkShadow", new Color(0f, 0f, 0f, 0.85f));
                text = MakeText("Bark", Color.white);
            }
            text.text = line;
            shadow.text = line;
            text.gameObject.SetActive(true);
            shadow.gameObject.SetActive(true);
            hideAt = Time.time + seconds;
        }

        private TextMesh MakeText(string name, Color colour)
        {
            var obj = new GameObject(name);
            var mesh = obj.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = 0.035f;
            mesh.color = colour;
            mesh.fontStyle = FontStyle.Bold;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                mesh.font = font;
                obj.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return mesh;
        }

        private void LateUpdate()
        {
            if (text == null || !text.gameObject.activeSelf) return;
            if (Time.time >= hideAt)
            {
                text.gameObject.SetActive(false);
                shadow.gameObject.SetActive(false);
                return;
            }
            Camera cam = Camera.main;
            float top = transform.position.y + 2.2f * Mathf.Max(1f, transform.lossyScale.y / 1.8f);
            Vector3 at = new Vector3(transform.position.x, top, transform.position.z);
            Quaternion face = cam != null ? Quaternion.LookRotation(at - cam.transform.position, Vector3.up) : Quaternion.identity;
            text.transform.SetPositionAndRotation(at, face);
            shadow.transform.SetPositionAndRotation(at + face * new Vector3(0.012f, -0.012f, 0.01f), face);
        }

        private void OnDestroy()
        {
            if (text != null) Destroy(text.gameObject);
            if (shadow != null) Destroy(shadow.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Count = 0;
    }
}
