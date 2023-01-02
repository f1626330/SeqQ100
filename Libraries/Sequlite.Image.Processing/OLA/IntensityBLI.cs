using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace Sequlite.Image.Processing
{
    class IntensityBLI
    {
        static ulong Params_per_cluster = 3; // Signal, Background, SNR
        string _filename = "";
        string _csvFilename = "";
        bool _outputCSV = false;
        int _ver = 0;
        float _err = 0;
        UInt64 _numClusters = 0;
        UInt64 _fov = 0;
        float[] _flt = null;
        IntensityBLI added = null;
        public IntensityBLI(string fileName, bool outputCSV=false)
        {
            _filename = fileName + ".bli";
            _csvFilename = fileName + ".csv";
            _outputCSV = outputCSV;

            bool readFailed = true;
            int retries = 0;
            while (readFailed && retries < 5)
            {
                try
                {
                    ReadFile();
                    readFailed = false;
                }
                catch (Exception )
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
        public bool OutputCSV { get => _outputCSV; set => _outputCSV = value; }

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
                    _flt = new float[_numClusters * _fov * Params_per_cluster];
                    for (ulong i = 0; i < (ulong)_numClusters * _fov * Params_per_cluster; i++)
                    {
                        // Read a 4-byte floating point value from the current stream and advance the current position of the stream by 4 bytes.
                        _flt[i] = sr.ReadSingle();
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
            if (_outputCSV)
                UpdateCSVFile();
            return true; // all good
        }

        public bool AppendBLI(IntensityBLI anotherBLI)
        {
            if (NumClusters != anotherBLI.NumClusters)
                throw new Exception($"AppendBLI failed. 1st bli: {NumClusters}, 2nd bli: {anotherBLI.NumClusters} ");

            Fov += anotherBLI.Fov;

            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Open)))
            {
                sr.Seek(0, SeekOrigin.Begin);
                sr.Write(Ver);
                sr.Write(Err);
                sr.Write(NumClusters);
                sr.Write(Fov);

                // write new values at the end of the file
                sr.Seek(0, SeekOrigin.End);
                foreach (float item in anotherBLI.Flt)
                    sr.Write(item);
            }

            if (OutputCSV)
                AppendCSV(anotherBLI);

            return true; // all good
        }

        public void AppendCSV(IntensityBLI anotherBLI)
        {
            Debug.Assert(NumClusters == anotherBLI.NumClusters);
            ulong floats_per_fov = NumClusters * Params_per_cluster;

            using (StreamWriter sr = new StreamWriter(_csvFilename, true))
            {
                UInt64 fi;
                for (UInt64 fj = 0; fj < anotherBLI.Fov; fj++)
                {
                    // This object Fov already includes anotherBLI.Fov, so fi is a zero-based index of the fov being appended 
                    fi = Fov - fj; 

                    StringBuilder line = new StringBuilder();

                    // Write color
                    switch (fi % 4)
                    {
                        case 0:
                            line.Append($"A{fi / 4 + 1}");
                            break;
                        case 1:
                            line.Append($"T{fi / 4 + 1}");
                            break;
                        case 2:
                            line.Append($"G{fi / 4 + 1}");
                            break;
                        case 3:
                            line.Append($"C{fi / 4 + 1}");
                            break;
                    }

                    // Write "signal,background,SNR" for each cluster in the fj fov
                    for (ulong fk=0; fk < floats_per_fov; fk++)
                        line.Append($",{anotherBLI.Flt[fj * floats_per_fov + fk]}");
                    sr.WriteLine(line);
                }
            }
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
                //sr.Seek((int)(_numClusters * (_fov-1) * Params_per_cluster), SeekOrigin.Current);

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
            // memory-mapped implementation
            ulong offset = sizeof(int) + sizeof(float) + sizeof(UInt64) + sizeof(UInt64) 
                + (sizeof(float) * _numClusters * (_fov - 1) * Params_per_cluster); //eveything but the added floats
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

        public void WriteFile(string fileName)
        {
            _filename = fileName + ".bli";
            using (BinaryWriter sr = new BinaryWriter(File.Open(_filename, FileMode.Create)))
            {
                sr.Write(_ver);
                sr.Write(_err);
                sr.Write(_numClusters);
                sr.Write(_fov);
                //foreach (float item in _flt)
                for (ulong idx = 0; idx < _numClusters * _fov * Params_per_cluster; idx++)
                    sr.Write(Flt[idx]);
            }

            if (_outputCSV)
            {
                _csvFilename = fileName + ".csv";
                WriteCSVFile();
            }
        }

        public void WriteCSVFile()
        {
            using (StreamWriter sr = new StreamWriter(_csvFilename, true))
            {
                StringBuilder line = new StringBuilder();

                // Write csv header
                for (ulong i = 0; i < _numClusters; i++)
                    line.Append($",Signal{i},Bgnd{i},SNR{i}");
                sr.WriteLine(line);

                // For each FOV, write signal, background and SNR for each cluster
                ulong start = 0;
                ulong end = 0;
                for (ulong j = 0; j < _fov; j++)
                {
                    line.Clear();
                    switch (j % 4)
                    {
                        case 0:
                            line.Append($"A{j / 4 + 1}");
                            break;
                        case 1:
                            line.Append($"T{j / 4 + 1}");
                            break;
                        case 2:
                            line.Append($"G{j / 4 + 1}");
                            break;
                        case 3:
                            line.Append($"C{j / 4 + 1}");
                            break;
                    }
                    start = end;
                    end = start + _numClusters * Params_per_cluster;
                    for (ulong idx = start; idx < end; idx++)
                        line.Append($",{Flt[idx]}");
                    sr.WriteLine(line);
                }
            }
        }

        // Append a single FOV to the file
        public void UpdateCSVFile()
        {
            using (StreamWriter sr = new StreamWriter(_csvFilename, true))
            {
                StringBuilder line = new StringBuilder();

                // Write color and cycle
                UInt64 fi = _fov - 1; // fi is a zero-based index of the fov being added 
                switch (fi % 4)
                {
                    case 0:
                        line.Append($"A{fi / 4 + 1}");
                        break;
                    case 1:
                        line.Append($"T{fi / 4 + 1}");
                        break;
                    case 2:
                        line.Append($"G{fi / 4 + 1}");
                        break;
                    case 3:
                        line.Append($"C{fi / 4 + 1}");
                        break;
                }

                // Write signal, background and SNR for each cluster
                for (ulong idx = 0; idx < _numClusters * Params_per_cluster; idx++)
                    line.Append($",{added.Flt[idx]}");
                sr.WriteLine(line);
            }
        }

        internal int Compare(IntensityBLI fl0, ulong offset = 0)
        {
            int numErrors = 0;
            ulong start = offset * fl0._fov * Math.Min(_numClusters, fl0._numClusters) * Params_per_cluster;
            ulong stop = start + fl0._fov * Math.Min(_numClusters, fl0._numClusters) * Params_per_cluster;
            for (ulong i = start; i < stop; i++)
            {
                if (_flt[i] != fl0._flt[i - start])
                    numErrors++;
            }
            return numErrors;
        }
    }
}
