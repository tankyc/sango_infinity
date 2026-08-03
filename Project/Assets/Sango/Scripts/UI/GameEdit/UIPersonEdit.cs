using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 武将编辑快照 - 保存Person可编辑属性的独立副本
    /// 界面操作仅修改快照,确认时才同步回Target
    /// </summary>
    internal struct PersonEditSnapshot
    {
        #region 兵种适应力 (0=S, 1=A, 2=B, 3=C)
        /// <summary>枪兵适应力</summary>
        public int spearLv;
        /// <summary>戟兵适应力</summary>
        public int halberdLv;
        /// <summary>弓弩适应力</summary>
        public int crossbowLv;
        /// <summary>骑兵适应力</summary>
        public int rideLv;
        /// <summary>水军适应力</summary>
        public int waterLv;
        /// <summary>器械适应力</summary>
        public int machineLv;
        #endregion

        #region 五维属性
        /// <summary>统率</summary>
        public int command;
        /// <summary>武力</summary>
        public int strength;
        /// <summary>智力</summary>
        public int intelligence;
        /// <summary>政治</summary>
        public int politics;
        /// <summary>魅力</summary>
        public int glamour;
        #endregion

        #region 直接属性
        /// <summary>相性</summary>
        public int compatibility;
        /// <summary>忠诚</summary>
        public int loyalty;
        /// <summary>功绩</summary>
        public int merit;
        #endregion

        #region 特技
        /// <summary>当前编辑中的特技列表</summary>
        public List<Feature> EditingFeatures;
        #endregion

        #region 亲爱/厌恶武将
        /// <summary>亲爱武将列表（拷贝）</summary>
        public List<Person> LikePersons;
        /// <summary>厌恶武将列表（拷贝）</summary>
        public List<Person> HatePersons;
        #endregion

        /// <summary>
        /// 从Person对象创建快照 - 拷贝所有可编辑字段
        /// </summary>
        /// <param name="p">源武将对象</param>
        /// <returns>武将编辑快照</returns>
        public static PersonEditSnapshot FromPerson(Person p)
        {
            PersonEditSnapshot snapshot = new PersonEditSnapshot();

            // 兵种适应力
            snapshot.spearLv = p.spearLv.baseValue;
            snapshot.halberdLv = p.halberdLv.baseValue;
            snapshot.crossbowLv = p.crossbowLv.baseValue;
            snapshot.rideLv = p.rideLv.baseValue;
            snapshot.waterLv = p.waterLv.baseValue;
            snapshot.machineLv = p.machineLv.baseValue;

            // 五维属性
            snapshot.command = p.command.baseValue;
            snapshot.strength = p.strength.baseValue;
            snapshot.intelligence = p.intelligence.baseValue;
            snapshot.politics = p.politics.baseValue;
            snapshot.glamour = p.glamour.baseValue;

            // 直接属性
            snapshot.compatibility = p.compatibility;
            snapshot.loyalty = p.loyalty;
            snapshot.merit = p.merit;

            // 特技 - 拷贝FeatureList
            snapshot.EditingFeatures = new List<Feature>();
            if (p.FeatureList != null)
            {
                foreach (Feature f in p.FeatureList)
                {
                    if (f != null)
                    {
                        snapshot.EditingFeatures.Add(f);
                    }
                }
            }

            // 亲爱武将列表
            snapshot.LikePersons = new List<Person>();
            if (p.LikePersonList != null)
            {
                foreach (Person likePerson in p.LikePersonList)
                {
                    if (likePerson != null)
                    {
                        snapshot.LikePersons.Add(likePerson);
                    }
                }
            }

            // 厌恶武将列表
            snapshot.HatePersons = new List<Person>();
            if (p.HatePersonList != null)
            {
                foreach (Person hatePerson in p.HatePersonList)
                {
                    if (hatePerson != null)
                    {
                        snapshot.HatePersons.Add(hatePerson);
                    }
                }
            }

            return snapshot;
        }

        /// <summary>
        /// 将快照数据同步回Person对象 - 仅在确认时调用
        /// </summary>
        /// <param name="p">目标武将对象</param>
        public void ApplyTo(Person p)
        {
            // 兵种适应力
            p.spearLv.baseValue = spearLv;
            p.spearLv.Update();
            p.halberdLv.baseValue = halberdLv;
            p.halberdLv.Update();
            p.crossbowLv.baseValue = crossbowLv;
            p.crossbowLv.Update();
            p.rideLv.baseValue = rideLv;
            p.rideLv.Update();
            p.waterLv.baseValue = waterLv;
            p.waterLv.Update();
            p.machineLv.baseValue = machineLv;
            p.machineLv.Update();

            // 五维属性
            p.command.baseValue = command;
            p.command.Update();
            p.strength.baseValue = strength;
            p.strength.Update();
            p.intelligence.baseValue = intelligence;
            p.intelligence.Update();
            p.politics.baseValue = politics;
            p.politics.Update();
            p.glamour.baseValue = glamour;
            p.glamour.Update();

            // 直接属性
            p.compatibility = compatibility;
            p.loyalty = loyalty;
            p.merit = merit;

            // 特技
            if (EditingFeatures != null && EditingFeatures.Count > 0)
            {
                if (p.FeatureList != null)
                    p.FeatureList.Clear();
                else
                    p.FeatureList = new SangoObjectList<Feature>();

                for (int i = 0; i < EditingFeatures.Count; i++)
                {
                    if (EditingFeatures[i] != null)
                    {
                        p.FeatureList.Add(EditingFeatures[i]);
                    }
                }
            }

            // 亲爱武将列表
            if (LikePersons != null && LikePersons.Count > 0)
            {
                if (p.LikePersonList != null)
                    p.LikePersonList.Clear();
                else
                    p.LikePersonList = new SangoObjectList<Person>();
                for (int i = 0; i < LikePersons.Count; i++)
                {
                    if (LikePersons[i] != null)
                    {
                        p.LikePersonList.Add(LikePersons[i]);
                    }
                }
            }
            else
            {
                if (p.LikePersonList != null)
                    p.LikePersonList.Clear();
            }

            // 厌恶武将列表
            if (HatePersons != null && HatePersons.Count > 0)
            {
                if (p.HatePersonList != null)
                    p.HatePersonList.Clear();
                else
                    p.HatePersonList = new SangoObjectList<Person>();

                for (int i = 0; i < HatePersons.Count; i++)
                {
                    if (HatePersons[i] != null)
                    {
                        p.HatePersonList.Add(HatePersons[i]);
                    }
                }
            }
            else
            {
                if (p.HatePersonList != null)
                    p.HatePersonList.Clear();
            }


            // 刷新部队
            if(p.BelongTroop != null)
            {
                p.BelongTroop.ResetActionAndStatus();
            }
        }
    }

    /// <summary>
    /// 武将编辑窗口 - 编辑武将的各类属性、适应力、特技、亲爱武将、厌恶武将等
    /// 关联窗口: window_edit_person
    /// 使用快照模式: 界面操作仅修改快照,确认时才同步到Target
    /// </summary>
    public class UIPersonEdit : UGUIWindow
    {
        #region 基础引用
        /// <summary>
        /// 根节点
        /// </summary>
        public RectTransform root;

        /// <summary>
        /// 武将姓名标签
        /// </summary>
        public Text nameLabel;
        #endregion

        #region 适应力 - 6种兵种 × 4个等级(S/A/B/C)
        /// <summary>
        /// 兵种适应等级(S/A/B/C) - 枪兵
        /// </summary>
        public Toggle[] spearAdaptToggles = new Toggle[4];

        /// <summary>
        /// 兵种适应等级(S/A/B/C) - 戟兵
        /// </summary>
        public Toggle[] halberdAdaptToggles = new Toggle[4];

        /// <summary>
        /// 兵种适应等级(S/A/B/C) - 弓弩
        /// </summary>
        public Toggle[] crossbowAdaptToggles = new Toggle[4];

        /// <summary>
        /// 兵种适应等级(S/A/B/C) - 骑兵
        /// </summary>
        public Toggle[] rideAdaptToggles = new Toggle[4];

        /// <summary>
        /// 兵种适应等级(S/A/B/C) - 水军
        /// </summary>
        public Toggle[] waterAdaptToggles = new Toggle[4];

        /// <summary>
        /// 兵种适应等级(S/A/B/C) - 器械
        /// </summary>
        public Toggle[] machineAdaptToggles = new Toggle[4];
        #endregion

        #region 属性输入框
        /// <summary>
        /// 统率输入框
        /// </summary>
        public InputField commandInput;

        /// <summary>
        /// 相性输入框
        /// </summary>
        public InputField phaseInput;

        /// <summary>
        /// 武力输入框
        /// </summary>
        public InputField strengthInput;

        /// <summary>
        /// 忠诚输入框
        /// </summary>
        public InputField loyaltyInput;

        /// <summary>
        /// 智力输入框
        /// </summary>
        public InputField intelligenceInput;

        /// <summary>
        /// 政治输入框
        /// </summary>
        public InputField politicsInput;

        /// <summary>
        /// 魅力输入框
        /// </summary>
        public InputField glamourInput;

        /// <summary>
        /// 功绩输入框
        /// </summary>
        public InputField meritInput;
        #endregion

        #region 特技
        /// <summary>
        /// 特技显示标签
        /// </summary>
        public Text featureLabel;

        /// <summary>
        /// 特技按钮 - 打开特技选择器
        /// </summary>
        public Button featureButton;

        /// <summary>
        /// 取消特技按钮
        /// </summary>
        public Button featureCancelButton;
        #endregion

        #region 亲爱武将 / 厌恶武将
        /// <summary>
        /// 亲爱武将列表显示标签
        /// </summary>
        public Text beloveLabel;

        /// <summary>
        /// 亲爱武将选择按钮
        /// </summary>
        public Button beloveButton;

        /// <summary>
        /// 厌恶武将列表显示标签
        /// </summary>
        public Text hateLabel;

        /// <summary>
        /// 厌恶武将选择按钮
        /// </summary>
        public Button hateButton;
        #endregion

        #region 头像
        /// <summary>
        /// 武将头像
        /// </summary>
        public UIPersonItem personItem;
        #endregion

        #region 决定/返回
        /// <summary>
        /// 决定按钮 - 保存修改
        /// </summary>
        public Button confirmButton;

        /// <summary>
        /// 返回按钮 - 取消修改
        /// </summary>
        public Button cancelButton;
        #endregion

        /// <summary>
        /// 目标武将（原始对象，仅在确认时写入）
        /// </summary>
        public Person Target { get; private set; }

        /// <summary>
        /// 编辑快照 - 所有UI操作仅修改快照值
        /// </summary>
        private PersonEditSnapshot snapshot;

        /// <summary>
        /// 触发适应力切换的标识 - 防止OnValueChanged循环触发
        /// </summary>
        private bool refreshing = false;

        public UIObjectList objectList;
        List<SangoObject> allPersonsDatas;
        #region 窗口生命周期
        /// <summary>
        /// 窗口打开 - 接收目标武将对象并创建编辑快照
        /// </summary>
        /// <param name="objects">参数列表 - objects[0] 为 Person</param>
        public override void OnOpen(params object[] objects)
        {
            // 候选: 所有武将
            allPersonsDatas = new List<SangoObject>();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.personSet != null)
            {
                foreach (Person p in cur.personSet)
                {
                    if (p != null && p.IsValid) allPersonsDatas.Add(p);
                }
            }

            if (objects == null || objects.Length == 0 || objects[0] == null)
            {
                Target = allPersonsDatas[0] as Person;
            }
            else
            {
                Target = objects[0] as Person;
                if (Target == null)
                {
                    Log.Error("UIPersonEdit.OnOpen 传入的对象不是 Person 类型");
                    return;
                }
            }

            //allPersonsDatas.Sort(PersonSortFunction.SortByName.Sort);

            objectList.Init(allPersonsDatas, PersonSortFunction.SortByName, OnSelectEditPerson);
            objectList.SelectDefaultObject(Target);
            // 创建编辑快照 - 从Target拷贝所有可编辑数据
            snapshot = PersonEditSnapshot.FromPerson(Target);

            // 头像显示使用原始Target(头像不会在编辑中改变)
            personItem.SetPerson(Target, 1);

            BindEvents();
            Refresh();
        }

        void OnSelectEditPerson(int index)
        {
            Target = allPersonsDatas[index] as Person;
            snapshot = PersonEditSnapshot.FromPerson(Target);
            personItem.SetPerson(Target, 1);
            Refresh();
        }

        /// <summary>
        /// 窗口关闭 - 清理监听器和引用
        /// </summary>
        public override void OnClose()
        {
            base.OnClose();
            RemoveListeners();
            Target = null;
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 绑定UI事件 - 所有修改操作直接作用于快照
        /// </summary>
        private void BindEvents()
        {
            // 适应力Toggles - 6种兵种 × 4个等级,直接修改快照字段
            BindAdaptToggleGroup(spearAdaptToggles, () => snapshot.spearLv, (v) => snapshot.spearLv = v);
            BindAdaptToggleGroup(halberdAdaptToggles, () => snapshot.halberdLv, (v) => snapshot.halberdLv = v);
            BindAdaptToggleGroup(crossbowAdaptToggles, () => snapshot.crossbowLv, (v) => snapshot.crossbowLv = v);
            BindAdaptToggleGroup(rideAdaptToggles, () => snapshot.rideLv, (v) => snapshot.rideLv = v);
            BindAdaptToggleGroup(waterAdaptToggles, () => snapshot.waterLv, (v) => snapshot.waterLv = v);
            BindAdaptToggleGroup(machineAdaptToggles, () => snapshot.machineLv, (v) => snapshot.machineLv = v);

            // 属性输入框 - 通过SortTitle读取显示值,修改写入快照
            // 五维属性范围: 1-150
            BindSnapshotInputEndEdit(commandInput, PersonSortFunction.SortByCommand, () => snapshot.command, (v) => snapshot.command = v, 1, 150);
            BindSnapshotInputEndEdit(strengthInput, PersonSortFunction.SortByStrength, () => snapshot.strength, (v) => snapshot.strength = v, 1, 150);
            BindSnapshotInputEndEdit(intelligenceInput, PersonSortFunction.SortByIntelligence, () => snapshot.intelligence, (v) => snapshot.intelligence = v, 1, 150);
            BindSnapshotInputEndEdit(politicsInput, PersonSortFunction.SortByPolitics, () => snapshot.politics, (v) => snapshot.politics = v, 1, 150);
            BindSnapshotInputEndEdit(glamourInput, PersonSortFunction.SortByGlamour, () => snapshot.glamour, (v) => snapshot.glamour = v, 1, 150);
            // 相性范围: 0-255
            BindSnapshotInputEndEdit(phaseInput, null, () => snapshot.compatibility, (v) => snapshot.compatibility = v, 0, 255);
            // 忠诚范围: 0-250
            BindSnapshotInputEndEdit(loyaltyInput, PersonSortFunction.SortByLoyalty, () => snapshot.loyalty, (v) => snapshot.loyalty = v, 0, 250);
            // 功绩范围: 0-100000
            BindSnapshotInputEndEdit(meritInput, PersonSortFunction.SortByMerit, () => snapshot.merit, (v) => snapshot.merit = v, 0, 100000);

            // 特技
            if (featureButton != null) featureButton.onClick.AddListener(OnFeatureButtonClick);
            if (featureCancelButton != null) featureCancelButton.onClick.AddListener(OnFeatureCancelClick);

            // 亲爱武将 / 厌恶武将
            if (beloveButton != null) beloveButton.onClick.AddListener(OnBeloveButtonClick);
            if (hateButton != null) hateButton.onClick.AddListener(OnHateButtonClick);

            // 决定 / 返回
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClick);
        }

        /// <summary>
        /// 清理所有按钮监听器
        /// </summary>
        private void RemoveListeners()
        {
            if (featureButton != null) featureButton.onClick.RemoveListener(OnFeatureButtonClick);
            if (featureCancelButton != null) featureCancelButton.onClick.RemoveListener(OnFeatureCancelClick);
            if (beloveButton != null) beloveButton.onClick.RemoveListener(OnBeloveButtonClick);
            if (hateButton != null) hateButton.onClick.RemoveListener(OnHateButtonClick);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClick);
        }

        /// <summary>
        /// 绑定适应力Toggle组 - 同一组内S/A/B/C互斥,修改快照值
        /// </summary>
        /// <param name="toggles">4个Toggle的数组(S/A/B/C)</param>
        /// <param name="getter">从快照读取当前值的函数</param>
        /// <param name="setter">向快照写入新值的函数</param>
        private void BindAdaptToggleGroup(Toggle[] toggles, System.Func<int> getter, System.Action<int> setter)
        {
            if (toggles == null) return;
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] == null) continue;
                int level = i;
                toggles[i].onValueChanged.AddListener((isOn) =>
                {
                    if (refreshing) return;
                    if (isOn)
                    {
                        // 互斥 - 关闭同组其他Toggle
                        for (int j = 0; j < toggles.Length; j++)
                        {
                            if (j != level && toggles[j] != null && toggles[j].isOn)
                                toggles[j].SetIsOnWithoutNotify(false);
                        }
                        // 写入快照 (S=3, A=2, B=1, C=0)
                        setter(3 - level);
                    }
                });
            }
        }

        /// <summary>
        /// 绑定InputField结束编辑事件 - 验证后写入快照
        /// </summary>
        /// <param name="input">输入框</param>
        /// <param name="sortTitle">对应的排序标题(用于验证/回显, 可为null)</param>
        /// <param name="getter">从快照读取值的函数</param>
        /// <param name="setter">向快照写入值的函数</param>
        /// <param name="minValue">取值范围下限</param>
        /// <param name="maxValue">取值范围上限</param>
        private void BindSnapshotInputEndEdit(InputField input, ObjectSortTitle sortTitle,
            System.Func<int> getter, System.Action<int> setter, int minValue = int.MinValue, int maxValue = int.MaxValue)
        {
            if (input == null) return;
            input.onEndEdit.AddListener((text) =>
            {
                if (Target == null) return;
                if (int.TryParse(text, out int v))
                {
                    // 限制数值范围
                    v = System.Math.Max(minValue, System.Math.Min(maxValue, v));
                    setter(v);
                }
                // 回显快照中的当前值
                input.text = getter().ToString();
            });
        }
        #endregion

        #region UI刷新 - 从快照读取数据
        /// <summary>
        /// 刷新窗口 - 将快照当前值同步到UI
        /// </summary>
        public override void OnRefresh()
        {
            if (Target == null) return;

            refreshing = true;
            try
            {
                // 姓名 - 从Target读取(不可编辑, 仅显示)
                if (nameLabel != null) nameLabel.text = Target.Name;

                // 适应力 - 从快照读取
                RefreshAdaptGroup(spearAdaptToggles, snapshot.spearLv);
                RefreshAdaptGroup(halberdAdaptToggles, snapshot.halberdLv);
                RefreshAdaptGroup(crossbowAdaptToggles, snapshot.crossbowLv);
                RefreshAdaptGroup(rideAdaptToggles, snapshot.rideLv);
                RefreshAdaptGroup(waterAdaptToggles, snapshot.waterLv);
                RefreshAdaptGroup(machineAdaptToggles, snapshot.machineLv);

                // 属性输入框 - 从快照读取
                if (commandInput != null) commandInput.text = snapshot.command.ToString();
                if (phaseInput != null) phaseInput.text = snapshot.compatibility.ToString();
                if (strengthInput != null) strengthInput.text = snapshot.strength.ToString();
                if (loyaltyInput != null) loyaltyInput.text = snapshot.loyalty.ToString();
                if (intelligenceInput != null) intelligenceInput.text = snapshot.intelligence.ToString();
                if (politicsInput != null) politicsInput.text = snapshot.politics.ToString();
                if (glamourInput != null) glamourInput.text = snapshot.glamour.ToString();
                if (meritInput != null) meritInput.text = snapshot.merit.ToString();

                // 特技
                RefreshFeature();

                // 亲爱武将 / 厌恶武将 - 从快照读取
                RefreshPersonListLabel(beloveLabel, snapshot.LikePersons, "");
                RefreshPersonListLabel(hateLabel, snapshot.HatePersons, "");
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// 刷新适应力Toggle组 - 根据快照值点亮对应Toggle
        /// S=3→Toggle[0], A=2→Toggle[1], B=1→Toggle[2], C=0→Toggle[3]
        /// </summary>
        /// <param name="toggles">Toggle数组(S/A/B/C)</param>
        /// <param name="level">当前快照值(3=S/2=A/1=B/0=C)</param>
        private void RefreshAdaptGroup(Toggle[] toggles, int level)
        {
            if (toggles == null) return;
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] == null) continue;
                // level 3→i=0, level 2→i=1, level 1→i=2, level 0→i=3
                toggles[i].SetIsOnWithoutNotify(i == 3 - level);
            }
        }

        /// <summary>
        /// 刷新特技显示 - 从快照的EditingFeatures读取,用名字拼接
        /// </summary>
        private void RefreshFeature()
        {
            if (featureLabel == null) return;

            if (snapshot.EditingFeatures == null || snapshot.EditingFeatures.Count == 0)
            {
                featureLabel.text = string.Empty;
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < snapshot.EditingFeatures.Count; i++)
            {
                if (snapshot.EditingFeatures[i] == null) continue;
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(snapshot.EditingFeatures[i].Name);
            }
            featureLabel.text = sb.ToString();
        }

        /// <summary>
        /// 刷新武将列表标签 - 将List&lt;Person&gt;用名字拼接显示
        /// </summary>
        /// <param name="label">显示文本</param>
        /// <param name="list">武将列表</param>
        /// <param name="emptyText">列表为空时显示的提示</param>
        private void RefreshPersonListLabel(Text label, List<Person> list, string emptyText)
        {
            if (label == null) return;
            if (list == null || list.Count == 0)
            {
                label.text = emptyText;
                return;
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(list[i].Name);
            }
            label.text = sb.ToString();
        }
        #endregion

        #region 特技事件
        /// <summary>
        /// 特技按钮点击 - 弹出特技选择器（待实现: FeatureSelectSystem）
        /// </summary>
        public void OnFeatureButtonClick()
        {
            // 占位 - 待实现 FeatureSelectSystem 后替换
            Log.Info("打开特技选择器，目标武将: " + (Target != null ? Target.Name : "null"));
            FeatrueSelectSystem system = GameSystemManager.Instance.GetSystem<FeatrueSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 FeatrueSelectSystem");
                return;
            }

            // 候选: 所有武将(排除自身)
            List<Feature> allPersons = new List<Feature>();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData.Features != null)
            {
                foreach (Feature p in cur.CommonData.Features)
                {
                    if (p != null) allPersons.Add(p);
                }
            }

            //allPersons.Sort(PersonSortFunction.SortByName.Sort);

            // 初始选中 - 从快照列表中过滤出有效武将
            List<Feature> initialSelect = new List<Feature>();
            if (snapshot.EditingFeatures != null)
            {
                foreach (Feature p in snapshot.EditingFeatures)
                {
                    if (p != null) initialSelect.Add(p);
                }
            }

            system.Start(allPersons,
                initialSelect,
                allPersons.Count,
                OnFeatureSelected,
                FeatureSortFunction.DefaultSortList, "全部武将");
        }

        /// <summary>
        /// 亲爱武将选择完成回调 - 更新快照
        /// </summary>
        /// <param name="result">用户选择的武将列表</param>
        private void OnFeatureSelected(List<Feature> result)
        {
            if (Target == null) return;
            snapshot.LikePersons.Clear();
            if (result != null)
            {
                snapshot.EditingFeatures = result;
            }
            Refresh();
        }

        /// <summary>
        /// 取消特技按钮点击 - 清空快照中的特技列表
        /// </summary>
        public void OnFeatureCancelClick()
        {
            snapshot.EditingFeatures.Clear();
            RefreshFeature();
        }
        #endregion

        #region 亲爱/厌恶武将事件
        /// <summary>
        /// 亲爱武将按钮点击 - 打开武将选择器
        /// </summary>
        public void OnBeloveButtonClick()
        {
            OpenPersonSelectForList(snapshot.LikePersons, OnBeloveSelected);
        }

        /// <summary>
        /// 厌恶武将按钮点击 - 打开武将选择器
        /// </summary>
        public void OnHateButtonClick()
        {
            OpenPersonSelectForList(snapshot.HatePersons, OnHateSelected);
        }

        /// <summary>
        /// 通用: 打开武将选择器以编辑指定快照列表
        /// </summary>
        /// <param name="currentList">当前快照中的武将列表</param>
        /// <param name="finishAction">完成回调</param>
        private void OpenPersonSelectForList(List<Person> currentList, System.Action<List<Person>> finishAction)
        {
            if (Target == null) return;
            GameSystem system = GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 PersonSelectSystem");
                return;
            }
            EditPersonSelectSystem select = system as EditPersonSelectSystem;

            // 候选: 所有武将(排除自身)
            List<Person> allPersons = new List<Person>();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.personSet != null)
            {
                foreach (Person p in cur.personSet)
                {
                    if (p != null && p != Target && p.IsValid) allPersons.Add(p);
                }
            }

            allPersons.Sort(PersonSortFunction.SortByName.Sort);

            // 初始选中 - 从快照列表中过滤出有效武将
            List<Person> initialSelect = new List<Person>();
            if (currentList != null)
            {
                foreach (Person p in currentList)
                {
                    if (p != null && p != Target) initialSelect.Add(p);
                }
            }

            select.Start(allPersons,
                initialSelect,
                allPersons.Count,
                finishAction,
                PersonSortFunction.DefaultSortList, "全部武将");
        }

        /// <summary>
        /// 亲爱武将选择完成回调 - 更新快照
        /// </summary>
        /// <param name="result">用户选择的武将列表</param>
        private void OnBeloveSelected(List<Person> result)
        {
            if (Target == null) return;
            snapshot.LikePersons.Clear();
            if (result != null)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i] != null)
                    {
                        snapshot.LikePersons.Add(result[i]);
                    }
                }
            }
            Refresh();
        }

        /// <summary>
        /// 厌恶武将选择完成回调 - 更新快照
        /// </summary>
        /// <param name="result">用户选择的武将列表</param>
        private void OnHateSelected(List<Person> result)
        {
            if (Target == null) return;
            snapshot.HatePersons.Clear();
            if (result != null)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i] != null)
                    {
                        snapshot.HatePersons.Add(result[i]);
                    }
                }
            }
            Refresh();
        }
        #endregion

        #region 决定/返回事件
        /// <summary>
        /// 决定按钮 - 将快照数据同步到Target并关闭窗口
        /// </summary>
        public void OnConfirmClick()
        {
            if (Target == null) return;
            // 将快照所有修改同步回原始Person对象
            snapshot.ApplyTo(Target);
            Log.Info("保存武将编辑: " + Target.Name);
            GameSystemManager.Instance.Back();
        }

        /// <summary>
        /// 返回按钮 - 放弃修改,直接关闭窗口(不写入Target)
        /// </summary>
        public void OnCancelClick()
        {
            Log.Info("取消武将编辑: " + (Target != null ? Target.Name : "null"));
            GameSystemManager.Instance.Back();
        }
        #endregion
    }
}
