using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sango.Core.Action;
using System.Collections.Generic;
using System.Numerics;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Buff : SangoObjectExtensionData
    {
        [JsonProperty] public int kind;
        [JsonProperty] public int subKind;
        [JsonProperty] public int limit;

        [JsonProperty] public string asset;

        [JsonConverter(typeof(Vector3Converter))]
        [JsonProperty] public UnityEngine.Vector3 offset;

        /// <summary>
        /// 技能效果
        /// </summary>
        [JsonProperty] public JArray buffEffects;

        /// <summary>
        /// 属于"不能行动"的控制态效果类名。
        ///
        /// 【为什么是这两个】对应原版 <c>UnitState != UnitState_Normal</c>（s11_enum_scen.h:926-938）：
        /// 原版有 混乱 / 伪报 / 暴走 / 足止 / 诱引 / 回避 六种异常，本项目目前只实现了两种效果：
        ///   · <c>Stun</c>   —— 混乱类，回合开始直接置 ActionOver（不能行动）；
        ///   · <c>Escape</c> —— 伪报类，回合开始被送回城。
        /// 其余四种原版状态尚未实现，等实现后在这里补类名即可。
        /// </summary>
        static readonly string[] ControlEffectClasses = { "Stun", "Escape" };

        /// <summary>
        /// 本 Buff 是否会让部队**无法正常行动**（原版 <c>state != Normal</c>）。
        ///
        /// 【修复说明】旧实现恒返回 <c>true</c>，导致
        ///   · <c>Troop.HasControlBuff()</c> 对任何 Buff（包括加攻/加防这类增益）都为真；
        ///   · 反击判定 <c>SkillInstance</c> 里"被控则不能反击"因此对所有携带 Buff 的部队生效；
        ///   · AI 评分把"目标身上有任何 Buff"当成"目标被控"来加权。
        /// 这是明显的误判，现改为按 <see cref="buffEffects"/> 里实际挂的效果类名判定。
        /// </summary>
        public bool IsControlBuff()
        {
            return HasEffectClass(ControlEffectClasses);
        }

        /// <summary>
        /// 本 Buff 是否为**伪报**（原版 <c>UnitState_Gihou</c>）。
        ///
        /// AI 评分要用它做"打了也没意义"的降权：
        /// 原版 get_target_score（s11_sys_unit_action.cpp:1739-1741）对伪报目标
        /// <c>score = score * 0.1f</c>——伪报部队会自己往回跑，打它等于浪费一次行动。
        /// </summary>
        public bool IsEscapeBuff()
        {
            return HasEffectClass(EscapeEffectClasses);
        }

        static readonly string[] EscapeEffectClasses = { "Escape" };

        // ==================== 能力百分比修正 ====================
        //
        // 原版 unit_calc_stat 里有个 state_coef：混乱状态 ×0.8 同时作用于
        // Attack / Defence / BuildPower（s11_scen_obj_unit.cpp:995-1051）。
        // 本项目不用 "state_coef" 这种写死分支，而是让每个 Buff 自己声明乘算系数，
        // 由 Troop.Attack / Defence / BuildPower 实时读取（100 = 不变）。

        /// <summary>对部队攻击力的百分比修正（100 = 不变，80 = ×0.8）</summary>
        [JsonProperty] public int troopAttackCoef = 100;

        /// <summary>对部队防御力的百分比修正</summary>
        [JsonProperty] public int troopDefenceCoef = 100;

        /// <summary>对部队建设力的百分比修正</summary>
        [JsonProperty] public int troopBuildCoef = 100;

        /// <summary>
        /// 对部队**移动力**的百分比修正（100 = 不变，0 = 完全无法移动）。
        ///
        /// 【用途】足止（Ashidome）状态：`= 0` 时该部队行动力归零，只能原地攻击/施法。
        /// 【为什么用系数】与 <see cref="troopAttackCoef"/> 同一套思路：
        /// 状态差异全部由数据声明，代码只做累乘，新增状态不必改 Troop。
        /// </summary>
        [JsonProperty] public int troopMoveCoef = 100;

        /// <summary>
        /// 持有该 Buff 的部队**不向周围施加 ZOC**（对应原版 <c>UnitState_Konran</c> 的副作用）。
        ///
        /// 【原版依据】<c>System::is_zoc_pos</c> 的部队分支：
        /// <code>
        /// else if (hex.get_unit_id() != -1) {
        ///     const Unit* unit = get_unit(hex.get_unit_id());
        ///     if (unit->get_state() != UnitState_Konran and self->is_enemy(unit))
        ///         return true;
        /// }
        /// </code>
        /// （s11_sys_unit_path.cpp:359-364）—— 混乱部队不产生控制区域。
        ///
        /// 【为什么放在数据里】与 <see cref="troopAttackCoef"/> 同一套思路：状态差异由
        /// Buff 数据声明，代码只把它折叠成 <see cref="TroopStateFlag"/> 位标记。
        /// 混乱 Buff（Buffs.json Id=1）配 <c>"noZoc": true</c> 即生效。
        /// </summary>
        [JsonProperty] public bool noZoc;

        /// <summary>buffEffects 里是否挂了指定类名集合中的任意一个效果</summary>
        bool HasEffectClass(string[] classes)
        {
            if (buffEffects == null || classes == null || classes.Length == 0)
                return false;

            for (int i = 0; i < buffEffects.Count; i++)
            {
                JObject obj = buffEffects[i] as JObject;
                if (obj == null) continue;

                string cls = obj.Value<string>("class");
                if (string.IsNullOrEmpty(cls)) continue;

                for (int j = 0; j < classes.Length; j++)
                {
                    if (cls == classes[j])
                        return true;
                }
            }
            return false;
        }
    }
}
