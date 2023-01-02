using Sequlite.ALF.SerialPeripherals;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.ALF.EngineerGUI.ViewModel
{
    public class BarCodeReaderViewModel : ViewModelBase
    {
        #region Private Fields
        private BarCodeReader _Reader;
        private string _ScannedCode;
        #endregion Private Fields

        #region Constructor
        public BarCodeReaderViewModel()
        {
            _Reader = BarCodeReader.GetInstance();
        }
        #endregion Constructor

        #region Public Properties
        public string ScannedCode
        {
            get { return _ScannedCode; }
            set
            {
                if (_ScannedCode != value)
                {
                    _ScannedCode = value;
                    RaisePropertyChanged(nameof(ScannedCode));
                }
            }
        }
        #endregion Public Properties

        #region Public Functions
        public bool Connect(string portName, int baudRate = 9600)
        {
            return _Reader.Connect(portName, baudRate);
        }
        public bool Connect(int baudRate = 9600)
        {
            var portList = SerialPort.GetPortNames();
            foreach(var port in portList)
            {
                if(_Reader.Connect(port, baudRate))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion Public Functions

        #region Read Command
        private RelayCommand _ReadCmd;
        public ICommand ReadCmd
        {
            get
            {
                if (_ReadCmd == null)
                {
                    _ReadCmd = new RelayCommand(ExecuteReadCmd, CanExecuteReadCmd);
                }
                return _ReadCmd;
            }
        }

        private void ExecuteReadCmd(object obj)
        {
            ScannedCode = _Reader.ScanBarCode();
        }

        private bool CanExecuteReadCmd(object obj)
        {
            return _Reader.IsConnected;
        }
        #endregion Read Command

    }
}
