using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sequlite.Image.Processing
{
    class DirectoryNumber
    {
        int _tile = 0;
        DirectoryInfo _di = null;
        int _cycle = 0;

        public DirectoryNumber(int num, DirectoryInfo di, int cycle = 0)
        {
            Tile = num;
            BaseDirectory = di;
            _cycle = cycle;
        }

        public int Tile { get => _tile; set => _tile = value; }
        public DirectoryInfo BaseDirectory { get => _di; set => _di = value; }
        public int Cycle { get => _cycle; set => _cycle = value; }

    }
}
