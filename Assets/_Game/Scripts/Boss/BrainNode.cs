using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Boss
{
    /// <summary>
    /// A weak point of the brain: a swelling of the folds on a wall (PROPOSED, 2026-11-08: the design says the brain dies,
    /// not how she hurts it). Her powers hurt it like any target, and a thrown grabber too. It is a boss part: too heavy to
    /// lift, immune to the short, no mk_edgar (LockOnTargetable heavy and shock immune). A dead node goes grey and stays.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class BrainNode : MonoBehaviour
    {
        public static readonly Color Alive = new Color(0.78f, 0.42f, 0.5f), Dead = new Color(0.22f, 0.2f, 0.2f);

        private static readonly List<BrainNode> all = new List<BrainNode>();
        public static IReadOnlyList<BrainNode> All => all;

        public Health Health { get; private set; }
        public bool IsDead => Health == null || Health.IsDead;

        private void Awake()
        {
            Health = GetComponent<Health>();
            all.Add(this);
        }

        private void OnDestroy() => all.Remove(this);

        private void OnEnable()
        {
            Health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            Health.OnDeath.AddListener(OnDeath);
        }

        private void OnDisable() => Health.OnDeath?.RemoveListener(OnDeath);

        private void Start() => Paint(Alive);

        private void OnDeath() => Paint(Dead);

        private void Paint(Color c)
        {
            Renderer r = GetComponent<Renderer>();
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }
}
