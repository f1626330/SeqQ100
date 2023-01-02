using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.Image.Processing
{
    class TileParameter
    {
        private string tileName;
        Dictionary<int, CycleParameter> _cycles = new Dictionary<int, CycleParameter>();

        public TileParameter(string tileName)
        {
            this.tileName = tileName;
        }

        internal void Add(int cycle, int color, float value)
        {
            for (int i = _cycles.Count; i < cycle; i++)
                _cycles[i + 1] = new CycleParameter(i + 1);

            _cycles[cycle].Add(color, value);
        }

        internal void GetByCycle(int clr, ref Dictionary<int, float> ret)
        {
            foreach (int cycle in _cycles.Keys)
            {
                ret[cycle] = _cycles[cycle].GetColor(clr);
            }
        }
    }
}
