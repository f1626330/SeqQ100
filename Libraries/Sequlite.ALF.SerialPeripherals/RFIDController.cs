using RFID_Reader_Cmds;
using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.SerialPeripherals
{
    public class RFIDController
    {
        public delegate void PacketReceivedHandle();
        public event PacketReceivedHandle OnReceivedPacket;

        #region Private Fields
        private RFID_Reader_Com.Sp _RFID_Reader;
        private RFID_Reader_Cmds.ReceiveParser _Receiver;
        private string _HWVersion;
        private ISeqLog Logger = SeqLogFactory.GetSeqFileLog("RFIDController");
        #endregion Private Fields

        #region Constructor & Instance
        private static RFIDController _Instance;
        public static RFIDController GetInstance()
        {
            if(_Instance == null)
            {
                _Instance = new RFIDController();
            }
            return _Instance;
        }
        private RFIDController()
        {
            _RFID_Reader = RFID_Reader_Com.Sp.GetInstance();
            _Receiver = new RFID_Reader_Cmds.ReceiveParser();
            _RFID_Reader.ComDevice.DataReceived += _Receiver.DataReceived;
            _Receiver.PacketReceived += _Receiver_PacketReceived;
            ReadIDs = new ObservableCollection<RFIDProperties>();
            _HWVersion = string.Empty;
        }

        private void _Receiver_PacketReceived(object sender, RFID_Reader_Cmds.StrArrEventArgs e)
        {
            string[] packetRx = e.Data;
            string strPacket = string.Empty;
            for (int i = 0; i < packetRx.Length; i++)
            {
                strPacket += packetRx[i] + " ";
            }
            ReadPkt = strPacket;
            #region update received packet
            ReadIDs.Clear();
            if (packetRx[1] == RFID_Reader_Cmds.ConstCode.FRAME_TYPE_INFO && packetRx[2] == RFID_Reader_Cmds.ConstCode.CMD_INVENTORY)         //Succeed to Read EPC
            {
                int PCEPCLength = ((Convert.ToInt32((packetRx[6]), 16)) / 8 + 1) * 2;
                var pc = packetRx[6] + " " + packetRx[7];
                var epc = string.Empty;
                for (int i = 0; i < PCEPCLength - 2; i++)
                {
                    epc = epc + packetRx[8 + i];
                }
                epc = RFID_Reader_Cmds.Commands.AutoAddSpace(epc);
                var crc = packetRx[6 + PCEPCLength] + " " + packetRx[7 + PCEPCLength];
                RFIDProperties newId = new RFIDProperties()
                {
                    PC = pc,
                    EPC = epc,
                    CRC = crc
                };
                ReadIDs.Add(newId);
            }
            else if (packetRx[2] == ConstCode.CMD_GET_MODULE_INFO)
            {
                if (packetRx[5] == ConstCode.MODULE_HARDWARE_VERSION_FIELD)
                {
                    try
                    {
                        _HWVersion = string.Empty;
                        for (int i = 0; i < Convert.ToInt32(packetRx[4], 16) - 1; i++)
                        {
                            _HWVersion += (char)Convert.ToInt32(packetRx[6 + i], 16);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        _HWVersion = packetRx[6].Substring(1, 1) + "." + packetRx[7];
                        Logger.LogError(ex.ToString());
                    }
                }
            }
            OnReceivedPacket?.Invoke();
            #endregion update received packet
        }
        #endregion Constructor & Instance

        #region Public Properties
        public ObservableCollection<RFIDProperties> ReadIDs { get; }
        public string ReadPkt { get; private set; }
        #endregion Public Properties

        #region Public Functions
        public bool Connect(string portName)
        {
            try
            {
                _RFID_Reader.ComDevice.PortName = portName;
                _RFID_Reader.ComDevice.BaudRate = 115200;
                _RFID_Reader.ComDevice.Parity = Parity.None;
                _RFID_Reader.ComDevice.DataBits = 8;
                _RFID_Reader.ComDevice.StopBits = StopBits.One;
                _RFID_Reader.Open();
                if (_RFID_Reader.IsOpen())
                {
                    _RFID_Reader.Send(Commands.BuildGetModuleInfoFrame(ConstCode.MODULE_HARDWARE_VERSION_FIELD));
                    Thread.Sleep(200);
                    if (string.IsNullOrEmpty(_HWVersion))
                    {
                        return false;
                    }
                    return true;
                }
                return false;
            }
            catch(Exception ex)
            {
                Logger.Log(ex.ToString());
                return false;
            }
        }

        public void ReadId()
        {
            string cmd = RFID_Reader_Cmds.Commands.BuildReadSingleFrame();
            _RFID_Reader.Send(cmd);
        }
        #endregion Public Functions
    }

    public class RFIDProperties
    {
        public string PC { get; set; }
        public string EPC { get; set; }
        public string CRC { get; set; }
    }
}
