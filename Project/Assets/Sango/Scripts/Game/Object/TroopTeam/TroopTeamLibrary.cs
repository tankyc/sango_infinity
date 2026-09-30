using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 队伍库文件的落盘结构（外面再包一层，方便以后加字段而不破坏旧文件）。
    /// </summary>
    [Serializable]
    public class TroopTeamFile
    {
        /// <summary>队伍列表</summary>
        public List<TroopTeam> teams = new List<TroopTeam>();
    }

    /// <summary>
    /// 玩家自建出征队伍（出征界面"保存队伍"）：可增 / 删，**跨存档保留**。
    ///
    /// 存放位置与"自建武将"（<c>GameCustomEdit</c>）同构：
    ///     `${Path.CustomEditRootPath}/Data/CustomTroopTeam.json`
    /// 由 <c>GameData.Init()</c> 在启动时载入，改动后立即落盘。
    ///
    /// 为什么不用 <c>ScenarioVariables</c>（随存档走）：那是**每个存档独立**的，
    /// 换剧本 / 新开局就没了；玩家辛苦配的队伍应该像自建武将那样长期保留。
    /// </summary>
    public static class CustomTroopTeams
    {
        /// <summary>单城队伍数量上限（防止列表无限膨胀）</summary>
        public const int MaxTeams = 30;

        /// <summary>玩家自建队伍（按保存顺序）</summary>
        public static List<TroopTeam> teams = new List<TroopTeam>();

        static string FilePath
        {
            get { return Sango.Path.CustomEditRootPath + "/Data/CustomTroopTeam.json"; }
        }

        /// <summary>启动时载入（由 <c>GameData.Init()</c> 调用）。</summary>
        public static void Init()
        {
            teams.Clear();
            try
            {
                if (string.IsNullOrEmpty(Sango.Path.CustomEditRootPath))
                    return;
                if (!Sango.File.Exists(FilePath))
                    return;

                string json = Sango.File.ReadAllText(FilePath);
                TroopTeamFile file = JsonConvert.DeserializeObject<TroopTeamFile>(json);
                if (file != null && file.teams != null)
                {
                    for (int i = 0; i < file.teams.Count; i++)
                    {
                        if (file.teams[i] != null && !string.IsNullOrEmpty(file.teams[i].name))
                            teams.Add(file.teams[i]);
                    }
                }
                Sango.Log.Info("自建出征队伍载入 " + teams.Count + " 支");
            }
            catch (Exception e)
            {
                Sango.Log.Warning("自建出征队伍载入失败：" + e.Message);
            }
        }

        /// <summary>落盘（增删改后调用）。</summary>
        public static bool Save()
        {
            try
            {
                if (string.IsNullOrEmpty(Sango.Path.CustomEditRootPath))
                    return false;

                TroopTeamFile file = new TroopTeamFile();
                file.teams = teams;
                Sango.Directory.Create(Sango.Path.CustomEditRootPath + "/Data");
                Sango.File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Sango.Log.Warning("自建出征队伍保存失败：" + e.Message);
                return false;
            }
        }

        /// <summary>队伍数量。</summary>
        public static int Count { get { return teams.Count; } }

        /// <summary>取某支队伍（越界返回 null）。</summary>
        public static TroopTeam Get(int index)
        {
            if (index < 0 || index >= teams.Count)
                return null;
            return teams[index];
        }

        /// <summary>
        /// 保存一支队伍：同名视为"覆盖"（避免玩家反复保存出几十个同名项），否则新增。
        /// </summary>
        /// <param name="team">队伍（会被 <c>Clone()</c> 一份存下，避免与界面共享引用）</param>
        /// <param name="replaced">输出：true = 覆盖了已有同名队伍</param>
        /// <returns>落盘结果（超上限 / 参数非法时为 false）</returns>
        public static bool Add(TroopTeam team, out bool replaced)
        {
            replaced = false;
            if (team == null || string.IsNullOrEmpty(team.name))
                return false;

            int index = IndexOf(team.name);
            if (index >= 0)
            {
                teams[index] = team.Clone();
                replaced = true;
            }
            else
            {
                if (teams.Count >= MaxTeams)
                    return false;
                teams.Add(team.Clone());
            }
            Save();
            return true;
        }

        /// <summary>删除一支队伍并落盘。</summary>
        public static bool RemoveAt(int index)
        {
            if (index < 0 || index >= teams.Count)
                return false;
            teams.RemoveAt(index);
            Save();
            return true;
        }

        /// <summary>删除同名队伍并落盘。</summary>
        public static bool Remove(string name)
        {
            int index = IndexOf(name);
            if (index < 0)
                return false;
            return RemoveAt(index);
        }

        /// <summary>按名字找下标（不存在返回 -1）。</summary>
        public static int IndexOf(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;
            for (int i = 0; i < teams.Count; i++)
            {
                if (teams[i] != null && teams[i].name == name)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 生成一个不与现有队伍重名的默认名（"我的队伍1"、"我的队伍2"…）。
        /// </summary>
        public static string SuggestName()
        {
            for (int n = 1; n <= MaxTeams + 1; n++)
            {
                string name = "我的队伍" + n;
                if (IndexOf(name) < 0)
                    return name;
            }
            return "我的队伍";
        }
    }

    /// <summary>
    /// 预制作推荐队伍（只读模板）：`Data/Common/RecommendedTroopTeams.json`，可被 Mod 覆盖。
    ///
    /// 载入方式与 <c>AIConfig</c> 同构：懒加载单例 + <c>ModManager.LoadFile</c>（Mod 文件优先）
    /// + 换剧本时 <c>Reset()</c>（由 <c>ScenarioLifecycle</c> 调用）。
    /// </summary>
    public class RecommendedTroopTeams
    {
        /// <summary>模板列表（载入后按 priority 降序）</summary>
        public List<TroopTeam> teams = new List<TroopTeam>();

        static RecommendedTroopTeams instance;

        /// <summary>全局实例（首次访问时加载）。</summary>
        public static RecommendedTroopTeams Instance
        {
            get
            {
                if (instance == null)
                    instance = Load();
                return instance;
            }
        }

        /// <summary>重置，下次访问时重新加载（换剧本 / 调试用）。</summary>
        public static void Reset()
        {
            instance = null;
        }

        /// <summary>全部模板（可能为空，调用方需判空）。</summary>
        public static List<TroopTeam> All
        {
            get { return Instance.teams; }
        }

        /// <summary>
        /// 取适用于某圈层的模板（<c>ring &lt; 0</c> 的通用队伍任何城都适用），已按优先级降序。
        /// </summary>
        /// <param name="ring">目标城圈层（0 = 前线，1 = 次前线，其余为后方）</param>
        public static List<TroopTeam> ForRing(int ring)
        {
            List<TroopTeam> result = new List<TroopTeam>();
            List<TroopTeam> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                TroopTeam t = all[i];
                if (t == null) continue;
                if (t.ring < 0 || t.ring == ring)
                    result.Add(t);
            }
            return result;
        }

        /// <summary>按名字找模板（不存在返回 null）。</summary>
        public static TroopTeam Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            List<TroopTeam> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        static RecommendedTroopTeams Load()
        {
            RecommendedTroopTeams config = new RecommendedTroopTeams();
            try
            {
                Sango.Mod.ModManager.Instance.LoadFile("Data/Common/RecommendedTroopTeams.json", file =>
                {
                    string json = Sango.File.ReadAllText(file);
                    RecommendedTroopTeams loaded = JsonConvert.DeserializeObject<RecommendedTroopTeams>(json);
                    if (loaded != null)
                        config = loaded;
                });
            }
            catch (Exception e)
            {
                Sango.Log.Warning("推荐队伍数据加载失败，按空模板处理：" + e.Message);
            }

            if (config.teams == null)
                config.teams = new List<TroopTeam>();
            config.teams.Sort(delegate (TroopTeam a, TroopTeam b)
            {
                int pa = a != null ? a.priority : 0;
                int pb = b != null ? b.priority : 0;
                return pb.CompareTo(pa);
            });
            return config;
        }
    }
}
