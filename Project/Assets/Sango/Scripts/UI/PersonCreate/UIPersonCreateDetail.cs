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
    /// 提供“基本设定”与“能力设定”两个标签页，用于编辑 <see cref="PersonLib"/> 与 <see cref="Person"/> 数据。
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

            // 以下字段仅在 Person 编辑模式（剧本编辑页）下使用
            public int BelongForce;
            public int BelongCorps;
            public int BelongCity;
            public int state;
            public string image_old;
            public int loyalty;
            public int birthplace;
            public int official;
            public ItemStore itemStore = new ItemStore();

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
            public int[] SpouseList = new int[0];
            public int[] BrotherList = new int[0];
            public int[] LikePersonList = new int[0];
            public int[] HatePersonList = new int[0];
            public int[] FeatureList = new int[0];
        }

        /// <summary>
        /// 编辑模式：新建 / 自建武将库 / 当前剧本武将。
        /// </summary>
        private enum PersonEditMode
        {
            /// <summary>新建武将</summary>
            Create,
            /// <summary>编辑自建武将库 PersonLib</summary>
            PersonLib,
            /// <summary>编辑当前剧本中的 Person</summary>
            Person
        }

        /// <summary>
        /// 当前编辑模式。
        /// </summary>
        private PersonEditMode editMode = PersonEditMode.Create;

        /// <summary>
        /// 标签页类型。
        /// </summary>
        private enum TabType
        {
            /// <summary>基本设定</summary>
            Basic,
            /// <summary>能力设定</summary>
            Ability,
            /// <summary>剧本编辑</summary>
            Scenario
        }

        /// <summary>
        /// Person 编辑模式下的目标武将。
        /// </summary>
        private Person targetPerson;

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

        /// <summary>
        /// 剧本编辑标签页切换（仅在 Person 编辑模式下可见）
        /// </summary>
        public Toggle scenarioTabToggle;

        /// <summary>
        /// 剧本编辑页面根节点（仅在 Person 编辑模式下可见）
        /// </summary>
        public GameObject scenarioPanel;
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

        #region 剧本编辑（仅在 Person 编辑模式下使用）
        /// <summary>所属势力选择按钮</summary>
        public Button belongForceSelectButton;
        /// <summary>所属势力显示文本</summary>
        public Text belongForceText;
        /// <summary>所属势力清除按钮</summary>
        public Button belongForceCancelButton;

        /// <summary>所属军团选择按钮</summary>
        public Button belongCorpsSelectButton;
        /// <summary>所属军团显示文本</summary>
        public Text belongCorpsText;
        /// <summary>所属军团清除按钮</summary>
        public Button belongCorpsCancelButton;

        /// <summary>所属城市选择按钮</summary>
        public Button belongCitySelectButton;
        /// <summary>所属城市显示文本</summary>
        public Text belongCityText;
        /// <summary>所属城市清除按钮</summary>
        public Button belongCityCancelButton;

        /// <summary>身份下拉菜单</summary>
        public Dropdown stateDropdown;

        /// <summary>立绘输入框（老年）</summary>
        public InputField imageInput;
        /// <summary>立绘输入框（青年）</summary>
        public InputField imageOldInput;

        /// <summary>登场年按钮</summary>
        public Button yearAvailableButton;
        /// <summary>登场年显示文本</summary>
        public Text yearAvailableTextScenario;

        /// <summary>忠诚按钮</summary>
        public Button loyaltyButton;
        /// <summary>忠诚显示文本</summary>
        public Text loyaltyText;

        /// <summary>义理下拉菜单</summary>
        public Dropdown argumentationDropdown;
        /// <summary>出生地下拉菜单</summary>
        public Dropdown birthplaceDropdown;

        /// <summary>官职选择按钮</summary>
        public Button officialSelectButton;
        /// <summary>官职显示文本</summary>
        public Text officialText;
        /// <summary>官职清除按钮</summary>
        public Button officialCancelButton;

        /// <summary>理想下拉菜单</summary>
        public Dropdown idealDropdown;
        /// <summary>才干下拉菜单</summary>
        public Dropdown talentDropdown;

        /// <summary>道具下拉菜单</summary>
        public Dropdown itemDropdown;
        /// <summary>道具数量输入框</summary>
        public InputField itemCountInput;
        /// <summary>道具添加按钮</summary>
        public Button itemAddButton;
        /// <summary>道具清空按钮</summary>
        public Button itemClearButton;
        /// <summary>道具列表显示文本</summary>
        public Text itemListText;
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
            editMode = PersonEditMode.Create;
            targetPerson = null;
            InitSnapshot();
            // 无参打开时若 GameCustomEdit 存在编辑目标，则视为 PersonLib 编辑模式
            PersonLib target = GameCustomEdit.Instance != null ? GameCustomEdit.Instance.TargetEditPerson : null;
            if (target != null)
                editMode = PersonEditMode.PersonLib;
            SwitchTab(TabType.Basic);
            RefreshScenarioTabVisibility();
            RefreshAll();
        }

        public override void OnOpen(params object[] objs)
        {
            base.OnOpen(objs);
            if (objs != null && objs.Length > 0)
            {
                if (objs[0] is Person)
                {
                    targetPerson = objs[0] as Person;
                    editMode = PersonEditMode.Person;
                    InitSnapshotFromPerson(targetPerson);
                    RefreshConfirmButton();
                    SwitchTab(TabType.Basic);
                    RefreshScenarioTabVisibility();
                    RefreshAll();
                    return;
                }
                if (objs[0] is Snapshot)
                {
                    targetPerson = null;
                    editMode = PersonEditMode.PersonLib;
                    snapshot = objs[0] as Snapshot;
                    RefreshConfirmButton();
                    SwitchTab(TabType.Basic);
                    RefreshScenarioTabVisibility();
                    RefreshAll();
                    return;
                }
            }

            // 未识别到有效参数时按新建武将处理
            editMode = PersonEditMode.Create;
            targetPerson = null;
            InitSnapshot();
            SwitchTab(TabType.Basic);
            RefreshScenarioTabVisibility();
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
                SpouseList = CloneArray(target.SpouseList),
                BrotherList = CloneArray(target.BrotherList),
                LikePersonList = CloneArray(target.LikePersonList),
                HatePersonList = CloneArray(target.HatePersonList),
                FeatureList = CloneArray(target.FeatureList)
            };
        }

        /// <summary>
        /// 从当前剧本的 <see cref="Person"/> 初始化快照。
        /// 该模式不依赖 <see cref="GameCustomEdit"/>，所有数据来源均为 <see cref="Scenario.Cur"/>。
        /// </summary>
        /// <param name="person">目标武将</param>
        private void InitSnapshotFromPerson(Person person)
        {
            if (person == null)
            {
                Log.Error("Person 编辑模式传入的武将对象为空");
                return;
            }

            snapshot = new Snapshot
            {
                familyName = person.familyName ?? string.Empty,
                giveName = person.giveName ?? string.Empty,
                nickName = person.nickName ?? string.Empty,
                description = person.description ?? string.Empty,
                sex = person.sex,
                image = person.image ?? string.Empty,
                headIconID = person.headIconID,

                yearBorn = person.yearBorn,
                yearDead = person.yearDead,
                yearAvailable = person.appearance,
                compatibility = person.compatibility,

                // 剧本编辑相关字段
                BelongForce = person.BelongForce,
                BelongCorps = person.BelongCorps,
                BelongCity = person.BelongCity,
                state = person.state,
                image_old = person.image_old ?? string.Empty,
                loyalty = person.loyalty,
                birthplace = person.birthplace,
                official = person.Official != null ? person.Official.Id : 0,
                itemStore = person.itemStore != null ? person.itemStore.Copy() : new ItemStore(),

                personality = person.personality,
                argumentation = person.argumentation,
                voice = person.voice,
                tone = person.tone,
                kanshitsu = person.kanshitsu,
                ideal = person.ideal,
                talent = person.talent,

                command = person.command != null ? person.command.baseValue : 50,
                strength = person.strength != null ? person.strength.baseValue : 50,
                intelligence = person.intelligence != null ? person.intelligence.baseValue : 50,
                politics = person.politics != null ? person.politics.baseValue : 50,
                glamour = person.glamour != null ? person.glamour.baseValue : 50,
                attributeChangeType = person.command != null ? person.command.changeId : 5,
                attributeDuration = 0,

                spearLv = person.spearLv != null ? person.spearLv.baseValue : 0,
                halberdLv = person.halberdLv != null ? person.halberdLv.baseValue : 0,
                crossbowLv = person.crossbowLv != null ? person.crossbowLv.baseValue : 0,
                rideLv = person.rideLv != null ? person.rideLv.baseValue : 0,
                waterLv = person.waterLv != null ? person.waterLv.baseValue : 0,
                machineLv = person.machineLv != null ? person.machineLv.baseValue : 0,

                Father = person.Father,
                Mother = person.Mother,
                SpouseList = CloneArray(person.SpouseList),
                BrotherList = PersonListToIds(person.BrotherList),
                LikePersonList = CloneArray(person.LikePersonList),
                HatePersonList = CloneArray(person.HatePersonList),
                FeatureList = CloneArray(person.FeatureList)
            };
        }

        /// <summary>
        /// 将快照数据写回目标对象；根据当前编辑模式分别写入 PersonLib 或 Person。
        /// </summary>
        private void ApplySnapshotToTarget()
        {
            switch (editMode)
            {
                case PersonEditMode.Person:
                    ApplySnapshotToPerson(targetPerson);
                    break;
                default:
                    ApplySnapshotToPersonLib();
                    break;
            }
        }

        /// <summary>
        /// 将快照写回自建武将库 PersonLib，并存入自建武将列表。
        /// </summary>
        private void ApplySnapshotToPersonLib()
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
            // 相性值范围为 0-255；Person 编辑模式直接保存数值，自建武将模式高位可存储来源武将 ID 用于显示
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
            target.SpouseList = CloneArray(snapshot.SpouseList);
            target.BrotherList = CloneArray(snapshot.BrotherList);
            target.LikePersonList = CloneArray(snapshot.LikePersonList);
            target.HatePersonList = CloneArray(snapshot.HatePersonList);
            target.FeatureList = CloneArray(snapshot.FeatureList);

            GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary.Add(target);
            SaveScenarioAddon();
        }

        /// <summary>
        /// 将快照写回当前剧本中的 <see cref="Person"/>。
        /// 所有引用均来自 <see cref="Scenario.Cur"/>，不操作 <see cref="GameCustomEdit"/>。
        /// </summary>
        /// <param name="target">目标武将</param>
        private void ApplySnapshotToPerson(Person target)
        {
            if (target == null) return;
            Scenario cur = Scenario.Cur;

            target.familyName = snapshot.familyName;
            target.giveName = snapshot.giveName;
            target.Name = snapshot.familyName + snapshot.giveName;
            target.nickName = snapshot.nickName;
            target.description = snapshot.description;
            target.sex = snapshot.sex;
            target.image = snapshot.image;
            target.headIconID = snapshot.headIconID;

            target.yearBorn = snapshot.yearBorn;
            target.yearDead = snapshot.yearDead;
            target.appearance = snapshot.yearAvailable;
            target.compatibility = snapshot.compatibility;

            // 剧本编辑字段：更新 ID 与运行时引用
            target.BelongForce = snapshot.BelongForce;
            target.mBelongForce = snapshot.BelongForce > 0 ? cur.forceSet.Get(snapshot.BelongForce) : null;
            target.BelongCorps = snapshot.BelongCorps;
            target.mBelongCorps = snapshot.BelongCorps > 0 ? cur.corpsSet.Get(snapshot.BelongCorps) : null;
            target.BelongCity = snapshot.BelongCity;
            target.mBelongCity = snapshot.BelongCity > 0 ? cur.citySet.Get(snapshot.BelongCity) : null;
            target.state = snapshot.state;
            target.image_old = snapshot.image_old;
            target.loyalty = snapshot.loyalty;
            target.birthplace = snapshot.birthplace;
            target.Official = snapshot.official > 0 ? cur.CommonData.Officials.Get(snapshot.official) : null;
            target.itemStore = snapshot.itemStore != null ? snapshot.itemStore.Copy() : new ItemStore();

            target.personality = snapshot.personality;
            target.argumentation = snapshot.argumentation;
            target.mArgumentation = snapshot.argumentation > 0 ? cur.CommonData.Argumentations.Get(snapshot.argumentation) : null;
            target.voice = snapshot.voice;
            target.tone = snapshot.tone;
            target.kanshitsu = snapshot.kanshitsu;
            target.ideal = snapshot.ideal;
            target.talent = snapshot.talent;

            if (target.command == null) target.command = new PersonAttributeValue();
            if (target.strength == null) target.strength = new PersonAttributeValue();
            if (target.intelligence == null) target.intelligence = new PersonAttributeValue();
            if (target.politics == null) target.politics = new PersonAttributeValue();
            if (target.glamour == null) target.glamour = new PersonAttributeValue();

            target.command.baseValue = snapshot.command;
            target.strength.baseValue = snapshot.strength;
            target.intelligence.baseValue = snapshot.intelligence;
            target.politics.baseValue = snapshot.politics;
            target.glamour.baseValue = snapshot.glamour;

            target.command.changeId = snapshot.attributeChangeType;
            target.strength.changeId = snapshot.attributeChangeType;
            target.intelligence.changeId = snapshot.attributeChangeType;
            target.politics.changeId = snapshot.attributeChangeType;
            target.glamour.changeId = snapshot.attributeChangeType;

            // 强制重新解析 AttributeChangeType 缓存
            target.command.changeType = null;
            target.strength.changeType = null;
            target.intelligence.changeType = null;
            target.politics.changeType = null;
            target.glamour.changeType = null;

            if (cur == null || !cur.Variables.AgeEnabled || !cur.Variables.EnableAgeAbilityFactor)
            {
                target.command.UpdateNoAge();
                target.strength.UpdateNoAge();
                target.intelligence.UpdateNoAge();
                target.politics.UpdateNoAge();
                target.glamour.UpdateNoAge();
            }
            else
            {
                target.command.Update(target.Age, cur);
                target.strength.Update(target.Age, cur);
                target.intelligence.Update(target.Age, cur);
                target.politics.Update(target.Age, cur);
                target.glamour.Update(target.Age, cur);
            }

            if (target.spearLv == null) target.spearLv = new PersonAbilityValue();
            if (target.halberdLv == null) target.halberdLv = new PersonAbilityValue();
            if (target.crossbowLv == null) target.crossbowLv = new PersonAbilityValue();
            if (target.rideLv == null) target.rideLv = new PersonAbilityValue();
            if (target.waterLv == null) target.waterLv = new PersonAbilityValue();
            if (target.machineLv == null) target.machineLv = new PersonAbilityValue();

            target.spearLv.baseValue = snapshot.spearLv;
            target.halberdLv.baseValue = snapshot.halberdLv;
            target.crossbowLv.baseValue = snapshot.crossbowLv;
            target.rideLv.baseValue = snapshot.rideLv;
            target.waterLv.baseValue = snapshot.waterLv;
            target.machineLv.baseValue = snapshot.machineLv;

            target.spearLv.Update();
            target.halberdLv.Update();
            target.crossbowLv.Update();
            target.rideLv.Update();
            target.waterLv.Update();
            target.machineLv.Update();

            target.Father = snapshot.Father;
            target.Mother = snapshot.Mother;
            target.mFather = GetPersonById(snapshot.Father);
            target.mMother = GetPersonById(snapshot.Mother);
            target.SpouseList = CloneArray(snapshot.SpouseList);

            // 兄弟关系：更新运行时列表与序列化字段
            if (target.BrotherList == null)
                target.BrotherList = new List<Person>();
            target.BrotherList.Clear();
            if (snapshot.BrotherList != null && snapshot.BrotherList.Length > 0)
            {
                foreach (int id in snapshot.BrotherList)
                {
                    Person brother = GetPersonById(id);
                    if (brother != null)
                        target.BrotherList.Add(brother);
                }
                target.Brother = snapshot.BrotherList[0];
                target.mBrother = GetPersonById(target.Brother);
            }
            else
            {
                target.Brother = 0;
                target.mBrother = null;
            }

            target.LikePersonList = CloneArray(snapshot.LikePersonList);
            target.HatePersonList = CloneArray(snapshot.HatePersonList);
            target.FeatureList = CloneArray(snapshot.FeatureList);

            // 同步运行时的对象引用列表
            target.mFeatureList = RefreshPersonObjectList(target.mFeatureList, snapshot.FeatureList, id => cur != null ? cur.CommonData.Features.Get(id) : null);
            target.mLikePersonList = RefreshPersonObjectList(target.mLikePersonList, snapshot.LikePersonList, id => GetPersonById(id));
            target.mHatePersonList = RefreshPersonObjectList(target.mHatePersonList, snapshot.HatePersonList, id => GetPersonById(id));

            // mSpouseList 的 setter 为 private，只能清空/添加，不能重新赋值
            if (target.mSpouseList != null)
            {
                target.mSpouseList.Clear();
                if (snapshot.SpouseList != null)
                {
                    foreach (int id in snapshot.SpouseList)
                    {
                        Person spouse = GetPersonById(id);
                        if (spouse != null)
                            target.mSpouseList.Add(spouse);
                    }
                }
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

        /// <summary>
        /// 将 Person 运行时列表转换为 Id 数组。
        /// </summary>
        private int[] PersonListToIds(List<Person> list)
        {
            if (list == null || list.Count == 0) return new int[0];
            return list.Where(p => p != null).Select(p => p.Id).Distinct().ToArray();
        }

        /// <summary>
        /// 根据武将 Id 从当前剧本中查找 Person。
        /// </summary>
        private Person GetPersonById(int personId)
        {
            if (personId <= 0) return null;
            Scenario cur = Scenario.Cur;
            if (cur == null || cur.personSet == null) return null;
            return cur.personSet.Get(personId);
        }

        /// <summary>
        /// 根据 Id 数组刷新 SangoObjectList 运行时列表。
        /// </summary>
        /// <returns>刷新后的运行时列表</returns>
        private SangoObjectList<T> RefreshPersonObjectList<T>(SangoObjectList<T> list, int[] ids, Func<int, T> getter) where T : SangoObject, new()
        {
            if (list == null)
                list = new SangoObjectList<T>();
            list.Clear();
            if (ids != null)
            {
                foreach (int id in ids)
                {
                    T obj = getter(id);
                    if (obj != null)
                        list.Add(obj);
                }
            }
            return list;
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
                basicTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) SwitchTab(TabType.Basic); });
            if (abilityTabToggle != null)
                abilityTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) SwitchTab(TabType.Ability); });
            if (scenarioTabToggle != null)
                scenarioTabToggle.onValueChanged.AddListener((isOn) => { if (isOn) SwitchTab(TabType.Scenario); });

            // 剧本编辑页（仅在 Person 编辑模式下使用）
            BindScenarioPanel();

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
            // 相性（点击选择武将复制其相性值；Person 编辑模式直接保存数值，自建武将模式低 8 位保存数值，高 24 位存储来源武将 ID）
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
            BindRelationshipSelectButton(fatherSelectButton, false, OnFatherSelectedIds, IsValidFatherFilterLib, IsValidFatherFilterPerson);
            BindRelationshipButton(fatherCancelButton, () => snapshot.Father = 0, RefreshFather);
            BindRelationshipSelectButton(motherSelectButton, false, OnMotherSelectedIds, IsValidMotherFilterLib, IsValidMotherFilterPerson);
            BindRelationshipButton(motherCancelButton, () => snapshot.Mother = 0, RefreshMother);
            BindRelationshipSelectButton(spouseSelectButton, true, OnSpouseSelectedIds, IsValidSpouseFilterLib, IsValidSpouseFilterPerson);
            BindRelationshipButton(spouseCancelButton, () => snapshot.SpouseList = new int[0], RefreshSpouse);
            BindRelationshipSelectButton(brotherSelectButton, true, OnBrotherSelectedIds, IsValidBrotherFilterLib, IsValidBrotherFilterPerson);
            //BindRelationshipButton(brotherCancelButton, () => snapshot.Brother = 0, RefreshBrother);
            //BindRelationshipButton(swornBrotherSelectButton, true, OnSwornBrotherSelected);
            //BindRelationshipButton(swornBrotherCancelButton, () => snapshot.swornBrotherList = new int[0], RefreshSwornBrother);
            BindRelationshipSelectButton(likeSelectButton, true, OnLikeSelectedIds, IsValidLikeFilterLib, IsValidLikeFilterPerson);
            //BindRelationshipButton(likeCancelButton, () => snapshot.LikePersonList = new int[0], RefreshLike);
            BindRelationshipSelectButton(hateSelectButton, true, OnHateSelectedIds, IsValidHateFilterLib, IsValidHateFilterPerson);
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

        /// <summary>
        /// 绑定剧本编辑页所有 UI 事件。
        /// 这些对象即使在非 Person 模式下被绑定，也不会影响其他模式的数据。
        /// </summary>
        private void BindScenarioPanel()
        {
            // 势力 / 军团 / 城市 / 官职选择
            if (belongForceSelectButton != null) belongForceSelectButton.onClick.AddListener(OpenBelongForceSelect);
            if (belongForceCancelButton != null) belongForceCancelButton.onClick.AddListener(() => { snapshot.BelongForce = 0; RefreshBelongForce(); });
            if (belongCorpsSelectButton != null) belongCorpsSelectButton.onClick.AddListener(OpenBelongCorpsSelect);
            if (belongCorpsCancelButton != null) belongCorpsCancelButton.onClick.AddListener(() => { snapshot.BelongCorps = 0; RefreshBelongCorps(); });
            if (belongCitySelectButton != null) belongCitySelectButton.onClick.AddListener(OpenBelongCitySelect);
            if (belongCityCancelButton != null) belongCityCancelButton.onClick.AddListener(() => { snapshot.BelongCity = 0; RefreshBelongCity(); });
            if (officialSelectButton != null) officialSelectButton.onClick.AddListener(OpenOfficialSelect);
            if (officialCancelButton != null) officialCancelButton.onClick.AddListener(() => { snapshot.official = 0; RefreshOfficial(); });

            // 文本输入
            BindTextInput(imageInput, () => snapshot.image, v => snapshot.image = v);
            BindTextInput(imageOldInput, () => snapshot.image_old, v => snapshot.image_old = v);

            // 数字输入
            BindButtonCalculator(yearAvailableButton, yearAvailableTextScenario, () => snapshot.yearAvailable, v => snapshot.yearAvailable = v, 0, 300, null);
            BindButtonCalculator(loyaltyButton, loyaltyText, () => snapshot.loyalty, v => snapshot.loyalty = v, 0, 255, null);

            // 下拉菜单：初始化选项与事件
            List<Dropdown.OptionData> stateOptions = new List<Dropdown.OptionData>();
            List<int> stateValues = new List<int>();
            GetStateOptions(stateOptions, stateValues);
            BindDropdown(stateDropdown, stateOptions, stateValues, () => snapshot.state, v => snapshot.state = v);

            List<Dropdown.OptionData> argumentationOptions = new List<Dropdown.OptionData>();
            List<int> argumentationValues = new List<int>();
            GetArgumentationOptions(argumentationOptions, argumentationValues);
            BindDropdown(argumentationDropdown, argumentationOptions, argumentationValues, () => snapshot.argumentation, v => snapshot.argumentation = v);

            List<Dropdown.OptionData> birthplaceOptions = new List<Dropdown.OptionData>();
            List<int> birthplaceValues = new List<int>();
            GetBirthplaceOptions(birthplaceOptions, birthplaceValues);
            BindDropdown(birthplaceDropdown, birthplaceOptions, birthplaceValues, () => snapshot.birthplace, v => snapshot.birthplace = v);

            List<Dropdown.OptionData> idealOptions = new List<Dropdown.OptionData>();
            List<int> idealValues = new List<int>();
            GetIdealOptions(idealOptions, idealValues);
            BindDropdown(idealDropdown, idealOptions, idealValues, () => snapshot.ideal, v => snapshot.ideal = v);

            List<Dropdown.OptionData> talentOptions = new List<Dropdown.OptionData>();
            List<int> talentValues = new List<int>();
            GetTalentOptions(talentOptions, talentValues);
            BindDropdown(talentDropdown, talentOptions, talentValues, () => snapshot.talent, v => snapshot.talent = v);

            // 道具
            InitItemDropdown();
            if (itemAddButton != null) itemAddButton.onClick.AddListener(OnItemAddClick);
            if (itemClearButton != null) itemClearButton.onClick.AddListener(OnItemClearClick);
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

                RefreshScenarioTabVisibility();
                RefreshScenarioPanel();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// 切换标签页并同步 Toggle 状态。
        /// </summary>
        /// <param name="tab">目标标签页</param>
        private void SwitchTab(TabType tab)
        {
            if (basicPanel != null) basicPanel.SetActive(tab == TabType.Basic);
            if (abilityPanel != null) abilityPanel.SetActive(tab == TabType.Ability);
            if (scenarioPanel != null) scenarioPanel.SetActive(tab == TabType.Scenario);

            // 同步 Toggle 显示状态
            if (basicTabToggle != null && basicTabToggle.isOn != (tab == TabType.Basic))
                basicTabToggle.SetIsOnWithoutNotify(tab == TabType.Basic);
            if (abilityTabToggle != null && abilityTabToggle.isOn != (tab == TabType.Ability))
                abilityTabToggle.SetIsOnWithoutNotify(tab == TabType.Ability);
            if (scenarioTabToggle != null && scenarioTabToggle.isOn != (tab == TabType.Scenario))
                scenarioTabToggle.SetIsOnWithoutNotify(tab == TabType.Scenario);
        }

        /// <summary>
        /// 根据当前编辑模式刷新剧本编辑标签页可见性。
        /// 仅在 Person 编辑模式下显示该标签页。
        /// </summary>
        private void RefreshScenarioTabVisibility()
        {
            bool visible = editMode == PersonEditMode.Person;
            if (scenarioTabToggle != null) scenarioTabToggle.gameObject.SetActive(visible);
            // 当剧本编辑页不可见时，自动切回基本设定页
            if (!visible && scenarioPanel != null && scenarioPanel.activeSelf)
                SwitchTab(TabType.Basic);
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
                int spouseSex = -1;
                if (editMode == PersonEditMode.Person)
                {
                    Person spouse = GetPersonById(spouseId);
                    if (spouse != null) spouseSex = spouse.sex;
                }
                else
                {
                    PersonLib spouse = GetPersonLibById(spouseId);
                    if (spouse != null) spouseSex = spouse.sex;
                }
                // 找不到武将信息时保留原 ID，避免误删
                if (spouseSex < 0)
                {
                    validList.Add(spouseId);
                    continue;
                }
                if (spouseSex == snapshot.sex)
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
            if (editMode == PersonEditMode.Person)
            {
                Person parent = GetPersonById(parentId);
                if (parent == null) return true;
                if (parent.sex != sex) return false;
                return parent.yearBorn <= snapshot.yearBorn - 15;
            }
            else
            {
                PersonLib parent = GetPersonLibById(parentId);
                if (parent == null) return true;
                if (parent.sex != sex) return false;
                return parent.yearBorn <= snapshot.yearBorn - 15;
            }
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
        /// 绑定模式感知的人际关系选择按钮。
        /// 根据当前编辑模式，候选列表分别来自当前剧本或自建武将库。
        /// </summary>
        private void BindRelationshipSelectButton(Button button, bool isMultiSelect, Action<int[]> onSelectedIds,
            Func<PersonLib, bool> libFilter = null, Func<Person, bool> personFilter = null)
        {
            if (button == null) return;
            button.onClick.AddListener(() => OpenPersonSelectModeAware(isMultiSelect, onSelectedIds, libFilter, personFilter));
        }

        /// <summary>
        /// 打开武将选择器，候选列表根据编辑模式分别来自当前剧本 Person 或自建武将库 PersonLib。
        /// </summary>
        /// <param name="isMultiSelect">是否多选</param>
        /// <param name="onSelectedIds">选择完成回调，返回选中的武将 Id 列表</param>
        /// <param name="libFilter">自建武将库过滤条件</param>
        /// <param name="personFilter">当前剧本武将过滤条件</param>
        private void OpenPersonSelectModeAware(bool isMultiSelect, Action<int[]> onSelectedIds,
            Func<PersonLib, bool> libFilter = null, Func<Person, bool> personFilter = null)
        {
            GameSystem system = GameSystemManager.Instance.GetSystem<EditPersonSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 EditPersonSelectSystem");
                return;
            }
            EditPersonSelectSystem select = system as EditPersonSelectSystem;

            if (editMode == PersonEditMode.Person)
            {
                // 从当前剧本中获取数据，并按过滤条件筛选
                List<Person> allPersons = new List<Person>();
                Scenario cur = Scenario.Cur;
                if (cur != null && cur.personSet != null)
                {
                    foreach (Person p in cur.personSet)
                    {
                        if (p == null || !p.IsValid) continue;
                        if (p == targetPerson) continue;
                        if (personFilter != null && !personFilter(p)) continue;
                        allPersons.Add(p);
                    }
                }
                allPersons.Sort(PersonSortFunction.SortByName.Sort);

                select.Start(allPersons,
                    new List<Person>(),
                    isMultiSelect ? allPersons.Count : 1,
                    (Action<List<Person>>)(result => onSelectedIds?.Invoke(ConvertToIds(result))),
                    PersonSortFunction.DefaultSortList, "全部武将");
            }
            else
            {
                // 从全武将库中获取数据，并按过滤条件筛选
                List<PersonLib> allPersons = new List<PersonLib>();
                List<PersonLib> allPersonLibs = GameCustomEdit.Instance != null ? GameCustomEdit.Instance.AllPersonLibs : null;
                if (allPersonLibs != null)
                {
                    foreach (PersonLib p in allPersonLibs)
                    {
                        if (p == null) continue;
                        if (libFilter != null && !libFilter(p)) continue;
                        allPersons.Add(p);
                    }
                }
                allPersons.Sort((a, b) => PersonLibSortFunction.SortByName.personSortFunc(a, b));

                select.Start(allPersons,
                    new List<PersonLib>(),
                    isMultiSelect ? allPersons.Count : 1,
                    (Action<List<PersonLib>>)(result => onSelectedIds?.Invoke(ConvertToIds(result))),
                    PersonLibSortFunction.DefaultSortList, "全部武将");
            }
        }

        /// <summary>
        /// 打开相性武将选择器。
        /// Person 编辑模式下选择武将后，直接保存其相性值，不保存武将 ID；
        /// 自建武将模式候选来自 <see cref="GameCustomEdit.CoreScenarioAddon"/> 的核心剧本武将库。
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

            if (editMode == PersonEditMode.Person)
            {
                // 从当前剧本中收集候选武将
                List<Person> persons = new List<Person>();
                Scenario cur = Scenario.Cur;
                if (cur != null && cur.personSet != null)
                {
                    foreach (Person p in cur.personSet)
                    {
                        if (p != null) persons.Add(p);
                    }
                }
                persons.Sort(PersonSortFunction.SortByName.Sort);

                select.Start(persons,
                    new List<Person>(),
                    1,
                    (Action<List<Person>>)(result =>
                    {
                        if (result == null || result.Count == 0) return;
                        Person selected = result[0];
                        if (selected == null) return;
                        int compatibilityValue = selected.compatibility & 0xFF;
                        // Person 编辑模式下只保存相性值，不再保存来源武将 ID
                        snapshot.compatibility = compatibilityValue;
                        RefreshCompatibility();
                        Log.Info("已复制武将【" + selected.Name + "】的相性：" + compatibilityValue);
                    }),
                    PersonSortFunction.DefaultSortList, "全部武将");
            }
            else
            {
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
                    (Action<List<PersonLib>>)(result =>
                    {
                        if (result == null || result.Count == 0) return;
                        PersonLib selected = result[0];
                        if (selected == null) return;
                        int compatibilityValue = selected.compatibility & 0xFF;
                        snapshot.compatibility = (selected.Id << 8) | compatibilityValue;
                        RefreshCompatibility();
                        Log.Info("已复制武将【" + selected.Name + "】的相性：" + compatibilityValue);
                    }),
                    PersonLibSortFunction.DefaultSortList, "核心武将");
            }
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
        /// 刷新相性显示。
        /// 自建武将模式下若存在来源武将 ID 则显示其姓名；
        /// Person 编辑模式或没有来源武将时，相性值大于 0 显示数值，为 0 时不显示任何内容。
        /// </summary>
        private void RefreshCompatibility()
        {
            if (compatibilityCancelButton != null)
                compatibilityCancelButton.interactable = snapshot.compatibility > 0;
            if (compatibilityText == null) return;

            // Person 编辑模式下不解析来源武将 ID，直接显示相性值
            if (editMode != PersonEditMode.Person)
            {
                int sourcePersonId = snapshot.compatibility >> 8;
                if (sourcePersonId > 0)
                {
                    string sourceName = GetCompatibilitySourceName(sourcePersonId);
                    if (!string.IsNullOrEmpty(sourceName))
                    {
                        compatibilityText.text = sourceName;
                        return;
                    }
                }
            }

            int value = snapshot.compatibility & 0xFF;
            compatibilityText.text = value > 0 ? value.ToString() : string.Empty;
        }

        /// <summary>
        /// 根据武将 Id 获取相性来源武将姓名。
        /// Person 编辑模式从 <see cref="Scenario.Cur.personSet"/> 查找；
        /// 自建武将模式从 <see cref="GameCustomEdit.CoreScenarioAddon"/> 查找。
        /// </summary>
        private string GetCompatibilitySourceName(int personId)
        {
            if (personId <= 0) return null;
            if (editMode == PersonEditMode.Person)
            {
                Person p = GetPersonById(personId);
                return p?.Name;
            }
            else
            {
                if (GameCustomEdit.Instance == null) return null;
                ScenarioAddon coreAddon = GameCustomEdit.Instance.CoreScenarioAddon;
                if (coreAddon == null || coreAddon.PersonLibrary == null) return null;
                PersonLib source = coreAddon.PersonLibrary.Find(p => p != null && p.Id == personId);
                return source?.Name;
            }
        }

        private void OnFatherSelectedIds(int[] ids)
        {
            snapshot.Father = ids != null && ids.Length > 0 ? ids[0] : 0;
            RefreshFather();
        }

        private void OnMotherSelectedIds(int[] ids)
        {
            snapshot.Mother = ids != null && ids.Length > 0 ? ids[0] : 0;
            RefreshMother();
        }

        private bool IsValidFatherFilterLib(PersonLib person)
        {
            if (person == null) return false;
            return person.sex == 0 && person.yearBorn <= snapshot.yearBorn - 15;
        }

        private bool IsValidFatherFilterPerson(Person person)
        {
            if (person == null) return false;
            return person.sex == 0 && person.yearBorn <= snapshot.yearBorn - 15;
        }

        private bool IsValidMotherFilterLib(PersonLib person)
        {
            if (person == null) return false;
            return person.sex == 1 && person.yearBorn <= snapshot.yearBorn - 15;
        }

        private bool IsValidMotherFilterPerson(Person person)
        {
            if (person == null) return false;
            return person.sex == 1 && person.yearBorn <= snapshot.yearBorn - 15;
        }

        private bool IsValidSpouseFilterLib(PersonLib person)
        {
            if (person == null) return false;
            return person.sex != snapshot.sex;
        }

        private bool IsValidSpouseFilterPerson(Person person)
        {
            if (person == null) return false;
            return person.sex != snapshot.sex;
        }

        private bool IsValidLikeFilterLib(PersonLib person)
        {
            if (person == null) return false;
            return !ContainsId(snapshot.HatePersonList, person.Id);
        }

        private bool IsValidLikeFilterPerson(Person person)
        {
            if (person == null) return false;
            return !ContainsId(snapshot.HatePersonList, person.Id);
        }

        private bool IsValidHateFilterLib(PersonLib person)
        {
            if (person == null) return false;
            if (person.Id == snapshot.Father || person.Id == snapshot.Mother) return false;
            if (ContainsId(snapshot.BrotherList, person.Id)) return false;
            if (ContainsId(snapshot.LikePersonList, person.Id)) return false;
            return true;
        }

        private bool IsValidHateFilterPerson(Person person)
        {
            if (person == null) return false;
            if (person.Id == snapshot.Father || person.Id == snapshot.Mother) return false;
            if (ContainsId(snapshot.BrotherList, person.Id)) return false;
            if (ContainsId(snapshot.LikePersonList, person.Id)) return false;
            return true;
        }

        private bool IsValidBrotherFilterLib(PersonLib person)
        {
            if (person == null) return false;
            if (person.Brother > 0) return false;
            PersonLib p = GameCustomEdit.Instance != null && GameCustomEdit.Instance.SelfScenarioAddon != null
                ? GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary.Find(x =>
                {
                    if (x.BrotherList != null)
                    {
                        for (int i = 0; i < x.BrotherList.Length; i++)
                        {
                            if (x.BrotherList[i] == person.Id)
                                return true;
                        }
                    }
                    return false;
                })
                : null;
            return p == null;
        }

        private bool IsValidBrotherFilterPerson(Person person)
        {
            if (person == null) return false;
            // Person 模式下先放宽兄弟过滤条件，避免复杂运行时关系校验影响编辑
            return true;
        }

        private void OnBrotherSelectedIds(int[] ids)
        {
            snapshot.BrotherList = ids != null ? ids.Distinct().ToArray() : new int[0];
            RefreshBrother();
        }

        private void OnSpouseSelectedIds(int[] ids)
        {
            snapshot.SpouseList = ids != null ? ids.Distinct().ToArray() : new int[0];
            RefreshSpouse();
        }

        private void OnLikeSelectedIds(int[] ids)
        {
            snapshot.LikePersonList = ids != null ? ids.Distinct().ToArray() : new int[0];
            RefreshLike();
        }

        private void OnHateSelectedIds(int[] ids)
        {
            snapshot.HatePersonList = ids != null ? ids.Distinct().ToArray() : new int[0];
            RefreshHate();
        }

        private int[] ConvertToIds(List<PersonLib> persons)
        {
            if (persons == null) return new int[0];
            return persons.Where(p => p != null).Select(p => p.Id).Distinct().ToArray();
        }

        private int[] ConvertToIds(List<Person> persons)
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
            SetPersonNamesText(brotherText, snapshot.BrotherList);
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
        /// 根据武将 Id 获取名称。
        /// Person 编辑模式从当前剧本查找，自建武将模式从全武将库查找。
        /// </summary>
        private string GetPersonName(int personId)
        {
            if (personId <= 0) return string.Empty;
            if (editMode == PersonEditMode.Person)
            {
                Person p = GetPersonById(personId);
                if (p != null) return p.Name;
            }
            else
            {
                PersonLib lib = GetPersonLibById(personId);
                if (lib != null) return lib.Name;
            }
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
            if (editMode == PersonEditMode.Person)
            {
                Log.Info("武将编辑已保存：" + snapshot.familyName + snapshot.giveName);
                Close();
            }
            else
            {
                Log.Info("新建武将已保存：" + snapshot.familyName + snapshot.giveName);
                //Back();
                Close();
                Window.Instance.Open("window_create_person_menu");
            }
        }

        private void OnBackClick()
        {
            // Back();
            Close();
            if (editMode != PersonEditMode.Person)
                Window.Instance.Open("window_create_person_menu");
        }

        private void OnCancelClick()
        {
            // Back();
            OnBackClick();
        }
        #endregion

        #region 剧本编辑页刷新与交互
        /// <summary>
        /// 道具下拉选项缓存：索引对应 Dropdown.value，值对应 ItemType.Id。
        /// </summary>
        private List<int> itemDropdownValues = new List<int>();

        /// <summary>
        /// 刷新剧本编辑页所有 UI 显示。
        /// </summary>
        private void RefreshScenarioPanel()
        {
            RefreshBelongForce();
            RefreshBelongCorps();
            RefreshBelongCity();
            RefreshOfficial();
            RefreshStateDropdown();
            RefreshArgumentationDropdown();
            RefreshBirthplaceDropdown();
            RefreshIdealDropdown();
            RefreshTalentDropdown();
            RefreshItemList();
        }

        private void RefreshBelongForce()
        {
            if (belongForceText != null) belongForceText.text = GetForceName(snapshot.BelongForce);
            if (belongForceCancelButton != null) belongForceCancelButton.interactable = snapshot.BelongForce > 0;
        }

        private void RefreshBelongCorps()
        {
            if (belongCorpsText != null) belongCorpsText.text = GetCorpsName(snapshot.BelongCorps);
            if (belongCorpsCancelButton != null) belongCorpsCancelButton.interactable = snapshot.BelongCorps > 0;
        }

        private void RefreshBelongCity()
        {
            if (belongCityText != null) belongCityText.text = GetCityName(snapshot.BelongCity);
            if (belongCityCancelButton != null) belongCityCancelButton.interactable = snapshot.BelongCity > 0;
        }

        private void RefreshOfficial()
        {
            if (officialText != null) officialText.text = GetOfficialName(snapshot.official);
            if (officialCancelButton != null) officialCancelButton.interactable = snapshot.official > 0;
        }

        private string GetForceName(int forceId)
        {
            if (forceId <= 0) return string.Empty;
            Scenario cur = Scenario.Cur;
            Force force = cur != null ? cur.forceSet.Get(forceId) : null;
            return force != null ? force.Name : forceId.ToString();
        }

        private string GetCorpsName(int corpsId)
        {
            if (corpsId <= 0) return string.Empty;
            Scenario cur = Scenario.Cur;
            Corps corps = cur != null ? cur.corpsSet.Get(corpsId) : null;
            return corps != null ? corps.Name : corpsId.ToString();
        }

        private string GetCityName(int cityId)
        {
            if (cityId <= 0) return string.Empty;
            Scenario cur = Scenario.Cur;
            City city = cur != null ? cur.citySet.Get(cityId) : null;
            return city != null ? city.Name : cityId.ToString();
        }

        private string GetOfficialName(int officialId)
        {
            if (officialId <= 0) return string.Empty;
            Scenario cur = Scenario.Cur;
            Official official = cur != null ? cur.CommonData.Officials.Get(officialId) : null;
            return official != null ? official.Name : officialId.ToString();
        }

        private void OpenBelongForceSelect()
        {
            if (refreshing) return;
            Scenario cur = Scenario.Cur;
            if (cur == null) return;
            GameSystem system = GameSystemManager.Instance.GetSystem<ForceSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 ForceSelectSystem");
                return;
            }
            ForceSelectSystem select = system as ForceSelectSystem;
            List<Force> forces = new List<Force>();
            foreach (Force f in cur.forceSet) { if (f != null) forces.Add(f); }
            Force selected = snapshot.BelongForce > 0 ? cur.forceSet.Get(snapshot.BelongForce) : null;
            select.Start(forces, selected != null ? new List<Force> { selected } : new List<Force>(), 1,
                (Action<List<Force>>)(result =>
                {
                    if (result == null || result.Count == 0) return;
                    snapshot.BelongForce = result[0] != null ? result[0].Id : 0;
                    RefreshBelongForce();
                }),
                new List<ObjectSortTitle> { ForceSortFunction.SortByName }, "全部势力");
        }

        private void OpenBelongCorpsSelect()
        {
            if (refreshing) return;
            Scenario cur = Scenario.Cur;
            if (cur == null) return;
            GameSystem system = GameSystemManager.Instance.GetSystem<CorpsSelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 CorpsSelectSystem");
                return;
            }
            CorpsSelectSystem select = system as CorpsSelectSystem;
            List<Corps> corpsList = new List<Corps>();
            foreach (Corps c in cur.corpsSet) { if (c != null) corpsList.Add(c); }
            Corps selected = snapshot.BelongCorps > 0 ? cur.corpsSet.Get(snapshot.BelongCorps) : null;
            select.Start(corpsList, selected != null ? new List<Corps> { selected } : new List<Corps>(), 1,
                (Action<List<Corps>>)(result =>
                {
                    if (result == null || result.Count == 0) return;
                    snapshot.BelongCorps = result[0] != null ? result[0].Id : 0;
                    RefreshBelongCorps();
                }),
                new List<ObjectSortTitle> { CorpsSortFunction.SortByName }, "全部军团");
        }

        private void OpenBelongCitySelect()
        {
            if (refreshing) return;
            Scenario cur = Scenario.Cur;
            if (cur == null) return;
            GameSystem system = GameSystemManager.Instance.GetSystem<CitySelectSystem>();
            if (system == null)
            {
                Log.Error("未找到 CitySelectSystem");
                return;
            }
            CitySelectSystem select = system as CitySelectSystem;
            List<City> cities = new List<City>();
            foreach (City c in cur.citySet) { if (c != null) cities.Add(c); }
            City selected = snapshot.BelongCity > 0 ? cur.citySet.Get(snapshot.BelongCity) : null;
            select.Start(cities, selected != null ? new List<City> { selected } : new List<City>(), 1,
                (Action<List<City>>)(result =>
                {
                    if (result == null || result.Count == 0) return;
                    snapshot.BelongCity = result[0] != null ? result[0].Id : 0;
                    RefreshBelongCity();
                }),
                new List<ObjectSortTitle> { CitySortFunction.SortByName }, "全部城市");
        }

        private void OpenOfficialSelect()
        {
            if (refreshing) return;
            Scenario cur = Scenario.Cur;
            if (cur == null || cur.CommonData.Officials == null) return;
            List<SangoObject> officials = new List<SangoObject>();
            cur.CommonData.Officials.ForEach(o => { if (o != null) officials.Add(o); });
            ObjectSelectSystem select = new ObjectSelectSystem();
            SangoObject selected = snapshot.official > 0 ? cur.CommonData.Officials.Get(snapshot.official) : null;
            List<SangoObject> initial = selected != null ? new List<SangoObject> { selected } : new List<SangoObject>();
            select.Start(officials, initial, 1,
                (Action<List<SangoObject>>)(result =>
                {
                    if (result == null || result.Count == 0) return;
                    snapshot.official = result[0] != null ? result[0].Id : 0;
                    RefreshOfficial();
                }),
                new List<ObjectSortTitle>(), "全部官职");
        }

        /// <summary>
        /// 绑定下拉菜单：options 与 values 一一对应，点击后通过 values 索引写入快照。
        /// </summary>
        private void BindDropdown(Dropdown dropdown, List<Dropdown.OptionData> options, List<int> values,
            Func<int> getter, Action<int> setter)
        {
            if (dropdown == null || options == null || values == null) return;
            dropdown.options = options;
            dropdown.onValueChanged.AddListener(index =>
            {
                if (refreshing) return;
                if (index >= 0 && index < values.Count)
                {
                    setter(values[index]);
                    // 重新同步显示，避免外部 setter 修正后索引不一致
                    SetDropdownValue(dropdown, values, getter());
                }
            });
        }

        /// <summary>
        /// 根据当前值设置 Dropdown 的显示索引。
        /// </summary>
        private void SetDropdownValue(Dropdown dropdown, List<int> values, int currentValue)
        {
            if (dropdown == null || values == null) return;
            int index = values.IndexOf(currentValue);
            if (index < 0) index = 0;
            dropdown.value = index;
            dropdown.RefreshShownValue();
        }

        private void RefreshStateDropdown()
        {
            if (stateDropdown == null) return;
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            List<int> values = new List<int>();
            GetStateOptions(options, values);
            if (stateDropdown.options.Count != options.Count) stateDropdown.options = options;
            SetDropdownValue(stateDropdown, values, snapshot.state);
        }

        private void RefreshArgumentationDropdown()
        {
            if (argumentationDropdown == null) return;
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            List<int> values = new List<int>();
            GetArgumentationOptions(options, values);
            if (argumentationDropdown.options.Count != options.Count) argumentationDropdown.options = options;
            SetDropdownValue(argumentationDropdown, values, snapshot.argumentation);
        }

        private void RefreshBirthplaceDropdown()
        {
            if (birthplaceDropdown == null) return;
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            List<int> values = new List<int>();
            GetBirthplaceOptions(options, values);
            if (birthplaceDropdown.options.Count != options.Count) birthplaceDropdown.options = options;
            SetDropdownValue(birthplaceDropdown, values, snapshot.birthplace);
        }

        private void RefreshIdealDropdown()
        {
            if (idealDropdown == null) return;
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            List<int> values = new List<int>();
            GetIdealOptions(options, values);
            if (idealDropdown.options.Count != options.Count) idealDropdown.options = options;
            SetDropdownValue(idealDropdown, values, snapshot.ideal);
        }

        private void RefreshTalentDropdown()
        {
            if (talentDropdown == null) return;
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            List<int> values = new List<int>();
            GetTalentOptions(options, values);
            if (talentDropdown.options.Count != options.Count) talentDropdown.options = options;
            SetDropdownValue(talentDropdown, values, snapshot.talent);
        }

        private void GetStateOptions(List<Dropdown.OptionData> options, List<int> values)
        {
            if (options == null || values == null) return;
            AddOption(options, values, "君主", (int)PersonStateType.Governor);
            AddOption(options, values, "都督", (int)PersonStateType.Commander);
            AddOption(options, values, "太守", (int)PersonStateType.Leader);
            AddOption(options, values, "一般", (int)PersonStateType.Normal);
            AddOption(options, values, "在野", (int)PersonStateType.Unemployed);
            AddOption(options, values, "俘虏", (int)PersonStateType.Prisoner);
            AddOption(options, values, "未登场", (int)PersonStateType.Invalid);
            AddOption(options, values, "未发现", (int)PersonStateType.Invisible);
            AddOption(options, values, "已死亡", (int)PersonStateType.Dead);
        }

        private void GetArgumentationOptions(List<Dropdown.OptionData> options, List<int> values)
        {
            if (options == null || values == null) return;
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData.Argumentations != null)
            {
                cur.CommonData.Argumentations.ForEach(arg =>
                {
                    if (arg != null) AddOption(options, values, arg.Name, arg.Id);
                });
            }
            if (options.Count == 0) AddOption(options, values, "—", 0);
        }

        private void GetBirthplaceOptions(List<Dropdown.OptionData> options, List<int> values)
        {
            if (options == null || values == null) return;
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData.Provinces != null)
            {
                cur.CommonData.Provinces.ForEach(prov =>
                {
                    if (prov != null) AddOption(options, values, prov.Name, prov.Id);
                });
            }
            if (options.Count == 0) AddOption(options, values, "—", 0);
        }

        private void GetIdealOptions(List<Dropdown.OptionData> options, List<int> values)
        {
            if (options == null || values == null) return;
            AddOption(options, values, "霸道", 0);
            AddOption(options, values, "王道", 1);
            AddOption(options, values, "我道", 2);
            AddOption(options, values, "割據", 3);
            AddOption(options, values, "義俠", 4);
        }

        private void GetTalentOptions(List<Dropdown.OptionData> options, List<int> values)
        {
            if (options == null || values == null) return;
            AddOption(options, values, "王佐", 0);
            AddOption(options, values, "出世", 1);
            AddOption(options, values, "安全", 2);
            AddOption(options, values, "隱遁", 3);
        }

        private void AddOption(List<Dropdown.OptionData> options, List<int> values, string text, int value)
        {
            options.Add(new Dropdown.OptionData(text));
            values.Add(value);
        }

        /// <summary>
        /// 初始化道具下拉菜单选项。
        /// </summary>
        private void InitItemDropdown()
        {
            if (itemDropdown == null) return;
            itemDropdownValues.Clear();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData.ItemTypes != null)
            {
                cur.CommonData.ItemTypes.ForEach(itemType =>
                {
                    if (itemType != null)
                    {
                        options.Add(new Dropdown.OptionData(itemType.Name));
                        itemDropdownValues.Add(itemType.storeKind);
                    }
                });
            }
            if (options.Count == 0) options.Add(new Dropdown.OptionData("—"));
            itemDropdown.options = options;
            itemDropdown.value = 0;
            itemDropdown.RefreshShownValue();
        }

        /// <summary>
        /// 道具添加按钮：读取当前选中的道具类型与数量，加入快照的 itemStore。
        /// </summary>
        private void OnItemAddClick()
        {
            if (refreshing) return;
            if (itemDropdown == null || itemDropdownValues.Count == 0) return;
            int index = itemDropdown.value;
            if (index < 0 || index >= itemDropdownValues.Count) return;
            int storeKind = itemDropdownValues[index];
            int count = 1;
            if (itemCountInput != null && int.TryParse(itemCountInput.text, out int inputCount))
                count = System.Math.Max(1, inputCount);
            if (snapshot.itemStore == null) snapshot.itemStore = new ItemStore();
            snapshot.itemStore.Add(storeKind, count);
            Log.Info("已添加道具：" + itemDropdown.options[index].text + " × " + count);
            RefreshItemList();
        }

        /// <summary>
        /// 道具清空按钮：清空快照中所有道具。
        /// </summary>
        private void OnItemClearClick()
        {
            if (refreshing) return;
            if (snapshot.itemStore != null) snapshot.itemStore.Clear();
            Log.Info("已清空道具");
            RefreshItemList();
        }

        /// <summary>
        /// 刷新道具列表显示。
        /// </summary>
        private void RefreshItemList()
        {
            if (itemListText == null) return;
            if (snapshot.itemStore == null || snapshot.itemStore.Items.Count == 0)
            {
                itemListText.text = string.Empty;
                return;
            }
            List<string> lines = new List<string>();
            foreach (int storeKind in snapshot.itemStore.Items.Keys)
            {
                int number = snapshot.itemStore.Items[storeKind];
                if (number <= 0) continue;
                string itemName = GetItemTypeName(storeKind);
                lines.Add(itemName + " × " + number);
            }
            itemListText.text = string.Join("\n", lines.ToArray());
        }

        private string GetItemTypeName(int storeKind)
        {
            Scenario cur = Scenario.Cur;
            ItemType itemType = cur != null ? cur.CommonData.ItemTypes.Get(storeKind) : null;
            return itemType != null ? itemType.Name : storeKind.ToString();
        }
        #endregion

    }
}
