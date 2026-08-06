using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UITroopSupply : UGUIWindow
    {
        Action<ItemStore, int, int, int> sureAction;
        Action cancelAction;
        Troop srcTroop;
        Troop targetTroop;
        int food;
        int gold;
        int troops;
        ItemStore itemStore = new ItemStore();

        public override void OnOpen(params object[] objects )
        {
            srcTroop = (Troop)objects[0];
            targetTroop = (Troop)objects[1];
            sureAction = (Action<ItemStore, int, int, int>)objects[2];
            cancelAction = (Action)objects[3];
            itemStore.Clear();
            food = 0;
            gold = 0;
            troops = 0;



        }


        /// <summary>
        /// 退出
        /// </summary>
        public void OnCancel()
        {
            GameSystemManager.Instance.Back();
        }

        /// <summary>
        /// 新建建筑
        /// </summary>
        public void OnSure()
        {
            Close();
            sureAction?.Invoke(itemStore, gold, food, troops);
        }
    }
}
