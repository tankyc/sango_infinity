using System;
using System.Collections;
using Newtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PersonAttributeValue : IAarryDataObject
    {
        public int changeId = 5;
        AttributeChangeType _changeType;
        /// <summary>
        /// 能力变化类型
        /// </summary>
        public AttributeChangeType changeType
        {
            get
            {
                if (_changeType == null)
                {
                    if (changeId == 0) changeId = 5;
                    _changeType = GameData.Instance.ScenarioCommonData.AttributeChangeTypes.Get(changeId);
                }
                return _changeType;
            }
            set
            {
                _changeType = value;
            }
        }

        /// <summary>
        /// 基础能力
        /// </summary>
        public int baseValue;

        /// <summary>
        /// 能力经验
        /// </summary>
        public int valueExp;

        /// <summary>
        /// 能力万分比
        /// </summary>
        public int valueFacter = 10000;

        /// <summary>
        /// 额外值
        /// </summary>
        public int extra_value;

        /// <summary>
        /// 能力值,计算出来的
        /// </summary>
        public int _value;

        int expAddValue;
        int ageAddValue;


        /// <summary>
        /// 最终值
        /// </summary>
        public int Value => _value + extra_value;

        public override string ToString()
        {
            return $"{baseValue},{changeType.Id},{valueExp},{valueFacter},{_value}";
        }

        public IAarryDataObject FromArray(int[] content)
        {
            int count = content.Length;
            if (count == 0) return this;
            if (count > 0) baseValue = content[0];

            if (count > 1) changeId = content[1];
            if (changeId == 0) changeId = 5;
            if (count > 2) valueExp = content[2];
            if (count > 3) valueFacter = content[3];
            if (count > 4) _value = content[4];

            return this;
        }

        public int[] ToArray()
        {
            return new int[] { baseValue, changeType.Id, valueExp, valueFacter, _value };
        }

        public void UpdateNoAge()
        {
            Update(0, null);
        }

        public void Update(int age, Scenario scenario)
        {
            if(age > 0)
            {
                expAddValue = Math.Min(scenario.Variables.MaxAttributeGet, (valueExp / scenario.Variables.AttributeExpLevelNeed));
                ageAddValue = changeType.GetAgeFactor(age) / 10000;
                Update();
            }
            else
            {
                _value = baseValue;
            }
        }

        void UpdateExpValue(Scenario scenario)
        {
            expAddValue = Math.Min(scenario.Variables.MaxAttributeGet, (valueExp / scenario.Variables.AttributeExpLevelNeed));
            _value = baseValue + (expAddValue + ageAddValue) * valueFacter / 10000;
        }

        void UpdateAgeValue(int age, Scenario scenario)
        {
            Update(age, scenario);
        }

        void Update()
        {
            _value = baseValue + (expAddValue + ageAddValue) * valueFacter / 10000;
        }

        public void SetExp(int exp, Scenario scenario)
        {
            if (scenario == null) return;

            int expNeed = scenario.Variables.AttributeExpLevelNeed;
            if (expNeed <= 0) return;                                   // 防止数据配置为 0 时除零

            // 上限直接按经验实算：expAddValue 是缓存字段，只有 Update/UpdateExpValue 之后才有效，
            // 存档刚载入还没刷新时它可能是 0，用它做封顶判断会失效（经验能越过 MaxAttributeGet）。
            int expMax = scenario.Variables.MaxAttributeGet * expNeed;
            if (valueExp >= expMax) return;                             // 已到顶：保持原样（与旧行为一致）

            int next = Math.Min(exp, expMax);
            if (valueExp != next)
            {
                valueExp = next;
                UpdateExpValue(scenario);
            }
        }
        public void SetFacter(int facter)
        {
            if (facter != valueFacter)
            {
                valueFacter = facter;
                Update();
            }
        }
    }
}
