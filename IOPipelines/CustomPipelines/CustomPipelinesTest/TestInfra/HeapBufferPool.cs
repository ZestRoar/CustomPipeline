using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelinesTest
{
    public class HeapBufferPool : MemoryPool<byte>
    {
        public override int MaxBufferSize => int.MaxValue;

        public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
        {
            return new Owner(minBufferSize == -1 ? 4096 : minBufferSize);
        }

        protected override void Dispose(bool disposing)
        {

        }

        private class Owner : IMemoryOwner<byte>
        {
            public Owner(int size)
            {
                Memory = new byte[size].AsMemory();
            }

            public Memory<byte> Memory { get; }

            public void Dispose()
            {

            }
        }
    }
}
