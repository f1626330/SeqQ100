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
    public class ImageBackupRequest
    {
        public ImagingStep Step { get; set; }
        public string ImageFileName { get; set; }
        public string DestPath { get; set; }
    }

    public class ImageBackupEventArgs : EventArgs
    {
        public RecipeStepBase Step { get; set; }
        public string Message { get; set; }
        public bool IsError { get; set; }
    }

    public class ImageBackup
    {
        public event EventHandler<ImageBackupEventArgs> OnImageBackupStatus;
        object _RecipeCopydatalock = new object();
        List<(string, string)> FailImageTranfer = new List<(string, string)>();
        ISeqLog Logger { get; }
        string NasFolder { get;  set; }
        AutoSetQueue<ImageBackupRequest> ImageBackupQ { get;  set; }
        Thread ImageBackupQProcessingThread { get; set; }

        public bool IsAbort { get; set; }
        public bool IsSimulationMode { get; set; }
        public bool IsBackupStarted { get; private set; }
        void OnStatusUpdateInvoke(RecipeStepBase step, string msg, bool isError) =>
            OnImageBackupStatus?.Invoke(
                this, 
                new ImageBackupEventArgs()
                {
                    Step = step, 
                    Message = msg, 
                    IsError = isError
                });
        

        public ImageBackup(ISeqLog seqLog)
        {
            Logger = seqLog;
           
        }

        public void StartImageBackupQProcessingThread()
        {
            if (IsBackupStarted)
            {
                LogMessage($"Already started to backup");
                return;
            }
            ImageBackupQ = new AutoSetQueue<ImageBackupRequest>();
            ImageBackupQProcessingThread = new Thread(() => ImageBackupQProcessing());
            ImageBackupQProcessingThread.Name = "ImageBackup";
            ImageBackupQProcessingThread.IsBackground = true;
            ImageBackupQProcessingThread.Start();
            IsBackupStarted = true;
        }

        public void BackupImage(ImagingStep step, string imageFileName, string destFilename)
        {
            //string[] filename = imageFileName.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
            if(imageFileName.Length > 0)
            {
                //string destpath = Path.Combine(NasFolder, filename[filename.Length - 3]);
                //destpath = Path.Combine(destpath, Path.GetFileName(imageFileName));
                string destpath = destFilename;
                ImageBackupQ.Enqueue(
                    new ImageBackupRequest()
                    {
                        Step = step,
                        ImageFileName = imageFileName,
                        DestPath = destpath,
                    });
            }
            
        }
        public void SetLastImage()
        {
            LogMessage($"Add last null image backup request to the image backup queue, current QSize={ImageBackupQ?.QueueCount}");
            ImageBackupQ?.Enqueue(null);
        }

        public void WaitForBackingupImageComplete()
        {
            if (ImageBackupQProcessingThread?.IsAlive == true)
            {
                ImageBackupQProcessingThread.Join();
            }
            ImageBackupQProcessingThread = null;
            if (FailImageTranfer.Count > 0 )
            {
                foreach (var item in FailImageTranfer)
                {
                    LogMessage(string.Format("Images failed to transfer: {0}", item));
                }
            }
            IsBackupStarted = false;
        }

        public void PutBackFailImagesForTransfering()
        {
            if (FailImageTranfer != null && FailImageTranfer.Count > 0)
            {
                ImagingStep step = new ImagingStep();
                OnStatusUpdateInvoke(step, "Retry image transfer", false);
                List<(string, string)> filestocopy = FailImageTranfer;
                foreach ((string, string) filename in filestocopy)
                {
                    BackupImage(step, filename.Item1, filename.Item2);
                }
            }
        }

        private void ImageBackupQProcessing()
        {
            bool runProcess = true;
            int _FailCount = 0; 
            while (runProcess)
            {
                if (IsAbort)
                {
                    OnStatusUpdateInvoke(new ImagingStep(), $"Simulation: backup aborted", false);

                    break;
                }
                ImageBackupRequest imgBck = ImageBackupQ.WaitForNextItem();
                if (imgBck != null)
                {
                    
                    if (IsSimulationMode)
                    {
                        OnStatusUpdateInvoke(imgBck.Step, $"Simulation: backup image file {imgBck.ImageFileName} to {Path.GetDirectoryName(imgBck.DestPath)}", false);
                        LogMessage($"Simulation: backup image file {imgBck.ImageFileName} to ==>  {Path.GetDirectoryName(imgBck.DestPath)}");
                        Thread.Sleep(5000);
                    }
                    else
                    {
                        //OnStatusUpdateInvoke(imgBck.Step, $"Backup image file {imgBck.ImageFileName} to {imgBck.DestPath}", false);
                        if(!CopytoNas(imgBck.Step, imgBck.ImageFileName, imgBck.DestPath))
                        {
                            _FailCount++;
                            if(_FailCount > 100)
                            {
                                OnStatusUpdateInvoke(imgBck.Step, "Backup image failure count exceed limit, thread stopped", true);
                                break;
                            }
                        }
                    }

                }
                else
                {
                    OnStatusUpdateInvoke(new ImagingStep(), $"Simulation: backup completed", false);
                    //processing done
                    LogMessage("Image backup processing is going to exit.");
                    break;
                }
                Thread.Sleep(10);
            }
            LogMessage($"Image backup processing thread exited.");
        }

        bool CopytoNas(RecipeStepBase step, string filename, string destpath)
        {
            bool result = true;
            lock (_RecipeCopydatalock)
            {
                try
                {
                    File.Copy(filename, destpath, true);
                    //FindQCFromImage QC = new FindQCFromImage(filename);
                }
                catch (Exception ex)
                {
                    OnStatusUpdateInvoke(step, string.Format("Failed to copy image file to Nas, retry: {0}, Exception:{1}", Path.GetFileName(filename), ex.ToString()), false);
                    try
                    {
                        Thread.Sleep(5000);
                        File.Copy(filename, destpath, true);
                        //FindQCFromImage QC = new FindQCFromImage(filename);
                    }
                    catch (Exception nex)
                    {
                        FailImageTranfer.Add((filename, destpath));
                        result = false;
                        OnStatusUpdateInvoke(step, string.Format("Failed to copy image file to Nas, added to list : {0}, Exception:{1}", Path.GetFileName(filename), nex.ToString()), false);
                    }
                }
            }
            return result;
        }
        private void LogMessage(string msg, SeqLogMessageTypeEnum msgType = SeqLogMessageTypeEnum.INFO) =>
           Logger.LogMessage(msg, msgType);
    }
}
