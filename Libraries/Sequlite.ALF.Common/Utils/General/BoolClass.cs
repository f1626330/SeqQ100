using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public class BoolClass
    {
        public BoolClass() : this(false)
        {
        }

        public BoolClass(bool boolValue)
        {
            BoolValue = boolValue;
        }

        public bool BoolValue { get; set; }

    }
}
