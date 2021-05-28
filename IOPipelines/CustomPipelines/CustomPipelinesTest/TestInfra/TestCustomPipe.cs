using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomPipelines;

namespace CustomPipelines
{
    internal class TestCustomPipe : CustomPipe
    {
        public TestCustomPipe(CustomPipeOptions options) 
        {

        }

        public bool WriteEmpty(int writeBytes)
        {
            return false;
        }
        
    }
}
