using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.RIPP
{
    /// <summary>
    /// Process Instrument images files all the way to FASTQ
    /// </summary>
    public class ImageProcessingCMD
    {
        string _RN = "";
        string _FC = "";
        static List<int> _FCList = new List<int>();
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
        int minTemplateCycle = 4;
        static readonly string logFileName = "trackingCMD.txt";
        static string LogFile = "";
        List<bool> _templateCreated = new List<bool>();
        List<int> _LastCycleExtracted = new List<int>();
        List<bool> _enableTile = new List<bool>();
        static readonly bool _LogCMDLineOutput = false;
        static bool _enableBaseStatistics = true;
        int _baseCallMinCycle = 4;
        static int _maxThreads = 6;

        public string RN { get => _RN; set => _RN = value; }
        public string FC { get => _FC; set => _FC = value; }
        public List<int> FCList
        {
            get
            {
                return _FCList;
            }
            set
            {
                _FCList = value;
                ReAjustByFlowCell();
            }
        }
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
        public List<bool> TemplateCreated { get => _templateCreated; set => _templateCreated = value; }
        public int BaseCallMinCycle { get => _baseCallMinCycle; set => _baseCallMinCycle = value; }
        public List<bool> EnableTile { get => _enableTile; set => _enableTile = value; }
        public static bool EnableBaseStatistics { get => _enableBaseStatistics; set => _enableBaseStatistics = value; }
        public static int MaxThread { get => _maxThreads; set => _maxThreads = value; }

        public ImageProcessingCMD(string baseDir)
        {
            if (baseDir != null)
            {
                _TargetDir = new DirectoryInfo(baseDir); // this is the location of the data and the results

                // create list of tiles
                DirectoryInfo dataDir = _TargetDir.GetDirectories("data")[0];
                _FCList.Clear();
                foreach (FileInfo fi in dataDir.GetFiles())
                {
                    string[] items = fi.Name.Split('_');
                    int fc = Convert.ToInt32(items[3].Replace("m", ""));
                    if (!_FCList.Contains(fc))
                        _FCList.Add(fc);
                }
            }
            LogFile = System.IO.Path.Combine(_TargetDir.FullName, logFileName);
            LoadRunSHParameters();
            ReAjustByFlowCell();
        }

        private void ReAjustByFlowCell()
        {
            for (int i = 0; i < _FCList.Count; i++)
            {
                _LastCycleExtracted.Add(0);
                _enableTile.Add(true);
                _templateCreated.Add(false);
            }
        }
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
            for (int i = 0; i < _templateCreated.Count; i++)
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

            string exePath = Path.Combine(TargetDir.FullName, "bin", "BaseCall-static.exe");
            if (!File.Exists(exePath))
                exePath = Path.Combine(TargetDir.Parent.FullName, "bin", "BaseCall-static.exe");
            sb.Append(exePath);
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

            for (int retries = 0; retries < 4; retries++)
            {
                bool ok = ExecuteCommandSync(sb.ToString(), calls);
                if (ok)
                    break;
                sb = sb.Replace(" -n 0 ", " -n " + Math.Max(_maxThreads / 2, 1) + " ");
                sb = sb.Replace(" -n 8 ", " -n " + Math.Max(_maxThreads / 2, 1) + " ");
                LogToFile("Error: Base calling failed, trying again " + retries + 1);

            }

            FileManipulator.MoveFile("proc-int.log", "../logs", calls);
            //ExecuteCommandSync("move proc-int.log ..//logs", calls);
            // wait for now RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".post");
        }

        public void LoadRunSHParameters()
        {
            //IntensityBLI fl0 = new IntensityBLI(@"D:\Data\ALF\20190814CRT61\proc-0\extr\proc_000000-int.bli");
            //IntensityBLI fl1 = new IntensityBLI(@"D:\Data\ALF\20190814CRT61\proc-0\extr\proc_000001-int.bli");
            //IntensityBLI fl2 = new IntensityBLI(@"D:\Data\ALF\20190814CRT61\proc-0\extr\proc_000001-int.bli");
            //IntensityBLI fl3 = new IntensityBLI(@"D:\Data\ALF\20190814CRT61\proc-0\extr\proc_000003-int.bli");

            //fl0.WriteFile(@"D:\Data\ALF\20190814CRT61\proc-0\extr\proc_test-int.bli");
            //fl0.addFOV(fl1);

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

        private bool JoinCyclesByCell(DirectoryInfo sd, int cycle = 0)
        {
            bool useNewMethod = true;
            DirectoryInfo extr = new DirectoryInfo(System.IO.Path.Combine(sd.FullName, "extr"));

            StringBuilder sb = new StringBuilder();
            if (cycle == 0)
                cycle = NS;
            //CreateProcTxt(1, cycle, extr, false);

            // new method
            if (useNewMethod)
            {
                IntensityBLI current = new IntensityBLI(Path.Combine(sd.FullName, "calls", "proc-int.bli"));
                AffineFile curAF = new AffineFile(Path.Combine(sd.FullName, "at", "proc-at.bla"));
                ulong start = current.Fov;
                if (current.Fov == 0)
                {
                    string initFileName = Path.Combine(sd.FullName, "extr", "proc_000000-int.bli");
                    if (!File.Exists(initFileName))
                        return false; // can find initial file
                    current = new IntensityBLI(initFileName);
                    if (current.NumClusters < 1)
                    {
                        // extracted intensities not found
                        return false; // indicate error
                    }
                    current.WriteFile(Path.Combine(sd.FullName, "calls", "proc-int.bli"));
                    start = 1; // already loaded 0
                }
                if (curAF.N == 0)
                {
                    curAF = new AffineFile(Path.Combine(sd.FullName, "extr", "proc_000000-at.bla"));
                    curAF.WriteFile(Path.Combine(sd.FullName, "at", "proc-at.bla"));
                }

                ulong stop = (ulong)extr.GetFiles("proc_*-int.bli").Length;

                for (int i = (int)start; i < (int)stop; i++)
                {
                    string flname = Path.Combine(sd.FullName, "extr", String.Format("proc_{0:D6}-int.bli", i));
                    IntensityBLI fl1 = new IntensityBLI(flname);
                    current.AddFOV(fl1);
                    string atflname = Path.Combine(sd.FullName, "extr", String.Format("proc_{0:D6}-at.bla", i));
                    AffineFile aff = new AffineFile(atflname);
                    curAF.add(aff);
                }
            }
            else
            {
                string exePath = Path.Combine(TargetDir.FullName, "bin", "JoinCycles-static.exe");
                if (!File.Exists(exePath))
                    exePath = Path.Combine(TargetDir.Parent.FullName, "bin", "JoinCycles-static.exe");
                sb.Append(exePath);
                sb.Append(" " + JO);
                sb.Append(" -H \"" + NC + "\"");
                sb.Append(" -b \"" + BO + "\"");
                sb.Append(" proc.txt");
                ExecuteCommandSync(sb.ToString(), extr);

                FileManipulator.MoveFile("proc-int.*", "../calls", extr);
                FileManipulator.MoveFile("proc-at.*", "../at", extr);
            }
            //ExecuteCommandSync("move proc-int.* ../calls", extr);
            //ExecuteCommandSync("move proc-at.* ../at", extr);
            // for short time RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".baseCall");
            return true;
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
            /* old way without retries
            for (int num = (cycleStart - 1) * 4; num < cycleStop * 4; num++)
            {
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
            */

            //check for failues and retry
            for (int tries = 0; tries < 4; tries++)
            {
                int errors = 0;
                for (int num = (cycleStart - 1) * 4; num < cycleStop * 4; num++)
                {
                    string targetFile = Path.Combine(sd.FullName, string.Format("proc_{0:D6}-int.bli", num));
                    if (!File.Exists(targetFile))
                    {
                        errors++;
                        if (tries > 0)
                            LogToFile("ERROR: attempt number " + tries + " for " + targetFile);
                        newThread[num] = new Thread(ExtractSingleIntensity);
                        newThread[num].Name = sd.Name + ":" + num.ToString();
                        DirectoryNumber dn = new DirectoryNumber(num, sd);
                        newThread[num].Start(dn);
                        Thread.Sleep(100); // start process
                    }
                }

                for (int num = 0; num < CycleFrames; num++)
                {
                    if (newThread[num] != null)
                    {
                        newThread[num].Join();
                        newThread[num] = null; // no longer needed,mark as unused
                    }
                }
                if (errors < 1)
                {
                    break; // no errors found, we are done
                }
            }
        }

        private void PostProcessingForExtractIntensities(DirectoryInfo sd)
        {
            FileManipulator.MoveFile("*.log", "logs", sd);
            FileManipulator.MoveFile("*-loc.*", "locs", sd);
            FileManipulator.MoveFile("*-int.*", "extr", sd);
            FileManipulator.MoveFile("*-at.*", "extr", sd);
            FileManipulator.MoveFile("proc.txt", "extr", sd, false);
            FileManipulator.MoveFile("*-qc.*", "qc", sd);

            /*
            ExecuteCommandSync("move *.log logs", sd);
            ExecuteCommandSync("move *-loc.* locs", sd);
            ExecuteCommandSync("move *-int.* extr", sd);
            ExecuteCommandSync("move *-at.* extr", sd);
            ExecuteCommandSync("copy proc.txt extr", sd);
            ExecuteCommandSync("move *-qc.* qc", sd);
            */
            //hold on this RemoveFiles(System.IO.Path.Combine(sd.FullName, ".dep"), ".joinCycles");
        }

        private static void ExtractSingleIntensity(object item)
        {
            DirectoryNumber dn = (DirectoryNumber)item;
            DirectoryInfo di = TargetDir;
            StringBuilder sb = new StringBuilder();

            //LogToFile("Extracting in " + TargetDir);

            string exePath = Path.Combine(TargetDir.FullName, "bin", "ExtractInt-static.exe");
            if (!File.Exists(exePath))
                exePath = Path.Combine(TargetDir.Parent.FullName, "bin", "ExtractInt-static.exe");
            sb.Append(exePath);
            sb.Append(" " + EO);
            sb.Append(" -e \"" + EP + "\"");
            sb.Append(" -a \"" + SE + "\"");
            sb.Append(" -f \"" + FE + "\"");
            sb.Append(" -G \"" + TE + "\"");
            sb.Append(" -L tmplt/proc-loc.blb -R tmplt/proc-tmplt.tif");
            sb.Append(" -N " + dn.Tile);
            sb.Append(" proc.txt");

            for (int tries = 0; tries < 4; tries++)
            {
                bool ok = ExecuteCommandSync(sb.ToString(), dn.BaseDirectory);
                if (ok)
                    break;
                sb = sb.Replace(" -n 0 ", " -n " + Math.Max(_maxThreads / 2, 1) + " ");
                sb = sb.Replace(" -n 8 ", " -n " + Math.Max(_maxThreads / 2, 1) + " ");
                LogToFile("Error: Extract failed, trying again " + tries + 1);
            }
        }

        public void CallMatchStats(ref Dictionary<string, float> perfectMatch, ref Dictionary<string, float> OneOff, int cycle = -1)
        {
            foreach (int tileName in _FCList)
            {
                CallMatchStats(tileName, ref perfectMatch, ref OneOff, cycle);
            }
        }
        public void CallMatchStats(int tileName, ref Dictionary<string, float> perfectMatch, ref Dictionary<string, float> OneOff, int cycle = -1)
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
            Dictionary<string, float> DLDist = new Dictionary<string, float>();
            Dictionary<string, float> DLDist3 = new Dictionary<string, float>();
            foreach (string key in reference.Keys)
            {
                perfect[key] = 0;
                oneOff[key] = 0;
                DLDist[key] = 0;
                DLDist3[key] = 0;
            }
            int numClusters = 0;

            string curCalls = Path.Combine(_TargetDir.FullName, "proc-" + tileName, "calls", "proc-int-clr.fastq");
            if (!File.Exists(curCalls))
                return;

            int curCycle = cycle;
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
                        for (int i = 0; i < curCycle; i++)
                        {
                            if (call[i] != reference[key][i])
                                errCount++;
                        }
                        if (errCount == 0)
                            perfect[key]++;
                        if (errCount == 1)
                            oneOff[key]++;
                        int dlDist = GetDamerauLevenshteinDistance(reference[key].Substring(0, curCycle), call.Substring(0, curCycle));
                        if (dlDist < 3)
                            DLDist[key]++;
                        if (dlDist < 4)
                            DLDist3[key]++;
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

            foreach (string key in reference.Keys)
            {
                DLDist[key] = 100.0f * (float)DLDist[key] / (float)numClusters;
                DLDist3[key] = 100.0f * (float)DLDist3[key] / (float)numClusters;
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

            float totDLDist = 0;
            float totDLDist3 = 0;
            foreach (string key in reference.Keys)
            {
                totDLDist += DLDist[key];
                totDLDist3 += DLDist3[key];
            }


            string statFile = Path.Combine(_TargetDir.FullName, "proc-" + tileName, "Stat" + curCycle + ".txt");

            using (StreamWriter sr = new StreamWriter(statFile, false))
            {

                sr.WriteLine("Name\tPerfect%\t1 edit%\t2 edit%\t3 edit%");
                foreach (string key in reference.Keys)
                {
                    sr.WriteLine(String.Format("{0}\t{1:F1}\t{2:F1}\t{3:F1}\t{4:f1}", key, perfectMatch[key], OneOff[key], DLDist[key], DLDist3[key]));
                }
                sr.WriteLine();
                sr.WriteLine(String.Format("Total\t{0:F1}\t{1:F1}\t{2:F1}\t{3:f1}", totPerfect, totOneOff, totDLDist, totDLDist3));
            }

        }

        public static int GetDamerauLevenshteinDistance(string s, string t)
        {
            var bounds = new { Height = s.Length + 1, Width = t.Length + 1 };

            int[,] matrix = new int[bounds.Height, bounds.Width];

            for (int height = 0; height < bounds.Height; height++) { matrix[height, 0] = height; };
            for (int width = 0; width < bounds.Width; width++) { matrix[0, width] = width; };

            for (int height = 1; height < bounds.Height; height++)
            {
                for (int width = 1; width < bounds.Width; width++)
                {
                    int cost = (s[height - 1] == t[width - 1]) ? 0 : 1;
                    int insertion = matrix[height, width - 1] + 1;
                    int deletion = matrix[height - 1, width] + 1;
                    int substitution = matrix[height - 1, width - 1] + cost;

                    int distance = Math.Min(insertion, Math.Min(deletion, substitution));

                    if (height > 1 && width > 1 && s[height - 1] == t[width - 2] && s[height - 2] == t[width - 1])
                    {
                        distance = Math.Min(distance, matrix[height - 2, width - 2] + cost);
                    }

                    matrix[height, width] = distance;
                }
            }

            return matrix[bounds.Height - 1, bounds.Width - 1];
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

            string exePath = Path.Combine(TargetDir.FullName, "bin", "BuildTmplt-static.exe");
            if (!File.Exists(exePath))
                exePath = Path.Combine(TargetDir.Parent.FullName, "bin", "BuildTmplt-static.exe");
            sb.Append(exePath);
            sb.Append(" " + TO);
            sb.Append(" -T \"" + TP + "\"");
            sb.Append(" -f \"" + FT + "\"");
            sb.Append(" -G \"" + TT + "\"");
            sb.Append(" -a \"" + ST + "\"");
            sb.Append(" proc.txt");

            for (int tries = 0; tries < 5; tries++)
            {
                ExecuteCommandSync(sb.ToString(), sd);
                int numFiles = sd.GetFiles("proc-loc.blb").Length;
                if (numFiles > 0)
                {
                    LogToFile("Template built after " + tries + " tries");
                    break;
                }
            }

            int filesMoved = 0;
            filesMoved = FileManipulator.MoveFile("proc.log", "logs/proc-tmplt.log", sd);
            if (filesMoved < 1)
                LogToFile("Build template: no log files found");
            filesMoved = FileManipulator.MoveFile("proc-tmplt-at.*", "tmplt", sd);
            if (filesMoved < 1)
                LogToFile("Build template: no proc-tmplt-at.* files found");
            filesMoved = FileManipulator.MoveFile("proc-tmplt*.*", "tmplt", sd);
            if (filesMoved < 1)
                LogToFile("Build template: no proc-tmplt*.* files found");
            filesMoved = FileManipulator.MoveFile("proc-loc.blb", "tmplt", sd);
            if (filesMoved < 1)
                LogToFile("Build template: no proc-loc.blb files found");

            /*
            ExecuteCommandSync("move proc.log logs/proc-tmplt.log", sd);
            ExecuteCommandSync("move proc-tmplt-at.* tmplt", sd);
            ExecuteCommandSync("move proc-tmplt*.* tmplt", sd);
            ExecuteCommandSync("move proc-loc.blb tmplt", sd);
            */
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

        public static bool ExecuteCommandSync(string command, DirectoryInfo workingDir)
        {
            string errResult = "";
            try
            {
                // create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // Incidentally, /c tells cmd that we want it to execute the command that follows,
                // and then exit.
                if (DoNotExe)
                    return true;
                System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);
                string[] items = command.Split(' ');
                string cmd = items[0];
                string args = command.Substring(cmd.Length + 1);
                args = args.Replace(" -n 0 ", " -n " + _maxThreads + " ");
                procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
                string result = "";

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                if (_LogCMDLineOutput)
                    procStartInfo.RedirectStandardOutput = true;
                procStartInfo.RedirectStandardError = true;
                procStartInfo.UseShellExecute = false;
                procStartInfo.ErrorDialog = false;
                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;
                procStartInfo.WorkingDirectory = workingDir.FullName;
                // Now we create a process, assign its ProcessStartInfo and start it
                using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
                {
                    proc.StartInfo = procStartInfo;
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    proc.Start();
                    // Get the output into a string
                    if (_LogCMDLineOutput)
                        result = proc.StandardOutput.ReadToEnd();
                    errResult = proc.StandardError.ReadToEnd();
                    sw.Stop();
                    LogToFile(command.Split(' ')[0] + " took " + sw.ElapsedMilliseconds + " ms to exec shell command");
                }

                // Display the command output.
                if (_LogCMDLineOutput)
                    LogToFile(result, workingDir);
                if (errResult.Length > 0)
                    LogToFile("ERROR: cmd - " + errResult);
            }
            catch (Exception objException)
            {
                LogToFile(objException.ToString(), workingDir);
            }
            if (errResult.Length > 0)
                return false; //error!
            return true;
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
            /* too distructive to remove file - disable until we understand the commercial product
            DirectoryInfo di = new DirectoryInfo(targetDir);
            foreach (FileInfo fi in di.GetFiles())
            {
                fi.Delete();
            }
            */
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
            Thread[] newThread = new Thread[FCList.Count];
            for (int i = 0; i < FCList.Count; i++)
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
            for (int i = 0; i < FCList.Count; i++)
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
            bool allAtOnce = true;
            LogFile = System.IO.Path.Combine(_TargetDir.FullName, logFileName);

            Thread[] newThread = new Thread[FCList.Count];
            int[] FCCycleStarted = new int[FCList.Count];
            for (int i = 0; i < FCList.Count; i++)
            {
                FCCycleStarted[i] = 0;
                newThread[i] = new Thread(RunCycles);
                newThread[i].Name = "Prep" + FCList[i];
            }
            //for (int cycle = 1; cycle <= NS; cycle++)
            while (true)
            {
                //Log("Cycle Start: " + cycle);
                for (int i = 0; i < FCList.Count; i++)
                {
                    if (_enableTile[i] && !newThread[i].IsAlive && FCCycleStarted[i] != NS)
                    {
                        int cycle = FCCycleStarted[i] + 1;
                        Log("Cycle Start: " + cycle + " tile " + i);
                        newThread[i] = new Thread(RunCycles);
                        newThread[i].Name = "Prep" + FCList[i];
                        DirectoryNumber dn = new DirectoryNumber(FCList[i], _TargetDir, cycle);
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        newThread[i].Start(dn);
                        FCCycleStarted[i] = cycle;
                        if (!_templateCreated[i] && cycle == _baseCallMinCycle)
                            Thread.Sleep(30 * 1000);
                        else
                            Thread.Sleep(10);
                        if (!allAtOnce)
                            newThread[i].Join();
                        sw.Stop();
                        LogToFile("Cycle " + cycle + " took " + sw.ElapsedMilliseconds);

                        //if (false)
                        //{
                        //    Dictionary<string, float> perfect = new Dictionary<string, float>();
                        //    Dictionary<string, float> oneOff = new Dictionary<string, float>();
                        //    sw.Start();
                        //    CallMatchStats(FCList[i], ref perfect, ref oneOff);
                        //    sw.Stop();
                        //    LogToFile("Realtime stats took " + sw.ElapsedMilliseconds);
                        //}
                    }
                }
                Thread.Sleep(1000); // wait for tile & cycle to finish
                bool stillRunning = false;
                for (int i = 0; i < FCList.Count; i++)
                {
                    if (_enableTile[i])
                        if (newThread[i].IsAlive || FCCycleStarted[i] < NS)
                            stillRunning = true;
                }
                if (!stillRunning)
                    break; // all done
                /*
                if (allAtOnce)
                {
                    for (int i = 0; i < FCList.Length; i++)
                    {
                        if (_enableTile[i])
                            newThread[i].Join();
                    }
                }
                */

            }

            Log("Done Processing: time " + DateTime.Now.ToLongTimeString());
        }

        public void RunCycles(object obj)
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
            for (int i = 0; i < _FCList.Count; i++)
            {
                if (_FCList[i] == dn.Tile)
                {
                    tileindex = i;
                    break; // found it
                }
            }

            if (dn.Cycle < minTemplateCycle)
                return; // too early for template

            // must do template before anything else
            if (!_templateCreated[tileindex])
            {
                // prepare this tile for run
                Prep(di);
                WriteToParams(di);

                // build the tamplate
                CreateProcTxt(1, minTemplateCycle, di);
                BuildTemplateForaCell(di);
                _templateCreated[tileindex] = true; // ok, done. mark as done
            }

            //check for template file
            DirectoryInfo tmplt = new DirectoryInfo(Path.Combine(di.FullName, "tmplt"));
            if (tmplt.GetFiles().Length < 1)
            {
                LogToFile("ERROR: RunCycles failing: no files in " + tmplt.FullName);
                return;
            }
            // run requested cycle - no base calling
            CreateProcTxt(1, dn.Cycle, di); // update to this cycle
            Log("Cycle " + dn.Cycle + " start Extract Intensities ");
            DateTime starttime = DateTime.Now;
            ExtractIntensitiesByCell(di, _LastCycleExtracted[tileindex] + 1, dn.Cycle); // just this cycle
            _LastCycleExtracted[tileindex] = dn.Cycle; // update where we are in extraction
            DateTime stoptime = DateTime.Now;
            Log("Extract Intensities took = " + stoptime.Subtract(starttime).TotalMilliseconds);
            Log("Cycle " + dn.Cycle + " start Post Process Extract intensities");
            PostProcessingForExtractIntensities(di);
            Log("Final start Join ");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            bool err = JoinCyclesByCell(di, dn.Cycle);
            sw.Stop();
            LogToFile("Join took " + sw.ElapsedMilliseconds + " ms to execute");
            if (!err)
                LogToFile("ERROR: Join Cycle failed " + di.FullName + " " + dn.Cycle);

            // only run base calling if this is the last cycle
            if (dn.Cycle >= _baseCallMinCycle)
            {
                Log("Final start base calling ");
                BaseCallByCell(di, dn.Cycle);
                if (_enableBaseStatistics)
                {
                    Dictionary<string, float> perfect = new Dictionary<string, float>();
                    Dictionary<string, float> oneOff = new Dictionary<string, float>();
                    sw.Start();
                    CallMatchStats(FCList[tileindex], ref perfect, ref oneOff);
                    sw.Stop();
                    LogToFile("Realtime stats took " + sw.ElapsedMilliseconds);
                }
            }

            if (dn.Cycle == NS)
            {
                // finish all files and set to done
                // disable rename for now
                // PostProcessByCell(di);
            }
        }


#if false
        private void MinimumCycleProcessing(DirectoryInfo di, int cycle)
        {
            Log("Cycle " + cycle + " / " + NS);
            CreateProcTxt(1, cycle, di);
            Log("Cycle " + cycle + " start Extract Intensities ");
            ExtractIntensitiesByCell(di, 1, cycle);
            Log("Cycle " + cycle + " start Post Process Extract ");
            PostProcessingForExtractIntensities(di);
            //Log("Cycle " + cycle + " start Join ");
            //JoinCyclesByCell(di, cycle);
            //Log("Cycle " + cycle + " start base calling ");
            //BaseCallByCell(di, cycle);
            //File.Copy(System.IO.Path.Combine(di.FullName, "calls", "proc-int-clr.fastq"),
            //    System.IO.Path.Combine(di.FullName, "calls", "proc-int-clr" + cycle + ".fastq"));
        } 
#endif
    }
}
