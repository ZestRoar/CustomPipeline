

namespace CustomPipelines
{
    public readonly struct StateResult   // 해제되거나 에러, 강제 종료 등 외부적인 상태 체크용
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
