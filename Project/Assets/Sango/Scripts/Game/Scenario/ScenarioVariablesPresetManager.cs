using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 剧本参数（<see cref="ScenarioVariables"/>）预设管理器。
    ///
    /// 【为什么需要】剧本参数有上百项，玩家/作者调好一套（难度、成长倍率、AI 加成……）后
    /// 换剧本或重开档就得重配一遍。预设把整份 <see cref="ScenarioVariables"/> 落成 JSON，
    /// 剧本侧只记一个名字即可复用。
    ///
    /// 【存放位置（三个来源，后加载覆盖先加载）】
    ///   1. 内置：<c>{ContentRootPath}/Data/ScenarioVariables/{名字}.json</c>
    ///   2. 自建：<c>{CustomEditRootPath}/Data/ScenarioVariables/{名字}.json</c>   ← 保存目标
    ///   3. 模组：<c>{ModRootPath}/**/Data/ScenarioVariables/{名字}.json</c>
    /// 覆盖规则与事件库（<c>EventDatabase</c>）保持一致：同名时后加载的生效。
    ///
    /// 【预设名】文件名去掉 <c>.json</c> 即预设名。
    /// 剧本用 <see cref="ScenarioInfo.variablesPreset"/> 指定默认读哪个预设，空串 = 不读。
    /// </summary>
    public static class ScenarioVariablesPresetManager
    {
        /// <summary>相对数据子目录（相对 ContentRoot / CustomEditRoot，或 Mod 内任意层级的 Data）</summary>
        const string FolderName = "Data/ScenarioVariables";

        /// <summary>默认预设名：剧本没指定名字时，保存用这个名字落盘</summary>
        public const string DefaultPresetName = "default";

        /// <summary>一份预设</summary>
        public class Preset
        {
            /// <summary>预设名（= 文件名去掉扩展名）</summary>
            public string Name;

            /// <summary>来源标记：内置 / 自建 / Mod，用于界面区分</summary>
            public string Source;

            /// <summary>物理文件路径</summary>
            public string File;

            /// <summary>解析后的参数对象</summary>
            public ScenarioVariables Variables;
        }

        static readonly List<Preset> presets = new List<Preset>();
        static bool loaded;

        /// <summary>全部预设（按名字升序；同名已按来源优先级合并）</summary>
        public static List<Preset> Presets
        {
            get { EnsureLoaded(); return presets; }
        }

        /// <summary>强制重新扫描磁盘（保存后 / 玩家手工改过文件后调用）</summary>
        public static void Reload()
        {
            loaded = false;
            presets.Clear();
            EnsureLoaded();
        }

        /// <summary>全部预设名（供界面下拉使用）</summary>
        /// <returns>名字列表；没有任何预设时返回空列表</returns>
        public static List<string> GetNames()
        {
            EnsureLoaded();
            List<string> names = new List<string>(presets.Count);
            for (int i = 0; i < presets.Count; i++)
                names.Add(presets[i].Name);
            return names;
        }

        /// <summary>按名字找预设</summary>
        /// <param name="name">预设名（不含扩展名）</param>
        /// <returns>找到返回预设，否则 null</returns>
        public static Preset Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            EnsureLoaded();
            for (int i = 0; i < presets.Count; i++)
            {
                if (string.Equals(presets[i].Name, name, System.StringComparison.OrdinalIgnoreCase))
                    return presets[i];
            }
            return null;
        }

        /// <summary>
        /// 把预设套用到剧本上（直接替换 <see cref="Scenario.Variables"/> 引用）。
        ///
        /// 【为什么是替换引用而不是逐字段拷贝】<see cref="ScenarioVariables"/> 有上百个字段，
        /// 逐字段拷贝必然漏项；而全项目读取参数都是 <c>Scenario.Cur.Variables.xxx</c> 现取，
        /// 替换引用后下一次读取自然拿到新对象。参数界面在套用后会 <c>RefreshSetting()</c> 重建，
        /// 旧的局部引用不会残留。
        /// </summary>
        /// <param name="scenario">目标剧本</param>
        /// <param name="name">预设名</param>
        /// <returns>套用成功返回 true</returns>
        public static bool TryApply(Scenario scenario, string name)
        {
            if (scenario == null)
                return false;

            Preset preset = Find(name);
            if (preset == null || preset.Variables == null)
            {
                if (!string.IsNullOrEmpty(name))
                    Sango.Log.Warning($"剧本参数预设「{name}」不存在，沿用剧本自带参数");
                return false;
            }

            scenario.Variables = preset.Variables;
            Sango.Log.Info($"已套用剧本参数预设「{preset.Name}」（来源：{preset.Source}）");
            return true;
        }

        /// <summary>
        /// 保存一份参数到自建目录：<c>{CustomEditRootPath}/Data/ScenarioVariables/{name}.json</c>。
        ///
        /// 同名文件直接覆盖（"保存配置"就是覆盖当前预设；想另存请改剧本的预设名后再保存）。
        /// </summary>
        /// <param name="name">预设名（为空则用 <see cref="DefaultPresetName"/>）</param>
        /// <param name="variables">参数对象</param>
        /// <returns>保存成功返回 true</returns>
        public static bool Save(string name, ScenarioVariables variables)
        {
            if (variables == null)
                return false;
            if (string.IsNullOrEmpty(Sango.Path.CustomEditRootPath))
            {
                Sango.Log.Error("保存剧本参数预设失败：CustomEditRootPath 为空");
                return false;
            }

            if (string.IsNullOrEmpty(name))
                name = DefaultPresetName;
            name = SanitizeName(name);

            string dir = Sango.Path.CustomEditRootPath + "/" + FolderName;
            string path = dir + "/" + name + ".json";

            try
            {
                Sango.Directory.Create(dir);
                string json = JsonConvert.SerializeObject(variables, Formatting.Indented);
                Sango.File.WriteAllText(path, json);
                Sango.Log.Info($"剧本参数预设已保存：{path}");
                Reload();       // 让新文件立刻出现在下拉列表里
                return true;
            }
            catch (System.Exception e)
            {
                Sango.Log.Error($"保存剧本参数预设失败：{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除一份**自建**预设：<c>{CustomEditRootPath}/Data/ScenarioVariables/{name}.json</c>。
        ///
        /// 模组 / 内置目录是只读的（属于发布内容，不该被游戏运行时破坏），
        /// 同名预设若只存在于模组里，这里会拒绝并给出原因。
        /// </summary>
        /// <param name="name">预设名</param>
        /// <returns>确实删掉了自建文件才返回 true</returns>
        public static bool Delete(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            name = SanitizeName(name);

            if (string.IsNullOrEmpty(Sango.Path.CustomEditRootPath))
            {
                Sango.Log.Error("删除剧本参数预设失败：CustomEditRootPath 为空");
                return false;
            }

            string path = Sango.Path.CustomEditRootPath + "/" + FolderName + "/" + name + ".json";
            if (!System.IO.File.Exists(path))
            {
                Preset preset = Find(name);
                if (preset != null)
                    Sango.Log.Warning($"预设「{preset.Name}」来自{preset.Source}，不是自建文件，无法删除");
                else
                    Sango.Log.Warning($"预设「{name}」不存在，无法删除");
                return false;
            }

            try
            {
                System.IO.File.Delete(path);
                Sango.Log.Info($"已删除剧本参数预设：{path}");

                // 若模组里有同名预设，删掉自建版本后它会重新浮现，这里提示一下避免玩家误会没删掉
                Preset fallback = Find(name);
                if (fallback != null)
                    Sango.Log.Info($"同名预设「{fallback.Name}」仍存在（来源：{fallback.Source}）");

                Reload();
                return true;
            }
            catch (System.Exception e)
            {
                Sango.Log.Error($"删除剧本参数预设失败：{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 把预设名清洗成安全文件名：去掉路径分隔符与 JSON 扩展名，避免玩家填 "../../x" 之类。
        /// </summary>
        /// <param name="name">原始名字</param>
        /// <returns>可用作文件名的名字</returns>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return DefaultPresetName;

            string result = name.Trim();
            if (result.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                result = result.Substring(0, result.Length - 5);

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                result = result.Replace(invalid[i], '_');
            result = result.Replace('/', '_').Replace('\\', '_');

            return string.IsNullOrEmpty(result) ? DefaultPresetName : result;
        }

        // ====================================================================
        // 磁盘扫描
        // ====================================================================

        /// <summary>首次访问时扫描三个来源。</summary>
        static void EnsureLoaded()
        {
            if (loaded)
                return;
            loaded = true;
            presets.Clear();

            // 顺序即优先级：内置 → 自建 → Mod（后者覆盖前者）
            LoadDirectory(Sango.Path.ContentRootPath, "内置");
            LoadDirectory(Sango.Path.CustomEditRootPath, "自建");
            LoadModDirectory(Sango.Path.ModRootPath);

            presets.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        /// <summary>加载某个根目录下的 <c>Data/ScenarioVariables</c>。</summary>
        /// <param name="root">根目录（ContentRoot / CustomEditRoot）</param>
        /// <param name="sourceTag">来源标记</param>
        static void LoadDirectory(string root, string sourceTag)
        {
            if (string.IsNullOrEmpty(root))
                return;

            string dir = root + "/" + FolderName;
            if (!System.IO.Directory.Exists(dir))
                return;

            string[] files;
            try
            {
                files = System.IO.Directory.GetFiles(dir, "*.json", System.IO.SearchOption.AllDirectories);
            }
            catch (System.Exception e)
            {
                Sango.Log.Warning($"扫描剧本参数预设目录失败（{dir}）：{e.Message}");
                return;
            }

            for (int i = 0; i < files.Length; i++)
                LoadFile(files[i], sourceTag);
        }

        /// <summary>
        /// 扫描模组根目录下所有 <c>Data/ScenarioVariables</c> 目录。
        /// 只认路径里带该片段的文件，避免把模组里其它 JSON 当预设读。
        /// </summary>
        /// <param name="modRoot">模组根目录</param>
        static void LoadModDirectory(string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot) || !System.IO.Directory.Exists(modRoot))
                return;

            string[] files;
            try
            {
                files = System.IO.Directory.GetFiles(modRoot, "*.json", System.IO.SearchOption.AllDirectories);
            }
            catch (System.Exception e)
            {
                Sango.Log.Warning($"扫描模组剧本参数预设失败：{e.Message}");
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                // 统一成正斜杠再判断，避免 Windows 反斜杠漏匹配
                string normalized = files[i].Replace('\\', '/');
                if (normalized.IndexOf("/" + FolderName + "/", System.StringComparison.Ordinal) < 0)
                    continue;
                LoadFile(files[i], "Mod");
            }
        }

        /// <summary>解析一份预设文件；同名覆盖已有项。</summary>
        /// <param name="file">文件绝对路径</param>
        /// <param name="sourceTag">来源标记</param>
        static void LoadFile(string file, string sourceTag)
        {
            try
            {
                string text = System.IO.File.ReadAllText(file);
                ScenarioVariables variables = JsonConvert.DeserializeObject<ScenarioVariables>(text);
                if (variables == null)
                {
                    Sango.Log.Warning($"剧本参数预设内容为空，已跳过：{file}");
                    return;
                }

                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(name))
                    return;

                Preset preset = new Preset();
                preset.Name = name;
                preset.Source = sourceTag;
                preset.File = file;
                preset.Variables = variables;

                // 同名覆盖（后加载的优先生效）
                for (int i = 0; i < presets.Count; i++)
                {
                    if (string.Equals(presets[i].Name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        presets[i] = preset;
                        return;
                    }
                }
                presets.Add(preset);
            }
            catch (System.Exception e)
            {
                Sango.Log.Warning($"剧本参数预设解析失败（{file}）：{e.Message}");
            }
        }
    }
}
