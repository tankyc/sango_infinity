namespace Sango.Core
{
    /// <summary>
    /// 城市**状态标记位**（位域，存在 City.stateFlags）。
    ///
    /// 【为什么需要】城市的某些状态是"由在册武将 / 建筑 / 势力科技派生"的，
    /// 但判定点在**很热的路径**上（例如换季掉忠是对全势力武将逐个询问），
    /// 每次都遍历武将列表找特技代价太高（此前的写法是全量扫 personSet，整体 O(N²)，
    /// 1500 名武将时单季要 2.4 秒）。这里把"状态是否成立"折叠成一个整数位域，
    /// 由城市在武将增删 / 回合结束时重算一次，业务侧只读一个 bit。
    ///
    /// 【设计参考】部队侧 <see cref="TroopStateFlag"/> 与 Troop.pathFlags 的同一套做法；
    /// 原版 Open311 的 <c>Unit::state_</c> 也是一个整数位域（s11_enum_scen.h:926-938），
    /// 不是逐帧遍历状态表。
    ///
    /// 【不序列化】内容完全由运行期的武将 / 建筑 / Action 决定，
    /// 存进存档只会在"读档后数据已变但标记还在"的情况下产生不一致。
    /// 读档后由 <see cref="City.RefreshStateFlags"/> 重算一次。
    /// </summary>
    [System.Flags]
    public enum CityStateFlag : int
    {
        /// <summary>无状态</summary>
        None = 0,

        /// <summary>
        /// 仁政（特技 Id 97）：本城所属武将换季结算时不会掉忠。
        /// 口径与原版一致 —— "所属相同的武将"包含带兵出征者（<see cref="City.allPersons"/> 收录的是所属关系，
        /// 不是"当前站在城里"），因此不能只看 <see cref="City.freePersons"/>。
        /// </summary>
        RenZheng = 1 << 0,
    }

    /// <summary>
    /// 与 <see cref="CityStateFlag"/> 相关的特技 Id（Features.json）。
    ///
    /// 【为什么单独放这里】标记位的重算（<see cref="City.RefreshStateFlags"/>）与
    /// 判定（<see cref="PersonLoyaltyRules"/>）都要用它，集中一处避免两边写死 97。
    /// </summary>
    public static class CityStateFeatureId
    {
        /// <summary>特技「仁政」</summary>
        public const int RenZheng = 97;
    }
}
