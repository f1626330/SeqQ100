using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.Image.Processing
{
    class RunTimeParameter
    {
        private string name;

        Dictionary<string, TileParameter> _tiles = new Dictionary<string, TileParameter>();

        public RunTimeParameter(string name)
        {
            this.name = name;
        }

        internal void Add(string tileName, int cycle, int color, float value)
        {
            if (!_tiles.ContainsKey(tileName))
                _tiles[tileName] = new TileParameter(tileName);
            _tiles[tileName].Add(cycle, color, value);
        }

        internal void GetByCycle(string tileName, int clr, ref Dictionary<int, float> ret)
        {
            if (_tiles.ContainsKey(tileName))
                _tiles[tileName].GetByCycle(clr, ref ret);
        }

        internal List<string> AvailableTiles()
        {
            List<string> ret = new List<string>();
            foreach (string tile in _tiles.Keys)
                ret.Add(tile.ToString());
            return ret;
        }
    }
}
