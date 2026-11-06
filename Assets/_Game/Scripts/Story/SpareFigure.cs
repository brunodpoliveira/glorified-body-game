using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// No dead end in a puzzle of mk_edgar (the writer, cutscene 11): if the figure behind the rail dies before the switch is used, a spare
    /// comes out after a while and takes his place. A kill never opens the way.
    /// </summary>
    public sealed class SpareFigure : MonoBehaviour
    {
        public const float WaitSeconds = 15f;

        [Tooltip("The figure it watches, by name, under this object's parent.")]
        public string watched = "";
        [Tooltip("The switch's flag: when it is up, no more spares.")]
        public string doneFlag = "";
        [Tooltip("The markers of the spares, inactive until their turn.")]
        public EnemySpawn[] spares = new EnemySpawn[0];

        private int next;
        private float deadAt = -1f;

        public int Used => next;

        private void Update()
        {
            if (next >= spares.Length || StoryFlags.Has(doneFlag)) return;
            Transform figure = transform.parent != null ? transform.parent.Find(watched) : null;
            Health health = figure != null ? figure.GetComponent<Health>() : null;
            if (health == null || !health.IsDead)
            {
                deadAt = -1f;
                return;
            }
            if (deadAt < 0f) deadAt = Time.time;
            if (Time.time - deadAt < WaitSeconds) return;
            EnemySpawn spare = spares[next++];
            watched = spare.name;
            deadAt = -1f;
            spare.gameObject.SetActive(true); // its Awake puts the figure in its place
            RunTelemetry.Event("spare_figure", watched);
        }
    }
}
