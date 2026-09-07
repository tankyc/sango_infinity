// =============================================================
// CloudSaveClient.cs
// 云存档服务器 Unity 客户端封装（无需第三方库，基于 UnityWebRequest）
//
// 用法:
//   1. 把本文件放入 Assets 任意目录（会自动编译）
//   2. 在任意 MonoBehaviour 中调用协程，例如:
//      StartCoroutine(CloudSaveClient.Login("player1", "mypassword123",
//          resp => { Debug.Log("登录成功 " + resp.user.username); },
//          err  => { Debug.LogError("登录失败: " + err); }));
//
// 依赖: 服务器已兼容 multipart/form-data / urlencoded / JSON 三种格式，
//       Unity 的 WWWForm 默认 multipart 直接可用。
// =============================================================

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// ---------- 与服务器 JSON 精确对应的序列化类 (JsonUtility 字段名区分大小写) ----------
[Serializable]
public class CloudSaveAuthResp
{
    public string token;
    public CloudSaveUser user;
}

[Serializable]
public class CloudSaveUser
{
    public int id;
    public string username;
}

[Serializable]
public class CloudSaveListResp
{
    public CloudSaveSaveEntry[] items;
}

[Serializable]
public class CloudSaveSaveEntry
{
    public int id;
    public string original_name; // 服务器返回 snake_case，需逐字对应
    public long size;
    public string note;
    public long created_at;      // 毫秒时间戳

    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(created_at).LocalDateTime;
}

[Serializable]
public class CloudSaveUploadResp
{
    public int id;
    public string original_name;
    public long size;
    public string note;
}

[Serializable]
public class CloudSaveSlotEntry
{
    public int id;
    public int slot;             // 槽位号
    public string original_name;
    public long size;
    public string note;
    public long created_at;      // 毫秒时间戳

    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(created_at).LocalDateTime;
}

[Serializable]
public class CloudSaveSlotListResp
{
    public CloudSaveSlotEntry[] items;
}

[Serializable]
public class CloudSaveApiError
{
    public string error;
}

public static class CloudSaveClient
{
    public const string BASE_URL = "http://139.155.98.66:8080";
    private const string TOKEN_KEY = "CloudSave_Token";

    // ---------- token 存取（可选：用 PlayerPrefs 持久化登录态） ----------
    public static string Token
    {
        get => PlayerPrefs.GetString(TOKEN_KEY, "");
        set => PlayerPrefs.SetString(TOKEN_KEY, value ?? "");
    }

    public static bool HasToken => !string.IsNullOrEmpty(Token);

    /// <summary>把 "Bearer xxx" 附加到需要鉴权的请求</summary>
    private static void AddAuth(UnityWebRequest req)
    {
        if (!string.IsNullOrEmpty(Token))
            req.SetRequestHeader("Authorization", "Bearer " + Token);
    }

    /// <summary>解析成功/失败回调，返回是否成功</summary>
    private static bool Resolve(UnityWebRequest req, Action<string> onError)
    {
        if (req.result == UnityWebRequest.Result.Success) return true;
        // 服务器错误均为 JSON: {"error":"..."}
        string msg = null;
        try
        {
            var err = JsonUtility.FromJson<CloudSaveApiError>(req.downloadHandler.text);
            msg = err.error;
        }
        catch { /* 忽略非 JSON 错误体 */ }
        onError?.Invoke(msg ?? $"请求失败 (HTTP {(int)req.responseCode})");
        return false;
    }

    // ============================================================
    // 1. 登录
    // ============================================================
    public static IEnumerator Login(string username, string password,
        Action<CloudSaveAuthResp> onSuccess, Action<string> onError)
    {
        var form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        using (var req = UnityWebRequest.Post(BASE_URL + "/login", form))
        {
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            var resp = JsonUtility.FromJson<CloudSaveAuthResp>(req.downloadHandler.text);
            if (resp == null || string.IsNullOrEmpty(resp.token))
            {
                onError?.Invoke("服务器返回数据异常");
                yield break;
            }
            Token = resp.token; // 自动记住登录态
            onSuccess?.Invoke(resp);
        }
    }

