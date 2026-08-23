using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class PlayerEndTurn : GameSystem
    {

        bool updateTroopAI = false;
        public string customTitleName;

        public override void Init()
        {
            customTitleName = "进行";
            //GameEvent.OnRightMouseButtonContextMenuShow += OnRightMouseButtonContextMenuShow;
        }

        public override void Clear()
        {
            //GameEvent.OnRightMouseButtonContextMenuShow -= OnRightMouseButtonContextMenuShow;
        }

        //protected virtual void OnRightMouseButtonContextMenuShow(IContextMenuData menuData)
        //{
        //    menuData.Add("进行", -9999, null, OnClickMenuItem, true);
        //}

        //protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        //{
        //    ContextMenu.CloseAll();
        //    GameSystemManager.Instance.Push(this);
        //}

        public override void OnEnter()
        {
            updateTroopAI = false;
            Scenario scenario = Scenario.Cur;
            Force force = scenario.CurRunForce;
            bool findNoActionTroop = false;
            Scenario.Cur.troopsSet.ForEach(x =>
            {
                if (!findNoActionTroop && x.IsAlive && x.mBelongForce == Scenario.Cur.CurRunForce && x.mBelongCorps.IsPlayerControl && !x.ActionOver && !x.IsAppoint)
                    findNoActionTroop = true;
            });

            string content;
            if (findNoActionTroop)
                content = $"将结束{force.ColorName}的战略，\n有<color=#f34242>尚未行动的部队</color>存在，\n请问是否确定";
            else
                content = $"结束{force.ColorName}的战略，\n请问是否确定";

            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, content, () =>
            {
                updateTroopAI = true;
                GameEvent.OnPlayerEndTurn?.Invoke(force, scenario);

            },() =>
            {
                Done();
            });
        }

        public override void Update()
        {
            if (!updateTroopAI) return;
            base.Update();
            Scenario scenario = Scenario.Cur;
            Force force = scenario.CurRunForce;
            if (force != null)
            {
                for (int i = 0; i < scenario.troopsSet.Count; ++i)
                {
                    var c = scenario.troopsSet[i];
                    if (c != null && c.IsAlive && c.mBelongForce == force && !c.ActionOver && c.missionType > 0)
                    {
                        if (!c.DoAI(scenario))
                            return;
                        c.Render?.UpdateRender();
                    }
                }

                if (force.CurRunCorps != null)
                    force.CurRunCorps.ActionOver = true;
            }
            Done();
        }

        public override void OnDestroy()
        {
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    GameSystemManager.Instance.Back(); break;
            }

        }
    }
}
