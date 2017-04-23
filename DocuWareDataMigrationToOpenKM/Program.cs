using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace DocuWareDataMigrationToOpenKM
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
            Console.ReadKey();

        }

        
    }
}
