using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Messaging;
using StackExchange.Redis;

namespace EnqueueDocuwareDataForMigrationToOpenKM
{
    class Program
    {
        
        static void Main(string[] args)
        {
            

            Worker worker = new Worker();
            worker.Start();

            while (worker.getWorkerThread().ThreadState != ThreadState.Stopped)
            {
                switch (Console.ReadKey(true).KeyChar)
                {
                    case 'p':
                        worker.Pause();
                        break;
                    case 'w':
                        worker.Resume();
                        break;
                    case 's':
                        worker.Stop();
                        break;
                }
                Thread.Sleep(100);
            }

            Console.WriteLine("Done");
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadKey();
        }


    }
}
