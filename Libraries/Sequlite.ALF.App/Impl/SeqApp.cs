using Sequlite.ALF.Common;
using Sequlite.ALF.RecipeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Sequlite.ALF.App
{
    partial class SeqApp : ISeqApp, ISystemInit,  ISystemMonitor
    {
        public string DiskSpaceCheck => "CheckSpace";
        public string AppLogName => "APP";

        public ISeqFileLog Logger { get; }
        public Dispatcher TheDispatcher { get; }
        //outgoing message
        Lazy<Subject<AppMessage>> _ObservableAppMessage = new Lazy<Subject<AppMessage>>(() => new Subject<AppMessage>());
        public IObservable<AppMessage> ObservableAppMessage => _ObservableAppMessage.Value;

        //incoming
        Lazy<Subject<AppMessage>> _IncomingObservableAppMessage = new Lazy<Subject<AppMessage>>(() => new Subject<AppMessage>());
        public IObservable<AppMessage> IncomingObservableAppMessage => _IncomingObservableAppMessage.Value;


        public ISystemInit GetSystemInitInterface() => this;
        public ISystemMonitor GetSystemMonitorInterface() => this;
        public ILoad CreateLoadInterface()
        {
            CheckInitialized();
            return new SeqAppLoad(this);
        }

        public ISystemCheck CreateSystemCheckInterface()
        {
            CheckInitialized();
            return new SeqAppSystemCheck(this);
        }

        public IRunSetup CreateRunSetupInterface()
        {
            CheckInitialized();
            return new SeqAppRunSetup(this);
        }

        public ISequence CreateSequenceInterface(bool checkAppInitialized = true)
        {
            if (checkAppInitialized)
            {
                CheckInitialized();
            }
            return new SeqAppSequence(this);
        }

        public IPostRun CreatePostRunInterface()
        {
            CheckInitialized();
            return new SeqAppPostRun(this);
        }

        public bool IsSimulation { get; private set; }

        public IIDHistory IDHistory { get; set; }

        public static bool HasObservers<T>(IObservable<T> observableMessage) =>
             (observableMessage as Subject<T>)?.HasObservers == true;

        public static bool HasObservers<T>(Subject<T> observableMessage) =>
            observableMessage?.HasObservers == true;

        public static bool HasObservers<T>(Lazy<Subject<T>> lazyObj) =>
            lazyObj.IsValueCreated && lazyObj.Value.HasObservers == true;
        public static void Send<T>(IObservable<T> observableMessage, T msg) =>
            (observableMessage as Subject<T>)?.OnNext(msg);


        public static void SendError<T>(IObservable<T> observableMessage, Exception ex) =>
            (observableMessage as Subject<T>)?.OnError(ex);


        public static void SendCompletion<T>(IObservable<T> observableMessage) =>
            (observableMessage as Subject<T>)?.OnCompleted();

        public SeqApp()
        {
            TheDispatcher = Application.Current?.Dispatcher;
            Logger = SeqLogFactory.GetSeqFileLog(AppLogName);
            SimulationSettings simConfig = SettingsManager.ConfigSettings.SystemConfig.SimulationConfig;
            if (simConfig?.IsSimulation == true)
            {
                IsSimulation = true;
            }
        }

        public void UpdateAppMessage(object msg, AppMessageTypeEnum msgType = AppMessageTypeEnum.Normal, bool bLog = true)
        {
            try
            {
                if (bLog)
                {
                    switch (msgType)
                    {
                        //case AppMessageTypeEnum.Status:
                        //    if (msg is AppStatus)
                        //    {
                        //        string str = (msg as AppStatus).LogMessage;
                        //        if (!string.IsNullOrEmpty(str))
                        //        {
                        //            Logger.Log(str);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        Logger.Log(msg.ToString());
                        //    }
                        //    break;
                        case AppMessageTypeEnum.Normal:
                        case AppMessageTypeEnum.Completed:
                            Logger.Log(msg.ToString());
                            break;
                        case AppMessageTypeEnum.Warning:
                            Logger.LogWarning(msg.ToString());
                            break;
                        case AppMessageTypeEnum.Error:
                        case AppMessageTypeEnum.ErrorNotification:
                            Logger.LogError(msg.ToString());
                            break;
                    }
                }
                if (HasObservers(_ObservableAppMessage))
                {
                    Send(ObservableAppMessage, new AppMessage() { MessageType = msgType, MessageObject = msg });
                }
            }
            catch (Exception ex)
            {
                if (HasObservers(_ObservableAppMessage))
                {
                    SendError(ObservableAppMessage, ex);
                }
            }
        }

        public void SendMessageToApp(object msg, AppMessageTypeEnum msgType = AppMessageTypeEnum.Normal)
        {
            try
            {
                if (HasObservers(_IncomingObservableAppMessage))
                {
                    Send(IncomingObservableAppMessage, new AppMessage() { MessageType = msgType, MessageObject = msg });
                }
            }
            catch (Exception ex)
            {
                if (HasObservers(_IncomingObservableAppMessage))
                {
                    SendError(IncomingObservableAppMessage, ex);
                }
            }
        }

            public void UpdateAppErrorMessage(string msg, bool bLog = true) => UpdateAppMessage(msg, AppMessageTypeEnum.Error);

        public void UpdateAppWarningMessage(string msg, bool bLog = true) => UpdateAppMessage(msg, AppMessageTypeEnum.Warning);

        //shall not have message box here, let subscriber to decide if popping up message box.
        //MessageBox.Show(msg);
        public void NotifyError(string msg) => UpdateAppMessage(msg, AppMessageTypeEnum.ErrorNotification);
        public void NotifyNormalError(string msg) => UpdateAppMessage(msg, AppMessageTypeEnum.Error);

        protected void CheckInitialized()
        {
            if (!IsSimulation && !Initialized)
            {
                throw new Exception("Application has not been initialized");
            }
        }

        public string SaveRecipeToNewPath(string recipeFullPathFile, string newRecipeDir, string newRecipeFileNameNoPath, string hintPath)
        {
            Recipe recipe = Recipe.LoadFromXmlFile(recipeFullPathFile);
            recipe.UpdatedTime = DateTime.Now;
            ReplaceAndCopyRunRecipePath(recipe, newRecipeDir, hintPath);
            //RunRecipeStep _PostWash = new RunRecipeStep("PostWash");
            //_PostWash.RecipePath = _RecipeBuildSettings.PostWashRecipePath;
            if (string.IsNullOrEmpty(newRecipeFileNameNoPath))
            {
                newRecipeFileNameNoPath = Path.GetFileName(recipeFullPathFile);
            }
            string recipeFile = Path.Combine(newRecipeDir, newRecipeFileNameNoPath);
            Recipe.SaveToXmlFile(recipe, recipeFile);
            return recipeFile;
        }

        public void ReplaceAndCopyRunRecipePath(Recipe recipe, string newRecipeDir, string hintPath)
        {
            foreach (var item in recipe.Steps)
            {
                if (item.Step.StepType == RecipeStepTypes.RunRecipe)
                {
                    RunRecipeStep runStep = item.Step as RunRecipeStep;
                    string fileFullName = runStep.RecipePath;
                    if (!File.Exists(fileFullName))
                    {
                        
                        string newfileFullName = Path.Combine(hintPath, Path.GetFileName(fileFullName));
                        Logger.LogWarning($"Recipe file {fileFullName} doesn't exist, try find it in the new file location {hintPath}");
                        fileFullName = newfileFullName;
                        if (!File.Exists(fileFullName))
                        {
                            throw new Exception($"Missing Recipe file: {fileFullName}"); 
                        }
                    }
                    Recipe rp = Recipe.LoadFromXmlFile(fileFullName);
                    ReplaceAndCopyRunRecipePath(rp, newRecipeDir, hintPath);
                    string rpPathFileName = Path.Combine(newRecipeDir, Path.GetFileName(runStep.RecipePath));
                    rp.UpdatedTime = DateTime.Now;
                    Recipe.SaveToXmlFile(rp, rpPathFileName);
                    runStep.RecipePath = rpPathFileName;
                }
            }
        }

        public string CreateTempRecipeLocation(string tempSubDir)
        {
            //string str = DateTime.Now.ToString("yyMMdd-HHmmss");

            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string recipeDir = Path.Combine(commonAppData, $"Sequlite\\Recipe\\{tempSubDir}");
            if (!Directory.Exists(recipeDir))
            {
                Directory.CreateDirectory(recipeDir);
            }
            return recipeDir;
        }

       


    }
}
