using System;
using System.Collections;
using Newtonsoft.Json;

namespace Sango.Game
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
        /// 最终能力值
        /// </summary>
        public int value;

        public override string ToString()
        {
            return $"{baseValue},{valueExp},{valueFacter},{value}";
        }

        public IAarryDataObject FromArray(int[] content)
        {
            int count = content.Length;
            if (count == 0) return this;
            if (count > 0) baseValue = content[0];
            int changeId = 1;
            if (count > 1) changeId = content[1];

            changeType = Scenario.Cur.CommonData.AttributeChangeTypes.Get(changeId);
            if (count > 2) valueExp = content[2];
            if (count > 3) valueFacter = content[3];
            if (count > 4) value = content[4];
            return this;
        }

        public int[] ToArray()
        {
            return new int[] { baseValue, changeType.Id, valueExp, valueFacter, value };
        }

        public void Update()
        {
            value = ((baseValue * changeType.GetAgeFactor(master.Age)) / 10000 + Math.Min(Scenario.Cur.Variables.MaxAttributeGet, (valueExp / Scenario.Cur.Variables.AbilityExpLevelNeed))) * valueFacter / 10000;
        }
        public void SetExp(int exp)
        {
            if (value - baseValue >= Scenario.Cur.Variables.MaxAttributeGet)
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
