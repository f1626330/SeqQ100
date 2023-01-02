using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.MotionControl
{
    public class HywireGalilControl
    {
        #region Private Fields
        private SerialPort _GalilPort;
        private bool _IsConnected = false;
        private byte[] _ReadBuf = new byte[500];
        private static object _Lock = new object();
        private static object _LogLock = new object();
        //private int _LogIndex;
        private bool _IsNewLog = true;
        private string _FolderStr;
        private string _Filename;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("GalilComm");
        private List<string> _LogList;
        private Thread _LogThread;
        private int _PortName;
        #endregion Private Fields

        #region Constructor
        public HywireGalilControl()
        {
            _LogList = new List<string>();
            _LogThread = new Thread(() =>
            {
                while (true)
                {
                    while (_LogList.Count > 0)
                    {
                        FileMode mode = _IsNewLog ? FileMode.Create : FileMode.Append;
                        _IsNewLog = false;
                        try
                        {
                            using (FileStream fs = new FileStream(_FolderStr, mode))
                            {
                                byte[] logs = Encoding.Default.GetBytes(_LogList[0]);
                                fs.Write(logs, 0, logs.Length);
                                fs.Flush();
                                _LogList.RemoveAt(0);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex.ToString());
                        }
                    }
                    Thread.Sleep(100);
                }
            });
            _LogThread.IsBackground = true;
            _LogThread.Start();
        }
        #endregion Constructor

        public bool IsConnected { get { return _IsConnected; } }

        #region Public functions
        public bool Connect()
        {
            if (_IsConnected) { return true; }
            _Filename = string.Format("{0}GalilCommLog.txt", DateTime.Now.ToString("yyyyMMdd"));
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _FolderStr = Path.Combine(commonAppData, "Sequlite\\Log\\MotionLog");
            if (!Directory.Exists(_FolderStr))
            {
                DirectoryInfo di = Directory.CreateDirectory(_FolderStr);
            }
            _FolderStr = Path.Combine(_FolderStr, _Filename);
            _IsNewLog = !File.Exists(_FolderStr);
            Logger.Log("Galil Serial Communiciation Created");
            var portlists = SerialPort.GetPortNames();
            byte[] connectingBytes = { 0x12, 0x16, 0x0d };
            byte[] readingBuf = new byte[20];
            if (portlists != null && portlists.Length > 0)
            {
                for (int i = 0; i < portlists.Length; i++)
                {
                    try
                    {
                        _GalilPort = new SerialPort(portlists[i]);
                        _GalilPort.BaudRate = 115200;
                        _GalilPort.Parity = Parity.None;
                        _GalilPort.DataBits = 8;
                        _GalilPort.StopBits = StopBits.One;
                        _GalilPort.WriteTimeout = 500;
                        _GalilPort.ReadTimeout = 500;
                        _GalilPort.Open();
                        _GalilPort.Write("\r");
                        _GalilPort.Read(_ReadBuf, 0, 1);
                        _GalilPort.Write(connectingBytes, 0, connectingBytes.Length);
                        Thread.Sleep(100);
                        _GalilPort.Read(readingBuf, 0, readingBuf.Length);
                        string readingStr = System.Text.Encoding.Default.GetString(readingBuf);
                        if (readingStr.Contains("DMCB140"))
                        {
                            _IsConnected = true;
                            _PortName = i;
                            HomeAllMotions();
                            break;
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        Logger.LogError(ex.ToString());
                        _GalilPort.Close();
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Logger.LogError(ex.ToString());
                    }
                }
            }
            return _IsConnected;
        }
        public bool Connect(string portName)
        {
            if (_IsConnected) { return true; }
            _Filename = string.Format("{0}GalilCommLog.txt", DateTime.Now.ToString("yyyyMMdd"));
            _FolderStr = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _Filename);
            _IsNewLog = !File.Exists(_FolderStr);
            Logger.Log("Galil Serial Communiciation Created");
            byte[] connectingBytes = { 0x12, 0x16, 0x0d };
            byte[] readingBuf = new byte[20];
            try
            {
                _GalilPort = new SerialPort(portName);
                _GalilPort.BaudRate = 115200;
                _GalilPort.Parity = Parity.None;
                _GalilPort.DataBits = 8;
                _GalilPort.StopBits = StopBits.One;
                _GalilPort.WriteTimeout = 500;
                _GalilPort.ReadTimeout = 500;
                _GalilPort.Open();
                _GalilPort.Write("\r");
                _GalilPort.Read(_ReadBuf, 0, 1);
                _GalilPort.Write(connectingBytes, 0, connectingBytes.Length);
                Thread.Sleep(100);
                _GalilPort.Read(readingBuf, 0, readingBuf.Length);
                string readingStr = System.Text.Encoding.Default.GetString(readingBuf);
                if (readingStr.Contains("DMCB140"))
                {
                    _IsConnected = true;
                    HomeAllMotions();
                }
            }
            catch (TimeoutException ex)
            {
                Logger.LogError(ex.ToString());
                _GalilPort.Close();
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.LogError(ex.ToString());
            }
            return _IsConnected;
        }
        public bool Reconnect()
        {
            _GalilPort.Close();
            bool suc = Connect(_GalilPort.PortName);
            return suc;

        }
        public bool SendCommand(string cmd)
        {
            try
            {
                lock (_Lock)
                {
                    if (_GalilPort == null)
                    {
                        return false;
                    }

                    for (int retry = 0; retry < 2; retry++)
                    {
                        if (_GalilPort.BytesToRead > 0)         // 1. clear the read buffer before sending the command
                        {
                            _GalilPort.Read(_ReadBuf, 0, _GalilPort.BytesToRead);
                        }

                        LogToFile(string.Format("{0} Sending Galil Command: {1}\r\n", DateTime.Now.ToString("HH:mm:ss:fff"), cmd));
                        _GalilPort.Write(cmd + "\r");
                        StringBuilder logStr = new StringBuilder();
                        logStr.Append(DateTime.Now.ToString("HH:mm:ss:fff"));
                        logStr.Append(" OUT: ");
                        byte[] cmdBytes = Encoding.ASCII.GetBytes(cmd + "\r");
                        foreach (byte b in cmdBytes)
                        {
                            logStr.AppendFormat("{0:X2} ", b);
                        }
                        logStr.Append("\r\n");
                        LogToFile(logStr.ToString());

                        byte[] resBytes;
                        int offset = 0;
                        int tryCounts = 0;
                        Thread.Sleep(100);       // wait for controller response
                        do
                        {
                            if (++tryCounts > 200)
                            {
                                break;
                            }
                            if (_GalilPort.BytesToRead == 0)
                            {
                                Thread.Sleep(10);
                                continue;
                            }
                            int bytesToRead = _GalilPort.BytesToRead;
                            _GalilPort.Read(_ReadBuf, offset, bytesToRead);
                            offset += bytesToRead;
                        }
                        while (offset <= 0 || _ReadBuf[offset - 1] != 0x3A);
                        if (tryCounts > 200)    // retry sending the command and wait for response again
                        {
                            if (retry == 0)
                            {
                                LogToFile(string.Format("{0} No response, retry...\r\n", DateTime.Now.ToString("HH:mm:ss:fff")));
                                //Logger.Log("No response, retry.");
                            }
                            continue;
                        }
                        resBytes = new byte[offset];
                        for (int i = 0; i < offset; i++)
                        {
                            resBytes[i] = _ReadBuf[i];
                        }

                        logStr = new StringBuilder();
                        logStr.Append(DateTime.Now.ToString("HH:mm:ss:fff"));
                        logStr.Append(" IN: ");
                        foreach (byte b in resBytes)
                        {
                            logStr.AppendFormat("{0:X2} ", b);
                        }
                        logStr.Append("\r\n");
                        LogToFile(logStr.ToString());
                        return true;
                    }
                    Logger.Log(string.Format("Send Command {0} failed.", cmd));
                    return false;       // return false if retry failed, too
                }

            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
                return false;
            }
        }
        public byte[] Record()
        {
            byte[] result = new byte[123];
            if (SendCommand("QR") == false) { return null; }
            int index = 122;
            for (; index < _ReadBuf.Length; index++)
            {
                if (_ReadBuf[index] == 0x3A)
                {
                    break;
                }
            }
            if (index == _ReadBuf.Length - 1) { return null; }

            Buffer.BlockCopy(_ReadBuf, index - 122, result, 0, 123);
            return result;
        }
        public double SourceValue(byte[] record, string source)
        {
            double retVal = 0;
            DataRecordStruct records = (DataRecordStruct)BytesToStructConverter.BytesToStruct(record, typeof(DataRecordStruct));
            switch (source)
            {
                case "_RPA":
                    retVal = records.OutputPosAxisA;
                    break;
                case "_RPB":
                    retVal = records.OutputPosAxisB;
                    break;
                case "_RPC":
                    retVal = records.OutputPosAxisC;
                    break;
            }
            return retVal;
        }
        #endregion Public functions

        #region Private functions
        private bool HomeAllMotions()
        {
            if (_IsConnected == false) { return false; }
            SendCommand("HX");
            SendCommand("STX");
            SendCommand("SHX");
            SendCommand(String.Format("SPX={0}", 4500));
            SendCommand("STY");
            SendCommand("SHY");
            SendCommand(String.Format("SPY={0}", 25000));
            SendCommand("STZ");
            SendCommand("SHZ");
            SendCommand(String.Format("SPZ={0}", 10000));
            SendCommand("HMX");
            SendCommand("HMY");
            SendCommand("HMZ");
            SendCommand("BGX");
            SendCommand("BGY");
            SendCommand("BGZ");
            return true;
        }

        private void LogToFile(string newLog)
        {
            _LogList.Add(newLog);
            //Task.Run(() =>
            //{
            //    lock (_LogLock)
            //    {
            //        FileMode mode = _IsNewLog ? FileMode.Create : FileMode.Append;
            //        _IsNewLog = false;
            //        try
            //        {
            //            using (FileStream fs = new FileStream(_Filename, mode))
            //            {
            //                byte[] logs = Encoding.Default.GetBytes(newLog);
            //                fs.Write(logs, 0, logs.Length);
            //                fs.Flush();
            //            }
            //            Logger.Log(newLog, SeqLogFlagEnum.DEBUG);
            //        }
            //        catch (Exception ex)
            //        {
            //            Logger.LogError(ex.ToString());
            //        }
            //    }

            //});
        }
        #endregion Private functions

    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1, Size = 123)]  // aligned by 1 byte
    internal struct DataRecordStruct
    {
        public uint Head;
        public ushort SampleCounts;
        public byte Input;
        public byte Output;
        public byte ErrorCode;
        public byte ThreadState;
        public uint ProfileCounts;
        public ushort ProfileState;
        public ushort InterpCounts;
        public ushort InterpState;
        public int InterpTrip;
        public ushort InterpBufState;
        public ushort StateAxisA;
        public byte SwitchesAxisA;
        public byte StopCodeAxisA;
        public int CommandPosAxisA;
        public int EncoderPosAxisA;
        public int OutputPosAxisA;
        public int SpeedAxisA;
        public int UserDefinedAxisA;
        public ushort StateAxisB;
        public byte SwitchesAxisB;
        public byte StopCodeAxisB;
        public int CommandPosAxisB;
        public int EncoderPosAxisB;
        public int OutputPosAxisB;
        public int SpeedAxisB;
        public int UserDefinedAxisB;
        public ushort StateAxisC;
        public byte SwitchesAxisC;
        public byte StopCodeAxisC;
        public int CommandPosAxisC;
        public int EncoderPosAxisC;
        public int OutputPosAxisC;
        public int SpeedAxisC;
        public int UserDefinedAxisC;
        public ushort StateAxisD;
        public byte SwitchesAxisD;
        public byte StopCodeAxisD;
        public int CommandPosAxisD;
        public int EncoderPosAxisD;
        public int OutputPosAxisD;
        public int SpeedAxisD;
        public int UserDefinedAxisD;
        public byte End;
    }
    internal static class BytesToStructConverter
    {
        public static byte[] StructToBytes(object structure)
        {
            int size = Marshal.SizeOf(structure);
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(structure, buffer, false);
                byte[] bytes = new byte[size];
                Marshal.Copy(buffer, bytes, 0, size);

                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

        }

        public static object BytesToStruct(byte[] bytes, Type strcutType)
        {
            int size = Marshal.SizeOf(strcutType);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            object result = new object();

            try
            {
                Marshal.Copy(bytes, 0, buffer, size);

                result = Marshal.PtrToStructure(buffer, strcutType);
            }
            catch
            {

            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return result;
        }
    }
}
