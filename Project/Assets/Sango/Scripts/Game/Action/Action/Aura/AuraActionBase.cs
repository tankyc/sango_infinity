using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 光环类效果基类：以一个格子为中心，给半径内的部队提供常驻加成。
    ///
    /// 子类只需要提供"光环中心"和"归属势力"，范围判定、目标进出处理、叠加与回退都在本类完成。
    /// 目前有两个宿主：
    ///   TroopAura    部队光环：中心 = 该部队所在格子（部队移动时自动重算）
    ///   BuildingAura 建筑光环：中心 = 建筑中心格
    ///
    /// 参数：
    /// value      光环数值（可为负，负数即减益光环）
    /// bound      光环半径（格）
    /// auraType   作用属性：0 攻击力 / 1 防御力
    /// targetType 作用对象：0 己方 / 1 敌方 / 2 所有
    /// stackMode  叠加规则：0 多个光环取最大值 / 1 累加
    ///
    /// 接入事件：OnTroopEnterCell（目标部队进 / 出光环范围，以及部队光环的源部队自身移动）。
    /// 属性落在 troop.extraAttack / troop.extraDefence 上，不会被属性重算重置。
    /// </summary>
    public abstract class AuraActionBase : ActionBase
    {
        /// <summary>作用属性：攻击力</summary>
        public const int AURA_ATTACK = 0;
        /// <summary>作用属性：防御力</summary>
        public const int AURA_DEFENCE = 1;

        /// <summary>叠加规则：多个光环取最大值</summary>
        public const int STACK_MAX = 0;
        /// <summary>叠加规则：多个光环数值累加</summary>
        public const int STACK_SUM = 1;

        /// <summary>光环数值</summary>
        protected int value;
        /// <summary>光环半径（格）</summary>
        protected int bound;
        /// <summary>作用属性（0 攻击 / 1 防御）</summary>
        protected int auraType;
        /// <summary>作用对象（0 己方 / 1 敌方 / 2 所有）</summary>
        protected int targetType;
        /// <summary>叠加规则（0 取最大 / 1 累加）</summary>
        protected int stackMode;

        /// <summary>势力对象（技法装配时有值）</summary>
        protected Force Force { get; set; }

        /// <summary>光环当前覆盖的部队</summary>
        readonly List<Troop> targets = new List<Troop>();

        /// <summary>光环数值（供叠加计算读取）</summary>
        public int Value => value;
        /// <summary>作用属性（供叠加计算读取）</summary>
        public int AuraType => auraType;
        /// <summary>叠加规则（供叠加计算读取）</summary>
        public int StackMode => stackMode;

        /// <summary>
        /// 光环中心格；为空表示光环当前不可用
        /// </summary>
        protected abstract Cell CenterCell { get; }

        /// <summary>
        /// 光环归属势力；用于敌我判定
        /// </summary>
        protected abstract Force OwnerForce { get; }

        /// <summary>
        /// 光环是否有效（源部队存活 / 建筑未摧毁 且 光环半径配置合法）
        /// </summary>
        public bool IsActive => bound > 0 && CenterCell != null && OwnerForce != null;

        /// <summary>
        /// 初始化光环参数并订阅移动事件，然后把范围内已有部队补挂一次
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            value = p.Value<int>("value");
            bound = p.Value<int>("bound");
            auraType = p.Value<int>("auraType");
            targetType = p.Value<int>("targetType");
            stackMode = p.Value<int>("stackMode");

            // 子类先解析宿主，再调用本方法
            if (!IsActive)
                return;

            GameEvent.OnTroopEnterCell += OnTroopEnterCell;

            // 效果生成时范围内可能已经有部队驻留，先补挂一次
            CenterCell.Spiral(bound, (cell) =>
            {
                if (cell != null && cell.troop != null)
                    AddTarget(cell.troop);
            });
        }

        /// <summary>
        /// 光环是事件驱动的常驻效果（随位置变化自动增减），不需要外部再主动触发
        /// </summary>
        /// <param name="trigger">触发事件（未使用）</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 清理光环：退订事件，并把已经加上去的加成全部回退
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopEnterCell -= OnTroopEnterCell;

            for (int i = 0; i < targets.Count; i++)
            {
                Troop troop = targets[i];
                if (troop != null)
                    AuraManager.Remove(troop, this);
            }
            targets.Clear();
        }

        /// <summary>
        /// 部队移动回调：目标进入 / 离开光环范围；源部队自身移动时整体重算
        /// </summary>
        /// <param name="troop">移动的部队</param>
        /// <param name="dest">移动后的格子</param>
        /// <param name="last">移动前的格子</param>
        void OnTroopEnterCell(Troop troop, Cell dest, Cell last)
        {
            if (troop == null)
                return;

            Cell center = CenterCell;
            if (center == null)
                return;

            // 源部队自己移动：整个光环圈跟着平移，全部重新判定
            if (IsAuraOwner(troop))
            {
                RebuildAll(center);
                return;
            }

            bool inNow = dest != null && dest.Distance(center) <= bound;
            bool inBefore = last != null && last.Distance(center) <= bound;

            if (!inBefore && inNow)
                AddTarget(troop);
            else if (inBefore && !inNow)
                RemoveTarget(troop);
        }

        /// <summary>
        /// 该部队是否是光环的源（部队光环才有意义）
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>是否是光环源</returns>
        protected virtual bool IsAuraOwner(Troop troop)
        {
            return false;
        }

        /// <summary>
        /// 源部队移动后重算：先剔除已经不在范围内的，再扫描新位置覆盖到的部队
        /// </summary>
        /// <param name="center">光环中心格</param>
        void RebuildAll(Cell center)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                Troop troop = targets[i];
                if (troop == null || !troop.IsAlive || troop.cell == null || troop.cell.Distance(center) > bound)
                    RemoveTarget(troop);
            }

            center.Spiral(bound, (cell) =>
            {
                if (cell != null && cell.troop != null)
                    AddTarget(cell.troop);
            });
        }

        /// <summary>
        /// 目标进入范围
        /// </summary>
        /// <param name="troop">部队对象</param>
        void AddTarget(Troop troop)
        {
            if (troop == null || targets.Contains(troop))
                return;
            if (!InScope(troop))
                return;

            targets.Add(troop);
            AuraManager.Add(troop, this);
        }

        /// <summary>
        /// 目标离开范围
        /// </summary>
        /// <param name="troop">部队对象</param>
        void RemoveTarget(Troop troop)
        {
            if (troop == null)
                return;
            if (!targets.Remove(troop))
                return;

            AuraManager.Remove(troop, this);
        }

        /// <summary>
        /// 敌我判定：该部队是否属于本光环的作用对象
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>是否需要作用在该部队上</returns>
        protected bool InScope(Troop troop)
        {
            Force owner = OwnerForce;
            if (troop == null || owner == null || !troop.IsAlive)
                return false;

            if (targetType == 0)
                return owner == troop.BelongForce;
            if (targetType == 1)
                return owner.IsEnemy(troop.BelongForce);
            return true;
        }
    }
}
