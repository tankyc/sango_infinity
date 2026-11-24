using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力情报。
    ///
    /// 左侧势力列表的候选集统一走 <see cref="GetAliveNormalForces"/>：
    /// **存活 + 普通势力**，排除异民族与贼、以及已灭亡势力（判断见 <see cref="Force.IsNormal"/>）。
    /// 技巧表 / 能力表两个子窗口复用同一个方法，保证三处列表一致。
    /// </summary>
    [GameSystem]
    public class ForceInformation : GameSystem
    {
        public Force Target;
        public List<SangoObject> default_objects = new List<SangoObject>();
        public List<SangoObject> all_objects = new List<SangoObject>();
        protected string windowName = "window_information_force";

        public void Start(Force target)
        {
            Target = target;
            all_objects = default_objects;
            Push();
        }

        public void Start(Force target, List<SangoObject> city_list)
        {
            Target = target;
            all_objects = city_list;
            Push();
        }

        public override void Init()
        {
            Name = "势力情报";
            GameEvent.OnCityRightMouseButtonContextMenuShow += OnCityRightMouseButtonContextMenuShow;
            GameEvent.OnGameInformationContextMenuShow += OnGameSettingContextMenuShow;
        }
        protected virtual bool CityMenuCanShow()
        {
            return true;
        }

        public override void Clear()
        {
            GameEvent.OnGameInformationContextMenuShow -= OnGameSettingContextMenuShow;
            GameEvent.OnCityRightMouseButtonContextMenuShow -= OnCityRightMouseButtonContextMenuShow;
        }
        protected virtual void OnGameSettingContextMenuShow(IContextMenuData menuData)
        {
            menuData.Add("全势力", 400, null, OnClickMenuItem);
        }

        protected virtual void OnCityRightMouseButtonContextMenuShow(IContextMenuData menuData, City city)
        {
            Target = city.BelongForce;
            // 菜单项与列表同一口径：异族 / 贼、已灭亡势力不进列表，也就不给它们的城池提供入口，
            // 否则点进去会落到"列表第一个势力"上，看起来像打开了别人的情报。
            Force force = city.BelongForce;
            if (CityMenuCanShow() && force != null && force.IsAlive && force.IsNormal)
                menuData.Add(Name, 20, force, OnClickMenuItem, true);
        }

        /// <summary>
        /// 势力情报类窗口的候选势力：**存活 + 普通势力**（排除异民族与贼）。
        ///
        /// 【为什么排除异族 / 贼】这两类势力没有常规内政、外交与技巧研究，
        /// 在情报列表里点开只会得到一堆空面板（技巧树为空、能力路径为空、外交无从谈起），
        /// 还会被玩家误当成可攻略对象。口径与 <see cref="Force.IsNormal"/>
        /// （对齐 Open311 `Force::is_normal()`）一致，也与外交情报、AI 选目标的口径统一。
        ///
        /// 【谁在用】势力情报（<see cref="ForceInformation"/>）、技巧表、能力表三处共用，
        /// 避免各写一遍导致"某个窗口里还能看到已灭亡势力"。
        /// </summary>
        /// <returns>存活且为普通势力的对象列表（按剧本 forceSet 的 Id 顺序，至少可能为空）</returns>
        public static List<SangoObject> GetAliveNormalForces()
        {
            List<SangoObject> result = new List<SangoObject>();

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.forceSet == null)
                return result;

            // 下标即 Id，0 号位是"默认空对象"槽位，从 1 开始扫
            for (int i = 1; i < scenario.forceSet.Count; i++)
            {
                Force force = scenario.forceSet[i];
                if (force == null || !force.IsAlive)
                    continue;
                if (!force.IsNormal)     // 异民族 / 贼
                    continue;
                result.Add(force);
            }
            return result;
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            all_objects = GetAliveNormalForces();

            // 列表为空时不能 Push：原实现对 all_objects[0] 直接取下标，
            // 空列表（所有势力都灭亡 / 剧本数据异常）会抛 ArgumentOutOfRangeException。
            if (all_objects.Count == 0)
            {
                Sango.Log.Warning("势力情报：当前没有可查看的势力（存活且非异族）");
                return;
            }

            Target = contextMenuItem.CustomData as Force;
            // 点城市右键进来的目标势力也要过一遍筛：异族 / 已灭亡势力不进列表，也就不能成为默认项
            if (Target == null || !Target.IsAlive || !Target.IsNormal)
                Target = all_objects[0] as Force;
            Push();
        }

        public override void OnEnter()
        {
            Window.Instance.Open(windowName, this);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.SetVisible(windowName, true);
        }

        public override void OnExit()
        {
            Window.Instance.SetVisible(windowName, false);
        }

        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        Back();
                        break;
                    }
            }
        }
    }
}
