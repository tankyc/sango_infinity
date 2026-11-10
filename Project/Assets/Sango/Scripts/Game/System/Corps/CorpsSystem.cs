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
            customMenuOrder = 4000;
            windowName = "window_corps_menu";


        }

        public override bool IsValid
        {
            get
            {
                // 【严格对齐原版 can_district_new_command】
                // 原版四道门槛：有空余编号 / 第 1 军团存在 / 第 1 军团 ≥2 城 /
                // 第 1 军团里 ≥2 座城有武将。
                // 原先只判 `CityCount > 1`，玩家能从只有 2 座城的主军团里再拆出 1 座，
                // 让主军团只剩 0 城（无法反击、无法征兵、AI 调度也会错乱）。
                return TargetCity.BelongForce != null && TargetCity.BelongForce.CityCount > 1;
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
                if (x.BelongForce == TargetCity.BelongForce)
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
            menuData.Add("新建军团", 10, this, OnClickMenuItem_CreateCorps, TargetCity.BelongForce.CanCreateCorps());

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
            targetCorps.BelongForce = TargetCity.BelongForce;
            targetCorps.number = targetNumber;
            // 默认方针取"委任"（Auto）。对齐 Open311 exec_district_new_command
            //（s11_sys_person_order_kunshu.cpp:287-303）：新建命令的参数默认落在 Auto 分支，
            // 即"委任 AI 自行发展"，而不是 None（无方针）。
            targetCorps.appoint = (int)Corps.AppointType.Auto;
            targetCorps.appoint_target = 0;
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
                    targetCorps.BelongForce = exsist.BelongForce;
                    targetCorps.number = exsist.number;
                    targetCorps.appoint = exsist.appoint;
                    targetCorps.appoint_target = exsist.appoint_target;
                    targetCorps.mComander = exsist.mComander;
                    targetCorps.ActionPoint = exsist.ActionPoint;

                    // 【必须整份复制委任相关字段】重编走的是
                    // ResetCorps → DeleteCorps(旧) + CreateCorps(副本)，副本没带上的字段
                    // 会在重建后变成默认值 —— 玩家设好的运输目的地就是这么丢的。
                    // 对齐 Open311 exec_district_reform_command
                    //（s11_sys_person_order_kunshu.cpp:432-436）：命令会把 policy /
                    // mid_objective / setting[] / transport_target 全部写回新军团。
                    targetCorps.mid_objective = exsist.mid_objective;
                    targetCorps.mid_objective_target = exsist.mid_objective_target;
                    targetCorps.transportDestBuildingId = exsist.transportDestBuildingId;
                    targetCorps.transportDestDistrictNumber = exsist.transportDestDistrictNumber;

                    for (int i = 0; i < exsist.appointSetting.Length; i++)
                        targetCorps.appointSetting[i] = exsist.appointSetting[i];

                    List<City> targetCityList = new List<City>();
                    exsist.BelongForce.ForEachCity(x =>
                    {
                        if (x.BelongCorps == exsist)
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
                    }, TargetCity.BelongForce.mGovernor);
                });
            },
            CorpsSortFunction.DefaultSortList, "解散军团");
        }

        public void CreateCorps()
        {
            TargetCity.BelongForce.CreateCorps(targetCorps);
            GameMedia.Instance.PlayDoAcitonSfx();

            // 【先退出命令，再播台词】Done() 挂 GameDialog 回调会被静默丢弃
            // （窗口创建失败 continue / Reset / seq 不匹配），命令不退出就会卡住整个回合。
            // 军团上一步已经建好，台词纯演出。
            Done();
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{targetCorps.ColorName}就交给我了。", null, targetCorps.mComander);
        }

        public void ResetCorps()
        {
            Corps dest = corps_list.Find(x => x.number == targetCorps.number);
            if (dest == null)
                return;
            TargetCity.BelongForce.ResetCorps(dest, targetCorps);
            GameMedia.Instance.PlayDoAcitonSfx();

            // 【先退出命令，再播台词】理由同 CreateCorps：Done() 挂回调有静默丢失风险
            Done();
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{dest.ColorName}就交给我了。", null, dest.mComander);
        }

        public void CreateCorps(int number, Person commander, List<City> cities)
        {
            TargetCity.BelongForce.CreateCorps(number, commander, cities);
        }

        public void DeleteCorps(int number)
        {
            TargetCity.BelongForce.DeleteCorps(number);
        }

        public void DeleteCorps(Corps corps)
        {
            TargetCity.BelongForce.DeleteCorps(corps);
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
