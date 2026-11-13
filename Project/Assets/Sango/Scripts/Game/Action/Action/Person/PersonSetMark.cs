using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 给武将设置标记位（<see cref="PersonMarkType"/>）。
    ///
    /// 【配置】
    /// <code>
    /// { "class": "PersonSetMark", "mark": "IgnoreCapture", "value": 1 }
    /// </code>
    ///   · mark  —— 标记位名（推荐，取 <see cref="PersonMarkType"/> 的枚举名）或位序号（0 起）
    ///   · value —— 1 = 置位，0 = 清位；缺省按 1 处理
    ///
    /// 【接入点】只依赖 <see cref="Init"/>，不订阅任何事件：
    /// 特技与装备的 actionEntities 装配统一由 <c>Person.InitPersonActions</c> 驱动
    /// （剧本解析、换装、授予 / 没收、学得 / 失去特技都会重跑），
    /// 因此"配置一改、下次初始化就生效"，热更 / 读档都不需要额外补逻辑。
    ///
    /// 【为什么 Clear 是空实现】标记位不存档，且 <c>Person.InitPersonActions</c> 每次都会先调
    /// <c>RefreshMarks</c> 把整张位图从当前状态重建一遍。Action 被 Clear 时（换装前）若再清一次位，
    /// 反而会误伤"另一个来源刚置的同一位置"，所以这里什么都不做。
    ///
    /// 【执行顺序】Action 在 RefreshMarks 之后装配，因此：
    ///   · value = 1 → 可以在系统推导结果（名马 / 强运）之上叠加；
    ///   · value = 0 → 可以反过来清掉系统推导出来的位。
    ///
    /// 【配置写错】枚举名解析失败 / 位序号越界时打一条 Warning 并静默跳过，
    /// 不影响装备 / 特技的其它效果（与 <see cref="ForceAddAbilityFlag"/> 的口径一致）。
    /// </summary>
    public class PersonSetMark : ActionBase
    {
        /// <summary>
        /// 解析配置并写入标记位。
        /// </summary>
        /// <param name="p">效果配置</param>
        /// <param name="sangoObjects">装配方传入的对象，固定是武将本人</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (p == null || sangoObjects == null || sangoObjects.Length == 0)
                return;

            Person person = sangoObjects[0] as Person;
            if (person == null)
                return;

            // mark 支持两种写法：枚举名（推荐）或位序号
            string markName = p.Value<string>("mark");
            if (string.IsNullOrEmpty(markName))
            {
                Sango.Log.Warning("*[标记位] 配置缺少 mark 字段，已跳过");
                return;
            }

            int bit;
            if (!int.TryParse(markName, out bit))
            {
                PersonMarkType parsed;
                if (!System.Enum.TryParse(markName, out parsed))
                {
                    Sango.Log.Warning($"*[标记位] 未知的标记位名：{markName}");
                    return;
                }
                bit = (int)parsed;
            }

            if (bit < 0 || bit >= 32)
            {
                Sango.Log.Warning($"*[标记位] 位序号越界：{bit}（有效范围 0~31）");
                return;
            }

            // value 缺省按"置位"处理：写了 class/mark 就是要生效
            JToken valueToken = p["value"];
            bool enable = valueToken == null || valueToken.Value<int>() != 0;

            PersonMarkType mark = (PersonMarkType)bit;
            if (enable) person.SetMark(mark, true);
            else person.ClearMark(mark);
        }

        /// <summary>
        /// 空实现 —— 标记位不存档，每次 <c>Person.InitPersonActions</c> 都会由 RefreshMarks 重建；
        /// 在这里清位会误伤其它来源刚置的同一位置（详见类注释）。
        /// </summary>
        public override void Clear()
        {
        }

        /// <summary>无触发式行为：本效果只在装配时写标记位。</summary>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
