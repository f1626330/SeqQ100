using Sequlite.ALF.Common;
using Sequlite.UI.Model;
using Sequlite.WPF.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Sequlite.UI.ViewModel
{

    public enum AccountSettingOptionTypeEnum
    {
        [Display(Name = "View/Edit Account", Description = "View or edit a user account")]
        ViewEdit,

        [Display(Name = "Create Account", Description = "Create a new account")]
        Create,

        [Display(Name = "Delete Account", Description = "Delete a existing account")]
        Delete,

        [Display(Name = "Change Password", Description = "Change Password")]
        ChangePassword,

        [Display(Name = "Update Profile", Description = "Update User Profile")]
        UpdateProfile,

        [Display(Name = "", Description = "Search User Account")]
        Search,

        [Display(Name = "Go Back", Description = "Back to account setting menu")]
        Back
    }

    public enum AcoountSettingStateEnum
    {
        None,
        Main,
        ViewingAccount,
        CreatingAccount,
        EditingAccount, //update profile
        ChangingPassoword,
        DeletingAccount,
    }

    
    public class AccessRight : ModelBase
    {
        AccessRightEnum _Value;
        public AccessRightEnum Value { get => _Value; set => SetProperty(ref _Value, value); }

        string _Name;
        public string Name { get => _Name; set => SetProperty(ref _Name, value); }

        string _Dscription;
        public string Dscription { get => _Dscription; set => SetProperty(ref _Dscription, value); }

        public override string ToString()
        {
            return Dscription;
        }
    }

    public class AccountItem
    {
        public AccountItem() { }
        public string UserName { get; set; }
        public AccessRightEnum AccessRight { get; set; }
    }

    public class AccountSettingMessage : ModelBase
    {
        string _Message;
        public string Message { get => _Message; set => SetProperty(ref _Message, value); }
        bool _IsError;
        public bool IsError { get => _IsError; set => SetProperty(ref _IsError, value); }
    }

    public class UserAccountSettingViewModel : ViewBaseViewModel, IDataErrorInfo
    {
       
        UserLogin _PreviousUserLoginInfo;
        UserLogin _UserLoginInfo;
        public UserLogin UserLoginInfo { get => _UserLoginInfo; set => SetProperty(ref _UserLoginInfo, value); }
        UserLogin CurrentUserLoginInfo { get; }

        bool _CanClose;
        public bool CanClose { get => _CanClose; set => SetProperty(ref _CanClose, value); }
        AcoountSettingStateEnum _AcoountSettingState;
        public AcoountSettingStateEnum AcoountSettingState { get => _AcoountSettingState;
            set
            {
                if (SetProperty(ref _AcoountSettingState, value))
                {
                    CanClose = _AcoountSettingState == AcoountSettingStateEnum.Main;
                }
            }
        }

        bool _IsRunningAccountAction;
        public bool IsRunningAccountAction
        {
            get => _IsRunningAccountAction;
            set
            {
                SetProperty(ref _IsRunningAccountAction, value);
                CanContinueCommand = !_IsRunningAccountAction;
            }
        }
        
        bool _IsAccountActionDone;
        public bool IsAccountActionDone { get => _IsAccountActionDone; set => SetProperty(ref _IsAccountActionDone, value); }

        AccountSettingMessage _AccountSettingUpdateMessage = null;
        public AccountSettingMessage AccountSettingUpdateMessage { get => _AccountSettingUpdateMessage; set => SetProperty(ref _AccountSettingUpdateMessage, value); }
        UserPageModel UserModel { get; }

        string _NewPassword2;
        public string NewPassword2 { get => _NewPassword2; set => SetProperty(ref _NewPassword2, value); }

        public bool VerifyingPassword { get; set; }

        ObservableCollection<AccessRight> _AccessRights;
        public ObservableCollection<AccessRight> AccessRights { get => _AccessRights; set => SetProperty(ref _AccessRights, value); }
        public UserAccountSettingViewModel(UserPageModel userModel)
        {
            UserModel = userModel;
            UserAccounts = GetAccountsFromHistory(UserModel.AuthenticationLevel);
            UserLoginInfo = new UserLogin();
            CurrentUserLoginInfo = UserModel.UserAccount.GetUser(UserModel.UserName);
            CurrentUserLoginInfo.Password = UserModel.Password;
            BuidlAcessRightList(UserModel.AuthenticationLevel);
            AddUserAccountNameToList(UserModel.UserName, UserModel.AuthenticationLevel);
            UserAccount = UserModel.UserName;
            AcoountSettingState = AcoountSettingStateEnum.Main;
        }

        void BuidlAcessRightList(AccessRightEnum loggedinRight)
        {
            AccessRightEnum[] allRights = (AccessRightEnum[])Enum.GetValues(typeof(AccessRightEnum));
            List<AccessRight> list = new List<AccessRight>();
            int loggedinRightInt = (int)loggedinRight;
            foreach (var it in allRights)
            {
                if ( (int)it <= loggedinRightInt && it != AccessRightEnum.None)
                {
                    list.Add(new AccessRight() { Value = it, Name = it.GetDisplayAttributesFrom().Name, Dscription = it.GetDisplayAttributesFrom().Description });
                }
            }
            AccessRights = new ObservableCollection<AccessRight>(list);
        }

        static Dictionary<string, AccessRightEnum> _UserAccountHistories = new Dictionary<string, AccessRightEnum>();
        ObservableCollection<AccountItem> _UserAccounts;
        public ObservableCollection<AccountItem> UserAccounts
        {
            get => _UserAccounts;
            set => SetProperty(ref _UserAccounts, value);
        }

        string _UserAccount;
        public string UserAccount
        {
            get => _UserAccount;
            set
            {
                if (SetProperty(ref _UserAccount, value))
                {
                    UpdateUserLoginName(_UserAccount);
                }

            }
        }

        ICommand _ContinueCommand;
        public ICommand ContinueCommand
        {
            get
            {
                if (_ContinueCommand == null)
                    _ContinueCommand = new RelayCommand(
                        (o) => this.RunContinueCommand(o),
                        (o) => this.CanContinueCommand);

                return _ContinueCommand;
            }
        }

        bool _CanContinueCommand = true;
        public bool CanContinueCommand
        {
            get
            {
                return _CanContinueCommand;
            }
            set
            {
                _CanContinueCommand = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }


        public string Error
        {
            get { return null; }
        }

        string PasswordVerificationError { get; set; }
        private string VerifyPassword()
        {
            string error = string.Empty;
            if (string.IsNullOrEmpty(UserLoginInfo.Password) && string.IsNullOrEmpty(NewPassword2))
            {
                error = "Password must be filed out";
            }
            else
            {
                if (string.IsNullOrEmpty(UserLoginInfo.Password))
                {
                    error = "Password must be filed out";
                }
                else if (string.IsNullOrEmpty(NewPassword2))
                {
                    error = "Password must be confirmed";
                }
                else if (NewPassword2 != UserLoginInfo.Password)
                {
                    error = "Password and confirmation password do not match";
                }
            }
            PasswordVerificationError = error;
            return error;
        }

        public string this[string columnName]
        {
            get
            {
                string error = string.Empty;
                switch (columnName)
                {
                    case "NewPassword2":
                        {
                            if (VerifyingPassword)
                            {
                                error = VerifyPassword();
                            }
                        }
                        break;
                }
                return error;
            }
        }

        async void  RunContinueCommand(object o)
        {
            AccountSettingOptionTypeEnum OptionType = (AccountSettingOptionTypeEnum)o;
            switch (OptionType)
            {
                case AccountSettingOptionTypeEnum.ViewEdit:
                    if (UserLoginInfo == null || string.IsNullOrEmpty(UserLoginInfo.UserName))
                    {
                        UserLoginInfo = CurrentUserLoginInfo;
                    }
                    AcoountSettingState = AcoountSettingStateEnum.ViewingAccount;
                    AccountSettingUpdateMessage = null;
                    break;

                case AccountSettingOptionTypeEnum.Create: //creating

                    if (AcoountSettingState == AcoountSettingStateEnum.Main)
                    {
                        if (CurrentUserLoginInfo.AccessRight != AccessRightEnum.Master)
                        {
                            RemoveAcessRightFromList(CurrentUserLoginInfo.AccessRight);
                        }
                        _PreviousUserLoginInfo = UserLoginInfo;
                        UserLoginInfo = new UserLogin();
                        UserLoginInfo.Password = "";
                        UserLoginInfo.AccessRight = AccessRightEnum.User;
                        AcoountSettingState = AcoountSettingStateEnum.CreatingAccount;
                        AccountSettingUpdateMessage = null;
                        VerifyingPassword = false;
                        NewPassword2 = "";
                    }
                    else if (AcoountSettingState == AcoountSettingStateEnum.CreatingAccount)
                    {
                        VerifyingPassword = true;
                        RaisePropertyChanged(nameof(NewPassword2));

                        if (string.IsNullOrEmpty(PasswordVerificationError))
                        {
                            IsRunningAccountAction = true;
                            AccountSettingUpdateMessage = null;
                            bool b = await AccountAction(UserLoginInfo, AccountSettingOptionTypeEnum.Create);
                            if (b)
                            {
                                AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Account \"{UserLoginInfo.UserName}\" is created" };
                                IsAccountActionDone = true;
                                AddUserAccountNameToList(UserLoginInfo.UserName, UserLoginInfo.AccessRight);
                            }
                            else
                            {
                                AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Failed to create account \"{UserLoginInfo.UserName}\"", IsError = true };
                            }
                            IsRunningAccountAction = false;
                        }
                        else
                        {
                            AccountSettingUpdateMessage = new AccountSettingMessage() { Message = PasswordVerificationError, IsError = true };
                            VerifyingPassword = false;
                        }
                    }
                    break;
                case AccountSettingOptionTypeEnum.Delete: //deleting
                    {
                        if (AcoountSettingState == AcoountSettingStateEnum.DeletingAccount)
                        {
                            IsRunningAccountAction = true;
                            AccountSettingUpdateMessage = null;
                            MessageBoxViewModel msgVm = new MessageBoxViewModel()
                            {
                                Message = $"Please confirm to delete the user account with\nUser Name: {UserLoginInfo.UserName}" +
                                    $"\nEmail: {UserLoginInfo.UserInfo.Email}\nLast Name: {UserLoginInfo.UserInfo.LastName}" +
                                    $"\nFirst Name: {UserLoginInfo.UserInfo.FirstName}",
                                Caption = $"Confirm User Account Deletion",
                                Image = MessageBoxImage.Question,
                                Buttons = MessageBoxButton.YesNo,
                                IsModal = true,
                            };

                            if (msgVm.Show(DialogService.Dialogs) == MessageBoxResult.Yes)
                            {
                                bool b = await AccountAction(UserLoginInfo, AccountSettingOptionTypeEnum.Delete);
                                if (b)
                                {
                                    AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Account \"{UserLoginInfo.UserName}\" is deleted" };
                                    IsAccountActionDone = true;
                                    RemoveUserAccountNameFromList(UserLoginInfo.UserName);
                                }
                                else
                                {
                                    AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Failed to delete account \"{UserLoginInfo.UserName}\"", IsError = true };
                                }
                            }
                            else
                            {
                                AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Aborted deleting account \"{UserLoginInfo.UserName}\"", IsError = true };
                            }
                            IsRunningAccountAction = false;
                        }
                        else if (AcoountSettingState == AcoountSettingStateEnum.Main)
                        {
                            RemoveUserAccountNameFromList(CurrentUserLoginInfo.UserName);
                            if (UserAccounts.Count <= 0)
                            {
                                UserLoginInfo.AccessRight = AccessRightEnum.None;
                                UserAccount = "";
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(UserAccount) ||
                                    string.Compare(UserAccount, CurrentUserLoginInfo.UserName, true) == 0)
                                {
                                    UserAccount = UserAccounts[0].UserName;
                                }
                            }

                            AccountSettingUpdateMessage = null;
                            AcoountSettingState = AcoountSettingStateEnum.DeletingAccount;
                        }
                    }
                    break;
                case AccountSettingOptionTypeEnum.Back:
                    {  
                        bool updateCurrentLogin = IsAccountActionDone && 
                            (AccountSettingUpdateMessage?.IsError == false) &&
                            string.Compare(UserLoginInfo.UserName, CurrentUserLoginInfo.UserName, true) == 0;
                        
                        if (AccountSettingUpdateMessage?.IsError == true || !IsAccountActionDone)
                        {
                            AccountSettingUpdateMessage = null;
                        }
                        IsAccountActionDone = false;
                        switch (AcoountSettingState)
                        {
                            case AcoountSettingStateEnum.ChangingPassoword:
                                AcoountSettingState = AcoountSettingStateEnum.ViewingAccount;
                                if (updateCurrentLogin)
                                {
                                    UserLoginViewModel vm = new UserLoginViewModel(UserModel);
                                    vm.Message = "Current user password has been changed, please log in again.";
                                    vm.Show(this.DialogService.Dialogs);
                                }
                                break;
                            case AcoountSettingStateEnum.EditingAccount:
                                AcoountSettingState = AcoountSettingStateEnum.ViewingAccount;
                                if (updateCurrentLogin)
                                {
                                    UserModel.Email = UserLoginInfo.UserInfo.Email;
                                }
                                break;
                            case AcoountSettingStateEnum.CreatingAccount:
                                AddAcessRightToList(CurrentUserLoginInfo.AccessRight);
                                UserLoginInfo = _PreviousUserLoginInfo;
                                AcoountSettingState = AcoountSettingStateEnum.Main;
                                break;
                            case AcoountSettingStateEnum.DeletingAccount:
                                AddUserAccountNameToList(CurrentUserLoginInfo.UserName, CurrentUserLoginInfo.AccessRight);
                                AcoountSettingState = AcoountSettingStateEnum.Main;
                                break;
                            case AcoountSettingStateEnum.ViewingAccount:
                                AcoountSettingState = AcoountSettingStateEnum.Main;
                                break;
                        }
                    }
                    break;

                case AccountSettingOptionTypeEnum.ChangePassword:
                    if (AcoountSettingState == AcoountSettingStateEnum.ChangingPassoword)
                    {
                        VerifyingPassword = true;
                        RaisePropertyChanged(nameof(NewPassword2));

                        if (string.IsNullOrEmpty(PasswordVerificationError))
                        {
                            IsRunningAccountAction = true;
                            AccountSettingUpdateMessage = null;
                            bool b = await AccountAction(UserLoginInfo, AccountSettingOptionTypeEnum.ChangePassword);
                            if (b)
                            {
                                AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Account password for \"{UserLoginInfo.UserName}\" is changed" };
                                IsAccountActionDone = true;
                            }
                            else
                            {
                                AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Failed to change account password for \"{UserLoginInfo.UserName}\"", IsError = true };
                            }
                            IsRunningAccountAction = false;
                        }
                        else
                        {
                            AccountSettingUpdateMessage = new AccountSettingMessage() { Message = PasswordVerificationError, IsError = true };
                            VerifyingPassword = false;
                        }
                    }
                    else if (AcoountSettingState == AcoountSettingStateEnum.ViewingAccount)
                    {
                        AccountSettingUpdateMessage = null;
                        VerifyingPassword = false;
                        AcoountSettingState = AcoountSettingStateEnum.ChangingPassoword;
                    }
                    break;
                case AccountSettingOptionTypeEnum.UpdateProfile:
                    if (AcoountSettingState == AcoountSettingStateEnum.EditingAccount)
                    {
                        IsRunningAccountAction = true;
                        AccountSettingUpdateMessage = null;
                        bool b = await AccountAction(UserLoginInfo, AccountSettingOptionTypeEnum.UpdateProfile);
                        if (b)
                        {
                            AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Account profile for \"{UserLoginInfo.UserName}\" is updated" };
                            IsAccountActionDone = true;
                        }
                        else
                        {
                            AccountSettingUpdateMessage = new AccountSettingMessage() { Message = $"Failed to update account profile for \"{UserLoginInfo.UserName}\"", IsError = true };
                        }
                        IsRunningAccountAction = false;
                    }
                    else if (AcoountSettingState == AcoountSettingStateEnum.ViewingAccount)
                    {
                        AccountSettingUpdateMessage = null;
                        AcoountSettingState = AcoountSettingStateEnum.EditingAccount;
                    }
                    break;

                case AccountSettingOptionTypeEnum.Search:
                    {
                        SearchAccountViewModel vm = new SearchAccountViewModel(UserModel, true);
                        vm.Show(this.DialogService.Dialogs);


                        UpdateUserLoginName(vm.SetectedUserAccount, true);
                    }
                    break;
            }
        }


        void UpdateUserLoginName(string newUserName, bool checkAccessRight = false)
        {
            if (!string.IsNullOrEmpty(newUserName))
            {
                if (string.Compare(newUserName, UserLoginInfo.UserName, true) != 0)
                {
                    UserLogin userLoginInfo = UserModel.UserAccount.GetUser(newUserName);
                    if (userLoginInfo != null)
                    {
                        if (!checkAccessRight ||
                            (CurrentUserLoginInfo.AccessRight == AccessRightEnum.Master || (int)userLoginInfo.AccessRight < (int)CurrentUserLoginInfo.AccessRight))
                        {
                            AddUserAccountNameToList(newUserName, userLoginInfo.AccessRight);
                            UserAccount = newUserName;
                            UserLoginInfo = userLoginInfo;
                        }
                    }
                }
            }
        }
        void AddUserAccountNameToList(string userLoginName, AccessRightEnum accessRightEnum)
        {

            if (!string.IsNullOrEmpty(userLoginName) )
            {
                
                if (UserAccounts.FirstOrDefault(i => i.UserName == userLoginName) == default(AccountItem))
                {
                    UserAccounts.Add(new AccountItem() { UserName = userLoginName, AccessRight = accessRightEnum });
                }
                if (!_UserAccountHistories.ContainsKey(userLoginName))
                {
                    _UserAccountHistories.Add(userLoginName, accessRightEnum);
                }
            }
        }

        void RemoveUserAccountNameFromList(string userLoginName)
        {
            if (!string.IsNullOrEmpty(userLoginName))
            {
                var it = UserAccounts.FirstOrDefault(i => i.UserName == userLoginName);
                if (it != default(AccountItem))
                {
                    UserAccounts.Remove(it);
                }
            }
        }

        ObservableCollection<AccountItem> GetAccountsFromHistory(AccessRightEnum currentAccessRight)
        {
            ObservableCollection<AccountItem> list = new ObservableCollection<AccountItem>();
            foreach (var it in _UserAccountHistories)
            {
                if (currentAccessRight == AccessRightEnum.Master || (int)it.Value < (int)currentAccessRight)
                {
                    list.Add(new AccountItem() { UserName = it.Key, AccessRight = it.Value });
                }
            }
            return list;
        }

        public void OnClose()
        {
            foreach (var it in UserAccounts)
            {
                if (!_UserAccountHistories.ContainsKey(it.UserName))
                {
                    _UserAccountHistories.Add(it.UserName, it.AccessRight);
                }
            }
        }

        void RemoveAcessRightFromList(AccessRightEnum accessRight)
        {
            var it = AccessRights.FirstOrDefault(i => i.Value == accessRight);
            if (it != default(AccessRight))
            {
                AccessRights.Remove(it);
            }
        }

        void AddAcessRightToList(AccessRightEnum accessRight)
        {
            var it = AccessRights.FirstOrDefault(i => i.Value == accessRight);
            if (it == default(AccessRight))
            {
                AccessRights.Add(new AccessRight() { Value = accessRight, 
                    Name = accessRight.GetDisplayAttributesFrom().Name, Dscription = accessRight.GetDisplayAttributesFrom().Description });
            }
        }

        private  Task<bool> AccountAction(UserLogin userLogin, AccountSettingOptionTypeEnum actionType)
        {
            var result =   Task<bool>.Run(() =>
            {
                bool b = false;
                try
                {
                    switch (actionType)
                    {
                        case AccountSettingOptionTypeEnum.ChangePassword:
                            b = UserModel.UserAccount.ChangePassword(userLogin.UserName, userLogin.Password);
                            break;
                        case AccountSettingOptionTypeEnum.Create:
                            b = UserModel.UserAccount.AddALogin(userLogin);
                            break;
                        case AccountSettingOptionTypeEnum.UpdateProfile:
                            b = UserModel.UserAccount.UpdateUserProfile(userLogin);
                            break;
                        case AccountSettingOptionTypeEnum.Delete:
                            b = UserModel.UserAccount.SetUserRetired(userLogin.UserName);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError($"Failed to do account action {Enum.GetName(typeof(AccountSettingOptionTypeEnum), actionType)} with error {exp.Message}");
                }
                return  b;
            });
            
            return result;
        }

    }
}
