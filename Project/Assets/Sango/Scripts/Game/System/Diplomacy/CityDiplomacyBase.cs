using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 外交类城池命令的公共基类。
    ///
    /// 【为什么抽这一层】`window_city_diplomacy_alliance` 这个 prefab 的界面元素
    /// （标题 / 目标势力 / 使者 / 关系 / 距离 / 金额条 / 确认）对所有"选一个势力 + 一个使者 + 可能带钱"
    /// 的外交命令都是通用的 —— 结盟、劝降、俘虏交换、请求援军只是标题和金额条可见性不同。
    ///
    /// 复用的前提是 UI 能拿到"当前正在操作哪个命令"：`GameSystem.GetSystem<T>()` 是按具体类型取的，
    /// 复用同一个窗口时它只能取到 `CityDiplomacyAlliance`，取不到实际命令。
    /// 因此这里加一个静态 <see cref="Current"/>，由各命令在 `OnEnter` 里通过 <see cref="Activate"/> 写入。
    ///
    /// 【不复用的部分】停战（`CityDiplomacyTruce`）与宣战（`CityDiplomacyDeclareWar`）走的是
    /// `counsellorRecommendList` 的军师推荐流程，结构不同，继续用各自窗口。
    /// </summary>
    public abstract class CityDiplomacyBase : CityBaseSystem
    {
        /// <summary>候选目标势力</summary>
        public List<Force> targetForces = new List<Force>();

        /// <summary>目标势力列表的排序标题（各命令可覆盖）</summary>
        public List<ObjectSortTitle> customBelongForceTitleList;

        /// <summary>本次外交投入的资金</summary>
        public int gold;

        /// <summary>当前活跃的外交命令（供复用的窗口读取）</summary>
        public static CityDiplomacyBase Current { get; protected set; }

        /// <summary>
        /// 金额条是否需要显示。劝降不花钱（原版 `can_annex_command` 没有金的要求），覆盖为 false。
        /// </summary>
        public virtual bool NeedGold => true;

        /// <summary>在 OnEnter 里调用，登记为当前命令</summary>
        protected void Activate()
        {
            Current = this;
        }

        /// <summary>在 OnDestroy 里调用，避免窗口复用后读到已结束的命令</summary>
        protected void Deactivate()
        {
            if (Current == this)
                Current = null;
        }
    }
}
