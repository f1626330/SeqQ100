using Hywire.CommLibrary;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.SerialPeripherals
{
    public class SerialPeripheralBase : SerialCommBase
    {
        #region Private Fields
        protected object _ThreadLock;
        protected ALFCommonProtocol _Answer;
        protected AddressTypes _Address;
        #endregion Private Fields

        #region Constructor
        public SerialPeripheralBase(AddressTypes address) : base(1500)
        {
            _Address = address;
            _ThreadLock = new object();
        }
        #endregion Constructor

        #region Public Properties
        public bool IsConnected { get; protected set; }
        public string DeviceType { get; protected set; }
        public string HWVersion { get; protected set; }
        public string FWVersion { get; protected set; }
        #endregion Public Properties

        #region Public Functions
        public bool Connect(int baudRate = 115200)
        {
            if (IsConnected) { return true; }
            string[] ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                if (Open(port, baudRate))
                {
                    byte[] sendBytes = ReadRequest(1, 3);
                    if (SendBytes(sendBytes))
                    {
                        IsConnected = true;
                        break;
                    }
                    Close();
                }
            }
            return IsConnected;
        }
        public bool Connect(string portName, int baudRate = 115200)
        {
            if (IsConnected) { return true; }
            if (Open(portName, baudRate))
            {
                byte[] sendBytes = ReadRequest(1, 3);
                if (SendBytes(sendBytes))
                {
                    IsConnected = true;
                }
                else
                {
                    Close();
                }
            }
            return IsConnected;
        }
        public void DisConnect()
        {
            if (!IsConnected) { return; }
            Close();
        }

        public override bool Close()
        {
            IsConnected = false;
            return base.Close();
        }
        #endregion Public Functions

        #region Protected Functions
        protected bool ReadRegisters(byte startReg, byte regNums)
        {
            lock (_ThreadLock)
            {
                byte[] sendBytes = ReadRequest(startReg, regNums);
                if (SendBytes(sendBytes))
                {
                    if (!_Answer.IsCrcCorrect) { return false; }
                    return true;
                }
                else { return false; }
            }
        }
        protected SettingResults WriteRegisters(byte startReg, byte regNums, int[] values)
        {
            lock (_ThreadLock)
            {
                byte[] sendBytes = WriteRequest(startReg, regNums, values);
                if (SendBytes(sendBytes))
                {
                    if (!_Answer.IsCrcCorrect) { return SettingResults.CrcCheckFailed; }
                    return (SettingResults)_Answer.DataField[0];
                }
                else
                {
                    return SettingResults.SettingTimeout;
                }
            }
        }
        public virtual string[] GetRegisterNames()
        {
            return null;
        }
        protected byte[] ReadRequest(byte startReg, byte regNums)
        {
            ALFCommonProtocol protocol = new ALFCommonProtocol();
            protocol.Address = _Address;
            protocol.Function = FunctionTypes.Read;
            protocol.StartReg = startReg;
            protocol.RegNums = regNums;
            return protocol.GetBytes();
        }
        protected byte[] WriteRequest(byte startReg, byte regNums, int[] values)
        {
            if (values == null || regNums > values.Length)
            {
                throw new ArgumentException("Reg Numbers is larger than values' length");
            }
            ALFCommonProtocol protocol = new ALFCommonProtocol();
            protocol.Address = _Address;
            protocol.Function = FunctionTypes.Write;
            protocol.StartReg = startReg;
            protocol.RegNums = regNums;
            protocol.DataField = new byte[regNums * 4];
            for (int i = 0; i < regNums; i++)
            {
                byte[] bytes = BitConverter.GetBytes(values[i]);
                bytes.CopyTo(protocol.DataField, i * 4);
            }
            return protocol.GetBytes();
        }

        protected override void ResponseDetect(out int detectedFrameIndex)
        {
            throw new NotImplementedException();
        }
        //protected virtual void DetectingResponse(out int detectedFrameIndex)
        //{
        //    detectedFrameIndex = 0;
        //}
        //protected override void ResponseDetect(out int detectedFrameIndex)
        //{
        //    DetectingResponse(out detectedFrameIndex);
        //}
        #endregion Protected Functions
    }
}
