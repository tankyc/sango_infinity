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
        public class Snapshot
        {
            public string familyName;
            public string giveName;
            public string nickName;
            public string description;
            public int sex;
            public string image;
            public int headIconID;

            public int yearBorn;
            public int yearDead;
            public int yearAvailable;
            public int compatibility;

            public int personality;
            public int argumentation;
            public int voice;
            public int tone;
            public int kanshitsu;
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
        /// 出生年按钮（点击调用 UICalculator）
        /// </summary>
        public Button yearBornButton;

        /// <summary>
        /// 出生年显示文本
        /// </summary>
        public Text yearBornText;

        /// <summary>
        /// 寿命按钮（点击调用 UICalculator）
        /// </summary>
        public Button lifeSpanButton;

        /// <summary>
        /// 寿命显示文本
        /// </summary>
        public Text lifeSpanText;

        /// <summary>
        /// 殁年显示文本（出生年+寿命计算得出）
        /// </summary>
        public Text yearDeadText;

        /// <summary>
        /// 登场年显示文本
        /// </summary>
        public Text yearAvailableText;
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
        //public InputField compatibilityInput;
        public Text compatibilityText;
        public Button compatibilitySelectButton;
        public Button compatibilityCancelButton;
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
        //public Button brotherCancelButton;

        /// <summary>
        /// 义兄弟姓名文本
        /// </summary>
        //public Text swornBrotherText;

        //public Button swornBrotherSelectButton;
        //public Button swornBrotherCancelButton;

        /// <summary>
        /// 亲爱武将姓名文本
        /// </summary>
        public Text likeText;

        public Button likeSelectButton;
        //public Button likeCancelButton;

        /// <summary>
        /// 厌恶武将姓名文本
        /// </summary>
        public Text hateText;

        public Button hateSelectButton;
        //public Button hateCancelButton;

        public InputField biographyInput;


        #endregion

        #region 能力设定 - 基准能力
        /// <summary>
        /// 统率值按钮（点击调用 UICalculator）
        /// </summary>
        public Button commandButton;

        /// <summary>
        /// 统率值显示文本
        /// </summary>
        public Text commandText;

        /// <summary>
        /// 武力值按钮（点击调用 UICalculator）
        /// </summary>
        public Button strengthButton;

        /// <summary>
        /// 武力值显示文本
        /// </summary>
        public Text strengthText;

        /// <summary>
        /// 智力值按钮（点击调用 UICalculator）
        /// </summary>
        public Button intelligenceButton;

        /// <summary>
        /// 智力值显示文本
        /// </summary>
        public Text intelligenceText;

        /// <summary>
        /// 政治值按钮（点击调用 UICalculator）
        /// </summary>
        public Button politicsButton;

        /// <summary>
        /// 政治值显示文本
        /// </summary>
        public Text politicsText;

        /// <summary>
        /// 魅力值按钮（点击调用 UICalculator）
        /// </summary>
        public Button glamourButton;

        /// <summary>
        /// 魅力值显示文本
        /// </summary>
        public Text glamourText;

        /// <summary>
        /// 能力合计显示文本
        /// </summary>
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

        public override void OnOpen(params object[] objs)
        {
            base.OnOpen(objs);
            //InitSnapshot();
            snapshot = (Snapshot)objs[0];
            RefreshConfirmButton();
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
                snapshot.headIconID = GameCustomEdit.Instance.headDataList[0];
                // 设置默认值
                snapshot.yearBorn = 190;
                snapshot.yearDead = 289; // 190 + 99
                snapshot.yearAvailable = 190;
                snapshot.command = 50;
                snapshot.strength = 50;
                snapshot.intelligence = 50;
                snapshot.politics = 50;
                snapshot.glamour = 50;
                snapshot.sex = 0;
                // 音声默认值：男性为 0，女性为 4
                snapshot.voice = 0;
                RefreshConfirmButton();
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
                kanshitsu = target.kanshitsu,
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
            // 相性值占用低 8 位（0-255），高位存储来源武将 ID 仅用于编辑器内显示，保存时只写入相性值
            target.compatibility = snapshot.compatibility;

            target.personality = snapshot.personality;
            target.argumentation = snapshot.argumentation;
            target.voice = snapshot.voice;
            target.tone = snapshot.tone;
            target.kanshitsu = snapshot.kanshitsu;
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
            target.LikePersonList = CloneArray(snapshot.LikePersonList);
            target.HatePersonList = CloneArray(snapshot.HatePersonList);
            target.FeatureList = CloneArray(snapshot.FeatureList);

            if (GameCustomEdit.Instance != null && GameCustomEdit.Instance.SelfScenarioAddon != null)
            {
                GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary.Add(target);
                SaveScenarioAddon();
            }
        }

        /// <summary>
        /// 将当前自建武将数据序列化保存到本地文件。
        /// </summary>
        private void SaveScenarioAddon()
        {
            GameCustomEdit.Instance.SaveScenarioAddon();
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

            // 姓名与列传（姓和名变化时触发确认按钮校验）
            BindTextInput(familyNameInput, () => snapshot.familyName, v => snapshot.familyName = v, RefreshConfirmButton);
            BindTextInput(giveNameInput, () => snapshot.giveName, v => snapshot.giveName = v, RefreshConfirmButton);
            BindTextInput(nickNameInput, () => snapshot.nickName, v => snapshot.nickName = v);
            BindTextInput(descriptionInput, () => snapshot.description, v => snapshot.description = v);

            // 性别（0=男，1=女），性别变化时检查配偶性别与音声值是否仍合法
            BindToggleGroup(sexToggles, () => snapshot.sex, v => snapshot.sex = v, i => i, v => v, OnSexChanged);

            // 出生年（按钮点击弹出 UICalculator，范围 135-250，默认 190）
            BindButtonCalculator(yearBornButton, yearBornText, () => snapshot.yearBorn, v => snapshot.yearBorn = v, 135, 250, OnLifeYearChanged);
            // 寿命（按钮点击弹出 UICalculator，范围 30-99，默认 99）
            BindButtonCalculator(lifeSpanButton, lifeSpanText, () => System.Math.Max(0, snapshot.yearDead - snapshot.yearBorn), v =>
            {
                snapshot.yearDead = snapshot.yearBorn + System.Math.Max(30, v);
            }, 30, 99, OnLifeYearChanged);

            // 性格与相性
            BindToggleGroup(personalityToggles, () => snapshot.personality, v => snapshot.personality = v, i => i + 1, v => v - 1);
            BindVoiceToggleGroup();
            BindToggleGroup(toneToggles, () => snapshot.tone, v => snapshot.tone = v, i => i, v => v);
            BindToggleGroup(hanLoyaltyToggles, () => snapshot.kanshitsu, v => snapshot.kanshitsu = v, i => i, v => v);
            BindToggleGroup(idealToggles, () => snapshot.ideal, v => snapshot.ideal = v, i => i, v => v);
            BindToggleGroup(talentToggles, () => snapshot.talent, v => snapshot.talent = v, i => i, v => v);
            //BindIntInput(compatibilityInput, () => snapshot.compatibility, v => snapshot.compatibility = v, 0, 255);
            // 相性（点击选择武将复制其相性值，相性值占低 8 位，高 24 位存储来源武将 ID）
            if (compatibilitySelectButton != null)
                compatibilitySelectButton.onClick.AddListener(OpenCompatibilityPersonSelect);
            if (compatibilityCancelButton != null)
                compatibilityCancelButton.onClick.AddListener(OnCompatibilityCancelClick);

            // 能力（按钮点击弹出 UICalculator，范围 1-100，默认 50）
            BindButtonCalculator(commandButton, commandText, () => snapshot.command, v => snapshot.command = v, 1, 100, OnAbilityChanged);
            BindButtonCalculator(strengthButton, strengthText, () => snapshot.strength, v => snapshot.strength = v, 1, 100, OnAbilityChanged);
            BindButtonCalculator(intelligenceButton, intelligenceText, () => snapshot.intelligence, v => snapshot.intelligence = v, 1, 100, OnAbilityChanged);
            BindButtonCalculator(politicsButton, politicsText, () => snapshot.politics, v => snapshot.politics = v, 1, 100, OnAbilityChanged);
            BindButtonCalculator(glamourButton, glamourText, () => snapshot.glamour, v => snapshot.glamour = v, 1, 100, OnAbilityChanged);

            // 成长与持续
            //BindGrowthToggleGroup();
            BindToggleGroup(growthToggles, () => snapshot.attributeChangeType, v => snapshot.attributeChangeType = v, i => i + 1, v => v - 1);
            BindToggleGroup(durationToggles, () => snapshot.attributeDuration, v => snapshot.attributeDuration = v, i => i, v => v);

            // 兵种适性（S=3, A=2, B=1, C=0）
            BindAdaptToggleGroup(spearAdaptToggles, () => snapshot.spearLv, v => snapshot.spearLv = v);
            BindAdaptToggleGroup(halberdAdaptToggles, () => snapshot.halberdLv, v => snapshot.halberdLv = v);
            BindAdaptToggleGroup(crossbowAdaptToggles, () => snapshot.crossbowLv, v => snapshot.crossbowLv = v);
            BindAdaptToggleGroup(rideAdaptToggles, () => snapshot.rideLv, v => snapshot.rideLv = v);
            BindAdaptToggleGroup(waterAdaptToggles, () => snapshot.waterLv, v => snapshot.waterLv = v);
            BindAdaptToggleGroup(machineAdaptToggles, () => snapshot.machineLv, v => snapshot.machineLv = v);

            // 人际关系（父亲候选武将需为男性，母亲候选武将需为女性，且年龄大于等于自身年龄15岁）
            BindRelationshipButton(fatherSelectButton, false, OnFatherSelected, IsValidFatherFilter);
            BindRelationshipButton(fatherCancelButton, () => snapshot.Father = 0, RefreshFather);
            BindRelationshipButton(motherSelectButton, false, OnMotherSelected, IsValidMotherFilter);
            BindRelationshipButton(motherCancelButton, () => snapshot.Mother = 0, RefreshMother);
            BindRelationshipButton(spouseSelectButton, true, OnSpouseSelected, IsValidSpouseFilter);
            BindRelationshipButton(spouseCancelButton, () => snapshot.SpouseList = new int[0], RefreshSpouse);
            BindRelationshipButton(brotherSelectButton, false, OnBrotherSelected);
            //BindRelationshipButton(brotherCancelButton, () => snapshot.Brother = 0, RefreshBrother);
            //BindRelationshipButton(swornBrotherSelectButton, true, OnSwornBrotherSelected);
            //BindRelationshipButton(swornBrotherCancelButton, () => snapshot.swornBrotherList = new int[0], RefreshSwornBrother);
            BindRelationshipButton(likeSelectButton, true, OnLikeSelected, IsValidLikeFilter);
            //BindRelationshipButton(likeCancelButton, () => snapshot.LikePersonList = new int[0], RefreshLike);
            BindRelationshipButton(hateSelectButton, true, OnHateSelected, IsValidHateFilter);
            //BindRelationshipButton(hateCancelButton, () => snapshot.HatePersonList = new int[0], RefreshHate);

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

                if (yearBornText != null) yearBornText.text = snapshot.yearBorn.ToString();
                if (yearDeadText != null) yearDeadText.text = snapshot.yearDead.ToString();
                if (yearAvailableText != null) yearAvailableText.text = snapshot.yearAvailable.ToString();
                if (lifeSpanText != null) lifeSpanText.text = System.Math.Max(0, snapshot.yearDead - snapshot.yearBorn).ToString();

                RefreshToggleGroup(personalityToggles, snapshot.personality, i => i - 1, 1);
                RefreshToggleGroup(growthToggles, snapshot.attributeChangeType, i => i - 1, 1);
                RefreshVoiceToggleGroup();
                RefreshToggleGroup(toneToggles, snapshot.tone, i => i, 0);
                RefreshToggleGroup(hanLoyaltyToggles, snapshot.kanshitsu, i => i, 0);
                RefreshToggleGroup(idealToggles, snapshot.ideal, i => i, 0);
                RefreshToggleGroup(talentToggles, snapshot.talent, i => i, 0);

                // if (compatibilityInput != null) compatibilityInput.text = snapshot.compatibility.ToString();
                RefreshCompatibility();

                if (commandText != null) commandText.text = snapshot.command.ToString();
                if (strengthText != null) strengthText.text = snapshot.strength.ToString();
                if (intelligenceText != null) intelligenceText.text = snapshot.intelligence.ToString();
                if (politicsText != null) politicsText.text = snapshot.politics.ToString();
                if (glamourText != null) glamourText.text = snapshot.glamour.ToString();
                OnAbilityChanged();

                //RefreshGrowthToggleGroup();
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
            Texture tex = GameRenderHelper.LoadHeadIcon(snapshot.headIconID, 1);
            if (tex != null)
                personImage.texture = tex;
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

            // 刷新确认按钮可用状态
            RefreshConfirmButton();
        }

        /// <summary>
        /// 生卒年变化时刷新寿命显示，并检查父母年龄差距。
        /// </summary>
        private void OnLifeYearChanged()
        {
            if (lifeSpanText != null)
                lifeSpanText.text = System.Math.Max(0, snapshot.yearDead - snapshot.yearBorn).ToString();
            if (yearDeadText != null)
                yearDeadText.text = snapshot.yearDead.ToString();
            // 自身年龄变化后，判断父母年龄差距是否仍满足要求，低于15岁则自动解除关系
            CheckParentAgeGap();
        }

        /// <summary>
        /// 父亲候选过滤条件：候选武将必须为男性，且年龄大于等于自身年龄15岁。
        /// 年龄 = 参考年 - 出生年，参考年对所有武将一致，因此等价于：候选出生年 <= 自身出生年 - 15。
        /// </summary>
        private bool IsValidFatherFilter(PersonLib person)
        {
            if (person == null) return false;
            return person.sex == 0 && person.yearBorn <= snapshot.yearBorn - 15;
        }

        /// <summary>
        /// 母亲候选过滤条件：候选武将必须为女性，且年龄大于等于自身年龄15岁。
        /// 年龄 = 参考年 - 出生年，参考年对所有武将一致，因此等价于：候选出生年 <= 自身出生年 - 15。
        /// </summary>
        private bool IsValidMotherFilter(PersonLib person)
        {
            if (person == null) return false;
            return person.sex == 1 && person.yearBorn <= snapshot.yearBorn - 15;
        }

        /// <summary>
        /// 检查父母关系合法性，父亲不为男性、母亲不为女性或年龄差距低于15岁则自动解除关系。
        /// </summary>
        private void CheckParentAgeGap()
        {
            bool changed = false;
            if (snapshot.Father > 0 && !IsValidParent(snapshot.Father, 0))
            {
                snapshot.Father = 0;
                changed = true;
            }
            if (snapshot.Mother > 0 && !IsValidParent(snapshot.Mother, 1))
            {
                snapshot.Mother = 0;
                changed = true;
            }
            if (changed)
            {
                RefreshFather();
                RefreshMother();
                Log.Warning("自身年龄变化后，父母关系不再符合性别或年龄要求，已自动解除父母关系");
            }
        }

        /// <summary>
        /// 配偶候选过滤条件：候选武将必须与自身性别不同（0=男，1=女）。
        /// </summary>
        private bool IsValidSpouseFilter(PersonLib person)
        {
            if (person == null) return false;
            return person.sex != snapshot.sex;
        }

        /// <summary>
        /// 亲爱武将候选过滤条件：不能选择已经列入厌恶列表中的武将。
        /// </summary>
        private bool IsValidLikeFilter(PersonLib person)
        {
            if (person == null) return false;
            return !ContainsId(snapshot.HatePersonList, person.Id);
        }

        /// <summary>
        /// 厌恶武将候选过滤条件：不能选择父母、兄弟以及已经列入亲爱列表中的武将。
        /// </summary>
        private bool IsValidHateFilter(PersonLib person)
        {
            if (person == null) return false;
            if (person.Id == snapshot.Father || person.Id == snapshot.Mother) return false;
            if (person.Id == snapshot.Brother) return false;
            if (ContainsId(snapshot.BrotherList, person.Id)) return false;
            if (ContainsId(snapshot.LikePersonList, person.Id)) return false;
            return true;
        }

        /// <summary>
        /// 判断 ID 数组是否包含指定武将 Id。
        /// </summary>
        private bool ContainsId(int[] ids, int personId)
        {
            if (ids == null || ids.Length == 0) return false;
            return System.Array.IndexOf(ids, personId) >= 0;
        }

        /// <summary>
        /// 性别切换回调：检查配偶性别与音声值合法性。
        /// </summary>
        private void OnSexChanged()
        {
            CheckSpouseSex();
            CheckVoiceValid();
        }

        /// <summary>
        /// 检查音声值是否在性别有效范围内：男性 0-3，女性 4-5。
        /// 不在范围内则修正至该性别默认值（男 0、女 4）。
        /// </summary>
        private void CheckVoiceValid()
        {
            bool isFemale = snapshot.sex == 1;
            bool valid = isFemale
                ? snapshot.voice == 4 || snapshot.voice == 5
                : snapshot.voice >= 0 && snapshot.voice <= 3;
            if (valid) return;
            snapshot.voice = isFemale ? 4 : 0;
            RefreshVoiceToggleGroup();
            Log.Warning("性别切换后音声值不在有效范围内，已修正为默认值：" + snapshot.voice);
        }

        /// <summary>
        /// 检查配偶性别合法性，自身性别变化后，若配偶与自身性别相同则自动解除配偶关系。
        /// </summary>
        private void CheckSpouseSex()
        {
            if (snapshot.SpouseList == null || snapshot.SpouseList.Length == 0) return;
            bool changed = false;
            List<int> validList = new List<int>();
            foreach (int spouseId in snapshot.SpouseList)
            {
                PersonLib spouse = GetPersonLibById(spouseId);
                // 找不到武将信息时保留原 ID，避免误删
                if (spouse == null)
                {
                    validList.Add(spouseId);
                    continue;
                }
                if (spouse.sex == snapshot.sex)
                {
                    changed = true;
                    continue;
                }
                validList.Add(spouseId);
            }
            if (changed)
            {
                snapshot.SpouseList = validList.ToArray();
                RefreshSpouse();
                Log.Warning("自身性别变化后，与配偶性别相同，已自动解除配偶关系");
            }
        }

        /// <summary>
        /// 判断指定武将是否可作为合法的父亲或母亲（性别匹配且年龄差距大于等于15岁）。
        /// </summary>
        private bool IsValidParent(int parentId, int sex)
        {
            if (parentId <= 0) return true;
            PersonLib parent = GetPersonLibById(parentId);
            if (parent == null) return true;
            if (parent.sex != sex) return false;
            return parent.yearBorn <= snapshot.yearBorn - 15;
        }
        #endregion

        #region 通用绑定助手
        /// <summary>
        /// 刷新确认按钮的可用状态。
        /// 条件：
        ///   1. 姓不能为空
        ///   2. 名不能为空
        ///   3. 容貌ID必须大于0
        /// 同时满足以上三个条件时确认按钮才可点击。
        /// </summary>
        private void RefreshConfirmButton()
        {
            if (confirmButton == null) return;
            bool canConfirm = !string.IsNullOrEmpty(snapshot.familyName)
                           && !string.IsNullOrEmpty(snapshot.giveName)
                           && snapshot.headIconID > 0;
            confirmButton.interactable = canConfirm;
        }

        /// <summary>
        /// 绑定文本输入框：结束编辑时直接写入快照，支持值变化后的附加回调。
        /// </summary>
        private void BindTextInput(InputField input, Func<string> getter, Action<string> setter, Action onChanged = null)
        {
            if (input == null) return;
            input.onEndEdit.AddListener((text) =>
            {
                if (refreshing) return;
                setter(text ?? string.Empty);
                if (input != null) input.text = getter();
                onChanged?.Invoke();
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
            input.text = getter().ToString();
        }

        /// <summary>
        /// 绑定按钮+文本：点击按钮打开 UICalculator 输入整数值，写入快照并刷新文本。
        /// </summary>
        /// <param name="button">触发按钮</param>
        /// <param name="text">显示文本</param>
        /// <param name="getter">读取当前值</param>
        /// <param name="setter">写入新值（已在 min/max 范围内）</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <param name="onChanged">值变化后的附加回调</param>
        private void BindButtonCalculator(Button button, Text text, Func<int> getter, Action<int> setter, int minValue, int maxValue, Action onChanged = null)
        {
            if (button == null) return;
            button.onClick.AddListener(() =>
            {
                if (refreshing) return;
                int currentValue = getter();
                Window.Instance.Open("window_calculator", currentValue, minValue, maxValue,
                    (Action<int>)((val) =>
                    {
                        setter(val);
                        if (text != null) text.text = getter().ToString();
                        onChanged?.Invoke();
                    }),
                    null);
            });
            // 初始显示
            if (text != null) text.text = getter().ToString();
        }

        /// <summary>
        /// 绑定通用 Toggle 组：同一组内互斥，选中时按 indexToValue 写入快照。
        /// </summary>
        /// <summary>
        /// 绑定音声 Toggle 组。
        /// 男性有效范围为 0-3（索引即值）；女性仅可选中中间两个选项，实际值 = 索引 + 3（4、5）。
        /// </summary>
        private void BindVoiceToggleGroup()
        {
            if (voiceToggles == null) return;
            for (int i = 0; i < voiceToggles.Length; i++)
            {
                if (voiceToggles[i] == null) continue;
                int index = i;
                voiceToggles[i].onValueChanged.AddListener((isOn) =>
                {
                    if (refreshing) return;
                    if (!isOn) return;
                    // 女性仅可选中中间两个选项（索引 1、2）
                    if (snapshot.sex == 1 && index != 1 && index != 2) return;
                    for (int j = 0; j < voiceToggles.Length; j++)
                    {
                        if (j != index && voiceToggles[j] != null && voiceToggles[j].isOn)
                            voiceToggles[j].SetIsOnWithoutNotify(false);
                    }
                    // 男性：值 = 索引；女性：值 = 索引 + 3
                    snapshot.voice = snapshot.sex == 1 ? index + 3 : index;
                });
            }
        }

        /// <summary>
        /// 刷新音声 Toggle 组：根据性别启用/禁用选项并点亮对应 Toggle。
        /// 男性全部选项可用（值 0-3）；女性仅中间两个可用（值 4-5）。
        /// </summary>
        private void RefreshVoiceToggleGroup()
        {
            if (voiceToggles == null) return;
            bool isFemale = snapshot.sex == 1;
            int defaultValue = isFemale ? 4 : 0;
            int value = snapshot.voice;
            // 音声值不在当前性别有效范围内时，按默认值点亮
            if (isFemale)
            {
                if (value < 4 || value > 5) value = defaultValue;
            }
            else
            {
                if (value < 0 || value > 3) value = defaultValue;
            }
            int index = isFemale ? value - 3 : value;
            for (int i = 0; i < voiceToggles.Length; i++)
            {
                if (voiceToggles[i] == null) continue;
                // 女性仅中间两个选项可选，其余置灰
                voiceToggles[i].interactable = !isFemale || i == 1 || i == 2;
                voiceToggles[i].SetIsOnWithoutNotify(i == index);
            }
        }

        private void BindToggleGroup(Toggle[] toggles, Func<int> getter, Action<int> setter,
            Func<int, int> indexToValue, Func<int, int> valueToIndex, Action onChanged = null)
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
                        onChanged?.Invoke();
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

        #region 人际关系
        /// <summary>
        /// 绑定人际关系按钮。
        /// </summary>
        /// <param name="button">按钮</param>
        /// <param name="isMultiSelect">是否为多选</param>
        /// <param name="onSelected">选择完成回调（多选时参数有效）</param>
        /// <param name="filter">可选过滤条件（返回 true 的武将才会显示）</param>
        private void BindRelationshipButton(Button button, bool isMultiSelect, Action<List<PersonLib>> onSelected, Func<PersonLib, bool> filter = null)
        {
            if (button == null) return;
            button.onClick.AddListener(() => OpenPersonSelect(isMultiSelect, onSelected, filter));
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
        /// 打开武将选择器，候选列表来自全武将库。
        /// </summary>
        /// <param name="isMultiSelect">是否多选</param>
        /// <param name="onSelected">选择完成回调</param>
        /// <param name="filter">可选过滤条件（返回 true 的武将才会显示）</param>
        private void OpenPersonSelect(bool isMultiSelect, Action<List<PersonLib>> onSelected, Func<PersonLib, bool> filter = null)
        {
            GameSystem system = GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 EditPersonSelectSystem");
                return;
            }
            EditPersonSelectSystem select = system as EditPersonSelectSystem;

            // 从全武将库中获取数据，并按过滤条件筛选
            List<PersonLib> allPersons = new List<PersonLib>();
            List<PersonLib> allPersonLibs = GameCustomEdit.Instance.AllPersonLibs;
            if (allPersonLibs != null)
            {
                foreach (PersonLib p in allPersonLibs)
                {
                    if (p == null) continue;
                    if (filter != null && !filter(p)) continue;
                    allPersons.Add(p);
                }
            }
            allPersons.Sort((a, b) => PersonLibSortFunction.SortByName.personSortFunc(a, b));

            select.Start(allPersons,
                new List<PersonLib>(),
                isMultiSelect ? allPersons.Count : 1,
                onSelected,
                PersonLibSortFunction.DefaultSortList, "全部武将");
        }

        /// <summary>
        /// 打开相性武将选择器，候选武将来自 <see cref="GameCustomEdit.CoreScenarioAddon"/> 的核心剧本武将库。
        /// </summary>
        private void OpenCompatibilityPersonSelect()
        {
            if (refreshing) return;
            GameSystem system = GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 EditPersonSelectSystem");
                return;
            }
            EditPersonSelectSystem select = system as EditPersonSelectSystem;

            // 从核心剧本武将库中收集候选武将
            List<PersonLib> persons = new List<PersonLib>();
            ScenarioAddon coreAddon = GameCustomEdit.Instance != null ? GameCustomEdit.Instance.CoreScenarioAddon : null;
            if (coreAddon != null && coreAddon.PersonLibrary != null)
            {
                coreAddon.PersonLibrary.ForEach(p =>
                {
                    if (p != null) persons.Add(p);
                });
            }
            persons.Sort((a, b) => PersonLibSortFunction.SortByName.personSortFunc(a, b));

            select.Start(persons,
                new List<PersonLib>(),
                1,
                OnCompatibilityPersonSelected,
                PersonLibSortFunction.DefaultSortList, "核心武将");
        }

        /// <summary>
        /// 相性武将选择完成：复制该武将的相性值到当前武将。
        /// 相性值范围为 0-255，占用低 8 位；高 24 位存储来源武将 ID 用于显示姓名。
        /// </summary>
        private void OnCompatibilityPersonSelected(List<PersonLib> persons)
        {
            if (persons == null || persons.Count == 0) return;
            PersonLib selected = persons[0];
            if (selected == null) return;
            int compatibilityValue = selected.compatibility & 0xFF;
            snapshot.compatibility = (selected.Id << 8) | compatibilityValue;
            RefreshCompatibility();
            Log.Info("已复制武将【" + selected.Name + "】的相性：" + compatibilityValue);
        }

        /// <summary>
        /// 相性取消按钮：清除来源武将 ID 与相性值，相性置 0 且不显示任何内容。
        /// </summary>
        private void OnCompatibilityCancelClick()
        {
            if (refreshing) return;
            snapshot.compatibility = 0;
            RefreshCompatibility();
        }

        /// <summary>
        /// 刷新相性显示：若存在来源武将 ID 则显示其姓名，否则相性值大于 0 时显示数值，为 0 时不显示任何内容。
        /// </summary>
        private void RefreshCompatibility()
        {
            if (compatibilityCancelButton != null)
                compatibilityCancelButton.interactable = snapshot.compatibility > 0;
            if (compatibilityText == null) return;
            int sourcePersonId = snapshot.compatibility >> 8;
            if (sourcePersonId > 0)
            {
                PersonLib source = GetCorePersonById(sourcePersonId);
                if (source != null)
                {
                    compatibilityText.text = source.Name;
                    return;
                }
            }
            int value = snapshot.compatibility & 0xFF;
            compatibilityText.text = value > 0 ? value.ToString() : string.Empty;
        }

        /// <summary>
        /// 根据武将 Id 从核心剧本武将库中查找武将。
        /// </summary>
        private PersonLib GetCorePersonById(int personId)
        {
            if (personId <= 0 || GameCustomEdit.Instance == null) return null;
            ScenarioAddon coreAddon = GameCustomEdit.Instance.CoreScenarioAddon;
            if (coreAddon == null || coreAddon.PersonLibrary == null) return null;
            return coreAddon.PersonLibrary.Find(p => p != null && p.Id == personId);
        }

        private void OnFatherSelected(List<PersonLib> result)
        {
            if (result != null && result.Count > 0) snapshot.Father = result[0].Id;
            RefreshFather();
        }

        private void OnMotherSelected(List<PersonLib> result)
        {
            if (result != null && result.Count > 0) snapshot.Mother = result[0].Id;
            RefreshMother();
        }

        private void OnBrotherSelected(List<PersonLib> result)
        {
            if (result != null && result.Count > 0) snapshot.Brother = result[0].Id;
            RefreshBrother();
        }

        private void OnSpouseSelected(List<PersonLib> result)
        {
            snapshot.SpouseList = ConvertToIds(result);
            RefreshSpouse();
        }

        private void OnLikeSelected(List<PersonLib> result)
        {
            snapshot.LikePersonList = ConvertToIds(result);
            RefreshLike();
        }

        private void OnHateSelected(List<PersonLib> result)
        {
            snapshot.HatePersonList = ConvertToIds(result);
            RefreshHate();
        }

        private int[] ConvertToIds(List<PersonLib> persons)
        {
            if (persons == null) return new int[0];
            return persons.Where(p => p != null).Select(p => p.Id).Distinct().ToArray();
        }

        private void RefreshFather()
        {
            SetPersonNameText(fatherText, snapshot.Father);
            if (fatherCancelButton != null)
                fatherCancelButton.interactable = snapshot.Father > 0;
        }

        private void RefreshMother()
        {
            SetPersonNameText(motherText, snapshot.Mother);
            if (motherCancelButton != null)
                motherCancelButton.interactable = snapshot.Mother > 0;
        }

        private void RefreshBrother()
        {
            SetPersonNameText(brotherText, snapshot.Brother);
        }

        private void RefreshSpouse()
        {
            SetPersonNamesText(spouseText, snapshot.SpouseList);
            if (spouseCancelButton != null)
                spouseCancelButton.interactable = snapshot.SpouseList != null && snapshot.SpouseList.Length > 0;
        }

        private void RefreshSwornBrother()
        {
            //SetPersonNamesText(swornBrotherText, snapshot.swornBrotherList);
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
        /// 根据武将 Id 获取名称，从全武将库中查找。
        /// </summary>
        private string GetPersonName(int personId)
        {
            if (personId <= 0) return string.Empty;
            PersonLib lib = GetPersonLibById(personId);
            if (lib != null) return lib.Name;
            return personId.ToString();
        }

        /// <summary>
        /// 根据武将 Id 从全武将库中查找武将。
        /// </summary>
        private PersonLib GetPersonLibById(int personId)
        {
            if (personId <= 0 || GameCustomEdit.Instance == null) return null;
            List<PersonLib> allPersonLibs = GameCustomEdit.Instance.AllPersonLibs;
            if (allPersonLibs != null)
            {
                foreach (PersonLib lib in allPersonLibs)
                {
                    if (lib != null && lib.Id == personId) return lib;
                }
            }
            return null;
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
            if (GameData.Instance.ScenarioCommonData != null && GameData.Instance.ScenarioCommonData.Features != null)
            {
                foreach (Feature f in GameData.Instance.ScenarioCommonData.Features)
                {
                    if (f != null) allFeatures.Add(f);
                }
            }

            List<Feature> initialSelect = new List<Feature>();
            if (snapshot.FeatureList != null)
            {
                foreach (int id in snapshot.FeatureList)
                {
                    Feature f = GameData.Instance.ScenarioCommonData.Features.Get(id);
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
            ScenarioCommonData scenarioCommonData = GameData.Instance.ScenarioCommonData;
            List<string> names = new List<string>();
            foreach (int id in snapshot.FeatureList)
            {
                Feature f = scenarioCommonData.Features.Get(id);
                names.Add(f != null ? f.Name : id.ToString());
            }
            featureText.text = string.Join(", ", names.ToArray());
        }
        #endregion

        #region 头像与造型
        private void OnChangeImageClick()
        {
            Log.Info("打开头像选择窗口");
            Window.Instance.Open("window_create_person_image", snapshot.headIconID, (Action<int>)((headId) =>
            {
                snapshot.headIconID = headId;
                RefreshImage();
                // 容貌ID变更后刷新确认按钮状态
                RefreshConfirmButton();
            }));
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
            //GameSystemManager.Instance.Back();
            Close();
            Window.Instance.Open("window_create_person_menu");
        }

        private void OnBackClick()
        {
            // GameSystemManager.Instance.Back();
            Close();
            Window.Instance.Open("window_create_person_menu");
        }

        private void OnCancelClick()
        {
            // GameSystemManager.Instance.Back();
            OnBackClick();
        }
        #endregion

    }
}
