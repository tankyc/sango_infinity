using Sango.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// "新建势力"二级编辑界面。
    /// 组件均在 prefab 中手动指派：
    /// 势力名称输入框、君主/本城/支配数/爵位/国号 各一个文本、势力颜色显示、头像RawImage，
    /// 以及五个按钮：作成、都市、势力颜色、爵位、初始化。
    /// </summary>
    public class UIScenarioCreateEditNewForce : UGUIWindow
    {
        // ===== 手动指派组件（从 prefab 中拖拽绑定） =====

        // 势力名称输入
        public InputField forceNameInput;
        // 君主名称文本
        public Text governorText;
        // 本城名称文本
        public Text cityText;
        // 支配数文本
        public Text cityCountText;
        // 势力颜色显示（Image）
        public Image flagColorImage;
        // 爵位名称文本
        public Text rankText;
        // 国号显示文本
        public Text nationText;
        // 君主头像（RawImage）
        public RawImage avatarImage;

        // 作成按钮
        public Button createBtn;
        // 都市按钮
        public Button cityBtn;
        // 势力颜色按钮
        public Button flagColorBtn;
        // 爵位按钮
        public Button rankBtn;
        // 初始化按钮
        public Button initBtn;
        public Button sureBtn;

        // ===== 数据 =====
        UIEditWorldMap uIEditWorldMap;

        Title selectedTitle;
        ShortScenario scenario;
        ShortScenario src_scenario;
        ScenarioCommonData commonData;
        public Action onClose;
        public GameObject hideNodeOnEditForce;

        /// <summary>
        /// 本界面持有的新势力数据，确认时保存到 addData.NewForces[slotIndex]
        /// </summary>
        ShortForce newForceData;

        bool eventsBound;

        // 默认势力名（君主姓氏 + "军"）
        string defaultForceName;
        // 默认国号
        string defaultNationName;

        /// <summary>
        /// 设置隐藏节点的显示状态
        /// </summary>
        void SetHideNodeActive(bool active)
        {
            if (hideNodeOnEditForce != null)
                hideNodeOnEditForce.SetActive(active);
        }

        public override void OnOpen(params object[] args)
        {
            base.OnOpen(args);
            SetHideNodeActive(true);
            // 解析参数
            uIEditWorldMap = GetArg<UIEditWorldMap>(args, 0);
            int editForceDataId = GetArgInt(args, 1);
            src_scenario = GetArg<ShortScenario>(args, 2);
            scenario = src_scenario.Copy();
            newForceData = scenario.forceSet.Get(editForceDataId);
            commonData = GetArg<ScenarioCommonData>(args, 3);
            BindButtonEvents();
            RefreshUI();
        }

        /// <summary>
        /// 从参数数组安全取值
        /// </summary>
        T GetArg<T>(object[] args, int index) where T : class
        {
            if (args != null && args.Length > index && args[index] is T t)
                return t;
            return null;
        }

        /// <summary>
        /// 从参数数组安全取整数值
        /// </summary>
        int GetArgInt(object[] args, int index)
        {
            if (args != null && args.Length > index && args[index] is int i)
                return i;
            return -1;
        }

        /// <summary>
        /// 刷新界面显示
        /// </summary>
        void RefreshUI()
        {
            bool isValid = newForceData.Governor > 0;
            createBtn.interactable = !isValid;
            // 都市按钮
            cityBtn.interactable = isValid;
            // 势力颜色按钮
            flagColorBtn.interactable = isValid;
            // 爵位按钮
            rankBtn.interactable = false;
            // 初始化按钮
            initBtn.interactable = isValid;

            // 君主
            if (governorText != null)
                governorText.text = newForceData.Governor > 0 ? scenario.personSet[newForceData.Governor].Name : "-";

            // 本城
            if (cityText != null)
                cityText.text = newForceData.CapitalCity > 0 ? scenario.citySet[newForceData.CapitalCity].Name : "-";

            // 支配数（当前只有本城，所以为 1；后续可扩展）
            if (cityCountText != null)
            {
                int count = 0;
                scenario.citySet.ForEach(x =>
                {
                    if (x.BelongForce == newForceData.Id)
                        count++;
                });
                cityCountText.text = count.ToString();
            }

            // 势力颜色
            if (flagColorImage != null)
                flagColorImage.color = newForceData.Flag > 0 ? commonData.Flags[newForceData.Flag].color : Color.white;

            // 爵位：自动分配第一个可用爵位
            if (selectedTitle == null && commonData != null)
                selectedTitle = GetNextAvailableTitle();
            if (rankText != null)
                rankText.text = selectedTitle != null ? selectedTitle.Name : "无";

            // 国号
            if (nationText != null)
                nationText.text = defaultNationName;

            // 君主头像
            RefreshAvatar();
            sureBtn.interactable = isValid;
        }

        /// <summary>
        /// 刷新君主头像
        /// </summary>
        void RefreshAvatar()
        {
            if (avatarImage == null)
                return;
            if (newForceData.Governor > 0)
            {

                Texture tex = GameRenderHelper.LoadHeadIcon(scenario.personSet[newForceData.Governor].headIconID);
                if (tex != null)
                {
                    avatarImage.texture = tex;
                    avatarImage.enabled = true;
                    return;
                }
            }
            avatarImage.enabled = false;
        }

        /// <summary>
        /// 获取下一个可用爵位（按 commonData 中 Titles 顺序取未使用的）
        /// </summary>
        Title GetNextAvailableTitle()
        {
            if (commonData == null || commonData.Titles == null)
                return null;

            //var addData = UIScenarioAddonMenu.AddData;
            //if (addData == null || addData.NewForces == null)
            //    return null;

            //// 收集已用爵位
            //HashSet<int> usedTitleIds = new HashSet<int>();
            //foreach (var nf in addData.NewForces)
            //{
            //    if (nf.Title != null)
            //        usedTitleIds.Add(nf.Title.Id);
            //}

            //foreach (var title in commonData.Titles.objects)
            //{
            //    if (title != null && !usedTitleIds.Contains(title.Id))
            //        return title;
            //}
            return null;
        }

        #region 按钮事件

        void BindButtonEvents()
        {
            if (eventsBound)
                return;
            eventsBound = true;

            BindEvent(createBtn, OnCreate);
            BindEvent(cityBtn, OnSelectCity);
            BindEvent(flagColorBtn, OnSelectFlagColor);
            BindEvent(rankBtn, OnSelectRank);
            BindEvent(initBtn, OnInit);
        }

        void BindEvent(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        /// <summary>
        /// 作成：打开选君主/本城界面，由 OnCreateForce 回调完成数据保存
        /// </summary>
        void OnCreate()
        {
            Window.Instance.Open("window_scenario_edit_select_governor", uIEditWorldMap, (Action<PersonLib, ShortCity>)OnCreateForce).
                ugui_instance.
                OnCloseAction = () =>
            {
                SetHideNodeActive(true);
            };
            SetHideNodeActive(false);


        }

        void OnCreateForce(PersonLib person, ShortCity city)
        {
            // 填充持有的 NewForceData
            newForceData.Governor = person.targetShortPersonId;
            newForceData.CapitalCity = city.Id;
            uIEditWorldMap.RefreshCity();
            hideNodeOnEditForce.SetActive(true);
            RefreshUI();
        }

        /// <summary>
        /// 都市：循环切换到下一个空白城市
        /// </summary>
        void OnSelectCity()
        {
            Window.Instance.Open("window_scenario_edit_select_city", uIEditWorldMap, (Action<List<ShortCity>>)OnChangeCites).
                ugui_instance.
                OnCloseAction = () =>
                {
                    SetHideNodeActive(true);
                    RefreshUI();
                    uIEditWorldMap.RefreshCity();
                }; ;
        }

        void OnChangeCites(List<ShortCity> cityList)
        {
            uIEditWorldMap.RefreshCity();
            RefreshUI();
        }

        /// <summary>
        /// 势力颜色：循环切换到下一个可用旗帜颜色
        /// </summary>
        void OnSelectFlagColor()
        {
            Window.Instance.Open("window_flag_color_selector", (Action<Flag>)OnChangeFlag);
        }

        void OnChangeFlag(Flag flag)
        {
            newForceData.Flag = flag.Id;
            uIEditWorldMap.RefreshCity();
            RefreshUI();
        }

        /// <summary>
        /// 爵位：循环切换到下一个可用爵位
        /// </summary>
        void OnSelectRank()
        {
            List<Title> titles = new List<Title>();
            if (commonData != null && commonData.Titles != null && commonData.Titles.objects != null)
            {
                foreach (var title in commonData.Titles.objects)
                {
                    if (title != null)
                        titles.Add(title);
                }
            }
            if (titles.Count == 0)
                return;

            int idx = titles.IndexOf(selectedTitle);
            idx = (idx + 1) % titles.Count;
            selectedTitle = titles[idx];
            RefreshUI();
        }

        /// <summary>
        /// 初始化：重置为默认值
        /// </summary>
        void OnInit()
        {
            //newForceData.Governor = 0;
            //newForceData.Flag = 0;
            //newForceData.CapitalCity = 0;

            RefreshUI();
            uIEditWorldMap.RefreshCity();
        }

        public void OnReturn()
        {
            Close();
        }

        /// <summary>
        /// 确认：关闭本窗口，回到 AddonMenu 并刷新
        /// </summary>
        public void OnConfirm()
        {
            src_scenario.citySet = scenario.citySet;
            src_scenario.forceSet = scenario.forceSet;
            src_scenario.personSet = scenario.personSet;
            Close();
        }

        #endregion

        #region 数据查询


        #endregion
    }
}
