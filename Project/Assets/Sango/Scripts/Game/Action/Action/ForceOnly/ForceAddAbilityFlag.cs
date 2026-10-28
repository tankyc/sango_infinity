using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 给势力打上"能力标记位"（<see cref="ForceAbilityFlags"/>）。
    ///
    /// 【用途】有些技巧 / 事件的效果本身就是纯数值（例如神火计只是"火计射程 +2"），
    /// 但业务侧还要单独判断"这个势力到底会不会这招"：
    /// 原版是查科技表里的神火计，本项目落成势力身上的 bit，判断方只读一个字段，
    /// 不必逐个反查技巧列表（技巧可能来自剧本预置 / 事件授予 / Mod 覆盖）。
    ///
    /// 【配置】
    /// <code>
    /// { "class": "ForceAddAbilityFlag", "flag": "DivineFire" }
    /// </code>
    /// <c>flag</c> 取 <see cref="ForceAbilityFlags"/> 的枚举名，解析失败或为 <c>None</c> 时静默跳过
    /// （Mod 写错了不会让整条技巧失效，只是没有标记位）。
    ///
    /// 【接入点】只依赖 <see cref="Init"/>：技巧的 effects 在势力掌握该技巧时被注册
    /// （<see cref="Force.Init"/> 重放已拥有技巧、<see cref="Force.GainTechnique"/> 研究完成），
    /// 因此不需要任何事件订阅，也没有运行时开销。
    /// </summary>
    public class ForceAddAbilityFlag : ActionBase
    {
        /// <summary>
        /// 初始化：解析枚举名并置位。
        /// </summary>
        /// <param name="p">效果配置</param>
        /// <param name="sangoObjects">装配方传入的对象，技巧效果固定是所属势力</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (p == null || sangoObjects == null || sangoObjects.Length == 0)
                return;

            Force force = sangoObjects[0] as Force;
            if (force == null)
                return;

            string flagName = p.Value<string>("flag");
            if (string.IsNullOrEmpty(flagName))
                return;

            ForceAbilityFlags flag;
            if (!System.Enum.TryParse<ForceAbilityFlags>(flagName, out flag) || flag == ForceAbilityFlags.None)
            {
                Sango.Log.Warning($"*[势力能力] 未知的能力标记位名：{flagName}");
                return;
            }

            force.SetAbilityFlag(flag);
        }

        /// <summary>
        /// 不撤销标记位。
        ///
        /// 标记位表达的是"势力已掌握该能力"，而本项目没有"剥夺技巧"的流程；
        /// 这里若在 Clear 时清位，反而会在势力对象回收 / 重建时把仍在生效的能力抹掉。
        /// 将来真要支持剥夺，应由调用方显式 <see cref="Force.SetAbilityFlag"/> 清除。
        /// </summary>
        public override void Clear()
        {
        }

        /// <summary>
        /// 无触发式行为：本效果只在注册时置位。
        /// </summary>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
