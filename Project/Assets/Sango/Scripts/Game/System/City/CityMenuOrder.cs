using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城市菜单（左键点城弹出的命令菜单）按钮排序值的**登记表**。
    ///
    /// 设计意图：
    ///   · 这里**不预先写死任何数值**——排序值的唯一来源是各指令系统自己的
    ///     customMenuOrder（或直接写在 menuData.Add 里的值）。
    ///   · 框架在注册菜单（menuData.Add）时把"按钮路径 → 排序值"登记进来，
    ///     因此本表记录的就是**框架当前实际使用的菜单排序值**。
    ///   · MOD 通过本表按路径查询该值，据此决定自己的按钮该插在哪个位置，
    ///     不必去抄框架代码里的魔法数字。
    ///
    /// 排序约定（与 ContextMenuData / ContextMenu 一致）：
    ///   · 值越小越靠前；同一父级下的按钮按各自的值排序；
    ///   · 父级菜单取子级的最小值参与排序；
    ///   · 框架目前大体以 100 为步进，MOD 插入时取中间值即可（如 150、1150）。
    ///
    /// MOD 用法示例：
    ///   int order = CityMenuOrder.Get("都市/开发");      // 取框架已有值
    ///   menuData.Add("都市/我的按钮", order + 50, ...);  // 插在它后面
    /// </summary>
    public static class CityMenuOrder
    {
        /// <summary>框架当前使用的步进（仅作提示，MOD 插值时取中间值即可）</summary>
        public const int Step = 100;

        /// <summary>查询不到时返回的值（表示"框架没有登记过这个路径"）</summary>
        public const int Unknown = int.MinValue;

        /// <summary>
        /// "自动委任 / 解除委任"按钮的登记键。
        /// 该按钮的标题是本地化字符串（GameLanguage 10000003 / 10000004），没法当路径用，
        /// 所以固定用这个键登记 / 查询。
        /// </summary>
        public const string AutoAppointWorkingKey = "城市/自动委任";

        static readonly Dictionary<string, int> s_orders = new Dictionary<string, int>();

        /// <summary>
        /// 登记一个按钮的排序值（框架在 menuData.Add 时调用）。
        /// 同一路径**只记第一次**，保证记下的是框架的原始值，后续再登记不会把它改掉。
        /// </summary>
        public static void Record(string path, int order)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (s_orders.ContainsKey(path))
                return;

            s_orders.Add(path, order);
        }

        /// <summary>
        /// 登记并把按钮加进菜单（等价于 Record + menuData.Add）。
        /// 框架的城市菜单注册统一走这里，避免"加了菜单却忘了登记"。
        /// </summary>
        public static void Add(IContextMenuData menuData, string title, int order, object custom,
            Action<IContextMenuItem> action, bool valide = true)
        {
            Record(title, order);
            menuData.Add(title, order, custom, action, valide);
        }

        /// <summary>是否登记过该路径</summary>
        public static bool Contains(string path)
        {
            return !string.IsNullOrEmpty(path) && s_orders.ContainsKey(path);
        }

        /// <summary>按按钮路径取框架排序值；未登记返回 <see cref="Unknown"/>。</summary>
        public static int Get(string path)
        {
            return TryGet(path, out int order) ? order : Unknown;
        }

        /// <summary>按按钮路径取框架排序值；未登记返回 false 并给出 0。</summary>
        public static bool TryGet(string path, out int order)
        {
            order = 0;
            if (string.IsNullOrEmpty(path))
                return false;
            return s_orders.TryGetValue(path, out order);
        }

        /// <summary>已登记的全部"路径 → 排序值"（MOD 可枚举，用于批量调整）</summary>
        public static IReadOnlyDictionary<string, int> All => s_orders;

        /// <summary>已登记的全部路径</summary>
        public static IEnumerable<string> AllPaths => s_orders.Keys;

        /// <summary>清空登记（重开剧本 / 测试用；正常游戏流程不需要）</summary>
        public static void Clear()
        {
            s_orders.Clear();
        }
    }
}
