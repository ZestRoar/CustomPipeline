using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsTest
{
    public class CustomPipeScheduler
    {
        public bool IsAsync { get; private set; }
        private readonly bool isStandard;
        public CustomPipeScheduler(bool isAsync)
        {
            IsAsync = isAsync;
#if NETSTANDARD
            IsStandard = true;
#elif NETCOREAPP
            isStandard = false;
#endif
        }

        
        public bool Schedule(System.Action<object?> action, object? state) 
            => IsAsync ? ThreadSchedule(action, state): InlineSchedule(action, state);


        private bool ThreadSchedule(System.Action<object?> action, object? state)
            => isStandard ? ThreadScheduleStandard(action, state) : ThreadScheduleCore(action, state);
        
        private bool InlineSchedule(System.Action<object?> action, object? state) 
        {
            action(state);
            return true;
        }
        private bool ThreadScheduleStandard(System.Action<object?> action, object? state)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(s =>
                {
                    var tuple = (Tuple<Action<object>, object>)s;
                    tuple.Item1(tuple.Item2);
                },
                Tuple.Create(action, state));
            return true;
        }
        private bool ThreadScheduleCore(System.Action<object?> action, object? state)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(action, state, preferLocal: false);
            return true;
        }

    }
}
