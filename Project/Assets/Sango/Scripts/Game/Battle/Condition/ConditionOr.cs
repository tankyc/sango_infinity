using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Condition
{
    public class ConditionOr : Condition
    {
        public Condition l;
        public Condition r;
    }
}