using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    class CustomPipeState
    {
        private FlowState readState;
        private FlowState writeState;
        private OperationState operationState;

        public CustomPipeState()
        {
            readState = FlowState.None;
            writeState = FlowState.None;
            operationState = OperationState.None;
        }

        internal bool FlushObserved { get; set; } = false;
        internal bool ReadObserved { get; set; } = false;

        public void Reset()
        {
            readState = FlowState.None;
            writeState = FlowState.None;
            operationState = OperationState.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRead()
        {
            if ((operationState & OperationState.Reading) == OperationState.Reading)
            {
                throw new InvalidOperationException("AlreadyReading");
            }

            operationState |= OperationState.Reading;
            readState |= FlowState.Running;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginReadTentative()
        {
            if ((operationState & OperationState.Reading) == OperationState.Reading)
            {
                throw new InvalidOperationException("AlreadyReading");
            }

            operationState |= OperationState.ReadingTentative;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndRead()
        {
            if ((operationState & OperationState.Reading) != OperationState.Reading &&
                (operationState & OperationState.ReadingTentative) != OperationState.ReadingTentative)
            {
                throw new InvalidOperationException("NoReadToComplete");
            }

            operationState &= ~(OperationState.Reading | OperationState.ReadingTentative);
            readState &= ~(FlowState.Running);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginWrite()
        {
            operationState |= OperationState.Writing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndWrite()
        {
            operationState &= ~OperationState.Writing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginFlush()
        {
            operationState |= OperationState.Flushing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndFlush()
        {
            operationState &= ~OperationState.Flushing;
            writeState &= ~FlowState.Running;
        }

        public void FinishWriting()
        {
            writeState |= FlowState.Completed;
            writeState |= FlowState.Over;          // completion 간소화하면 해당 상태 없애도 됨
        }
        public void FinishReading()
        {
            readState |= FlowState.Completed;
            readState |= FlowState.Over;
        }
        public void ResumeWriting()
        {
            writeState |= ~FlowState.Completed;
            writeState |= ~FlowState.Over;          // completion 간소화하면 해당 상태 없애도 됨
        }
        public void ResumeReading()
        {
            readState &= ~FlowState.Completed;
            readState &= ~FlowState.Over;
        }

        public bool IsWritingActive => (operationState & OperationState.Writing) == OperationState.Writing;

        public bool IsReadingActive => (operationState & OperationState.Reading) == OperationState.Reading;

        public bool IsWritingCompleted => (writeState & (FlowState.Completed | FlowState.Canceled)) != 0;

        public bool IsWritingRunning => (writeState & FlowState.Running) != 0;
        
        public bool IsReadingCompleted => (readState & (FlowState.Completed | FlowState.Canceled)) != 0;

        public bool IsReadingRunning => (readState & FlowState.Running) != 0;
        public bool IsWritingOver => (writeState & FlowState.Over) != 0;
        public bool IsReadingOver => (readState & FlowState.Over) != 0;

        [Flags]
        internal enum FlowState : byte
        {
            None = 0,
            Completed = 1 << 0,
            Running = 1 << 1,
            Canceled = 1 << 2,
            Over = 1 << 3
        }

        [Flags]
        internal enum OperationState : byte
        {
            None = 0,
            Reading = 1 << 0,
            ReadingTentative = 1 << 1,
            Writing = 1 << 2,
            Flushing = 1 << 3
        }
    }

    
}
