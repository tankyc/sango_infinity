//using System.IO;

//namespace Sango.Game.Condition
//{
//    public class CityAgriculture : Condition
//    {
//        public int value;
//        public int compareResult; //-2小于等于-1小于0等于1大于2大于等于
//        public int cityId;  //0则不需要检测

//        public override ConditionType ConditionType { get { return ConditionType.CityAgriculture; } }

//        public override bool Check(SangoObject sanObj)
//        {
//            if (sanObj.ObjectType != SangoObjectType.City)
//                return false;

//            City target = (City)sanObj;
//            if (cityId > 0 && target.Id != cityId)
//                return false;

//            if (compareResult == 0 && value == target.agriculture)
//                return true;
//            else if (compareResult == -2 && value <= target.agriculture)
//                return true;
//            else if (compareResult == -1 && value < target.agriculture)
//                return true;
//            else if (compareResult == 1 && value > target.agriculture)
//                return true;
//            else if (compareResult == 2 && value >= target.agriculture)
//                return true;

//            return false;
//        }
//        public override Condition Clone()
//        {
//            return new CityAgriculture() { value = value, compareResult = compareResult, cityId = cityId };
//        }
//    }
//}
