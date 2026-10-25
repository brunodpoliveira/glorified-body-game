namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A brain that can tend a dead body (CorpseCare): the soldier and the unarmed civilian (Bruno, 2026-10-25: "the
    /// unarmed civvies should also have the drag/cpr logic onto them").
    /// </summary>
    public interface ICarer
    {
        /// <summary>Free to tend a body now.</summary>
        bool CanCare { get; }
        /// <summary>The care goes on with her in sight (the nurse). The others start and keep it only unseen.</summary>
        bool CareIgnoresHer { get; }
        IEnemyBody Body { get; }
        bool TryBeginCare();
        void EndCare();
        /// <summary>The barks of the care (the writer's barks.csv). Null: no bark.</summary>
        string CareStartLine { get; }
        string DragLine { get; }
        string CprLine { get; }
    }
}
