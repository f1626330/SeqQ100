using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    public class ComponentConnectionEventArgs : EventArgs
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public bool IsErrorMessage {get; set;}
    }
}
