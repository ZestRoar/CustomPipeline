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
            writeState = FlowState.Over;
        }

        public void Reset()
        {
            readState = FlowState.None;
            writeState = FlowState.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRead()
        {
            readState &= ~FlowState.Over;
            writeState |= FlowState.Over;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndRead()
        {
            readState |= FlowState.Over;
            writeState &= ~(FlowState.Over);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginWrite()
        {
            writeState &= ~FlowState.Over;
            readState |= FlowState.Over;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndWrite()
        {
            writeState |= FlowState.Over;
            readState &= ~(FlowState.Over);
        }


        public bool IsWritingCompleted => (writeState & (FlowState.Completed | FlowState.Canceled)) != 0;
        public bool IsReadingCompleted => (readState & (FlowState.Completed | FlowState.Canceled)) != 0;
        public bool IsWritingRunning => (writeState & FlowState.Running) != 0;
        public bool IsReadingRunning => (readState & FlowState.Running) != 0;

        public bool CanWrite => (writeState & FlowState.Over) != 0;
        public bool CanNotWrite => !CanWrite;
        public bool CanRead => (readState & FlowState.Over) != 0;
        public bool CanNotRead => !CanRead;

        [Flags]
        internal enum FlowState : byte
        {
            None = 0,
            Completed = 1 << 0,
            Running = 1 << 1,
            Canceled = 1 << 2,
            Over = 1 << 3
        }

    }

    
}
