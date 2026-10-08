using System;

namespace MobileMarket.Common
{
    /// <summary>Something that can carry out a short timed action (e.g. opening a crate) and may get interrupted.</summary>
    public interface ITimedActionRunner
    {
        /// <summary>
        /// Starts a timed action showing label. Calls onComplete after duration seconds, or onCancel if interrupted.
        /// Returns false (calling neither) when another timed action is already running.
        /// </summary>
        bool BeginTimedAction(string label, float duration, Action onComplete, Action onCancel);
    }
}
