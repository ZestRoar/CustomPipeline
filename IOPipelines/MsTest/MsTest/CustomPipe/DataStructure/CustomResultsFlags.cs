
using System;

namespace MsTest
{
    [Flags]
    public enum CustomResultFlags : byte
    {
        None = 0b0,
        Canceled = 0b01,
        Completed = 0b10
    }
}
