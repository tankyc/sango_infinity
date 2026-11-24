/*
 * 文件名：CityTreasury.cs
 * 描述：都市菜单「势力 / 国库」—— 打开本势力国库预览（宝物 / 装备一览）
 * 创建日期：2026-11-24
 * 作者：XBJ
 */

using Sango.UI;

namespace Sango.Core.Player
{
    /// <summary>
    /// 都市菜单「势力 / 国库」。
    ///
    /// 【它不是一次"都市工作"】国库预览纯粹是"看一眼"：
    ///   · 不消耗行动力、不选武将、不打开自己的窗口；
    ///   · 因此本类**不压入命令栈**（<see cref="CityBaseSystem.OnClickMenuItem"/> 的默认实现会 Push 自己），
    ///     点击后直接把道具选择器压栈。
    ///   · 选择器关闭后回到 <see cref="CitySystem"/>，由它的 OnBack 重新显示城市菜单 ——
    ///     与其它都市工作结束后回到菜单是同一套流程，玩家不会"退出到地图"。
    ///
    /// 【为什么挂在城市菜单而不是全局菜单】城市菜单是玩家最常打开的面板，
    /// 而国库是势力级数据：只要是自己势力的城就能看（<see cref="CityBaseSystem.OnCityContextMenuShow"/>
    /// 已经限定"自家城市"）。全局的「情报 → 国库」入口仍然保留，两处共用同一个
    /// <see cref="ForceTreasuryInformation.Open"/>。
    /// </summary>
    [GameSystem]
    public class CityTreasury : CityBaseSystem
    {
        public CityTreasury()
        {
            // 与「势力 / 能力研究」(3980)、「势力 / 培育武将」(3990) 同属"势力"分组，排在它们之后
            customMenuName = "势力/国库";
            customMenuOrder = 4000;
            // 本命令不开窗口，避免 CityBaseSystem.OnEnter/OnDestroy 去开关一个不存在的窗口
            windowName = null;
        }

        /// <summary>
        /// 国库里有珍品才可点：空国库点开只会得到一张空表，不如直接置灰并说明原因。
        /// </summary>
        public override bool IsValid
        {
            get
            {
                Force force = TargetCity != null ? TargetCity.BelongForce : null;
                return force != null;
            }
        }

        /// <summary>置灰原因（只在 <see cref="IsValid"/> 为 false 时被菜单读取）</summary>
        /// <param name="city">目标据点</param>
        /// <returns>原因文案</returns>
        public override string GetInvalidReason(City city)
        {
            return "国库里还没有宝物或装备";
        }

        /// <summary>
        /// 点击菜单项：直接打开国库预览，不压入本命令（见类注释）。
        /// </summary>
        /// <param name="contextMenuItem">被点击的菜单项，CustomData 是目标城市</param>
        protected override void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            TargetCity = contextMenuItem.CustomData as City;
            Force force = TargetCity != null ? TargetCity.BelongForce : null;
            ForceTreasuryInformation.Open(force);
        }
    }
}
