using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Common;

using System.Data.SQLite;
using System.Data.SQLite.EF6;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sequlite.ALF.Common
{
    class UserAccountImpl : DBTableBaseImpl,IUser
    {
        public UserAccountImpl(DatabaseContext db, ISeqLog logger) : base(db, logger)
        {
           
        }

        public  bool AddALogin(UserLogin it)
        {
            bool b = false;
            bool canAddAccount = false;
            Login loginUser = GetLogin(it.UserName);
            if (loginUser != null && !IsValidAccount(loginUser))
            {
                var user = _SqDb.User.Where(u => (u.ID == loginUser.ID)).FirstOrDefault();
                if (user != default(User) )
                {
                    _SqDb.User.Remove(user);
                }
                _SqDb.Login.Remove(loginUser);

                if (SaveChanges())
                {
                    canAddAccount = true;
                }
                else
                {
                    Logger.LogError($"Cannot add user {it.UserName} because it cannot be deleted first");
                }
            }
            else if (loginUser == null)
            {
                canAddAccount = true;
            }

            if (canAddAccount) //!HasUser(it.UserName, _SqDb))
            {
                DateTime dateTime = DateTime.Now;
                User userInfo = new User()
                {
                    LastName = it.UserInfo.LastName,
                    Email = it.UserInfo.Email,
                    FirstName = it.UserInfo.FirstName,
                    UpdateTime = dateTime,
                    Address = it.UserInfo.Address,
                    Company = it.UserInfo.Company,
                    PhoneNumber = it.UserInfo.PhoneNumber,
                    WeChatID = it.UserInfo.WeChatID
                };

                var login = new Login()
                {
                    UserName = it.UserName,
                    AccessRight = (int)it.AccessRight,
                    CreateTime = dateTime,
                    UpdateTime = dateTime,
                    Retired = 0,
                    User = userInfo
                };
                EncryptPasswordAndAccessRight(login, it.Password);
                _SqDb.Login.Add(login);
                b = SaveChanges();
            }
            else
            {
                Logger.LogError($"Cannot add user {it.UserName} since it already exits");
            }
            return b;
        }

        void EncryptPasswordAndAccessRight(Login lg, string password)
        {
            PasswordHashContainer ps = PasswordHashProvider.CreateHash(password);
            byte[] acessLevelByteHash = PasswordHashProvider.CreateHash(lg.AccessRight.ToString(), ps.Salt);
            string hashPassword = ByteConverter.GetHexString(ps.HashedPassword);
            string saltPassword = ByteConverter.GetHexString(ps.Salt);
            string hashAccessRight = ByteConverter.GetHexString(acessLevelByteHash);

            lg.Password1 = saltPassword;
            lg.Password2 = hashPassword;
            lg.AccessRightCode = hashAccessRight;
        }

        

        private bool IsValidAccount(Login it) => it.Retired == 0;
        public List<string> FindUsers(string partialUserName, AccessRightEnum searchAccessRight)
        {
            string upeercaseUser = partialUserName.ToUpper();
            var users = _SqDb.Login.Where(it => it.UserName.ToUpper().Contains(upeercaseUser));
            List<string> list = new List<string>();
            if (users != null)
            {
                foreach (var it in users)
                {
                    if (IsValidAccount(it) && (int)it.AccessRight <= (int)searchAccessRight)
                    {
                        list.Add(it.UserName);
                    }
                }
            }
            return list;
        }
        public UserLogin GetUser(string userName)
        {
            UserLogin userLoggedin = null;
            Login lg = GetLogin(userName);
            if (lg != null) 
            {
               User userProfile = lg.User;
               AccessRightEnum accessRightEnum = (AccessRightEnum)lg.AccessRight;

               UserInfo uInfo = new UserInfo()
                {
                    LastName = userProfile.LastName,
                    FirstName = userProfile.FirstName,
                    Email = userProfile.Email,
                    Address = userProfile.Address,
                    Company = userProfile.Company,
                    PhoneNumber = userProfile.PhoneNumber,
                    WeChatID = userProfile.WeChatID

                };
                userLoggedin = new UserLogin() { UserName = userName, AccessRight = accessRightEnum, UserInfo = uInfo };
            }
            return userLoggedin;
        }

        public  UserLogin Login(string userName, string password)
        {
            //Task<UserLogin> task = Task.Run<UserLogin>(async () => await Login(userName, password, _SqDb));
            //return task.Result;
            return Login(userName, password, _SqDb).Result;
        }

        private Login GetLogin(string userName)
        {
            Login loginUser = null;
            string userNameInUpperCase = userName.ToUpper();
            var user = _SqDb.Login.Where(it => userNameInUpperCase.Equals(it.UserName.ToUpper()));
            Login lg = user.FirstOrDefault(); 
            if (lg != default(Login))
            {
                loginUser = lg;
            }
            return loginUser;
        }

        public bool ChangePassword(string userName, string newPassword)
        {
            bool b = false;
            Login lg = GetLogin(userName);
            if (lg != null) 
            {
                EncryptPasswordAndAccessRight(lg, newPassword);
                lg.UpdateTime = DateTime.Now;
                b = SaveChanges();
            }
            else
            {
                Logger.LogError($"Failed to change user password: User {userName} doesn't exist");
            }
            return b;
        }


        public bool UpdateUserProfile(UserLogin it)
        {
            bool b = false;
            Login lg = GetLogin(it.UserName);
            if (lg != null)
            {
                User userInfo = lg.User;
                userInfo.LastName = it.UserInfo.LastName;
                userInfo.Email = it.UserInfo.Email;
                userInfo.FirstName = it.UserInfo.FirstName;
                userInfo.UpdateTime = DateTime.Now;
                userInfo.Address = it.UserInfo.Address;
                userInfo.Company = it.UserInfo.Company;
                userInfo.PhoneNumber = it.UserInfo.PhoneNumber;
                userInfo.WeChatID = it.UserInfo.WeChatID;

                b = SaveChanges();
            }
            else
            {
                Logger.LogError($"Failed to update user profile: User {it.UserName} doesn't exist");
            }
            return b;
        }

        public bool SetUserRetired(string userName)
        {
            bool b = false;
            Login lg = GetLogin(userName);
            if (lg != null)
            {
                lg.Retired = 1;
                lg.UpdateTime = DateTime.Now;
                b = SaveChanges();
            }

            return b;
        }
        //private  bool HasUser(string userName, DatabaseContext db)
        //{
        //    string userNameInUpperCase = userName.ToUpper();
        //    var user = db.Login.Where(it => userNameInUpperCase.Equals(it.UserName.ToUpper()));
        //    return user.Any();
        //}

        private  async Task<UserLogin> Login(string userName, string password, DatabaseContext db)
        {
            string userNameInUpperCase = userName.ToUpper();
            var user = db.Login.Where(it => userNameInUpperCase.Equals(it.UserName.ToUpper()));
            UserLogin userLoggedin = null;
            Login lg = await user.FirstOrDefaultAsync();
            if (lg != default(Login))
            {
                if (IsValidAccount(lg))
                {
                    byte[] salt = ByteConverter.GetHexBytes(lg.Password1);
                    if (PasswordHashProvider.ValidatePassword(password, salt, ByteConverter.GetHexBytes(lg.Password2)))
                    {
                        AccessRightEnum accessRightEnum = GetMatchedAccessRight(lg.AccessRight, lg.AccessRightCode, salt);
                        User userProfile = lg.User;
                        UserInfo uInfo = new UserInfo()
                        {
                            LastName = userProfile.LastName,
                            FirstName = userProfile.FirstName,
                            Email = userProfile.Email,
                            Address = userProfile.Address,
                            Company = userProfile.Company,
                            PhoneNumber = userProfile.PhoneNumber,
                            WeChatID = userProfile.WeChatID

                        };
                        userLoggedin = new UserLogin() { UserName = userName, Password = password, AccessRight = accessRightEnum, UserInfo = uInfo };
                    }
                    else
                    {
                        Logger.LogError($"Failed to login: Wrong password for User {userName} ");
                    }
                }
                else
                {
                    Logger.LogError($"Failed to login: : User {userName} is invalid (might be deleted)");
                }
            }
            else
            {
                Logger.LogError($"Failed to login: User {userName} doesn't exist");
            }
            return userLoggedin;
        }

        private  AccessRightEnum GetMatchedAccessRight(int accessRight, string accessRightString, byte[] salt)
        {
            if (PasswordHashProvider.ValidatePassword(accessRight.ToString(), salt, ByteConverter.GetHexBytes(accessRightString)))
            {
                return (AccessRightEnum)accessRight;
            }
            else
            {
                return AccessRightEnum.User;
            }
        }
    }
}
