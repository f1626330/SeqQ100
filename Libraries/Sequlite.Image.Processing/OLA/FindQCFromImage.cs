using Sequlite.ALF.Common;
using System;
using System.IO;
using System.Text;

namespace Sequlite.Image.Processing
{
    public class FindQCFromImage
    {
        #region Fields
        String[] _qcmeasures = {"Number of features", "Fraction of good features",
            "SNR Mean", "Intensity Mean", "Background Mean",
            "Noise Mean", "Image Focus Mean", "Relative Intensity Mean"};
        float[] _qcValues = new float[6];
        string _args = "-r 1 -t 0.5 -f \"0.00001 1 1 0\" -G \"5 5 1\" -S 1 -F 1 -j \"30 30 30 30\" -O 1 -n 0 -Q 5";
        string _binDir;
        string _fileName;
        private string _FolderStr;
        private string _FileName;
        private static object _SummaryLock = new object();
        public string FileName
        {
            get { return _fileName; }
            set { _fileName = value; }
        }
        public string Args
        {
            get { return _args; }
            set { _args = value; }
        }
        public float[] QcValues
        {
            get { return _qcValues; }
            set { _qcValues = value; }
        }
        public string BinDir
        {
            get { return _binDir; }
            set { _binDir = value; }
        }
        public string[] QCmeasures
        {
            get { return _qcmeasures; }
        }
        #endregion Fields
        public FindQCFromImage(string imageFile = "", string binDir = "")
        {
            if (imageFile.Length > 0)
            {
                StringBuilder folderBuilder = new StringBuilder();
                folderBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                folderBuilder.Append("\\Sequlite\\ALF\\Recipe\\Images\\ImageSummary\\");
                _FolderStr = folderBuilder.ToString();
                if (!Directory.Exists(_FolderStr))
                {
                    Directory.CreateDirectory(_FolderStr);
                }
                _FileName = _FolderStr + DateTime.Now.ToString("yyyyMMdd")+".csv";
                RecipeRunSettings RecipeRunConfig = SettingsManager.ConfigSettings.SystemConfig.RecipeRunConfig;
                _binDir = RecipeRunConfig.GetOLABinDir("");
                _fileName = imageFile;
                FileInfo fi = new FileInfo(imageFile);
                if (fi.Exists)
                    Process(fi.FullName);
            }
        }

        public void Process(string imageFileName)
        {
            if (File.Exists(_binDir + "\\" + "FindBlobs-static.exe "))
            {
                _fileName = imageFileName;
                FileInfo fullName = new FileInfo(FileName);
                string cmd = "FindBlobs-static.exe " + _args + " " + fullName.FullName;
                DirectoryInfo di = new DirectoryInfo(_binDir);
                ExecuteCommandSync(cmd, di);
                QcValues = ProcessFile(fullName);
            }
        }

        private float[] ProcessFile(FileInfo fi)
        {
            FileInfo qc = new FileInfo(Path.Combine(fi.DirectoryName, fi.Name.Replace(".tif", "-qc.csv")));
            float[] values = new float[_qcmeasures.Length];
            if (File.Exists(@qc.FullName))
            {
                StreamReader sr = new StreamReader(qc.FullName);
                while (!sr.EndOfStream)
                {
                    string header = sr.ReadLine();
                    string data = sr.ReadLine();
                    string[] headers = header.Split(',');
                    string[] items = data.Split(',');
                    lock (_SummaryLock)
                    {
                        if (!File.Exists(_FileName))
                        {
                            FileStream aFile = new FileStream(_FileName, FileMode.Create);
                            StreamWriter sw = new StreamWriter(aFile, Encoding.Default);
                            sw.WriteLine(header);
                            sw.WriteLine(data);
                            sw.Close();
                        }
                        else
                        {
                            File.AppendAllText(_FileName, data+"\n");
                        }
                    }
                    //if (!File.Exists(_FileName))
                    //{
                    //    FileStream aFile = new FileStream(_FileName, FileMode.Create);
                    //    StreamWriter sw = new StreamWriter(aFile, Encoding.Default);
                    //    sw.WriteLine(header);
                    //    sw.WriteLine(data);
                    //    sw.Close();
                    //}
                    //else
                    //{
                    //    File.AppendAllText(_FileName, data+"\n");
                    //}
                    int index = 0;
                    foreach (string item in _qcmeasures)
                    {
                        for (int i = 0; i < headers.Length; i++)
                        {
                            if (headers[i].Contains(item))
                            {
                                if (items[i] != "nan")
                                {
                                    values[index] = Convert.ToSingle(items[i]);
                                    index++;
                                    break;
                                }
                                else
                                {
                                    values[index] = 0;
                                    index++;
                                    break;
                                }

                            }
                        }
                    }
                }
                sr.Close();
                //qc.Delete();
                FileInfo log = new FileInfo(Path.Combine(fi.DirectoryName, fi.Name.Replace(".tif", ".log")));
                log.Delete();
                log = new FileInfo(Path.Combine(fi.DirectoryName, fi.Name.Replace(".tif", "_000000-loc.csv")));
                log.Delete();
                return values;
            }
            else
            {
                return values;
            }
        }

        public static void ExecuteCommandSync(string command, DirectoryInfo workingDir)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo =
                    new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                procStartInfo.WorkingDirectory = workingDir.FullName;
                // Now we create a process, assign its ProcessStartInfo and start it
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                // Get the output into a string
                string result = proc.StandardOutput.ReadToEnd();
                // Display the command output.
                //Log(result);
            }
            catch (Exception )
            {
                throw;
            }
        }
    }
}
