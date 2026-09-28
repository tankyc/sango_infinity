using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

namespace Sango.Core
{
    /// <summary>
    /// 武将库数据文件的结构版本定义。
    ///
    /// 【新版结构（版本 <see cref="CurrentVersion"/>）】
    /// <list type="bullet">
    /// <item>在 PersonLibrary 容器级写入版本标记 <c>"dataVersion": 2</c>；</item>
    /// <item>五维 <c>command/strength/intelligence/politics/glamour</c> 与
    /// 兵种适性 <c>spearLv/halberdLv/crossbowLv/rideLv/waterLv/machineLv</c>
    /// 由"单个整数"改为数组（见 <see cref="PersonAttributeValue"/> / <see cref="PersonAbilityValue"/>）；</item>
    /// <item>库条目上的登场年字段名由 <c>yearAvailable</c> 改为 <c>appearance</c>（与 <see cref="Person"/> 一致）。</item>
    /// </list>
    ///
    /// 【旧结构兼容】读取时若发现标记缺失或小于当前版本，仍按旧结构解析：
    /// <list type="bullet">
    /// <item>标量整数形式的五维 / 兵种适性由对应的 JsonConverter 兼容读取；</item>
    /// <item>旧的 <c>yearAvailable</c> 由 <see cref="PersonLib"/> 在反序列化完成后回填到 <c>appearance</c>。</item>
    /// </list>
    /// </summary>
    public static class PersonLibraryDataFormat
    {
        /// <summary>容器级版本标记的键名（写在 PersonLibrary 下）</summary>
        public const string VersionKey = "dataVersion";

        /// <summary>当前（新版）结构版本号</summary>
        public const int CurrentVersion = 2;

        /// <summary>旧版的登场年字段名，读取旧文件时回填到 appearance</summary>
        public const string LegacyAppearanceKey = "yearAvailable";

        /// <summary>最近一次读到的库数据版本；0 表示文件未带标记（按旧结构处理）</summary>
        public static int LoadedVersion { get; private set; }

        /// <summary>最近一次读取的库文件是否为新版结构</summary>
        public static bool IsCurrentFormat { get { return LoadedVersion >= CurrentVersion; } }

        /// <summary>
        /// 开始读取一个新的武将库容器时调用：先把版本清零，
        /// 待真正读到容器级标记后再写入，保证"文件没有标记即视为旧结构"的判断准确。
        /// </summary>
        public static void BeginLoad()
        {
            LoadedVersion = 0;
        }

        /// <summary>
        /// 记录从数据文件中读到的版本标记，并对旧结构给出提示。
        /// </summary>
        /// <param name="version">版本号（文件未标记时传 0）</param>
        public static void MarkLoadedVersion(int version)
        {
            LoadedVersion = version;
            if (version < CurrentVersion)
            {
                Sango.Log.Warning($"武将库数据缺少新版标记[{VersionKey}]（读到 {version}，当前 {CurrentVersion}），已按旧结构兼容加载；建议用武将库工具重新导出为新格式。");
            }
        }
    }

