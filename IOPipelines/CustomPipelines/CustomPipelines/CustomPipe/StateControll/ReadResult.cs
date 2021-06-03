

using System.Buffers;

namespace CustomPipelines
{
    public readonly struct ReadResult   // 해제되거나 에러, 강제 종료 등 외부적인 상태 체크용
    {
        internal readonly ReadOnlySequence<byte>? resultBuffer;
        internal readonly StateFlags ResultFlags;

        public ReadResult(bool isCanceled, bool isCompleted)
        {
            resultBuffer = null;

            this.ResultFlags = StateFlags.None;
            this.ResultFlags |= isCompleted ? StateFlags.Completed : StateFlags.None;
            this.ResultFlags |= isCanceled ? StateFlags.Canceled : StateFlags.None;
        }
        public ReadResult(ReadOnlySequence<byte> buffer, bool isCanceled, bool isCompleted)
        {
            this.resultBuffer = buffer;

            this.ResultFlags = StateFlags.None;
            this.ResultFlags |= isCompleted ? StateFlags.Completed : StateFlags.None;
            this.ResultFlags |= isCanceled ? StateFlags.Canceled : StateFlags.None;
        }

        public ReadOnlySequence<byte>? Buffer => this.resultBuffer;
        public bool IsCanceled => (ResultFlags & StateFlags.Canceled) != 0; // 모종의 이유로 작업이 중간 취소됬을 때
        public bool IsCompleted => (ResultFlags & StateFlags.Completed) != 0;   // 더 이상 작업이 존재하지 않을 때
    }
}
