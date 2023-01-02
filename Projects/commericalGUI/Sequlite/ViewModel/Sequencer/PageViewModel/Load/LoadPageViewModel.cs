using Sequlite.ALF.App;
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
using System.Windows;

namespace Sequlite.UI.ViewModel
{
    public partial class LoadPageViewModel : PageViewBaseViewModel
    {

        //to do: move these strings to resource
        static readonly string LoadFlowCellPageStatusName_Ejected = "Unload Flow Cell";
        static readonly string LoadFlowCellPageStatusName_FlowCellLoaded = "Load Flow Cell";

        static readonly string LoadReagentsPageStatusName_ReagentsLoaded = "Load Reagents";

        static readonly string LoadBufferPageStatusName_BufferLoaded = "Load Buffer Cartridge";

        static readonly string LoadWastePageStatusName_WasteEmptied = "Empty & Load Waste Bottle";
        public ILoad LoadApp { get; }

        public LoadPageModel LoadModel { get; }
        ObservableCollection<PageStatusModel>[] LoadPageStatusListArray { get; }
        string[] _AnimationFileLocations;
        public LoadPageViewModel(ISequncePageNavigator _PageNavigator, ISeqApp seqApp, IDialogService dialogs) : 
            base(seqApp, _PageNavigator, dialogs)
        {
            LoadApp = seqApp.CreateLoadInterface();
            //IsSimulation = Sequlite.UI.Properties.Settings.Default.LoadPageSimulation; //from app.config
            LoadModel = new LoadPageModel() { FCBarcodeId = "", ReagentRFID = "" };
            _PageNavigator.AddPageModel(SequencePageTypeEnum.Load, LoadModel);
            SubpageNames = new string[]
            {
                Strings.PageDisplayName_Load_FlowCell,
                Strings.PageDisplayName_Load_Reagents,
                Strings.PageDisplayName_Load_Buffer,
                Strings.PageDisplayName_Load_Waste
            };
            LoadPageStatusListArray = new ObservableCollection<PageStatusModel>[]
            {
                new ObservableCollection<PageStatusModel>()
                {
                    new PageStatusModel() {Name = LoadFlowCellPageStatusName_Ejected },
                    new PageStatusModel() {Name = LoadFlowCellPageStatusName_FlowCellLoaded }
                },

                new ObservableCollection<PageStatusModel>()
                {
                    new PageStatusModel() {Name = LoadReagentsPageStatusName_ReagentsLoaded },
                },

                    new ObservableCollection<PageStatusModel>()
                {
                    new PageStatusModel() {Name = LoadBufferPageStatusName_BufferLoaded },
                },

                new ObservableCollection<PageStatusModel>()
                {
                    new PageStatusModel() {Name = LoadWastePageStatusName_WasteEmptied },
                }
            };

            _AnimationFileLocations = new string[]
                {
                    Path.Combine(SettingsManager.ApplicationMediaDataPath, "Load_FC.mov"), //load flow cell
                    Path.Combine(SettingsManager.ApplicationMediaDataPath, "Load_Reagent_Cartridge.mov"), //load reagents
                    Path.Combine(SettingsManager.ApplicationMediaDataPath, "Load_Buffer.mov"), //to do: add file for buffer load
                    Path.Combine(SettingsManager.ApplicationMediaDataPath, "Load_Waste.mov"), //to do: add file for waste load
                };
            SubpageCount = 4;
            CurrentSubpageIndex = 0;
        }

       

        void SetPageStatusList(int pageIndex)
        {
            StatusList = LoadPageStatusListArray[pageIndex];
        }

        protected override bool  UpdateCurrentPageStatus(string statusName, PageStatusEnum status, string msg = "")
        {
            bool b = base.UpdateCurrentPageStatus(statusName, status, msg);
            if (b)
            {
               
                PageNavigator.CanMoveToNextPage = IsPageDone();
                if (status == PageStatusEnum.Start)
                {
                    CanCancelPage = false;
                    //IsPageBusy = true;
                    PageNavigator.CanMoveToPreviousPage = false;

                }
                else if (status == PageStatusEnum.Complted_Error ||
                    status == PageStatusEnum.Complted_Success ||
                    status == PageStatusEnum.Complted_Warning)
                {
                    CanCancelPage = true;
                    PageNavigator.CanMoveToPreviousPage = true;
                }
            }
            return b;
        }

        void ResetPageStatus(int pageIndex)
        {
            if (pageIndex >= 0 && pageIndex < SubpageCount)
            {
                foreach (var it in LoadPageStatusListArray[pageIndex])
                {
                    it.Status = PageStatusEnum.Reset;
                    it.Message = "";
                }
            }
        }

        public override string DisplayName => Strings.PageDisplayName_Load;


        internal override bool IsPageDone()
        {
            bool b = false;
            if (StatusList != null && StatusList.Count > 0)
            {
                b = true;
                foreach (var it in StatusList)
                {
                    if (it.IsSuccess != true)
                    {
                        b = false;
                        break;
                    }
                }
            }
            return b;
        }

