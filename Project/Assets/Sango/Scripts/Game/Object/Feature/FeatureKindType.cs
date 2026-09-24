using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 特性
    /// </summary>
    public enum FeatureKindType : int
    {
        TroopMove = 1,
        TroopAttack,
        TroopDefence,
        TroopStrategy,
        TroopSupport,
        CityProduce,
        CityHarvest,
        CityDisaster,
        PersonRelationship
    }
}
