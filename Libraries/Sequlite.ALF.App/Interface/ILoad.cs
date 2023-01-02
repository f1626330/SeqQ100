using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public interface ILoad
    {
        bool UnloadFC();
        bool LoadFC();
        bool LoadReagent();
        bool LoadBuffer();
        bool LoadWaste();
        string FCBarcode { get; }
        string ReagentRFID { get; }
       
    }
}
