using Sango.Render;
using Sango.UI;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopInteractiveDialog : GameSystem
    {
        public List<Cell> MovePath;
        public Troop TargetTroop { get; set; }
        public Cell TargetCell { get; set; }

        public TroopRender troopRender;

        public void Start(Troop troop, Cell targetCell, Vector3 startPoint)
        {
            TargetTroop = troop;
            TargetCell = targetCell;
            TroopInteractiveDialogData.InteractiveDialogData.Clear();
            GameEvent.OnTroopInteractiveContextDialogShow?.Invoke(TroopInteractiveDialogData.InteractiveDialogData, troop, targetCell);
            if (!TroopInteractiveDialogData.InteractiveDialogData.IsEmpty())
            {
                // 条件不满足时内容仍显示，但确认按钮不响应（可见但置灰）
                System.Action sureAction = TroopInteractiveDialogData.InteractiveDialogData.IsValid
                    ? TroopInteractiveDialogData.InteractiveDialogData.sureAction
                    : null;

                GameDialog.Instance.Open(GameDialog.DialogStyle.ChoosePersonSay, TroopInteractiveDialogData.InteractiveDialogData.content,
                    sureAction, () =>
                    {
                        if (!GameSystemManager.Instance.BackTo(GameSystem.GetSystem<CityExpedition>()))
                        {
                            if (!GameSystemManager.Instance.BackTo(GameSystem.GetSystem<CityTransport>()))
                            {
                                GameSystemManager.Instance.Done();
                            }
                        }
                    }, troop.Leader);
                GameSystemManager.Instance.Push(this);
            }
        }

        public override void OnEnter()
        {
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;
            GameSystem.GetSystem<TroopSystem>().ShowMoveRange();
            GameSystem.GetSystem<TroopSystem>().ShowMovePath();
        }

        /// <summary>
        /// 离开当前命令的时候触发
        /// </summary>
        public override void OnExit()
        {

        }

        public override void OnDestroy()
        {
            GameSystem.GetSystem<TroopSystem>().ClearShowMovePath();
            GameSystem.GetSystem<TroopSystem>().ClearShowMoveRange();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            //switch (eventType)
            //{
            //    case CommandEventType.Cancel:
            //    case CommandEventType.RClick:
            //        {
            //            Back();
            //            break;
            //        }
            //}
        }
    }
}
