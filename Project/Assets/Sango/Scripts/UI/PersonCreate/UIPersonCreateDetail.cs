using Sango;
using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TKNewtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 新建武将详情编辑窗口。
    /// 提供“基本设定”与“能力设定”两个标签页，用于编辑 <see cref="PersonLib"/> 数据。
    /// </summary>
    public class UIPersonCreateDetail : UGUIWindow
    {
        #region 内部快照
        /// <summary>
        /// 编辑快照：保存当前窗口中对 PersonLib 的修改，确认后再写回目标对象。
        /// </summary>
        private class Snapshot
        {
            public string familyName;
            public string giveName;
            public string nickName;
            public string description;
            public int sex;
            public string image;
            public string headIconID;

            public int yearBorn;
            public int yearDead;
            public int yearAvailable;
            public int compatibility;

            public int personality;
            public int argumentation;
            public int voice;
            public int tone;
            public int hanLoyalty;
            public int ideal;
            public int talent;

            public int command;
            public int strength;
            public int intelligence;
            public int politics;
            public int glamour;
            public int attributeChangeType;
            public int attributeDuration;

            public int spearLv;
            public int halberdLv;
            public int crossbowLv;
            public int rideLv;
            public int waterLv;
            public int machineLv;

            public int Father;
            public int Mother;
            public int Brother;
            public int[] SpouseList = new int[0];
            public int[] BrotherList = new int[0];
            public int[] swornBrotherList = new int[0];
            public int[] LikePersonList = new int[0];
            public int[] HatePersonList = new int[0];
            public int[] FeatureList = new int[0];
        }

        /// <summary>
        /// 当前编辑快照
        /// </summary>
        private Snapshot snapshot = new Snapshot();

        /// <summary>
        /// 是否正在刷新 UI，用于避免事件回调造成循环修改。
        /// </summary>
        private bool refreshing = false;
        #endregion

        #region 标签页
        /// <summary>
        /// 基本设定标签页切换
        /// </summary>
        public Toggle basicTabToggle;

        /// <summary>
        /// 能力设定标签页切换
        /// </summary>
        public Toggle abilityTabToggle;

        /// <summary>
        /// 基本设定页面根节点
        /// </summary>
        public GameObject basicPanel;

        /// <summary>
        /// 能力设定页面根节点
        /// </summary>
        public GameObject abilityPanel;
        #endregion

        #region 基本设定 - 头像与姓名
        /// <summary>
        /// 头像显示
        /// </summary>
        public RawImage personImage;

        /// <summary>
        /// 姓输入框
        /// </summary>
        public InputField familyNameInput;

        /// <summary>
        /// 名输入框
        /// </summary>
        public InputField giveNameInput;

        /// <summary>
        /// 字输入框
        /// </summary>
        public InputField nickNameInput;

        /// <summary>
        /// 列传输入框（多行）
        /// </summary>
        public InputField descriptionInput;

        /// <summary>
        /// 性别 Toggle 组（男/女）
        /// </summary>
        public Toggle[] sexToggles = new Toggle[2];

        /// <summary>
        /// 变更容貌按钮
        /// </summary>
        public Button changeImageButton;

        /// <summary>
        /// 造型按钮
        /// </summary>
        public Button modelButton;
        #endregion

        #region 基本设定 - 生卒与寿命
        /// <summary>
        /// 出生年输入框
        /// </summary>
        public InputField yearBornInput;

        /// <summary>
        /// 寿命输入框
        /// </summary>
        public InputField lifeSpanInput;

        /// <summary>
        /// 殁年输入框
        /// </summary>
        public InputField yearDeadInput;

        /// <summary>
        /// 登场年输入框
        /// </summary>
        public InputField yearAvailableInput;
        #endregion

        #region 基本设定 - 性格与相性
        /// <summary>
        /// 性格 Toggle 组（胆小/冷静/刚胆/莽撞）
        /// </summary>
        public Toggle[] personalityToggles = new Toggle[4];

        /// <summary>
        /// 音聲 Toggle 组（胆小/冷静/刚胆/莽撞）
        /// </summary>
        public Toggle[] voiceToggles = new Toggle[4];

        /// <summary>
        /// 語氣 Toggle 组（恭敬/普通/威嚴/自大/蠻族）
        /// </summary>
        public Toggle[] toneToggles = new Toggle[5];

        /// <summary>
        /// 漢室 Toggle 组（無視/普通/重視）
        /// </summary>
        public Toggle[] hanLoyaltyToggles = new Toggle[3];

        /// <summary>
        /// 理想 Toggle 组（霸道/王道/我道/割據/義俠）
        /// </summary>
        public Toggle[] idealToggles = new Toggle[5];

        /// <summary>
        /// 才幹 Toggle 组（王佐/出世/安全/隱遁）
        /// </summary>
        public Toggle[] talentToggles = new Toggle[4];

        /// <summary>
        /// 相性输入框
        /// </summary>
        public InputField compatibilityInput;
        #endregion

        #region 基本设定 - 人际关系
        /// <summary>
        /// 父亲姓名文本
        /// </summary>
        public Text fatherText;

        /// <summary>
        /// 父亲选择按钮
        /// </summary>
        public Button fatherSelectButton;

        /// <summary>
        /// 父亲清除按钮
        /// </summary>
        public Button fatherCancelButton;

        /// <summary>
        /// 母亲姓名文本
        /// </summary>
        public Text motherText;

        public Button motherSelectButton;
        public Button motherCancelButton;

        /// <summary>
        /// 配偶姓名文本（多个以逗号分隔）
        /// </summary>
        public Text spouseText;

        public Button spouseSelectButton;
        public Button spouseCancelButton;

        /// <summary>
        /// 兄弟姓名文本
        /// </summary>
        public Text brotherText;

        public Button brotherSelectButton;
        public Button brotherCancelButton;

        /// <summary>
        /// 义兄弟姓名文本
        /// </summary>
        public Text swornBrotherText;

        public Button swornBrotherSelectButton;
        public Button swornBrotherCancelButton;

        /// <summary>
        /// 亲爱武将姓名文本
        /// </summary>
        public Text likeText;

        public Button likeSelectButton;
        public Button likeCancelButton;

        /// <summary>
        /// 厌恶武将姓名文本
        /// </summary>
        public Text hateText;

        public Button hateSelectButton;
        public Button hateCancelButton;
        #endregion

        #region 能力设定 - 基准能力
        public InputField commandInput;
        public InputField strengthInput;
        public InputField intelligenceInput;
        public InputField politicsInput;
        public InputField glamourInput;
        public Text abilityTotalText;
        #endregion

        #region 能力设定 - 成长与持续
        /// <summary>
        /// 成长期 Toggle 组（維持/早熟/普通/晚成）
        /// </summary>
        public Toggle[] growthToggles = new Toggle[4];

        /// <summary>
        /// 能力持续 Toggle 组（長/短）
        /// </summary>
        public Toggle[] durationToggles = new Toggle[2];
        #endregion

        #region 能力设定 - 兵种适性
        /// <summary>
        /// 枪兵适性 Toggle 组（S/A/B/C）
        /// </summary>
        public Toggle[] spearAdaptToggles = new Toggle[4];

        /// <summary>
        /// 戟兵适性 Toggle 组（S/A/B/C）
        /// </summary>
        public Toggle[] halberdAdaptToggles = new Toggle[4];

        /// <summary>
        /// 弩兵适性 Toggle 组（S/A/B/C）
        /// </summary>
        public Toggle[] crossbowAdaptToggles = new Toggle[4];

        /// <summary>
        /// 骑兵适性 Toggle 组（S/A/B/C）
        /// </summary>
        public Toggle[] rideAdaptToggles = new Toggle[4];

        /// <summary>
        /// 水军适性 Toggle 组（S/A/B/C）
        /// </summary>
        public Toggle[] waterAdaptToggles = new Toggle[4];

        /// <summary>
        /// 器械适性 Toggle 组（S/A/B/C）
        /// </summary>
        public Toggle[] machineAdaptToggles = new Toggle[4];
        #endregion

        #region 能力设定 - 特技
        /// <summary>
        /// 特技显示文本
        /// </summary>
        public Text featureText;

        /// <summary>
        /// 特技选择按钮
        /// </summary>
        public Button featureButton;

        /// <summary>
        /// 攻心按钮（占位）
        /// </summary>
        public Button specialFeatureButton;

        /// <summary>
        /// 清除特技按钮
        /// </summary>
        public Button featureCancelButton;
        #endregion

        #region 底部按钮
        public Button confirmButton;
        public Button backButton;
        public Button cancelButton;
        #endregion

        #region 生命周期
        protected override void Awake()
        {
            base.Awake();
            BindAll();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            InitSnapshot();
            SwitchTab(true);
            RefreshAll();
        }
        #endregion

        #region 快照初始化与回写
        /// <summary>
        /// 从 <see cref="GameCustomEdit.TargetEditPerson"/> 初始化快照；
        /// 若目标为空，则创建空白新建武将。
        /// </summary>
        private void InitSnapshot()
        {
            PersonLib target = GameCustomEdit.Instance != null ? GameCustomEdit.Instance.TargetEditPerson : null;
            if (target == null)
            {
                snapshot = new Snapshot();
                return;
            }

            snapshot = new Snapshot
            {
                familyName = target.familyName ?? string.Empty,
                giveName = target.giveName ?? string.Empty,
                nickName = target.nickName ?? string.Empty,
                description = target.description ?? string.Empty,
                sex = target.sex,
                image = target.image ?? string.Empty,
                headIconID = target.headIconID,

                yearBorn = target.yearBorn,
                yearDead = target.yearDead,
                yearAvailable = target.yearAvailable,
                compatibility = target.compatibility,

                personality = target.personality,
                argumentation = target.argumentation,
                voice = target.voice,
                tone = target.tone,
                hanLoyalty = target.hanLoyalty,
                ideal = target.ideal,
                talent = target.talent,

                command = target.command,
                strength = target.strength,
                intelligence = target.intelligence,
                politics = target.politics,
                glamour = target.glamour,
                attributeChangeType = target.attributeChangeType,
                attributeDuration = target.attributeDuration,

                spearLv = target.spearLv,
                halberdLv = target.halberdLv,
                crossbowLv = target.crossbowLv,
                rideLv = target.rideLv,
                waterLv = target.waterLv,
                machineLv = target.machineLv,

                Father = target.Father,
                Mother = target.Mother,
                Brother = target.Brother,
                SpouseList = CloneArray(target.SpouseList),
                BrotherList = CloneArray(target.BrotherList),
                swornBrotherList = CloneArray(target.swornBrotherList),
                LikePersonList = CloneArray(target.LikePersonList),
                HatePersonList = CloneArray(target.HatePersonList),
                FeatureList = CloneArray(target.FeatureList)
            };
        }

        /// <summary>
        /// 将快照数据写回目标 PersonLib，并存入自建武将列表。
        /// </summary>
        private void ApplySnapshotToTarget()
        {
            PersonLib target = GameCustomEdit.Instance != null ? GameCustomEdit.Instance.TargetEditPerson : null;
            bool isNew = target == null;
            if (isNew)
            {
                target = new PersonLib();
                target.Id = GenerateNewPersonLibId();
                if (GameCustomEdit.Instance != null)
                    GameCustomEdit.Instance.TargetEditPerson = target;
            }

            target.familyName = snapshot.familyName;
            target.giveName = snapshot.giveName;
            target.nickName = snapshot.nickName;
            target.description = snapshot.description;
            target.sex = snapshot.sex;
            target.image = snapshot.image;
            target.headIconID = snapshot.headIconID;

            target.yearBorn = snapshot.yearBorn;
            target.yearDead = snapshot.yearDead;
            target.yearAvailable = snapshot.yearAvailable;
            target.compatibility = snapshot.compatibility;

            target.personality = snapshot.personality;
            target.argumentation = snapshot.argumentation;
            target.voice = snapshot.voice;
            target.tone = snapshot.tone;
            target.hanLoyalty = snapshot.hanLoyalty;
            target.ideal = snapshot.ideal;
            target.talent = snapshot.talent;

            target.command = snapshot.command;
            target.strength = snapshot.strength;
            target.intelligence = snapshot.intelligence;
            target.politics = snapshot.politics;
            target.glamour = snapshot.glamour;
            target.attributeChangeType = snapshot.attributeChangeType;
            target.attributeDuration = snapshot.attributeDuration;

            target.spearLv = snapshot.spearLv;
            target.halberdLv = snapshot.halberdLv;
            target.crossbowLv = snapshot.crossbowLv;
            target.rideLv = snapshot.rideLv;
            target.waterLv = snapshot.waterLv;
            target.machineLv = snapshot.machineLv;

            target.Father = snapshot.Father;
            target.Mother = snapshot.Mother;
            target.Brother = snapshot.Brother;
            target.SpouseList = CloneArray(snapshot.SpouseList);
            target.BrotherList = CloneArray(snapshot.BrotherList);
            target.swornBrotherList = CloneArray(snapshot.swornBrotherList);
            target.LikePersonList = CloneArray(snapshot.LikePersonList);
            target.HatePersonList = CloneArray(snapshot.HatePersonList);
            target.FeatureList = CloneArray(snapshot.FeatureList);

            if (GameCustomEdit.Instance != null && GameCustomEdit.Instance.ScenarioAddon != null)
            {
                GameCustomEdit.Instance.ScenarioAddon.PersonAddonMap.Set(target);
                SaveScenarioAddon();
            }
        }

        /// <summary>
        /// 生成一个新的不重复 PersonLib Id。
        /// </summary>
        private int GenerateNewPersonLibId()
        {
            int maxId = 0;
            ScenarioAddon addon = GameCustomEdit.Instance != null ? GameCustomEdit.Instance.ScenarioAddon : null;
            if (addon != null && addon.PersonAddonMap != null)
            {
                addon.PersonAddonMap.ForEach(p =>
                {
                    if (p != null && p.Id > maxId) maxId = p.Id;
                });
            }
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.personSet != null)
            {
                cur.personSet.ForEach(p =>
                {
                    if (p != null && p.Id > maxId) maxId = p.Id;
                });
            }
            return maxId + 1;
        }

        /// <summary>
        /// 将当前自建武将数据序列化保存到本地文件。
        /// </summary>
        private void SaveScenarioAddon()
        {
            ScenarioAddon addon = GameCustomEdit.Instance != null ? GameCustomEdit.Instance.ScenarioAddon : null;
            if (addon == null) return;
            string path = Sango.Path.SaveRootPath + "/CustomEdit/CustomPerson.json";
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            string json = JsonConvert.SerializeObject(addon, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        private int[] CloneArray(int[] source)
        {
            if (source == null) return new int[0];
            return (int[])source.Clone();
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 绑定所有 UI 事件。
        /// </summary>
        private void BindAll()
        {
            // 标签页 - Toggle 互斥切换
            if (basicTabToggle != null)
                basicTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) SwitchTab(true); });
            if (abilityTabToggle != null)
                abilityTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) SwitchTab(false); });

            // 姓名与列传
            BindTextInput(familyNameInput, () => snapshot.familyName, v => snapshot.familyName = v);
            BindTextInput(giveNameInput, () => snapshot.giveName, v => snapshot.giveName = v);
            BindTextInput(nickNameInput, () => snapshot.nickName, v => snapshot.nickName = v);
            BindTextInput(descriptionInput, () => snapshot.description, v => snapshot.description = v);

            // 性别（0=男，1=女）
            BindToggleGroup(sexToggles, () => snapshot.sex, v => snapshot.sex = v, i => i, v => v);

            // 生卒年
            BindIntInput(yearBornInput, () => snapshot.yearBorn, v => snapshot.yearBorn = v, 1, 9999, OnLifeYearChanged);
            BindIntInput(yearDeadInput, () => snapshot.yearDead, v => snapshot.yearDead = v, 1, 9999, OnLifeYearChanged);
            BindIntInput(yearAvailableInput, () => snapshot.yearAvailable, v => snapshot.yearAvailable = v, 1, 9999);

            // 性格与相性
            BindToggleGroup(personalityToggles, () => snapshot.personality, v => snapshot.personality = v, i => i + 1, v => v - 1);
            BindToggleGroup(voiceToggles, () => snapshot.voice, v => snapshot.voice = v, i => i, v => v);
            BindToggleGroup(toneToggles, () => snapshot.tone, v => snapshot.tone = v, i => i, v => v);
            BindToggleGroup(hanLoyaltyToggles, () => snapshot.hanLoyalty, v => snapshot.hanLoyalty = v, i => i, v => v);
            BindToggleGroup(idealToggles, () => snapshot.ideal, v => snapshot.ideal = v, i => i, v => v);
            BindToggleGroup(talentToggles, () => snapshot.talent, v => snapshot.talent = v, i => i, v => v);
            BindIntInput(compatibilityInput, () => snapshot.compatibility, v => snapshot.compatibility = v, 0, 255);

            // 能力
            BindIntInput(commandInput, () => snapshot.command, v => snapshot.command = v, 1, 150, OnAbilityChanged);
            BindIntInput(strengthInput, () => snapshot.strength, v => snapshot.strength = v, 1, 150, OnAbilityChanged);
            BindIntInput(intelligenceInput, () => snapshot.intelligence, v => snapshot.intelligence = v, 1, 150, OnAbilityChanged);
            BindIntInput(politicsInput, () => snapshot.politics, v => snapshot.politics = v, 1, 150, OnAbilityChanged);
            BindIntInput(glamourInput, () => snapshot.glamour, v => snapshot.glamour = v, 1, 150, OnAbilityChanged);

            // 成长与持续
            BindGrowthToggleGroup();
            BindToggleGroup(durationToggles, () => snapshot.attributeDuration, v => snapshot.attributeDuration = v, i => i, v => v);

            // 兵种适性（S=3, A=2, B=1, C=0）
            BindAdaptToggleGroup(spearAdaptToggles, () => snapshot.spearLv, v => snapshot.spearLv = v);
            BindAdaptToggleGroup(halberdAdaptToggles, () => snapshot.halberdLv, v => snapshot.halberdLv = v);
            BindAdaptToggleGroup(crossbowAdaptToggles, () => snapshot.crossbowLv, v => snapshot.crossbowLv = v);
            BindAdaptToggleGroup(rideAdaptToggles, () => snapshot.rideLv, v => snapshot.rideLv = v);
            BindAdaptToggleGroup(waterAdaptToggles, () => snapshot.waterLv, v => snapshot.waterLv = v);
            BindAdaptToggleGroup(machineAdaptToggles, () => snapshot.machineLv, v => snapshot.machineLv = v);

            // 人际关系
            BindRelationshipButton(fatherSelectButton, false, OnFatherSelected);
            BindRelationshipButton(fatherCancelButton, () => snapshot.Father = 0, RefreshFather);
            BindRelationshipButton(motherSelectButton, false, OnMotherSelected);
            BindRelationshipButton(motherCancelButton, () => snapshot.Mother = 0, RefreshMother);
            BindRelationshipButton(spouseSelectButton, true, OnSpouseSelected);
            BindRelationshipButton(spouseCancelButton, () => snapshot.SpouseList = new int[0], RefreshSpouse);
            BindRelationshipButton(brotherSelectButton, false, OnBrotherSelected);
            BindRelationshipButton(brotherCancelButton, () => snapshot.Brother = 0, RefreshBrother);
            BindRelationshipButton(swornBrotherSelectButton, true, OnSwornBrotherSelected);
            BindRelationshipButton(swornBrotherCancelButton, () => snapshot.swornBrotherList = new int[0], RefreshSwornBrother);
            BindRelationshipButton(likeSelectButton, true, OnLikeSelected);
            BindRelationshipButton(likeCancelButton, () => snapshot.LikePersonList = new int[0], RefreshLike);
            BindRelationshipButton(hateSelectButton, true, OnHateSelected);
            BindRelationshipButton(hateCancelButton, () => snapshot.HatePersonList = new int[0], RefreshHate);

            // 特技
            if (featureButton != null) featureButton.onClick.AddListener(OnFeatureButtonClick);
            if (featureCancelButton != null) featureCancelButton.onClick.AddListener(OnFeatureCancelClick);
            if (specialFeatureButton != null) specialFeatureButton.onClick.AddListener(OnSpecialFeatureClick);

            // 头像与造型
            if (changeImageButton != null) changeImageButton.onClick.AddListener(OnChangeImageClick);
            if (modelButton != null) modelButton.onClick.AddListener(OnModelClick);

            // 底部按钮
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClick);
            if (backButton != null) backButton.onClick.AddListener(OnBackClick);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClick);
        }
        #endregion

        #region UI 刷新
        /// <summary>
        /// 刷新整个窗口：将快照数据同步到所有 UI 组件。
        /// </summary>
        private void RefreshAll()
        {
            refreshing = true;
            try
            {
                RefreshImage();

                if (familyNameInput != null) familyNameInput.text = snapshot.familyName;
                if (giveNameInput != null) giveNameInput.text = snapshot.giveName;
                if (nickNameInput != null) nickNameInput.text = snapshot.nickName;
                if (descriptionInput != null) descriptionInput.text = snapshot.description;

                RefreshToggleGroup(sexToggles, snapshot.sex, i => i, 0);

                if (yearBornInput != null) yearBornInput.text = snapshot.yearBorn.ToString();
                if (yearDeadInput != null) yearDeadInput.text = snapshot.yearDead.ToString();
                if (yearAvailableInput != null) yearAvailableInput.text = snapshot.yearAvailable.ToString();
                if (lifeSpanInput != null) lifeSpanInput.text = System.Math.Max(0, snapshot.yearDead - snapshot.yearBorn).ToString();

                RefreshToggleGroup(personalityToggles, snapshot.personality, i => i + 1, 1);
                RefreshToggleGroup(voiceToggles, snapshot.voice, i => i, 0);
                RefreshToggleGroup(toneToggles, snapshot.tone, i => i, 0);
                RefreshToggleGroup(hanLoyaltyToggles, snapshot.hanLoyalty, i => i, 0);
                RefreshToggleGroup(idealToggles, snapshot.ideal, i => i, 0);
                RefreshToggleGroup(talentToggles, snapshot.talent, i => i, 0);

                if (compatibilityInput != null) compatibilityInput.text = snapshot.compatibility.ToString();

                if (commandInput != null) commandInput.text = snapshot.command.ToString();
                if (strengthInput != null) strengthInput.text = snapshot.strength.ToString();
                if (intelligenceInput != null) intelligenceInput.text = snapshot.intelligence.ToString();
                if (politicsInput != null) politicsInput.text = snapshot.politics.ToString();
                if (glamourInput != null) glamourInput.text = snapshot.glamour.ToString();
                OnAbilityChanged();

                RefreshGrowthToggleGroup();
                RefreshToggleGroup(durationToggles, snapshot.attributeDuration, i => i, 0);

                RefreshAdaptGroup(spearAdaptToggles, snapshot.spearLv);
                RefreshAdaptGroup(halberdAdaptToggles, snapshot.halberdLv);
                RefreshAdaptGroup(crossbowAdaptToggles, snapshot.crossbowLv);
                RefreshAdaptGroup(rideAdaptToggles, snapshot.rideLv);
                RefreshAdaptGroup(waterAdaptToggles, snapshot.waterLv);
                RefreshAdaptGroup(machineAdaptToggles, snapshot.machineLv);

                RefreshFather();
                RefreshMother();
                RefreshSpouse();
                RefreshBrother();
                RefreshSwornBrother();
                RefreshLike();
                RefreshHate();
                RefreshFeature();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// 切换标签页并同步 Toggle 状态。
        /// </summary>
        /// <param name="isBasic">true=基本设定，false=能力设定</param>
        private void SwitchTab(bool isBasic)
        {
            if (basicPanel != null) basicPanel.SetActive(isBasic);
            if (abilityPanel != null) abilityPanel.SetActive(!isBasic);
            // 同步 Toggle 显示状态
            if (basicTabToggle != null && basicTabToggle.isOn != isBasic)
                basicTabToggle.SetIsOnWithoutNotify(isBasic);
            if (abilityTabToggle != null && abilityTabToggle.isOn == isBasic)
                abilityTabToggle.SetIsOnWithoutNotify(!isBasic);
        }

        /// <summary>
        /// 刷新头像显示。
        /// </summary>
        private void RefreshImage()
        {
            if (personImage == null) return;
            if (!string.IsNullOrEmpty(snapshot.image))
            {
                Texture2D tex = Resources.Load<Texture2D>(snapshot.image);
                if (tex != null)
                    personImage.texture = tex;
            }
        }

        /// <summary>
        /// 能力合计变化时刷新显示。
        /// </summary>
        private void OnAbilityChanged()
        {
            if (abilityTotalText != null)
            {
                int total = snapshot.command + snapshot.strength + snapshot.intelligence + snapshot.politics + snapshot.glamour;
                abilityTotalText.text = total.ToString();
            }
        }

        /// <summary>
        /// 生卒年变化时刷新寿命显示。
        /// </summary>
        private void OnLifeYearChanged()
        {
            if (lifeSpanInput != null)
                lifeSpanInput.text = System.Math.Max(0, snapshot.yearDead - snapshot.yearBorn).ToString();
        }
        #endregion

        #region 通用绑定助手
        /// <summary>
        /// 绑定文本输入框：结束编辑时直接写入快照。
        /// </summary>
        private void BindTextInput(InputField input, Func<string> getter, Action<string> setter)
        {
            if (input == null) return;
            input.onEndEdit.AddListener((text) =>
            {
                if (refreshing) return;
                setter(text ?? string.Empty);
                if (input != null) input.text = getter();
            });
        }

        /// <summary>
        /// 绑定整数输入框：结束编辑时验证范围并写入快照，支持附加回调。
        /// </summary>
        private void BindIntInput(InputField input, Func<int> getter, Action<int> setter, int minValue, int maxValue, Action onChanged = null)
        {
            if (input == null) return;
            input.onEndEdit.AddListener((text) =>
            {
                if (refreshing) return;
                if (int.TryParse(text, out int v))
                {
                    v = System.Math.Max(minValue, System.Math.Min(maxValue, v));
                    setter(v);
                    onChanged?.Invoke();
                }
                if (input != null) input.text = getter().ToString();
            });
        }

        /// <summary>
        /// 绑定通用 Toggle 组：同一组内互斥，选中时按 indexToValue 写入快照。
        /// </summary>
        private void BindToggleGroup(Toggle[] toggles, Func<int> getter, Action<int> setter,
            Func<int, int> indexToValue, Func<int, int> valueToIndex)
        {
            if (toggles == null) return;
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] == null) continue;
                int index = i;
                toggles[i].onValueChanged.AddListener((isOn) =>
                {
                    if (refreshing) return;
                    if (isOn)
                    {
                        for (int j = 0; j < toggles.Length; j++)
                        {
                            if (j != index && toggles[j] != null && toggles[j].isOn)
                                toggles[j].SetIsOnWithoutNotify(false);
                        }
                        setter(indexToValue(index));
                    }
                });
            }
        }

        /// <summary>
        /// 刷新通用 Toggle 组：根据快照值点亮对应 Toggle。
        /// </summary>
        private void RefreshToggleGroup(Toggle[] toggles, int value, Func<int, int> valueToIndex, int defaultValue)
        {
            if (toggles == null) return;
            int index = valueToIndex(value);
            if (index < 0 || index >= toggles.Length)
                index = valueToIndex(defaultValue);
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] == null) continue;
                toggles[i].SetIsOnWithoutNotify(i == index);
            }
        }
        #endregion

        #region 兵种适性绑定
        /// <summary>
        /// 绑定兵种适性 Toggle 组（S=3, A=2, B=1, C=0）。
        /// </summary>
        private void BindAdaptToggleGroup(Toggle[] toggles, Func<int> getter, Action<int> setter)
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
                        for (int j = 0; j < toggles.Length; j++)
                        {
                            if (j != level && toggles[j] != null && toggles[j].isOn)
                                toggles[j].SetIsOnWithoutNotify(false);
                        }
                        setter(3 - level);
                    }
                });
            }
        }

        /// <summary>
        /// 刷新兵种适性 Toggle 组。
        /// </summary>
        private void RefreshAdaptGroup(Toggle[] toggles, int level)
        {
            if (toggles == null) return;
            int index = 3 - level;
            if (index < 0 || index >= toggles.Length) index = 3;
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] == null) continue;
                toggles[i].SetIsOnWithoutNotify(i == index);
            }
        }
        #endregion

        #region 成长期绑定
        /// <summary>
        /// 绑定成长期 Toggle 组（維持/早熟/普通/晚成）。
        /// 与 AttributeChangeType 名称对应：維持→持续型，早熟→早熟型，普通→普通型，晚成→晚成型。
        /// </summary>
        private void BindGrowthToggleGroup()
        {
            if (growthToggles == null) return;
            for (int i = 0; i < growthToggles.Length; i++)
            {
                if (growthToggles[i] == null) continue;
                int index = i;
                growthToggles[i].onValueChanged.AddListener((isOn) =>
                {
                    if (refreshing) return;
                    if (isOn)
                    {
                        for (int j = 0; j < growthToggles.Length; j++)
                        {
                            if (j != index && growthToggles[j] != null && growthToggles[j].isOn)
                                growthToggles[j].SetIsOnWithoutNotify(false);
                        }
                        snapshot.attributeChangeType = GetAttributeChangeTypeIdByIndex(index);
                    }
                });
            }
        }

        /// <summary>
        /// 刷新成长期 Toggle 组。
        /// </summary>
        private void RefreshGrowthToggleGroup()
        {
            if (growthToggles == null) return;
            int index = GetAttributeChangeTypeIndexById(snapshot.attributeChangeType);
            for (int i = 0; i < growthToggles.Length; i++)
            {
                if (growthToggles[i] == null) continue;
                growthToggles[i].SetIsOnWithoutNotify(i == index);
            }
        }

        /// <summary>
        /// 将 Toggle 索引映射为 AttributeChangeType Id。
        /// </summary>
        private int GetAttributeChangeTypeIdByIndex(int index)
        {
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData != null && cur.CommonData.AttributeChangeTypes != null)
            {
                string[] names = { "持续型", "早熟型", "普通型", "晚成型" };
                if (index >= 0 && index < names.Length)
                {
                    foreach (AttributeChangeType t in cur.CommonData.AttributeChangeTypes)
                    {
                        if (t != null && t.Name == names[index])
                            return t.Id;
                    }
                }
            }
            return index + 1;
        }

        /// <summary>
        /// 将 AttributeChangeType Id 映射为 Toggle 索引。
        /// </summary>
        private int GetAttributeChangeTypeIndexById(int id)
        {
            Scenario cur = Scenario.Cur;
            AttributeChangeType type = null;
            if (cur != null && cur.CommonData != null && cur.CommonData.AttributeChangeTypes != null)
                type = cur.CommonData.AttributeChangeTypes.Get(id);
            if (type == null) return 0;
            string[] names = { "持续型", "早熟型", "普通型", "晚成型" };
            for (int i = 0; i < names.Length; i++)
            {
                if (type.Name == names[i]) return i;
            }
            return 0;
        }
        #endregion

        #region 人际关系
        /// <summary>
        /// 绑定人际关系按钮。
        /// </summary>
        /// <param name="button">按钮</param>
        /// <param name="isMultiSelect">是否为多选</param>
        /// <param name="onSelected">选择完成回调（多选时参数有效）</param>
        private void BindRelationshipButton(Button button, bool isMultiSelect, Action<List<Person>> onSelected)
        {
            if (button == null) return;
            button.onClick.AddListener(() => OpenPersonSelect(isMultiSelect, onSelected));
        }

        /// <summary>
        /// 绑定清除型人际关系按钮。
        /// </summary>
        private void BindRelationshipButton(Button button, Action clearAction, Action refreshAction)
        {
            if (button == null) return;
            button.onClick.AddListener(() =>
            {
                if (refreshing) return;
                clearAction?.Invoke();
                refreshAction?.Invoke();
            });
        }

        /// <summary>
        /// 打开武将选择器。
        /// </summary>
        private void OpenPersonSelect(bool isMultiSelect, Action<List<Person>> onSelected)
        {
            GameSystem system = GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 EditPersonSelectSystem");
                return;
            }
            EditPersonSelectSystem select = system as EditPersonSelectSystem;

            List<Person> allPersons = new List<Person>();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.personSet != null)
            {
                foreach (Person p in cur.personSet)
                {
                    if (p != null && p.IsValid) allPersons.Add(p);
                }
            }
            allPersons.Sort((a, b) => PersonSortFunction.SortByName.personSortFunc(a, b));

            select.Start(allPersons,
                new List<Person>(),
                isMultiSelect ? allPersons.Count : 1,
                onSelected,
                PersonSortFunction.DefaultSortList, "全部武将");
        }

        private void OnFatherSelected(List<Person> result)
        {
            if (result != null && result.Count > 0) snapshot.Father = result[0].Id;
            RefreshFather();
        }

        private void OnMotherSelected(List<Person> result)
        {
            if (result != null && result.Count > 0) snapshot.Mother = result[0].Id;
            RefreshMother();
        }

        private void OnBrotherSelected(List<Person> result)
        {
            if (result != null && result.Count > 0) snapshot.Brother = result[0].Id;
            RefreshBrother();
        }

        private void OnSpouseSelected(List<Person> result)
        {
            snapshot.SpouseList = ConvertToIds(result);
            RefreshSpouse();
        }

        private void OnSwornBrotherSelected(List<Person> result)
        {
            snapshot.swornBrotherList = ConvertToIds(result);
            RefreshSwornBrother();
        }

        private void OnLikeSelected(List<Person> result)
        {
            snapshot.LikePersonList = ConvertToIds(result);
            RefreshLike();
        }

        private void OnHateSelected(List<Person> result)
        {
            snapshot.HatePersonList = ConvertToIds(result);
            RefreshHate();
        }

        private int[] ConvertToIds(List<Person> persons)
        {
            if (persons == null) return new int[0];
            return persons.Where(p => p != null).Select(p => p.Id).Distinct().ToArray();
        }

        private void RefreshFather()
        {
            SetPersonNameText(fatherText, snapshot.Father);
        }

        private void RefreshMother()
        {
            SetPersonNameText(motherText, snapshot.Mother);
        }

        private void RefreshBrother()
        {
            SetPersonNameText(brotherText, snapshot.Brother);
        }

        private void RefreshSpouse()
        {
            SetPersonNamesText(spouseText, snapshot.SpouseList);
        }

        private void RefreshSwornBrother()
        {
            SetPersonNamesText(swornBrotherText, snapshot.swornBrotherList);
        }

        private void RefreshLike()
        {
            SetPersonNamesText(likeText, snapshot.LikePersonList);
        }

        private void RefreshHate()
        {
            SetPersonNamesText(hateText, snapshot.HatePersonList);
        }

        /// <summary>
        /// 设置单个武将名称文本。
        /// </summary>
        private void SetPersonNameText(Text text, int personId)
        {
            if (text == null) return;
            text.text = GetPersonName(personId);
        }

        /// <summary>
        /// 设置多个武将名称文本，以逗号分隔。
        /// </summary>
        private void SetPersonNamesText(Text text, int[] personIds)
        {
            if (text == null) return;
            if (personIds == null || personIds.Length == 0)
            {
                text.text = string.Empty;
                return;
            }
            text.text = string.Join(", ", personIds.Select(id => GetPersonName(id)).ToArray());
        }

        /// <summary>
        /// 根据武将 Id 获取名称。
        /// </summary>
        private string GetPersonName(int personId)
        {
            if (personId <= 0) return string.Empty;
            Scenario cur = Scenario.Cur;
            Person p = null;
            if (cur != null && cur.personSet != null)
                p = cur.personSet.Get(personId);
            if (p == null && GameCustomEdit.Instance != null && GameCustomEdit.Instance.ScenarioAddon != null)
            {
                PersonLib lib = GameCustomEdit.Instance.ScenarioAddon.PersonAddonMap.Get(personId);
                if (lib != null) return lib.Name;
            }
            return p != null ? p.Name : personId.ToString();
        }
        #endregion

        #region 特技
        /// <summary>
        /// 特技按钮点击：打开特技选择器。
        /// </summary>
        private void OnFeatureButtonClick()
        {
            GameSystem system = GameSystemManager.Instance.GetSystem<FeatrueSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 FeatrueSelectSystem");
                return;
            }
            FeatrueSelectSystem select = system as FeatrueSelectSystem;

            List<Feature> allFeatures = new List<Feature>();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData != null && cur.CommonData.Features != null)
            {
                foreach (Feature f in cur.CommonData.Features)
                {
                    if (f != null) allFeatures.Add(f);
                }
            }

            List<Feature> initialSelect = new List<Feature>();
            if (snapshot.FeatureList != null)
            {
                foreach (int id in snapshot.FeatureList)
                {
                    Feature f = cur != null && cur.CommonData != null && cur.CommonData.Features != null
                        ? cur.CommonData.Features.Get(id)
                        : null;
                    if (f != null) initialSelect.Add(f);
                }
            }

            select.Start(allFeatures,
                initialSelect,
                allFeatures.Count,
                OnFeatureSelected,
                FeatureSortFunction.DefaultSortList, "全部特技");
        }

        private void OnFeatureSelected(List<Feature> result)
        {
            snapshot.FeatureList = result != null
                ? result.Where(f => f != null).Select(f => f.Id).Distinct().ToArray()
                : new int[0];
            RefreshFeature();
        }

        private void OnFeatureCancelClick()
        {
            snapshot.FeatureList = new int[0];
            RefreshFeature();
        }

        private void OnSpecialFeatureClick()
        {
            Log.Info("攻心按钮占位，待后续扩展");
        }

        private void RefreshFeature()
        {
            if (featureText == null) return;
            if (snapshot.FeatureList == null || snapshot.FeatureList.Length == 0)
            {
                featureText.text = string.Empty;
                return;
            }
            Scenario cur = Scenario.Cur;
            List<string> names = new List<string>();
            foreach (int id in snapshot.FeatureList)
            {
                Feature f = cur != null && cur.CommonData != null && cur.CommonData.Features != null
                    ? cur.CommonData.Features.Get(id)
                    : null;
                names.Add(f != null ? f.Name : id.ToString());
            }
            featureText.text = string.Join(", ", names.ToArray());
        }
        #endregion

        #region 头像与造型
        private void OnChangeImageClick()
        {
            Log.Info("打开头像选择窗口");
            Window.Instance.Open("window_create_person_image");
        }

        private void OnModelClick()
        {
            Log.Info("造型按钮占位，待后续扩展");
        }
        #endregion

        #region 底部按钮事件
        private void OnConfirmClick()
        {
            ApplySnapshotToTarget();
            Log.Info("新建武将已保存：" + snapshot.familyName + snapshot.giveName);
            GameSystemManager.Instance.Back();
        }

        private void OnBackClick()
        {
            GameSystemManager.Instance.Back();
        }

        private void OnCancelClick()
        {
            GameSystemManager.Instance.Back();
        }
        #endregion

    }
}
