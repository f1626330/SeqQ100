using Sequlite.ALF.Common;
using Sequlite.Image.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.RecipeLib
{
    public class SequenceDataBackupRequest
    {
        public RecipeStepBase Step { get; set; }
        public string FileNameToBebackedup { get; set; }
        public string DestPath { get; set; }
        public bool IsFolder { get; set; }
    }

    public class SequenceDataBackupEventArgs : EventArgs
    {
        public RecipeStepBase Step { get; set; }
        public string Message { get; set; }
        public bool IsError { get; set; }
    }

    public class SequenceDataBackup
    {
        public event EventHandler<SequenceDataBackupEventArgs> OnSequenceDataBackupStatus;
        //object _RecipeCopydatalock = new object();
        List<(string, string)> FailDataTranfer = new List<(string, string)>();
        ISeqLog Logger { get; }
        string NasFolder { get;  set; }
        AutoSetQueue<SequenceDataBackupRequest> SequenceDataBackupQ { get;  set; }
        Thread SequenceBackupQProcessingThread { get; set; }

        public bool IsAbort { get; set; }
        //public bool IsSimulationMode { get=>false; set { } } //for real sim test
        public bool IsSimulationMode { get; set; }
        public bool IsBackupStarted { get; private set; }
        void OnStatusUpdateInvoke(RecipeStepBase step, string msg, bool isError) =>
            OnSequenceDataBackupStatus?.Invoke(
                this, 
                new SequenceDataBackupEventArgs()
                {
                    Step = step, 
                    Message = msg, 
                    IsError = isError
                });
        

        public SequenceDataBackup(ISeqLog seqLog)
        {
            Logger = seqLog;
           
        }

        public void StartBackupQProcessingThread()
        {
            if (IsBackupStarted)
            {
                LogMessage($"Already started to backup");
                return;
            }
            SequenceDataBackupQ = new AutoSetQueue<SequenceDataBackupRequest>();
            SequenceBackupQProcessingThread = new Thread(() => SequenceDataBackupQProcessing());
            SequenceBackupQProcessingThread.Name = "SequenceDataBackup";
            SequenceBackupQProcessingThread.IsBackground = true;
            SequenceBackupQProcessingThread.Start();
            IsBackupStarted = true;
            //OnStatusUpdateInvoke(new ImagingStep(), "Data backup starts", false);

        }

        public void AddABackupRequest(RecipeStepBase step, string fileorFolderNameToBeBackuped, string destFilename, bool isFolder = false)
        {
            if(fileorFolderNameToBeBackuped.Length > 0)
            {
                string destpath = destFilename;
                SequenceDataBackupQ.Enqueue(
                    new SequenceDataBackupRequest()
                    {
                        Step = step,
                        FileNameToBebackedup = fileorFolderNameToBeBackuped,
                        DestPath = destpath,
                        IsFolder = isFolder
                    });
            }
            
        }
        public void SetLastData()
        {
            LogMessage($"Add last null data backup request to the  backup queue, current QSize={SequenceDataBackupQ?.QueueCount}");
            PutBackFailDataForTransfering();
            SequenceDataBackupQ?.Enqueue(null);
        }

        public void WaitForBackingupComplete()
        {
            if (SequenceBackupQProcessingThread?.IsAlive == true)
            {
                SequenceBackupQProcessingThread.Join();
            }
            SequenceBackupQProcessingThread = null;
            if (FailDataTranfer.Count > 0 )
            {
                foreach (var item in FailDataTranfer)
                {
                    LogMessage(string.Format("Data failed to transfer: {0}", item));
                }
            }
            IsBackupStarted = false;
        }

        private void PutBackFailDataForTransfering()
        {
            if (FailDataTranfer != null && FailDataTranfer.Count > 0)
            {
                ImagingStep step = new ImagingStep();
                OnStatusUpdateInvoke(step, "Retry data transfer", false);
                List<(string, string)> filestocopy = FailDataTranfer;
                foreach ((string, string) filename in filestocopy)
                {
                    AddABackupRequest(step, filename.Item1, filename.Item2);
                }
            }
        }

        private void SequenceDataBackupQProcessing()
        {
            bool runProcess = true;
            int _FailCount = 0; 
            while (runProcess)
            {
                if (IsAbort)
                {
                    OnStatusUpdateInvoke(new ImagingStep(), $"Data backup aborted", false);

                    break;
                }
                SequenceDataBackupRequest dataBck = SequenceDataBackupQ.WaitForNextItem();
                if (dataBck != null)
                {
                    if (!CopytoNas(dataBck.Step, dataBck.FileNameToBebackedup, dataBck.DestPath, dataBck.IsFolder))
                    {
                        _FailCount++;
                        if (_FailCount > 100)
                        {
                            OnStatusUpdateInvoke(dataBck.Step, "Backup data failure count exceed limit, thread stopped", true);
                            break;
                        }
                    }
                }
                else
                {
                    OnStatusUpdateInvoke(new ImagingStep(), $"Backup completed", false);
                    //processing done
                    LogMessage("data backup processing is going to exit.");
                    break;
                }
                Thread.Sleep(10);
            }
            LogMessage($"data backup processing thread exited.");
        }

        bool CopytoNas(RecipeStepBase step, string name, string destpath, bool isFolder)
        {
            bool result = true;
            //lock (_RecipeCopydatalock)
            {
                if (isFolder)
                {
                    result = CopyAFolder(step, name, destpath, true);
                }
                else
                {
                    result = CopyAFile(step, name, destpath);
                    
                } 
            }
            return result;
        }
        private void LogMessage(string msg, SeqLogMessageTypeEnum msgType = SeqLogMessageTypeEnum.INFO) =>
           Logger.LogMessage(msg, msgType);

        //destpathFileName --- full file name
        private bool CopyAFile(RecipeStepBase step, string name, string destpathFileName)
        {
            bool b = false;
            int retry = 0;
            while (retry < 2)
            {
                try
                {
                    OnStatusUpdateInvoke(step, $"backup data file {name} to {destpathFileName}", false);
                    LogMessage($"backup data file {name} to ==>  {destpathFileName}");
                    if (IsSimulationMode)
                    {
                       
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        File.Copy(name, destpathFileName, true);
                    }
                    b = true;
                    break;
                }
                catch (Exception ex)
                {
                    OnStatusUpdateInvoke(step, $"Failed to copy data {Path.GetFileName(name)} to Nas with error {ex.Message}, retry: {retry}", false);
                    retry++;
                    Thread.Sleep(3000);
                }
            } //while
            return b;
        }

        //name -- dir name
        private bool CopyAFolder(RecipeStepBase step, string name, string destDirName, bool copySubDirs)
        {
            bool b = true;
            try
            {
                // Get the subdirectories for the specified directory.
                DirectoryInfo dir = new DirectoryInfo(name);
                if (!dir.Exists)
                {
                    OnStatusUpdateInvoke(step, $"Source directory does not exist or could not be found: {name} ", false);
                    b = false;
                }

                List<DirectoryInfo> dirs = dir.GetDirectories().ToList<DirectoryInfo>();

                if (!IsSimulationMode)
                {
                    // If the destination directory doesn't exist, create it.       
                    Directory.CreateDirectory(destDirName);
                }

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files)
                {
                    if (IsAbort)
                    {
                        OnStatusUpdateInvoke(step, $"Abort copying file {file.FullName} ", false);
                        break;
                    }
                    
                    b = CopyAFile(step, file.FullName, Path.Combine(destDirName, file.Name));
                    if (!b)
                    { 
                        break; 
                    }
                }

                // If copying subdirectories, copy them and their contents to new location.
                if (copySubDirs)
                {
                    foreach (DirectoryInfo subdir in dirs)
                    {
                        if (IsAbort)
                        {
                            OnStatusUpdateInvoke(step, $"Abort copying folder {subdir.FullName} ", false);
                            break;
                        }
                        
                        b = CopyAFolder(step, subdir.FullName, Path.Combine(destDirName, subdir.Name), copySubDirs);
                        if (!b)
                        {
                            
                            break;
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                OnStatusUpdateInvoke(step, $"Failed to copy folder {name} with error {ex.Message}", false);
                b = false;
            }
            return b;
        }

    }
}
