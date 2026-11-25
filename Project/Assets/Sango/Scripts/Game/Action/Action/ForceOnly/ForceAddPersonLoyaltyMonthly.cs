using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 仁德：每月自动提升本势力所有武将的忠诚（每月 +1）。
    ///
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnMonthStart"/>，全局每月派发一次（`Scenario`）。
    /// 本类只处理自己绑定的势力，别的势力不受影响。
    /// </para>
    /// <para>
    /// 【为什么不遍历部队或城池】忠诚是武将属性，与是否出征、在哪座城无关，
    /// 因此直接扫 <c>Scenario.personSet</c> 并按 <c>BelongForce</c> 过滤，
    /// 避免"城墙上的太守涨了、出征的没涨"这种漏算。
    /// </para>
    /// <para>
    /// 【性能】每个月一次、O(全势力武将)，量级在几百次整数加法，可忽略。
    /// </para>
    /// </summary>
    public class ForceAddPersonLoyaltyMonthly : ForceActionBase
    {
        /// <summary>每月提升的忠诚点数</summary>
        int addLoyalty;

        /// <summary>忠诚上限（默认 100）</summary>
        int maxLoyalty;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value（每月忠诚点数）、max（上限，缺省 100）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (Force == null)
            {
                Sango.Log.Error("ForceAddPersonLoyaltyMonthly 必须绑定势力，当前宿主不是势力，不会生效");
                return;
            }

            addLoyalty = p.Value<int>("value");
            maxLoyalty = p.Value<int?>("max") ?? 100;

            if (addLoyalty <= 0)
            {
                Sango.Log.Warning("ForceAddPersonLoyaltyMonthly 的 value 未配置（或 <= 0），不会生效");
                return;
            }

            GameEvent.OnMonthStart += OnMonthStart;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnMonthStart -= OnMonthStart;
        }

        /// <summary>
        /// 每月给本势力全部在籍武将加忠诚。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        void OnMonthStart(Scenario scenario)
        {
            if (Force == null || scenario == null)
                return;

            SangoObjectSet<Person> persons = scenario.personSet;
            if (persons == null)
                return;

            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                if (person == null || !person.IsAlive)
                    continue;
                if (person.BelongForce != Force)
                    continue;

                int loyalty = person.loyalty + addLoyalty;
                if (loyalty > maxLoyalty)
                    loyalty = maxLoyalty;
                if (loyalty < 0)
                    loyalty = 0;
                person.loyalty = loyalty;
            }
        }
    }
}
