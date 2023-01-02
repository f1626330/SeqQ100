using Sequlite.ALF.Common;
using Sequlite.ALF.MainBoard;
using Sequlite.ALF.SerialPeripherals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Sequlite.ALF.App
{
    class MonitorEntity : TrackableData
    {
        public double ChemiTemperGet { get=>Get<double>(); set=>Set(value); }
        public bool? IsChemiTemperCtrlOnGet { get => Get<bool?>(); set => Set(value); }
        public double ChemiTemperCtrlRampGet { get => Get<double>(); set => Set(value); }
        public double HeatSinkTemper { get => Get<double>(); set => Set(value); }
        public double PreHeatingTemper { get => Get<double>(); set => Set(value); }
        public double CoolerTemperGet { get => Get<double>(); set => Set(value); }
        public double CoolerHeatSinkTemper { get => Get<double>(); set => Set(value); }        
        public double AmbientTemper { get => Get<double>(); set => Set(value); }
        public bool? IsFCClamped { get => Get<bool?>(); set => Set(value); }
        public bool? IsFCDoorClosed { get => Get<bool?>(); set => Set(value); }
        public bool? ChillerDoorClosed { get => Get<bool?>(); set => Set(value); }
        public bool? IsCartridgePresented { get => Get<bool?>(); set => Set(value); }
        public bool? IsOverflowSensorOn { get => Get<bool?>(); set => Set(value); }
        public bool? IsDoorOpened { get => Get<bool?>(); set => Set(value); }

        public bool? IsBufferSipperDown { get => Get<bool?>(); set => Set(value); }
        public bool? IsBufferTrayIn { get => Get<bool?>(); set => Set(value); }
        public double MassOfWaste { get => Get<double>(); set => Set(value); }        

        public void Copy(MonitorEntity d)
        {
            ChemiTemperGet = d.ChemiTemperGet;
            IsChemiTemperCtrlOnGet = d.IsChemiTemperCtrlOnGet;
            ChemiTemperCtrlRampGet = d.ChemiTemperCtrlRampGet;
            HeatSinkTemper = d.HeatSinkTemper;
            PreHeatingTemper = d.PreHeatingTemper;
            CoolerTemperGet = d.CoolerTemperGet;
            CoolerHeatSinkTemper = d.CoolerHeatSinkTemper;
            AmbientTemper = d.AmbientTemper;
            IsFCClamped = d.IsFCClamped;
            IsFCDoorClosed = d.IsFCDoorClosed;
            ChillerDoorClosed = d.ChillerDoorClosed;
            IsCartridgePresented = d.IsCartridgePresented;
            IsOverflowSensorOn = d.IsOverflowSensorOn;
            IsDoorOpened = d.IsDoorOpened;
			IsBufferTrayIn = d.IsBufferTrayIn;
            IsBufferSipperDown = d.IsBufferSipperDown;
            MassOfWaste = d.MassOfWaste;
        }

        public List<object> GetDatas()
        {            
            return new List<object>()
            {
                DateTime.Now.ToLocalTime().ToString("HH:mm:ss.fff"),
                AmbientTemper.ToString("f3"),
                ChemiTemperGet.ToString("f3"),
                HeatSinkTemper.ToString("f3"),
                CoolerTemperGet.ToString("f3"),
                CoolerHeatSinkTemper.ToString("f3"),
                PreHeatingTemper.ToString("f3"),
                MassOfWaste.ToString("f3"),
            };
        }
    }

    partial class SeqApp 
    {
        [Flags]
        enum ChangeFlags
        {
           TemperChange = 0x0001,

        }

        int[] _MonitorDataLock = new int[0];
        Lazy<Subject<TemperatureStatusBase>> _TemperatureMonitor = new Lazy<Subject<TemperatureStatusBase>>(() => new Subject<TemperatureStatusBase>());
        Lazy<Subject<StatusBase>> _DeviceStatusMonitor = new Lazy<Subject<StatusBase>>(() => new Subject<StatusBase>());
        public IObservable<TemperatureStatusBase> TemperatureMonitor { get => _TemperatureMonitor.Value; }
        public IObservable<StatusBase> DeviceStatusMonitor { get => _DeviceStatusMonitor.Value; }
        private Chiller _Chiller;
        //private MotionController _MotionController;
        //private System.Timers.Timer _Timer;
        //public bool IsChillerTempReady { get; private set; }// = true;// for simul
        double SampleInterval { get; set; } = 1;

        Thread AppMonitor { get; set; }
        bool RunMonitor { get; set; }

        MonitorEntity MonitorData { get; set; }
        ChangeFlags _Flags;
        void SetChanges(ChangeFlags flag) => _Flags |= flag;
        void ResetChanges() => _Flags = 0;
        bool IsChanged(ChangeFlags flag) => (_Flags & flag) == flag;

        void StartAppMonitor()
        {
            if (Initialized || IsSimulation)
            {
                CreateTemperatureMetrics();
                if (MonitorData != null)
                {
                    MonitorData.PropertyChanged -= MonitorData_PropertyChanged;
                }
                MonitorData =   new MonitorEntity();
                MonitorData.PropertyChanged += MonitorData_PropertyChanged;
                //start monitoring service
                AppMonitor = new Thread(() => Monitor());
                AppMonitor.Name = "AppMonitor";
                AppMonitor.IsBackground = true;
                AppMonitor.Start();
            }
        }

        private void MonitorData_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MonitorEntity.AmbientTemper):
                case nameof(MonitorEntity.CoolerTemperGet):
                case nameof(MonitorEntity.ChemiTemperGet):
                case nameof(MonitorEntity.PreHeatingTemper):
                case nameof(MonitorEntity.HeatSinkTemper):
                    SetChanges(ChangeFlags.TemperChange);
                    break;
            }

            var filed = sender.GetType().GetProperties().FirstOrDefault(o => o.Name == e.PropertyName);
            if (filed != null)
                Send(DeviceStatusMonitor, new StatusBase(e.PropertyName, filed.GetValue(sender)));          
        }

        void StopAppMinitor()
        {
            RunMonitor = false;
            if (MonitorData != null)
            {
                MonitorData.PropertyChanged -= MonitorData_PropertyChanged;
            }
            if (AppMonitor?.IsAlive == true)
            {
                AppMonitor?.Join();
            }
            MonitorData = null;
        }

        void Monitor()
        {
            RunMonitor = true;
            Logger.Log($"Monitor thread: {Logger.ThreadName()} starts");
            double currentTime = DateTime.Now.ToOADate();
            double startTime = currentTime;
            //Chiller _Chiller = null;
            TemperatureController tempcontroller = null;
            Random rd = new Random(); //for simulation
            if (IsMachineRev2)
            {
                _Chiller = Chiller.GetInstance();
                tempcontroller = TemperatureController.GetInstance();
            }
            try
            {
                MonitorEntity monitorEntity = new MonitorEntity();
                while (RunMonitor)
                {
                    //reset change flags
                    ResetChanges();

                    if (IsMachineRev2)
                    {
                        currentTime = DateTime.Now.ToOADate();
                        double x = (currentTime - startTime) * 24 * 3600;
                        if (MainBoardController.IsConnected)
                        {
                            // querying FC Temper Ctrl Ramp, FC Temper Ctrl Power, FC Temper, FC heatsink temper, MB PCB Temper, FC clamp & FC door status
                            bool queryMainBoardOK = false;
                            if (MainBoardController.IsProtocolRev2)
                            {
                                queryMainBoardOK = MainBoardController.ReadRegisters(MainBoardController.Registers.ChemiTemper, 4);
                                MainBoardController.GetFluidPreHeatingTemp();
                            }
                            else
                            {
                                queryMainBoardOK = MainBoardController.ReadRegisters(MainBoardController.Registers.ChemiTemperRamp, 6);
                            }
                            if (queryMainBoardOK)
                            {
                                monitorEntity.ChemiTemperCtrlRampGet = MainBoardController.ChemiTemperRamp;
                                monitorEntity.PreHeatingTemper = MainBoardController.FluidPreHeatingCrntTemper;
                                monitorEntity.HeatSinkTemper = MainBoardController.HeatSinkTemper;
                                monitorEntity.AmbientTemper = MainBoardController.AmbientTemper;
                                monitorEntity.IsFCClamped = MainBoardController.FCClampStatus;
                                monitorEntity.IsFCDoorClosed = !MainBoardController.FCDoorStatus;
                            }
                        }

                        if (tempcontroller.IsConnected)
                        {
                            if (tempcontroller.GetTemper())
                            {
                                monitorEntity.ChemiTemperGet = TemperatureController.GetInstance().CurrentTemper;
                            }
                        }
                        if (_Chiller.IsConnected)
                        {
                            // querying Chiller Temper, Heatsink Temper, PCB Temper, Cartridge present & Cartridge Door status
                            if (_Chiller.ReadRegisters(Chiller.Registers.ChillerTemper, 4))
                            {
                                monitorEntity.CoolerTemperGet = _Chiller.ChillerTemper;
                                monitorEntity.CoolerHeatSinkTemper = _Chiller.HeatSinkTemper;
                                monitorEntity.IsCartridgePresented = !_Chiller.CartridgePresent;
                                monitorEntity.ChillerDoorClosed = !_Chiller.CartridgeDoor;
                                //CoolerTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, CoolerTemper));
                                //CoolerHeatSinkTemperLine.AppendAsync(TheDispatcher, new Point(x, HeatSinkTemper));
                            }
                        }

                        if (SampleInterval >= 0.5)
                        {
                            Thread.Sleep((int)(SampleInterval * 1000) - 150);
                        }
                        else
                        {
                            Thread.Sleep(1000);
                        }
                    } //end rev2
                    else //rev1
                    {
                        if (MainboardDevice.IsConnected)
                        {
                            if (MainboardDevice.GetOnOffStatus())
                            {
                                Thread.Sleep(10);
                                monitorEntity.IsCartridgePresented = MainboardDevice.OnOffInputs.IsCartridgeSnsrOn;
                                monitorEntity.IsDoorOpened = MainboardDevice.OnOffInputs.IsDoorOpen;
                                monitorEntity.IsFCClamped = MainboardDevice.OnOffInputs.IsFCClampSnsrOn;
                                monitorEntity.IsFCDoorClosed = MainboardDevice.OnOffInputs.IsFCSensorOn;
                                monitorEntity.IsOverflowSensorOn = MainboardDevice.OnOffInputs.IsOvflowSnsrOn;
                            }
                            if (MainboardDevice.Query(MBProtocol.Registers.ChemiTemper, 4, true))
                            {
                                Thread.Sleep(10);
                                currentTime = DateTime.Now.ToOADate();
                                double x = (currentTime - startTime) * 24 * 3600;
                                //ChemiTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, ChemiTemperGet));
                                //HeatSinkTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, HeatSinkTemper));
                                //CoolerTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, CoolerTemper));
                                //AmbientTemperLine.AppendAsync(TheDispatcher, new System.Windows.Point(x, AmbientTemper));
                            }
                            if (MainboardDevice.Query(MBProtocol.Registers.ChemiTemperCtrlRamp, true))
                            {
                                Thread.Sleep(10);
                                monitorEntity.ChemiTemperCtrlRampGet = MainboardDevice.ChemiTemperCtrlRamp;
                            }
                            if (SampleInterval >= 0.5)
                            {
                                Thread.Sleep((int)(SampleInterval * 1000) - 150);
                            }
                            else
                            {
                                Thread.Sleep(1000);
                            }
                        }
                        else
                            Thread.Sleep(10); // cuts down CPU when in simulation
                    }
                    //test
                    if (IsSimulation)
                    {
                        monitorEntity.ChemiTemperGet = rd.NextDouble() * 100;
                        //monitorEntity.AmbientTemper =  rd.NextDouble() * 100;
                        //monitorEntity.CoolerTemperGet = rd.NextDouble() * 100;
                        //monitorEntity.HeatSinkTemper = rd.NextDouble() * 100;
                        //monitorEntity.ChemiTemperGet = 10.1;
                    }

                    FluidController _FluidController = FluidController.GetInstance();
                    if (_FluidController.ReadRegisters(FluidController.Registers.OnoffInputs, 1))
                    {
                        monitorEntity.IsBufferSipperDown = _FluidController.SipperDown;
                        monitorEntity.IsBufferTrayIn = _FluidController.BufferTrayIn;
                    }
                    // 废液桶称重传感器待验证 -TBD Findy 2022/6/15
                    //if (_FluidController.ReadRegisters(FluidController.Registers.WasteStatus, 1))
                    //    monitorEntity.MassOfWaste = _FluidController.MassOfWaste;

                    lock (_MonitorDataLock)
                    {
                        MonitorData.Copy(monitorEntity);
                    }

                    if (IsChanged(ChangeFlags.TemperChange) && HasObservers(_TemperatureMonitor))
                    {
                        Send(TemperatureMonitor, new TemperatureStatus()
                        {
                            
                            ChemiTemper = MonitorData.ChemiTemperGet,
                            HeatSinkTemper = MonitorData.HeatSinkTemper,
                            PreHeatTemper = MonitorData.PreHeatingTemper,
                            CoolerTemper = MonitorData.CoolerTemperGet,
                            AmbientTemper = MonitorData.AmbientTemper,
                        });
                    }

                    UpdateTemperatureMetrics(MonitorData.GetDatas());
                }//while
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to run app monitoring with error: {ex.Message}");
            }
            Logger.Log($"Monitor thread: {Logger.ThreadName()} exits");
        }

        public TemperatureStatus TemperatureData
        {
            get
            {
                TemperatureStatus temperatureStatus = null;
                lock (_MonitorDataLock)
                {
                    if (MonitorData != null)
                    {
                        temperatureStatus = new TemperatureStatus()
                        {

                            ChemiTemper = MonitorData.ChemiTemperGet,
                            HeatSinkTemper = MonitorData.HeatSinkTemper,
                            PreHeatTemper = MonitorData.PreHeatingTemper,
                            CoolerTemper = MonitorData.CoolerTemperGet,
                            AmbientTemper = MonitorData.AmbientTemper,
                        };

                    }
                    else
                    {
                        temperatureStatus = new TemperatureStatus();
                    }
                }
                return temperatureStatus;
            }
        }


        public void CheckTemperature()
        {
            ////Logger.Log("Checking system temperatures");
            //_Chiller = Chiller.GetInstance();
            ////_MotionController = MotionController.GetInstance();
            
            //Logger.Log($"Chiller target temperature: {_Chiller.ChillerTargetTemperature}");
            
            ////_Chiller.SetCoolerTargetTemperature(0); // do not always set chiller to 0
            ////_Timer_Elapsed(null, null);
            ////_Timer = new System.Timers.Timer();
            ////_Timer.Interval = 60 * 1000;
            ////_Timer.AutoReset = true;
            ////_Timer.Elapsed += _Timer_Elapsed;
            ////_Timer.Start();
            Task.Run(() => 
            { 
                RunTempCheck();
            });
        }

        private void RunTempCheck() //_Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            
            double temp = 0;
            try
            {
                _Chiller = Chiller.GetInstance();
                Logger.Log($"Chiller target temperature: {_Chiller.ChillerTargetTemperature}");

                if (IsSimulation)
                {
                    temp = 50;
                }
                else
                {
                    temp = _Chiller.ChillerTemper;
                }
                
                Send(TemperatureMonitor, new ChillerTemperatureStatus()
                {
                    ChillerTemperature = temp,
                    IsChillerTempReady = false,
                    HasError = false,
                });

                while (true)
                {
                    // Wait sipper Home before move down
                    //int trycount = 0;
                    //int CCurrentPos = 1;
                    //do
                    //{
                    //    if (trycount++ > 2400)
                    //    {
                    //        Logger.LogError("Failed to Move Cartridge, waiting expire.");
                    //        throw new Exception("Cartridge movement failed during temperature check");
                    //    }
                    //    Thread.Sleep(50);
                    //    if (MotionController.IsCartridgeAvailable)
                    //    {
                    //        MotionController.HywireMotionController.GetMotionInfo(Hywire.MotionControl.MotorTypes.Motor_Z);
                    //        CCurrentPos = MotionController.HywireMotionController.CurrentPositions[Hywire.MotionControl.MotorTypes.Motor_Z];
                    //    }
                    //    else { _Chiller.GetChillerMotorStatus(); }
                    //    trycount++;
                    //}
                    //while ((CCurrentPos != 0 && MotionController.HywireMotionController.MotionStates[Hywire.MotionControl.MotorTypes.Motor_Z].IsBusy)
                    //|| _Chiller.CartridgeMotorStatus != Chiller.CartridgeStatusTypes.Unloaded);

                    ////Don't lower down sipper if cartridge does not present
                    //_Chiller.ReadRegisters(Chiller.Registers.OnoffInputs, 1);
                    //bool cartridgeIsUnloaded = false;
                    //if (MotionController.IsCartridgeAvailable)
                    //{
                    //    if (MotionController.CCurrentPos == 0)
                    //    {
                    //        cartridgeIsUnloaded = true;
                    //    }
                    //}
                    //else
                    //{
                    //    if (_Chiller.CartridgeMotorStatus == Chiller.CartridgeStatusTypes.Unloaded)
                    //    {
                    //        cartridgeIsUnloaded = true;
                    //    }
                    //}
                    //Logger.Log("Reposition Sipper");

                    //if (cartridgeIsUnloaded && _Chiller.CartridgePresent)
                    //{
                    //    if (!MotionController.IsCartridgeAvailable)
                    //    {
                    //        var tgtPos = SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos <
                    //            SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos ?
                    //            SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos :
                    //            SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos;
                    //        _Chiller.SetChillerMotorAbsMove(tgtPos);
                    //    }
                    //    else
                    //    {
                    //        int pos;
                    //        if (SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos < SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos)
                    //        {
                    //            pos = (int)Math.Round(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.WashCartPos *
                    //            SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                    //        }
                    //        else
                    //        {
                    //            pos = (int)Math.Round(SettingsManager.ConfigSettings.CalibrationSettings.FluidicsCalibSettings.ReagentCartPos *
                    //         SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]);
                    //        }
                    //        int speed = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Speed *
                    //            SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    //        int accel = (int)Math.Round((SettingsManager.ConfigSettings.MotionStartupSettings[MotionTypes.Cartridge].Accel *
                    //            SettingsManager.ConfigSettings.MotionFactors[MotionTypes.Cartridge]));
                    //        MotionController.AbsoluteMove(MotionTypes.Cartridge, pos, speed, accel);
                    //    }
                    //}
                    //Check chiller


                    if (IsSimulation)
                    {
                        temp -= 2;
                    }
                    else
                    {
                        temp = _Chiller.ChillerTemper;
                    }
                    if (Math.Abs(temp - 4) < 5)
                    { 
                        Send(TemperatureMonitor, new ChillerTemperatureStatus()
                        {
                            ChillerTemperature = temp,
                            IsChillerTempReady = true,
                            HasError = false,
                        });
                        Logger.Log("Temperature check is successful");
                        break;
                    }
                    else
                    {
                        Send(TemperatureMonitor, new ChillerTemperatureStatus()
                        {
                            ChillerTemperature = temp,
                            IsChillerTempReady = false,
                            HasError = false,
                        });
                        Thread.Sleep(1000);
                    }
                } //while
            }
            catch (Exception ex)
            {
                Send(TemperatureMonitor, new ChillerTemperatureStatus()
                {
                    ChillerTemperature = temp,
                    IsChillerTempReady = false,
                    HasError = true,
                });
                Logger.LogError($"Failed to check chiller temperature with an exception error: {ex.Message}");
            }
        }

        #region Metrics
        private DateTime createRobotMetricsTime = DateTime.Now;
        private SmartCsvOutputFile robotMetricsFile = null;
        private void CreateTemperatureMetrics()
        {
            try
            {
                string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string dirPath = $"{commonAppData}\\Sequlite\\Metrics\\";
                string fileName = string.Format("TemperatureMetrics.{0}.csv", DateTime.Now.ToLocalTime().ToString("yyyy.MM.dd"));
                var dataHeaders = new List<string>(){
                    "Sample Time",
                    "Ambient",
                    "Chemistry",
                    "Heat sink",
                    "Chiller CoolHeatSink",
                    "Chiller HotHeatSink",
                    "FluidPreheat",
                    "MassOfWaste"
                };

                MetricsCommon.CreateCVS(ref robotMetricsFile, dirPath, fileName, dataHeaders);
                createRobotMetricsTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Create robot metrics file error:{ex}");
            }
        }

        private void UpdateTemperatureMetrics(List<object> datas)
        {
            try
            {
                if (DateTime.Now.Day - createRobotMetricsTime.Day > 0)
                {
                    CloseTemperatureMetrics();
                    CreateTemperatureMetrics();
                }

                MetricsCommon.UpdateCSVFile(ref robotMetricsFile, datas);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Update robot metrics file error:{ex}");
            }
        }

        private void CloseTemperatureMetrics()
        {
            try
            {
                MetricsCommon.closeCSV(ref robotMetricsFile);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Close robot metrics file error:{ex}");
            }
            finally
            {
                robotMetricsFile = null;
            }
        }
        #endregion Metrics
    }
}
