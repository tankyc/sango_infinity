using System.Collections.Generic;
using Sango.UI;

namespace Sango.Core
{
    /// <summary>
    /// 外交系统
    /// </summary>
    [GameSystem]
    public class DiplomacySystem : GameSystem
    {
        /// <summary>
        /// 目标势力
        /// </summary>
        public Force TargetForce { get; private set; }

        /// <summary>
        /// 所有势力列表
        /// </summary>
        public List<Force> AllForces { get; private set; }

        /// <summary>
        /// 窗口名称
        /// </summary>
        private const string windowName = "UIDiplomacy";

        /// <summary>
        /// 初始化外交系统
        /// </summary>
        /// <param name="targetForce">目标势力</param>
        /// <param name="allForces">所有势力列表</param>
        public void Start(Force targetForce, List<Force> allForces)
        {
            TargetForce = targetForce;
            AllForces = allForces;

            // 显示外交界面
            UIDiplomacy uiDiplomacy = Window.Instance.Open<UIDiplomacy>(windowName);
            uiDiplomacy.OnOpen(this);
        }

        /// <summary>
        /// 执行外交行动（重载，用于赎回俘虏）
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="receiver">接收方势力</param>
        /// <param name="diplomat">执行外交的武将</param>
        /// <param name="param">行动参数</param>
        /// <param name="captiveId">俘虏ID</param>
        /// <returns>行动是否成功</returns>
        public bool PerformDiplomacyAction(DiplomacyActionType actionType, Force receiver, Person diplomat = null, object param = null, int captiveId = 0)
        {
            // 获取当前势力
            Force sender = Scenario.Cur.CurRunForce;
            if (sender == null)
                return false;

            // 执行外交行动
            return GameSystem.GetSystem<DiplomacyManager>().PerformDiplomacyAction(actionType, sender, receiver, diplomat, param, captiveId);
        }

        /// <summary>
        /// 获取势力间的关系
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <returns>关系值</returns>
        public int GetRelation(Force forceA, Force forceB)
        {
            return GameSystem.GetSystem<DiplomacyManager>().GetRelation(forceA, forceB);
        }

        /// <summary>
        /// 检查是否可以执行外交行动
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        /// <param name="sender">发送方势力</param>
        /// <param name="receiver">接收方势力</param>
        /// <returns>是否可以执行</returns>
        public bool CanPerformDiplomacyAction(DiplomacyActionType actionType, Force sender, Force receiver)
        {
            return GameSystem.GetSystem<DiplomacyManager>().CanPerformDiplomacyAction(actionType, sender, receiver);
        }

        /// <summary>
        /// 获取势力的可用武将列表
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>可用武将列表</returns>
        public List<Person> GetAvailableDiplomats(Force force)
        {
            List<Person> diplomats = new List<Person>();
            force.ForEachPerson(person =>
            {
                if (person.IsFree && person.IsAlive)
                {
                    diplomats.Add(person);
                }
            });
            return diplomats;
        }

        /// <summary>
        /// 退出外交系统。
        ///
        /// 【为什么用 `new` 而不是 `override`】基类 `GameSystem.Exit()` 的语义是
        /// **退一层**（`GameSystemManager.Back(this)`，回到上一个命令，系统还在栈里）；
        /// 而"关闭外交"要的是**清空整个系统栈**（`Done()`，所有命令 `OnDone` 后回到无命令状态）。
        /// 两者语义不同 —— 若改成 `override` 并在内部调 `base.Exit()`，外交窗口会关掉，
        /// 但系统仍滞留在栈里，后续 `Push` 时会把已关闭的窗口当成当前命令。
        ///
        /// 这里显式用 `new` 声明"有意遮蔽"，消除 CS0114 警告。
        ///
        /// 【注意】`GameSystemManager` 不调用 `Exit()`（它只调 `OnExit` / `OnBack` /
        /// `OnDestroy` / `OnDone`），所以遮蔽不会导致"系统永不清理"。
        /// 但如果通过 `GameSystem` 基类型引用调用 `Exit()`，会走基类的 Back 语义 ——
        /// 因此调用方必须用 `DiplomacySystem` 静态类型（现只有 `UIDiplomacy.OnCancelButton` 一处）。
        /// </summary>
        public new void Exit()
        {
            Window.Instance.Close(windowName);
            Done();
        }
    }
}