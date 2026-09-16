using TKNewtonsoft.Json;
using Sango.Mod;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;

namespace Sango.Core
{
    public class GameLogin : Singleton<GameLogin>
    {
        public string url = "http://139.155.98.66:8080/";

        [Header("登录凭据")]
        public string username;
        public string password;
        public string token;
        public string saveFile;

        System.Action onLoginDone;

        public bool IsLogin => CloudSaveClient.HasToken;

        public void Init()
        {
            saveFile = Path.ContentRootPath + "/account";
            Load();
        }

        public void Login(string account, string password, System.Action action)
        {
            this.username = account;
            this.password = password;
            onLoginDone = action;
            App.Instance.StartCoroutine(Login());
        }

        void Save()
        {
            if (File.Exists(saveFile))
                File.Delete(saveFile);
            File.WriteAllText(saveFile, $"{username};{password}");
        }

        void Load()
        {
            if (File.Exists(saveFile))
            {
                string c = File.ReadAllText(saveFile);
                string[] cs = c.Split(';');
                username = cs[0];
                password = cs[1];
                Login(username, password, null);
            }
        }

        IEnumerator Login()
        {
            yield return CloudSaveClient.Login(username, password,
            resp =>
            {
                Sango.Log.Info($"登录成功! 用户: {resp.user.username} (id={resp.user.id})");
                Save();
                onLoginDone?.Invoke();
            },
            err =>
            {
                Sango.Log.Error("登录失败");
                //Debug.LogWarning("登录失败: " + err + " -> 尝试自动注册...");
                //StartCoroutine(CloudSaveClient.Register(username, password,
                //    r2 => Debug.Log($"自动注册并登录成功: {r2.user.username}"),
                //    e2 => Debug.LogError("注册失败: " + e2)));
            });
        }


        IEnumerator UploadSave(int slot)
        {
            if (!CloudSaveClient.HasToken) { Debug.LogError("未登录"); yield break; }

            string localPath = Sango.Core.Player.Player.GetSaveFileName(slot);
            if (!File.Exists(localPath))
            {
                Debug.LogWarning($"槽位{slot}本机没有存档文件，无法上传: {localPath}");
                yield break;
            }

            Debug.Log($"上传槽位{slot}: {localPath} ...");
            yield return CloudSaveClient.UploadToSlotFromFile(
                slot,
                localPath,
                $"save_slot_{slot}.dat",
                $"槽位{slot} @ {DateTime.Now:yyyy-MM-dd HH:mm}", // 备注里写个时间，方便看新旧
                resp => Debug.Log($"槽位{slot}上传成功 (云端 id={resp.id}, {CloudSaveClient.FormatSize(resp.size)})"),
                e => Debug.LogError($"槽位{slot}上传失败: {e}"));
        }

        IEnumerator DownloadSave(int slot)
        {
            if (!CloudSaveClient.HasToken) { Debug.LogError("未登录"); yield break; }

            string dest = Sango.Core.Player.Player.GetSaveFileName(slot);
            if (File.Exists(dest)) System.IO.File.Copy(dest, dest + ".bak", true); // 覆盖前备份

            Debug.Log($"下载槽位{slot} -> {dest} ...");
            yield return CloudSaveClient.DownloadSlotToFile(slot, dest,
                () => Debug.Log($"槽位{slot}下载完成"),
                e => Debug.LogError($"槽位{slot}下载失败(可能为空槽): {e}"));
        }

    }

    internal class LoginResp
    {
        public string token;
    }
}
