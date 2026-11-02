using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    public class CityBaseSystem : GameSystem
    {
        public City TargetCity { get; set; }
        public List<Person> personList = new List<Person>();

        public string customTitleName;
        public List<ObjectSortTitle> customTitleList;
        public string customMenuName;
        public int customMenuOrder;
        public string windowName;

        public int wonderNumber;

        public override void Init()
        {
            GameEvent.OnCityContextMenuShow += OnCityContextMenuShow;

            // 把本按钮的排序值登记到 CityMenuOrder（记录的是框架已有的值，供 MOD 查询）
            CityMenuOrder.Record(customMenuName, customMenuOrder);
        }

        public override void Clear()
        {
            GameEvent.OnCityContextMenuShow -= OnCityContextMenuShow;
        }

        protected virtual bool MenuCanShow()
        {
            return TargetCity.IsCity();
        }

        public virtual bool IsValid => true;

        /// <summary>
        /// 本命令不可用时的原因文案（可用时返回 null）。
        ///
        /// 对齐 Open311 各 `can_*_command` 里的 `set_command_error_text(...)` ——
        /// 原版在返回 false 前都会写明"为什么不能执行"。
        /// <para>
        /// 覆写约定：只在 `IsValid == false` 时被调用。
        /// **文案不再拼进菜单按钮标题**（按钮宽度固定，追加会把文本撑出框外，
        /// 见 <see cref="Sango.UI.UIMenuItem.SetValid"/>），只随
        /// <c>ContextMenuItem.disabledReason</c> 一起保留在数据层，
        /// 供将来的悬浮提示 / 调试面板使用。
        /// </para>
        /// </summary>
        /// <param name="city">目标据点</param>
        /// <returns>原因文案；null 表示无具体原因</returns>
        public virtual string GetInvalidReason(City city)
        {
            return null;
        }

        protected virtual void OnCityContextMenuShow(IContextMenuData menuData, City city)
        {
            TargetCity = city;
            if (MenuCanShow() && city.BelongForce != null && city.BelongForce.IsPlayer && city.BelongForce == Scenario.Cur.CurRunForce)
            {
                // IsValid 可能有副作用（如填充候选列表），只取一次
                bool valid = IsValid;
                // 排序值仍以本系统自己的 customMenuOrder 为准；Add 的同时登记进 CityMenuOrder 供 MOD 查询
                CityMenuOrder.Add(menuData, customMenuName, customMenuOrder, city, OnClickMenuItem,
                    valid, valid ? null : GetInvalidReason(city));
            }
        }
        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            TargetCity = contextMenuItem.CustomData as City;
            GameSystemManager.Instance.Push(this);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            personList.Clear();
            RecommandPersonList();
            UpdateJobValue();
            Window.Instance.Open(windowName);
        }
        public virtual void RecommandPersonList()
        {

        }
        public virtual void UpdateJobValue()
        {
            wonderNumber = CalculateWonderNumber();
        }

        public virtual int CalculateWonderNumber()
        {
            return 0;
        }

        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        public virtual void DoJob()
        {
            Done();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        // 数值输入器(window_calculator)打开时不响应返回,
                        // 否则会连同窗口一起退出,留下一个悬空的输入器
                        if (Window.Instance != null && Window.Instance.IsOpen("window_calculator"))
                        {
                            break;
                        }
                        Back();
                        break;
                    }
            }
        }
    }
}
