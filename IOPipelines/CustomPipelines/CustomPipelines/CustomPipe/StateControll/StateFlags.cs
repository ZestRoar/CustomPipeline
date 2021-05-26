using System;

namespace CustomPipelines
{
    [Flags]
    internal enum StateFlags : byte
    {
        None = 0x0,
        Canceled = 0x1,
        Completed = 0x2
    }
}
