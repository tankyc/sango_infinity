using System.Collections.Generic;
using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 往部队的战法列表里追加一个战法。
    ///
    /// 接入事件：OnTroopBeforeCalculateAttribute（属性重算前），重复追加同一战法会被跳过。
    /// 参数：value 为战法 Id。
    /// </summary>
    public class TroopAddSkill : TroopTroopActionBase
    {
        /// <summary>
        /// 已经报过"战法 Id 不存在"的配置值，避免刷屏。
        /// 属性重算（Troop.CalculateAttribute）在回合内会被高频触发，
        /// 每次重算都会走到本处理器，不去重会瞬间把控制台刷满。
        /// </summary>
        static readonly HashSet<int> reportedInvalidSkill = new HashSet<int>();

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopBeforeCalculateAttribute += OnTroopBeforeCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopBeforeCalculateAttribute -= OnTroopBeforeCalculateAttribute;
        }

        void OnTroopBeforeCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            Skill skill = scenario.GetObject<Skill>(value);
            if (skill == null)
            {
                // 取不到战法对象有两种来源，都会返回 null（Scenario.GetObject → SangoObjectSet.Get，
                // 见 Scenario.cs:663-666 与 SangoObjectSet.cs:105-110）：
                //   1. 配置的 value 在本局技能表里不存在（Mod 覆盖了 Skills.json、或 value 漏写被解析成 0）；
                //   2. value 越界 / 命中的是空位。
                // 之前直接调 skill.IsStrategy() 会抛 NullReferenceException，
                // 而这条处理器挂在"属性重算前"事件上，武将编辑确认、部队初始化等都会触发，
                // 一处配置错误就能把整个操作打崩（实战崩在 PersonEditSnapshot.ApplyTo）。
                ReportInvalidSkill();
                return;
            }

            if (skill.IsStrategy())
            {
                if (!troop.StrategySkills.Exists(x => x.Id == skill.Id))
                    troop.StrategySkills.Add(SkillInstance.Create(troop, skill));
            }
            else
            {
                if (kinds == null)
                {

                    if (!troop.landSkills.Exists(x => x.Id == skill.Id))
                        troop.landSkills.Add(SkillInstance.Create(troop, skill));
                    if (!troop.waterSkills.Exists(x => x.Id == skill.Id))
                        troop.waterSkills.Add(SkillInstance.Create(troop, skill));
                }
                else
                {
                    // LandTroopType / WaterTroopType 是**按需解析的可空引用**
                    // （Troop.cs:812-820：靠 LandTroopTypeId 经 IdRef.Resolve 反查，
                    //  id <= 0 或查不到兵种时就是 null），直接取 .kind 会抛 NullReferenceException。
                    if (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind))
                    {
                        if (!troop.landSkills.Exists(x => x.Id == value))
                            troop.landSkills.Add(SkillInstance.Create(troop, skill));

                    }
                    if (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind))
                    {
                        if (!troop.waterSkills.Exists(x => x.Id == value))
                            troop.waterSkills.Add(SkillInstance.Create(troop, skill));
                    }
                }

            }


        }

        /// <summary>
        /// 上报"配置的战法 Id 在技能表里不存在"。
        /// 同一个 value 只报一次：属性重算会被高频触发，不去重会刷屏。
        /// 日志里带上 value，方便直接去 Features.json / Techniques.json 里定位是哪一条动作配错了。
        /// </summary>
        void ReportInvalidSkill()
        {
            if (!reportedInvalidSkill.Add(value)) return;
            Sango.Log.Error($"TroopAddSkill：配置的战法 Id={value} 在技能表中不存在，已跳过。"
                + "请检查 Features.json / Techniques.json（含被 Mod 覆盖后的同名表）中该动作的 value 字段。");
        }

    }
}
