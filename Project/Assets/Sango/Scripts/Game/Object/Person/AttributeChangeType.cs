using SimpleJSON;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class AttributeChangeType : SangoObject
    {
        public struct AgeFactor
        {
            public int age;
            public int factor;
            public AgeFactor(int a, int f) { age = a; factor = f; }
        }
        [JsonProperty]
        public List<AgeFactor> ageFactorList = new List<AgeFactor>();
        public int GetAgeFactor(int age)
        {
            if (ageFactorList.Count == 0)
                return 10000;

            if (age < 0) age = 0;

            AgeFactor target = ageFactorList[ageFactorList.Count - 1];
            for (int i = 0; i < ageFactorList.Count; i++)
            {
                AgeFactor factor = ageFactorList[i];
                if (age <= factor.age)
                {
                    target = factor;
                    break;
                }
            }
            return target.factor;
        }
    }
}
