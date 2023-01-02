//-----------------------------------------------------------------------------
// Copyright 2016 (c) BGI.  All Rights Reserved.
// Confidential and proprietary works of BGI.
//-----------------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public static class Assert
    {
        public static void IsTrue(bool condition, string fmtMsg, params object[] args)
        {
            IsTrue(condition, string.Format(fmtMsg, args));
        }

        public static void IsTrue(bool condition, string msg)
        {
            if (condition == false)
            {
                throw new Exception(msg);
            }
        }
    }

    public class SmartCsvOutputFile
    {
        #region Constants

        public const string OLD_FILE_TIMESTAMP_FMT = "yyyy-MM-dd_HHmmss";
        public const string EXCEL_TIMESTAMP_FMT = "yyyy-MM-dd HH:mm:ss.fff";

        private const string ErrMessage = @"Error during '{0}' for CSV file '{1}'";

        public static readonly string CRLF = Environment.NewLine;

        #endregion Constants

        #region Properties

        public bool IsOpen { get; private set; }
        public uint RowCnt { get; private set; }
        public bool AutoFlush { get; set; } = true;

        #endregion Properties

        public string FileDescription { get; set; }

        #region Variables

        private string _fullPath = null;
        private string _tempPath = null;
        private FileMode _fileMode;
        private bool _needToWriteHeader = true;
        private StreamWriter _csvWriter = null;
        private bool _err = false;

        private string _tempExtension = ".tmp";

        private StringBuilder _csvHeaderRow = new StringBuilder(1024);
        private StringBuilder _csvDataRow = new StringBuilder(1024);
        private StringBuilder _csvComments = new StringBuilder(1024);

        private uint _resizeCnt = 0;    // number of StringBuilder resize operations
        private int _HistoryCount = 3; // Number of history files to maintain
        private string _Loggername; // This is not used because BGI.Common.Logger reference would cause circular reference.

        #endregion Variables

        #region Ctors

        public SmartCsvOutputFile(string fileDescription, string loggerName = null, int historyCount = 3)
        {
            Assert.IsTrue(string.IsNullOrEmpty(fileDescription) == false, "Null or empty FileDescription");

            FileDescription = fileDescription;
            _fileMode = FileMode.CreateNew;
            _fullPath = null;
            _needToWriteHeader = true;
            RowCnt = 0;
            _Loggername = loggerName;
            _HistoryCount = historyCount;
        }

        #endregion Ctors

        #region Public Methods

        public override string ToString()
        {
            return $"{FileDescription} {_fullPath}";
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool PathInit(params string[] pathParts)
        {
            // This method assembles pathParts into a single path
            // The output returns true if it was successful
            //Assert.IsTrue(_fullPath == null, "Full path is NOT null");
            _fullPath = null;

            _fullPath = pathParts[0];

            for (int i = 1; i < pathParts.Length; i++)
            {
                string pathPart = pathParts[i];

                if ((pathPart == null) || (pathPart.Length == 0))
                {
                    continue;
                }
                try
                {
                    _fullPath = Path.Combine(_fullPath, pathPart);
                }
                catch (Exception ex)
                {
                    // logErr(ex, "path combine");
                    _err = true;
                    throw ex;
                    //return false;
                }
            }

            try
            {
                _fullPath = Path.GetFullPath(_fullPath);
            }
            catch (Exception ex)
            {
                // logErr(ex, "Get_fullPath");
                _err = true;
                throw ex;
                //return false;
            }

            return true;
        }

        public bool Exists()
        {
            Assert.IsTrue(_fullPath == null, "Full path is null");

            return File.Exists(_fullPath);
        }

        public static bool CreateDir(string dirPath)
        {
            if (!Directory.Exists(dirPath))
            {
                try
                {
                    Directory.CreateDirectory(dirPath);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Open()
        {
            if (IsOpen)
            {
                /*
                 * Already open - ignore
                 */
                return true;
            }

            return open();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool OpenForAppend(params string[] pathParts)
        {
            _fileMode = FileMode.Append;
            _tempExtension = ".csv";

            return Open(pathParts);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Open(params string[] pathParts)
        {
            // Assemble all of the pathParts into a single path

            if (!PathInit(pathParts))
            {
                return false;
            }

            return open();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool OpenForTruncate(params string[] pathParts)
        {
            if (!PathInit(pathParts))
                return false;

            if(!File.Exists(_fullPath))
            {
                _fileMode = FileMode.CreateNew;
            }
            else
            {
                _fileMode = FileMode.Truncate;
            }
                
            _tempExtension = ".csv";

            return Open(pathParts);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            Close(/*keepFile*/true);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close(bool keepFile)
        {
            string closingPath = _fullPath;

            if (_csvWriter == null)
            {
                return;
            }

            #region Flush and Close File

            try
            {
                _csvWriter.Flush();
                _csvWriter.Close();
                _fullPath = null;

                //log("{0,-80} closed - '{1}'", _fileDescription, _tempPath);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception)
            {
                //logErr(ex, "file close");
                return;
            }
            finally
            {
                _csvWriter = null;
                IsOpen = false;
            }

            #endregion Flush and Close File

            #region Delete File If Empty or KeepFile Is False

            FileInfo tempFileInfo = new FileInfo(_tempPath);

            if ((keepFile == false) || (tempFileInfo.Length == 0))
            {
                try
                {
                    File.Delete(_tempPath);

                    //log("{0,-80} deleted - '{1}'", _fileDescription, _tempPath);
                }
                catch (Exception)
                {
                    //logErr(ex, "deleting file");
                }

                return;
            }

            #endregion Delete File If Empty or KeepFile Is False

            if (_tempExtension == Path.GetExtension(closingPath))
            {
                return; // never had a temporary extension - no need to rename it
            }

            // Save the tmp file as output file and save history
            if (_HistoryCount > 0)
            {
                if (File.Exists(closingPath))
                {
                    int cnt = 0;

                    renameOld:
                    try
                    {
                        File.Move(closingPath, Path.ChangeExtension(closingPath, ".old." + DateTime.Now.ToString(OLD_FILE_TIMESTAMP_FMT) + ".csv"));
                    }
                    catch (Exception)
                    {
                        //logErr(ex, "rename to .old");

                        if (++cnt > 3) // give up after 3 retries
                            throw new Exception("Could not save history file for file being saved.");
                       
                        Task.Delay(1000); // wait 1 seconds and try again

                        goto renameOld;
                    }
                }
            }
            else
            {
                try
                {
                    File.Delete(closingPath);
                }
                catch (Exception ex)
                {
                    throw new Exception(String.Format("Failed to delete old file '{0}' before saving new file.", closingPath), ex);
                }
            }

            // Now save the file with proper name
            try
            {
                File.Move(_tempPath, closingPath);
            }
            catch (Exception ex)
            {
                throw new Exception(String.Format("Failed to save new file '{0}'.", closingPath), ex);
            }
        }

        public bool WriteDataRow()
        {
            if (_needToWriteHeader)
            {
                if (writeHeaderRow() == false)
                {
                    return false;
                }
            }

            return WriteLine(_csvDataRow);
        }

        public bool WriteLine(StringBuilder sbLine)
        {
            bool OK = WriteLine(sbLine.ToString());

            sbLine.Length = 0;

            RowCnt++;

            return OK;
        }

        public bool WriteLine(string line)
        {
            //if ((_err == true) || (IsOpen == false))
            //{
            //    return false; // to simplify caller's logic
            //}

            Assert.IsTrue(_err == false, "WriteLine failed. _err is true");
            Assert.IsTrue(IsOpen == true, "WriteLine failed. IsOpen is false");

            Assert.IsTrue(line != null, "full path is null");
            Assert.IsTrue(_csvWriter != null, "CSV writer is null");

            try
            {
                _csvWriter.WriteLine(line);
            }
            catch (Exception ex)
            {
                //logErr(ex, "WriteLine");
                _err = true;
                try
                {
                    //_csvWriter.Flush();       //commented by shulin 208-1-19 14:00, becuase it may cause a IOException when the csv file that is being written was not found.
                    _csvWriter.Close();
                }
                catch
                {
                }
                finally
                {
                    _csvWriter = null;
                }
                throw ex;
                //return false;
            }

            return true;
        }

        public void AddComments(string fmtComments, params object[] args)
        {
            _csvComments.AppendLine("# " + string.Format(fmtComments, args));
        }

        public void AddHeaderField(string hdrField)
        {
            addField(hdrField, _csvHeaderRow);
        }

        public void AddField(object field)
        {
            AddField(field, /*digits*/0, /*digitsValid*/false);
        }

        public void AddField(object field, int digits, bool digitsValid)
        {
            if (field is double)
            {
                if (digitsValid)
                {
                    AddField((double)field, digits);
                }
                else
                {
                    AddField((double)field);
                }
            }
            else if (field is int) { AddField((int)field); }
            else if (field is char) { AddField((char)field); }
            else if (field is bool) { AddField((bool)field); }
            else if (field is DateTime) { AddField((DateTime)field); }
            else
            {
                addField((field ?? String.Empty).ToString(), _csvDataRow);
            }
        }

        public void AddField(string field)
        {
            addField(field, _csvDataRow);
        }

        public void AddField(double field)
        {
            addField(field.ToString(), _csvDataRow);
        }

        public void AddField(double field, int digits)
        {
            addField(field.ToString("F" + digits), _csvDataRow);
        }

        public void AddField(int field)
        {
            addField(field.ToString(), _csvDataRow);
        }

        public void AddField(char field)
        {
            addField(field.ToString(), _csvDataRow);
        }

        public void AddField(bool field)
        {
            addField(field.ToString(), _csvDataRow);
        }

        public void AddField(DateTime field)
        {
            addField(field.ToString(EXCEL_TIMESTAMP_FMT), _csvDataRow);
        }

        public void DiscardDataRow()
        {
            _csvDataRow.Length = 0;
        }

        #endregion Public Methods

        #region Private Methods

        private bool open()
        {
            Assert.IsTrue(_fullPath != null, "No specified path for the CSV");
            Assert.IsTrue(_csvWriter == null, "No valid CSV writer");

            try
            {
                _tempPath = Path.ChangeExtension(_fullPath, _tempExtension);

                string dirPath = Path.GetDirectoryName(_tempPath);

                if (CreateDir(dirPath) == false)
                {
                    return false;
                }

                if (File.Exists(_tempPath) && (_fileMode == FileMode.CreateNew))
                {
                    File.Delete(_tempPath);
                }

                if (_fileMode == FileMode.Append)
                {
                    if (File.Exists(_tempPath))
                    {
                        // Don't need to write a header
                        _needToWriteHeader = false;
                    }
                    else
                    {
                        // Need to write a header
                        _needToWriteHeader = true;
                    }
                }

                _csvWriter = new StreamWriter(new FileStream(_tempPath, _fileMode, FileAccess.Write, FileShare.Read), Encoding.UTF8);
                _csvWriter.AutoFlush = AutoFlush;

                //log("{0,-80} opened {1,-9} autoflush {2,-5} - '{3}'", _fileDescription, _fileMode, _autoFlush, _tempPath);

                IsOpen = true;
            }
            catch (Exception)
            {
                //logErr(ex, "file open");
                return false;
            }

            return true;
        }

        private void addField(string field, StringBuilder csvRow)
        {
            int capacity = _csvDataRow.Capacity;

            if (csvRow.Length > 0)
            {
                csvRow.Append(',');
            }

            // field contains character ',', append quote the field by '"'
            if (field != null && field.Contains(","))
            {
                field = "\"" + field + "\"";
            }

            csvRow.Append(field);

            if (csvRow.Capacity != capacity)
            {
                _resizeCnt++;
            }
        }

        private bool writeHeaderRow()
        {
            _needToWriteHeader = false;

            bool OK = true;

            if (_csvComments.Length > 0)
            {
                _csvComments.AppendLine("#");

                OK = OK && WriteLine(_csvComments);
            }

            OK = OK && WriteLine(_csvHeaderRow);

            return OK;
        }

        #endregion Private Methods
    }
}