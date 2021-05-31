using System;

namespace CustomPipelines
{
    internal class StateCallback
    {
        private Action? completeAction;
        private bool hangOnContaining;

        public StateCallback(bool repeatable = true)
        {
            completeAction = null;
            hangOnContaining = true;
        }
        public StateCallback(Action? onContinue, bool repeatable = true)
        {
            completeAction = onContinue;
            hangOnContaining = repeatable;
        }

        public void SetCallback(Action? onContinue, bool repeatable = true)
        {
            completeAction = onContinue;
            hangOnContaining = repeatable;
        }
        public void RunCallback()
        {
            if (completeAction != null)
            {
                completeAction.Invoke();
            }

            if (hangOnContaining == false)
            {
                completeAction = null;
            }
        }

    }
}
