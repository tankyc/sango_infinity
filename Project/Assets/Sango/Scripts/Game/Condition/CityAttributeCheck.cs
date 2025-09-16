using System;
using System.IO;

namespace Sango.Game.Condition
{
    public class CityAttributeCheck : Condition
    {
        public delegate int CityAttributeGetCall(City city);

        public int value;
        public int compareResult; //-2小于等于-1小于0等于1大于2大于等于
        public int cityId;  //0则不需要检测
        public CityAttributeGetCall getCall;
        public override ConditionType ConditionType { get { return ConditionType.CityAttributeCheck; } }

        public override bool Check(ConditionParams sanObj)
        {
            City target = sanObj.City;
            if (target == null) return false;

            if (cityId > 0 && target.Id != cityId)
                return false;

            // 可以当个城池ID检查器
            if (getCall == null) return true;
            int attr = getCall(target);
            if (compareResult == 0 && value == attr)
                return true;
            else if (compareResult == -2 && value <= attr)
                return true;
            else if (compareResult == -1 && value < attr)
                return true;
            else if (compareResult == 1 && value > attr)
                return true;
            else if (compareResult == 2 && value >= attr)
                return true;

            return false;
        }
        public override Condition Clone()
        {
            return new CityAttributeCheck()
            {
                value = value,
                compareResult = compareResult,
                cityId = cityId,
                getCall = getCall
            };
        }
    }
}
