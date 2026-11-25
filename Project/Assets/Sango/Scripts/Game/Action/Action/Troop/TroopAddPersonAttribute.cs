using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 特技五维可逆修正（统率 / 武力 / 智力 / 政治 / 魅力）。
    ///
    /// <para>
    /// 与 <see cref="TroopModifyAttributeByCondition"/> 的区别：那个改部队的攻击 / 防御 /
    /// 移动力，这个改武将个人五维（RawCommand 等），从而影响属性光环、单挑、内政等所有读取点。
    /// </para>
    /// <para>
    /// 【可逆】每次 <c>Troop.CalculateAttribute</c> 先清零本队武将的 feature 加成，
    /// 再由本 Action 重新累加；卸下来源 / 换将 / 条件不满足都会自然回到原值。
    /// </para>
    /// <para>
    /// JSON 参数：stat（0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力）、
    /// mode（0 加算 / 1 按当前值百分比乘算）、value、targetType（0 仅主将 / 1 本队所有）、condition。
    /// </para>
    /// </summary>
    public class TroopAddPersonAttribute : TroopTroopActionBase
    {
        /// <summary>属性：0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力</summary>
        int stat;

        /// <summary>计算方式：0 加算固定值（默认），1 按百分比乘算</summary>
        int mode;

        /// <summary>作用对象：0 仅主将 / 1 本队所有武将（默认）</summary>
        int targetType;

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            stat = p.Value<int>("stat");
            mode = p.Value<int>("mode");
            targetType = p.Value<int>("targetType");
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        /// <summary>部队属性重算时给本队武将加五维修正</summary>
        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (troop == null || Troop == null || troop != Troop)
                return;

            if (targetType == 0)
            {
                Apply(troop, troop.Leader);
                return;
            }

            Apply(troop, troop.Leader);
            Apply(troop, troop.Member1);
            Apply(troop, troop.Member2);
        }

        /// <summary>给单个武将累加修正</summary>
        void Apply(Troop troop, Person person)
        {
            if (person == null)
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            int current = GetRaw(person);
            int add = mode == 1 ? current * value / 100 : value;
            if (add == 0)
                return;

            SetFeature(person, GetFeature(person) + add);
        }

        /// <summary>取武将当前五维（含装备与已有 feature 加成）</summary>
        int GetRaw(Person p)
        {
            switch (stat)
            {
                case 1: return p.RawStrength;
                case 2: return p.RawIntelligence;
                case 3: return p.RawPolitics;
                case 4: return p.RawGlamour;
                default: return p.RawCommand;
            }
        }

        /// <summary>取武将当前 feature 加成值</summary>
        int GetFeature(Person p)
        {
            switch (stat)
            {
                case 1: return p.featureStrength;
                case 2: return p.featureIntelligence;
                case 3: return p.featurePolitics;
                case 4: return p.featureGlamour;
                default: return p.featureCommand;
            }
        }

        /// <summary>写入武将 feature 加成值</summary>
        void SetFeature(Person p, int v)
        {
            switch (stat)
            {
                case 1: p.featureStrength = v; break;
                case 2: p.featureIntelligence = v; break;
                case 3: p.featurePolitics = v; break;
                case 4: p.featureGlamour = v; break;
                default: p.featureCommand = v; break;
            }
        }
    }
}
