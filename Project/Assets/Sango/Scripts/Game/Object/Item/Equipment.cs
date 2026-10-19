namespace Sango.Core
{
    /// <summary>
    /// 装备 / 宝物的运行期语义类型（继承 <see cref="ItemType"/>）。
    ///
    /// 【为什么是空类】
    ///   道具库由 <c>SangoObjectMap&lt;ItemType&gt;</c> 承载，反序列化走
    ///   <c>SangoObjectMapConverter&lt;T&gt;</c> 里的 <c>serializer.Deserialize&lt;T&gt;(reader)</c>，
    ///   其中 T 恒为 ItemType —— 运行期**所有**道具（含宝物与装备）都是 ItemType 实例，
    ///   本类因此拿不到任何"只有子类才有"的字段。
    ///
    ///   所以珍品的全部数据字段（品级 grade、发现权重 discoverProbability、五维加成、
    ///   特技 features、特效 actionEntities）都定义在 <see cref="ItemType"/> 上，
    ///   涉及的判定请用 <see cref="ItemType.IsTreasure"/> / <see cref="ItemType.IsEquipment"/>
    ///   / <see cref="ItemType.IsRareItem"/>。
    ///
    ///   本类暂留作语义标记与后续扩展位（例如日后要给装备加"耐久"这类独立字段时再启用）。
    /// </summary>
    public class Equipment : ItemType
    {
    }
}
