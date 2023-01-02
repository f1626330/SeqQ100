using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.Image.Processing
{
    public class FileManipulator
    {
        private const string dropDir = "../";

        static public int MoveFile(string fileSpec, string targetSubDir, DirectoryInfo sourceDir, bool move = true)
        {
            int numFilesMoved = 0;
            foreach (FileInfo fi in sourceDir.GetFiles(fileSpec))
            {
                try
                {
                    string newName;
                    if (targetSubDir.Contains(dropDir))
                    {
                        string subDir = targetSubDir.Replace(dropDir, "");
                        newName = Path.Combine(sourceDir.Parent.FullName, subDir, fi.Name);
                    }
                    else if (targetSubDir.Contains('/'))
                    {
                        newName = Path.Combine(sourceDir.FullName, targetSubDir);
                    }
                    else
                    {
                        newName = Path.Combine(sourceDir.FullName, targetSubDir, fi.Name);
                    }
                    if (File.Exists(newName))
                        File.Delete(newName);
                    if (move)
                        fi.MoveTo(newName);
                    else
                        fi.CopyTo(newName);
                    numFilesMoved++;
                }
                catch 
                {
                    throw;
                }
            }

            return numFilesMoved;
        }
    }
}
