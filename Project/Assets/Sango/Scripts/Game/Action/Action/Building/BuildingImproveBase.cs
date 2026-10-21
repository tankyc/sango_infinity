using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑范围增益基类（子类只负责 OnEnter / OnLeave 具体加减什么属性）。
    ///
    /// 接入事件：OnTroopEnterCell（部队进入格子时判定是否进入 / 离开影响范围），
    /// 以及 Init 时对范围内已有部队做一次补挂。
    ///
    /// 参数：
    /// value      增益数值（具体含义由子类决定，一般是百分比点数）
    /// bound      生效范围（格）
    /// targetType 作用对象 0 己方 / 1 敌人 / 2 所有（旧配置键名拼写为 tartgetType，仍然兼容）
    ///
    /// 同名效果（improveKey 相同）在同一个部队上只保留一个"持有者"，取 value 最大者，
    /// 持有者离开或建筑被拆时会重新挑选场上剩余的最强者，避免"阵 + 城塞"重叠区里
    /// 城塞一毁、阵的加成凭空消失。
    /// </summary>
    public class BuildingImproveBase : BuildingActionBase
    {
        /// <summary>
        /// 提升值（具体含义由子类决定）
        /// </summary>
        protected int value;

        /// <summary>
        /// 生效范围（格）
        /// </summary>
        protected int bound;

        /// <summary>
        /// 作用对象范围：0 己方 / 1 敌人 / 2 所有
        /// </summary>
        protected int targetType;

        /// <summary>
        /// 效果唯一 key，同 key 的效果在同一部队上只保留最大值
        /// </summary>
        protected string improveKey;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            // 只安装在建筑上；势力（技法）装配时没有格子范围，直接不生效
            if (Building == null)
                return;

            value = p.Value<int>("value");
            bound = p.Value<int>("bound");
            targetType = ReadTargetType(p);

            GameEvent.OnTroopEnterCell += OnTroopEnterCell;

            // 效果生成时，范围内可能已经有部队驻留，先补挂一次
            Building.CenterCell.Spiral(bound, (cell) =>
            {
                if (cell.troop != null && InScope(cell.troop))
                    ApplyTo(cell.troop);
            });
        }

        /// <summary>
        /// 清理效果订阅，并把已经挂上的增益摘掉（若自己是持有者则回退给次强者）
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopEnterCell -= OnTroopEnterCell;

            // 【空引用修复】Init 在 Building == null 时会提前返回、不订阅任何东西，
            // 但 Clear 原先无条件访问 Building.CenterCell，势力绑定时会抛 NullReferenceException。
            if (Building == null || Building.CenterCell == null)
                return;

            Building.CenterCell.Spiral(bound, (cell) =>
            {
                if (cell.troop != null)
                    RemoveFrom(cell.troop);
            });
        }

        /// <summary>
        /// 部队进入格子时判定是否进入 / 离开影响范围
        /// </summary>
        /// <param name="troop">移动的部队</param>
        /// <param name="dest">移动后的格子</param>
        /// <param name="last">移动前的格子</param>
        void OnTroopEnterCell(Troop troop, Cell dest, Cell last)
        {
            if (troop == null || dest == null || Building == null || Building.CenterCell == null)
                return;

            if (!InScope(troop))
                return;

            int destDis = dest.Distance(Building.CenterCell);
            int lastDis = last == null ? 999 : last.Distance(Building.CenterCell);

            // 进入范围：与当前持有者比较，取最大值
            if (lastDis > bound && destDis <= bound)
            {
                ApplyTo(troop);
            }
            // 离开范围：只有自己是持有者时才摘除，并回退给场上剩下的最强者
            else if (lastDis <= bound && destDis > bound)
            {
                RemoveFrom(troop);
            }
        }

        /// <summary>
        /// 部队是否属于本效果的作用对象（敌我判定）
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>是否需要作用在该部队上</returns>
        protected bool InScope(Troop troop)
        {
            if (troop == null || Building == null)
                return false;
            if (targetType == 0 && !troop.IsSameForce(Building))
                return false;
            if (targetType == 1 && !troop.IsEnemy(Building))
                return false;
            return true;
        }

        /// <summary>
        /// 把本效果登记到部队上；已有更强的持有者时保持不变
        /// </summary>
        /// <param name="troop">部队对象</param>
        void ApplyTo(Troop troop)
        {
            if (troop == null || troop.buildingImproveMap == null)
                return;

            if (troop.buildingImproveMap.TryGetValue(improveKey, out BuildingImproveBase holder))
            {
                if (holder == this || holder.value >= value)
                    return;

                // 更强者顶替更弱者
                holder.OnLeave(troop);
                troop.buildingImproveMap[improveKey] = this;
                OnEnter(troop);
            }
            else
            {
                troop.buildingImproveMap.Add(improveKey, this);
                OnEnter(troop);
            }
        }

        /// <summary>
        /// 把本效果从部队上摘掉；若本效果正是持有者，则回退到场上剩余的最强同类效果
        /// </summary>
        /// <param name="troop">部队对象</param>
        void RemoveFrom(Troop troop)
        {
            if (troop == null || troop.buildingImproveMap == null)
                return;
            if (!troop.buildingImproveMap.TryGetValue(improveKey, out BuildingImproveBase holder))
                return;
            if (holder != this)
                return;

            troop.buildingImproveMap.Remove(improveKey);
            OnLeave(troop);
            ReapplyBestHolder(troop);
        }

        /// <summary>
        /// 回退：在场上重新找出覆盖该部队、且 value 最大的同类效果补挂。
        /// 用于"持有者建筑被拆 / 持有者过期"的场景，避免增益凭空消失。
        /// </summary>
        /// <param name="troop">部队对象</param>
        protected void ReapplyBestHolder(Troop troop)
        {
            if (troop == null || troop.cell == null)
                return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.buildingSet == null)
                return;

            BuildingImproveBase best = null;
            for (int i = 0; i < scenario.buildingSet.Count; i++)
            {
                // 已摧毁的建筑（IsAlive=false）不再提供增益；Clear 阶段摧毁的建筑此时还在集合里
                if (!(scenario.buildingSet[i] is Building building) || !building.IsAlive)
                    continue;
                if (building.CenterCell == null || building.actionList == null)
                    continue;

                for (int j = 0; j < building.actionList.Count; j++)
                {
                    if (!(building.actionList[j] is BuildingImproveBase improve))
                        continue;
                    if (improve == this || improve.improveKey != improveKey)
                        continue;
                    if (!improve.InScope(troop))
                        continue;
                    if (building.CenterCell.Distance(troop.cell) > improve.bound)
                        continue;

                    if (best == null || improve.value > best.value)
                        best = improve;
                }
            }

            if (best != null)
            {
                troop.buildingImproveMap[improveKey] = best;
                best.OnEnter(troop);
            }
        }

        /// <summary>
        /// 效果生效时给部队加减属性
        /// </summary>
        /// <param name="troop">部队对象</param>
        protected virtual void OnEnter(Troop troop)
        {

        }

        /// <summary>
        /// 效果失效时把属性还原
        /// </summary>
        /// <param name="troop">部队对象</param>
        protected virtual void OnLeave(Troop troop)
        {

        }
    }
}
