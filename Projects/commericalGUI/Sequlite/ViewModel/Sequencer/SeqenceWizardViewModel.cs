using Sequlite.ALF.App;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class SeqenceWizardViewModel : WizardBaseViewModel, ISequncePageNavigator
    {
        #region Constructor
        IDisposable TemperatureSubscriber { get; }
        public TemperatureModel TemperModel { get; }
        public UserPageModel UserModel { get; set; }
        public SeqenceWizardViewModel(ISeqApp seqApp, IDialogService dialogs, UserPageModel userModel) : base(seqApp, dialogs)
        {
            UserModel = userModel;
            AddPageModel(SequencePageTypeEnum.SequenceWizard, userModel);

            TemperModel = new TemperatureModel();
            TemperModel.UpdateTemperarures(SeqApp.GetSystemMonitorInterface().TemperatureData);
            ISystemMonitor systemMonitor = SeqApp.GetSystemMonitorInterface();
            TemperatureSubscriber =  AppObservableSubscriber.Subscribe(systemMonitor.TemperatureMonitor,
                it => TemperatureUpdated(it)
                );

            CurrentPage = Pages[0];
        }

       
        void TemperatureUpdated(TemperatureStatusBase temperStatus)
        {
            Dispatch(() =>
            {
                TemperModel.UpdateTemperarures(temperStatus as TemperatureStatus);
            });
        }

        #endregion // Constructor

        protected override void CreatePages(ISeqApp seqApp)
        {
            
            var pages = new List<PageViewBaseViewModel>();

            pages.Add(new UserPageViewModel(this, seqApp, DialogService));
            pages.Add(new LoadPageViewModel(this, seqApp, DialogService));
            pages.Add(new RunSetupPageViewModel(this, seqApp, DialogService));
            pages.Add(new CheckPageViewModel(this, seqApp, DialogService));
            pages.Add(new SeqencePageViewModel(this, seqApp, DialogService));
            pages.Add(new PostRunPageViewModel(this, seqApp, DialogService));
            pages.Add(new SummaryPageViewModel(this, seqApp, DialogService));
            _pages = new ReadOnlyCollection<PageViewBaseViewModel>(pages);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            AppObservableSubscriber.Unsubscribe(TemperatureSubscriber);
            
        }

        string GetSequencePageTypeEnumName(SequencePageTypeEnum e) => Enum.GetName(typeof(SequencePageTypeEnum), e);
        public ModelBase GetPageModel(SequencePageTypeEnum pageType) 
        {
            string key = GetSequencePageTypeEnumName( pageType);
            return GetPageModel(key);
            
        }

        public void AddPageModel(SequencePageTypeEnum pageType, ModelBase model)
        {
            string key = GetSequencePageTypeEnumName( pageType);
            AddPageModel(key, model);

           
        }

        protected override bool ConfirmCancel()
        {
            bool b = false;


            MessageBoxViewModel msgVm = new MessageBoxViewModel()
            {
                Message = "Do you want to exit sequence?",
                Caption = "Exit Sequence",
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
    }
}