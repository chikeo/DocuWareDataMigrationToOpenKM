using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnqueueDocuwareDataForMigrationToOpenKM
{

    using System;
    using System.IO;
    using System.Collections;
    using System.Threading;
    using System.Xml.Linq;
    using System.Xml;
    using ImageMagick;
    using System.Messaging;
    using EnqueueDocuwareDataForMigrationToOpenKM;
    using StackExchange.Redis;
    using DocuwareMigrationDataPointerAssembly;


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
        public void ProcessDirectory(string targetDirectory)
        {
            string ext = String.Empty;
            string pathWithoutExtention = String.Empty;
            string fileNameWithoutFolderPath = String.Empty;
            string localTempPdfFilename = String.Empty;
            Dictionary<string, string> metadata = null;
            MagickImageCollection collectionOfSinglePagesForPdfTransformation = null;
            XDocument xdoc = null;

            string baseDocPath = @"/okm:root/DocuWareToOpenKMMigration/";
            string dynamicDocPath = baseDocPath;
            string transformedPath = string.Empty;
            string transformedPathForNonXmlFiles = string.Empty;

            int year = 0;
            string month = string.Empty;

            Console.WriteLine("In RecursiveFileProcessor.ProcessDirectory. About to calculate the size of the target folder.");
            //First calculate the amount of work that is needed i.e the total size of all files in the folder and all sub folders. There will be need to call this method rather than use this value as pdf files are being generated on the fly.
            float totalFolderSize = CalculateFolderSize(targetDirectory, _shutdownEvent, _pauseEvent);
            Console.WriteLine(totalFolderSize);

            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.WriteLine("The size is {0} MB of target folder {0} is {1}.", targetDirectory, totalFolderSize/(1024*1024));

            Console.ResetColor();
            // Retrieve the list of files found in the directory.
            string[] fileEntries = Directory.GetFiles(targetDirectory);

            // Find the metadata information first
            foreach (string fileName in fileEntries)
            {
                _pauseEvent.WaitOne(Timeout.Infinite);

                if (_shutdownEvent.WaitOne(0))
                    break;

                ext = Path.GetExtension(fileName);
                if (String.Equals(".xml", ext.ToLower()))
                {
                    try
                    {
                        //XDocument xdoc = XDocument.Load(fileName);
                        xdoc = XDocument.Parse(RemoveTroublesomeCharacters(System.IO.File.ReadAllText(fileName)).Replace("&#x1E;", String.Empty).Replace("&#x15;", String.Empty));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(fileName + " " + ex.Message);
                        Console.WriteLine(ex.Source);
                        Console.WriteLine(ex.StackTrace);
                        Console.WriteLine(ex.TargetSite);

                        continue;
                    }

                    metadata = new Dictionary<string, string>();

                    IEnumerable<XElement> elmListOfDate = xdoc.Descendants("Date");

                    foreach (XElement elm in elmListOfDate)
                    {
                        // Logic for retrieving Date metadata fields  
                        if (!metadata.ContainsKey(elm.Attribute("field").Value))
                        { 
                            metadata.Add(elm.Attribute("field").Value, elm.Value);

                            Console.WriteLine(elm.Attribute("field").Value);
                            Console.WriteLine(elm.Value);
                        }
                    }

                    IEnumerable<XElement> elmListOfTextVar = xdoc.Descendants("TextVar");

                    foreach (XElement elm in elmListOfTextVar)
                    {
                        // Logic for retrieving other metadata fields. All other fields are string
                        if (!metadata.ContainsKey(elm.Attribute("field").Value))
                        {
                            metadata.Add(elm.Attribute("field").Value, elm.Value);

                            Console.WriteLine(elm.Attribute("field").Value);
                            Console.WriteLine(elm.Value);
                        }
                    }


                    
                }


                if (String.Equals(".dwtiff", ext.ToLower()))
                {
                    //Rename to .tiff
                    File.Move(fileName, Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + ".tiff");
                }
            }



            // Retrieve the list of files found in the directory again due to the rename .dwtiff to .tiff operation above.
            fileEntries = Directory.GetFiles(targetDirectory);


            //Initialize Magick.NET for merging the images per document folder into a pdf document.
            collectionOfSinglePagesForPdfTransformation = new MagickImageCollection();

            //Upload the files with the metadata to OpenKM
            foreach (string fileName in fileEntries)
            {
                _pauseEvent.WaitOne(Timeout.Infinite);

                if (_shutdownEvent.WaitOne(0))
                    break;

                ext = Path.GetExtension(fileName);
                                                                

                //As we have retrieved the metadata from the xml files above, no need to process xml files again. Process only the document scanned images.
                // Addendum: We need to process the xml files too, so that we can have the metadata as fulltext searchable xml files.
                if (String.Equals(".xml", ext.ToLower()))
                {

                    /////

                    // See whether Dictionary contains the string containing the date metadata field specifying when the document was received.
                    if (metadata.ContainsKey("DATE"))
                    {
                        string dateOfDocumentReceiptString = metadata["DATE"];

                        // Specify exactly how to interpret the string.
                        IFormatProvider culture = new System.Globalization.CultureInfo("en-US", true);

                        // Alternate choice: If the string has been input by an end user, you might  
                        // want to format it according to the current culture: 
                        // IFormatProvider culture = System.Threading.Thread.CurrentThread.CurrentCulture;
                        DateTime dateOfDocumentReceiptDate = DateTime.Parse(dateOfDocumentReceiptString, culture, System.Globalization.DateTimeStyles.AssumeLocal);
                        year = dateOfDocumentReceiptDate.Year;
                        month = dateOfDocumentReceiptDate.ToString("MMMM", culture).ToLower().Trim();

                    }

                    //Check if folder exists and create if not.
                    // NOTE that OpenKM folder creation fails when there is a trailing / in the docPath and when you attempt to craate nested folders at once.
                    // The Folder creation code below works

                    //Attempt to create the year folder in OpenKM
                    //Modify the OpenKM path string to contain the year as a folder
                    dynamicDocPath = dynamicDocPath + year.ToString();

                    

                    //Attempt to create the month folder in OpenKM
                    //Modify the OpenKM path string to contain the month as a folder
                    dynamicDocPath = dynamicDocPath + "/" + month;

                    

                    //string extension = Path.GetExtension(fileName);
                    pathWithoutExtention = Path.GetFileNameWithoutExtension(fileName);
                    fileNameWithoutFolderPath = pathWithoutExtention;

                    //Attempt to create the file's folder in OpenKM
                    //Modify the OpenKM path string to contain the filename as a folder
                    dynamicDocPath = dynamicDocPath + "/" + pathWithoutExtention;

                    

                    transformedPath = dynamicDocPath + "/" + pathWithoutExtention.Substring(pathWithoutExtention.LastIndexOf(@"\") + 1) + ext;

                    /////

                    ProcessFile(fileName, metadata, transformedPath);

                }
                else if ((!String.Equals(".xml", ext.ToLower())) && (String.Equals(".png", ext.ToLower()) || String.Equals(".jpg", ext.ToLower()) || String.Equals(".jpeg", ext.ToLower()) || String.Equals(".tiff", ext.ToLower()) || String.Equals(".tif", ext.ToLower()) || String.Equals(".bmp", ext.ToLower())))
                {
                    //string extension = Path.GetExtension(fileName);
                    pathWithoutExtention = Path.GetFileNameWithoutExtension(fileName);
                    transformedPathForNonXmlFiles = dynamicDocPath + "/" + pathWithoutExtention.Substring(pathWithoutExtention.LastIndexOf(@"\") + 1) + ext;

                    ProcessFile(fileName, metadata, transformedPathForNonXmlFiles);

                    try
                    {
                        //Aggregate all the pages of the file needed for binding together to create single document view
                        collectionOfSinglePagesForPdfTransformation.Add(new MagickImage(fileName));
                    }
                    catch (Exception ex)
                    {
                        //collectionOfSinglePagesForPdfTransformation = null;

                        ////Initialize Magick.NET for merging the images per document folder into a pdf document.
                        //collectionOfSinglePagesForPdfTransformation = new MagickImageCollection();
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.Source);
                        Console.WriteLine(ex.StackTrace);
                        Console.WriteLine(ex.TargetSite);

                        continue;
                    }
                }

                localTempPdfFilename = Path.GetDirectoryName(fileName) + @"\" + fileNameWithoutFolderPath + ".pdf";
                

                ////Print the total work done to the Console.
                //Console.ForegroundColor = ConsoleColor.Magenta;
                //string totalFilesProcessed = 
                //Console.WriteLine()
                //Rest for a little while
                //Thread.Sleep(100);
            }



            if ((collectionOfSinglePagesForPdfTransformation.Count > 0) && (!String.IsNullOrEmpty(localTempPdfFilename)))
            {
                //Prepare the OKM Path for the generated pdf file
                transformedPathForNonXmlFiles = dynamicDocPath + "/" + fileNameWithoutFolderPath + ".pdf";

                if (!File.Exists(localTempPdfFilename))
                {
                    // Create pdf file from the collection of image pages
                    collectionOfSinglePagesForPdfTransformation.Write(localTempPdfFilename);
                }

                //Upload bound file to OpenKM.
                ProcessFile(localTempPdfFilename, metadata, transformedPathForNonXmlFiles);

                //Dispose object
                collectionOfSinglePagesForPdfTransformation.Dispose();
                localTempPdfFilename = String.Empty;
            }

            // Reset dynamicDocPath
            //dynamicDocPath = baseDocPath;


            // Recurse into subdirectories of this directory.
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
            foreach (string subdirectory in subdirectoryEntries)
            {
                _pauseEvent.WaitOne(Timeout.Infinite);

                if (_shutdownEvent.WaitOne(0))
                    break;

                ProcessDirectory(subdirectory);

                //Rest for a little while
                //Thread.Sleep(100);
            }
        }


        /// <summary>
        /// Removes control characters and other non-UTF-8 characters
        /// </summary>
        /// <param name="inString">The string to process</param>
        /// <returns>A string with no control characters or entities above 0x00FD</returns>
        public static string RemoveTroublesomeCharacters(string inString)
        {
            if (inString == null) return null;

            StringBuilder newString = new StringBuilder();
            char ch;

            for (int i = 0; i < inString.Length; i++)
            {

                ch = inString[i];
                // remove any characters outside the valid UTF-8 range as well as all control characters
                // except tabs and new lines
                //if ((ch < 0x00FD && ch > 0x001F) || ch == '\t' || ch == '\n' || ch == '\r')
                //if using .NET version prior to 4, use above logic
                if (XmlConvert.IsXmlChar(ch)) //this method is new in .NET 4
                {
                    newString.Append(ch);
                }
            }
            return newString.ToString();

        }


        // Insert logic for processing found files here.
        public void ProcessFile(string path, Dictionary<string, string> metadata, string transformedPath)
        {
            try
            {
                RedisValue value = keyValueCache.StringGet(path);
                

                if (!value.HasValue)
                {
                    ProcessNonExistingFile(path, metadata, transformedPath);

                    RedisValue processedFilesSizeValue = keyValueCache.StringGet("Enqueue-Producer");

                    if (!processedFilesSizeValue.HasValue)
                    {
                        //Sum up the total files that have been processed, and store the sum in the Redis Key/Value NoSQL database.
                        processedFilesSize += new FileInfo(path).Length;

                        keyValueCache.StringSet("Enqueue-Producer", processedFilesSize.ToString());
                    }
                    else
                    {
                        processedFilesSize = Convert.ToSingle(processedFilesSizeValue.ToString());

                        //Sum up the total files that have been processed, and store the sum in the Redis Key/Value NoSQL database.
                        processedFilesSize += new FileInfo(path).Length;

                        keyValueCache.StringSet("Enqueue-Producer", processedFilesSize.ToString());

                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.Source);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.TargetSite);

            } 
        }

        private static void ProcessNonExistingFile(string path, Dictionary<string, string> metadata, string transformedPath)
        {
            
            //Queue the processing information required for each file that will be later uploaded to OpenKM

            DocuwareMigrationDataPointer docuwareMigrationDataPointer = new DocuwareMigrationDataPointer();
            docuwareMigrationDataPointer.Path = path;
            docuwareMigrationDataPointer.Metadata = metadata;
            docuwareMigrationDataPointer.TransformedPath = transformedPath;

            string quepath = @".\Private$\" + Path.GetFileName(Worker._path); // Path.GetFileName may not work with a Directory name having a trailing /

            //Send the message to the Microsoft MSMQ
            SendMessageToQueue(quepath, docuwareMigrationDataPointer);

            //Mark this message as already queued for processing. DO NOT PROCESS THIS MESSAGE AGAIN.
            keyValueCache.StringSet(path, transformedPath);

            Console.WriteLine("Queueing a message with data for uploading a file from '{0}' -> '{1}'.", path, transformedPath);

            
        }

        private static void SendMessageToQueue(string quepath, DocuwareMigrationDataPointer dataPointer)
        {
            //Initisalize the Message Queue
            DefaultPropertiesToSend dpts = new DefaultPropertiesToSend();
            dpts.Label = "Docuware 5.1b, Data Queued for Import into OpenKM Community 6.3.1";
            dpts.Recoverable = true;
            dpts.UseJournalQueue = true;
            dpts.AttachSenderId = true;
            MessageQueue msgq = null;
            if (!MessageQueue.Exists(quepath))
            {
                msgq = MessageQueue.Create(quepath);
                msgq.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);

            }
            MessageQueue pQueue = new MessageQueue(quepath);
            //pQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(DocuwareMigrationDataPointer) });
            BinaryMessageFormatter formatter = new BinaryMessageFormatter();
            System.Messaging.Message message = new System.Messaging.Message(dataPointer, formatter);
            message.Recoverable = true;
            pQueue.DefaultPropertiesToSend = dpts;

            //Send the message
            pQueue.Send(message);

            //REMOVE LATER
            //Receiving Message
            //Message Mymessage = pQueue.Receive();
            //Mymessage.Formatter = new BinaryMessageFormatter();
            //DocuwareMigrationDataPointer docuwareMigrationDataPointerReceiver = (DocuwareMigrationDataPointer) Mymessage.Body;
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
