using System;
using System.IO;
using System.IO.Pipelines;

namespace PipePerformanceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var testApp = new PipePerformanceTest();

            testApp.InitializeTargetPipe(PipeBrand.CUSTOM);

            testApp.RunFileCopy("../../../testTxt.txt");

            if (testApp.CheckFile())
            {
                Console.WriteLine("copy succeed");
            }
            else
            {
                Console.WriteLine("copy failed");
            }

            testApp.DumpResult();
        }
    }
}
