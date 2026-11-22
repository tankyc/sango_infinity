using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 剧本附加数据
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioAddon
    {
        /// <summary>
        /// 自建武将
        /// </summary>
        [JsonConverter(typeof(SangoObjectOffSetConverter<PersonLib>))]
        [JsonProperty] public SangoObjectOffSet<PersonLib> PersonLibrary = new SangoObjectOffSet<PersonLib>();

        /// <summary>
        /// 从文件流式读取一个全新的 <see cref="ScenarioAddon"/>。
        ///
        /// 【为什么不再用 File.ReadAllText + PopulateObject】
        ///   ① 内存：PersonLibrary.json 有 15MB，ReadAllText 会一次性分配等长的 UTF-16 字符串
        ///      （≈31MB 托管内存），启动阶段直接把 GC 峰值顶上去；JsonTextReader 是流式的。
        ///   ② 速度：这里每次都是"新建对象"，没有合并旧值的需求，DeserializeObject 走
        ///      预编译的成员契约，比 PopulateObject 的逐成员匹配少一遍匹配开销
        ///      （Profiler 实测该文件 PopulateObject 占 2.2s / 25.3%）。
        /// </summary>
        /// <param name="file">JSON 文件完整路径</param>
        /// <returns>解析结果；文件内容为 null 时返回 null</returns>
        static ScenarioAddon DeserializeFromFile(string file)
        {
            // 64KB 缓冲：默认 1KB 读 15MB 文件会产生上万次 syscall
            using (StreamReader stream = new StreamReader(file, Encoding.UTF8, true, 64 * 1024))
            using (JsonTextReader reader = new JsonTextReader(stream))
            {
                return JsonSerializer.CreateDefault().Deserialize<ScenarioAddon>(reader);
            }
        }

        public void Load(string file)
        {
            if (File.Exists(file))
            {
                // 【容错】附加武将库是可选的增强数据：文件被手工改坏（少冒号、多逗号）、
                // 或旧版本字段结构不兼容时，异常不能冒泡打断 GameData.Init ——
                // 一旦抛出，后面的 LoadFaceData 与整个剧本加载都不再执行，
                // 表现就是"启动后一连串空引用"，而真正的原因（哪个文件坏了）被埋掉。
                // 实测：PersonLibrary.11554.compatibility 处少了冒号，异常信息里只有
                // JSON 路径和行列号、没有文件名，几乎无法定位。
                // 这里既兜住异常，又把完整路径打出来。
                ScenarioAddon scenarioAddon;
                try
                {
                    scenarioAddon = DeserializeFromFile(file);
                }
                catch (JsonReaderException e)
                {
                    Sango.Log.Error(
                        $"附加数据解析失败，已跳过该文件：{file}\n" +
                        $"位置：行 {e.LineNumber}，列 {e.LinePosition}；路径：{e.Path}\n{e.Message}");
                    return;
                }
                catch (System.Exception e)
                {
                    Sango.Log.Error(
                        $"附加数据加载失败，已跳过该文件：{file}\n{e.GetType().Name}: {e.Message}");
                    return;
                }

                if (scenarioAddon == null)
                    return;

                Combine(scenarioAddon);
            }
        }

        void FixID(ref int[] ids, int start)
        {
            if(ids == null || ids.Length == 0) { return; }
            for(int i = 0; i < ids.Length; i++)
            {
                int v = ids[i];
                if (v > 30000)
                {
                    v = v - 30000 + start;
                    ids[i] = v;
                }
            }
        }

        void FixID(ref int id, int start)
        {
            if (id > 30000)
                id = id + start;
        }

        public int Load(Mod.Mod mod, int start, string file)
        {
            if (File.Exists(file))
            {
                ScenarioAddon scenarioAddon = new ScenarioAddon();
                scenarioAddon.PersonLibrary.offset = PersonLibrary.offset;
                // 【容错】与上面无 mod 版本同一个道理：某个 Mod 的自建武将文件语法错，
                // 不能让整个启动链断掉。这里额外带上 Mod 名字，方便玩家定位到具体模组。
                try
                {
                    // 【保持 Populate】SangoObjectOffSetConverter 依赖 container.offset
                    // 在解析过程中修正条目 Id（Id < offset 时补 offset），所以必须解析进
                    // 这个"预设过 offset"的实例，不能换成 Deserialize（会新建对象、offset 丢失）。
                    // 但输入源改成 JsonTextReader 流式读取，省掉 File.ReadAllText 为
                    // Mod 武将库再分配的一整份 UTF-16 字符串。
                    using (StreamReader stream = new StreamReader(file, Encoding.UTF8, true, 64 * 1024))
                    using (JsonTextReader reader = new JsonTextReader(stream))
                    {
                        JsonSerializer.CreateDefault().Populate(reader, scenarioAddon);
                    }
                }
                catch (JsonReaderException e)
                {
                    Sango.Log.Error(
                        $"模组附加数据解析失败，已跳过该文件（模组：{(mod != null ? mod.Name : "未知")}）：{file}\n" +
                        $"位置：行 {e.LineNumber}，列 {e.LinePosition}；路径：{e.Path}\n{e.Message}");
                    return start;
                }
                catch (System.Exception e)
                {
                    Sango.Log.Error(
                        $"模组附加数据加载失败，已跳过该文件（模组：{(mod != null ? mod.Name : "未知")}）：{file}\n" +
                        $"{e.GetType().Name}: {e.Message}");
                    return start;
                }
                scenarioAddon.PersonLibrary.ForEach(x =>
                {
                    x.Id = x.Id + start;
                    // 分配新ID
                    PersonLibrary.Add(x);

                    x.modName = mod.Name;
                    
                    // 梳理关系ID
                    FixID(ref x.FatherId, start);
                    FixID(ref x.MotherId, start);
                    FixID(ref x.BrotherId, start);
                    FixID(ref x.BrotherListId, start);
                    FixID(ref x.LikePersonListId, start);
                    FixID(ref x.HatePersonListId, start);
                });
            }
            return start;
        }

        public void Combine(ScenarioAddon scenarioAddon)
        {
            //int count = PersonLibrary.Count;
            scenarioAddon.PersonLibrary.ForEach(x =>
            {
                //// 检查重复名字的
                //PersonLib exsist = PersonLibrary.Find(y => x.familyName == y.familyName && x.giveName == y.giveName);
                //if (exsist == null)
                //{
                //    x.Id += count;
                //}
                //else
                //{

                //}
                PersonLibrary.Add(x);
            });
        }
    }
}
