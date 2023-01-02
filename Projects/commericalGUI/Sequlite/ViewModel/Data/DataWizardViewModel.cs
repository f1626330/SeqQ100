using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Sequlite.UI.ViewModel
{
    public class DataWizardViewModel : WizardBaseViewModel
    {
        DataOptionTypeEnum DataOption { get; set; }
        public UserPageModel UserModel { get; set; }
        public DataWizardViewModel(ISeqApp seqApp, IDialogService dialogs) : base(seqApp, dialogs)
        {
           
        }

        public void CreateDataWizardPages(DataOptionTypeEnum dataOption)
        {
            DataOption = dataOption;
            CreatePages(SeqApp);
            if (Pages.Count > 0)
            {
                CurrentPage = Pages[0];
            }
        }

        protected override void CreatePages(ISeqApp seqApp)
        {
            switch (DataOption)
            {
                case DataOptionTypeEnum.View:
                    {
                        var pages = new List<PageViewBaseViewModel>();
                        pages.Add(new DataViewFileLocationViewModel(seqApp, UserModel,this, DialogService));
                        pages.Add(new DataViewDisplayViewModel(seqApp, this, DialogService));
                        _pages = new ReadOnlyCollection<PageViewBaseViewModel>(pages);
                    }
                    break;
                case DataOptionTypeEnum.Process:
                    {
                        var pages = new List<PageViewBaseViewModel>();
                        pages.Add(new DataProcessFileLocationViewModel(seqApp, UserModel, this, DialogService));
                        pages.Add(new DataProcessRunViewModel(seqApp, this, DialogService));
                        _pages = new ReadOnlyCollection<PageViewBaseViewModel>(pages);
                    }
                    break;
                case DataOptionTypeEnum.Transfer:
                    {
                        var pages = new List<PageViewBaseViewModel>();
                        pages.Add(new DataTransferDefaultViewModel(seqApp, this, DialogService));
                        _pages = new ReadOnlyCollection<PageViewBaseViewModel>(pages);
                    }
                    break;
                case DataOptionTypeEnum.Delete:
                    {
                        var pages = new List<PageViewBaseViewModel>();
                        pages.Add(new DataDeleteDefaultViewModel(seqApp, this, DialogService));
                        _pages = new ReadOnlyCollection<PageViewBaseViewModel>(pages);
                    }
                    break;
            }
        }

        protected override bool ConfirmCancel()
        {
            bool b = false;
            string title = "";
            
            switch (DataOption)
            {
                case DataOptionTypeEnum.View:
                    {
                        title = "Data View";
                    }
                    break;
                case DataOptionTypeEnum.Process:
                    {
                        title = "Data Process";
                    }
                    break;
                case DataOptionTypeEnum.Transfer:
                    {
                        title = "Data Transfer";
                    }
                    break;
                case DataOptionTypeEnum.Delete:
                    {
                        title = "Data Delete";
                    }
                    break;
            }
            MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                Message = $"Do you want to exit {title}?",
                Caption = $"Exit {title}",
                Image = MessageBoxImage.Question,
                Buttons = MessageBoxButton.YesNo,
                IsModal = true,
            };

            if (msgVm.Show(DialogService.Dialogs) == MessageBoxResult.Yes)
            {
                b = true;
            }
            return b;
        }

        override public bool IsOnLastPage
        {
            get
            {
                if (CurrentPage != null )
                {
                    if (!CurrentPage.HasSubpages)
                    {
                        return (CurrentPageIndex == Pages.Count - 1);
                    }
                    else
                    {
                        return CurrentPage.IsOnLastSubpage;
                    }
                }
                else 
                {
                    return true; 
                }
            }
        }
    }
}
