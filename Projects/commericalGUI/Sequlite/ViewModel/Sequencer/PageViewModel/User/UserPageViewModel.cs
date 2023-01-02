using Sequlite.ALF.App;
using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.UI.Resources;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class UserPageViewModel : PageViewBaseViewModel
    {
        //static readonly string LOGIN_TEXT = "Log in";
        //static readonly string LOGOUT_TEXT = "Log out";
        //ISeqLog Logger = SeqLogFactory.GetSeqFileLog("CUI"); //commercial UI
        
        public UserPageModel UserModel { get; }
        //ISeqApp SeqApp { get; }
        public UserPageViewModel(ISequncePageNavigator _PageNavigator, ISeqApp seqApp, IDialogService dialogs) : base(seqApp,_PageNavigator, dialogs)
        {
            Description = Descriptions.UserPage_Description;
            PageNavigator.CanMoveToNextPage = false;
            SeqApp = seqApp;
            UserModel = (UserPageModel) _PageNavigator.GetPageModel(SequencePageTypeEnum.SequenceWizard);// new UserPageModel() { UserName = "login-name", Password = "password" };
            _PageNavigator.AddPageModel(SequencePageTypeEnum.User, UserModel);
            string sessionId = UserModel.GetNewSessionId();
            UserModel.CurrentSessionId = sessionId;
            SeqApp.UpdateAppMessage($"User: {UserModel.UserName} is logged in");
        }
        public override string DisplayName => Strings.PageDisplayName_User;

        internal override bool IsPageDone()
        {
            return UserModel.IsLoggedIn;
        }

        private string _Instruction = Instructions.UserPage_Instruction;


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

        //string _LoginText = LOGIN_TEXT;
        //public string LoginText
        //{
        //    get
        //    {
        //        return _LoginText;
        //    }
        //    set
        //    {
        //        SetProperty(ref _LoginText, value);
        //    }
        //}

        private ICommand _LoginCmd = null;
        public ICommand LoginCmd
        {
            get
            {
                if (_LoginCmd == null)
                {
                    _LoginCmd = new RelayCommand(o =>Loginout(o), o => CanLogin);
                }
                return _LoginCmd;
            }
        }

        bool _CanLogin = true;
        public bool CanLogin
        {
            get
            {
                return _CanLogin;
            }
            set
            {
                SetProperty(ref _CanLogin, value, nameof(CanLogin), true);
            }
        }

        void Loginout(object o)
        {
            try
            {
                if (UserModel.IsLoggedIn)
                {
                    //LoginText = LOGOUT_TEXT;
                    UserModel.IsLoggedIn = false; //to do perform log out
                    UserModel.Email = "";
                    SeqApp.UpdateAppMessage($"User: {UserModel.UserName} logged out");
                    
                    PageNavigator.CanMoveToNextPage = false;

                }
                else
                {
                    //LoginText = LOGIN_TEXT;
                    UserModel.IsLoggedIn = true; //to do perform log in
                    UserModel.Email = "test@sequlite.com";
                    PageNavigator.CanMoveToNextPage = true;
                    SeqApp.UpdateAppMessage($"User: {UserModel.UserName} logged in");
                    string sessionId = UserModel.GetNewSessionId();
                    UserModel.CurrentSessionId = sessionId;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Failed to login/out a user account with error: {0}", ex.Message));
            }
        }

        public override void OnUpdateCurrentPageChanged()
        {
            PageNavigator.CanMoveToNextPage = IsPageDone();
        }

    }
}
