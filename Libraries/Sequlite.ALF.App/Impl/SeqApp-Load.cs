using Sequlite.ALF.Common;
using System;
using Sequlite.ALF.SerialPeripherals;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.IO;

namespace Sequlite.ALF.App
{
    class SeqAppLoad : ILoad
    {
        #region private fields
        //private string OldFCBarcode;
        //private string OldReagentRFID;
        private string RecordFileDir;
        
        #endregion private fields

        #region Public Properties
        public string FCBarcode { get; private set; }

        public string ReagentRFID { get; private set; }
        #endregion Public Properties
        SeqApp SeqApp { get; }
        public SeqAppLoad(SeqApp seqApp)
        {
            SeqApp = seqApp;
        }
        public string ReadFCBarCode()
        {
            BarCodeReader _BarCodeReader = BarCodeReader.GetInstance();
            string barcode = null;
            if (!_BarCodeReader.IsConnected)
            {
                SeqApp.UpdateAppErrorMessage("Barcode reader did not connect");
                
            }
            else
            {
                int xPos = (int)(25 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                int xSpeed = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                int xAcc = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
                int yPos = (int)(95 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                int ySpeed = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                int yAcc = (int)(SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
                if(SeqApp.MotionController.AbsoluteMove(MotionTypes.XStage, xPos, xSpeed, xAcc, true) && SeqApp.MotionController.AbsoluteMove(MotionTypes.YStage, yPos, ySpeed, yAcc, true))
                {
                    int waitCnts = 0;
                    while (SeqApp.MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Y].IsBusy || SeqApp.MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_X].IsBusy)
                    {
                        if (waitCnts > 100)
                        {
                            SeqApp.UpdateAppErrorMessage("Move XY failed");
                            return barcode;
                        }
                        Thread.Sleep(100);
                    }
                    barcode = _BarCodeReader.ScanBarCode();
                    if (string.IsNullOrEmpty(barcode))
                    {
                        barcode = _BarCodeReader.ScanBarCode();
                        //if (string.IsNullOrEmpty(barcode)) { SeqApp.UpdateAppErrorMessage("Barcode reader failed"); }
                    }
                }
                else { SeqApp.UpdateAppErrorMessage("Move XY failed"); }
                
            }
            return barcode;
        }
        public string ReadRFID()
        {
            RFIDController _RFIDReader = RFIDController.GetInstance();
            string RFID = null;
            int tryCnts = 0;
            do
            {
                _RFIDReader.ReadId();
                Thread.Sleep(100);
                if (_RFIDReader.ReadIDs != null && _RFIDReader.ReadIDs.Count > 0)
                {
                    RFID = _RFIDReader.ReadIDs[0].EPC;
                    break;
                }

                tryCnts++;
                if (tryCnts > 10)
                {
                    SeqApp.UpdateAppErrorMessage("Failed to read RFID");
                    break;
                }
            } while (_RFIDReader.ReadIDs.Count == 0);
            return RFID;
        }
        public void RecordBarcodeID(string barcode, string RFID)
        {
            SeqApp.IDHistory.AddIDHistory(barcode, IdTypeEnum.Barcode);
            SeqApp.IDHistory.AddIDHistory(RFID, IdTypeEnum.RFID);
        }
        public bool CheckIDExist(string id)
        {
            try
            {
                using(var reader = new StreamReader(RecordFileDir))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] values = line.Split(';');

                        if (values.Contains(id))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch(Exception ex)
            {
                SeqApp.UpdateAppErrorMessage(ex.ToString());
                return false;
            }
            
        }
        public bool UnloadFC()
        {
            //Barcode RFID file dir
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string RecordDir = Path.Combine(commonAppData, "Sequlite\\");
            bool ismovesucc = true;
            RecordFileDir = RecordDir + "BarCode_RFID_Record.csv";
            //Check FC Clamp
            MainBoardController _MBController =  MainBoardController.GetInstance();
            _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
            if (_MBController.FCClampStatus)
            {

                SeqApp.NotifyError("Please put on FC and clamp it");
                return false;
            }
            //OldFCBarcode = ReadFCBarCode();

            //open FC door
#if !DisableFCDoor
            if (_MBController.HWVersion == "2.0.0.1")
            {
                if (!MainBoardController.GetInstance().SetDoorStatus(true))
                {
                    Thread.Sleep(100);
                    if (!MainBoardController.GetInstance().SetDoorStatus(true))
                    {
                        SeqApp.NotifyError("Failed to open door");
                        return false;
                    }
                }
            }
            else if (_MBController.IsProtocolRev2)
            {
                if (!MotionControl.MotionController.GetInstance().SetFCDoorStatus(true))
                {
                    Thread.Sleep(100);
                    if (!MotionControl.MotionController.GetInstance().SetFCDoorStatus(true))
                    {
                        SeqApp.NotifyError("Failed to open door");
                        return false;
                    }
                }
            }
#endif

            //change valvepos to 24, prevent backflow
            SeqApp.FluidicsInterface.Valve.SetToNewPos(24, true);

            //raise reagent sipper
            int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
            if (!SeqApp.MotionController.IsCartridgeAvailable)
            {
                var _Chiller = Chiller.GetInstance();
                if (_Chiller.ChillerMotorControl(false) == false) //Unload
                {
                    SeqApp.UpdateAppErrorMessage("Move Cartridge failed");
                    ismovesucc = false;
                }
            }
            else
            {
                if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, 0, speed, accel, false, true))
                {
                    SeqApp.UpdateAppErrorMessage("Move Cartridge failed");
                    ismovesucc = false;
                }
                //else
                //{
                //    OldReagentRFID = ReadRFID();
                //    // what if it is empty.
                //    //if (string.IsNullOrEmpty(OldReagentRFID)) { return false; }
                //}
            }