    /// <summary>
    /// 武将库条目（基础武将库 / 自建武将库的数据模板）。
    ///
    /// 【结构说明】本类直接继承 <see cref="Person"/>，与剧本运行期的武将共用同一套数据结构：
    /// 凡是 <see cref="Person"/> 已经定义的成员（姓名、列传、头像、五维、兵种适性、血缘与关系 Id 等）
    /// 一律不再重复声明，交由继承获得，避免两套结构漂移。
    ///
    /// 本类只保留"库"特有的成员：
    /// <list type="bullet">
    /// <item>库工具的元数据：<see cref="tags"/>（标签）、<see cref="updatedAt"/>（最后修改时间）</item>
    /// <item>武将类型：<see cref="type"/></item>
    /// <item>自建武将的关联关系：<see cref="targetShortPersonId"/>（对应剧本武将 Id）、<see cref="modName"/>（来源 Mod）</item>
    /// <item>库中"兄弟"记录的是 Id 数组（运行时对象列表由 <see cref="Person.BrotherList"/> 承载），故改名区分</item>
    /// </list>
    ///
    /// 【运行期约定】库条目只是数据模板，不参与剧本运行期的归属/关系解析，
    /// 因此 <see cref="OnScenarioPrepare"/> 覆写为空实现（详见该方法注释）。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class PersonLib : Person
    {
        /// <summary>
        /// 姓名：由"姓 + 名"拼接得到。
        /// 注意这与 <see cref="SangoObject.Name"/> 的存储值不同——库条目不使用基类的名字字段，
        /// 一律以 familyName/giveName 为准（保持与旧武将库数据一致的行为）。
        /// </summary>
        public override string Name => $"{familyName}{giveName}";

        /// <summary>
        /// 是否为追加（自建）武将
        /// </summary>
        public bool isAppend = false;

        /// <summary>
        /// 武将类型（对应 PersonTypeEnum）
        /// </summary>
        [JsonProperty] public int type;

        /// <summary>
        /// 武将标签（由武将库工具维护，用于分类与按标签筛选；基础武将库不使用该字段）
        /// 标注 JsonProperty 是为了让游戏保存自建武将时能原样回写，避免标签丢失
        /// </summary>
        [JsonProperty]
        public string[] tags = new string[0];

        /// <summary>
        /// 最后修改时间（ISO 8601 UTC 字符串，由武将库工具维护；基础武将库不使用该字段）
        /// 标注 JsonProperty 是为了让游戏保存自建武将时能原样回写，避免修改时间丢失
        /// </summary>
        [JsonProperty]
        public string updatedAt = "";

        /// <summary>
        /// 兄弟列表（Id 数组）。
        /// <see cref="Person.BrotherList"/> 承载的是运行期的对象列表，与库中的 Id 数组语义不同，
        /// 故此处改名区分，但沿用原 JSON 键名 "BrotherList" 以保证数据文件兼容。
        /// </summary>
        [JsonProperty("BrotherList")]
        public int[] BrotherListId;

        /// <summary>
        /// 自建武将对应的剧本武将（ShortPerson）Id，0 表示尚未登场
        /// </summary>
        public int targetShortPersonId;

        /// <summary>
        /// 来源 Mod 名称
        /// </summary>
        public string modName;

        /// <summary>
        /// 取得该库条目在指定剧本中对应的所属城池 Id。
        /// （<see cref="Person.BelongCityId"/> 是运行期字段，名字已被占用，故本方法加 Get 前缀区分）
        /// </summary>
        /// <param name="scenario">剧本（简版）</param>
        /// <returns>所属城池 Id，未关联时返回 0</returns>
        public int GetBelongCityId(ShortScenario scenario)
        {
            if (targetShortPersonId > 0)
            {
                ShortPerson person = scenario.personSet[targetShortPersonId];
                if (person != null)
                {
                    return person.BelongCityId;
                }
                else
                {
                    targetShortPersonId = 0;
                    return 0;
                }
            }
            return 0;
        }

        /// <summary>
        /// 取得该库条目在指定剧本中对应的所属势力 Id。
        /// （<see cref="Person.BelongForceId"/> 是运行期字段，名字已被占用，故本方法加 Get 前缀区分）
        /// </summary>
        /// <param name="scenario">剧本（简版）</param>
        /// <returns>所属势力 Id，未关联时返回 0</returns>
        public int GetBelongForceId(ShortScenario scenario)
        {
            if (targetShortPersonId > 0)
            {
                ShortPerson person = scenario.personSet[targetShortPersonId];
                if (person != null)
                {
                    return person.BelongForceId;
                }
                else
                {
                    targetShortPersonId = 0;
                    return 0;
                }
            }
            return 0;
        }

        /// <summary>
        /// 剧本准备：库条目只是数据模板，不做任何运行期解析。
        ///
        /// 之所以要显式覆写成空实现：<see cref="ScenarioCommonData.Prepare"/> 会遍历武将库调用本方法，
        /// 若沿用 <see cref="Person"/> 的实现，会把库数据当成运行时武将处理
        /// （往城池的武将列表塞人、按年龄折算能力、注册官职等），从而污染剧本数据。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public override void OnScenarioPrepare(Scenario scenario)
        {
        }

        /// <summary>
        /// 兼容旧版数据文件：旧结构把登场年写在 <c>yearAvailable</c> 上。
        /// 该键在新结构里已不再对应任何字段，反序列化时会被
        /// <see cref="SangoObjectExtensionData"/> 收进扩展数据字典，这里在反序列化结束后
        /// 把它回填到 <see cref="Person.appearance"/>，避免旧文件读出来丢失登场年。
        /// </summary>
        /// <param name="context">序列化上下文</param>
        [OnDeserialized]
        private void FixLegacyFieldsOnDeserialized(StreamingContext context)
        {
            // 新结构已写入 appearance 时以新字段为准
            if (appearance > 0) return;

            JToken legacy = GetExtensionData(PersonLibraryDataFormat.LegacyAppearanceKey);
            if (legacy == null || legacy.Type != JTokenType.Integer) return;

            appearance = legacy.Value<int>();
            // 已迁移到 appearance，移除扩展数据中的旧键，避免保存时再写出一份
            RemoveExtensionData(PersonLibraryDataFormat.LegacyAppearanceKey);
        }
    }
}
