namespace Sango.Core
{
    /// <summary>
    /// 反击来源类型。
    /// </summary>
    public enum AttackBackSource
    {
        /// <summary>非反击</summary>
        None = 0,
        /// <summary>部队反击（普攻 / 战法 / 远程）</summary>
        Troop = 1,
        /// <summary>据点（城市 / 关 / 港）反击</summary>
        Building = 2,
    }

    /// <summary>
    /// 反击信息：把"是否反击"与"反击强度因子"从同一个 int 里拆开。
    ///
    /// <para>
    /// 【为什么要有这个结构体】历史上 <c>Troop.ChangeTroops</c> 的第三个参数是一个裸 <c>int</c>，
    /// 同时承担"是否反击"和"强度因子"两种语义，导致订阅方写出 <c>atkBack == 1</c>
    /// 这种永远不成立的判断（部队反击实际传 28~90、据点反击传 20~50）。
    /// 现在语义拆开：是否反击看 <see cref="IsAttackBack"/>，强度看 <see cref="Factor"/>，
    /// 来源看 <see cref="Source"/>。
    /// </para>
    /// <para>
    /// 【兼容】保留 <c>int</c> 的隐式转换，调用点仍可写 <c>ChangeTroops(-dmg, atk, 0)</c>；
    /// 但事件与订阅方一律使用本结构体，编译期即可阻止 <c>atkBack == 1</c> 这类写法。
    /// </para>
    /// <para>
    /// 【值域】部队反击：普攻 90 / 战法 40，再乘包围惩罚，实际 28~90；
    /// 据点反击：<c>BuildingType.atkBack</c>，20~50；其余（火焰 / 毒 / 陷阱 / 补给）传 0。
    /// </para>
    /// </summary>
    public readonly struct AttackBackInfo
    {
        /// <summary>非反击（默认值，等价于 <c>default(AttackBackInfo)</c>）</summary>
        public static readonly AttackBackInfo None = default;

        /// <summary>反击强度因子：0 = 非反击；部队反击 28~90、据点反击 20~50</summary>
        public readonly int Factor;

        /// <summary>是否属于反击（等价于 <c>Factor != 0</c>）</summary>
        public readonly bool IsAttackBack;

        /// <summary>反击来源（非反击时为 <see cref="AttackBackSource.None"/>）</summary>
        public readonly AttackBackSource Source;

        /// <summary>构造反击信息</summary>
        /// <param name="factor">反击强度因子，0 = 非反击</param>
        /// <param name="source">反击来源</param>
        public AttackBackInfo(int factor, AttackBackSource source)
        {
            Factor = factor;
            IsAttackBack = factor != 0;
            Source = factor != 0 ? source : AttackBackSource.None;
        }

        /// <summary>构造"部队反击"</summary>
        /// <param name="factor">反击强度因子（<c>GetAttackBackFactor</c> 的返回值）</param>
        public static AttackBackInfo TroopAttackBack(int factor)
        {
            return new AttackBackInfo(factor, AttackBackSource.Troop);
        }

        /// <summary>构造"据点反击"</summary>
        /// <param name="factor">据点反击强度（<c>BuildingType.atkBack</c>）</param>
        public static AttackBackInfo BuildingAttackBack(int factor)
        {
            return new AttackBackInfo(factor, AttackBackSource.Building);
        }

        /// <summary>
        /// 旧 int 参数的兼容转换：非 0 一律视为部队反击。
        /// 仅供尚未迁移的调用点使用，新代码请显式调用
        /// <see cref="TroopAttackBack"/> / <see cref="BuildingAttackBack"/>。
        /// </summary>
        /// <param name="factor">反击强度因子</param>
        public static implicit operator AttackBackInfo(int factor)
        {
            return new AttackBackInfo(factor, AttackBackSource.Troop);
        }

        /// <summary>调试用文本（日志中文）</summary>
        public override string ToString()
        {
            return IsAttackBack ? $"反击(来源={Source},因子={Factor})" : "非反击";
        }
    }
}
