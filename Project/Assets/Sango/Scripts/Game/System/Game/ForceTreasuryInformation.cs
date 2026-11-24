/*
 * 文件名：ForceTreasuryInformation.cs
 * 描述：国库预览 —— 用道具选择器列出势力国库里的宝物 / 装备（名字、品级、数量、能力加成、说明）
 * 创建日期：2026-11-24
 * 作者：XBJ
 */

using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 国库预览。
    ///
    /// 【功能】把指定势力国库（<see cref="Force.Treasury"/>）里的宝物 / 装备列成一张表：
    /// 名字（按品级上色）、品级（★）、数量、能力加成（五维）、说明。
    ///
    /// 【展示形态】复用工程现成的道具选择器 <see cref="ItemTypeSelectSystem"/>
    /// （打开 `window_object_selector`）—— 与兵装 / 宝物选择是同一套列表控件，
    /// 自带表头排序、搜索框与横向滚动，不需要另造一个窗口。
    ///
    /// 【只读，不是选择】传 <c>limit = 0</c> + <c>browseOnly = true</c>
    /// （见 <see cref="ObjectsDisplaySystem.BrowseMode"/>）：
    /// 界面不显示勾选列、「确定」与底部功能按钮（一并 / 一并解除），
    /// 点行没有任何反应，唯一出口是「返回」（取消按钮 / 右键 / ESC）。
    /// 国库预览只是"看"，不该产生选择结果。
    ///
    /// 【数据来源】<see cref="Force.GetTreasuryItems"/> 是国库的唯一出口
    /// （内部按 Id 升序，保证列表顺序稳定）；数量按 <see cref="Force.GetTreasuryCount"/> 现算，
    /// 不缓存在道具本身 —— 同一件珍品在不同势力的国库里持有量不同。
    ///
    /// 【入口】情报菜单 →「国库」，视角势力为当前玩家势力（<see cref="Scenario.CurRunForce"/>）。
    /// 其它界面要复用，直接调静态 <see cref="Open"/>。
    /// </summary>
    [GameSystem]
    public class ForceTreasuryInformation : GameSystem
    {
        /// <summary>选择器标题前缀（后面接势力名）</summary>
        private const string TitlePrefix = "国库";

        /// <summary>情报菜单里的菜单项排序值（与全势力 400 相邻，排在势力情报之后）</summary>
        private const int MenuOrder = 450;

        public override void Init()
        {
            Name = "国库";
            //GameEvent.OnGameInformationContextMenuShow += OnGameInformationContextMenuShow;
        }

        public override void Clear()
        {
            //GameEvent.OnGameInformationContextMenuShow -= OnGameInformationContextMenuShow;
        }

        /// <summary>
        /// 情报菜单项：国库为空时置灰并给出原因（与"全武将 / 全势力"同一套处理）。
        /// </summary>
        /// <param name="menuData">情报菜单数据</param>
        void OnGameInformationContextMenuShow(IContextMenuData menuData)
        {
            Force force = Scenario.Cur != null ? Scenario.Cur.CurRunForce : null;
            int count = BuildItemList(force).Count;
            bool has = count > 0;
            menuData.Add("国库", MenuOrder, null, OnClickMenuItem, has,
                has ? null : "国库里还没有宝物或装备");
        }

        /// <summary>点菜单项：以当前玩家势力为视角打开预览</summary>
        /// <param name="contextMenuItem">菜单项</param>
        void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            Open(Scenario.Cur != null ? Scenario.Cur.CurRunForce : null);
        }

        /// <summary>
        /// 打开国库预览：以 <paramref name="force"/> 的国库为数据源。
        /// </summary>
        /// <param name="force">视角势力；为空时忽略本次请求</param>
        public static void Open(Force force)
        {
            if (force == null)
            {
                // 缺参数属于调用方错误，但界面按钮可能在空势力时被点到，只告警不抛
                Sango.Log.Warning("[国库] 没有可预览的势力，已忽略本次打开请求");
                return;
            }

            ItemTypeSelectSystem selectSystem = GameSystem.GetSystem<ItemTypeSelectSystem>();
            if (selectSystem == null)
            {
                // 选择系统没注册上来属于工程级异常，必须报出来而不是让玩家以为"点了没反应"
                Sango.Log.Error("[国库] ItemTypeSelectSystem 未注册，无法打开道具列表");
                return;
            }

            List<ItemType> items = BuildItemList(force);
            // Force.Name 取的是君主名（Force.cs 的 Name 重写），君主未解析时为空，这里兜底成势力 Id
            string forceName = string.IsNullOrEmpty(force.Name) ? $"势力{force.Id}" : force.Name;
            Sango.Log.Info($"[国库]{forceName} 打开国库预览：{items.Count} 件珍品（上限 {force.TreasuryLimit}，0=不限）");

            selectSystem.Start(
                items,
                new List<ItemType>(),   // 没有预选项：这是浏览，不是选择
                0,                      // limit = 0 → ClickMode 为 false，不会点一行就退出
                null,                   // 只读：不产生选择结果
                BuildSortTitles(force),
                $"{TitlePrefix}（{forceName}）",
                -1,                     // 不按列排序：保持 GetTreasuryItems 的 Id 顺序
                true,                   // browseOnly = true → 无勾选列、无确定按钮，只能返回
                true);                  // hideSortGroups = true → 列固定，不显示一排排序组表卡
        }

        /// <summary>
        /// 收集国库里的珍品（宝物 / 装备）。
        /// </summary>
        /// <param name="force">势力，可为 null</param>
        /// <returns>珍品列表（按 Id 升序）；势力为空或国库为空时返回空列表</returns>
        public static List<ItemType> BuildItemList(Force force)
        {
            List<ItemType> items = new List<ItemType>();
            if (force == null)
                return items;

            force.GetTreasuryItems(items);
            return items;
        }

        /// <summary>
        /// 列定义：道具 / 品级 / 数量 / 能力加成 / 说明。
        ///
        /// 第 0 列必须是名字 —— 列表控件把第 0 列当行名渲染（见 UIObjectDisplay 的骨架实现）。
        /// 「数量」列用工厂创建（闭包固定国库），其余列都是道具自身的属性，直接复用静态定义。
        /// </summary>
        /// <param name="force">视角势力（取它的国库算数量），可为 null</param>
        /// <returns>列定义列表</returns>
        public static List<ObjectSortTitle> BuildSortTitles(Force force)
        {
            ItemStore store = force != null ? force.Treasury : null;
            return new List<ObjectSortTitle>()
            {
                ItemTypeSortFunction.SortByColorName,
                ItemTypeSortFunction.SortByGradeStars,
                ItemTypeSortFunction.GetSortByCount(store),
                ItemTypeSortFunction.SortByBonus,
                ItemTypeSortFunction.SortByDesc,
            };
        }
    }
}
