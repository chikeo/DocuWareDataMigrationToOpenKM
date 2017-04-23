using com.openkm.ws;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Specialized;

namespace DocuWareDataMigrationToOpenKM
{
    public class Worker
    {
        
        public static string _path = string.Empty;
        DirectoryInfo _di = null;
        static IDatabase keyValueCache = RedisConnectorHelper.Connection.GetDatabase();
        NameValueCollection nvcAllAppSettings = ConfigurationManager.AppSettings;

        ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        ManualResetEvent _pauseEvent = new ManualResetEvent(true);
        Thread _thread;

        public Worker()
        {
            _path = nvcAllAppSettings["path"];
            _di = new DirectoryInfo(_path);
        }

        public Thread getWorkerThread()
        {
            return _thread;
        }
        public void Start()
        {
            _thread = new Thread(DoWork);
            _thread.Start();
        }

        public void Pause()
        {
            float processedFilesValueInFloat = 0.0F;
            RedisValue processedFilesSizeValue = keyValueCache.StringGet("Webservice-Consumer");
            if (processedFilesSizeValue.HasValue)
            {
                processedFilesValueInFloat = Convert.ToSingle(processedFilesSizeValue);
            }
            else
            {
                processedFilesValueInFloat = 0.0F;
            }
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("The % data migrated is {0} %", ((processedFilesValueInFloat / (1024 * 1024)) / (RecursiveFileProcessor.CalculateFolderSize(_path, _shutdownEvent, _pauseEvent) / (1024 * 1024))) * 100);
            Console.WriteLine("Application, now paused.");
            Console.ResetColor();

            _pauseEvent.Reset();
        }

        public void Resume()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Application, now resumed.");
            Console.ResetColor();

            _pauseEvent.Set();
        }

        public void Stop()
        {
            float processedFilesValueInFloat = 0.0F;
            RedisValue processedFilesSizeValue = keyValueCache.StringGet("Webservice-Consumer");
            if (processedFilesSizeValue.HasValue)
            {
                processedFilesValueInFloat = Convert.ToSingle(processedFilesSizeValue);
            }
            else
            {
                processedFilesValueInFloat = 0.0F;
            }
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("The % data migrated is {0} %", ((processedFilesValueInFloat / (1024 * 1024)) / (RecursiveFileProcessor.CalculateFolderSize(_path, _shutdownEvent, _pauseEvent) / (1024 * 1024))) * 100);
            Console.WriteLine("Application, now stopping.");
            Console.ResetColor();

            // Signal the shutdown event
            _shutdownEvent.Set();

            // Make sure to resume any paused threads
            _pauseEvent.Set();

            // Wait for the thread to exit
            _thread.Join();
        }

        public void DoWork()
        {
            
            string host = nvcAllAppSettings["host"];
            string user = "okmAdmin";
            string password = "admin";
            String token = string.Empty;
            OKMWebservice webservice = null;

            try { 
                    RecursiveFileProcessor recursiveFileProcessor = new RecursiveFileProcessor(_shutdownEvent, _pauseEvent);


                    Console.Title = Path.GetFileName(_path) + " Webservice-Consumer";
                    


                    
                    if (Directory.Exists(_path))
                    {
                        LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);

                        // This path is a directory
                        recursiveFileProcessor.ProcessDirectory(_path, webservice, token, host, user, password);

                        //Log out of OpenKM
                        webservice.logout(token);
                        webservice = null;

                        Console.WriteLine("The thread has completed its work. Press ENTER to continue.");
                    }
                    else
                    {
                        //Console.WriteLine("{0} is not a valid file or directory.", _path);
                        Console.WriteLine("{0} is not a valid directory.", _path);
                    }
                }
            catch (OKMWebserviceException okex)
            {
                Console.WriteLine(okex.Message);
                Console.WriteLine(okex.Source);
                Console.WriteLine(okex.StackTrace);
                Console.WriteLine(okex.TargetSite);

                //LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.TargetSite);
                //LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
            }
            finally
            {
                if (webservice != null)
                {
                    //Log out of OpenKM
                    webservice.logout(token);
                }
            }

        }

        public static void LoginToOpenKMCommunityEdition(string host, string user, string password, ref String token, ref OKMWebservice webservice)
        {
            try
            {
                //Assuming the Application is still logged in to OpenKM, first log out.
                if (webservice != null)
                {
                    //Log out of OpenKM
                    webservice.logout(token);
                }

                //Log into OpenKM

                //Initialise WebService
                webservice = OKMWebServiceFactory.getInstance(host, user, password, OKMWebServiceFactory.COMMUNITY_6_0);

                //prepare token
                token = webservice.login();
            }
            catch (OKMWebserviceException okex)
            {
                Console.WriteLine(okex.Message);
                Console.WriteLine(okex.Source);
                Console.WriteLine(okex.StackTrace);
                Console.WriteLine(okex.TargetSite);

                //LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.TargetSite);
                //LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
            }
        }
    }
}
