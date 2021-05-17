using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PipelineAsync
{
    internal interface IPipeAsync
    {
        bool IsWaiting
        {
            get;
        }

        public void Create();

    }
}
