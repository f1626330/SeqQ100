using Sequlite.ALF.Common;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using Sequlite.ALF.App;
using System.Diagnostics;
using System.Windows.Media.Animation;

namespace Sequlite.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        ISeqApp SeqApp { get; set; }

        private ISeqFileLog Logger { get; set; }
        readonly string subSystemName = "UI";
        protected async override void OnStartup(StartupEventArgs e)
        {

            Logger = SeqLogFactory.GetSeqFileLog(subSystemName);
            Assembly assembly = Assembly.GetExecutingAssembly();
            Logger.Log("Start " + assembly.GetName().Name + " Application.");
            // *******************************************************************
            // TODO - Uncomment one of the lines of code that create a CultureInfo
            // in order to see the application run with localized text in the UI.
            // *******************************************************************

            CultureInfo culture = null;

            // Example: GERMAN
            //culture = new CultureInfo("de-DE");

            if (culture != null)
            {
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }

            // Ensure the current culture passed into bindings is the OS culture.
            // By default, WPF uses en-US as the culture, regardless of the system settings.
            FrameworkElement.LanguageProperty.OverrideMetadata(
              typeof(FrameworkElement),
              new FrameworkPropertyMetadata(
                  XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

            Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline),new FrameworkPropertyMetadata{ DefaultValue = 30 });

            
            LaunchWindowViewModel launchWinVM = null;
            try
            {
                string companyName = string.Empty;
                string productName = string.Empty;
                string productVersion = string.Empty;
                object[] customAttributes = null;
                customAttributes = assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if ((customAttributes != null) && (customAttributes.Length > 0))
                {
                    companyName = ((AssemblyCompanyAttribute)customAttributes[0]).Company;
                }

                customAttributes = assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if ((customAttributes != null) && (customAttributes.Length > 0))
                {
                    productName = ((AssemblyProductAttribute)customAttributes[0]).Product;
                }
                string commonAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                          companyName + "\\" + productName);
                Directory.CreateDirectory(commonAppDataPath);

                string calibDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                          companyName + "\\Calibration");
                Directory.CreateDirectory(calibDataPath);

                await SettingsManager.OnStartup(Logger, commonAppDataPath, calibDataPath);

                ulong processorAffinity = SettingsManager.ConfigSettings.SystemConfig.GetProcessorAffinityValue();
                if (processorAffinity != 0)
                {
                    Process Proc = Process.GetCurrentProcess();
                    ulong AffinityMask = (ulong)Proc.ProcessorAffinity;
                    AffinityMask &= processorAffinity;
                    Proc.ProcessorAffinity = (IntPtr)AffinityMask;
                }

                LogWindowViewModel logWindowVM = new LogWindowViewModel(false) { LogViewerVM = new LogViewerViewModel(Logger), DispalyDebugMessage = false };
                logWindowVM.Title = "Log Viewer";
                launchWinVM = new LaunchWindowViewModel(Logger);/*{  WinLeft = 8 , WinTop = 8, WinWidth = 450, WinHeight = 700};*/
                launchWinVM.CanClose = false;
                LaunchWindow launchWindow = new LaunchWindow() { DataContext = launchWinVM };
                launchWinVM.TitleMessage = "Application initializing, please wait...";
                launchWindow.Topmost = true;
                launchWindow.Show();

                //connect to SQ database
                string dbFileName = Path.Combine(commonAppDataPath, "Sequlite.db");
                IUser userAcc =  UserAccountFactory.CreaeteUserAccountInterfaceFromSeqDB(dbFileName, Logger);
                IIDHistory iDHistory = IDHistoryFactory.CreaeteIDHistoryInterfaceFromSeqDB(dbFileName, Logger);
                ISeqApp seqApp = SeqAppFactory.GetSeqApp();
                seqApp.IDHistory = iDHistory;


                //for testing purpose, add the following two id
                //bool b = false;
                //b = iDHistory.AddIDHistory("SeqQ100-v01-100M-1234567-072021", IdTypeEnum.Barcode, "testing bar-code");
                //b = iDHistory.AddIDHistory("SeqQ100-v01-100M-1234567-072021-SE-75", IdTypeEnum.RFID, "testing RFID");
                //b = iDHistory.MatchId("SeqQ100-v01-100M-1234567-072021-SE-75", IdTypeEnum.RFID);
                //SeqId seqid = iDHistory.ParseId("SeqQ100-v01-100M-1234567-072021-SE-75", IdTypeEnum.RFID);

                ISystemInit systemInit = seqApp.GetSystemInitInterface();
                
                IDisposable   appMessageObserver = AppObservableSubscriber.Subscribe(seqApp.ObservableAppMessage,
                            it => AppMessagegUpdated(it));
                
                bool deviceConnected = false;
                await Task.Factory.StartNew(() =>
                {
                    
                    deviceConnected = systemInit.Initialize(true); //connect to main hardware
                    Thread.Sleep(2000);
                    MainWindowViewModel mv = new MainWindowViewModel(Logger, seqApp, userAcc) { LogWindowVM = logWindowVM };
                    this.Dispatcher.Invoke(() =>
                    {
                        MainWindow _MainWindow = new MainWindow() { DataContext = mv };
                        this.MainWindow = _MainWindow;

                        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                        Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
                        if (_MainWindow != null)
                        {
                            _MainWindow.Show();
                            //_MainWindow.Topmost = false;
                            //launchWindow.Topmost = false;
                            launchWindow.Owner = _MainWindow;
                            launchWindow.Activate(); //bring it to front
                        }
                        else
                        {
                            if (Logger != null)
                            {
                                Logger.LogError("Failed to launch Main Window, exit the application.");
                            }
                            Thread.Sleep(1000);
                            Environment.Exit(-1);
                        }

                        Version version = Assembly.GetExecutingAssembly().GetName().Version;
                        productVersion = string.Format("{0}.{1}.{2}.{3}",
                                                   version.Major,
                                                   version.Minor,
                                                   version.Build,
                                                   version.Revision.ToString("D4"));

                    });
                });
                AppObservableSubscriber.Unsubscribe(appMessageObserver);
                //close launching window if devices were connected
                launchWinVM.TitleMessage = "Application initialization done";
                launchWinVM.CanClose = true;
                bool closeLunchingWindow = deviceConnected;
                if (closeLunchingWindow)
                {
                    Thread.Sleep(4000); //to output last log
                    this.Dispatcher.Invoke(() =>
                    {
                        launchWindow.Close();
                    });
                }
            }
            catch (Exception ex)
            {
                if (launchWinVM != null)
                {
                    launchWinVM.CanClose = true;
                }
                string message = string.Empty;
                message = "Application Startup Exception: " + ex.Message + "\n\nStack Trace:\n" + ex.StackTrace;
                if (Logger != null)
                {
                    Logger.LogError(message);
                }
                throw ex;
            }

            base.OnStartup(e);
        }
        void AppMessagegUpdated(AppMessage appMsg)
        {
            if (appMsg.MessageObject is LowDiskSpaceMessage e)
            {
                Dispatcher.Invoke(() =>
                {
         
                    MessageBox.Show(e.Mesage, e.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }
        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            string message = string.Empty;
            message = "Unhandled Application Exception: " + e.Exception.Message + "\n\nStack Trace:\n" + e.Exception.StackTrace;
            if (Logger != null)
            {
                Logger.LogError(message);
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            string message = string.Empty;
            message = "Unhandled Application Domain Exception: " + ex.Message + "\n\nStack Trace:\n" + ex.StackTrace;
            if (Logger != null)
            {
                Logger.LogError(message);
            }
        }
    }

}
