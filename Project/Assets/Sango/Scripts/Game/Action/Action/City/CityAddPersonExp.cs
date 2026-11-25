using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 所在城市的在册武将每回合获得经验（劝学）。
    ///
    /// <para>
    /// 【口径】遍历 <c>City.allPersons</c>（所属关系），不是 <c>freePersons</c>（待命关系）：
    /// 出征在外、寄寓在野的武将不算"所在城市将领"，与设计稿的"所在城市将领经验值每回合提升"一致。
    /// </para>
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnCityTurnEnd"/>，每城每回合派发一次（<c>City.cs:2365</c>），
    /// 与"一年 36 回合"的节奏对齐；不放在逐帧或属性重算里，避免按次重复加经验。
    /// </para>
    /// <para>
    /// 【经验口径】调用 <c>Person.GainExp</c>（武将经验），它内部已处理等级与上限；
    /// 不走 <c>GainStatExp</c>（那是五维能力经验），以免把"经验值"和"能力成长"混成一件事。
    /// </para>
    /// </summary>
    public class CityAddPersonExp : CityActionBase
    {
        /// <summary>每回合获得的武将经验（CityActionBase 不含通用 value 字段，这里自行解析）</summary>
        int expPerTurn;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value（每回合获得的武将经验）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            expPerTurn = p.Value<int>("value");
            if (expPerTurn <= 0)
            {
                Sango.Log.Warning("CityAddPersonExp 的 value 未配置（或 <= 0），不会生效");
                return;
            }

            // 必须有具体城市宿主：势力级无法确定"所在城市"
            if (City == null)
            {
                Sango.Log.Warning("CityAddPersonExp 没有绑定城市宿主，不会生效");
                return;
            }

            GameEvent.OnCityTurnEnd += OnCityTurnEnd;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCityTurnEnd -= OnCityTurnEnd;
        }

        /// <summary>
        /// 回合结束时给城内武将加经验。
        /// </summary>
        /// <param name="city">回合结束的城市</param>
        /// <param name="scenario">场景对象</param>
        void OnCityTurnEnd(City city, Scenario scenario)
        {
            if (city == null || City == null || city != City)
                return;

            SangoObjectList<Person> persons = city.allPersons;
            if (persons == null)
                return;

            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                if (person == null || !person.IsAlive)
                    continue;
                person.GainExp(expPerTurn);
            }
        }
    }
}
