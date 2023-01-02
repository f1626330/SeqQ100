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
        string _Instruction_LoadFlowCell = @"
                <h4><u>Load the Flow Cell:</u></h4>   
                <ol> 
                    <li>Check if space is clear to open</li>
                    <li>Click Eject</li>
                    <li>Open the clamp</li>
                    <li>Remove the old flow cell</li>
                    <li>Place the new flow cell on the surface, properly aligned</li>
                    <li>Close clamp, then click Load</li>
                </ol>";

        public string Instruction_LoadFlowCell
        {
            get
            {
                return _Instruction_LoadFlowCell;
            }
        }

        private ICommand _LoadFlowCellCmd = null;
        public ICommand LoadFlowCellCmd
        {
            get
            {
                if (_LoadFlowCellCmd == null)
                {
                    _LoadFlowCellCmd = new RelayCommand(o => LoadFlowCell(o), o => CanLoadFlowCell);
                }
                return _LoadFlowCellCmd;
            }
        }



        bool _CanLoadFlowCell = false;
        public bool CanLoadFlowCell
        {
            get
            {
                return _CanLoadFlowCell;
            }
            set
            {
                SetProperty(ref _CanLoadFlowCell, value, nameof(CanLoadFlowCell), true);
            }
        }

        async void LoadFlowCell(object o)
        {
            bool bCanLoadFlowCell = CanLoadFlowCell;
            bool bLoaded = false;
            bool bCanEject = CanEject;
            try
            {
                
                CanLoadFlowCell = false;
                IsLoadingFlowCell = true;
                CanEject = false;
                LoadModel.ReagentRFID = "";
                ResetPageStatus(CurrentSubpageIndex + 1);
                UpdateCurrentPageStatus(LoadFlowCellPageStatusName_FlowCellLoaded, PageStatusEnum.Start);
                bLoaded = await LoadFlowCell();
                IsLoadingFlowCell = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to load flow cell with error: {0}", ex.Message));

            }
            finally
            {

                IsLoadingFlowCell = false;
                CanLoadFlowCell = bCanLoadFlowCell;
                CanEject = bCanEject;
                CanCancelPage = true;
                UpdateCurrentPageStatus(LoadFlowCellPageStatusName_FlowCellLoaded, bLoaded?PageStatusEnum.Complted_Success: PageStatusEnum.Complted_Error);
            }
        }

        bool _IsLoadingFlowCell = false;
        public bool IsLoadingFlowCell
        {
            get
            {
                return _IsLoadingFlowCell;
            }
            set
            {
                SetProperty(ref _IsLoadingFlowCell, value, nameof(IsLoadingFlowCell));
            }
        }


        private ICommand _EjectCmd = null;
        public ICommand EjectCmd
        {
            get
            {
                if (_EjectCmd == null)
                {
                    _EjectCmd = new RelayCommand(o => Eject(o), o => CanEject);
                }
                return _EjectCmd;
            }
        }

        bool _CanEject = true;
        public bool CanEject
        {
            get
            {
                return _CanEject;
            }
            set
            {
                SetProperty(ref _CanEject, value, nameof(CanEject), true);
            }
        }

       
        async void Eject(object o)
        {
            bool bEjected = false;
            try
            {
               
                CanEject = false;
                CanLoadFlowCell = false;
                UpdateCurrentPageStatus(LoadFlowCellPageStatusName_FlowCellLoaded, PageStatusEnum.Reset);
                UpdateCurrentPageStatus(LoadFlowCellPageStatusName_Ejected, PageStatusEnum.Start);
                bEjected = await Eject();
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to eject clamp with error: {0}", ex.Message));
            }
            finally
            {
                
                 CanEject = true;
                CanLoadFlowCell = bEjected;
                UpdateCurrentPageStatus( LoadFlowCellPageStatusName_Ejected, bEjected?PageStatusEnum.Complted_Success: PageStatusEnum.Complted_Error);
            }
        }


        //string _FileLocation_FlowCell = Path.Combine(SettingsManager.ApplicationMediaDataPath, "load_flow_cell.mov");
        //public string FileLocation_FlowCell { get => _FileLocation_FlowCell; set => SetProperty(ref _FileLocation_FlowCell, value); }

        //bool _IsPageLoaded_FlowCell = false;
        //public bool IsPageLoaded_FlowCell
        //{
        //    get => _IsPageLoaded_FlowCell;
        //    set => SetProperty(ref _IsPageLoaded_FlowCell, value);
        //}


        //bool _IsPageUnloaded_FlowCell = false;
        //public bool IsPageUnloaded_FlowCell
        //{
        //    get => _IsPageUnloaded_FlowCell;
        //    set => SetProperty(ref _IsPageUnloaded_FlowCell, value);
        //}
    }
}
