using Sequlite.ALF.Common;

using System;

using System.Windows;
using System.Windows.Input;

namespace Sequlite.WPF.Framework
{
    /// <summary>
    /// LaunchWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LaunchWindow : Window
    {
       
        public LaunchWindow()
        {
            InitializeComponent();
        }

       
    }

    public class LaunchWindowViewModel : ViewModelBase
    {
        public ILogDisplayFilter LogDisplayFilter { get; private set; }

        private LogViewerViewModel _LogViewerVM = null;
        public LogViewerViewModel LogViewerVM
        {
            get { return _LogViewerVM; }
            set
            {
                _LogViewerVM = value;
                if (value != null)
                {
                    LogDisplayFilter = value.LogDisplayFilter;
                }
                else
                {
                    LogDisplayFilter = null;
                }
                RaisePropertyChanged("LogViewerVM");
                RaisePropertyChanged("LogDisplayFilter");
            }
        }

        ISeqLog Logger { get; }
        public LaunchWindowViewModel(ISeqFileLog log, SeqLogFlagEnum logFlagToDisplay = SeqLogFlagEnum.STARTUP)
        {
            Logger = log;
            LogViewerVM = new LogViewerViewModel(log);
            //LogViewerVM.LogDisplayFilter?.AddSubSystemDisplayFilter(uiSubSystemFilterName);
            LogViewerVM.LogDisplayFilter?.AddFlagDisplayFilterOut(~logFlagToDisplay);
            Logger.Log("Application starts.");
        }

      

        private ICommand _WindowClosing = null;
        public ICommand WindowClosing
        {
            get
            {
                if (_WindowClosing == null)
                {
                    _WindowClosing = new RelayCommand(o => Closeing(o), o => CanClose);
                }
                return _WindowClosing;
            }
        }

        bool _canClose;
        public bool CanClose
        {
            get
            {
                return _canClose;
            }
            set
            {
                _canClose = value;
                RaisePropertyChanged(nameof(CanClose));
            }

        }

        private void Closeing(object obj)
        {
            
        }

        string _Title = "Launching, please wait...";
        public string TitleMessage
        {
            get => _Title;
            set 
            {
                SetProperty(ref _Title, value, nameof(TitleMessage));
            }
        }

        int _WinWidth = 600;
        public int WinWidth
        {
            get => _WinWidth;
            set
            {
                SetProperty(ref _WinWidth, value, nameof(WinWidth));
            }
        }

        int _WinHeight = 400;
        public int WinHeight
        {
            get => _WinHeight;
            set
            {
                SetProperty(ref _WinHeight, value, nameof(WinHeight));
            }
        }

        int _WinLeft = 10;
        public int WinLeft
        {
            get => _WinLeft;
            set
            {
                SetProperty(ref _WinLeft, value, nameof(WinLeft));
            }
        }

        int _WinTop = 10;
        public int WinTop
        {
            get => _WinTop;
            set
            {
                SetProperty(ref _WinTop, value, nameof(WinTop));
            }
        }
    }
}
