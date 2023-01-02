using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public partial class LoadPageViewModel : PageViewBaseViewModel
    {
        string _Instruction_LoadReagents = @"
                <h4><u>Load the Reagent Cartridge:</u></h4>   
                <ol> 
                    <li>Unpack the cartridge</li>
                    <li>Load the template into cartridge</li>
                    <li>Open the left side door & remove the old cartridge</li>
                    <li>Insert the new cartridge & close the door</li>
                    <li>Click Load</li>
                </ol>";

        public string Instruction_LoadReagents
        {
            get
            {
                return _Instruction_LoadReagents;
            }
        }

        private ICommand _LoadReagentsCmd = null;
        public ICommand LoadReagentsCmd
        {
            get
            {
                if (_LoadReagentsCmd == null)
                {
                    _LoadReagentsCmd = new RelayCommand(o => LoadReagents(o), o => CanLoadReagents);
                }
                return _LoadReagentsCmd;
            }
        }



        bool _CanLoadReagents = true;
        public bool CanLoadReagents
        {
            get
            {
                return _CanLoadReagents;
            }
            set
            {
                SetProperty(ref _CanLoadReagents, value, nameof(CanLoadReagents), true);
            }
        }

        async void LoadReagents(object o)
        {
            bool bCanLoadReagents = CanLoadReagents;
            bool bLoaded = false;
            try
            {
                CanCancelPage = false;
                 CanLoadReagents = false;
                IsLoadingReagents = true;

                LoadModel.ReagentRFID = "";
                ResetPageStatus(CurrentSubpageIndex + 1);
                UpdateCurrentPageStatus( LoadReagentsPageStatusName_ReagentsLoaded, PageStatusEnum.Start);
                bLoaded = await LoadReagents();
                IsLoadingReagents = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to load reagents with error: {0}", ex.Message));

            }
            finally
            {
                CanLoadReagents = bCanLoadReagents;
                IsLoadingReagents = false;
                CanCancelPage = true;
                UpdateCurrentPageStatus( LoadReagentsPageStatusName_ReagentsLoaded, bLoaded ? PageStatusEnum.Complted_Success : PageStatusEnum.Complted_Error);

            }
        }

        bool _IsLoadingReagents = false;
        public bool IsLoadingReagents
        {
            get
            {
                return _IsLoadingReagents;
            }
            set
            {
                SetProperty(ref _IsLoadingReagents, value, nameof(IsLoadingReagents));
            }
        }

        //string _FileLocation_Reagents = Path.Combine(SettingsManager.ApplicationMediaDataPath, "load_wash_cartridge.gif");
        //public string FileLocation_Reagents { get => _FileLocation_Reagents; set => SetProperty(ref _FileLocation_Reagents, value); }

        //bool _IsPageLoaded_Reagents = false;
        //public bool IsPageLoaded_Reagents
        //{
        //    get => _IsPageLoaded_Reagents;
        //    set => SetProperty(ref _IsPageLoaded_Reagents, value);
        //}


        //bool _IsPageUnloaded_Reagents = false;
        //public bool IsPageUnloaded_Reagents
        //{
        //    get => _IsPageUnloaded_Reagents;
        //    set => SetProperty(ref _IsPageUnloaded_Reagents, value);
        //}

    }

}
