using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsTest
{
    public struct CustomFlushResult
    {
        internal CustomResultFlags _resultFlags;

        public CustomFlushResult(CustomResultFlags resultFlags)
        {
            _resultFlags = resultFlags;
        }

        public bool IsCanceled => (_resultFlags & CustomResultFlags.Canceled) != 0;
        public bool IsCompleted => (_resultFlags & CustomResultFlags.Completed) != 0;
        public bool IsEnded => (_resultFlags & CustomResultFlags.Ended) != 0;
    }
}