            int xSpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Speed);
            int xAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.XStage].Accel);
            int xPos = (int)(20 * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage]);
            int ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Speed);
            int yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.YStage].Accel);
            int yPos = (int)(SettingsManager.ConfigSettings.MotionSettings[MotionTypes.YStage].MotionRange.LimitHigh * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage]);
            if(!SeqApp.MotionController.AbsoluteMove(MotionTypes.XStage, xPos, xSpeed, xAccel, false, true))
            {
                //if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.XStage, xPos, xSpeed, xAccel, true))
                //{ 
                ismovesucc = false; 
                //}
            }
            if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.YStage, yPos, ySpeed, yAccel, false, true)) 
            { 
                //if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.YStage, yPos, ySpeed, yAccel, true)) 
                //{ 
                ismovesucc = false; 
                //} 
            }
            
            return ismovesucc;
        }
        public bool LoadFC()
        {
            //Check FC Clamp
            MainBoardController _MBController = MainBoardController.GetInstance();
            _MBController.ReadRegisters(MainBoardController.Registers.OnoffInputs, 1);
            if (_MBController.FCClampStatus)
            {
                SeqApp.NotifyError("Please load FC and clamp it");
                return false;
            }
            //Read Barcode
#if !DisableBarCodeReader
            if (MotionControl.MotionController.GetInstance().IsBarcodeReaderEnabled)
            {
                FCBarcode = ReadFCBarCode();
                if (string.IsNullOrEmpty(FCBarcode)) { SeqApp.NotifyError("Failed to read FC's barcode, please check and try again"); return false; }
            }
#endif
            int ySpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Speed);
            int yAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.YStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.YStage].Accel);
            int xSpeed = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Speed);
            int xAccel = (int)(SettingsManager.ConfigSettings.MotionFactors[MotionTypes.XStage] * SettingsManager.ConfigSettings.MotionHomeSettings[MotionTypes.XStage].Accel);
            if (!SeqApp.MotionController.AbsoluteMove(MotionTypes.YStage, 0, ySpeed, yAccel, true, true) || !SeqApp.MotionController.AbsoluteMove(MotionTypes.XStage, 0, xSpeed, xAccel, true, true))
            {
                SeqApp.NotifyError("Load failed");
                return false;
            }
            // close door
#if !DisableFCDoor
            if(_MBController.HWVersion == "2.0.0.1") //2.2 / 2.3
            {
                if (!MainBoardController.GetInstance().SetDoorStatus(false))
                {
                    Thread.Sleep(100);
                    if (!MainBoardController.GetInstance().SetDoorStatus(false))
                    {
                        SeqApp.NotifyError("Failed to close door");
                        return false;
                    }
                }
            }
            else if (_MBController.IsProtocolRev2) // 2.5
            {
                if (!MotionControl.MotionController.GetInstance().SetFCDoorStatus(false))
                {
                    Thread.Sleep(100);
                    if (!MotionControl.MotionController.GetInstance().SetFCDoorStatus(false))
                    {
                        SeqApp.NotifyError("Failed to close door");
                        return false;
                    }
                }
            }
