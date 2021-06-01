using System;

namespace CustomPipelines
{
    internal class StateCallback
    {
#nullable enable

        private Action? completeAction;
        private bool hangOnContaining;

        public bool ActionNotExist => completeAction == null;

        public StateCallback(bool repeatable = false)
        {
            this.completeAction = null;
            this.hangOnContaining = repeatable;
        }
        public StateCallback(Action? onContinue, bool repeatable = false)
        {
            this.completeAction = onContinue;
            this.hangOnContaining = repeatable;
        }

        public void SetCallback(Action? onContinue, bool repeatable = false)
        {
            this.completeAction = onContinue;
            this.hangOnContaining = repeatable;
        }
        public void RunCallback()
        {
            completeAction?.Invoke();

            if (this.hangOnContaining == false) // 콜백이 바뀌지 않는 경우 지정
            {
                this.completeAction = null;
            }
        }

    }
}
