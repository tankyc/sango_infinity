using TKNewtonsoft.Json;
using System;

namespace Sango.Core
{
    /// <summary>
    /// 角色兵种适应力
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]

    public class PersonAbilityValue : IAarryDataObject
    {
        public int baseValue;
        public int valueExp;
        public int value;

        public override string ToString()
        {
            return $"{baseValue},{valueExp},{value}";
        }


        public AbilityLevelType AbilityLevelType
        {
            get
            {
                return Scenario.Cur.CommonData.AbilityLevelTypes.Get(value);
            }
        }

        public IAarryDataObject FromArray(int[] content)
        {
            int count = content.Length;
            if (count == 0) return this;
            if (count > 0) baseValue = content[0];
            if (count > 1) valueExp = content[1];
            if (count > 2) value = content[2];

            return this;
        }

        public int[] ToArray()
        {
            return new int[] { baseValue, valueExp, value };
        }

        public void Update()
        {
            value = (byte)(Math.Min(Scenario.Cur.Variables.MaxAbilityLevel, baseValue + (valueExp / Scenario.Cur.Variables.AbilityExpLevelNeed)));
        }
        public void SetExp(ushort exp)
        {
            if (value >= Scenario.Cur.Variables.MaxAbilityLevel)
                return;

            if (valueExp != exp)
            {
                valueExp = exp;
                Update();
            }
        }

        //public override void Load(BinaryReader reader)
        //{
        //    baseValue = reader.ReadByte();
        //    valueExp = reader.ReadUInt16();
        //    Update();
        //}

        //public override void Save(BinaryWriter writer)
        //{
        //    writer.Write(baseValue);
        //    writer.Write(valueExp);
        //}

    }
}
