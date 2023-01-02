using System.Collections.Generic;
using System.IO;

namespace Sequlite.Image.Processing.utils
{
    public class FileHelper
    {
        public static void WriteAllLinesWithSeparator(string path, IEnumerable<string> lines, string separator)
        {
            using (var writer = new StreamWriter(path))
            {
                foreach (var line in lines)
                {
                    writer.Write(line);
                    writer.Write(separator);
                }
            }
        }
    }
}
