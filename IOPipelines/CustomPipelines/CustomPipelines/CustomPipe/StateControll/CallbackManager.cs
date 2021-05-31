

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
