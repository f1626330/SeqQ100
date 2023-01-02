using System;
using System.Globalization;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;

namespace IndexMerge
{
    class Logger
    {
        public Logger(string path)
        {
            FilePath = path;
        }

        public void Log(string msg)
        {
            using (var fs = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                using (StreamWriter w = new StreamWriter(fs))
                {
                    w.WriteLine($"{DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss.fff]", CultureInfo.InvariantCulture)} {msg}");
                }
            }
        }

        string FilePath;
    }

    class Program
    {
        static string IndexMergeLogFileName => "IndexMerge.log";

        static int Main(string[] args)
        {
            //using (StreamWriter w = File.AppendText("C:\\bin\\IndexMerge\\IndexMerge.log"))
            //{
            //    w.Write("\r\nLog Entry : ");
            //    w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
            //    w.WriteLine("  :");

            //    foreach (var item in args)
            //    {
            //        w.WriteLine(item.ToString());
            //    }
            //}

            var rootCommand = new RootCommand
            {
                new Option<bool>("--index",
                    description: "Specify whether indexing is required before merging"),
                new Option<string>("--baseWorkingDir",
                    description: "Specify base working directory"),
                new Option<long>("--runId",
                    description: "Specify run id"),
                new Option<string>("--instrument",
                    description: "Specify instrument name"),
                new Option<string>("--instrumentId",
                    description: "Specify instrument id"),
                new Option<string>("--flowCellId",
                    description: "Specify flow cell id"),
            };

            rootCommand.Description = "Console App to index, format, and merge fastq files.";

            rootCommand.Handler = CommandHandler.Create<bool/*index or not*/, string/*working dir*/, long/*run id*/, string/*instrument*/, string/*instrument id*/, string/*flow cell id*/>(Execute);

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        static public void Execute(bool index, string baseWorkingDir, long runId, string instrument, string instrumentId, string flowCellId)
        {
            Logger = new Logger(Path.Combine(baseWorkingDir, IndexMergeLogFileName));

            Logger.Log($"index: {index}");
            Logger.Log($"baseWorkingDir: {baseWorkingDir}");
            Logger.Log($"runId: {runId}");
            Logger.Log($"instrument: {instrument}");
            Logger.Log($"instrumentId: {instrumentId}");
            Logger.Log($"flowCellId: {flowCellId}");

            IndexerMerger im = new IndexerMerger(baseWorkingDir, runId, instrument, instrumentId, flowCellId){ Logger = Logger};

            try
            {
                im.Run(index);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
                Logger.Log("IndexMerge.exe ends with error code 1");
            }

            Logger.Log("IndexMerge.exe ends with error code 0");
        }

        static Logger Logger;
    }
}


