using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 攻城时不受到据点反击（先登）。
    ///
    /// <para>
    /// 【与 BuildingBaseAttackBack 的区别】那个是势力级（`ForceBuildingActionBase`），
    /// 从**据点拥有方**改"据点反击伤害"，全势力所有据点一起变强/变弱；
    /// 本类从**攻方部队**改"本次反击伤害"，只对持技部队自己攻城时生效，
    /// 符合设计稿「先登：攻城不受到反击」。
    /// </para>
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnBuildCalculateAttackBack"/>，
    /// 参数里 <c>troop</c> 是发起攻击的部队、<c>buildingBase</c> 是被打的据点。
    /// 置 0 即"本次据点反击打不出来"，后续的反击动画与伤害统计都按 0 处理。
    /// </para>
    /// </summary>
    public class TroopIgnoreBuildingAttackBack : TroopActionBase
    {
        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：condition（可选）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (Troop == null && Force == null)
            {
                Sango.Log.Warning("TroopIgnoreBuildingAttackBack 既没有绑定部队也没有绑定势力，不会生效");
                return;
            }

            GameEvent.OnBuildCalculateAttackBack += OnBuildCalculateAttackBack;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnBuildCalculateAttackBack -= OnBuildCalculateAttackBack;
        }

        /// <summary>
        /// 据点反击伤害结算时，把持技部队遭到的反击清零。
        /// </summary>
        /// <param name="troop">发起攻击的部队</param>
        /// <param name="spellCell">战法目标格</param>
        /// <param name="buildingBase">被攻击的据点</param>
        /// <param name="skill">本次战法（普攻为 null）</param>
        /// <param name="overrideData">反击伤害</param>
        void OnBuildCalculateAttackBack(Troop troop, Cell spellCell, BuildingBase buildingBase, SkillInstance skill, OverrideData<int> overrideData)
        {
            if (troop == null || overrideData == null)
                return;

            // 归属过滤：必须是持技部队本人在攻城
            if (Troop != null)
            {
                if (troop != Troop)
                    return;
            }
            else if (Force != null)
            {
                if (troop.BelongForce != Force)
                    return;
            }
            else
            {
                return;
            }

            overrideData.Value = 0;
        }
    }
}
