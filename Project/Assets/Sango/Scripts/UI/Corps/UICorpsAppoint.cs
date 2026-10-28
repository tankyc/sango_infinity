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

        /// <summary>
        /// 每个委任按钮的显示/交互类型，下标与 <see cref="Corps.AppointContentType"/> 一一对应。
        ///   0 = 允许 / 禁止（含末尾的"港关驻军"开关：0 = 允许，1 = 禁止）
        ///   1 = 重视 / 轻视   2 = 轻视 / 重视   3 = 选择运输目标城（点击后弹城选择器）
        /// </summary>
        int[] appointShowType = new int[] {
            1,1,1,1,1,1,
            2,
            1,1,
            0,0,
            3,
            0,0,
            0
        };

        int[] appointSetting;

        /// <summary>
        /// 运输目的地的本地草稿（窗口未点"确定"前不写回军团）。
        ///
        /// 对应原版 `District::transport_dest_building_`（s11_scen.h:1023）。
        /// 与 <see cref="Corps.appointSetting"/> 的 Transport 位是**两个独立字段**：
        /// 开关位只存 0/1，目的地存这里 —— 原版由 `District::set_setting`
        /// （s11_scen_obj_district.cpp:224-229）保证两者同步，本项目由
        /// <see cref="Corps.SetTransportDest"/> 承担同一职责。
        /// -1 = 未指定。
        /// </summary>
        int transportDraftBuildingId = -1;


        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < corpsAppointBtns.Length; i++)
            {
                Button button = corpsAppointBtns[i];
                button.onClick.RemoveAllListeners();
                int id = i;
                button.onClick.AddListener(() =>
                {
                    if (this.appointSetting == null) return;
                    int value = this.appointSetting[id];
                    int showType = this.appointShowType[id];
                    switch (showType)
                    {
                        case 0:
                            {
                                value++;
                                if (value > 1)
                                    value = 0;
                                this.appointSetting[id] = value;
                            }
                            break;
                        case 1:
                            {
                                value++;
                                if (value > 1)
                                    value = 0;
                                this.appointSetting[id] = value;
                            }
                            break;
                        case 2:
                            {
                                value++;
                                if (value > 1)
                                    value = 0;
                                this.appointSetting[id] = value;
                            }
                            break;
                        case 3:
                            {
                                // 目的地写进 transportDraftBuildingId（原版 transport_dest_building_），
                                // 开关位只记 1 —— 原版由 set_setting 原子写入两者
                                // （s11_scen_obj_district.cpp:224-229）。
                                // 城市列表取自**全势力** CityList，可跨军团选城：这就是原版的
                                // "跨军团运输" —— get_player_district_transport_dest 只校验
                                // 活着 / 同势力 / 非自身，不校验军团
                                // （s11_game_ai_kyoten_cmd_mil_transport.cpp:417-431）。
                                GameSystem.GetSystem<CitySelectSystem>().Start(targetCorps.BelongForce.CityList,
                                    null, 1, (sel_cs) =>
                                    {
                                        if (sel_cs == null || sel_cs.Count == 0) return;
                                        this.transportDraftBuildingId = sel_cs[0].Id;
                                        this.appointSetting[id] = 1;
                                        cancelTransBtn.interactable = true;
                                        corpsAppointBtnLabels[id].text = GetAppointTypeShow(1, 3);
                                    }, CitySortFunction.DefaultSortList, "输送城池选择");

                            }
                            break;
                    }
                    corpsAppointBtnLabels[id].text = GetAppointTypeShow(value, showType);
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

            // 运输目的地草稿：对应原版 transport_dest_building_（s11_scen.h:1023）
            transportDraftBuildingId = targetCorps.transportDestBuildingId;
            // 旧存档只记了"目标军团编号"时，按原版 District::update()
            // （s11_scen_obj_district.cpp:49-55）解析成该军团都督所在据点。
            // 点"确定"后由 SetTransportDest 固化进 transport_dest_building_ 并清军团编号，
            // 等价原版"惰性解析后写回字段"的行为。
            if (transportDraftBuildingId <= 0)
                transportDraftBuildingId = targetCorps.GetTransportDestCityId(Scenario.Cur);

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
                        // 显示"运输目的地"。注意：不能拿 value 当城 Id ——
                        // 按原版结构，开关位（setting_[Transport]，s11_scen.h:1025）
                        // 与目的地（transport_dest_building_，s11_scen.h:1023）是两个字段。
                        // 开关为 0 时即使军团上还留着旧目的地，也只显示"无"
                        // （原版 set_setting 取消运输时会把目的地一起清成 -1）。
                        if (value == 0)
                            return "无";
                        int destId = transportDraftBuildingId;
                        if (destId <= 0 && targetCorps != null)
                            destId = targetCorps.GetTransportDestCityId(Scenario.Cur);
                        if (destId > 0)
                        {
                            City c = Scenario.Cur.citySet.Get(destId);
                            return c != null ? c.Name : "无";
                        }
                        return "无";
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

            // "取消输送"可用 ⇔ 已指定目的地（对应原版 transport_dest_building_ >= 0）
            cancelTransBtn.interactable = transportDraftBuildingId > 0;

        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        public void OnCancel()
        {
            Close();
        }

        /// <summary>
        /// 取消运输对象：清空目的地**与**开关。
        ///
        /// 对齐出处：原版 `District::set_setting(Transport, false, -1)`
        /// （s11_scen_obj_district.cpp:224-229）—— 两者必须一起清，
        /// 只清开关会留下一个"永不生效的残留目的地"。
        /// </summary>
        public void OnCancelTransport()
        {
            transportDraftBuildingId = -1;
            appointSetting[(int)Corps.AppointContentType.Transport] = 0;
            UpdateContent();
            cancelTransBtn.interactable = false;
        }

        /// <summary>
        /// 确定按钮：把草稿落盘到军团。
        /// </summary>
        public void OnSure()
        {
            // 先写运输目的地（内部会同步 appointSetting[Transport]），
            // 再整体写回数组 —— 顺序反了会被下面的循环覆盖成旧开关值。
            if (targetCorps != null)
                targetCorps.SetTransportDest(transportDraftBuildingId);

            for (int i = 0; i < appointSetting.Length; i++)
                targetCorps.appointSetting[i] = appointSetting[i];
            Close();
            uICorpsSetting?.UpdateContent();

        }
    }
}
