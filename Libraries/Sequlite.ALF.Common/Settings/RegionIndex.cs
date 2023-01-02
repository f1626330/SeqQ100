using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    
    public struct RegionIndex
    {
        //region.Lane, region.Column, region.Row 
        public Tuple<int, int, int> Index { get; set; }
        //public RegionIndex()
        //{
        //    Index = Tuple.Create(0, 0, 0);
        //}

        public RegionIndex(int[] it)
        {
            Index = Tuple.Create(0, 0, 0);
            if (it.Length >= 3)
            {
                Index = Tuple.Create(it[0], it[1], it[2]);
            }
        }

        public override string ToString()
        {
            return Index.ToString();
        }
    }
}
