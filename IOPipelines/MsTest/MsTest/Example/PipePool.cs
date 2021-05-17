using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Pipelines;
using MsTest;

namespace PipelineAsync
{


    class PipePool
    {
        private Queue<IPipeAsync> waitingPipes;
        private List<IPipeAsync> actingPipes;
        private List<IPipeAsync> removingPipes;
        private int currentSize;
        private readonly PipeOptions pipeOptions;

        public PipePool(int sizePool)
        {
            pipeOptions = PipeOptions.Default;
            currentSize = sizePool;
            waitingPipes = new();
            actingPipes = new();
            removingPipes = new();
            for (int index = 0; index < currentSize; ++index)
            {
                PipeAsync pipeEx = new PipeAsync();
                waitingPipes.Enqueue(pipeEx);
            }
        }
        public PipePool(int sizePool, PipeOptions options)
        {
            pipeOptions = options;
            currentSize = sizePool;
            waitingPipes = new();
            actingPipes = new();
            removingPipes = new();
            for (int index = 0; index < currentSize; ++index)
            {
                PipeAsync pipeEx = new PipeAsync(pipeOptions);
                waitingPipes.Enqueue(pipeEx);
            }
        }
        //public PipePool(int sizePool, bool notAwait)
        //{
        //    pipeOptions = PipeOptions.Default;
        //    currentSize = sizePool;
        //    waitingPipes = new();
        //    actingPipes = new();
        //    removingPipes = new();
        //    for (int index = 0; index < currentSize; ++index)
        //    {
        //        PipeInvoke pipeEx = new PipeInvoke(pipeOptions);
        //        waitingPipes.Enqueue(pipeEx);
        //    }
        //}
        //public PipePool(int sizePool, PipeOptions options, bool notAwait)
        //{
        //    pipeOptions = options;
        //    currentSize = sizePool;
        //    waitingPipes = new();
        //    actingPipes = new();
        //    removingPipes = new();
        //    for (int index = 0; index < currentSize; ++index)
        //    {
        //        PipeInvoke pipeEx = new PipeInvoke(pipeOptions);
        //        waitingPipes.Enqueue(pipeEx);
        //    }
        //}

        public IPipeAsync CreatePipe()
        {

            if (!waitingPipes.Any())
            {
                for (int index = 0; index < currentSize; ++index)
                {
                    PipeAsync pipeEx = new PipeAsync(pipeOptions);
                    waitingPipes.Enqueue(pipeEx);
                }
                currentSize = currentSize * 2;
            }

            IPipeAsync newbiePipe = waitingPipes.Dequeue();

            newbiePipe.Create();

            actingPipes.Add(newbiePipe);

            return newbiePipe;
        }

        public void CollectPipe()
        {
            foreach (var pipe in actingPipes)
            {
                if (pipe.IsWaiting)
                {
                    removingPipes.Add(pipe);
                }
            }

            foreach (var pipe in removingPipes)
            {
                waitingPipes.Enqueue(pipe);
                actingPipes.Remove(pipe);
            }

            removingPipes.Clear();
        }

    }
}
