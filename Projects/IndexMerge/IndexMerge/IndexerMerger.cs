using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics;

namespace IndexMerge
{
    public class OLATile
    {
        public string Name { get; set; } = ""; // e.g. bL102A
        public string Surface { get; set; } = ""; // b or t
        public int Lane { get; set; } = -1; // 1,2,3, or 4
        public int Column { get; set; } = 0; // "00" - "44"
        public string Row { get; set; } = ""; // A,B,C, or D
        public int ID { get; set; } = -1; // an integer representing a combination of all properties
        public bool Valid { get; set; } = false;
        public bool Failed { get; set; } = false;

        public OLATile(string name)
        {
            Name = name;
            string pattern = @"^(?<surface>(b|t))L(?<lane>(1|2|3|4))(?<column>\d{2})(?<row>(A|B|C|D))";
            Match match = Regex.Match(Name, pattern);

            if (match.Success)
            {
                Surface = match.Groups["surface"].Value;
                Lane = int.Parse(match.Groups["lane"].Value);
                Column = int.Parse(match.Groups["column"].Value);
                Row = match.Groups["row"].Value;

                int surface_id = -1;
                switch (Surface)
                {
                    case "b":
                        surface_id = 1;
                        break;
                    case "t":
                        surface_id = 2;
                        break;
                }

                int row_id = -1;
                switch (Row)
                {
                    case "A":
                        row_id = 1;
                        break;
                    case "B":
                        row_id = 2;
                        break;
                    case "C":
                        row_id = 3;
                        break;
                    case "D":
                        row_id = 4;
                        break;
                }

                Debug.Assert(surface_id > 0);
                Debug.Assert(Lane > 0);
                Debug.Assert(Column > 0);
                Debug.Assert(row_id > 0);

                ID = surface_id * 10000 + Lane * 1000 + Column * 10 + row_id;

                Valid = true;
            }
        }
    }

    public class OLAIndexInfo
    {
        public static readonly int INVALID_ID = -1;
        public int Id { get; set; } //a generic unique id for index
        public string Sequence; //for example "CTGAAGCT"

        public static string NonIndexedReads = "NonIndexedReads";

        public OLAIndexInfo(int id, string sequence)
        {
            Id = id;
            Sequence = sequence;
        }

        public OLAIndexInfo(OLAIndexInfo another)
        {
            Id = another.Id;
            Sequence = another.Sequence;
        }
    }

    class IndexerMerger
    {
        //private string LogPath { get; set; } = "";

        public Logger Logger;

        private string BaseWorkingDir { get; set; } = "";
        private long RunId { get; set; } = 0L;
        private string Instrument { get; set; } = "";
        private string InstrumentId { get; set; } = "";
        private string FlowCellId { get; set; } = "";

        //  index sequence id vs index sequence itself
        Dictionary<int, string> IndexFileNames = new Dictionary<int, string>();

        public IndexerMerger(string baseWorkingDir, long runId, string instrument, string instrumentId, string flowCellId)
        {
            BaseWorkingDir = baseWorkingDir;
            RunId = runId;
            Instrument = instrument;
            InstrumentId = instrumentId;
            FlowCellId = flowCellId;

            //LogPath = Path.Combine(baseWorkingDir, "IndexMerge.log");
        }

        internal void Run(bool index)
        {
            // Build a list of good tiles
            var tilesFile = File.ReadAllLines(Path.Combine(BaseWorkingDir, "GoodTiles.txt"));
            List<string> tiles = new List<string>(tilesFile);

            Logger.Log($"Good tiles count: {tiles.Count}");

            if (index)
                IndexFormatMergeFastqFiles(tiles);
            else
                FormatMergeFastqFiles(tiles);
        }

