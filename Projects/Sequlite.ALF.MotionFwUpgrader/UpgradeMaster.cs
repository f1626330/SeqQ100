using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.MotionFwUpgrader
{
    class UpgradeMaster : ThreadBase
    {
        public delegate void MessageNotifyHandle(string msg);
        public event MessageNotifyHandle OnMessageNotified;
        public delegate void ProgressUpdatedHandle(int percentage);
        public event ProgressUpdatedHandle OnProgressUpdated;

        #region Private Fields
        const int TotalFlashMemorySize = 0x00200000;
        const int UserImageStartAddress = 0x000b0000;
        const int MaximumUserImageSize = TotalFlashMemorySize - UserImageStartAddress;
        const int UserImageSpaceFlashPages = MaximumUserImageSize / 256;
        const int PageSize = 256;
        const int PackageSize = 1024;
        const string BackupFilePath = "backup.bin";

        private UpgradeComm _UpgradeComm;
        private string _UpgradeInfo;
        private byte[] _UpgradeData;
        private int _FlashReadAddress;
        private int _FlashWriteAddress;
        #endregion Private Fields

        public UpgradeMaster(UpgradeComm upgradeComm, string info, byte[] data)
        {
            _UpgradeComm = upgradeComm;
            _UpgradeInfo = info;
            _UpgradeData = data;
        }

        public override void ThreadFunction()
        {
            #region extract valid image data, trim the 0xff in the end of the data
            int endIndex = _UpgradeData.Length - 2;
            for (; endIndex > 1; endIndex--)
            {
                if (_UpgradeData[endIndex] != 0xff && _UpgradeData[endIndex + 1] == 0xff)
                {
                    break;
                }
            }
            byte[] validData = new byte[endIndex + 1];
            Buffer.BlockCopy(_UpgradeData, 0, validData, 0, endIndex + 1);
            _UpgradeData = validData;
            #endregion extract valid image data, trim the 0xff in the end of the data

            #region Check Upgrade data size
            if (_UpgradeData.Length < PackageSize || _UpgradeData.Length > MaximumUserImageSize)
            {
                OnMessageNotified?.Invoke("Upgarde data is invalid, upgrade failed.\n");
                ExitStat = ThreadExitStat.Error;
                return;
            }
            #endregion Check Upgrade data size

            #region Backup previous user image
            //OnMessageNotified?.Invoke("Backup current image...");
            //OnProgressUpdated?.Invoke(0);
            //byte[] backupData = new byte[UserImageSpaceFlashPages * PageSize];
            //int readIndex = 0;
            //_FlashReadAddress = UserImageStartAddress;
            //int page = 0;
            //for (; page < UserImageSpaceFlashPages;)
            //{
            //    _UpgradeComm.UpgraderReadEpcsContent(_FlashReadAddress, PackageSize, backupData, readIndex);
            //    _FlashReadAddress += PackageSize;
            //    page += PackageSize / PageSize;
            //    readIndex += PackageSize;
            //    OnProgressUpdated?.Invoke((int)(100.0 * (page + 1) / UserImageSpaceFlashPages));
            //}
            ////reverse bits of the data
            //for (int i = 0; i < backupData.Length; i++)
            //{
            //    backupData[i] = ReverseBits(backupData[i]);
            //}
            //File.WriteAllBytes(BackupFilePath, backupData);
            //OnMessageNotified?.Invoke("OK.\n");

            byte[] backupData = new byte[512];
            _UpgradeComm.UpgraderReadEpcsContent(TotalFlashMemorySize - 512, 512, backupData, 0);
            #endregion Backup previous user image

            #region Erase user image
            OnMessageNotified?.Invoke("Erasing User Image...");
            _FlashWriteAddress = UserImageStartAddress;
            for (int sector = UserImageStartAddress / 65536; sector < 32; sector++)
            {
                _UpgradeComm.UpgraderEraseSector(_FlashWriteAddress);
                _FlashWriteAddress += 65536;
                OnProgressUpdated?.Invoke((int)(100.0 * (sector - UserImageStartAddress / 65536 - 1) / (MaximumUserImageSize / 65536)));
            }
            OnMessageNotified?.Invoke("OK.\n");
            #endregion Erase user image

            #region Write user image
            //reverse bits of the upgrading data
            for (int i = 0; i < _UpgradeData.Length; i++)
            {
                _UpgradeData[i] = ReverseBits(_UpgradeData[i]);
            }
            _FlashWriteAddress = UserImageStartAddress;
            int writeIndex = 0;
            // align program data to 1024
            int writeLoops = (_UpgradeData.Length / PackageSize);
            if (_UpgradeData.Length % PackageSize != 0)
            {
                writeLoops++;
            }
            byte[] alignedData = new byte[writeLoops * PackageSize];
            for (int i = 0; i < alignedData.Length; i++)
            {
                if (i < _UpgradeData.Length)
                {
                    alignedData[i] = _UpgradeData[i];
                }
                else
                {
                    alignedData[i] = 0xff;
                }
            }
            OnMessageNotified?.Invoke("Updating User Image...");
            for (int writeLoop = 0; writeLoop < writeLoops; writeLoop++)
            {
                _UpgradeComm.UpgraderWriteEpcsContent(_FlashWriteAddress, PackageSize, alignedData, writeIndex);
                _FlashWriteAddress += PackageSize;
                writeIndex += PackageSize;
                OnProgressUpdated?.Invoke((int)(100.0 * (writeLoop + 1) / writeLoops));
            }
            OnMessageNotified?.Invoke("OK.\n");
            #endregion Write user image

            #region Recover system info, which is the last 512 bytes of the epcs memory
            _FlashWriteAddress = TotalFlashMemorySize - 512;
            writeIndex = backupData.Length - 512;
            string upgradeDateTimeStr = string.Format("Upgrade Time: {0}", DateTime.Now.ToString());
            byte[] upgradeDateTimeBytes = Encoding.ASCII.GetBytes(upgradeDateTimeStr);
            Buffer.BlockCopy(upgradeDateTimeBytes, 0, backupData, writeIndex, upgradeDateTimeBytes.Length > 256 ? 256 : upgradeDateTimeBytes.Length);
            _UpgradeComm.UpgraderWriteEpcsContent(_FlashWriteAddress, 512, backupData, writeIndex);
            #endregion Recover system info, which is the last 512 bytes of the epcs memory

            #region Verify user image
            //OnMessageNotified?.Invoke("Verify Download\n");
            //byte[] verifyData = new byte[2 * 1024 * 1024];
            //for(int addr = 0; addr < verifyData.Length - 1024;)
            //{
            //    _UpgradeComm.UpgraderReadEpcsContent(addr, 1024, verifyData, addr);
            //    addr += 1024;
            //}
            //for (int i = 0; i < verifyData.Length; i++)
            //{
            //    verifyData[i] = ReverseBits(verifyData[i]);
            //}

            //File.WriteAllBytes("Verify.rpd", verifyData);
            #endregion Verify user image

            #region Reconfig FPGA to user image
            _UpgradeComm.UpgraderReconfigFPGA(true);
            OnMessageNotified?.Invoke("Upgrade finished.\n");
            #endregion Reconfig FPGA to user image
        }

        public override void AbortWork()
        {
        }

        public override void Finish()
        {
        }

        private byte ReverseBits(byte byteData)
        {
            byteData = (byte)((byteData << 4) | (byteData >> 4));
            byteData = (byte)(((byteData << 2) & 0xcc) | ((byteData >> 2) & 0x33));
            byteData = (byte)(((byteData << 1) & 0xaa) | ((byteData >> 1) & 0x55));
            return byteData;
        }
    }
}
