using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 军团 AI 命令集合。
    /// <para>
    /// partial 拆分：CorpsAIDistrict.cs 承载"行动力分配 + 据点轮转调度"
    /// （对齐原版 s11_game_ai_district*.cpp 的 district_ai / district_command）。
    /// </para>
    /// </summary>
    public partial class CorpsAI
    {
        public static bool AICities(Corps corps, Scenario scenario)
        {
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == corps.BelongForce && c.BelongCorps == corps && !c.ActionOver)
                {
                    if (!c.DoAI(scenario))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 部队调度：收集本军团未行动的部队 → 按原版优先级排序 → 逐支执行。
        ///
        /// 对齐出处：Open311 `district_unit_ai`（s11_game_ai_unit.cpp:26-80）。原实现直接
        /// 按 <c>troopsSet</c> 的存储顺序遍历，缺少原版的排序环节；排序会实打实影响结果 ——
        /// 例如"正在移动的补给队"应当先于"原地待命的部队"行动，否则后者可能先占掉格子。
        /// <para>
        /// 【排序键】原版四级：请求势力 &gt; 指令优先级 &gt; 携带金 &gt; Id。
        /// 本项目没有部队级的"请求加入势力"字段（`Force.requestForceId` 是势力级援军请求，
        /// 语义不同），因此跳过第一级，其余三级逐项对齐。
        /// </para>
        /// </summary>
        /// <param name="corps">军团</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>全部部队执行完毕返回 true</returns>
        public static bool AITroops(Corps corps, Scenario scenario)
        {
            // ① 收集本军团未行动的部队 —— :31-43
            List<Troop> unitList = new List<Troop>();
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == corps.BelongForce
                    && c.BelongCorps == corps && !c.ActionOver)
                {
                    unitList.Add(c);
                }
            }

            // ② 排序 —— :45-68
            unitList.Sort((a, b) =>
            {
                // 指令优先级降序
                int ap = GetUnitOrderPriority(a.missionType);
                int bp = GetUnitOrderPriority(b.missionType);
                if (ap != bp)
                    return bp.CompareTo(ap);
                // 携带金降序
                if (a.gold != b.gold)
                    return b.gold.CompareTo(a.gold);
                // Id 升序
                return a.Id.CompareTo(b.Id);
            });

            // ③ 逐支执行 —— :69-78
            for (int i = 0; i < unitList.Count; ++i)
            {
                if (!unitList[i].DoAI(scenario))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 部队指令优先级。
        ///
        /// 对齐出处：s11_game_ai_unit.cpp:8-22 的 <c>UnitOrderPriority</c> 表：
        /// <code>
        /// 待机 -1 / 移动 5 / 安装 2 / 攻击 3 / 撤退 5 / 咬住 4 /
        /// 护卫 1 / 攻城 -1 / 修复 2 / 征服 3 / 补给 5
        /// </code>
        /// 本项目按**任务性质**映射到同名档位（不按枚举值，枚举值只是存储顺序）。
        /// </summary>
        /// <param name="missionType">部队任务类型</param>
        /// <returns>优先级（越大越先行动）</returns>
        static int GetUnitOrderPriority(int missionType)
        {
            switch ((MissionType)missionType)
            {
                // 待机 / 攻城：原版给 -1（最末）
                case MissionType.None:
                case MissionType.TroopStay:
                    return -1;

                // 移动 / 撤退 / 补给 —— 5
                case MissionType.TroopMovetoCity:
                case MissionType.TroopMovetoCell:
                case MissionType.TroopMovetoBuild:
                case MissionType.TroopReturnCity:
                case MissionType.TroopTransformGoodsToCity:
                case MissionType.TroopSupplyTroop:
                case MissionType.TroopAskSupply:
                    return 5;

                // 攻击 / 征服 —— 3
                case MissionType.TroopDestroyTroop:
                case MissionType.TroopHarassCity:
                case MissionType.TroopBanishTroop:
                case MissionType.TroopOccupyCity:
                case MissionType.TroopDestroyBuilding:
                    return 3;

                // 安装 / 修复 —— 2
                case MissionType.TroopBuildBuilding:
                case MissionType.TroopFixBuilding:
                    return 2;

                // 护卫 —— 1
                case MissionType.TroopProtectBuilding:
                case MissionType.TroopProtectTroop:
                case MissionType.TroopProtectCity:
                    return 1;

                default:
                    return -1;
            }
        }

    }
}
