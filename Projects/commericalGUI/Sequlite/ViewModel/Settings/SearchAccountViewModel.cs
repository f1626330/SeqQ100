using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{
    class SearchAccountViewModel : DialogViewModelBase
    {
        
        public string SetectedUserAccount { get; private set; }
        IUser _UserAccountInterface;
        UserPageModel _UserModel;
        public SearchAccountViewModel(UserPageModel userModel, bool isModal )
        {
            IsModal = isModal;
            CanOKCommand = false;
            _UserAccountInterface = userModel.UserAccount;
            _UserModel = userModel;
        }

        ObservableCollection<string> _UserAccounts;
        public ObservableCollection<string> UserAccounts
        {
            get =>_UserAccounts;
            set =>SetProperty(ref _UserAccounts, value);
            
        }

        string _SearchUserName;
        public string SearchUserName
        {
            get => _SearchUserName;
            set
            {
                SetProperty(ref _SearchUserName, value);
                if (!string.IsNullOrEmpty(_SearchUserName))
                {
                    CanSearchCommand = true;
                }
                else
                {
                    CanSearchCommand = false;
                }
            }
        }

        string _UserAccount;
        public string UserAccount 
        {
            get => _UserAccount;
            set
            {
                SetProperty(ref _UserAccount, value);
                if (!string.IsNullOrEmpty(_UserAccount))
                {
                    CanOKCommand = true;
                }
                else
                {
                    CanOKCommand = false;
                }
            }
        }

        protected override void RunOKCommand(object o)
        {
            SetectedUserAccount = UserAccount;
            Close();
        }

        void RunSearchCommand()
        {
            AccessRightEnum searchAccessRight;
            switch (_UserModel.AuthenticationLevel)
            {
                case AccessRightEnum.Master:
                    searchAccessRight = AccessRightEnum.Master;
                    break;
                case AccessRightEnum.Tech:
                    searchAccessRight = AccessRightEnum.Admin;
                    break;
                case AccessRightEnum.Admin:
                    searchAccessRight = AccessRightEnum.User;
                    break;
                default:
                    searchAccessRight = AccessRightEnum.None;
                    break;
            }

            if (searchAccessRight != AccessRightEnum.None)
            {
                List<string> ls = _UserAccountInterface.FindUsers(SearchUserName, searchAccessRight);
                UserAccounts = new ObservableCollection<string>(ls);
            }
        }

        ICommand _SearchCommand;
        public ICommand SearchCommand
        {
            get
            {
                if (_SearchCommand == null)
                    _SearchCommand = new RelayCommand(
                        (o) => this.RunSearchCommand(),
                        (o) => this.CanSearchCommand);

                return _SearchCommand;
            }
        }

        bool _CanSearchCommand = true;
        public bool CanSearchCommand
        {
            get => _CanSearchCommand; set => SetProperty(ref _CanSearchCommand, value);
        }
    }
}
