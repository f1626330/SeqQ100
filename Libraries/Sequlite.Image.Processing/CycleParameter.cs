using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sequlite.Image.Processing
{
    class CycleParameter
    {
        private int cycle;
        Dictionary<int, float> _colorVals = new Dictionary<int, float>();

        public CycleParameter(int cycle)
        {
            this.cycle = cycle;
        }

        internal void Add(int color, float value)
        {
            if (!_colorVals.ContainsKey(color))
                _colorVals[color] = value;
        }

        internal float GetColor(int clr)
        {
            if (_colorVals.Count < 1)
                return 0;
            if (clr >= _colorVals.Count)
                return _colorVals[0];
            return _colorVals[clr];
        }
    }
}
