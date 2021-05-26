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
            FlushCallback = null;
            ReadCallback = null;
            WriteCallback = null;
            FlushCompletionCallback = null;
            ReadCompletionCallback = null;
            WriteCompletionCallback = null;
        }
        
        internal StateCallback FlushCallback { get; set; }
        internal StateCallback ReadCallback { get; set; }
        internal StateCallback WriteCallback { get; set; }
        internal StateCallback FlushCompletionCallback { get; set; }
        internal StateCallback ReadCompletionCallback { get; set; }
        internal StateCallback WriteCompletionCallback { get; set; }
    }
}
