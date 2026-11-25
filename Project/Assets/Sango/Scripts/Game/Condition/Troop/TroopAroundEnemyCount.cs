using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 邻接敌军数量检查条件（<see cref="TroopAroundTeammateCount"/> 的敌军版）。
    ///
    /// <para>
    /// 【用途】支撑"被包围才生效"的特技设计（例如四面受敌时增伤、被围时减伤）。
    /// 现有条件类只能数邻格有几支友军，数不了敌军。
    /// </para>
    /// <para>
    /// 【统计口径】只统计紧邻的一圈（与 Cell.GetNeighbors 一致，与友军版完全相同），
    /// 不提供半径参数 —— 需要两格 / 三格范围的效果应走光环（AuraActionBase），
    /// 不要在这里放开半径，否则每帧热路径上的开销会失控。
    /// </para>
    /// </summary>
    public class TroopAroundEnemyCount : Condition
    {
        /// <summary>参与比较的常量</summary>
        int count;

        /// <summary>true = 统计施法方邻格，false = 统计目标邻格</summary>
        bool compareTarget;

        /// <summary>比较运算符（eq / gt / lt / gte / lte）</summary>
        string @operator;

        /// <summary>
        /// 初始化条件。
        /// <para>JSON 参数：count、compareTarget（缺省 self）、operator（缺省 gte）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            count = p.Value<int>("count");
            compareTarget = (p.Value<string>("compareTarget") ?? "self") == "self";
            @operator = p.Value<string>("operator") ?? "gte";
        }

        /// <summary>
        /// 检查邻格敌军数量是否满足条件。
        /// </summary>
        /// <param name="database">条件上下文</param>
        /// <returns>满足返回 true；部队或所在格为空时返回 false</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop troop = compareTarget ? database.ActionTroop : database.TargetTroop;
            if (troop == null || troop.cell == null)
                return false;

            // 【势力基准】与友军版保持一致：始终以"施法方"为基准判敌我。
            // 否则 compareTarget=target 时会把目标周围的友军误当成敌军来数。
            Troop basis = database.ActionTroop ?? troop;

            int num = 0;
            troop.cell.GetNeighbors(x =>
            {
                if (x != null && x.troop != null && !x.troop.IsSameForce(basis))
                    num++;
            });

            return GameUtility.CheckCondition(count, @operator, num);
        }
    }
}
