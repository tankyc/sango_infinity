using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 提高登用武将的成功率（伯乐「登庸成功率 +30%」、蜀相「人事成功率加倍」、论客等）。
    ///
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnPersonRecruitProbability"/>，
    /// 由 <c>GameFormula.RecruitPersonProbability</c> 在返回最终成功率前询问
    /// （<c>GameFormula.cs:252</c> 附近）。派发点会做 0-100 截断，所以本类不必自己夹取。
    /// </para>
    /// <para>
    /// 【与 Override 委托的区别】<c>RecruitPersonProbabilityOverride</c> 是"整段替换"，
    /// 谁最后赋值谁生效；本事件是增量修正，同一批特技可以叠加。
    /// </para>
    /// <para>
    /// 【mode】0 = 加算百分点（伯乐 +30）；1 = 按百分比缩放（蜀相"成功率翻倍" = 100）。
    /// </para>
    /// </summary>
    public class PersonImproveRecruitProbability : PersonActionBase
    {
        /// <summary>修正量（百分点或百分比，取决于 mode）</summary>
        int value;

        /// <summary>0 加算百分点 / 1 按百分比缩放</summary>
        int mode;

        /// <summary>可选附加条件</summary>
        Condition condition;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value、mode（缺省 0）、condition（可选）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            value = p.Value<int>("value");
            mode = p.Value<int>("mode");

            if (Person == null && Force == null)
            {
                Sango.Log.Warning("PersonImproveRecruitProbability 没有绑定武将也没有绑定势力，不会生效");
                return;
            }
            if (value == 0)
            {
                Sango.Log.Warning("PersonImproveRecruitProbability 的 value 未配置，不会生效");
                return;
            }

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                    Sango.Log.Error($"登用概率 Action 的条件类未注册：{conObj.Value<string>("class")}，已忽略该条件");
            }

            GameEvent.OnPersonRecruitProbability += OnPersonRecruitProbability;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnPersonRecruitProbability -= OnPersonRecruitProbability;
        }

        /// <summary>
        /// 登用成功率结算时修正。
        /// </summary>
        /// <param name="actor">执行登用的武将</param>
        /// <param name="target">被登用的目标武将</param>
        /// <param name="type">登用类型（PersonRecruitType）</param>
        /// <param name="rateData">成功率（0-100）</param>
        void OnPersonRecruitProbability(Person actor, Person target, int type, Tools.OverrideData<int> rateData)
        {
            if (actor == null || rateData == null)
                return;

            // 归属过滤：特技持有者必须就是这次执行登用的人
            if (Person != null)
            {
                if (actor != Person)
                    return;
            }
            else if (Force != null)
            {
                if (actor.BelongForce != Force)
                    return;
            }
            else
            {
                return;
            }

            if (condition != null)
            {
                DefaultConditionDatabase db = new DefaultConditionDatabase
                {
                    ActionPerson = actor,
                    TargetPerson = target,
                    ActionForce = actor.BelongForce,
                    ActionObject = actor,
                };
                if (!condition.Check(db))
                    return;
            }

            rateData.Value = mode == 1
                ? rateData.Value * (100 + value) / 100
                : rateData.Value + value;
        }
    }
}
