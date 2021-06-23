using System;
using System.IO;
using System.IO.Pipelines;
using System.Transactions;

namespace PipePerformanceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                var testApp = new PipePerformanceTest();

                testApp.InitializeSelection();
                //testApp.InitializeTargetPipe(PipeBrand.CUSTOM);
                //testApp.InitializeTargetPipe(PipeBrand.ORIGIN);
                //testApp.InitializeTargetPipe(PipeBrand.MAD);

                testApp.RunFileCopy();
                //testApp.RunFileCopy("../../../testTxt.txt");

                if (testApp.CheckFile())
                {
                    Console.WriteLine("복사 성공!");
                    testApp.DumpResult();
                }
                else
                {
                    Console.WriteLine("복사 실패!");
                }

                Console.WriteLine("계속 하려면 엔터를 눌러주세요. (종료는 ESC)");

                bool getEnter = false;
                while (getEnter == false)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(false);

                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Escape:
                            return;
                        case ConsoleKey.Enter:
                            Console.Clear();
                            getEnter = true;
                            break;
                    }
                    
                }
            }
        }
    }
}
