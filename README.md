DocuWareDataMigrationToOpenKM
=============================


﻿This is a Project that migrates data from Docuware 5.1b to OpenKM Community 6.3.1.

Steps
=====

 1. Setup MSMQ and Telnet Client in Control Panel in Windows 8.1 and above. If .NET Framework 4.0 is required, download and install it from the Microsoft website.
 2. Install 64-bit Redis Server from the MSOpenTech website e.g. Redis-x64-3.2.100.msi. If your machine's architecture is 32 bit, then download that version of the Redis Server.
 3. Install Redis desktop manager (for viewing the Redis database using a GUI) e.g. redis-desktop-manager-0.8.8.384.exe
 4. Install MSMQViewer (for deleting poison messages that block the Queue) e.g. MSMQViewer.Installer.v.1.3.msi.
 5. Modify the application to have multiple threads by re-factoring the Worker class to receive the AppSettings values (e.g. path) in its constructor. Let each thread have a dedicated AppSettings value.
   Create as many threads as needed in the Program.cs main method. These actions should happen for both the Enqueuer (or EnqueueDocuwareDataForMigrationToOpenKM) application and the consumer (or DocuWareDataMigrationToOpenKM) application.
   Alternatively, if do not want the solution to be multi-threaded, but rather prefer to have different instances running in their own appdomains, create at least 4 instances each of the Enqueuer and Consumer applications, setting the config files accordingly.
   
 6. Divide the C:\Docustore folder into four different subfolders by creating the folders C:\Docustore\Docustore1, C:\Docustore\Docustore2, C:\Docustore\Docustore3, and C:\Docustore\Docustore4
 7. Modify each of the config files for each of the C# Console application instances, taking care to provide the appropriate values.
 8. Start up the application and monitor for poison messages (such as personal images that do not match the pattern of documents the application is searching for).
 9. If there are Application Exceptions as a result of poison Queued messages, delete the identify the full range of poison messages and delete them. Restart the target application where applicable.