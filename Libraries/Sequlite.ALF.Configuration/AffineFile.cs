using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sequlite.ALF.RIPP
{
    class AffineFile
    {
        string _filename = "";
        int _ver = 0;
        float _err = 0;
        ulong _N = 0UL;
        float[] _flt = null;

        public AffineFile(string name)
        {
            _filename = name;
            ReadFile();
        }

        public float[] Flt { get => _flt; set => _flt = value; }
        public ulong N { get => _N; set => _N = value; }
        public float Err { get => _err; set => _err = value; }
        public int Ver { get => _ver; set => _ver = value; }

        private void ReadFile()
        {
            if (!File.Exists(_filename))
                return;
            using (BinaryReader sr = new BinaryReader(File.Open(_filename, FileMode.Open)))
            {
                _ver = sr.ReadInt32();
                _err = sr.ReadSingle();
                _N = sr.ReadUInt64();
                _flt = new float[_N];
                for (ulong i = 0; i < _N; i++)
                {
                    _flt[i] = sr.ReadSingle();
                }
            }
        }

        public void WriteFile(string fileName)
        {
            _filename = fileName;
            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Create)))
            {
                sr.Write(_ver);
                sr.Write(_err);
                sr.Write(_N);
                for (ulong i = 0; i < _N; i++)
                    sr.Write(_flt[i]);
            }
        }

        public bool add(AffineFile newFov)
        {
            _N += newFov._N;
            UpdateFile(newFov);
            return true; // all good
        }

        public void UpdateFile(AffineFile af)
        {
            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Open)))
            {
                sr.Seek(0, SeekOrigin.Begin);
                sr.Write(_ver);
                sr.Write(_err);
                sr.Write(_N);
                //sr.Seek((int)(_numClusters * (_fov-1) * 3), SeekOrigin.Current);
                sr.Seek(0, SeekOrigin.End);
                //foreach (float item in _flt)
                //    sr.Write(item);
                for (ulong i = 0; i < af._N; i++)
                    sr.Write(af._flt[i]);
                //sr.Write((byte*)FirstResult, 0, (int)added._numClusters * sizeof(float));
            }
        }

        internal int Compare(AffineFile cmp)
        {
            int numErrors = 0;
            if (_N != cmp._N)
                return (int)_N;
            for (ulong i = 0; i < _N; i++)
            {
                if (_flt[i] != cmp._flt[i])
                    numErrors++;
            }
            return numErrors;
        }
    }
}
