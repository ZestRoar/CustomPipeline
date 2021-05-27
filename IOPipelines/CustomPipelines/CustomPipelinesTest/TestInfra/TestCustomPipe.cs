using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;

namespace CustomPipelinesTest
{
    internal class TestCustomPipe : CustomPipe
    {

        public int BlockingWrite(Stream? obj)
        {
            int bytes = 0;
            while (WriteAsync(obj))
            {
            }

            return bytes;
        }

        public int BlockingWrite(Object? obj)
        {
            int bytes = 0;
            while (WriteAsync(obj))
            {
            }

            return bytes;
        }
    }
}
