using System;
using System.Runtime.CompilerServices;

namespace CustomPipelines
{
    internal class CustomPipeState
    {
        private FlowState readState;
        private FlowState writeState;

        public CustomPipeState()
        {
            readState = FlowState.None;
            writeState = FlowState.None;
        }

        public void Reset()
        {
            readState = FlowState.None;
            writeState = FlowState.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRead()
        {
            readState |= FlowState.Running;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndRead()
        {
            readState &= ~(FlowState.Running);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginWrite()
        {
            writeState &= ~FlowState.Running;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndWrite()
        {
            writeState |= FlowState.Running;
        }


        public bool IsWritingCompleted => (writeState & FlowState.Completed) != 0;
        public bool IsReadingCompleted => (readState & FlowState.Completed) != 0;

        public bool IsWritingCanceled => (writeState & FlowState.Canceled) != 0;
        public bool IsReadingCanceled => (readState & FlowState.Canceled) != 0;
        
        public bool CanWrite => (writeState & FlowState.Running) == 0;
        public bool CanRead => (readState & FlowState.Running) == 0;

        [Flags]
        internal enum FlowState : byte
        {
            None = 0,
            Completed = 1 << 0,
            Running = 1 << 1,
            Canceled = 1 << 2
        }

    }

    
}
