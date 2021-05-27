using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomPipelines
{
    internal class CallbackManager
    {
       
        public CallbackManager()
        {
            ReadCallback = null;
            WriteCallback = null;
        }

        internal StateCallback ReadCallback { get; set; }
        internal StateCallback WriteCallback { get; set; }

    }
}
