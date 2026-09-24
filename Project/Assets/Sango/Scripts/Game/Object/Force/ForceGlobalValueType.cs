using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Sango.Core
{
    public enum GameEffectType
    {
        None = 0,

        /// <summary>
        /// 兵种攻击力(兵种id)
        /// </summary>
        TroopTypeAttack1,

        /// <summary>
        /// 兵种防御(兵种id)
        /// </summary>
        TroopTypeDefence1,

        /// <summary>
        /// 兵种移动力(兵种id)
        /// </summary>
        TroopTypeMoveAbility1,

        /// <summary>
        /// 兵种战法威力(兵种id)
        /// </summary>
        TroopTypeSkillPower1,

        /// <summary>
        /// 兵种替代战法(兵种id, 技能id)
        /// </summary>
        TroopTypeSkillReplace2,

        /// <summary>
        /// 兵种战法增加(兵种id)
        /// </summary>
        TroopTypeSkillAdd1,

        /// <summary>
        /// 兵种战法效果增加(兵种id)
        /// </summary>
        TroopTypeSkillEffectAdd1,

        /// <summary>
        /// 兵种被动效果增加(兵种id)
        /// </summary>
        TroopTypePassiveEffectAdd1,

        /// <summary>
        /// 部队带兵上限
        /// </summary>
        TroopLimit,

        /// <summary>
        /// 部队buff效果添加
        /// </summary>
        TroopAddBuffEffectAdd,



    }
}
