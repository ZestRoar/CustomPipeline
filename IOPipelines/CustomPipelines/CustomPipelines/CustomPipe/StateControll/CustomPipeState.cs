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

        public void CompleteWrite()
        {
            writeState |= FlowState.Completed;
        }
        public void CompleteRead()
        {
            readState |= FlowState.Completed;
        }
        public void CancelWrite()
        {
            writeState |= FlowState.Cancelled;
        }
        public void ResumeWrite()
        {
            writeState &= ~FlowState.Cancelled;
        }
        public void CancelRead()
        {
            readState |= FlowState.Cancelled;
        }
        public void ResumeRead()
        {
            readState &= ~FlowState.Cancelled;
        }
        public bool IsWritingCompleted => (writeState & FlowState.Completed) != 0;
        public bool IsReadingCompleted => (readState & FlowState.Completed) != 0;

        public bool IsWritingCanceled => (writeState & FlowState.Cancelled) != 0;
        public bool IsReadingCanceled => (readState & FlowState.Cancelled) != 0;
        
        [Flags]
        internal enum FlowState : byte
        {
            None = 0,
            Completed = 1 << 0,
            Cancelled = 1 << 1
        }

    }

    
}
