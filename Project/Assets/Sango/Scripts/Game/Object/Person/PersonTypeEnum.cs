namespace Sango.Core
{
    /// <summary>
    /// 武将类型
    /// 用于标识武将的来源与用途，取值范围 0 - 7，Max 为枚举上限（非法值）
    /// </summary>
    public enum PersonTypeEnum : int
    {
        /// <summary>
        /// 历史
        /// </summary>
        PersonType_Historical = 0,

        /// <summary>
        /// 古代
        /// </summary>
        PersonType_Ancient = 1,

        /// <summary>
        /// 事件
        /// </summary>
        PersonType_Event = 2,

        /// <summary>
        /// 自定义
        /// </summary>
        PersonType_Custom = 3,

        /// <summary>
        /// 临时
        /// </summary>
        PersonType_Temporary = 4,

        /// <summary>
        /// 未知
        /// </summary>
        PersonType_Unknown = 5,

        /// <summary>
        /// NPC
        /// </summary>
        PersonType_NPC = 6,

        /// <summary>
        /// 生成
        /// </summary>
        PersonType_Auto = 7,

        /// <summary>
        /// 枚举上限
        /// </summary>
        PersonType_Max = 8,
    }
}
