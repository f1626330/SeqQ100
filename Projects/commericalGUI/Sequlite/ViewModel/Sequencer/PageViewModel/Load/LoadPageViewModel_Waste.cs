using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public partial class LoadPageViewModel : PageViewBaseViewModel
    {
        string _Instruction_EmptyWaste = @"
                <h4><u>Load the Waste bottle:</u></h4>   
                <ol> 
                    <li>Carefully remove the waste bottle</li>
                    <li>Empty the waste bottle</li>
                    <li>Load the waste bottle</li>
                    <li>Click Done</li>
                </ol>";

        public string Instruction_EmptyWaste
        {
            get
            {
                return _Instruction_EmptyWaste;
            }
        }

        private ICommand _WasteEmptiedCmd = null;
        public ICommand WasteEmptiedCmd
        {
            get
            {
                if (_WasteEmptiedCmd == null)
                {
                    _WasteEmptiedCmd = new RelayCommand(o => WasteEmptied(o), o => CanWasteEmptied);
                }
                return _WasteEmptiedCmd;
            }
        }

        bool _CanWasteEmptied = true;
        public bool CanWasteEmptied
        {
            get
            {
                return _CanWasteEmptied;
            }
            set
            {
                SetProperty(ref _CanWasteEmptied, value, nameof(CanWasteEmptied), true);
            }
        }


        async void WasteEmptied(object o)
        {
            bool bSetWasteEmptied = false;
            try
            {
                CanWasteEmptied = false;
                
                UpdateCurrentPageStatus(LoadWastePageStatusName_WasteEmptied, PageStatusEnum.Start);
                bSetWasteEmptied = await EmptyWaste();
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to set waste emptied with error: {0}", ex.Message));
            }
            finally
            {
                CanWasteEmptied = true;
                
                UpdateCurrentPageStatus(LoadWastePageStatusName_WasteEmptied, bSetWasteEmptied ? PageStatusEnum.Complted_Success : PageStatusEnum.Complted_Error);
            }
        }
    }
}
