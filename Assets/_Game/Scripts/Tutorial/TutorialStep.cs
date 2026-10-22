using System;

namespace GlorifiedBody.Tutorial
{
    /// <summary>
    /// One step of the tutorial: the presenter's line (a placeholder until the writer gives the words), the
    /// control hint with {Action} tokens that the prompt replaces with the bound keys, the condition that
    /// ends it, and what happens when it starts and ends.
    /// </summary>
    public sealed class TutorialStep
    {
        public string Id;
        public string Line;
        public string Hint;
        public Func<bool> IsDone;
        public Action OnEnter;
        public Action OnExit;

        public TutorialStep(string id, string line, string hint, Func<bool> isDone, Action onEnter = null, Action onExit = null)
        {
            Id = id;
            Line = line;
            Hint = hint;
            IsDone = isDone;
            OnEnter = onEnter;
            OnExit = onExit;
        }
    }
}