        private void IndexMergeFastqFiles(List<string> tiles)
        {
            // Build a dictionary of output fastq file names: index sequence id vs the actual index sequence
            string fasta_Path = Path.Combine(BaseWorkingDir, "Index1", "OLA", "ngram", "index.fasta");
            FileInfo indexFasta_File = new FileInfo(fasta_Path);
            if (!indexFasta_File.Exists)
                throw new Exception($"Index fasta file {fasta_Path} does not exist.");

            string r_line;
            using (StreamReader r = new StreamReader(indexFasta_File.FullName))
            {
                int line_number = -1;
                int id=0;
                while (r.Peek() >= 0)
                {
                    r_line = r.ReadLine();

                    line_number++;
                    // For even lines read the id; for odd lines read the sequence
                    if (line_number % 2 == 0)
                    {
                        string pattern = @">(?<id>-?\d+)";
                        Match match = Regex.Match(r_line, pattern, RegexOptions.IgnoreCase);
                        if (match.Success && match.Groups.Count > 0)
                        {
                            bool success = true;
                            if (!int.TryParse(match.Groups["id"].Value, out id))
                                success = false;
                            if (!success)
                            {
                                throw new Exception($"Wrong line {r_line} in {indexFasta_File.FullName}");
                            }
                        }
                        else
                            throw new Exception($"Wrong line {r_line} in {indexFasta_File.FullName}");
                    }
                    else
                    {
                        IndexFileNames[id] = r_line + ".fastq";
                    }
                }
            }

            if (IndexFileNames.Count == 0)
                throw new Exception("No index sequences available");

            // Add a file name for non-indexed reads
            IndexFileNames[OLAIndexInfo.INVALID_ID] = OLAIndexInfo.NonIndexedReads + ".fastq";

            Parallel.ForEach(Partitioner.Create(0, tiles.Count), range =>
            {
                for (var j = range.Item1; j < range.Item2; j++)
                {
                    IndexFastqFilesByTile(tiles[j]);
                }
            });

            // Create a list of all possible index sequence file names
            List<string> fileNames = new List<string>();
            foreach (var item in IndexFileNames)
            {
                fileNames.Add(item.Value);
            }

            Parallel.ForEach(Partitioner.Create(0, fileNames.Count), range =>
            {
                for (var j = range.Item1; j < range.Item2; j++)
                {
                    MergeIndexedFastqFiles(fileNames[j]);
                }
            });

            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(BaseWorkingDir, "temp"));
            if (tempDir.Exists)
                Directory.Delete(tempDir.FullName, true);
        }

        public class FastqWriter
        {
            string FilePath = "";
            public string IndexFileName = "";
            ConcurrentQueue<string> Buffer = new ConcurrentQueue<string>();
            Thread WriterThread;

            public FastqWriter(string BaseWorkingDir, string tileName, string indexFileName)
            {
                IndexFileName = indexFileName;
                FilePath = Path.Combine(BaseWorkingDir, "temp", tileName, IndexFileName);
            }

            public void Start()
            {
                WriterThread = new Thread(() => WriteFileSynchronous());
                WriterThread.IsBackground = true;
                WriterThread.Name = "OutputFastq_" + Path.GetFileNameWithoutExtension(IndexFileName);
                WriterThread.Priority = ThreadPriority.Normal;
                WriterThread.Start();
            }

            public void WriteFileAsynchronous()
            {
                FileStream fs = new FileStream(FilePath, FileMode.OpenOrCreate);

                string line;
                using (var writer = new StreamWriter(fs))
                {
                    while (Buffer.TryDequeue(out line))
                    {
                        writer.Write(line);
                        writer.Write('\n');
                    }
                }
            }

            private void WriteFileSynchronous()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                FileStream fs = new FileStream(FilePath, FileMode.OpenOrCreate);

                string line;
                using (var writer = new StreamWriter(fs))
                {
                    while (true)
                    {
                        bool stop = false;
                        while (Buffer.TryDequeue(out line))
                        {
                            if (line == "-1")
                            {
                                stop = true;
                                break;
                            }

                            writer.Write(line);
                            writer.Write('\n');
                        }

                        if (stop)
                            break;

                        Thread.Sleep(1000);
                    }
                }
            }

            public void AddLine(string line)
            {
                Buffer.Enqueue(line);
            }

            public void Flush()
            {
            }

