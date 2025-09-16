using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Condition
{
    public class ConditionAnd : Condition
    {
        public Condition l;
        public Condition r;
    }
}