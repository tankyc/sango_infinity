using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 新建武将详情编辑窗口。
    /// 提供“基本设定”与“能力设定”两个标签页，用于编辑 <see cref="PersonLib"/> 数据。
    /// </summary>
    public class UIPersonCreateAuto : UGUIWindow
    {
        UIPersonCreateDetail.Snapshot snapshot = new UIPersonCreateDetail.Snapshot();

        public GameObject[] pages;

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
        /// 性别 Toggle 组（男/女）
        /// </summary>
        public Toggle[] sexToggles = new Toggle[2];

        /// <summary>
        /// 变更容貌按钮
        /// </summary>
        public Button changeImageButton;
        public Button changeNameButton;
        #endregion


        #region 基本设定 - 性格与相性
        /// <summary>
        /// 性格 Toggle 组（胆小/冷静/刚胆/莽撞）
        /// </summary>
        public Toggle[] personalityToggles = new Toggle[4];

        #endregion

        #region 能力设定 - 基准能力
        public Button heroButton;
        public Button counsellorButton;
        public Button generalButton;
        public Button mediocrityButton;

        /// <summary>
        /// 统率值显示文本
        /// </summary>
        public Text commandText;

        /// <summary>
        /// 武力值显示文本
        /// </summary>
        public Text strengthText;

        /// <summary>
        /// 智力值显示文本
        /// </summary>
        public Text intelligenceText;

        /// <summary>
        /// 政治值显示文本
        /// </summary>
        public Text politicsText;

        /// <summary>
        /// 魅力值显示文本
        /// </summary>
        public Text glamourText;

        /// <summary>
        /// 能力合计显示文本
        /// </summary>
        public Text abilityTotalText;
        #endregion

        bool refreshing;
        int curTabIndex = 0;

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
            GameRandom.Init();
            snapshot.sex = GameRandom.Range(0, 2);
            snapshot.personality = GameRandom.Range(1, 5);
   
            SwitchTab(0);
            OnChangeNameClick();
            OnSexChange();
            int t = GameRandom.Range(0, 4);
            switch (t)
            {
                case 0:
                    OnHeroClick();
                    break;
                case 1:
                    OnCounsellorClick();
                    break;
                case 2:
                    OnGeneralClick();
                    break;
                case 3:
                    OnMediocrityClick();
                    break;
            }

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
            snapshot = new UIPersonCreateDetail.Snapshot();
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
            // 姓名与列传（姓和名变化时触发确认按钮校验）
            BindTextInput(familyNameInput, () => snapshot.familyName, v => snapshot.familyName = v);
            BindTextInput(giveNameInput, () => snapshot.giveName, v => snapshot.giveName = v);
            BindTextInput(nickNameInput, () => snapshot.nickName, v => snapshot.nickName = v);

            // 性别（0=男，1=女），性别变化时检查配偶性别与音声值是否仍合法
            BindToggleGroup(sexToggles, () => snapshot.sex, v => snapshot.sex = v, i => i, v => v, OnSexChange);

            // 性格与相性
            BindToggleGroup(personalityToggles, () => snapshot.personality, v => snapshot.personality = v, i => i + 1, v => v - 1);

            if (heroButton != null) heroButton.onClick.AddListener(OnHeroClick);
            if (counsellorButton != null) counsellorButton.onClick.AddListener(OnCounsellorClick);
            if (generalButton != null) generalButton.onClick.AddListener(OnGeneralClick);
            if (mediocrityButton != null) mediocrityButton.onClick.AddListener(OnMediocrityClick);

            // 头像与造型
            if (changeImageButton != null) changeImageButton.onClick.AddListener(OnChangeImageClick);
            if (changeNameButton != null) changeNameButton.onClick.AddListener(OnChangeNameClick);

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

                RefreshToggleGroup(sexToggles, snapshot.sex, i => i, 0);
                RefreshToggleGroup(personalityToggles, snapshot.personality, i => i - 1, 1);
                RefreshStatus();
            }
            finally
            {
                refreshing = false;
            }
        }

        void RefreshStatus()
        {
            if (commandText != null) commandText.text = snapshot.command.ToString();
            if (strengthText != null) strengthText.text = snapshot.strength.ToString();
            if (intelligenceText != null) intelligenceText.text = snapshot.intelligence.ToString();
            if (politicsText != null) politicsText.text = snapshot.politics.ToString();
            if (glamourText != null) glamourText.text = snapshot.glamour.ToString();
            OnAbilityChanged();
        }

        /// <summary>
        /// 切换标签页并同步 Toggle 状态。
        /// </summary>
        /// <param name="isBasic">true=基本设定，false=能力设定</param>
        private void SwitchTab(int index)
        {
            curTabIndex = index;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
            }
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
        }

        /// <summary>
        /// 判断 ID 数组是否包含指定武将 Id。
        /// </summary>
        private bool ContainsId(int[] ids, int personId)
        {
            if (ids == null || ids.Length == 0) return false;
            return System.Array.IndexOf(ids, personId) >= 0;
        }
        #endregion

        #region 通用绑定助手
      
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

        #region 头像与造型
        private void OnChangeImageClick()
        {
            Log.Info("打开头像选择窗口");
            Window.Instance.Open("window_create_person_image", snapshot.headIconID, (Action<int>)((headId) =>
            {
                snapshot.headIconID = headId;
                RefreshImage();
                // 容貌ID变更后刷新确认按钮状态
            }));
        }

        private void OnChangeNameClick()
        {
            AutoName.Instance.Init();
            snapshot.familyName = AutoName.Instance.GetRandomFirstName();
            // 根据性别从对应的名库随机（sex：0=男，1=女）
            snapshot.giveName = AutoName.Instance.GetRandomGivingName(snapshot.sex == 0);
            if (familyNameInput != null) familyNameInput.text = snapshot.familyName;
            if (giveNameInput != null) giveNameInput.text = snapshot.giveName;
        }
        #endregion

        #region 底部按钮事件
        public void OnConfirmClick()
        {
            curTabIndex++;
            if (curTabIndex >= pages.Length)
            {
                Close();
                Window.Instance.Open("window_create_person", snapshot);
            }
            else
            {
                SwitchTab(curTabIndex);
            }
        }

        public void OnCancelClick()
        {
            curTabIndex--;
            if(curTabIndex < 0)
            {
                Close();
                Window.Instance.Open("window_create_person_menu");
            }
            else
            {
                SwitchTab(curTabIndex);
            }
        }
        #endregion

        void OnSexChange()
        {
            CheckVoiceValid();
            int headIndex;
            if (snapshot.sex == 0)
            {
                headIndex = GameRandom.Range(0, GameCustomEdit.Instance.femaleStartIndex);
            }
            else
            {
                headIndex = GameRandom.Range(GameCustomEdit.Instance.femaleStartIndex, GameCustomEdit.Instance.headDataList.Count);
            }

            snapshot.headIconID = GameCustomEdit.Instance.headDataList[headIndex];
            RefreshImage();
        }

        private void OnHeroClick()
        {
            snapshot.command = GameRandom.Range(55, 90);
            snapshot.strength = GameRandom.Range(75, 100);
            snapshot.intelligence = GameRandom.Range(30, 80);
            snapshot.politics = GameRandom.Range(15, 70);
            snapshot.glamour = GameRandom.Range(30, 70);
            RefreshStatus();
        }
        private void OnCounsellorClick()
        {
            snapshot.command = GameRandom.Range(55, 95);
            snapshot.strength = GameRandom.Range(30, 70);
            snapshot.intelligence = GameRandom.Range(30, 100);
            snapshot.politics = GameRandom.Range(15, 90);
            snapshot.glamour = GameRandom.Range(30, 80);
            RefreshStatus();
        }
        private void OnGeneralClick()
        {
            snapshot.command = GameRandom.Range(70, 99);
            snapshot.strength = GameRandom.Range(70, 90);
            snapshot.intelligence = GameRandom.Range(70, 80);
            snapshot.politics = GameRandom.Range(15, 80);
            snapshot.glamour = GameRandom.Range(30, 70);
            RefreshStatus();
        }
        private void OnMediocrityClick()
        {
            snapshot.command = GameRandom.Range(25, 70);
            snapshot.strength = GameRandom.Range(25, 70);
            snapshot.intelligence = GameRandom.Range(30, 70);
            snapshot.politics = GameRandom.Range(15, 70);
            snapshot.glamour = GameRandom.Range(30, 70);
            RefreshStatus();
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
            Log.Warning("性别切换后音声值不在有效范围内，已修正为默认值：" + snapshot.voice);
        }
    }
}
