using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public interface IRunSetup
    {
        bool RunSetup();
        bool ValidateSampleSheetData(SampleSheetDataInfo sData, out string errMsg);
    }
}