        private string _Instruction = Instructions.LoadPage_Instruction;


        public override string Instruction
        {
            get
            {
                return HtmlDecorator.CSS1 + _Instruction;
            }
            protected set
            {
                _Instruction = value;
                RaisePropertyChanged(nameof(Instruction));
            }
        }

        string[] _subPageDesps = new string[]
        {
            Descriptions.LoadPage_Description_0,
            Descriptions.LoadPage_Description_1,
            Descriptions.LoadPage_Description_2,
            Descriptions.LoadPage_Description_3,
        };
        protected override void SetSubPageDiscription(int pageIndex)
        {
            Description = _subPageDesps[pageIndex];
        }

        public override void OnUpdateCurrentPageChanged()
        {
            PageNavigator.CanMoveToNextPage = IsPageDone();// IsFlowCellLoaded;
        }

        protected override void OnUpdateSubpageInderChanged(int subpageIndex)
        {
            SetPageStatusList(subpageIndex);
            PageNavigator.CanMoveToNextPage = IsPageDone();
            FileLocation = _AnimationFileLocations[subpageIndex];
           
        }
        private async Task<bool> LoadFlowCell()
        {
            bool bLoaded = false;

            bLoaded = await Task<bool>.Run(() =>
            {
                if (IsSimulation)
                {
                    SeqApp.UpdateAppMessage("Loading Flow Cell (sim)");
                    for (int i = 0; i < 50; i++)
                    {
                        Thread.Sleep(100);
                    }
                    LoadModel.FCBarcodeId = "12345678900";
                    SeqApp.UpdateAppMessage("Flow Cell loaded (sim)");
                    return true;
                }
                else
                {
                    bool b = LoadApp.LoadFC();
                    if (b)
                    {
                        LoadModel.FCBarcodeId = LoadApp.FCBarcode;
                    }
                    return b;
                }
            });

            return bLoaded;
        }

        private async Task<bool> Eject()
        {
            bool bEjected = false;
            bEjected = await Task<bool>.Run(() =>
            {
                if (IsSimulation)
                {
                    SeqApp.UpdateAppMessage("Unloading Flow Cell (sim)");
                    for (int i = 0; i < 20; i++)
                    {
                        Thread.Sleep(100);
                    }
                    SeqApp.UpdateAppMessage("Flow Cell unloaded (sim)");
                    return true;
                }
                else
                {
                    bool b = LoadApp.UnloadFC();
                    return b;
                }
            });
            return bEjected;
        }

        private async Task<bool> LoadReagents()
        {
            bool bLoaded = false;
            bLoaded = await Task<bool>.Run(() =>
            {
                if (IsSimulation)
                {
                    SeqApp.UpdateAppMessage("Loading Reagents (sim)");
                    for (int i = 0; i < 50; i++)
                    {
                        Thread.Sleep(100);
                    }
                    LoadModel.ReagentRFID = "1234567890012";
                    SeqApp.UpdateAppMessage("Reagents unloaded (sim)");
                    return true;
                }
                else
                {
                    bool b = LoadApp.LoadReagent();
                    if (b)
                    {
                        LoadModel.ReagentRFID = LoadApp.ReagentRFID;
                    }
                    return b;
                }
            });
            return bLoaded;
        }

        private async Task<bool> LoadBuffer()
        {
            bool bLoaded = false;
            bLoaded = await Task<bool>.Run(() =>
            {
                if (IsSimulation)
                {
                    SeqApp.UpdateAppMessage("Loading Buffer (sim)");
                    for (int i = 0; i < 5; i++)
                    {
                        Thread.Sleep(100);
                    }
                    SeqApp.UpdateAppMessage("Buffer unloaded (sim)");
                    return true;
                }
                else
                {
                    bool b = LoadApp.LoadBuffer();
                    return b;
                }
            });
            return bLoaded;
        }

        private async Task<bool> EmptyWaste()
        {
            bool bEmptied = false;
            bEmptied = await Task<bool>.Run(() =>
            {
                if (IsSimulation)
                {
                    SeqApp.UpdateAppMessage("Emptying Waste (sim)");
                    for (int i = 0; i < 5; i++)
                    {
                        Thread.Sleep(100);
                    }
                    SeqApp.UpdateAppMessage("Waste emptied (sim)");
                    return true;
                }
                else
                {
                    bool b = LoadApp.LoadWaste();
                    return b;
                }
            });
            return bEmptied;
        }


        //return true if don't want  wizard to handle cancel.
        public  override bool CanCelPage()
        {
            //if (IsPageBusy)
            //{
            //    await Task.Run(() =>
            //    {
            //        MessageBoxViewModel msgVm = new MessageBoxViewModel()
            //        {
            //            Message = "Do you want to cancel the current operation",
            //            Caption = "Cancel Current Operation",
            //            Image = MessageBoxImage.Question,
            //            Buttons = MessageBoxButton.YesNo
            //        };

            //        if (msgVm.Show(Dialogs) == MessageBoxResult.Yes)
            //        {
            //            //to do : cancel current loading
            //        }
            //    });

            //}
            return false;
        }
    }

}
