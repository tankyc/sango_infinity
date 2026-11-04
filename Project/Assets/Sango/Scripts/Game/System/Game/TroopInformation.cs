using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城池治安系统逻辑
    /// </summary>
    [GameSystem]
    public class TroopInformation : GameSystem
    {
        public Troop Target;
        public City TargetCity;
        public List<SangoObject> default_objects = new List<SangoObject>();
        public List<SangoObject> all_objects = new List<SangoObject>();
        protected string windowName = "window_information_troop";

        public void Start(Troop target)
        {
            Target = target;
            all_objects = new List<SangoObject>() { target };
            Push();
        }

        public void Start(Troop target, List<SangoObject> troop_list)
        {
            Target = target;
            all_objects = troop_list;
            Push();
        }

        public override void Init()
        {
            Name = "部队情报";
            GameEvent.OnTroopRightMouseButtonContextMenuShow += OnTroopRightMouseButtonContextMenuShow;
            GameEvent.OnGameInformationContextMenuShow += OnGameSettingContextMenuShow;
#if UNITY_ANDROID || UNITY_IPHONE
            GameEvent.OnTroopContextMenuShow += OnTroopContextMenuShow;
            GameEvent.OnTroopActionContextMenuShow += OnTroopActionContextMenuShow;
#endif
        }
        protected virtual bool MenuCanShow()
        {
            return true;
        }

        public override void Clear()
        {
            GameEvent.OnTroopRightMouseButtonContextMenuShow -= OnTroopRightMouseButtonContextMenuShow;
            GameEvent.OnGameInformationContextMenuShow -= OnGameSettingContextMenuShow;
#if UNITY_ANDROID || UNITY_IPHONE
            GameEvent.OnTroopContextMenuShow -= OnTroopContextMenuShow;
            GameEvent.OnTroopActionContextMenuShow -= OnTroopActionContextMenuShow;
#endif
        }

        protected virtual void OnGameSettingContextMenuShow(IContextMenuData menuData)
        {
            default_objects.Clear();

            // 剧本可能已经被卸载：退出到主菜单、读档失败兜底等路径都会把 Scenario.Cur 置空
            // （Scenario.cs:1613-1621 的 finally 里 Cur = null），而 UIGame 界面此时还没销毁，
            // 玩家仍然点得到"情报"按钮。
            // 直接写 Scenario.Cur.troopsSet 就会抛 NullReferenceException，
            // 报错行正是本方法里发起遍历的那一行（Unity 栈显示为 TroopInformation.cs:61）。
            Scenario scenario = Scenario.Cur;
            if (scenario != null && scenario.troopsSet != null)
            {
                scenario.troopsSet.ForEach(troop =>
                {
                    default_objects.Add(troop);
                });
            }

            if (default_objects.Count > 0)
            {
                Target = default_objects[0] as Troop;
            }

            // 没有部队时菜单项仍然给出，但置灰并写明原因，
            // 避免玩家点了没反应、又不知道是当前真的没有部队
            menuData.Add("全部队", 300, null, OnClickMenuItem,
                default_objects.Count > 0,
                default_objects.Count > 0 ? null : "当前没有部队");
        }

        protected virtual void OnTroopRightMouseButtonContextMenuShow(IContextMenuData menuData, Troop troop)
        {
            default_objects.Clear();
            Target = troop;
            default_objects.Add(troop);
            if (MenuCanShow())
                menuData.Add(Name, 20, null, OnClickMenuItem, true);
        }

#if UNITY_ANDROID || UNITY_IPHONE


        protected virtual void OnTroopContextMenuShow(IContextMenuData menuData, Troop troop)
        {
            Target = troop;
            default_objects.Clear();
            default_objects.Add(troop);
            if (!troop.IsPlayer || troop.ActionOver)
                menuData.Add("情报", 1000, troop, OnClickMenuItem, true);
        }

        protected virtual void OnTroopActionContextMenuShow(IContextMenuData menuData, Troop troop, Cell actionCell)
        {

            if (troop.BelongForce != null && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
            {
                if (troop.IsJustCreated) return;

                Target = troop;
                default_objects.Clear();
                default_objects.Add(troop);
                menuData.Add("情报", 9000, troop, OnClickMenuItemInAction, true);
            }
        }
        
#endif
        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();
            all_objects = default_objects;
            Push();
        }

        protected virtual void OnClickMenuItemInAction(IContextMenuItem contextMenuItem)
        {
            GameSystemManager.Instance.Done();
            ContextMenu.CloseAll();
            all_objects = default_objects;
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
