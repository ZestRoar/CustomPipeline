
using System.Buffers;

namespace CustomPipelines.CustomPipe.StreamControll
{
    public readonly struct StateResult
    {
        internal readonly ReadOnlySequence<byte>? resultBuffer; // read 일때 버퍼를 가져옴 (flush에서는 null)
        internal readonly StateFlags resultFlags;

        public StateResult(bool isCanceled, bool isCompleted, ReadOnlySequence<byte>? buffer = null)
        {
            resultBuffer = buffer;
            resultFlags = StateFlags.None;
            resultFlags |= isCompleted ? StateFlags.Completed : StateFlags.None;
            resultFlags |= isCanceled ? StateFlags.Canceled : StateFlags.None;
        }

        public ReadOnlySequence<byte>? Buffer => resultBuffer;  // 파이프라인에서 꺼낸 메모리를 읽는 용도
        public bool IsCanceled => (resultFlags & StateFlags.Canceled) != 0; // 모종의 이유로 작업이 중간 취소됬을 때
        public bool IsCompleted => (resultFlags & StateFlags.Completed) != 0;   // 더 이상 작업이 존재하지 않을 때
    }
}
