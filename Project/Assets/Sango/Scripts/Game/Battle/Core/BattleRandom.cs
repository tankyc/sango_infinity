
using System;

namespace Sango.Game.Battle.Core
{
    public class BattleRandom
    {
        Random Random;

        public BattleRandom()
        {
            Random = new Random();
        }

        public BattleRandom(int seed)
        {
            Random = new Random(seed);
        }

        public int RandInt(int lft, int rht)
        {
            return Random.Next(lft, rht);
        }

        public int Gen()
        {
            return Random.Next(0, 10000);
        }
    }
}
