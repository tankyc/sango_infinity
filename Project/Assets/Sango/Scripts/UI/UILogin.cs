using Sango.Manager;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using System.Collections;
using System;

namespace Sango.UI
{
    /// <summary>
    /// 游戏开始界面
    /// </summary>
    public class UILogin : UGUIWindow
    {
        public InputField account;
        public InputField password;

        public Button loginBtn;
        public GameObject alreadyLogin;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void OnOpen()
        {
            if (GameLogin.Instance.IsLogin)
            {
                alreadyLogin.SetActive(true);
                account.SetTextWithoutNotify(GameLogin.Instance.username);
                password.SetTextWithoutNotify(GameLogin.Instance.password);
            }
            else
            {
                account.SetTextWithoutNotify("");
                password.SetTextWithoutNotify("");
                alreadyLogin.SetActive(false);
            }
        }

        public void Login()
        {
            string accountStr = account.text;
            string passwordStr = password.text;
            GameLogin.Instance.Login(accountStr, passwordStr, () =>
            {
                OnOpen();
            });
        }

        public void OnBack()
        {
            Close();
        }

    }
}
