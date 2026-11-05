using System.Collections;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A fixture of the world (DEMO_PLAN.md §3.4, round 30, Bruno: the player can microwave food, drink coffee, flush a
    /// toilet and run a faucet, no gameplay effect): the Interact key, a sound and a small animation of the prop, not of
    /// her. The microwave runs its seconds with the light on, then dings. The coffee machine fills a cup, she drinks,
    /// nothing changes. The toilet flushes, and a hider of the room answers it once ("Occupied."). The faucet runs until
    /// she leaves the room, or until she turns it off. The sounds are placeholders (tools/fixture_placeholders.py).
    /// </summary>
    public sealed class Fixture : Interactable
    {
        public enum Kind { Microwave, Coffee, Toilet, Faucet }

        public const string HumKey = "microwave_hum", DingKey = "microwave_ding", PumpKey = "coffee_pump", FlushKey = "toilet_flush", RunKey = "faucet_run";
        public const float FillSeconds = 3f, DrinkSeconds = 2f, FlushSeconds = 3f;

        public Kind kind;
        public string thing = "the microwave";
        [Tooltip("The microwave's light, the faucet's stream, the coffee cup: shown while it runs (the cup hides while she drinks).")]
        public GameObject shown;
        [Tooltip("The coffee in the cup (it grows), the toilet's water (it drops and comes back).")]
        public Transform moving;
        [Min(0.5f)] public float runSeconds = 12f;
        [Tooltip("The toilet: a hider of the room says it once when it flushes (the plan: the hider in G6, \"Occupied.\").")]
        public string answerLine = "";

        public bool Running { get; private set; }
        public int Uses { get; private set; }
        public bool Answered { get; private set; }
        public static int Dings { get; private set; }

        private AudioSource loop;
        private Coroutine run;
        private Transform her;
        private Vector3 restScale;
        private Vector3 restPosition;

        public override string Hint => "use " + thing;
        public override InteractKind KindFor(Interactor who) => InteractKind.Act;
        public override string TextFor(Interactor who) => "";

        private void Awake()
        {
            if (moving != null)
            {
                restScale = moving.localScale;
                restPosition = moving.localPosition;
            }
            if (kind == Kind.Coffee) SetFill(0f); // the cup starts empty
        }

        public override string Confirm(Interactor who)
        {
            Uses++;
            RunTelemetry.Event("fixture", $"{kind}:{name}");
            switch (kind)
            {
                case Kind.Microwave:
                    if (Running) return "fixture:busy:" + name;
                    run = StartCoroutine(Microwave());
                    return "fixture:microwave";
                case Kind.Coffee:
                    if (Running) return "fixture:busy:" + name;
                    run = StartCoroutine(Coffee());
                    return "fixture:coffee";
                case Kind.Toilet:
                    if (Running) return "fixture:busy:" + name;
                    run = StartCoroutine(Toilet());
                    return "fixture:toilet";
                default:
                    if (Running)
                    {
                        StopFaucet();
                        return "fixture:faucet_off";
                    }
                    StartFaucet(who != null ? who.transform : null);
                    return "fixture:faucet_on";
            }
        }

        private void Update()
        {
            // The faucet runs until she leaves the room (the plan).
            if (Running && kind == Kind.Faucet && her != null && !Room.Same(transform.position, her.position)) StopFaucet();
        }

        private IEnumerator Microwave()
        {
            Running = true;
            if (shown != null) shown.SetActive(true);
            loop = Sfx.Loop(HumKey, gameObject, 0.6f);
            yield return new WaitForSeconds(runSeconds);
            StopLoop();
            if (shown != null) shown.SetActive(false);
            Sfx.Play(DingKey, Point, 0.9f, 0f);
            Dings++;
            Running = false;
        }

        private IEnumerator Coffee()
        {
            Running = true;
            Sfx.Play(PumpKey, Point, 0.8f, 0f);
            float t = 0f;
            while (t < FillSeconds)
            {
                t += Time.deltaTime;
                SetFill(Mathf.Clamp01(t / FillSeconds));
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            // She drinks: the cup leaves the machine for a moment and comes back empty. Nothing changes (L9: ORAL INTAKE: NONE).
            if (shown != null) shown.SetActive(false);
            yield return new WaitForSeconds(DrinkSeconds);
            SetFill(0f);
            if (shown != null) shown.SetActive(true);
            Running = false;
        }

        /// <summary>The coffee in the cup: its height from nothing to the rest scale, standing on the cup's floor.</summary>
        private void SetFill(float u)
        {
            if (moving == null) return;
            float h = Mathf.Lerp(0.004f, restScale.y, u);
            moving.localScale = new Vector3(restScale.x, h, restScale.z);
            moving.localPosition = restPosition + Vector3.up * (h - restScale.y) * 0.5f;
        }

        private IEnumerator Toilet()
        {
            Running = true;
            Sfx.Play(FlushKey, Point, 0.9f, 0f);
            float t = 0f;
            while (t < FlushSeconds)
            {
                t += Time.deltaTime;
                if (moving != null)
                {
                    float drop = t < 0.8f ? t / 0.8f : Mathf.Clamp01(1f - (t - 0.8f) / 2.2f);
                    moving.localPosition = restPosition + Vector3.down * 0.12f * drop;
                }
                if (!Answered && t >= 0.6f) Answer();
                yield return null;
            }
            if (moving != null) moving.localPosition = restPosition;
            Running = false;
        }

        /// <summary>A hider of this room, alive, answers the flush once.</summary>
        private void Answer()
        {
            Answered = true;
            if (string.IsNullOrEmpty(answerLine)) return;
            foreach (CivilianBrain brain in FindObjectsByType<CivilianBrain>(FindObjectsSortMode.None))
            {
                if (!brain.Hides || !Room.Same(brain.transform.position, transform.position)) continue;
                var health = brain.GetComponent<GlorifiedBody.Core.Health>();
                if (health != null && health.IsDead) continue;
                Bark.Say(brain.gameObject, answerLine, 2f);
                return;
            }
        }

        private void StartFaucet(Transform who)
        {
            Running = true;
            her = who;
            if (shown != null) shown.SetActive(true);
            loop = Sfx.Loop(RunKey, gameObject, 0.5f);
        }

        private void StopFaucet()
        {
            Running = false;
            her = null;
            if (shown != null) shown.SetActive(false);
            StopLoop();
        }

        private void StopLoop()
        {
            if (loop != null) Destroy(loop);
            loop = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (run != null) StopCoroutine(run);
            run = null;
            StopLoop();
            Running = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Dings = 0;
    }
}
