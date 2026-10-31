namespace GlorifiedBody.Player
{
    /// <summary>
    /// The feed starts locked in the story (round 18, CANON: "the feed starts locked. Reading log L9 sets one flag that
    /// unlocks it for the rest of the game. Before the flag, a body gives no prompt and no HP"). The story raises
    /// LockFlag at its start (a sequence in W1). Her care chart (L9) raises UnlockFlag when she reads it
    /// (ItemDefinition.readFlag). A room with neither flag (the test rooms, the tutorial) feeds as before. The flags are
    /// in the saves.
    /// </summary>
    public sealed partial class Feeding
    {
        public const string LockFlag = "feed_locked";
        public const string UnlockFlag = "feed_unlocked";

        /// <summary>The feed is locked now: the story locked it and she has not read her care chart yet.</summary>
        public static bool Locked => GlorifiedBody.Saving.StoryFlags.Has(LockFlag) && !GlorifiedBody.Saving.StoryFlags.Has(UnlockFlag);
    }
}
