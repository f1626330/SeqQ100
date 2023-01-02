using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sequlite.Image.Processing
{
    public class OLAStats
    {
        public OLAStats(string directory)
        {
            ReadStatsFiles(new DirectoryInfo(directory));
            ReadQCFiles(new DirectoryInfo(directory));
        }

        private void ReadStatsFiles(DirectoryInfo directoryInfo)
        {
            foreach (DirectoryInfo di in directoryInfo.GetDirectories())
            {
                if (!di.Name.ToString().StartsWith("proc-"))
                    continue;
                
                ReadStatsFiles(di);
            }

            foreach (FileInfo fi in directoryInfo.GetFiles("Stat*.txt"))
            {
                if (fi == null)
                    continue;
                DirectoryInfo parentDir = fi.Directory.Parent;
                string cycleSTR = fi.Name.Substring(4, fi.Name.IndexOf('.') - 4);
                int color = 0; // all colors
                int cycle = Convert.ToInt32(cycleSTR);
                string tileName = fi.Directory.Name.Substring(5);
                string header = "Perfect%,1 edit%,2 edit%,3 edit%";

                using (StreamReader sr = new StreamReader(fi.FullName))
                {
                    while (!sr.EndOfStream)
                    {
                        string values = sr.ReadLine();
                        if (values.Contains("Total"))
                        {
                            values = values.Replace("\t", ",");
                            values = values.Replace("Total,", "");
                            Add(tileName, cycle, color, header, values);
                        }
                    }
                }
            }
        }

        public List<string> AvailableTiles()
        {
            if (RTparams.Keys.Count() > 0 && RTparams.ContainsKey(RTparams.Keys.ElementAt(0)))
                return RTparams[RTparams.Keys.ElementAt(0)].AvailableTiles();
            else
                return new List<string>();
        }

        private void ReadQCFiles(DirectoryInfo directory)
        {
            foreach (DirectoryInfo di in directory.GetDirectories())
            {
                if (!di.Name.ToString().StartsWith("proc-"))
                    continue;

                ReadQCFiles(di);
            }

            foreach (FileInfo fi in directory.GetFiles("proc_*-qc.csv"))
            {
                if (fi == null)
                    continue;
                using (StreamReader sr = new StreamReader(fi.FullName))
                {
                    while (!sr.EndOfStream)
                    {
                        DirectoryInfo parentDir = fi.Directory.Parent;
                        string index = fi.Name.Substring(5, 6);
                        int idx = Convert.ToInt32(index);
                        int color = idx % 4; // rel 0
                        int cycle = (idx - color) / 4 + 1; // rel 1
                        string tileName = parentDir.Name.Substring(5);
                        string header = sr.ReadLine();
                        string values = sr.ReadLine();
                        Add(tileName, cycle, color, header, values);
                    }
                }
            }
        }

        public Dictionary<int, float> GetByCycle(string tileName, int clr, string v)
        {
            Dictionary<int, float> ret = new Dictionary<int, float>();
            if (RTparams.ContainsKey(v))
                RTparams[v].GetByCycle(tileName, clr, ref ret);
            return ret;
        }

        Dictionary<string, RunTimeParameter> RTparams = new Dictionary<string, RunTimeParameter>();
        private void Add(string tileName, int cycle, int color, string header, string values)
        {
            string[] items = header.Split(',');
            string[] vals = values.Split(',');
            int start = 0;
            int stop = vals.Length;

            
#if false // RB 06022020 I don't know what this code is for, so if-false it for now
            if (vals.Length > 2)
            {
                start = 1;
                stop = 29;
            }
#endif

            for (int i = start; i < stop; i++)
            {
                if (!RTparams.ContainsKey(items[i]))
                    RTparams[items[i]] = new RunTimeParameter(items[i]);
                RTparams[items[i]].Add(tileName, cycle, color, Convert.ToSingle(vals[i]));
            }
        }

        public List<String> AvailableParams()
        {
            List<String> ret = new List<string>();
            foreach (string key in RTparams.Keys)
                ret.Add(key);
            return ret;
        }
    }
}
