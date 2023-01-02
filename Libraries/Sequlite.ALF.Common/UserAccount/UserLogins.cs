using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    
    public enum AccessRightEnum
    {
        [Display(Name = "None", Description = "Invalid Access Right")]
        None = 0,

        [Display(Name = "User", Description = "Normal User, can run sequence and view data" )]
        User = 1,

        [Display(Name = "Admin", Description = "Administrator, can create a user account" )]
        Admin = 2,

        [Display(Name = "Tech", Description = "Technician and field service, can be an administrator and run Engineering service")]
        Tech = 4,

        [Display(Name = "Master", Description = "Account with all privileges, can install/update Sequlite Software")]
        Master = 8,
    }

    public class UserLogin : BaseModel
    {
        public UserLogin()
        {
            UserInfo = new UserInfo();
        }

        string _UserName;
        public string UserName { get=> _UserName; set => SetProperty(ref _UserName, value); }

        AccessRightEnum _AccessRight;
        public AccessRightEnum AccessRight { get=> _AccessRight; set=>SetProperty(ref _AccessRight, value); }
        
        string _Password;
        public string Password { get=> _Password; set=>SetProperty(ref _Password, value); }

        UserInfo _UserInfo;
        public UserInfo UserInfo { get=> _UserInfo; set=>SetProperty(ref _UserInfo, value); }

        public override string this[string columnName]
        {
            get
            {
                string error = string.Empty;
                switch (columnName)
                {
                    
                    case "UserName":
                        {
                            if (string.IsNullOrEmpty(UserName) || UserName.Length < 6)
                            {

                                error = "User login name must be at least 6 characters long.";
                            }
                            
                        }
                        break;
                    case "Password":
                        {
                            if (string.IsNullOrEmpty(Password) || Password.Length < 8)
                            {
                                error = "Passwords must be at least 8 characters long";
                            }
                        }
                        break;
                   

                }

                UpdateErrorBits(columnName, !string.IsNullOrEmpty(error));
                return error;
            }
        }
    }
    public class UserInfo : BaseModel
    {
        string _LastName;
        public string LastName { get => _LastName; set => SetProperty(ref _LastName, value); }

        string _FirstName;
        public string FirstName { get => _FirstName; set => SetProperty(ref _FirstName, value); }

        string _Email;
        public string Email { get => _Email; set => SetProperty(ref _Email, value); }

        string _PhoneNumber;
        public string PhoneNumber { get => _PhoneNumber; set => SetProperty(ref _PhoneNumber, value); }

        string _Company;
        public string Company { get => _Company; set => SetProperty(ref _Company, value); }

        string _Address;
        public string Address { get => _Address; set => SetProperty(ref _Address, value); }

        string _WeChatID;
        public string WeChatID { get => _WeChatID; set => SetProperty(ref _WeChatID, value); }
        public override string this[string columnName]
        {
            get
            {
                string error = string.Empty;
                switch (columnName)
                {

                    case "LastName":
                        {
                            if (string.IsNullOrEmpty(LastName))
                            {

                                error = "Last name cannot be empty.";
                            }

                        }
                        break;
                    case "FirstName":
                        {
                            if (string.IsNullOrEmpty(FirstName))
                            {

                                error = "First name cannot be empty.";
                            }

                        }
                        break;
                    case "Email":
                        {
                            if (string.IsNullOrEmpty(Email))
                            {

                                error = "Email cannot be empty.";
                            }
                            else if (!IsValidEmail(Email))
                            {
                                error = "Input a valid email address.";
                            }
                        }
                        break;

                }

                UpdateErrorBits(columnName, !string.IsNullOrEmpty(error));
                return error;
            }
        }

        bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

    }

    public interface IUser
    {
        bool AddALogin(UserLogin it);
        UserLogin GetUser(string userName);
        UserLogin Login(string userName, string password);
        bool ChangePassword(string userName, string newPassword);
        List<string> FindUsers(string partialUserName, AccessRightEnum searchAccessRight);
        bool UpdateUserProfile(UserLogin it);
        bool SetUserRetired(string userName);
    }
}