    // ============================================================
    // 2. 注册（注册成功即自动登录）
    // ============================================================
    public static IEnumerator Register(string username, string password,
        Action<CloudSaveAuthResp> onSuccess, Action<string> onError)
    {
        var form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        using (var req = UnityWebRequest.Post(BASE_URL + "/register", form))
        {
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            var resp = JsonUtility.FromJson<CloudSaveAuthResp>(req.downloadHandler.text);
            if (resp == null || string.IsNullOrEmpty(resp.token))
            {
                onError?.Invoke("服务器返回数据异常");
                yield break;
            }
            Token = resp.token;
            onSuccess?.Invoke(resp);
        }
    }

    // ============================================================
    // 3. 获取存档列表
    // ============================================================
    public static IEnumerator GetSaveList(Action<CloudSaveListResp> onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Get(BASE_URL + "/saves"))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            var resp = JsonUtility.FromJson<CloudSaveListResp>(req.downloadHandler.text);
            resp.items = resp.items ?? new CloudSaveSaveEntry[0];
            onSuccess?.Invoke(resp);
        }
    }

    // ============================================================
    // 4. 上传存档（byte[] 在内存中直接上传）
    //    bytes    - 存档文件内容（如 File.ReadAllBytes(...)）
    //    fileName - 存到云端显示的文件名，如 "chapter2.sav"
    //    note     - 可选备注，如 "第二章通关存档"
    // ============================================================
    public static IEnumerator UploadSave(byte[] bytes, string fileName, string note,
        Action<CloudSaveUploadResp> onSuccess, Action<string> onError)
    {
        var form = new WWWForm();
        form.AddBinaryData("file", bytes, fileName, "application/octet-stream");
        if (!string.IsNullOrEmpty(note))
            form.AddField("note", note);

        using (var req = UnityWebRequest.Post(BASE_URL + "/saves", form))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            var resp = JsonUtility.FromJson<CloudSaveUploadResp>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
    }

    // ============================================================
    // 5. 上传存档（直接读本地文件，适合大文件）
    // ============================================================
    public static IEnumerator UploadSaveFromFile(string localPath, string fileName, string note,
        Action<CloudSaveUploadResp> onSuccess, Action<string> onError)
    {
        if (!File.Exists(localPath))
        {
            onError?.Invoke("本地文件不存在: " + localPath);
            yield break;
        }
        byte[] bytes;
        try { bytes = File.ReadAllBytes(localPath); }
        catch (Exception e)
        {
            onError?.Invoke("读取文件失败: " + e.Message);
            yield break;
        }
        yield return UploadSave(bytes, fileName, note, onSuccess, onError);
    }

    // ============================================================
    // 6. 下载存档（到内存，适合中小文件，之后自己写盘）
    // ============================================================
    public static IEnumerator DownloadSave(int saveId, Action<byte[]> onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Get(BASE_URL + "/saves/" + saveId + "/download"))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            onSuccess?.Invoke(req.downloadHandler.data);
        }
    }

    // ============================================================
    // 7. 下载存档（直接落盘，适合大文件，省内存）
    // ============================================================
    public static IEnumerator DownloadSaveToFile(int saveId, string savePath,
        Action onDone, Action<string> onError)
    {
        var req = new UnityWebRequest(BASE_URL + "/saves/" + saveId + "/download")
        {
            downloadHandler = new DownloadHandlerFile(savePath)
        };
        AddAuth(req);

        using (req)
        {
            yield return req.SendWebRequest();
            if (!Resolve(req, onError)) yield break;
            onDone?.Invoke();
        }
    }

    // ============================================================
    // 8. 删除存档
    // ============================================================
    public static IEnumerator DeleteSave(int saveId, Action onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Delete(BASE_URL + "/saves/" + saveId))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            onSuccess?.Invoke();
        }
    }

    /// <summary>格式化文件大小，如 "2.3 MB"</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        int i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return size.ToString(i == 0 ? "F0" : "F1") + " " + units[i];
    }

    // ============================================================
    // 9. 槽位列表 —— 返回该账号所有"已占用"的槽位
    //    云端槽位号通常 1~N 与游戏存档槽一一对应，请用下面
    //    GetSlotEntries() 快速取出你关心的某槽位。
    // ============================================================
    public static IEnumerator GetSlots(Action<CloudSaveSlotListResp> onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Get(BASE_URL + "/slots"))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            var resp = JsonUtility.FromJson<CloudSaveSlotListResp>(req.downloadHandler.text);
            resp.items = resp.items ?? new CloudSaveSlotEntry[0];
            onSuccess?.Invoke(resp);
        }
    }

    /// <summary>从槽位列表取某槽位存档（槽位空则返回 null）</summary>
    public static CloudSaveSlotEntry GetSlotEntry(CloudSaveSlotListResp resp, int slot)
    {
        if (resp == null || resp.items == null) return null;
        foreach (var it in resp.items)
            if (it.slot == slot) return it;
        return null;
    }

    // ============================================================
    // 10. 上传到指定槽位（覆盖式：该槽位已有存档会被自动替换）
    // ============================================================
    public static IEnumerator UploadToSlot(int slot, byte[] bytes, string fileName, string note,
        Action<CloudSaveSlotEntry> onSuccess, Action<string> onError)
    {
        var form = new WWWForm();
        form.AddBinaryData("file", bytes, fileName, "application/octet-stream");
        if (!string.IsNullOrEmpty(note))
            form.AddField("note", note);

        using (var req = PutForm(BASE_URL + "/slots/" + slot, form))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            var resp = JsonUtility.FromJson<CloudSaveSlotEntry>(req.downloadHandler.text);
            onSuccess?.Invoke(resp);
        }
    }

    /// <summary>上传本地文件到指定槽位（覆盖式）</summary>
    public static IEnumerator UploadToSlotFromFile(int slot, string localPath, string fileName, string note,
        Action<CloudSaveSlotEntry> onSuccess, Action<string> onError)
    {
        if (!File.Exists(localPath))
        {
            onError?.Invoke("本地文件不存在: " + localPath);
            yield break;
        }
        byte[] bytes;
        try { bytes = File.ReadAllBytes(localPath); }
        catch (Exception e)
        {
            onError?.Invoke("读取文件失败: " + e.Message);
            yield break;
        }
        yield return UploadToSlot(slot, bytes, fileName, note, onSuccess, onError);
    }

    // ============================================================
    // 11. 从指定槽位下载（直接写盘，省内存，适合大存档）
    // ============================================================
    public static IEnumerator DownloadSlotToFile(int slot, string savePath,
        Action onDone, Action<string> onError)
    {
        var req = new UnityWebRequest(BASE_URL + "/slots/" + slot + "/download")
        {
            downloadHandler = new DownloadHandlerFile(savePath)
        };
        AddAuth(req);
        using (req)
        {
            yield return req.SendWebRequest();
            if (!Resolve(req, onError)) yield break;
            onDone?.Invoke();
        }
    }

    /// <summary>从指定槽位下载到内存</summary>
    public static IEnumerator DownloadSlot(int slot, Action<byte[]> onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Get(BASE_URL + "/slots/" + slot + "/download"))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            onSuccess?.Invoke(req.downloadHandler.data);
        }
    }

    // ============================================================
    // 12. 清空指定槽位（槽位已空会收到 404 错误，属正常情况）
    // ============================================================
    public static IEnumerator ClearSlot(int slot, Action onSuccess, Action<string> onError)
    {
        using (var req = UnityWebRequest.Delete(BASE_URL + "/slots/" + slot))
        {
            AddAuth(req);
            yield return req.SendWebRequest();

            if (!Resolve(req, onError)) yield break;
            onSuccess?.Invoke();
        }
    }

    // ---------- 工具：以 multipart/form-data 方式发 PUT（UnityWebRequest 无内置 PUT+表单） ----------
    private static UnityWebRequest PutForm(string url, WWWForm form)
    {
        var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
        req.uploadHandler = new UploadHandlerRaw(form.data);
        req.downloadHandler = new DownloadHandlerBuffer();
        // WWWForm.headers 已含正确的 multipart boundary
        foreach (var kv in form.headers)
            req.SetRequestHeader(kv.Key, kv.Value);
        return req;
    }
}
