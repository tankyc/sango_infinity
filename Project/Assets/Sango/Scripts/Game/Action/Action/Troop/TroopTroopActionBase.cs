using Sango.Core.Tools;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队→部队类效果基类（"归属部队"与"被作用部队"分离的场景）。
    ///
    /// 公共参数：
    /// kinds      兵种类型过滤，null 表示不限制
    /// isDefender 0 攻击方 / 1 受击方
    /// isNormal   -1 不限 / 0 仅战法 / 1 仅一般攻击
    /// isRange    -1 不限 / 1 远程 / 2 近战
    /// isFire     0 不限 / 1 仅一般攻击（当前实现与 isNormal 重叠，数据中未使用）
    /// condition  额外条件（TroopSkillConditionDatabase）
    /// </summary>
    public abstract class TroopTroopActionBase : TroopActionBase
    {
        protected int checkLand;
        protected int isDefender;
        protected int isNormal;
        protected int isFire;
        protected int isRange;
        protected Condition condition;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            checkLand = p.Value<int>("checkLand");
            isDefender = p.Value<int>("isDefender");
            isNormal = p.Value<int>("isNormal");
            isRange = p.Value<int>("isRange");
            isFire = p.Value<int>("isFire");
            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                condition.Init(conObj, sangoObjects);
            }
        }

        protected virtual bool CheckTroop(Troop defencer, SangoObject atker)
        {
            Troop troop = defencer;
            SkillInstance skill = null;
            if(atker != null && atker.ObjectType == SangoObjectType.SkillInstance)
            {
                skill = (SkillInstance)atker;
            }

            if (isDefender == 0)
            {
                if (skill == null) 
                    return false;

                troop = skill.master;
            }
            if (Force != null && troop.BelongForce != Force) return false;
            if (Troop != null && Troop != troop) return false;

            if (!CheckIsNormalSkill(skill, isNormal))
                return false;

            if (!CheckIsRangeSkill(skill, isRange))
                return false;

            // isFire：Skill 上并没有"是否火焰战法"的判定，这里的实现退化为
            // "isFire == 1 时只放行一般攻击"，与 isNormal == 1 完全重叠。
            // 现有配置未使用，建议留空；等 Skill 增加火焰标记后再实现真正的火焰过滤。
            if (isFire == 1 && skill != null)
                return false;

            if (checkLand == 1 && troop.IsInWater)
                return false;
            if (checkLand == 2 && !troop.IsInWater)
                return false;

            if (checkLand == 0 && kinds != null && !kinds.Contains(troop.LandTroopType.kind) && !kinds.Contains(troop.WaterTroopType.kind))
                return false;

            if (checkLand == 1 && kinds != null && !kinds.Contains(troop.LandTroopType.kind))
                return false;

            if (checkLand == 2 && kinds != null && !kinds.Contains(troop.WaterTroopType.kind))
                return false;

            if (condition != null && !condition.Check(new TroopActionConditionDatabase(skill, defencer.cell)))
                return false;

            return true;
        }
    }
}
