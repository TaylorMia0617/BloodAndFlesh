using System;
using System.Collections.Generic;
using Unity.UOS.Auth;
using Unity.UOS.Insight.Service;

namespace Unity.UOS.Insight
{
    public partial class InsightSDK
    {
        private static bool _isLoggedIn;

        private static bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                if (!_initialized)
                {
                    _operationCache.Enqueue(() => { IsLoggedIn = value; });
                    return;
                }

                if (!_enabled)
                    return;

                _isLoggedIn = value;
            }
        }

        /// <summary>
        /// Set the User ID in current SDK config.
        /// </summary>
        /// <param name="userId">User ID to set</param>
        public static void SetUserId(string userId)
        {
            if (!_initialized)
            {
                _operationCache.Enqueue(() => SetUserId(userId));
                return;
            }

            if (!_enabled)
                return;

            AnalyticsService.Login(userId);
        }

        /// <summary>
        /// Unset the User ID in current SDK config.
        /// </summary>
        public static void UnsetUserId()
        {
            if (!_initialized)
            {
                _operationCache.Enqueue(UnsetUserId);
                return;
            }

            if (!_enabled)
                return;

            AnalyticsService.Logout();
        }

        // set the user id from SDK that is independent on AuthTokenManager.
        public static void SetAccount(string account)
        {
            if (IsLoggedIn)
                return;
            SetUserId(account);
        }

        // set the user id after AuthTokenManager.SaveToken and report login event
        public static void LoginByTokenInfo(TokenInfo tokenInfo, string loginFunction)
        {
            var superProperties = new Dictionary<string, object>()
            {
                ["login_session_id"] = Guid.NewGuid().ToString()
            };

            string accountId;
            if (string.IsNullOrEmpty(tokenInfo.PersonaId))
            {
                accountId = tokenInfo.UserId;
            }
            else
            {
                accountId = tokenInfo.PersonaId;
                superProperties["passport_user_id"] = tokenInfo.UserId;
            }

            SetSuperProperties(superProperties);
            SetUserId(accountId);
            ReportEvent("login", ("login_function", loginFunction));
            IsLoggedIn = true;
        }

        /// <summary>
        /// Set the User ID in current config and report `login` event. Use it if you have your own login process.
        /// </summary>
        /// <param name="account">User ID to set</param>
        public static void LoginByAccount(string account)
        {
            // remove passport preset properties
            UnsetSuperProperties("passport_user_id");
            SetSuperProperties(new Dictionary<string, object>()
            {
                ["login_session_id"] = Guid.NewGuid().ToString(),
            });
            SetUserId(account);
            ReportEvent("login", ("login_function", "LoginByAccount"));
            IsLoggedIn = true;
        }

        /// <summary>
        /// Clear the User ID in current config and report `logout` event.
        /// </summary>
        public static void Logout()
        {
            ReportEvent("logout");
            UnsetUserId();
            UnsetSuperProperties("login_session_id", "passport_user_id");
            IsLoggedIn = false;
        }

        public static event Action OnApplicationQuitCallback;

        private void OnApplicationQuit()
        {
            OnApplicationQuitCallback?.Invoke();
            // logout should be the last of all remaining reports
            Logout();
            Flush();
        }

        private void OnDisable()
        {
            Flush();
        }

        private void OnDestroy()
        {
            Flush();
        }
    }
}