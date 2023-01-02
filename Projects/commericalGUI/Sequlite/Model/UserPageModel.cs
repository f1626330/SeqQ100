using Sequlite.ALF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using AuthenticationEnum = Sequlite.ALF.Common.AccessRightEnum;

namespace Sequlite.UI.Model
{
   
    public class UserAccount
    {
        public string UserName { get; }
        public string Password { get;  }
        public AuthenticationEnum AuthenticationLevel { get; }
        public UserAccount(string userName, string password, AuthenticationEnum authenticationLevel)
        {
            UserName = userName;
            Password = password;
            AuthenticationLevel = authenticationLevel;
        }
    }

    public class UserPageModel : ModelBase
    {
        public IUser UserAccount { get; set; }
        private static Dictionary<string, bool> _SessionIdList = new Dictionary<string, bool>();
        AuthenticationEnum _AuthenticationLevel;
        public AuthenticationEnum AuthenticationLevel { get => _AuthenticationLevel; set => SetProperty(ref _AuthenticationLevel, value); }
        string _UserName;
        public string UserName
        {
            get => _UserName;
            set
            {
                if (SetProperty(ref _UserName, value))
                {
                    LoginError = "";
                    CheckCanLogin();
                }
            }
        }

        string _password;
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    LoginError = "";
                    CheckCanLogin();
                }
            }
        }

        void CheckCanLogin() => CanLogin = UserName != "" && Password != "" && LoginError == "";
       
        bool _CanLogin;
        public bool CanLogin
        {
            get => _CanLogin;

            set
            {
                SetProperty(ref _CanLogin, value);
                CommandManager.InvalidateRequerySuggested();
            }
            
        }


        string _Email;
        public string Email
        {
            get => _Email;
            set
            {
                if (_Email != value)
                {
                    _Email = value;
                    OnPropertyChanged(nameof(Email));
                }
            }
        }

        bool _IsLoggedIn = false;
        public bool IsLoggedIn
        {
            get => _IsLoggedIn;
            set
            {
                SetProperty(ref _IsLoggedIn, value, nameof(IsLoggedIn));
            }
        }

        string _LoginError = "";
        public string LoginError { 
            get => _LoginError;
            set
            {
                if (SetProperty(ref _LoginError, value))
                {
                    if (_LoginError != "")
                    {
                        CanLogin = false;
                    }
                }
            }
        }

       
        public void Login()
        {
            try
            {
                CanLogin = false;
                bool isLoggedIn = false;
                UserLogin u = UserAccount.Login(UserName, Password);
                if (u != null)
                {
                    isLoggedIn = true;
                    AuthenticationLevel = u.AccessRight;
                    Email = u.UserInfo.Email;
                }
                
                IsLoggedIn = isLoggedIn;
                if (!isLoggedIn)
                {
                    LoginError = "Login failed: Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                
                IsLoggedIn = false;
                LoginError = $"Login failed: {ex.Message} : {ex.StackTrace}";
            }
            finally
            {
                CanLogin = true;
            }
        }

        public void Logout()
        {
            UserName = "";
            Password = "";
            IsLoggedIn = false;
            LoginError = "";
            Email = "";
            AuthenticationLevel = AuthenticationEnum.User;
        }

        public string CurrentSessionId { get;  set; }
        public string GetNewSessionId()
        {
            string str ;
            do
            {
                str = DateTime.Now.ToString("yyMMdd-HHmmss");
            }
            while (_SessionIdList.ContainsKey(str));
            _SessionIdList.Add(str, true);
            
            return str;
        }
    }
}
