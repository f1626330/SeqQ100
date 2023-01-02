//-----------------------------------------------------------------------------
// Copyright 2016-2021 (c) BGI.  All Rights Reserved.
// Confidential and proprietary works of BGI.
//-----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;

namespace Sequlite.ALF.Common
{
    public static class MetricsCommon
    {
        private static ISeqLog _StaticLogger = SeqLogFactory.GetSeqFileLog("MetricsCommon");
        //private static Logger _StaticLogger = LogMgr.GetLogger("MetricsCommon");

        public static void CreateCVS(ref SmartCsvOutputFile csvWriter, string filePath, string fileName, List<string> headers,
            FileMode fileMode = FileMode.Append)
        {
            if (csvWriter == null)
            {
                // Code to turn on csv file output here
                csvWriter = new SmartCsvOutputFile(fileName, /*loggerName*/null);
                lock (csvWriter)
                {
                    try
                    {
                        switch (fileMode)
                        {
                            case FileMode.Append:
                                csvWriter.OpenForAppend(filePath, fileName);
                                break;

                            case FileMode.Truncate:
                                csvWriter.OpenForTruncate(filePath, fileName);
                                break;

                            default:
                                csvWriter.Open(filePath, fileName);
                                break;
                        }

                        csvWriter.AutoFlush = true;

                        foreach (string header in headers)
                        {
                            csvWriter.AddHeaderField(header);
                        }
                    }
                    catch (Exception ex)
                    {
                        csvWriter.Close();
                        _StaticLogger.LogError($"create CSV file: {fileName} failed. {ex}");
                    }
                }
            }
            else
            {
                _StaticLogger.LogError($"create CSV file: {fileName} failed, csvWriter is not null");
            }
        }

        public static void UpdateCSVFile(ref SmartCsvOutputFile csvWriter, List<object> datas)
        {
            if (csvWriter != null)
            {
                lock (csvWriter)
                {
                    try
                    {
                        foreach (object data in datas)
                        {
                            csvWriter.AddField(data);
                        }
                        csvWriter.WriteDataRow();
                    }
                    catch (Exception ex)
                    {
                        _StaticLogger.LogError($"Update CSV file: {csvWriter.FileDescription} failed. {ex}");
                        csvWriter.Close();
                    }
                }
            }
        }

        public static void closeCSV(ref SmartCsvOutputFile csvWriter)
        {
            if (csvWriter != null)
            {
                lock (csvWriter)
                {
                    try
                    {
                        csvWriter.Close(true);
                    }
                    catch (Exception ex)
                    {
                        _StaticLogger.LogError($"Close CSV file: {csvWriter.FileDescription} failed. {ex}");
                    }
                }
            }
        }
    }
}