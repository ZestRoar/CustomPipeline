

namespace CustomPipelines
{
    public readonly struct StateResult   // 해제되거나 에러, 강제 종료 등 외부적인 상태 체크용
    {
        internal readonly StateFlags ResultFlags;

        public StateResult(bool isCanceled, bool isCompleted)
        {
            this.ResultFlags = StateFlags.None;
            this.ResultFlags |= isCompleted ? StateFlags.Completed : StateFlags.None;
            this.ResultFlags |= isCanceled ? StateFlags.Canceled : StateFlags.None;
        }

        public bool IsCanceled => (ResultFlags & StateFlags.Canceled) != 0; // 모종의 이유로 작업이 중간 취소됬을 때
        public bool IsCompleted => (ResultFlags & StateFlags.Completed) != 0;   // 더 이상 작업이 존재하지 않을 때
    }
}
