using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocuWareDataMigrationToOpenKM
{
    
    using System;
    using System.IO;
    using System.Collections;
    using System.Threading;
    using System.Xml.Linq;
    using com.openkm.ws;
    using com.openkm.ws.bean;
    using ImageMagick;
    using System.Xml;
    using System.Messaging;
    using DocuwareMigrationDataPointerAssembly;
    using StackExchange.Redis;
    

    public class RecursiveFileProcessor
    {
        ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        ManualResetEvent _pauseEvent = new ManualResetEvent(true);

        static IDatabase keyValueCache = RedisConnectorHelper.Connection.GetDatabase();
        float processedFilesSize = 0.0F;

        public RecursiveFileProcessor(ManualResetEvent shutdownEvent, ManualResetEvent pauseEvent)
        {
            _shutdownEvent = shutdownEvent;
            _pauseEvent = pauseEvent;

        }
 

        // Process all files in the directory passed in, recurse on any directories 
        // that are found, and process the files they contain.
        public void ProcessDirectory(string targetDirectory, OKMWebservice webservice, string token, string host, string user, string password)
        {
            //string ext = String.Empty;
            //string pathWithoutExtention = String.Empty;
            //string fileNameWithoutFolderPath = String.Empty;
            //string localTempPdfFilename = String.Empty;
            //string fileName = String.Empty;
            //Dictionary<string, string> metadata = null;
            
            const string baseDocPath = @"/okm:root/DocuWareToOpenKMMigration/";
            string dynamicDocPath = baseDocPath;
            //string transformedPath = string.Empty;
            //string transformedPathForNonXmlFiles = string.Empty;

            //int year = 0;
            //string month = string.Empty;

            string quepath = @".\Private$\" + Path.GetFileName(Worker._path); // Path.GetFileName may not work with a Directory name having a trailing /
            MessageQueue pQueue = new MessageQueue(quepath);


            //Synchronous Approach
            while (true)
            {
                _pauseEvent.WaitOne(Timeout.Infinite);

                if (_shutdownEvent.WaitOne(0))
                    break;

                //Receiving Message
                Message message = pQueue.Receive();
                message.Formatter = new BinaryMessageFormatter();
                DocuwareMigrationDataPointer docuwareMigrationDataPointerReceiver = (DocuwareMigrationDataPointer)message.Body;

                string[] transformedPathTokens = docuwareMigrationDataPointerReceiver.TransformedPath.Split('/');
                //year = dateOfDocumentReceiptDate.Year;
                //month = dateOfDocumentReceiptDate.ToString("MMMM", culture).ToLower().Trim();


                //Check if folder exists and create if not.
                // NOTE that OpenKM folder creation fails when there is a trailing / in the docPath and when you attempt to craate nested folders at once.
                // The Folder creation code below works

                //Attempt to create the year folder in OpenKM
                //Modify the OpenKM path string to contain the year as a folder
                dynamicDocPath = dynamicDocPath + transformedPathTokens[3].Trim();

                CreateOKMYearFolder(webservice, token, host, user, password, dynamicDocPath);

                //Attempt to create the month folder in OpenKM
                //Modify the OpenKM path string to contain the month as a folder
                dynamicDocPath = dynamicDocPath + "/" + transformedPathTokens[4].Trim();

                CreateOKMMonthFolder(webservice, token, host, user, password, dynamicDocPath);

                ////string extension = Path.GetExtension(fileName);
                //pathWithoutExtention = Path.GetFileNameWithoutExtension(fileName);
                //fileNameWithoutFolderPath = pathWithoutExtention;

                //Attempt to create the file's folder in OpenKM
                //Modify the OpenKM path string to contain the filename as a folder
                dynamicDocPath = dynamicDocPath + "/" + transformedPathTokens[5].Trim();

                CreateOKMFileFolder(webservice, token, host, user, password, dynamicDocPath);

                //transformedPath = dynamicDocPath + "/" + pathWithoutExtention.Substring(pathWithoutExtention.LastIndexOf(@"\") + 1) + ext;

                /////

                ProcessFile(docuwareMigrationDataPointerReceiver.Path, docuwareMigrationDataPointerReceiver.Metadata, webservice, token, docuwareMigrationDataPointerReceiver.TransformedPath, host, user, password);

                dynamicDocPath = baseDocPath;

                RedisValue processedFilesSizeValue = keyValueCache.StringGet("Webservice-Consumer");

                if (!processedFilesSizeValue.HasValue)
                {
                    //Sum up the total files that have been processed, and store the sum in the Redis Key/Value NoSQL database.
                    processedFilesSize += new FileInfo(docuwareMigrationDataPointerReceiver.Path).Length;

                    keyValueCache.StringSet("Webservice-Consumer", processedFilesSize.ToString());
                }
                else
                {
                    processedFilesSize = Convert.ToSingle(processedFilesSizeValue.ToString());

                    //Sum up the total files that have been processed, and store the sum in the Redis Key/Value NoSQL database.
                    processedFilesSize += new FileInfo(docuwareMigrationDataPointerReceiver.Path).Length;

                    keyValueCache.StringSet("Webservice-Consumer", processedFilesSize.ToString());

                }
            }

                            
        }

        static void OnMessageArrival(IAsyncResult ar)
        {
            //Cast the state object to type MessageQueue
            MQObjectStateStore mQObjectStateStore = (MQObjectStateStore)ar.AsyncState;
            //MessageQueue pQueue = (MessageQueue)ar.AsyncState;
            try
            {
                Message message = mQObjectStateStore.PQueue.EndReceive(ar);
                message.Formatter = new BinaryMessageFormatter();
                DocuwareMigrationDataPointer docuwareMigrationDataPointerReceiver = (DocuwareMigrationDataPointer)message.Body;

                string[] transformedPathTokens = docuwareMigrationDataPointerReceiver.TransformedPath.Split('/');
                //year = dateOfDocumentReceiptDate.Year;
                //month = dateOfDocumentReceiptDate.ToString("MMMM", culture).ToLower().Trim();


                //Check if folder exists and create if not.
                // NOTE that OpenKM folder creation fails when there is a trailing / in the docPath and when you attempt to craate nested folders at once.
                // The Folder creation code below works

                //Attempt to create the year folder in OpenKM
                //Modify the OpenKM path string to contain the year as a folder
                mQObjectStateStore.DynamicDocPath = mQObjectStateStore.DynamicDocPath + transformedPathTokens[3].Trim();

                CreateOKMYearFolder(mQObjectStateStore.Webservice, mQObjectStateStore.Token, mQObjectStateStore.Host, mQObjectStateStore.User, mQObjectStateStore.Password, mQObjectStateStore.DynamicDocPath);

                //Attempt to create the month folder in OpenKM
                //Modify the OpenKM path string to contain the month as a folder
                mQObjectStateStore.DynamicDocPath = mQObjectStateStore.DynamicDocPath + "/" + transformedPathTokens[4].Trim();

                CreateOKMMonthFolder(mQObjectStateStore.Webservice, mQObjectStateStore.Token, mQObjectStateStore.Host, mQObjectStateStore.User, mQObjectStateStore.Password, mQObjectStateStore.DynamicDocPath);

                ////string extension = Path.GetExtension(fileName);
                //pathWithoutExtention = Path.GetFileNameWithoutExtension(fileName);
                //fileNameWithoutFolderPath = pathWithoutExtention;

                //Attempt to create the file's folder in OpenKM
                //Modify the OpenKM path string to contain the filename as a folder
                mQObjectStateStore.DynamicDocPath = mQObjectStateStore.DynamicDocPath + "/" + transformedPathTokens[5].Trim();

                CreateOKMFileFolder(mQObjectStateStore.Webservice, mQObjectStateStore.Token, mQObjectStateStore.Host, mQObjectStateStore.User, mQObjectStateStore.Password, mQObjectStateStore.DynamicDocPath);

                //transformedPath = dynamicDocPath + "/" + pathWithoutExtention.Substring(pathWithoutExtention.LastIndexOf(@"\") + 1) + ext;

                /////

                ProcessFile(docuwareMigrationDataPointerReceiver.Path, docuwareMigrationDataPointerReceiver.Metadata, mQObjectStateStore.Webservice, mQObjectStateStore.Token, docuwareMigrationDataPointerReceiver.TransformedPath, mQObjectStateStore.Host, mQObjectStateStore.User, mQObjectStateStore.Password);

            }
            catch
            {
                Console.WriteLine("MessageQueue TimeOut Occurred! Recovering...");
            }
            finally
            {
                mQObjectStateStore.PQueue.BeginReceive(TimeSpan.FromSeconds(5), mQObjectStateStore.PQueue, new AsyncCallback(OnMessageArrival));
            }
            
        }

        
        private static void CreateOKMFileFolder(OKMWebservice webservice, string token, string host, string user, string password, string dynamicDocPath)
        {
            try
            {
                if (!webservice.folderExist(token, dynamicDocPath))
                {
                    webservice.createSimple(token, dynamicDocPath);
                }
            }
            catch (OKMWebserviceException okex)
            {
                // Re-obtaining OpenKM authentication token, and retrying...
                Console.WriteLine(okex.Message);
                Console.WriteLine(okex.Source);
                Console.WriteLine(okex.StackTrace);
                Console.WriteLine(okex.TargetSite);

                //Console.WriteLine("Re-obtaining OpenKM authentication token, and retrying CreateOKMFileFolder...");
                //Worker.LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
                ////recurse
                //CreateOKMFileFolder(webservice, token, host, user, password, dynamicDocPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.TargetSite);
                //Console.WriteLine("Retrying CreateOKMFileFolder...");
                ////recurse
                //CreateOKMFileFolder(webservice, token, host, user, password, dynamicDocPath);

            }
        }

        private static void CreateOKMMonthFolder(OKMWebservice webservice, string token, string host, string user, string password, string dynamicDocPath)
        {
            try
            {

                if (!webservice.folderExist(token, dynamicDocPath))
                {
                    webservice.createSimple(token, dynamicDocPath);
                }
            }
            catch (OKMWebserviceException okex)
            {
                // Re-obtaining OpenKM authentication token, and retrying...
                Console.WriteLine(okex.Message);
                Console.WriteLine(okex.Source);
                Console.WriteLine(okex.StackTrace);
                Console.WriteLine(okex.TargetSite);
                //Console.WriteLine("Re-obtaining OpenKM authentication token, and retrying CreateOKMMonthFolder...");
                //Worker.LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
                ////recurse
                //CreateOKMMonthFolder(webservice, token, host, user, password, dynamicDocPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.TargetSite);
                //Console.WriteLine("Retrying CreateOKMMonthFolder...");
                ////recurse
                //CreateOKMMonthFolder(webservice, token, host, user, password, dynamicDocPath);

            }
        }

        private static void CreateOKMYearFolder(OKMWebservice webservice, string token, string host, string user, string password, string dynamicDocPath)
        {
            try
            {
                if (!webservice.folderExist(token, dynamicDocPath))
                {
                    webservice.createSimple(token, dynamicDocPath);
                }
            }
            catch (OKMWebserviceException okex)
            {
                // Re-obtaining OpenKM authentication token, and retrying...
                Console.WriteLine(okex.Message);
                Console.WriteLine(okex.Source);
                Console.WriteLine(okex.StackTrace);
                Console.WriteLine(okex.TargetSite);
                //Console.WriteLine("Re-obtaining OpenKM authentication token, and retrying CreateOKMYearFolder...");
                //Worker.LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
                ////recurse
                //CreateOKMYearFolder(ref webservice, ref token, host, user,  password, dynamicDocPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.TargetSite);
                //Console.WriteLine("Retrying CreateOKMYearFolder...");
                ////recurse
                //CreateOKMYearFolder(ref webservice, ref token, host, user, password, dynamicDocPath);

            }
        }

        // Insert logic for processing found files here.
        public static void ProcessFile(string path, Dictionary<string, string> metadata, OKMWebservice webservice, string token, string transformedPath, string host, string user, string password)
        {
            try {



                ProcessNonExistingFile(path, metadata, webservice, token, transformedPath);
            }
            catch (OKMWebserviceException okex)
            {
                // Re-obtaining OpenKM authentication token, and retrying...
                Console.WriteLine(okex.Message);
                Console.WriteLine(okex.Source);
                Console.WriteLine(okex.StackTrace);
                Console.WriteLine(okex.TargetSite);
                //Console.WriteLine("Re-obtaining OpenKM authentication token, and retrying ProcessNonExistingFile...");
                //Worker.LoginToOpenKMCommunityEdition(host, user, password, ref token, ref webservice);
                ////recurse
                //ProcessNonExistingFile(path, metadata, webservice, token, transformedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.TargetSite);
                //Console.WriteLine("Retrying ProcessNonExistingFile...");
                ////recurse
                //ProcessNonExistingFile(path, metadata, webservice, token, transformedPath);

            } 
        }

        private static void ProcessNonExistingFile(string path, Dictionary<string, string> metadata, OKMWebservice webservice, string token, string transformedPath)
        {
            if (!webservice.documentExists(token, transformedPath))
            {
                //webservice.createSimple(token, docPath + path.Substring(path.LastIndexOf(@"\") + 1), File.ReadAllBytes(path));
                // Upload the file to OpenKM
                webservice.createSimple(token, transformedPath, File.ReadAllBytes(path));


                //Add Property Group and set the properties in the Group
                webservice.addGroup(token, transformedPath, "okg:systemspecs_docuware_import");

                Dictionary<string, string> okmPropertiesMap = new Dictionary<string, string>();

                if ((!okmPropertiesMap.ContainsKey("okp:systemspecs_docuware_import.companyname")) && (metadata.ContainsKey("COMPANY_NAME")))
                {
                    okmPropertiesMap.Add("okp:systemspecs_docuware_import.companyname", metadata["COMPANY_NAME"]);
                }

                if ((!okmPropertiesMap.ContainsKey("okp:systemspecs_docuware_import.productline")) && (metadata.ContainsKey("PRODUCT_LINE")))
                {
                    okmPropertiesMap.Add("okp:systemspecs_docuware_import.productline", metadata["PRODUCT_LINE"]);
                }

                if ((!okmPropertiesMap.ContainsKey("okp:systemspecs_docuware_import.subjectheading")) && (metadata.ContainsKey("SUBJECT_HEADING")))
                {
                    okmPropertiesMap.Add("okp:systemspecs_docuware_import.subjectheading", metadata["SUBJECT_HEADING"]);
                }

                if ((!okmPropertiesMap.ContainsKey("okp:systemspecs_docuware_import.documentdirection")) && (metadata.ContainsKey("INGOING_OUTGOING")))
                {
                    okmPropertiesMap.Add("okp:systemspecs_docuware_import.documentdirection", metadata["INGOING_OUTGOING"]);
                }

                if ((!okmPropertiesMap.ContainsKey("okp:systemspecs_docuware_import.documenttype")) && (metadata.ContainsKey("DOCUMENT_TYPE")))
                {
                    okmPropertiesMap.Add("okp:systemspecs_docuware_import.documenttype", metadata["DOCUMENT_TYPE"]);
                }

                if ((!okmPropertiesMap.ContainsKey("okp:systemspecs_docuware_import.referencenumber")) && (metadata.ContainsKey("REFERENCE_NUMBER")))
                {
                    okmPropertiesMap.Add("okp:systemspecs_docuware_import.referencenumber", metadata["REFERENCE_NUMBER"]);
                }

                if ((!okmPropertiesMap.ContainsKey("okp:systemspecs_docuware_import.date")) && (metadata.ContainsKey("DATE")))
                {
                    DateTime dateTime = DateTime.Parse(metadata["DATE"], new System.Globalization.CultureInfo("en-US", true), System.Globalization.DateTimeStyles.AssumeLocal);
                    string dateTimeString = dateTime.Year.ToString() + "0" + dateTime.Month.ToString() + "0" + dateTime.Day.ToString() + "000000";
                    okmPropertiesMap.Add("okp:systemspecs_docuware_import.date", dateTimeString);
                }

                webservice.setPropertiesSimple(token, transformedPath, "okg:systemspecs_docuware_import", okmPropertiesMap);

                Console.WriteLine("Uploaded file from '{0}' -> '{1}'.", path, transformedPath);
            }
            else
            {
                Console.WriteLine("File already exits: for transaction '{0}' -> '{1}'.", path, transformedPath);
            }

            //Handling omitted training property group
            //The above is not necessary again as search and eye inspection of the filesystem repository for Docuware revealed the above property group as the only consistent one.
            
        }

        public static float CalculateFolderSize(string folder, ManualResetEvent _shutdownEvent, ManualResetEvent _pauseEvent)
        {
            float folderSize = 0.0f;
            string ext = string.Empty;

            try
            {
                //Checks if the path is valid or not
                if (!Directory.Exists(folder))
                    return folderSize;
                else
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(folder))
                        {
                            _pauseEvent.WaitOne(Timeout.Infinite);

                            if (_shutdownEvent.WaitOne(0))
                                break;

                            ext = Path.GetExtension(file);
                            if ((File.Exists(file)) && (String.Equals(".pdf", ext.ToLower()) || String.Equals(".xml", ext.ToLower()) || String.Equals(".png", ext.ToLower()) || String.Equals(".jpg", ext.ToLower()) || String.Equals(".jpeg", ext.ToLower()) || String.Equals(".tiff", ext.ToLower()) || String.Equals(".tif", ext.ToLower()) || String.Equals(".bmp", ext.ToLower())))
                            {
                                FileInfo finfo = new FileInfo(file);
                                folderSize += finfo.Length;
                            }
                        }

                        foreach (string dir in Directory.GetDirectories(folder))
                        {
                            _pauseEvent.WaitOne(Timeout.Infinite);

                            if (_shutdownEvent.WaitOne(0))
                                break;

                            folderSize += CalculateFolderSize(dir, _shutdownEvent, _pauseEvent);
                        }
                    }
                    catch (NotSupportedException e)
                    {
                        Console.WriteLine("Unable to calculate folder size: {0}", e.Message);
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine("Unable to calculate folder size: {0}", e.Message);
            }
            return folderSize;
        }
    }
}
