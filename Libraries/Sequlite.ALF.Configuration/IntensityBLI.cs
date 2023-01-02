using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sequlite.ALF.RIPP
{
    class IntensityBLI
    {
        string _filename;
        int _ver = 0;
        float _err = 0;
        UInt64 _numClusters = 0;
        UInt64 _fov = 0;
        float[] _flt = null;
        IntensityBLI added = null;
        public IntensityBLI(string name)
        {
            _filename = name;
            bool readFailed = true;
            int retries = 0;
            while (readFailed && retries < 5)
            {
                try
                {
                    ReadFile();
                    readFailed = false;
                }
                catch (Exception ex)
                {
                    retries++; // try again
                    if (retries > 4)
                        throw;
                    Thread.Sleep(5000);
                }
            }
        }

        public int Ver { get => _ver; set => _ver = value; }
        public float Err { get => _err; set => _err = value; }
        public float[] Flt { get => _flt; set => _flt = value; }
        public ulong NumClusters { get => _numClusters; set => _numClusters = value; }
        public ulong Fov { get => _fov; set => _fov = value; }

        private bool ReadFile()
        {
            if (!File.Exists(_filename))
                return false;
            try
            {
                using (BinaryReader sr = new BinaryReader(File.Open(_filename, FileMode.Open)))
                {
                    _ver = sr.ReadInt32();
                    _err = sr.ReadSingle();
                    _numClusters = sr.ReadUInt64();
                    _fov = sr.ReadUInt64();
                    if (_fov < 20)
                    {
                        _flt = new float[_numClusters * _fov * 3];
                        for (ulong i = 0; i < (ulong)_numClusters * _fov * 3; i++)
                        {
                            _flt[i] = sr.ReadSingle();
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                throw ex;
            }
            return true;

        }

        public bool AddFOV(IntensityBLI newFov)
        {
            _fov++;
            added = newFov;
            if (added._numClusters != _numClusters)
                return false;
            UpdateFile();
            return true; // all good
        }

        private void UpdateFile()
        {
            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Open)))
            {
                sr.Seek(0, SeekOrigin.Begin);
                sr.Write(_ver);
                sr.Write(_err);
                sr.Write(_numClusters);
                sr.Write(_fov);
                //sr.Seek((int)(_numClusters * (_fov-1) * 3), SeekOrigin.Current);

                /* block copy is not fast, just plain write each float is very fast
                    var byteArray = new byte[_flt.Length * 4];
                    Buffer.BlockCopy(_flt, 0, byteArray, 0, byteArray.Length);
                    sr.Write(byteArray);
                */

                //write new values at the end of the file
                sr.Seek(0, SeekOrigin.End);
                foreach (float item in added._flt)
                    sr.Write(item);

                // since we are added to the end of the file, seemed reasonable to extend the file, but this is slow
                //sr.Seek((added._flt.Length-1) * sizeof(float), SeekOrigin.End);
                //sr.Write(added._flt[added._flt.Length - 1]); //write to last spot
            }


            /* do not use MMF
            // memmory mapped implementation
            ulong offset = sizeof(int) + sizeof(float) + sizeof(UInt64) + sizeof(UInt64) 
                + (sizeof(float) * _numClusters * (_fov - 1) * 3); //eveything but the added floats
            long length = added._flt.Length * sizeof(float);

            // Create the memory-mapped file.
            using (var mmf = MemoryMappedFile.CreateFromFile(_filename, FileMode.Open, "ImgA"))
            {
                using (var accessor = mmf.CreateViewAccessor((long)offset, length))
                {
                    accessor.WriteArray<float>(0, added._flt, 0, added._flt.Length);
                }
            }
            */
        }

        public void WriteFile(string name)
        {
            _filename = name;
            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Create)))
            {
                sr.Write(_ver);
                sr.Write(_err);
                sr.Write(_numClusters);
                sr.Write(_fov);
                //foreach (float item in _flt)
                for (ulong idx = 0; idx < _numClusters * _fov * 3; idx++)
                    sr.Write(Flt[idx]);
            }
        }

        internal int Compare(IntensityBLI fl0, ulong offset = 0)
        {
            int numErrors = 0;
            ulong start = offset * fl0._fov * Math.Min(_numClusters, fl0._numClusters) * 3;
            ulong stop = start + fl0._fov * Math.Min(_numClusters, fl0._numClusters) * 3;
            for (ulong i = start; i < stop; i++)
            {
                if (_flt[i] != fl0._flt[i - start])
                    numErrors++;
            }
            return numErrors;
        }
    }
}
