using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.FirmwareUpgrader
{
    static class YModem
    {
        public static byte SOH { get; } = 0x01;
        public static byte STX { get; } = 0x02;
        public static byte EOT { get; } = 0x04;
        public static byte ACK { get; } = 0x06;
        public static byte NAK { get; } = 0x15;
        public static byte CAN { get; } = 0x18;

        public static string ResponseCharacter { get; set; }


        public static bool Transmit(SerialPort port, string fileName, byte[] data)
        {
            bool result = false;
            byte[] rxBuf;
            byte[] txBuf;
            byte pkgCnt = 0;

            try
            {
                // step1: receiving 'C' from device
                int tryCounts = 0;
                while(port.BytesToRead == 0)
                {
                    tryCounts++;
                    if (tryCounts > 5)
                    {
                        throw new Exception("Y modem Communication timeout, please confirm that the device is in upgrader loader");
                    }
                    Thread.Sleep(500);
                }
                int len = port.BytesToRead;
                rxBuf = new byte[len];
                port.Read(rxBuf, 0, len);
                if(rxBuf[len-1] != (byte)(ResponseCharacter.ToCharArray()[0]))    // not 'C', return false
                {
                    return false;
                }
                // step2: send SOH package
                byte[] nameBytes = Encoding.ASCII.GetBytes(fileName);
                byte[] sizeBytes = Encoding.ASCII.GetBytes(Convert.ToString(data.Length, 10));
                txBuf = new byte[133];
                txBuf[0] = SOH;
                txBuf[1] = pkgCnt;
                txBuf[2] = (byte)(0xff - pkgCnt);
                pkgCnt++;
                Buffer.BlockCopy(nameBytes, 0, txBuf, 3, nameBytes.Length);
                Buffer.BlockCopy(sizeBytes, 0, txBuf, 4 + nameBytes.Length, sizeBytes.Length);
                var crc = Cal_CRC16(txBuf, 3, 128);
                txBuf[131] = (byte)(crc >> 8);
                txBuf[132] = (byte)(crc & 0x00ff);
                bool msgRcvd = false;
                do
                {
                    port.Write(txBuf, 0, 133);
                    try
                    {
                        string rspns = port.ReadTo(ResponseCharacter);
                        byte[] rspnsBytes = Encoding.ASCII.GetBytes(rspns);
                        if (rspns[0] == ACK)
                        {
                            msgRcvd = true;
                        }
                    }
                    catch (TimeoutException)
                    {
                        return false;
                    }
                }
                while (!msgRcvd);
                // step3: start transmission of data
                int loops = data.Length / 1024;
                int remains = data.Length % 1024;
                txBuf = new byte[1029];
                txBuf[0] = STX;
                int i = 0;
                for(i = 0; i < loops; i++)
                {
                    txBuf[1] = pkgCnt;
                    txBuf[2] = (byte)(0xff - pkgCnt);
                    pkgCnt++;
                    Buffer.BlockCopy(data, i * 1024, txBuf, 3, 1024);
                    crc = Cal_CRC16(txBuf, 3, 1024);
                    txBuf[1027] = (byte)(crc >> 8);
                    txBuf[1028] = (byte)(crc & 0x00ff);
                    msgRcvd = false;
                    do
                    {
                        port.Write(txBuf, 0, 1029);
                        try
                        {
                            int tryCnts = 0;
                            while (port.BytesToRead == 0)
                            {
                                if (++tryCnts > 5000)
                                {
                                    return false;
                                }
                                Thread.Sleep(1);
                            }
                            port.Read(rxBuf, 0, 1);
                            if (rxBuf[0] == ACK)
                            {
                                msgRcvd = true;
                            }
                        }
                        catch (TimeoutException)
                        {
                            return false;
                        }
                    }
                    while (!msgRcvd);
                }
                int lastPkgLen = 128;
                if(remains > 128)
                {
                    lastPkgLen = 1024;
                }
                txBuf = new byte[lastPkgLen + 5];
                if(lastPkgLen == 128)
                {
                    txBuf[0] = SOH;
                }
                else
                {
                    txBuf[0] = STX;
                }
                txBuf[1] = pkgCnt;
                txBuf[2] = (byte)(0xff - pkgCnt);
                pkgCnt++;
                Buffer.BlockCopy(data, i * 1024, txBuf, 3, remains);
                crc = Cal_CRC16(txBuf, 3, lastPkgLen);
                txBuf[lastPkgLen+3] = (byte)(crc >> 8);
                txBuf[lastPkgLen+4] = (byte)(crc & 0x00ff);
                msgRcvd = false;
                do
                {
                    port.Write(txBuf, 0, lastPkgLen+5);
                    try
                    {
                        int tryCnts = 0;
                        while (port.BytesToRead == 0)
                        {
                            if (++tryCnts > 5000)
                            {
                                return false;
                            }
                            Thread.Sleep(1);
                        }
                        port.Read(rxBuf, 0, 1);
                        if (rxBuf[0] == ACK)
                        {
                            msgRcvd = true;
                        }
                    }
                    catch (TimeoutException)
                    {
                        return false;
                    }
                }
                while (!msgRcvd);
                // Step4: Send EOT
                txBuf[0] = EOT;
                for(int j = 0; j < 2; j++)
                {
                    msgRcvd = false;
                    do
                    {
                        port.Write(txBuf, 0, 1);
                        try
                        {
                            int tryCnts = 0;
                            while (port.BytesToRead == 0)
                            {
                                if (++tryCnts > 5000)
                                {
                                    return false;
                                }
                                Thread.Sleep(1);
                            }
                            port.Read(rxBuf, 0, 1);
                            if (rxBuf[0] == NAK || rxBuf[0] == ACK)
                            {
                                msgRcvd = true;
                            }
                        }
                        catch (TimeoutException)
                        {
                            return false;
                        }
                    }
                    while (!msgRcvd);
                }
                // Step5: Send End package
                txBuf = new byte[133];
                txBuf[0] = SOH;
                txBuf[1] = 0x00;
                txBuf[2] = 0xff;
                crc = Cal_CRC16(txBuf, 3, 128);
                txBuf[131] = (byte)(crc >> 8);
                txBuf[132] = (byte)(crc & 0x00ff);
                msgRcvd = false;
                do
                {
                    port.Write(txBuf, 0, 133);
                    try
                    {
                        int tryCnts = 0;
                        while (port.BytesToRead == 0)
                        {
                            if (++tryCnts > 5000)
                            {
                                return false;
                            }
                            Thread.Sleep(1);
                        }
                        port.Read(rxBuf, 0, 1);
                        if (rxBuf[0] == ACK)
                        {
                            msgRcvd = true;
                        }
                    }
                    catch (TimeoutException)
                    {
                        return false;
                    }
                }
                while (!msgRcvd);
                result = true;
            }
            catch
            {
                result = false;
            }
            return result;
        }


        private static ushort Cal_CRC16(byte[] data, int offset, int len)
        {
            uint crc = 0;
            for (int i = offset; i < len + offset; i++)
            {
                crc = UpdateCRC16(crc, data[i]);
            }

            crc = UpdateCRC16(crc, 0);
            crc = UpdateCRC16(crc, 0);
            return (ushort)crc;
        }

        private static ushort UpdateCRC16(uint crcIn, byte dat)
        {
            uint crc = crcIn;
            uint input = (uint)(dat | 0x100);
            do
            {
                crc <<= 1;
                input <<= 1;
                if ((input & 0x100) == 0x100)
                {
                    ++crc;
                }
                if ((crc & 0x10000) == 0x10000)
                {
                    crc ^= 0x1021;
                }
            }
            while (!((input & 0x10000) == 0x10000));
            return (ushort)crc;
        }

    }
}