            public void Stop()
            {
                AddLine("-1");
                WriterThread.Join();
            }
        }

        public void IndexFastqFilesByTile(string tileName)
        {
            const string fileType = "cpf";

            // Open the Read1 file
            FileInfo read1_File = new FileInfo(Path.Combine(BaseWorkingDir, "Read1", "OLA", "proc-" + tileName, "calls", "proc-int-" + fileType + ".fastq"));
            if (!read1_File.Exists)
                throw new Exception($"Could not find file {read1_File.FullName}");

            // Open the Index1 file
            FileInfo index1_File = new FileInfo(Path.Combine(BaseWorkingDir, "Index1", "OLA", "proc-" + tileName, "calls", "proc-int-" + fileType + ".fastq"));
            if (!index1_File.Exists)
                throw new Exception($"Could not find file {index1_File.FullName}");

            // Create fastq file writers, one per index 
            ConcurrentDictionary<int, FastqWriter> writers = new ConcurrentDictionary<int, FastqWriter>();
            foreach (var index in IndexFileNames)
            {
                writers[index.Key] = new FastqWriter(BaseWorkingDir, tileName, index.Value);
                writers[index.Key].Start();
            }

            string r1_line, i1_line;
            using (StreamReader r1 = new StreamReader(read1_File.FullName), i1 = new StreamReader(index1_File.FullName))
            {
                int line_number = -1;
                int index = -1;
                while (r1.Peek() >= 0)
                {
                    if (i1.Peek() == 0)
                        throw new Exception($"Files {read1_File.FullName} and {index1_File.FullName} have different number of lines");

                    r1_line = r1.ReadLine();
                    i1_line = i1.ReadLine();

                    line_number++;

                    if (line_number % 4 == 0)
                    {
                        //EXAMPLE: @proc - int - cpf.fastq_N:3_x: 2941.16_y: 7.81721_PF %:1_Index1: 2_ATGC
                        Match match = Regex.Match(i1_line, string.Format(@"_Index1:(?<index>\d+)"), RegexOptions.IgnoreCase);
                        if (match.Success && match.Groups.Count > 0)
                        {
                            string test = match.Groups["index"].Value;
                            int n;
                            bool b = int.TryParse(test, out n);
                            if (b)
                            {
                                index = n;
                            }
                            else
                                index = OLAIndexInfo.INVALID_ID;
                        }
                        else
                            index = OLAIndexInfo.INVALID_ID;

                        writers[index].AddLine(i1_line);
                    }
                    else if (line_number % 2 == 0)
                    {
                        writers[index].AddLine("+");
                    }
                    else
                        writers[index].AddLine(r1_line);

                    writers[index].Flush();
                }

                foreach (var entry in writers)
                    entry.Value.Stop();
            }
        }

        private void MergeIndexedFastqFiles(string indexFileName)
        {
            const int chunkSize = 2 * 1024; // 2KB

            string path = Path.Combine(BaseWorkingDir, "temp");
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists)
                throw new Exception($"Directory {path} is not found");

            var files = dir.GetFiles(indexFileName, SearchOption.AllDirectories);

            if (files.Count() == 0)
                return;

            string outputFilePath = Path.Combine(BaseWorkingDir, "fastq", indexFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
            using (var output = File.Create(outputFilePath))
            {
                foreach (var file in files)
                {
                    using (var input = File.OpenRead(file.FullName))
                    {
                        var buffer = new byte[chunkSize];
                        int bytesRead;
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }
        }
        private void MergeFormattedFastqFiles(List<string> tiles)
        {
            const int chunkSize = 2 * 1024; // 2KB
            const string fileType = "cpf";

            List<FileInfo> files = new List<FileInfo>();
            foreach (string tile in tiles)
            {
                FileInfo file = new FileInfo(Path.Combine(BaseWorkingDir, "temp", tile, fileType + ".fastq"));
                if (file.Exists)
                    files.Add(file);
            }

            if (files.Count() == 0)
                throw new Exception($"No {fileType} files found recursively in directory {BaseWorkingDir}");

            string fileName = "combined";
            //if (SeqInfo != null && !(String.IsNullOrEmpty(SeqInfo.ExpName) && String.IsNullOrEmpty(SeqInfo.SessionId)))
            //    fileName = SeqInfo.ExpName + "_" + SeqInfo.SessionId;
            fileName += '-' + fileType + ".fastq";

            string outputFilePath = Path.Combine(BaseWorkingDir, "fastq", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
            using (var output = File.Create(outputFilePath))
            {
                foreach (var file in files)
                {
                    using (var input = File.OpenRead(file.FullName))
                    {
                        var buffer = new byte[chunkSize];
                        int bytesRead;
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }
                }
            }
        }

        private void IndexFormatMergeFastqFiles(List<string> tiles)
        {
            Logger.Log("Executing IndexFormatMergeFastqFiles...");

            // Build a dictionary of output fastq file names: index sequence id vs the actual index sequence
            string fasta_Path = Path.Combine(BaseWorkingDir, "Index1", "OLA", "ngram", "index.fasta");
            FileInfo indexFasta_File = new FileInfo(fasta_Path);
            if (!indexFasta_File.Exists)
                throw new Exception($"Index fasta file {fasta_Path} does not exist.");

            string r_line;
            using (StreamReader r = new StreamReader(indexFasta_File.FullName))
            {
                int line_number = -1;
                int id = 0;
                while (r.Peek() >= 0)
                {
                    r_line = r.ReadLine();

                    line_number++;
                    // For even lines read the id; for odd lines read the sequence
                    if (line_number % 2 == 0)
                    {
                        string pattern = @">(?<id>-?\d+)";
                        Match match = Regex.Match(r_line, pattern, RegexOptions.IgnoreCase);
                        if (match.Success && match.Groups.Count > 0)
                        {
                            bool success = true;
                            if (!int.TryParse(match.Groups["id"].Value, out id))
                                success = false;
                            if (!success)
                            {
                                throw new Exception($"Wrong line {r_line} in {indexFasta_File.FullName}");
                            }
                        }
                        else
                            throw new Exception($"Wrong line {r_line} in {indexFasta_File.FullName}");
                    }
                    else
                    {
                        IndexFileNames[id] = r_line + ".fastq";
                    }
                }
            }

            if (IndexFileNames.Count == 0)
                throw new Exception("No index sequences available");

            // Add a file name for non-indexed reads
            IndexFileNames[OLAIndexInfo.INVALID_ID] = OLAIndexInfo.NonIndexedReads + ".fastq";

            // INDEX AND FORMAT FASTQ FILES PER TILE
            Parallel.ForEach(tiles, tile =>
            {
                IndexFormatFastqFilesByTile(tile);
            });

            // MERGE FORMATTED AND INDEXED FASTQ FILES

            // Create a list of all possible index sequence file names
            List<string> fileNames = new List<string>();
            foreach (var item in IndexFileNames)
            {
                fileNames.Add(item.Value);
            }

            // Merge fastq files of the same index sequence
            Parallel.ForEach(fileNames, fileName =>
            {
                MergeIndexedFastqFiles(fileName);
            });

            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(BaseWorkingDir, "temp"));
            if (tempDir.Exists)
                Directory.Delete(tempDir.FullName, true);
        }

        private void FormatMergeFastqFiles(List<string> tiles)
        {
            Logger.Log("Executing FormatMergeFastqFiles...");

            Parallel.ForEach(tiles, tile =>
            {
                FormatFastqFileByTile(tile);
            });

            MergeFormattedFastqFiles(tiles);

            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(BaseWorkingDir, "temp"));
            if (tempDir.Exists)
                Directory.Delete(tempDir.FullName, true);
        }

        public void IndexFormatFastqFilesByTile(string tileName)
        {
            const string fileType = "cpf";

            // Parse the tile name
            OLATile tile = new OLATile(tileName);
            if (!tile.Valid)
                throw new Exception($"Tile name {tileName} is invalid.");

            // Open the Read1 file
            FileInfo read1_File = new FileInfo(Path.Combine(BaseWorkingDir, "Read1", "OLA", "proc-" + tileName, "calls", "proc-int-" + fileType + ".fastq"));
            if (!read1_File.Exists)
                throw new Exception($"Could not find file {read1_File.FullName}");

            // Open the Index1 file
            FileInfo index1_File = new FileInfo(Path.Combine(BaseWorkingDir, "Index1", "OLA", "proc-" + tileName, "calls", "proc-int-" + fileType + ".fastq"));
            if (!index1_File.Exists)
                throw new Exception($"Could not find file {index1_File.FullName}");

            // TODO: determine read index
            int read = 1;

            // Create fastq file writers, one per file 
            ConcurrentDictionary<int, FastqWriter> writers = new ConcurrentDictionary<int, FastqWriter>();
            foreach (var index in IndexFileNames)
            {
                writers[index.Key] = new FastqWriter(BaseWorkingDir, tileName, index.Value);
                writers[index.Key].Start();
            }

            string r1_line, i1_line;
            using (StreamReader r1 = new StreamReader(read1_File.FullName), i1 = new StreamReader(index1_File.FullName))
            {
                int line_number = -1;
                int index = -1;
                string line_out = "";
                while (r1.Peek() >= 0)
                {
                    if (i1.Peek() == 0)
                        throw new Exception($"Files {read1_File.FullName} and {index1_File.FullName} have different number of lines");

                    r1_line = r1.ReadLine();
                    i1_line = i1.ReadLine();

                    line_number++;
                    if (line_number % 4 == 0)
                    {
                        // Example from fastq file generated by BaseCall: @proc-int-cpf.fastq_N:3_x:2941.16_y:7.81721_PF%:1_Index1:2_ATGC
                        string pattern = @"_x:(?<x>[+-]?([0-9]*[.])?[0-9]+)_y:(?<y>[+-]?([0-9]*[.])?[0-9]+)_PF%:(?<filtered>(1|0))_Index1:(?<index>-?\d+)";
                        Match match = Regex.Match(i1_line, pattern, RegexOptions.IgnoreCase);

                        float x = 0f;
                        float y = 0f;
                        int filtered = 0;
                        if (match.Success && match.Groups.Count > 0)
                        {
                            bool success = true;
                            if (!float.TryParse(match.Groups["x"].Value, out x))
                                success = false;
                            if (!float.TryParse(match.Groups["y"].Value, out y))
                                success = false;
                            if (!int.TryParse(match.Groups["filtered"].Value, out filtered))
                                success = false;
                            if (!int.TryParse(match.Groups["index"].Value, out index))
                                success = false;
                            if (!success)
                                throw new Exception($"File: {index1_File.FullName} Line: \"{i1_line}\" does not match the patern (1)");

                            if (!writers.ContainsKey(index))
                                throw new Exception($"Tile {tileName} invalid index number: {index}");
                        }
                        else
                            throw new Exception($"File: {index1_File.FullName} Line: \"{i1_line}\" does not match the patern (2)");

                        line_out = $"@{Instrument}_{InstrumentId}:{RunId}:{FlowCellId}:{tile.Lane}:{tile.ID}:{x}:{y}: {read}:{filtered}:0:";
                    }
                    else if (line_number % 4 == 1)
                    {
                        line_out += $"{i1_line}+"; // append index sequence
                        writers[index].AddLine(line_out);
                        writers[index].AddLine(r1_line);
                    }
                    else if (line_number % 4 == 2)
                    {
                        writers[index].AddLine("+");
                    }
                    else
                        writers[index].AddLine(r1_line); // read sequence
                }

                foreach (var entry in writers)
                    entry.Value.Stop();
            }
        }

        // Format fastq file Illumina style.  A version for non-indexed Read1.
        public void FormatFastqFileByTile(string tileName)
        {
            const string fileType = "cpf";

            // Parse the tile name
            OLATile tile = new OLATile(tileName);
            if (!tile.Valid)
                throw new Exception($"Tile name {tileName} is invalid.");

            // Open the Read1 file
            FileInfo read1_File = new FileInfo(Path.Combine(BaseWorkingDir, "Read1", "OLA", "proc-" + tileName, "calls", "proc-int-" + fileType + ".fastq"));
            if (!read1_File.Exists)
                throw new Exception($"Could not find file {read1_File.FullName}");

            // TODO: determine read index
            int read = 1;

            // Create a fastq file writer 
            FastqWriter writer = new FastqWriter(BaseWorkingDir, tileName, $"{fileType}.fastq");
            writer.Start();

            string r1_line;
            using (StreamReader r1 = new StreamReader(read1_File.FullName))
            {
                int line_number = -1;
                int index = -1;
                string line_out = "";

                while (r1.Peek() >= 0)
                {
                    r1_line = r1.ReadLine();

                    line_number++;
                    if (line_number % 4 == 0)
                    {
                        // Example from fastq file generated by BaseCall: @proc-int-cpf.fastq_N:3_x:2941.16_y:7.81721_PF%:1_Index1:-1_ATGC
                        string pattern = @"_x:(?<x>[+-]?([0-9]*[.])?[0-9]+)_y:(?<y>[+-]?([0-9]*[.])?[0-9]+)_PF%:(?<filtered>(1|0))_Index1:(?<index>-?\d+)";
                        Match match = Regex.Match(r1_line, pattern, RegexOptions.IgnoreCase);

                        float x = 0f;
                        float y = 0f;
                        int filtered = 0;
                        if (match.Success && match.Groups.Count > 0)
                        {
                            bool success = true;
                            if (!float.TryParse(match.Groups["x"].Value, out x))
                                success = false;
                            if (!float.TryParse(match.Groups["y"].Value, out y))
                                success = false;
                            if (!int.TryParse(match.Groups["filtered"].Value, out filtered))
                                success = false;
                            if (!int.TryParse(match.Groups["index"].Value, out index))
                                success = false;
                            if (!success)
                                throw new Exception($"File: {read1_File.FullName} Line: \"{r1_line}\" does not match the patern (1)");
                        }
                        else
                            throw new Exception($"File: {read1_File.FullName} Line: \"{r1_line}\" does not match the patern (2)");

                        line_out = $"@{Instrument}_{InstrumentId}:{RunId}:{FlowCellId}:{tile.Lane}:{tile.ID}:{x}:{y}: {read}:{filtered}:0:";
                        writer.AddLine(line_out);
                    }
                    else if (line_number % 4 == 2)
                    {
                        writer.AddLine("+");
                    }
                    else
                        writer.AddLine(r1_line); // read sequence
                }

                writer.Stop();
            }
        }
    }
}