#endif          

            //fire event update VM

            return true;

        }
        
        public bool LoadReagent()
        {
            //check Reagent sensor
            Chiller _Chiller = Chiller.GetInstance();
            _Chiller.ReadRegisters(Chiller.Registers.OnoffInputs, 1);
            if (!_Chiller.CartridgeDoor && MotionControl.MotionController.GetInstance().IsReagentDoorEnabled) // is closed
            {
#if !DisableReagentDoor
                SeqApp.NotifyError("Please close the reagent door");
                return false;
#endif
            }
            if (!_Chiller.CartridgePresent)
            {
                SeqApp.NotifyError("Please push back the reagent more");
                return false;
            }
            //Read RFID
#if !DisableRFIDReader
            if (MotionControl.MotionController.GetInstance().IsRFIDReaderEnabled)
            {
                ReagentRFID = ReadRFID();
                if (string.IsNullOrEmpty(ReagentRFID)) { SeqApp.NotifyError("Failed to read RFID, retry"); return false; }
                //Record Barcode and RFID
                RecordBarcodeID(FCBarcode, ReagentRFID);
            }
#endif

            //Lower Sipper
            if (!SeqApp.MotionController.IsCartridgeAvailable)
            {
                if (_Chiller.SetChillerMotorAbsMove(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos) == false)
                {
                    if(_Chiller.SetChillerMotorAbsMove(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos) == false)
                    {
                        SeqApp.UpdateAppErrorMessage("Load Cartridge failed.");
                        return false;
                    }
                }
                int retry = 0;
                _Chiller.GetChillerMotorPos();
                while(!_Chiller.CheckCartridgeSippersReagentPos())
                {
                    if(++retry > 140)
                    {
                        SeqApp.UpdateAppErrorMessage($"Load Cartridge timeout, current position:{_Chiller.CartridgeMotorPos}"); ;
                        return false;
                    }
                    Thread.Sleep(1000);
                }
            }
            else
            {
                int pos = (int)Math.Round((SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel * SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                {
                    //if (SeqApp.MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel, true) == false)
                    //{
                    SeqApp.UpdateAppErrorMessage("Load cartridge failed.");
                    return false;
                    //}
                }
            }
            // lock chiller door if the function avaliable 
            //if (Chiller.GetInstance().IsProtocolRev2)
            //{
            //    if (_Chiller.ChillerDoorControl(true))
            //    {
            //        if (_Chiller.ChillerDoorControl(true))
            //        {
            //            //SeqApp.UpdateAppErrorMessage("Lock Chiller Door failed.");
            //            //return false;
            //        }
            //    }
            //}
            return true;
        }

        public bool LoadBuffer()
        {
            FluidController _FluidController = FluidController.GetInstance();
            _FluidController.ReadRegisters(FluidController.Registers.OnoffInputs, 1);
            bool bufferin = _FluidController.BufferTrayIn;
            bool sipperdown = _FluidController.SipperDown;
            if (bufferin)
            {
                SeqApp.NotifyError("Please load the buffer");
                return false;
            }
            if (sipperdown)
            {
                SeqApp.NotifyError("Please lower the sipper");
                return false;
            }

            // Enable Next, event

            return true;
        }
        public bool LoadWaste()
        {
            if (MainBoardController.GetInstance().HWVersion != "2.0.0.0" && MainBoardController.GetInstance().HWVersion != "2.0.0.1" && MainBoardController.GetInstance().HWVersion != "2.0.0.2")
            {
                double MassOfWaste = -1;
                if (FluidController.GetInstance().ReadMassOfWaste())
                {
                    MassOfWaste = FluidController.GetInstance().MassOfWaste;
                }
                else
                {
                    if (FluidController.GetInstance().ReadMassOfWaste())
                    {
                        MassOfWaste = FluidController.GetInstance().MassOfWaste;
                    }
                    else
                    {
                        SeqApp.NotifyError("Failed to read mass of waste!");
                    }
                }
                if (MassOfWaste > SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WasteCartridgeEmptyWeight)
                {
                    SeqApp.NotifyError("Please empty waste");
                }
                else if (MassOfWaste < 0)
                {
                    SeqApp.NotifyError("Please place a waste container");
                }
            }


            return true;
        }
    }
}
