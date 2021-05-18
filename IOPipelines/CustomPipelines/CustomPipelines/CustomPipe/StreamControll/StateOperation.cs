using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    internal struct StateOperation
    {
        private State state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRead()
        {
            if ((state & State.Reading) == State.Reading)
            {
                throw new InvalidOperationException("AlreadyReading");
            }

            state |= State.Reading;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginReadTentative()
        {
            if ((state & State.Reading) == State.Reading)
            {
                throw new InvalidOperationException("AlreadyReading");
            }

            state |= State.ReadingTentative;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndRead()
        {
            if ((state & State.Reading) != State.Reading &&
                (state & State.ReadingTentative) != State.ReadingTentative)
            {
                throw new InvalidOperationException("NoReadToComplete");
            }

            state &= ~(State.Reading | State.ReadingTentative);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginWrite()
        {
            state |= State.Writing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EndWrite()
        {
            state &= ~State.Writing;
        }


        public bool IsWritingActive => (state & State.Writing) == State.Writing;

        public bool IsReadingActive => (state & State.Reading) == State.Reading;

        [Flags]
        internal enum State : byte
        {
            Reading = 1,
            ReadingTentative = 2,
            Writing = 4
        }
    }

   
}
