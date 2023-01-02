using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sequlite.Image.Processing
{
    /// <summary>
    /// Process Instrument images files all the way to FASTQ
    /// </summary>
    class ImageProcessingCMD
    {
        string _RN = "";
        string _FC = "";
        static int[] _FCList = { 0, 5, 10, 15, 20, 25 };
        string _BO = "";
        string _CE = "";
        string _TP = "";
        int _NC = -1;
        int _NS = -1;

        int _CD = 8;
        string _PP = "0 -0.01 0.5";
        string _CN = "1 0 1";
        static string _EO = "-S 2 -n 1 -s 1 -r 1 -t 0.5 -M 0 -T 1 -O 0 -Q 0 -F 2";
        string _TO = "-S 2 -n 0 -s 0 -r 1 -t 0.5 -e 0 -M 0 -F 2 -P 0 -K 0 -Q 0 -m 0";
        string _CO = "-S 1 -n 0 -s 0 -t 0 -c 1 -W 1 -Q 0 -k 1 -o 0 -d 1 -B 0.01 -E 0.5";
        string _JO = "-S 1";
        int _RI = 0;
        int _BC = 1;
        const string _SEQP = "pwd";
        const string _ARCH = "uname";
        const string _FT = "0.01 1 1 0";
        const string _FE = "0.01 1 1 0";
        const string _ST = "350 0";
        const string _SE = "350 4";
        const string _TT = "3 3 1";
        const string _TE = "0 0 1";
        const string _EP = "1 11 0.1 0.5";
        static DirectoryInfo _TargetDir = null;
        static object[] _lockObject = { 0, 1, 2, 3, 4, 5, 6, 7 };
        static Dictionary<string, int> CellToIndex = new Dictionary<string, int> { { "proc-0", 0 }, { "proc-5", 1 }, { "proc-10", 2 }, { "proc-15", 3 }, { "proc-20", 4 }, { "proc-25", 5 },
            { "extr", 6 }, {"calls", 7 } };
        int minTemplateCycle = 4;
        static string logFileName = "trackingCMD.txt";
        static string LogFile = "";
        bool[] _templateCreated = { false, false, false, false, false, false };
        int[] _LastCycleExtracted = { 0, 0, 0, 0, 0, 0 };
        bool[] _enableTile = { true, true, true, true, true, true };
        static bool _LogCMDLineOutput = false;
        int _baseCallMinCycle = 4;

        public string RN { get => _RN; set => _RN = value; }
        public string FC { get => _FC; set => _FC = value; }
        public static int[] FCList { get => _FCList; set => _FCList = value; }
        public string BO { get => _BO; set => _BO = value; }
        public string CE { get => _CE; set => _CE = value; }
        public string TP { get => _TP; set => _TP = value; }
        public int NC { get => _NC; set => _NC = value; }
        public int NS { get => _NS; set => _NS = value; }
        public int CycleFrames { get => NS * NC; }
        public int CD { get => _CD; set => _CD = value; }
        public string PP { get => _PP; set => _PP = value; }
        public string CN { get => _CN; set => _CN = value; }
        public static string EO { get => _EO; set => _EO = value; }
        public string TO { get => _TO; set => _TO = value; }
        public string CO { get => _CO; set => _CO = value; }
        public string JO { get => _JO; set => _JO = value; }
        public int RI { get => _RI; set => _RI = value; }
        public int BC { get => _BC; set => _BC = value; }


        static bool _DoNotExe = false;

        public static string SEQP => _SEQP;

        public static string ARCH => _ARCH;

        public static string FT => _FT;

        public static string FE => _FE;

        public static string ST => _ST;

        public static string SE => _SE;

        public static string TT => _TT;

        public static string TE => _TE;

        public static string EP => _EP;

        public static DirectoryInfo TargetDir { get => _TargetDir; set => _TargetDir = value; }
        public static bool DoNotExe { get => _DoNotExe; set => _DoNotExe = value; }
        public static string EO1 { get => _EO; set => _EO = value; }
        public string TO1 { get => _TO; set => _TO = value; }
        public string CO1 { get => _CO; set => _CO = value; }
        public string JO1 { get => _JO; set => _JO = value; }
        public int RI1 { get => _RI; set => _RI = value; }
        public int BC1 { get => _BC; set => _BC = value; }
        public bool[] TemplateCreated { get => _templateCreated; set => _templateCreated = value; }
        public int BaseCallMinCycle { get => _baseCallMinCycle; set => _baseCallMinCycle = value; }

        public void PostProcess()
        {
            DirectoryInfo di = ImageProcessingCMD.TargetDir;
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (sd.Name.Contains("proc"))
                {
                    PostProcessByCell(sd);
                }
            }
        }

        public void ResetTemplateCreate()
        {
            for (int i = 0; i < _templateCreated.Length; i++)
                _templateCreated[i] = false;
        }

        private void PostProcessByCell(DirectoryInfo sd)
        {
            string newName = sd.FullName.Replace("proc", "done");
            //sd.MoveTo(newName);
            try
            {
                Directory.Move(sd.FullName, newName);
            }
            catch (Exception ex)
            {
                Log(ex.ToString(), sd);
            }
        }

        private void Log(string msg, DirectoryInfo di = null)
        {
            string msgOut = DateTime.Now.ToLongTimeString() + ":" + Thread.CurrentThread.Name + ")" + msg + "\r\n";
            LogToFile(msg, di);
        }

        public void BaseCall()
        {
            DirectoryInfo di = ImageProcessingCMD.TargetDir;
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (sd.Name.Contains("proc"))
                {
                    BaseCallByCell(sd);
                }
            }
        }

        private void BaseCallByCell(DirectoryInfo sd, int cycle = 0)
        {
            DirectoryInfo calls = new DirectoryInfo(System.IO.Path.Combine(sd.FullName, "calls"));
            FileInfo proctxt = new FileInfo(System.IO.Path.Combine(calls.FullName, "proc.txt"));
            StringBuilder sb = new StringBuilder();
            if (cycle == 0)
                cycle = NS;
            //CreateProcTxt(1, cycle, calls, false);

            sb.Append(System.IO.Path.Combine(TargetDir.FullName, "bin", "BaseCall-static.exe"));
            sb.Append(" " + CO);
            sb.Append(" -i proc-int.bli");
            sb.Append(" -L ../tmplt/proc-loc.blb");
            sb.Append(" -g \"" + PP + "\"");
            sb.Append(" -q \"" + CN + "\"");
            sb.Append(" -b \"" + BO + "\"");
            sb.Append(" -H " + NC);
            sb.Append(" -D " + CD);
            sb.Append(" proc.txt");
            //sb.Append(" " + proctxt.FullName);
            ExecuteCommandSync(sb.ToString(), calls);

            ExecuteCommandSync("move proc-int.log ..//logs", calls);
            // wait for now RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".post");
        }

        internal void LoadRunSHParameters()
        {
            FileInfo fi = new FileInfo(System.IO.Path.Combine(_TargetDir.FullName, "run.sh"));
            if (!fi.Exists)
            {
                Log("file not found: " + fi.FullName);
                return;
            }
            using (StreamReader sr = new StreamReader(fi.FullName))
            {
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (line.Contains("export RN="))
                        _RN = line.Substring(line.IndexOf("=") + 1);
                    else if (line.Contains("export NS="))
                        _NS = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1));
                    else if (line.Contains("export BO="))
                        _BO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export CE="))
                        _CE = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export TP="))
                        _TP = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export NC="))
                        _NC = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));
                    else if (line.Contains("export CD="))
                        _CD = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));
                    else if (line.Contains("export PP="))
                        _PP = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export CN="))
                        _CN = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export EO="))
                        _EO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export TO="))
                        _TO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export CO="))
                        _CO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export JO="))
                        _JO = line.Substring(line.IndexOf("=") + 1).Replace("\"", "");
                    else if (line.Contains("export RI="))
                        _RI = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));
                    else if (line.Contains("export BC="))
                        _BC = Convert.ToInt32(line.Substring(line.IndexOf("=") + 1).Replace("\"", ""));

                }
            }
        }

        public void JoinCycles()
        {
            DirectoryInfo di = TargetDir;
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (sd.Name.Contains("proc"))
                {
                    JoinCyclesByCell(sd);
                }
            }

        }

        private void JoinCyclesByCell(DirectoryInfo sd, int cycle = 0)
        {
            DirectoryInfo extr = new DirectoryInfo(System.IO.Path.Combine(sd.FullName, "extr"));

            StringBuilder sb = new StringBuilder();
            if (cycle == 0)
                cycle = NS;
            //CreateProcTxt(1, cycle, extr, false);

            sb.Append(System.IO.Path.Combine(TargetDir.FullName, "bin", "JoinCycles-static.exe"));
            sb.Append(" " + JO);
            sb.Append(" -H \"" + NC + "\"");
            sb.Append(" -b \"" + BO + "\"");
            sb.Append(" proc.txt");
            ExecuteCommandSync(sb.ToString(), extr);

            ExecuteCommandSync(@"move proc-int.* ../calls", extr);
            ExecuteCommandSync(@"move proc-at.* ../at", extr);
            // for short time RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".baseCall");
        }

        public void WriteToParams(DirectoryInfo target = null)
        {
            if (target != null)
                Log("Write Param " + target.FullName);
            else
                Log("Write Param all");
            DirectoryInfo di = TargetDir;
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (target != null)
                {
                    if (sd.Name != target.Name)
                        continue;
                }
                if (sd.Name.Contains("proc"))
                {

                    DirectoryInfo paramDir = new DirectoryInfo(System.IO.Path.Combine(sd.FullName, "params"));
                    if (!paramDir.Exists)
                        paramDir.Create();
                    string contents = sd.Name.Substring(sd.Name.IndexOf('-') + 1);
                    WriteParams(paramDir, "flow-cell", contents);
                    WriteParams(paramDir, "base-order", BO);
                    WriteParams(paramDir, "color-ext", CE);
                    WriteParams(paramDir, "num-stages", NS.ToString());
                    WriteParams(paramDir, "num-colors", NC.ToString());
                    WriteParams(paramDir, "run-name", RN);
                    WriteParams(paramDir, "tmplt-par", TP);
                    WriteParams(paramDir, "filter-tmplt", FT);
                    WriteParams(paramDir, "filter-extr", FE);
                    WriteParams(paramDir, "shift-tmplt", ST);
                    WriteParams(paramDir, "shift-extr", SE);
                    WriteParams(paramDir, "tile-tmplt", TT);
                    WriteParams(paramDir, "tile-extr", TE);
                    WriteParams(paramDir, "extr-par", EP);
                    WriteParams(paramDir, "extr-opts", EO);
                    WriteParams(paramDir, "rm-int", RI.ToString());
                    WriteParams(paramDir, "tmplt-opts", TO);
                    WriteParams(paramDir, "jc-opts", JO);
                    WriteParams(paramDir, "cluster-dim", CD.ToString());
                    WriteParams(paramDir, "phasing-par", PP);
                    WriteParams(paramDir, "cluster-norm", CN);
                    WriteParams(paramDir, "call-opts", CO);
                    WriteParams(paramDir, "base-call", BC.ToString());


                }
            }
        }

        public void ExtractIntensities()
        {
            DirectoryInfo di = TargetDir;
            DirectoryInfo dataDir = new DirectoryInfo(System.IO.Path.Combine(TargetDir.FullName, "Data"));
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (sd.Name.Contains("proc"))
                {
                    // maybe we should only call this once
                    ExtractIntensitiesByCell(sd, minTemplateCycle, minTemplateCycle);

                }
            }
        }

        private void ExtractIntensitiesByCell(DirectoryInfo sd, int cycleStart, int cycleStop)
        {
            Thread[] newThread = new Thread[CycleFrames];
            for (int num = (cycleStart - 1) * 4; num < cycleStop * 4; num++)
            {
                /*float cpu = 100;
                do
                {
                    cpu = getCPUCounter();
                } while (cpu > 90);
                */

                newThread[num] = new Thread(ExtractSingleIntensity);
                newThread[num].Name = sd.Name + ":" + num.ToString();
                DirectoryNumber dn = new DirectoryNumber(num, sd);
                newThread[num].Start(dn);
                Thread.Sleep(10); // start process
            }

            for (int num = 0; num < CycleFrames; num++)
            {
                if (newThread[num] != null)
                    newThread[num].Join();
            }
            //PostProcessingForExtractIntensities(sd); dont do this here
        }

        private void PostProcessingForExtractIntensities(DirectoryInfo sd)
        {
            ExecuteCommandSync("move *.log logs", sd);
            ExecuteCommandSync("move *-loc.* locs", sd);
            ExecuteCommandSync("move *-int.* extr", sd);
            ExecuteCommandSync("move *-at.* extr", sd);
            ExecuteCommandSync("copy proc.txt extr", sd);
            ExecuteCommandSync("move *-qc.* qc", sd);
            //hold on this RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".joinCycles");
        }

        private static void ExtractSingleIntensity(object item)
        {
            DirectoryNumber dn = (DirectoryNumber)item;
            DirectoryInfo di = TargetDir;
            StringBuilder sb = new StringBuilder();

            //LogToFile("Extracting in " + TargetDir);

            sb.Append(System.IO.Path.Combine(di.FullName, "bin", "ExtractInt-static.exe"));
            sb.Append(" " + EO);
            sb.Append(" -e \"" + EP + "\"");
            sb.Append(" -a \"" + SE + "\"");
            sb.Append(" -f \"" + FE + "\"");
            sb.Append(" -G \"" + TE + "\"");
            sb.Append(" -L tmplt/proc-loc.blb -R tmplt/proc-tmplt.tif");
            sb.Append(" -N " + dn.Tile);
            sb.Append(" proc.txt");
            ExecuteCommandSync(sb.ToString(), dn.BaseDirectory);
        }

        public void CallMatchStats(ref Dictionary<string, float> perfectMatch, ref Dictionary<string, float> OneOff)
        {
            foreach (int tileName in _FCList)
            {
                CallMatchStats(tileName, ref perfectMatch, ref OneOff);
            }
        }
        internal void CallMatchStats(int tileName, ref Dictionary<string, float> perfectMatch, ref Dictionary<string, float> OneOff)
        {
            Dictionary<string, string> reference = new Dictionary<string, string>();
            string refFile = Path.Combine(_TargetDir.FullName, "ref.fasta");
            if (!File.Exists(refFile))
                return;
            using (StreamReader refSeq = new StreamReader(refFile))
            {
                while (!refSeq.EndOfStream)
                {
                    string name = refSeq.ReadLine();
                    string value = refSeq.ReadLine();
                    reference[name] = value;
                }

            }

            Dictionary<string, int> perfect = new Dictionary<string, int>();
            Dictionary<string, int> oneOff = new Dictionary<string, int>();
            foreach (string key in reference.Keys)
            {
                perfect[key] = 0;
                oneOff[key] = 0;
            }
            int numClusters = 0;

            string curCalls = Path.Combine(_TargetDir.FullName, "proc-" + tileName, "calls", "proc-int-clr.fastq");
            if (!File.Exists(curCalls))
                return;

            int curCycle = 0;
            using (StreamReader calls = new StreamReader(curCalls))
            {
                while (!calls.EndOfStream)
                {
                    string name = calls.ReadLine();
                    string call = calls.ReadLine();
                    string comment = calls.ReadLine();
                    string quality = calls.ReadLine();
                    numClusters++;
                    if (curCycle < 1)
                        curCycle = call.Length;
                    foreach (string key in reference.Keys)
                    {
                        int errCount = 0;
                        for (int i = 0; i < call.Length; i++)
                        {
                            if (call[i] != reference[key][i])
                                errCount++;
                        }
                        if (errCount == 0)
                            perfect[key]++;
                        if (errCount == 1)
                            oneOff[key]++;
                    }
                }
            }

            //Dictionary<string, float> stats = new Dictionary<string, float>();
            foreach (string key in reference.Keys)
            {
                perfectMatch[key] = 100.0f * (float)perfect[key] / (float)numClusters;
            }

            //Dictionary<string, float> stats1Error = new Dictionary<string, float>();
            foreach (string key in reference.Keys)
            {
                OneOff[key] = 100.0f * ((float)oneOff[key] + (float)perfect[key]) / (float)numClusters;
            }

            float totPerfect = 0;
            foreach (string key in reference.Keys)
            {
                totPerfect += perfectMatch[key];
            }

            float totOneOff = 0;
            foreach (string key in reference.Keys)
            {
                totOneOff += OneOff[key];
            }

            string statFile = Path.Combine(_TargetDir.FullName, "proc-" + tileName, "Stat" + curCycle + ".txt");

            using (StreamWriter sr = new StreamWriter(statFile))
            {

                sr.WriteLine("name\tPerfect%\tOne Off %");
                foreach (string key in reference.Keys)
                {
                    sr.WriteLine(key + "\t" + perfectMatch[key] + "\t" + OneOff[key]);
                }
                sr.WriteLine();
                sr.WriteLine("total\t" + totPerfect + "\t" + totOneOff);
            }

        }

        private static void LogToFile(string msg, DirectoryInfo di = null)
        {
            string dirname = "basic";
            if (di != null)
                dirname = di.Name;
            string msgOut = DateTime.Now.ToLongTimeString() + ":" + Thread.CurrentThread.Name + ")" + dirname + " " + msg + "\r\n";
            /*if (di == null)
            {*/
            lock (LogFile)
            {
                File.AppendAllText(LogFile, msgOut);
            }
            /*}
            else
            {
                lock(_lockObject[CellToIndex[di.Name]])
                {
                    File.AppendAllText(System.IO.Path.Combine(di.FullName, logFileName), msgOut);
                }
            }*/
        }


        public void BuildTemplate()
        {
            DirectoryInfo di = TargetDir;
            DirectoryInfo dataDir = new DirectoryInfo(System.IO.Path.Combine(TargetDir.FullName, "Data"));
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (sd.Name.Contains("proc"))
                {
                    BuildTemplateForaCell(sd);
                }
            }
        }

        private void BuildTemplateForaCell(DirectoryInfo sd)
        {
            StringBuilder sb = new StringBuilder();

            LogToFile("Building template in " + sd.FullName);

            sb.Append(System.IO.Path.Combine(TargetDir.FullName, "bin", "BuildTmplt-static.exe"));
            sb.Append(" " + TO);
            sb.Append(" -T \"" + TP + "\"");
            sb.Append(" -f \"" + FT + "\"");
            sb.Append(" -G \"" + TT + "\"");
            sb.Append(" -a \"" + ST + "\"");
            sb.Append(" proc.txt");
            ExecuteCommandSync(sb.ToString(), sd);

            ExecuteCommandSync("move proc.log logs/proc-tmplt.log", sd);
            ExecuteCommandSync("move proc-tmplt-at.* tmplt", sd);
            ExecuteCommandSync("move proc-tmplt*.* tmplt", sd);
            ExecuteCommandSync("move proc-loc.blb tmplt", sd);
            //hold off RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".extractInt");
        }

        private void WriteParams(DirectoryInfo paramDir, string name, string contents)
        {
            FileInfo target = new FileInfo(System.IO.Path.Combine(paramDir.FullName, name));
            if (target.Exists)
                target.Delete();
            File.WriteAllText(target.FullName, contents);
            //Log(target.Name + " = " + contents);
        }

        public static void ExecuteCommandSync(string command, DirectoryInfo workingDir)
        {
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                if (DoNotExe)
                    return;
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
                Stopwatch sw = new Stopwatch();
                sw.Start();
                proc.Start();
                // Get the output into a string
                string result = proc.StandardOutput.ReadToEnd();
                sw.Stop();
                LogToFile(sw.ElapsedMilliseconds + " to exec " + command);
                // Display the command output.
                if (_LogCMDLineOutput)
                    LogToFile(result, workingDir);
            }
            catch (Exception objException)
            {
                LogToFile(objException.ToString(), workingDir);
            }
        }


        private void RemoveFiles(string directory, string filter)
        {
            DirectoryInfo di = new DirectoryInfo(directory);
            foreach (FileInfo fl in di.GetFiles(filter))
            {
                fl.Delete();
            }
        }


        public void Prep(DirectoryInfo target = null)
        {
            if (target != null)
                Log("Preping " + target.FullName);
            else
                Log("prepping all");
            DirectoryInfo di = TargetDir;
            if (target == null)
            {
                foreach (int i in FCList)
                {
                    Directory.CreateDirectory(System.IO.Path.Combine(TargetDir.FullName, "proc-" + FCList[i]));
                }
            }
            else
            {
                target.Create();
            }

            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (target != null)
                {
                    if (sd.Name != target.Name)
                        continue;
                }
                if (sd.Name.Contains("proc"))
                {
                    CleanDirectory(sd.FullName);
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, ".dep"));
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, "at"));
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, "calls"));
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, "logs"));
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, "locs"));
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, "extr"));
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, "qc"));
                    CleanDirectory(System.IO.Path.Combine(sd.FullName, "tmplt"));
                }
            }



        }

        private void CleanDirectory(string targetDir)
        {
            CreateDirectory(targetDir);
            return;

            DirectoryInfo di = new DirectoryInfo(targetDir);
            foreach (FileInfo fi in di.GetFiles())
            {
                fi.Delete();
            }
        }

        private void CreateDirectory(DirectoryInfo sd, string subdir)
        {
            DirectoryInfo newDir = new DirectoryInfo(System.IO.Path.Combine(sd.FullName, subdir));
            newDir.Create();
        }

        private void CreateDirectory(string dirPath)
        {
            DirectoryInfo newDir = new DirectoryInfo(dirPath);
            newDir.Create();
        }

        private void CreateProcTxt(int startCycle, int stopCycle, DirectoryInfo subDir = null, bool subDirCreate = true)
        {
            DirectoryInfo di = TargetDir;
            DirectoryInfo data = new DirectoryInfo(System.IO.Path.Combine(di.FullName, "data"));

            if (!subDirCreate && subDir != null)
            {
                int zheight = Convert.ToInt32(subDir.Parent.Name.Substring(subDir.Parent.Name.IndexOf("proc-") + 5));
                FileCreateProcTxt(startCycle, stopCycle, zheight, data, subDir);
                return;
            }
            foreach (DirectoryInfo sd in di.GetDirectories())
            {
                if (subDir != null)
                {
                    if (sd.Name != subDir.Name)
                        continue;
                }
                if (sd.Name.Contains("proc"))
                {
                    int zheight = Convert.ToInt32(sd.Name.Substring(sd.Name.IndexOf("proc-") + 5));
                    FileCreateProcTxt(startCycle, stopCycle, zheight, data, sd);
                }
            }
        }

        private static void FileCreateProcTxt(int startCycle, int stopCycle, int zheight, DirectoryInfo data, DirectoryInfo sd)
        {
            StringBuilder relaventFiles = new StringBuilder();
            for (int cycle = startCycle; cycle <= stopCycle; cycle++)
            {
                string zheightString = "_" + zheight.ToString() + "m";
                string cycleTag = "_Inc" + cycle + "_";
                foreach (FileInfo fi in data.GetFiles("*" + cycleTag + "*"))
                {

                    if (fi.Name.Contains(zheightString) && fi.Name.Contains(cycleTag))
                        relaventFiles.AppendLine(@"../data/" + fi.Name);
                }
            }
            string proctxt = System.IO.Path.Combine(sd.FullName + @"/proc.txt");
            File.WriteAllText(proctxt, relaventFiles.ToString());
        }

        public void RunCycle(int cycle, DirectoryInfo baseDir)
        {
            _TargetDir = baseDir;
            Thread[] newThread = new Thread[FCList.Length];
            for (int i = 0; i < FCList.Length; i++)
            {
                if (_enableTile[i])
                {
                    newThread[i] = new Thread(RunCycles);
                    newThread[i].Name = "Prep" + FCList[i];
                    DirectoryNumber dn = new DirectoryNumber(FCList[i], baseDir, cycle);
                    newThread[i].Start(dn);
                    Thread.Sleep(10);
                    newThread[i].Join();
                }
            }

        }

        public void RunCycleAsynch(int cycle, DirectoryInfo baseDir)
        {
            DirectoryNumber dn = new DirectoryNumber(-1, baseDir, cycle);
            Thread newThread = new Thread(RunCyclesSynch);
            newThread.Name = "RunCycle all";
            ResetTemplateCreate();
            newThread.Start(dn);
            Thread.Sleep(10);
        }

        private void RunCyclesSynch(object obj)
        {
            DirectoryNumber dn = (DirectoryNumber)obj;
            RunCycle(dn.Cycle, dn.BaseDirectory);
        }

        public void RunAllByTile(DirectoryInfo baseDir)
        {
            _TargetDir = baseDir;
            for (int i = 0; i < FCList.Length; i++)
            {
                DirectoryNumber dn = new DirectoryNumber(FCList[i], _TargetDir, _NS);
                Thread newThread = new Thread(RunCycles);
                newThread.Name = "RunCycle all";
                _templateCreated[i] = false;
                newThread.Start(dn);
                Thread.Sleep(10);
                newThread.Join();
            }
        }

        public void PostProcess(DirectoryInfo basedir)
        {
            _TargetDir = basedir;
            LogFile = System.IO.Path.Combine(_TargetDir.FullName, logFileName);
            RunAllByTile(basedir);
        }

        private void RunPostProcess()
        {
            Prep();
            //CreateProcTxt();
            WriteToParams();

            BuildTemplate();
            ExtractIntensities();

            JoinCycles();
            BaseCall();
            PostProcess();

        }

        public void RunAll(object obj)
        {
            DirectoryNumber dnArg = (DirectoryNumber)obj;
            _TargetDir = dnArg.BaseDirectory;
            bool allAtOnce = false;
            LogFile = System.IO.Path.Combine(_TargetDir.FullName, logFileName);

            Thread[] newThread = new Thread[FCList.Length];
            for (int cycle = 1; cycle <= NS; cycle++)
            {
                Log("Cycle Start: " + cycle);
                for (int i = 0; i < FCList.Length; i++)
                {
                    if (_enableTile[i])
                    {
                        Log("Cycle Start: " + cycle + " tile " + i);
                        newThread[i] = new Thread(RunCycles);
                        newThread[i].Name = "Prep" + FCList[i];
                        DirectoryNumber dn = new DirectoryNumber(FCList[i], _TargetDir, cycle);
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        newThread[i].Start(dn);
                        Thread.Sleep(10);
                        if (!allAtOnce)
                            newThread[i].Join();
                        sw.Stop();
                        LogToFile("Cycle " + cycle + " took " + sw.ElapsedMilliseconds);

                        Dictionary<string, float> perfect = new Dictionary<string, float>();
                        Dictionary<string, float> oneOff = new Dictionary<string, float>();
                        //CallMatchStats(FCList[i], ref perfect, ref oneOff);
                    }
                }
                if (allAtOnce)
                {
                    for (int i = 0; i < FCList.Length; i++)
                    {
                        if (_enableTile[i])
                            newThread[i].Join();
                    }
                }

            }

            Log("Done Processing: time " + DateTime.Now.ToLongTimeString());
        }

        private void RunCycles(object obj)
        {
            DirectoryNumber dn = (DirectoryNumber)obj;
            DirectoryInfo di = new DirectoryInfo(System.IO.Path.Combine(dn.BaseDirectory.FullName, "proc-" + dn.Tile.ToString()));
            //dont start until we can build a template
            string[] items = TP.Split(' ');
            int cnt = Convert.ToInt32(items[0]);
            int start = Convert.ToInt32(items[1]);
            int step = Convert.ToInt32(items[2]);
            minTemplateCycle = cnt / 4; // this is the min cycle for template building
            int tileindex = 0;
            for (int i = 0; i < _FCList.Length; i++)
            {
                if (_FCList[i] == dn.Tile)
                    tileindex = i;
            }

            if (dn.Cycle < minTemplateCycle)
                return; // too early for template
            else if (dn.Cycle >= minTemplateCycle && !_templateCreated[tileindex])
            {
                // prepare this tile for run
                Prep(di);
                WriteToParams(di);

                // build the tamplate
                CreateProcTxt(1, minTemplateCycle, di);
                BuildTemplateForaCell(di);
                _templateCreated[tileindex] = true;
            }

            // run requested cycle - no base calling
            CreateProcTxt(1, dn.Cycle, di); // update to this cycle
            Log("Cycle " + dn.Cycle + " start Extract Intensities ");
            DateTime starttime = DateTime.Now;
            ExtractIntensitiesByCell(di, _LastCycleExtracted[tileindex] + 1, dn.Cycle); // just this cycle
            _LastCycleExtracted[tileindex] = dn.Cycle; // update where we are in extraction
            DateTime stoptime = DateTime.Now;
            Log("Extract Intensities took = " + stoptime.Subtract(starttime).TotalSeconds);
            Log("Cycle " + dn.Cycle + " start Post Process Extract intensities");
            PostProcessingForExtractIntensities(di);

            // only run base calling if this is the last cycle
            if (dn.Cycle >= _baseCallMinCycle)
            {
                Log("Final start Join ");
                JoinCyclesByCell(di, dn.Cycle);
                Log("Final start base calling ");
                BaseCallByCell(di, dn.Cycle);
            }

            if (dn.Cycle == NS)
            {
                // finish all files and set to done
                // disable rename for now
                // PostProcessByCell(di);
            }
        }
    }
}
