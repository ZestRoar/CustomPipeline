using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;

namespace CustomPipelinesTest
{
    internal class TestPoolPipeBuffer : CustomPipeBuffer
    {
        public TestPoolPipeBuffer(CustomPipeOptions options) : base(options)
        {

        }
    }
}
