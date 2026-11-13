using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 特性生效场景（决定特技在部队 / 城池 / 个人哪一层被装配成 Action）。
    ///
    /// 额外说明 <see cref="Mixed"/>：
    ///   混合组本身**不产生任何效果**，只由 Feature.childList 把若干子特技打包成一个特技条目
    ///   （典型用途：一条配置里同时挂"部队类 + 内政类"子特技，或让装备一次性附加多个特技）。
    ///   三个检测点（部队 / 城池 / 个人）都必须按**子特技各自的 kind** 递归判定是否生效，
    ///   见 <see cref="Feature.CollectEffective"/>。
    /// </summary>
    public enum FeatureKindType : int
    {
        /// <summary>混合组：自身无效果，按子特技的 kind 逐个判定（见 <see cref="Feature.childList"/>）</summary>
        Mixed = 0,
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
