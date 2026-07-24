using Sango.Core.Player;
using UnityEngine.UI;
using System.Collections.Generic;

using Sango.Core;
using System.Text;

namespace Sango.UI
{
    /// <summary>
    /// 军团菜单UI类
    /// </summary>
    public class UICorpsAppoint : UGUIWindow
    {
        /// <summary>
        /// 窗口标题
        /// </summary>
        public Text windowTitle;

        /// <summary>
        /// 军团名字
        /// </summary>
        public Text corpsNameText;

        public Button[] corpsAppointBtns;
        public Text[] corpsAppointBtnLabels;

        public Button cancelTransBtn;
        /// <summary>
        /// 当前选中的军团
        /// </summary>
        Corps targetCorps;
        Force targetForce;

        UICorpsSetting uICorpsSetting;

        public static List<ObjectSortTitle> PersonSortList = new List<ObjectSortTitle>
        {
            PersonSortFunction.SortByName,
            PersonSortFunction.SortByBelongCity,
            PersonSortFunction.SortByState,
            PersonSortFunction.SortByLoyalty,
            PersonSortFunction.SortByMerit,
            PersonSortFunction.SortByLevel,
        };

        int[] appointShowType = new int[] {
            0,0,0,0,0,0,
            2,
            1,1,
            0,0,
            3,
            0
        };

        int[] appointSetting;


        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < corpsAppointBtns.Length; i++)
            {
                Button button = corpsAppointBtns[i];
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (appointSetting == null) return;
                    int value = appointSetting[i];
                    int showType = appointShowType[i];
                    switch (showType)
                    {
                        case 0:
                            {
                                value++;
                                if (value > 1)
                                    value = 0;
                                appointSetting[i] = value;
                            }
                            break;
                        case 1:
                            {
                                value++;
                                if (value > 1)
                                    value = 0;
                                appointSetting[i] = value;
                            }
                            break;
                        case 2:
                            {
                                value++;
                                if (value > 1)
                                    value = 0;
                                appointSetting[i] = value;
                            }
                            break;
                        case 3:
                            {


                                List<City> cities = new List<City>();
                                targetCorps.ForEachCity((c) =>
                                {
                                    foreach (City neighbor in c.NeighborList)
                                    {
                                        if (neighbor.IsSameForce(c) && neighbor.BelongCorps != c.BelongCorps)
                                        {
                                            if (!cities.Contains(neighbor))
                                                cities.Add(neighbor);
                                        }
                                    }
                                });
                                List<City> all = new List<City>();
                                if (appointSetting[i] > 0)
                                {
                                    all.Add(Scenario.Cur.citySet.Get(appointSetting[i]));
                                }

                                GameSystem.GetSystem<CitySelectSystem>().Start(cities,
                                    all, 1, (sel_cs) =>
                                    {
                                        appointSetting[i] = sel_cs[0].Id;
                                        cancelTransBtn.interactable = true;

                                    }, CitySortFunction.DefaultSortList, "输送城池选择");

                            }
                            break;
                    }
                    corpsAppointBtnLabels[i].text = GetAppointTypeShow(value, showType);
                });
            }
        }

        /// <summary>
        /// 窗口显示时调用
        /// </summary>
        public override void OnOpen(params object[] objects)
        {
            targetCorps = objects[0] as Corps;
            targetForce = targetCorps.BelongForce;
            corpsNameText.text = targetCorps.Name;
            uICorpsSetting = objects[1] as UICorpsSetting;
            appointSetting = new int[targetCorps.appointSetting.Length];
            for (int i = 0; i < appointSetting.Length; i++)
                appointSetting[i] = targetCorps.appointSetting[i];

            UpdateContent();
        }

        string GetAppointTypeShow(int value, int showType)
        {
            switch (showType)
            {
                case 0:
                    {
                        return value == 0 ? "允许" : "禁止";
                    }
                case 1:
                    {
                        return value == 0 ? "重视" : "轻视";
                    }
                case 2:
                    {
                        return value == 0 ? "轻视" : "重视";
                    }
                case 3:
                    {
                        if(value > 0)
                        {
                            City c = Scenario.Cur.citySet.Get(value);
                            return c.Name;
                        }
                        else
                        {
                            return "无";
                        }
                    }
            }
            return "";
        }

        /// <summary>
        /// 更新内容
        /// </summary>
        public void UpdateContent()
        {
            for (int i = 0; i < corpsAppointBtnLabels.Length; i++)
            {
                int value = appointSetting[i];
                int showType = appointShowType[i];
                corpsAppointBtnLabels[i].text = GetAppointTypeShow(value, showType);
            }

            cancelTransBtn.interactable = appointSetting[(int)Corps.AppointContentType.Transport] > 0;

        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        public void OnCancel()
        {
            Close();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        public void OnCancelTransport()
        {
            appointSetting[(int)Corps.AppointContentType.Transport] = 0;
            UpdateContent();
            cancelTransBtn.interactable = false;
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        public void OnSure()
        {
            for (int i = 0; i < appointSetting.Length; i++)
                targetCorps.appointSetting[i] = appointSetting[i];
            Close();
            uICorpsSetting?.UpdateContent();

        }
    }
}
