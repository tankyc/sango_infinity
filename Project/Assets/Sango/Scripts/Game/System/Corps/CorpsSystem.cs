using System;
using System.Collections.Generic;
using Sango.Core;
using Sango.UI;

namespace Sango.Core.Player
{
    /// <summary>
    /// 玩家军团系统
    /// </summary>
    [GameSystem]
    public class CorpsSystem : CityBaseSystem
    {
        ContextMenuData menuData = new ContextMenuData();

        /// <summary>
        /// 已占用的军团编号标记(索引=编号-1),长度与Corps.numberTxt支持上限(第50军团)保持一致
        /// </summary>
        public bool[] has = new bool[Corps.numberTxt.Length];
        public int targetNumber = 0;
        public List<Corps> corps_list = new List<Corps>();
        public List<Corps> target_corps_list = new List<Corps>();
        Corps targetCorps;
        int showType = 0;
        public CorpsSystem()
        {
            customTitleName = "军团";
            customTitleList = new List<ObjectSortTitle>()
            {
                CorpsSortFunction.SortByName,
                CorpsSortFunction.SortByNumber,
                CorpsSortFunction.SortByLeader,
            };
            customMenuName = "君主/军团";
            customMenuOrder = 900;
            windowName = "window_corps_menu";


        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.mBelongForce.CityCount > 1;
            }
        }

        public override void OnEnter()
        {
            corps_list.Clear();
            targetNumber = 0;
            for (int i = 0; i < has.Length; i++)
                has[i] = false;
            menuData.Clear();
            Scenario.Cur.corpsSet.ForEach(x =>
            {
                if (x.mBelongForce == TargetCity.mBelongForce)
                {
                    has[x.number - 1] = true;
                    if (x.number > 1)
                    {
                        corps_list.Add(x);
                    }
                }
            });

            for (int i = 0; i < has.Length; i++)
            {
                if (!has[i])
                {
                    targetNumber = i + 1;
                    break;
                }
            }

            // 新建军团菜单
            menuData.Add("新建军团", 10, this, OnClickMenuItem_CreateCorps, targetNumber > 1 && targetNumber < Corps.numberTxt.Length);

            // 重编军团菜单
            menuData.Add("重编军团", 11, this, OnClickMenuItem_RearrangeCorps, corps_list.Count > 0);

            // 解散军团菜单
            menuData.Add("解散军团", 12, this, OnClickMenuItem_DisbandCorps, corps_list.Count > 0);

            // 解散军团菜单
            menuData.Add("返回", 12, this, OnClickMenuItem_Return, true);

            ContextMenu.CloseAll();
            ContextMenu.Show(menuData, UnityEngine.Input.mousePosition, ContextMenuType.Other);
        }

        public override void OnDestroy()
        {
            ContextMenu.CloseAll();
            ContextMenu.Show(ContextMenuData.MenuData, ContextMenuData.MenuData.startPosition);
        }

        private void OnClickMenuItem_Return(IContextMenuItem contextMenuItem)
        {
            GameSystemManager.Instance.Done();
        }


        /// <summary>
        /// 新建军团菜单点击事件
        /// </summary>
        private void OnClickMenuItem_CreateCorps(IContextMenuItem contextMenuItem)
        {
            showType = 1;
            targetCorps = new Corps();
            targetCorps.mBelongForce = TargetCity.mBelongForce;
            targetCorps.number = targetNumber;
            //targetCorps.policy = 0;
            targetCorps.appoint = 0;
            targetCorps.ActionPoint = 255;
            Window.Instance.Open("window_corps_setting", targetCorps, "军团", (System.Action)CreateCorps);
        }

        /// <summary>
        /// 重编军团菜单点击事件
        /// </summary>
        private void OnClickMenuItem_RearrangeCorps(IContextMenuItem contextMenuItem)
        {
            showType = 2;
            GameSystem.GetSystem<CorpsSelectSystem>().Start(corps_list,
            target_corps_list, 1, (x) =>
            {
                if (x.Count > 0)
                {
                    Corps exsist = x[0];
                    targetCorps = new Corps();
                    targetCorps.Id = exsist.Id;
                    targetCorps.mBelongForce = exsist.mBelongForce;
                    targetCorps.number = exsist.number;
                    //targetCorps.policy = exsist.policy;
                    targetCorps.appoint = exsist.appoint;
                    targetCorps.appoint_target = exsist.appoint_target;
                    targetCorps.mComander = exsist.mComander;
                    targetCorps.ActionPoint = exsist.ActionPoint;
                    for (int i = 0; i < exsist.appointSetting.Length; i++)
                        targetCorps.appointSetting[i] = exsist.appointSetting[i];

                    List<City> targetCityList = new List<City>();
                    exsist.mBelongForce.ForEachCity(x =>
                    {
                        if (x.mBelongCorps == exsist)
                            targetCityList.Add(x);
                    });
                    targetCorps.inti_cities = targetCityList;
                    Window.Instance.Open("window_corps_setting", targetCorps, "军团", (System.Action)ResetCorps);
                }
            },
            CorpsSortFunction.DefaultSortList, "重編军团");
        }

        /// <summary>
        /// 解散军团菜单点击事件
        /// </summary>
        private void OnClickMenuItem_DisbandCorps(IContextMenuItem contextMenuItem)
        {
            showType = 3;
            GameSystem.GetSystem<CorpsSelectSystem>().Start(corps_list,
            target_corps_list, 1, (x) =>
            {
                if (x.Count <= 0)
                {
                    return;
                }

                Corps t = x[0];
                if (t.IsCaptainCorps)
                {
                    Sango.Log.Error("不允许解散第一军团!!");
                    return;
                }

                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, $"要将{t.ColorName}解散吗?", () =>
                {
                    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"如今{t.ColorName}已经没有必要了。", () =>
                    {
                        DeleteCorps(t.number);
                        ContextMenu.CloseAll();
                        GameMedia.Instance.PlaySfx(56);
                        Done();
                    }, TargetCity.mBelongForce.mGovernor);
                });
            },
            CorpsSortFunction.DefaultSortList, "解散军团");
        }

        public void CreateCorps()
        {
            TargetCity.mBelongForce.CreateCorps(targetCorps);
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{targetCorps.ColorName}就交给我了。", () =>
            {
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            },targetCorps.mComander);
        }

        public void ResetCorps()
        {
            Corps dest = corps_list.Find(x => x.number == targetCorps.number);
            if (dest == null)
                return;
            TargetCity.mBelongForce.ResetCorps(dest, targetCorps);
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{dest.ColorName}就交给我了。", () =>
            {
                Done();
            },dest.mComander);
            GameMedia.Instance.PlayDoAcitonSfx();
        }

        public void CreateCorps(int number, Person commander, List<City> cities)
        {
            TargetCity.mBelongForce.CreateCorps(number, commander, cities);
        }

        public void DeleteCorps(int number)
        {
            TargetCity.mBelongForce.DeleteCorps(number);
        }

        public void DeleteCorps(Corps corps)
        {
            TargetCity.mBelongForce.DeleteCorps(corps);
        }
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        if (showType == 1 || showType == 2)
                        {
                            Window.Instance.Close("window_corps_setting");
                            showType = 0;
                        }
                        else
                        {
                            Back();
                        }
                        break;
                    }
            }
        }
    }
}
