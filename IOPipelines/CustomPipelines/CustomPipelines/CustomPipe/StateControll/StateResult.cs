
using System;
using System.Buffers;

namespace CustomPipelines
{
    public readonly struct StateResult
    {
        internal readonly StateFlags resultFlags;

        public StateResult(bool isCanceled, bool isCompleted)
        {
            resultFlags = StateFlags.None;
            resultFlags |= isCompleted ? StateFlags.Completed : StateFlags.None;
            resultFlags |= isCanceled ? StateFlags.Canceled : StateFlags.None;
        }

        public bool IsCanceled => (resultFlags & StateFlags.Canceled) != 0; // 모종의 이유로 작업이 중간 취소됬을 때
        public bool IsCompleted => (resultFlags & StateFlags.Completed) != 0;   // 더 이상 작업이 존재하지 않을 때
    }
}
