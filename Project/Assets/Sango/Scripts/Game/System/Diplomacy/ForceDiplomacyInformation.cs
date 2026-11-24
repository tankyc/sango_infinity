/*
 * 文件名：ForceDiplomacyInformation.cs
 * 描述：外交情报 —— 以指定势力为视角，用势力选择器列出其它势力与它的外交关系
 * 创建日期：2026-11-24
 * 作者：XBJ
 */

using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 外交情报。
    ///
    /// 【功能】以传入的目标势力为视角，列出**其它存活势力**（排除异民族 / 贼）与它的外交关系：
    /// 友好度档位、盟约类型（同盟 / 停战）、盟约剩余回合。
    ///
    /// 【候选集口径】严格对齐 Open311 `PersonOrderManager::get_normal_force_list`
    /// （`s11_sys_person_order_diplomacy.cpp:16-25`）：
    /// <code>
    /// for (Force* force : utils::ptr_view(system_->get_force_pool()))
    /// {
    ///     if (force->is_alive() and force->is_normal() and force != excluding)
    ///         v.push_back(force);
    /// }
    /// </code>
    /// 即"存活 + 普通势力 + 不是自己"。`is_normal()` 同时排除异民族与贼，
    /// 本项目对应 <see cref="Force.IsAlive"/> / <see cref="Force.IsNormal"/>（后者已按
    /// `Force::is_normal()` 实现：<c>!IsBarbarian &amp;&amp; !IsBandit</c>）。
    ///
    /// 【展示形态】复用工程现成的势力选择器 <see cref="ForceSelectSystem"/>
    /// （打开 `window_object_selector`）—— 它与外交选目标势力、军团任命、势力控制权交接
    /// 是同一套列表控件，自带表头排序、搜索框与横向滚动，不需要另造一个情报窗口。
    ///
    /// 【只读，不是选择】这里传 <c>limit = 0</c> + <c>browseOnly = true</c>（见
    /// <see cref="ObjectsDisplaySystem.BrowseMode"/>）：
    /// 界面不显示勾选列（每行的勾选圆点与表头的全选按钮）、不显示「确定」与底部功能按钮，
    /// 点行没有任何反应，唯一出口是「返回」（取消按钮 / 右键 / ESC）。
    /// 外交情报只是"看"，不该产生选择结果，也不该被误当成一次确认操作。
    /// </summary>
    public static class ForceDiplomacyInformation
    {
        /// <summary>选择器标题前缀（后面接视角势力名）</summary>
        private const string TitlePrefix = "外交情报";

        /// <summary>
        /// 打开外交情报：以 <paramref name="target"/> 为视角，列出其它存活势力与它的外交关系。
        /// </summary>
        /// <param name="target">视角势力（通常来自势力情报窗口当前查看的势力）</param>
        public static void Open(Force target)
        {
            if (target == null)
            {
                // 缺参数属于调用方错误，但界面按钮可能在手滑/空势力时被点到，只告警不抛
                Sango.Log.Warning("外交情报：未传入目标势力，已忽略本次打开请求");
                return;
            }

            ForceSelectSystem selectSystem = GameSystem.GetSystem<ForceSelectSystem>();
            if (selectSystem == null)
            {
                // 选择系统没注册上来属于工程级异常，必须报出来而不是让玩家以为"点了没反应"
                Sango.Log.Error("外交情报：ForceSelectSystem 未注册，无法打开势力列表");
                return;
            }

            List<Force> candidates = BuildCandidateList(target);
            Sango.Log.Info($"@外交@打开外交情报：视角势力 {target.Name}，候选势力 {candidates.Count} 个");

            selectSystem.Start(
                candidates,
                new List<Force>(),      // 没有预选项：这是浏览，不是选择
                0,                      // limit = 0 → ClickMode 为 false，不会点一行就退出
                null,                   // 只读：不产生选择结果
                BuildSortTitles(target),
                $"{TitlePrefix}（{target.Name}）",
                true);                  // browseOnly = true → 无勾选列、无确定按钮，只能返回
        }

        /// <summary>
        /// 构建候选势力列表：存活 + 普通势力（非异民族非贼）+ 不是视角势力本身。
        ///
        /// 顺序沿用剧本 forceSet 的原始顺序（不加排序），与 Open311 `get_normal_force_list`
        /// 按对象池顺序收集一致；具体先后由表头点击排序决定，打开时保持稳定。
        /// </summary>
        /// <param name="target">视角势力</param>
        /// <returns>候选势力列表（可能为空，界面会照常打开并显示空列表）</returns>
        public static List<Force> BuildCandidateList(Force target)
        {
            List<Force> candidates = new List<Force>();

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.forceSet == null)
                return candidates;

            scenario.forceSet.ForEach(force =>
            {
                if (force == null || force == target)
                    return;
                if (!force.IsAlive)
                    return;
                // 异民族 / 贼不参与外交，原版 is_normal() 一并排除
                if (!force.IsNormal)
                    return;
                candidates.Add(force);
            });

            return candidates;
        }

        /// <summary>
        /// 构建列定义：势力 / 主公 / 关系 / 盟约 / 剩余回合。
        ///
        /// 后三列都以 target 为视角，因此每次打开都新建一份（闭包固定 target），
        /// 不能做成静态字段，见 <see cref="ForceSortFunction.GetSortByLike"/> 的说明。
        /// </summary>
        /// <param name="target">视角势力</param>
        /// <returns>列定义列表（第 0 列必须是势力名，列表控件把它当行名渲染）</returns>
        public static List<ObjectSortTitle> BuildSortTitles(Force target)
        {
            return new List<ObjectSortTitle>()
            {
                ForceSortFunction.SortByName,
                ForceSortFunction.SortByLeader,
                ForceSortFunction.GetSortByLike(target),
                ForceSortFunction.GetSortByAgreement(target),
                ForceSortFunction.GetSortByAgreementLeftCount(target),
            };
        }
    }
}
