using System;
using System.Collections;
using TKNewtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PersonAttributeValue : IAarryDataObject
    {
        /// <summary>
        /// 所属
        /// </summary>
        public Person master;

        /// <summary>
        /// 能力变化类型
        /// </summary>
        public AttributeChangeType changeType;

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

        /// <summary>
        /// 最终值
        /// </summary>
        public int Value => _value + extra_value;

        public override string ToString()
        {
            return $"{baseValue},{valueExp},{valueFacter},{_value}";
        }

        public IAarryDataObject FromArray(int[] content)
        {
            int count = content.Length;
            if (count == 0) return this;
            if (count > 0) baseValue = content[0];
            int changeId = 1;
            if (count > 1) changeId = content[1];
            if (changeId == 0) changeId = 1;
            changeType = GameData.Instance.ScenarioCommonData.AttributeChangeTypes.Get(changeId);
            if (count > 2) valueExp = content[2];
            if (count > 3) valueFacter = content[3];
            if (count > 4) _value = content[4];
            return this;
        }

        public int[] ToArray()
        {
            return new int[] { baseValue, changeType.Id, valueExp, valueFacter, _value };
        }

        public void Update()
        {
            _value = ((baseValue * changeType.GetAgeFactor(master.Age)) / 10000 + Math.Min(Scenario.Cur.Variables.MaxAttributeGet, (valueExp / Scenario.Cur.Variables.AttributeExpLevelNeed))) * valueFacter / 10000;
        }
        public void SetExp(int exp)
        {
            if (_value - baseValue >= Scenario.Cur.Variables.MaxAttributeGet)
                return;

            if (valueExp != exp)
            {
                valueExp = exp;
                Update();
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

        //public void OnPersonAgeUpdate(Person person)
        //{
        //    AttributeChangeType personAbilityChangeType = Scenario.Cur.CommonData.AttributeChangeTypes.Get(changeType);
        //    if (personAbilityChangeType == null)
        //        Sango.Log.Error(changeType);
        //    ushort factor = personAbilityChangeType.GetAgeFactor(person.Age);
        //    SetFacter(factor);
        //}
    }
}
