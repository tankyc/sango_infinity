//using System.IO;

//namespace Sango.Game.Condition
//{
//    public class CityCommerce : Condition
//    {
//        public int value;
//        public int compareResult; //-2小于等于-1小于0等于1大于2大于等于
//        public int cityId;  //0则不需要检测

//        public override ConditionType ConditionType { get { return ConditionType.CityCommerce; } }

//        public override bool Check(SangoObject sanObj)
//        {
//            if (sanObj.ObjectType != SangoObjectType.City)
//                return false;

//            City target = (City)sanObj;
//            if (cityId > 0 && target.Id != cityId)
//                return false;

//            if (compareResult == 0 && value == target.commerce)
//                return true;
//            else if (compareResult == -2 && value <= target.commerce)
//                return true;
//            else if (compareResult == -1 && value < target.commerce)
//                return true;
//            else if (compareResult == 1 && value > target.commerce)
//                return true;
//            else if (compareResult == 2 && value >= target.commerce)
//                return true;

//            return false;
//        }
//        public override Condition Clone()
//        {
//            return new CityCommerce() { value = value, compareResult = compareResult, cityId = cityId };
//        }
//    }
//}
