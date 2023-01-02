using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    public class UserLoginViewModel : DialogViewModelBase
    {
        UserPageModel _CurrentUserPageModel;
        UserPageModel _UserModel;
        public UserPageModel UserModel { get => _UserModel; set => SetProperty(ref _UserModel, value); }
        public bool AllowCancel => false;
        string _Message;
        public string Message { get => _Message; set => SetProperty(ref _Message, value); }
        public UserLoginViewModel(UserPageModel userModel, bool isModal = true)
        {
            IsModal = isModal;
            _CurrentUserPageModel = userModel;
            UserModel = new UserPageModel()
            {

                UserAccount = userModel.UserAccount,
                UserName = userModel.UserName,
                Password = "",
            };
        }

        bool _IsLogging = false;
        public bool IsLogging { get => _IsLogging; set => SetProperty(ref _IsLogging, value); }

        protected override  void RunOKCommand(object o)
        {
            
        }

        private ICommand _LoginCmd = null;
        public ICommand LoginCmd
        {
            get
            {
                if (_LoginCmd == null)
                {
                    _LoginCmd = new RelayCommand(o => Login(o), o => UserModel.CanLogin);
                }
                return _LoginCmd;
            }
        }

        async void Login(object o)
        {
            IsLogging = true;
            await Task.Run(() =>
            {
                UserModel.Login();
            });
            IsLogging = false;
            if (UserModel.IsLoggedIn)
            {
                _CurrentUserPageModel.Password = UserModel.Password;
                RequestClose();
                //Logger.Log($"User {UserModel.UserName} has been logged in");
                //OnUserLoggedInStatusChanged?.Invoke(this, new UserLoginStatusEventArgs());// { IsLoggedIn = true , UserLevel = UserModel.AuthenticationLevel});
            }

        }
    }
}
