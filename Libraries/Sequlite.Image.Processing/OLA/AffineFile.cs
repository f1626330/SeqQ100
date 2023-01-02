using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sequlite.Image.Processing
{
    class AffineFile
    {
        static ulong Params_per_fov = 10;
        string _filename = "";
        string _csvFilename = "";
        bool _outputCSV = false;
        int _ver = 0;
        float _err = 0;
        ulong _N = 0UL;
        float[] _flt = null;

        public AffineFile(string fileName, bool outputCSV=false)
        {
            _filename = fileName + ".bla";
            _csvFilename = fileName + ".csv";
            _outputCSV = outputCSV;

            ReadFile();
        }

        public float[] Flt { get => _flt; set => _flt = value; }
        public ulong N { get => _N; set => _N = value; }
        public float Err { get => _err; set => _err = value; }
        public int Ver { get => _ver; set => _ver = value; }
        public bool OutputCSV { get => _outputCSV; set => _outputCSV = value; }

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
            _filename = fileName + ".bla";

            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Create)))
            {
                sr.Write(_ver);
                sr.Write(_err);
                sr.Write(_N);
                for (ulong i = 0; i < _N; i++)
                    sr.Write(_flt[i]);
            }

            if (_outputCSV)
            {
                _csvFilename = fileName + ".csv";
                WriteCSVFile();
            }
        }

        public bool add(AffineFile newFov)
        {
            _N += newFov._N;
            UpdateFile(newFov);
            if (_outputCSV)
                UpdateCSVFile(newFov);
            return true; // all good
        }
        public bool AppendBLA(AffineFile anotherBLA)
        {
            N += anotherBLA.N;

            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Open)))
            {
                sr.Seek(0, SeekOrigin.Begin);
                sr.Write(_ver);
                sr.Write(_err);
                sr.Write(_N);
                sr.Seek(0, SeekOrigin.End);

                for (ulong i = 0; i < anotherBLA.N; i++)
                    sr.Write(anotherBLA.Flt[i]);
            }

            if (OutputCSV)
                AppendCSV(anotherBLA);

            return true; // all good
        }

        public void AppendCSV(AffineFile anotherBLA)
        {
            using (StreamWriter sr = new StreamWriter(_csvFilename, true))
            {
                StringBuilder line = new StringBuilder();

                // For each FOV, write Params_per_fov parameters
                ulong j = 0;
                while (j < anotherBLA.N)
                {
                    line.Append($"{anotherBLA.Flt[j]},");

                    j++;
                    if (j % Params_per_fov == 0)
                    {
                        string str_line = line.ToString();
                        str_line.TrimEnd(',');
                        sr.WriteLine(str_line);
                        line.Clear();
                    }
                }
            }
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

        public void WriteCSVFile()
        {
            using (StreamWriter sr = new StreamWriter(_csvFilename, true))
            {
                StringBuilder line = new StringBuilder();

                // For each FOV, write Params_per_fov parameters
                ulong j = 0;
                line.Clear();
                while (j < N)
                {
                    line.Append($"{_flt[j]},");
                    j++;
                    if (j%Params_per_fov == 0)
                    {
                        string str_line = line.ToString();
                        str_line.TrimEnd(',');
                        sr.WriteLine(str_line);
                        line.Clear();
                    }
                }
            }
        }

        // Append a single FOV to the file
        public void UpdateCSVFile(AffineFile af)
        {
            using (StreamWriter sr = new StreamWriter(_csvFilename, true))
            {
                StringBuilder line = new StringBuilder();

                // For each FOV, write Params_per_fov parameters
                ulong j = 0;
                line.Clear();
                while (j < af._N)
                {
                    line.Append($"{af._flt[j]},");

                    j++;
                    if (j % Params_per_fov == 0)
                    {
                        string str_line = line.ToString();
                        str_line.TrimEnd(',');
                        sr.WriteLine(str_line);
                        line.Clear();
                    }
                }
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
