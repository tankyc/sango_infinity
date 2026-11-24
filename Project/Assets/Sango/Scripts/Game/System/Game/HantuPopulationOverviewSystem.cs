using Sango.UI;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core
{
    /// <summary>人口总览使用独立命令，关闭时按原生命令栈恢复地图操作。</summary>
    [GameSystem]
    public sealed class HantuPopulationOverviewSystem : GameSystem
    {
        UIHantuPopulationOverview view;

        public override void Init()
        {
            Clear();
            GameEvent.OnGameInformationContextMenuShow += AddMenu;
            GameEvent.OnRightMouseButtonContextMenuShow += AddMenu;
            GameEvent.OnScenarioEnd += OnScenarioEnd;
        }

        public override void Clear()
        {
            GameEvent.OnGameInformationContextMenuShow -= AddMenu;
            GameEvent.OnRightMouseButtonContextMenuShow -= AddMenu;
            GameEvent.OnScenarioEnd -= OnScenarioEnd;
            CloseView();
        }

        void AddMenu(IContextMenuData menu)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Variables?.hantuPopulationEnabled != true) return;
            menu.Add("人口总览", 205, null, item =>
            {
                ContextMenu.CloseAll();
                if (view == null) Push();
            });
        }

        public override void OnEnter()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || Game.Instance.UIRoot == null)
            {
                Back();
                return;
            }
            try { view = UIHantuPopulationOverview.Create(Game.Instance.UIRoot, scenario, Back); }
            catch (System.Exception exception)
            {
                Sango.Log.Error("人口总览打开失败：" + exception);
                Back();
            }
        }

        public override void OnDestroy() { CloseView(); }

        void OnScenarioEnd(Scenario scenario) { Back(); }

        void CloseView()
        {
            if (view != null)
            {
                view.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(view.gameObject);
                view = null;
            }
        }

        public override void HandleEvent(CommandEventType type, Cell cell, Vector3 position, bool isOverUI)
        {
            if (type == CommandEventType.Cancel || type == CommandEventType.RClickDown) Back();
        }
    }
}
