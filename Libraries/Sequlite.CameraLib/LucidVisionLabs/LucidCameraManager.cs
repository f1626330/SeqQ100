using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.CameraLib
{
    public static class LucidCameraManager
    {
        public delegate void UpdateCameraHandler();
        public static event UpdateCameraHandler OnCameraUpdated;
        #region Private Fields
        private static ArenaNET.ISystem _System;
        private static List<LucidCamera> _Cameras;
        private static bool _IsOpened;
        private static ISeqLog Logger { get; } = SeqLogFactory.GetSeqFileLog("LucidCamera Manager");
        #endregion Private Fields

        #region Open Cameras
        public static bool OpenCameras()
        {
            if (_IsOpened) { return true; }
            try
            {
                _System = ArenaNET.Arena.OpenSystem();
                _System.UpdateDevices(100);
                if (_System.Devices.Count == 0)
                {
                    ArenaNET.Arena.CloseSystem(_System);
                    _System = null;
                    return false;
                }
                _Cameras = new List<LucidCamera>();
                foreach (var item in _System.Devices)
                {
                    try
                    {
                        var device = _System.CreateDevice(item);
                        var camera = new LucidCamera(device, item.ModelName, item.SerialNumber);
                        _Cameras.Add(camera);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                        continue;
                    }
                }
                if (_Cameras.Count > 0)
                {
                    _IsOpened = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex1)
            {
                Logger.LogError(ex1.ToString());
                return false;
            }
        }

        public static bool OpenCamera(string serialNumber)
        {
            foreach (var item in _System.Devices)
            {
                try
                {
                    if(item.SerialNumber == serialNumber)
                    {
                        var device = _System.CreateDevice(item);
                        var camera = new LucidCamera(device, item.ModelName, item.SerialNumber);
                        for(int i = 0; i < _Cameras.Count; i++)
                        {
                            if(_Cameras[i].SerialNumber == serialNumber)
                            {
                                _Cameras.RemoveAt(i);
                                _Cameras.Insert(i, camera);
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                    return false;
                }
            }
            return false;
        }
        #endregion Open Cameras

        public static void CloseCameras()
        {
            if (!_IsOpened) { return; }
            try
            {
                foreach(var camera in _Cameras)
                {
                    _System.DestroyDevice(camera.Device);
                }
                ArenaNET.Arena.CloseSystem(_System);
                _IsOpened = false;
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
                return;
            }
        }

        public static void CloseCamera(LucidCamera camera)
        {
            try
            {
                _System.DestroyDevice(camera.Device);
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.ToString());
            }
        }
        public static void ReConnectCamera(string serialNumber)
        {
            foreach (var camera in _Cameras)
            {
                if(camera.SerialNumber == serialNumber)
                {
                    CloseCamera(camera);
                }
            }
            Thread.Sleep(1000);
            OpenCamera(serialNumber);
            OnCameraUpdated?.Invoke();
        }
        public static LucidCamera GetCamera(int index)
        {
            if (_Cameras == null) { return null; }
            try
            {
                return _Cameras[index];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
                return null;
            }
        }
        public static List<LucidCamera> GetAllCameras()
        {
            if (_Cameras != null)
            {
                return new List<LucidCamera>(_Cameras);
            }
            else
            {
                return new List<LucidCamera>();
            }
        }
    }
}
