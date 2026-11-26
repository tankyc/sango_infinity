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
        /// 本 Buff 声明的**状态位**（取值见 <see cref="TroopStateFlag"/>，0 = 未配置）。
        ///
        /// 【为什么允许数据显式声明】绝大多数情况由 <see cref="buffEffects"/> 的效果类名自动推导
        /// （见 <see cref="GetStateFlag"/>），零配置即可生效；该字段是给 Mod / 特殊情况留的
        /// **强制覆盖**入口（例如"某个效果类要按 Buff 区分成不同状态"）。
        /// 一旦非 0，就不再按类名推导，避免数据与代码两套真相互相打架。
        /// </summary>
        [JsonProperty] public int stateFlag;

        /// <summary>
        /// 本 Buff 对部队状态位域的**贡献值**。
        ///
        /// 【写入 / 移除成对】该方法只负责"算"，不负责"改"。真正的置位 / 清位由
        /// <see cref="BuffManager.GetStateFlags"/> 在每次 Buff 增删后整体重算一次
        /// （见 <see cref="TroopStateFlag"/> 的说明）。这样任何移除路径都不会漏清位，
        /// 同一部队叠两个同类 Buff 时移除其中一个也不会把状态误清掉。
        ///
        /// 【推导规则】
        ///   · <c>noZoc = true</c> → 追加 <see cref="TroopStateFlag.Chaos"/>（混乱不施加 ZOC）；
        ///   · <see cref="stateFlag"/> 非 0 → 以数据为准，不再推导；
        ///   · 否则按 <see cref="buffEffects"/> 里的效果类名逐条映射。
        /// </summary>
        /// <returns>状态位掩码（0 = 不产生任何状态）</returns>
        public int GetStateFlag()
        {
            int flag = stateFlag;

            if (noZoc)
                flag |= (int)TroopStateFlag.Chaos;

            // 数据显式声明优先：避免"策划只想给状态位、却因为效果类名被追加了别的位"
            if (stateFlag != 0)
                return flag;

            if (buffEffects == null || buffEffects.Count == 0)
                return flag;

            for (int i = 0; i < buffEffects.Count; i++)
            {
                JObject obj = buffEffects[i] as JObject;
                if (obj == null) continue;

                string cls = obj.Value<string>("class");
                if (string.IsNullOrEmpty(cls)) continue;

                flag |= GetEffectStateFlag(cls);
            }
            return flag;
        }

        /// <summary>
        /// 单个效果类名 → 状态位。
        ///
        /// 【为什么是 switch 而不是反射】效果类名是数据里的短字符串、数量有限，
        /// switch 编译成跳转表，且新增效果类时在这里补一行即可，
        /// 不会出现"反射拿到类名却漏配"的隐性失败。
        /// </summary>
        /// <param name="effectClass">buffEffects[].class 的类名</param>
        /// <returns>状态位掩码（未识别的类返回 0）</returns>
        static int GetEffectStateFlag(string effectClass)
        {
            switch (effectClass)
            {
                case "Stun": return (int)TroopStateFlag.Stun;

                // 兼容 Mod 里可能用的"混乱"类名：项目内置实现叫 Stun，但数据若写成 Chaos，
                // BuffEffect.Create 取不到效果类（只影响表现/回合逻辑），状态位仍要能推导出来，
                // 否则 ZOC 抑制会失效。
                case "Chaos": return (int)(TroopStateFlag.Chaos | TroopStateFlag.Stun);
                case "Escape": return (int)TroopStateFlag.Escape;
                case "MoveSeal": return (int)TroopStateFlag.MoveSeal;
                case "Berserk": return (int)TroopStateFlag.Berserk;
                case "Silence": return (int)TroopStateFlag.Silence;
                case "Poison": return (int)TroopStateFlag.Poison;
                case "Burn": return (int)TroopStateFlag.Burn;

                // 冰冻的字面语义就是"无法行动"，因此同时置 Freeze 与 Stun：
                // 业务侧只判 Stun 也能正确拦住行动，需要区分来源时再查 Freeze。
                case "Freeze": return (int)(TroopStateFlag.Freeze | TroopStateFlag.Stun);

                case "Invincible": return (int)TroopStateFlag.Invincible;
                case "Shield": return (int)TroopStateFlag.Shield;

                // AddAttack / AddDefence 是纯数值修正（直接改 extraAttack / extraDefence），
                // 不属于"异常状态"，不占状态位。
                default: return 0;
            }
        }

        /// <summary>
        /// 本 Buff 是否会让部队**无法正常行动**（原版 <c>state != Normal</c>）。
        ///
        /// 【修复说明】旧实现恒返回 <c>true</c>，导致
        ///   · <c>Troop.HasControlBuff()</c> 对任何 Buff（包括加攻/加防这类增益）都为真；
        ///   · 反击判定 <c>SkillInstance</c> 里"被控则不能反击"因此对所有携带 Buff 的部队生效；
        ///   · AI 评分把"目标身上有任何 Buff"当成"目标被控"来加权。
        /// 现改为与状态位域同一套语义（<see cref="TroopStateFlags.ControlMask"/>），
        /// 避免"类名判定"与"状态位判定"出现两套结论。
        /// </summary>
        public bool IsControlBuff()
        {
            return (GetStateFlag() & TroopStateFlags.ControlMask) != 0;
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
            return (GetStateFlag() & (int)TroopStateFlag.Escape) != 0;
        }

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

    }
}
