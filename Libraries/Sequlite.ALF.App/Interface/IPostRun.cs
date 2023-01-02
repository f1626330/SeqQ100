using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.App
{
    public enum WashOption
    {
        Prerun,
        Maintenance,
        ManualPostWash,
        PostWash
    }
    public class RunWashParams
    {
        public string SessionId { get; set; }
        public WashOption SelectedWashOption { get; set; }
    }

    public interface IPostRun
    {
        bool UnloadCart();
        bool LoadWash();
        bool RunWash(RunWashParams parametrs);
        bool CancelWashing();
    }
}
