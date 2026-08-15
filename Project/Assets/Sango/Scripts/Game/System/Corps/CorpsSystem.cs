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
        public bool[] has = new bool[8];
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
                return TargetCity.mForce.CityCount > 1;
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
                if (x.mForce == TargetCity.mForce)
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
            menuData.Add("新建军团", 10, this, OnClickMenuItem_CreateCorps, targetNumber > 1 && targetNumber < 10);

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
            targetCorps.mForce = TargetCity.mForce;
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
                    targetCorps.mForce = exsist.mForce;
                    targetCorps.number = exsist.number;
                    //targetCorps.policy = exsist.policy;
                    targetCorps.appoint = exsist.appoint;
                    targetCorps.appoint_target = exsist.appoint_target;
                    targetCorps.mComander = exsist.mComander;
                    targetCorps.ActionPoint = exsist.ActionPoint;
                    for (int i = 0; i < exsist.appointSetting.Length; i++)
                        targetCorps.appointSetting[i] = exsist.appointSetting[i];

                    List<City> targetCityList = new List<City>();
                    exsist.mForce.ForEachCity(x =>
                    {
                        if (x.mCorps == exsist)
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

                GameDialog.Open($"要将{t.ColorName}解散吗?", () =>
                {
                    GameDialog.Close();
                    GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"如今{t.ColorName}已经没有必要了。", () =>
                    {
                        DeleteCorps(t.number);
                        GameDialog.Close();
                        ContextMenu.CloseAll();
                        GameMedia.Instance.PlaySfx(56);
                        Done();
                    }).SetPerson(TargetCity.mForce.mGovernor);
                }).cancelAction = () =>
                {
                    GameDialog.Close();
                };
            },
            CorpsSortFunction.DefaultSortList, "解散军团");
        }

        public void CreateCorps()
        {
            TargetCity.mForce.CreateCorps(targetCorps);
            GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{targetCorps.ColorName}就交给我了。", () =>
            {
                GameDialog.Close();
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            }).SetPerson(targetCorps.mComander);
        }

        public void ResetCorps()
        {
            Corps dest = corps_list.Find(x => x.number == targetCorps.number);
            if (dest == null)
                return;
            TargetCity.mForce.ResetCorps(dest, targetCorps);
            GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{dest.ColorName}就交给我了。", () =>
            {
                GameDialog.Close();
                Done();
            }).SetPerson(dest.mComander);
            GameMedia.Instance.PlayDoAcitonSfx();
        }

        public void CreateCorps(int number, Person commander, List<City> cities)
        {
            TargetCity.mForce.CreateCorps(number, commander, cities);
        }

        public void DeleteCorps(int number)
        {
            TargetCity.mForce.DeleteCorps(number);
        }

        public void DeleteCorps(Corps corps)
        {
            TargetCity.mForce.DeleteCorps(corps);
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
