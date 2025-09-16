using Sango.Game.Battle.Buff;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Sango.Game.Battle.Core
{
    public class BattleUtility
    {
        public static int SortPersonByTroops(BattlePerson a, BattlePerson b)
        {
            if (a.troops > b.troops)
                return 1;
            else if (a.troops < b.troops)
                return -1;
            else
                return 0;
        }


        public static List<BattlePerson> WeightRandom(BattleInstance battle, List<BattlePerson> list, int num)
        {
            if (list == null)
                return null;

            int totalWeight = 0;
            for (int i = 0, cout = list.Count; i < cout; ++i)
                totalWeight += list[i].seat.weight;

            //List<BattlePerson> safe_list = new List<BattlePerson>(list);
            List<BattlePerson> safe_list = (list);
            safe_list.Sort((a, b) =>
            {
                if (a.seat.weight < b.seat.weight) return -1;
                else if (a.seat.weight > b.seat.weight)
                    return 1;
                return 0;
            });

            List<BattlePerson> result = new List<BattlePerson>();
            for (int j = 0; j < num; ++j)
            {
                int ran = battle.Random(0, totalWeight);
                for (int k = 0, count = safe_list.Count; k < count; ++k)
                {
                    int w = safe_list[k].seat.weight;
                    if (ran < w)
                    {
                        totalWeight = totalWeight - w;
                        result.Add(safe_list[k]);
                        safe_list.RemoveAt(k);
                        break;
                    }
                    else
                    {
                        ran = ran - w;
                    }
                }
            }
            return result;
        }
        public static List<BattlePerson> ListRandom(BattleInstance battle, List<BattlePerson> list, int num)
        {
            if (list == null)
                return null;
            //List<BattlePerson> safe_list = new List<BattlePerson>(list);
            List<BattlePerson> safe_list = (list);
            List<BattlePerson> result = new List<BattlePerson>();
            int listCount = safe_list.Count;
            for (int j = 0; j < num; ++j)
            {
                if (listCount > 0)
                {
                    int ran = battle.Random(0, list.Count - 1);
                    result.Add(safe_list[ran]);
                    safe_list.RemoveAt(ran);
                    listCount--;
                }
            }
            return result;
        }

        public static int SortHeroByOrder(BattlePerson a, BattlePerson b)
        {
            byte order_a = a.Order;
            byte order_b = b.Order;
            if (order_a == order_b)
            {
                if (a.Speed == b.Speed)
                    return 0;
                else if (a.Speed < b.Speed)
                    return -1;
                else return 1;
            }
            else if (order_a < order_b)
                return -1;
            else
                return 1;
        }


        public static bool FindBuffWithID(BattleBuff buff, int id)
        {
            return buff.id == id;
        }

        public static bool FindBuffWithType(BattleBuff buff, byte t)
        {
            return buff.buffType == t;
        }

        public static bool IsDeadBuff(BattleBuff buff)
        {
            return !buff.IsAlive;
        }
    }
}
