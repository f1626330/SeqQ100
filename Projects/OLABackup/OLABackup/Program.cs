using System;
using System.Globalization;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace OLABackup
{
    class Logger
    {
        public Logger(string path)
        {
            FilePath = path;
        }

        public void Log(string msg)
        {
            using (var fs = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                using (StreamWriter w = new StreamWriter(fs))
                {
                    w.WriteLine($"{DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture)} {msg}");
                }
            }

            //using (StreamWriter w = File.AppendText(FilePath))
            //{
            //    w.WriteLine($"{DateTime.Now.ToLongTimeString()} {msg}");
            //}
        }

        string FilePath;
    }
    
    class Program
    {
        static string OLAFolderName => "OLA";
        static string OLAInfoFileName => "OLAInfo.txt";
        static string BcqcFolderName => "bcqc";
        static string FastqFolderName => "fastq";
        static string IndexMergeLogFileName => "IndexMerge.log";
        static string OLABackup => "OLABackup";
        static int SleepWhileWaitingForAllToFinish_ms = 600000;

        static int Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<string>("--srcDir",
                    description: "Specify source directory"),
                new Option<string>("--backupDir",
                    description: "Specify backup directory"),
                new Option<string>("--taskDir",
                    description: "Specify task directory"),
                new Option<string>("--backupStep",
                    description: "Specify the backup step: Read1, Index1, Post_Read1, or Post_Index1"),
            };

            rootCommand.Description = "Console App to back up OLA results.";

            rootCommand.Handler = CommandHandler.Create<string,string,string,string>(Execute);

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        static public void Execute(string srcDir, string backupDir, string taskDir, string backupStep)
        {
            string logName = OLABackup + "_" + backupStep + ".log";
            Logger = new Logger(Path.Combine(srcDir, logName));
            Logger.Log($"Source directory: {srcDir}");
            Logger.Log($"Backup directory: {backupDir}");
            Logger.Log($"Task directory: {taskDir}");
            Logger.Log($"Step: {backupStep}");

            try
            {
                if (backupStep == "Read1" || backupStep == "Index1")
                {
                    string srcReadDir = Path.Combine(srcDir, backupStep);
                    string destReadDir = Path.Combine(backupDir, backupStep);

                    BackupFolder(srcReadDir, destReadDir, OLAFolderName);

                    File.Copy(Path.Combine(srcReadDir, OLAInfoFileName), Path.Combine(destReadDir, OLAInfoFileName));
                }

                if (backupStep == "Post_Read1" || backupStep == "Post_Index1")
                {
                    BackupFolder(srcDir, backupDir, BcqcFolderName);
                    BackupFolder(srcDir, backupDir, FastqFolderName);

                    File.Copy(Path.Combine(srcDir, IndexMergeLogFileName), Path.Combine(backupDir, IndexMergeLogFileName));

                    // Assuming the last subfolder of srcDir matches the experiment name, extract that name
                    string expName = srcDir.Substring(srcDir.LastIndexOf('\\') + 1);
                    // Build OLA log file base path
                    string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    string sysLogPath = Path.Combine(commonAppData, "Sequlite", "Log", "SysLog");
                    string logFilePathBaseName = "OLALog-" + expName;
                    foreach (string olaLogPath in Directory.GetFiles(Path.Combine(sysLogPath), logFilePathBaseName + "*"))
                        File.Copy(olaLogPath, Path.Combine(backupDir, Path.GetFileName(olaLogPath)));

                    // Create task(s) for the downstream analysis of OLA results. The script for the downstream analysis requires that the Read1 task is created first
                    if (WaitForOLABackup(srcDir, "Read1"))
                        CreateOLATask(srcDir, "Read1", taskDir);
                    
                    if (backupStep == "Post_Index1")
                        if (WaitForOLABackup(srcDir, "Index1"))
                            CreateOLATask(srcDir, "Index1", taskDir);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
                Logger.Log("OLABackup.exe ends with error code 1");
            }

            Logger.Log($"Successfully finished {backupStep} backup step");
            Logger.Log("OLABackup.exe ends with error code 0");
        }

        private static bool WaitForOLABackup(string srcDir, string backupStep)
        {
            bool backupSuccess;

            while(!IsBackupFinished(srcDir, backupStep, out backupSuccess))
                Thread.Sleep(SleepWhileWaitingForAllToFinish_ms);

            return backupSuccess;
        }

        // Keep checking the log file until a success or an error message appear
        private static bool IsBackupFinished(string srcDir, string backupStep, out bool backupSuccess)
        {
            backupSuccess = false;

            string logPath = Path.Combine(srcDir, OLABackup + "_" + backupStep + ".log");

            if (!File.Exists(logPath))
                return false;
            
            Logger.Log($"Waiting for an exit message in file: {logPath}");

            using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (line.Contains("OLABackup.exe ends with error code 1", StringComparison.InvariantCultureIgnoreCase))
                        {
                            backupSuccess = false;
                            return true;
                        }
                        else if (line.Contains("OLABackup.exe ends with error code 0", StringComparison.InvariantCultureIgnoreCase))
                        {
                            backupSuccess = true;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        
        // Create a "task" for the downstream analysis of OLA results 
        private static void CreateOLATask(string srcDir, string backupStep, string taskDir)
        {
            // Figure out the experiment name from the srcDir path
            DirectoryInfo srsDI = new DirectoryInfo(srcDir);
            string expName = srsDI.Name;

            string srcReadDir = Path.Combine(srcDir, backupStep);
            string srcFile = Path.Combine(srcReadDir, OLAInfoFileName);
            string targetFile = Path.Combine(taskDir, expName + "_" + backupStep + "_OLA.txt");
            File.Copy(srcFile, targetFile);
            Logger.Log($"Copied {srcFile} to {targetFile}. Created {backupStep} OLA task");
        }

        // Zip the folder locally, copy it to the destination and unzip there. Finally delete the zips on both sides.
        private static void BackupFolder(string srcDir, string destDir, string folderName)
        {
            string folder = Path.Combine(srcDir, folderName);
            string folderZip = Path.Combine(srcDir, folderName + ".zip");

            Logger.Log($"Compressing {folder}...");
            ZipFile.CreateFromDirectory(folder, folderZip);
            Logger.Log($"Compressed {folder}");

            Directory.CreateDirectory(destDir);
            string folderZipBackup = Path.Combine(destDir, folderName + ".zip");

            Logger.Log($"Copying {folderZip} to {folderZipBackup}...");
            File.Copy(folderZip, folderZipBackup);
            Logger.Log($"Copied {folderZip} to {folderZipBackup}");

            Logger.Log($"Extracting {folderZipBackup}...");
            ZipFile.ExtractToDirectory(folderZipBackup, Path.Combine(destDir, folderName));
            Logger.Log($"Extracted {folderZipBackup}");

            Logger.Log($"Deleting {folderZip}...");
            File.Delete(folderZip);
            Logger.Log($"Deleted {folderZip}");

            Logger.Log($"Deleting {folderZipBackup}...");
            File.Delete(folderZipBackup);
            Logger.Log($"Deleted {folderZipBackup}");
        }

        static Logger Logger;
    }
}
