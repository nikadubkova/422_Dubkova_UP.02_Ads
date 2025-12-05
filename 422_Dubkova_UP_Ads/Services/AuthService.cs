using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _422_Dubkova_UP_Ads.Services
{
    public static class AuthService
    {
        public static user CurrentUser { get; private set; }
        public static event Action<user> UserLoggedIn;
        public static event Action UserLoggedOut;

        /// <summary>
        /// Попытка синхронного логина. Возвращает true при успехе.
        /// </summary>
        public static bool Login(string login, string password)
        {
            using (var context = new Entities())
            {
                var user = context.user
                    .FirstOrDefault(u => u.user_login == login && u.user_password == password);

                if (user != null)
                {
                    CurrentUser = user;
                    UserLoggedIn?.Invoke(user);
                    return true;
                }
                return false;
            }
        }

        public static void Logout()
        {
            CurrentUser = null;
            UserLoggedOut?.Invoke();
        }
    }
}
