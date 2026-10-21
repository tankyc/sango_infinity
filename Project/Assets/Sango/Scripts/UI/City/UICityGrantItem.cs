using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 都市人事「授予宝物 / 没收宝物」窗口（prefab：<c>Assets/UI/Prefab/window_item_grant.prefab</c>）。
    ///
    /// 【布局】
    ///   左：珍品列表（授予 = 国库珍品；没收 = 当前武将身上的珍品）
    ///   中：选中珍品的详情（图标 / 品级 / 五维加成 / 特效 / 说明）+ 技巧点结算预览
    ///   右：目标武将（头像、姓名、忠诚、当前装备），点"选择武将"走既有的 PersonSelectSystem
    ///   底：技巧点消耗 / 余额、不能确认的原因 / 软提示、确认 / 取消
    ///
    /// 【连续操作】确认后窗口不关闭（见 CityItemGrantBase.DoJob），
    ///   玩家可以在同一回合里依次给多名武将调整装备，每件扣一次技巧点。
    ///
    /// 【为什么两个 Job 共用一个窗口】
    ///   授予与没收界面完全同构，只有"左侧列表的来源"与"忠诚变化方向"相反。
    ///   窗口在 OnOpen 时从 <see cref="GameSystemManager.CurrentCommand"/> 反查当前是哪个系统
    ///   （两个系统都继承 <see cref="CityItemGrantBase"/>），由 <c>IsGrantMode</c> 分流。
    ///
    /// 【职责边界】
    ///   窗口只显示 <see cref="CityItemGrantBase"/> 里的现成状态（列表、预览、拒绝原因），
    ///   自己不判断规则、不修改任何游戏数据；确认时只调 <c>DoJob()</c>。
    /// </summary>
    public class UICityGrantItem : UGUIWindow
    {
        /// <summary>窗口标题（授予宝物 / 没收宝物）</summary>
        public Text windowTitle;

        /// <summary>左侧列表来源提示（"国库珍品" / "该武将的宝物与装备"）</summary>
        public Text itemListTip;

        /// <summary>左侧珍品格模板（须挂 Button，点击走 UIBuildingTypeItem.onSelected）</summary>
        public UIBuildingTypeItem itemCellTemplate;

        /// <summary>左侧珍品列表的滚动区（用于列表为空时隐藏）</summary>
        public ScrollRect itemScrollRect;

        /// <summary>左侧列表为空时的提示文案</summary>
        public Text emptyItemLabel;

        // ==================== 中间：珍品详情 ====================

        /// <summary>珍品大图标</summary>
        public RawImage detailIcon;
        /// <summary>珍品名称（带品级颜色）</summary>
        public Text detailName;
        /// <summary>品级星级（★★★★★）</summary>
        public Text detailGrade;
        /// <summary>五维加成</summary>
        public Text detailBonus;
        /// <summary>附加特技（features）</summary>
        public Text detailFeature;
        /// <summary>道具说明（desc）</summary>
        public Text detailDesc;
        /// <summary>忠诚变化预览，形如 "78 → 86 (+8)"</summary>
        public Text loyaltyChangeLabel;

        // ==================== 右：目标武将 ====================

        /// <summary>目标武将格（头像 / 姓名 / 特技）</summary>
        public UIPersonItem targetPersonItem;

        /// <summary>打开武将选择器的按钮（来自 personStatus_selectBtn 预制体）</summary>
        public Button targetPersonSelectBtn;
        /// <summary>目标武将忠诚</summary>
        public Text targetPersonLoyalty;
        /// <summary>目标武将当前装备概览</summary>
        public Text targetPersonEquip;

        // ==================== 底：消耗与确认 ====================

        /// <summary>行动力消耗显示</summary>
        public UITextField action_value;
        /// <summary>不能确认的原因（红字）</summary>
        public Text blockReasonLabel;
        /// <summary>软提示（黄字，不阻止确认）</summary>
        public Text hintLabel;
        public Button sureButton;
        public Button cancelButton;

        /// <summary>当前正在操作的授予 / 没收系统</summary>
        CityItemGrantBase currentSystem;

        /// <summary>珍品格对象池（模板即 itemCellTemplate，实例挂在模板的父节点下）</summary>
        CreatePool<UIBuildingTypeItem> itemPool;

        protected override void Awake()
        {
            base.Awake();
            if (itemCellTemplate != null)
                itemPool = new CreatePool<UIBuildingTypeItem>(itemCellTemplate);
        }

        public override void OnOpen()
        {
            currentSystem = GameSystemManager.Instance.CurrentCommand as CityItemGrantBase;
            if (currentSystem == null)
            {
                Sango.Log.Warning("window_item_grant 打开时当前系统不是授予 / 没收系统，窗口将关闭。");
                Close();
                return;
            }

            if (windowTitle != null)
                windowTitle.text = currentSystem.customTitleName;

            if (itemListTip != null)
                itemListTip.text = currentSystem.IsGrantMode ? "国库珍品" : "该武将的宝物与装备";

            BindButtons();
            RefreshAll();
        }

        /// <summary>
        /// 绑定按钮事件。
        /// 走运行时绑定而不是预制体里的持久监听：两个 Job 共用一个窗口，
        /// 窗口每次都从 CurrentCommand 取当前系统，运行期绑定最不容易错；
        /// 先 RemoveAllListeners 避免重复 Open 时叠加回调。
        /// </summary>
        void BindButtons()
        {
            if (sureButton != null)
            {
                sureButton.onClick.RemoveAllListeners();
                sureButton.onClick.AddListener(OnSure);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancel);
            }

            if (targetPersonSelectBtn != null)
            {
                targetPersonSelectBtn.onClick.RemoveAllListeners();
                targetPersonSelectBtn.onClick.AddListener(OnSelectTargetPerson);
            }
        }

        /// <summary>整窗刷新：列表 → 详情 → 武将格 → 底部</summary>
        public void RefreshAll()
        {
            if (currentSystem == null)
                return;

            RefreshItemList();
            RefreshDetail();
            RefreshTargetPerson();
            RefreshBottom();
        }

        /// <summary>重建左侧珍品列表（授予 = 国库；没收 = 当前武将身上）</summary>
        void RefreshItemList()
        {
            if (itemPool == null)
                return;

            itemPool.Reset();
            List<ItemType> items = currentSystem.itemList;
            bool hasItem = items != null && items.Count > 0;

            if (itemScrollRect != null)
                itemScrollRect.gameObject.SetActive(hasItem);
            if (emptyItemLabel != null)
                emptyItemLabel.gameObject.SetActive(!hasItem);

            if (!hasItem)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                ItemType item = items[i];
                if (item == null)
                    continue;

                UIBuildingTypeItem cell = itemPool.Create();
                cell.onSelected = OnSelectItem;
                // 珍品每件唯一，不显示数量；titleObj 是"每 4 个换行"的分组标题，这里不需要
                cell.SetItemType(item).SetIndex(i).SetSelected(item == currentSystem.selectedItem);
                if (cell.titleObj != null)
                    cell.titleObj.SetActive(false);
                cell.SetValid(true);
            }
        }

        /// <summary>刷新中间详情区（无选中珍品时整体隐藏）</summary>
        void RefreshDetail()
        {
            ItemType item = currentSystem.selectedItem;
            bool has = item != null;

            if (detailName != null)
                detailName.text = has ? item.ColorName : "";
            if (detailGrade != null)
                detailGrade.text = has ? new string('★', Mathf.Clamp(item.grade, 0, 5)) : "";
            if (detailBonus != null)
                detailBonus.text = has ? BuildBonusText(item) : "";
            if (detailFeature != null)
                detailFeature.text = has ? BuildFeatureText(item) : "";
            if (detailDesc != null)
                detailDesc.text = has ? (string.IsNullOrEmpty(item.desc) ? "" : item.desc) : "";
            if (detailIcon != null)
            {
                detailIcon.enabled = has;
                detailIcon.texture = has ? GameRenderHelper.LoadItemIcon(item.icon) : null;
            }
        }

        /// <summary>刷新右侧目标武将格</summary>
        void RefreshTargetPerson()
        {
            Person person = currentSystem.TargetPerson;

            if (targetPersonItem != null)
                targetPersonItem.SetPerson(person);

            if (targetPersonLoyalty != null)
                targetPersonLoyalty.text = person != null ? $"忠诚 {person.loyalty}" : "";

            if (targetPersonEquip != null)
                targetPersonEquip.text = person != null ? BuildEquipText(person) : "";
        }

        /// <summary>刷新忠诚预览与底部按钮</summary>
        void RefreshBottom()
        {
            Person person = currentSystem.TargetPerson;
            ItemType item = currentSystem.selectedItem;

            if (action_value != null)
            {
                // 成本已从行动力改为技巧点（见 CityItemGrantBase 类注释）
                action_value.text = $"{currentSystem.TechniqueCost}/{currentSystem.TechniquePoint}";
            }

            if (loyaltyChangeLabel != null)
            {
                // 字段名沿用旧 prefab 绑定（原"忠诚变化预览"），现用于展示技巧点结算预览。
                // 改名会断掉预制体的序列化引用，所以不改字段名，只改内容。
                if (person == null || item == null || !currentSystem.CanConfirm)
                {
                    loyaltyChangeLabel.text = "";
                }
                else
                {
                    int cost = currentSystem.TechniqueCost;
                    int before = currentSystem.TechniquePoint;
                    int after = before - cost;
                    if (after < 0) after = 0;

                    loyaltyChangeLabel.text = cost > 0
                        ? $"技巧点 {before} → {after} <color=#FF6B5A>(-{cost})</color>"
                        : "本次不消耗技巧点";
                }
            }

            if (blockReasonLabel != null)
            {
                bool blocked = !string.IsNullOrEmpty(currentSystem.blockReason);
                blockReasonLabel.gameObject.SetActive(blocked);
                blockReasonLabel.text = blocked ? currentSystem.blockReason : "";
            }

            if (hintLabel != null)
            {
                bool hasHint = !string.IsNullOrEmpty(currentSystem.hintText);
                hintLabel.gameObject.SetActive(hasHint);
                hintLabel.text = hasHint ? currentSystem.hintText : "";
            }

            if (sureButton != null)
                sureButton.interactable = currentSystem.CanConfirm;
        }

        /// <summary>左侧珍品格点击</summary>
        public void OnSelectItem(UIBuildingTypeItem cell)
        {
            if (currentSystem == null || cell == null)
                return;

            int index = cell.index;
            if (index < 0 || index >= currentSystem.itemList.Count)
                return;

            currentSystem.SetItem(currentSystem.itemList[index]);
            RefreshAll();
        }

        /// <summary>
        /// 打开武将选择器。
        /// 复用通用对象选择系统（与兵装生产同一套），限本城可操作武将、单选。
        /// </summary>
        public void OnSelectTargetPerson()
        {
            if (currentSystem == null || currentSystem.targetList.Count == 0)
                return;

            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.targetList,
                currentSystem.personList, 1, OnTargetPersonChange,
                currentSystem.customTitleList, currentSystem.customTitleName);
        }

        /// <summary>武将选择回调</summary>
        /// <param name="personList">已被选中的武将（最多 1 个）</param>
        public void OnTargetPersonChange(List<Person> personList)
        {
            if (currentSystem == null)
                return;

            currentSystem.SetTargetPerson(personList != null && personList.Count > 0 ? personList[0] : null);
            RefreshAll();
        }

        /// <summary>确认执行（连续操作：成功后窗口不关闭，只刷新）</summary>
        public void OnSure()
        {
            if (currentSystem == null || !currentSystem.CanConfirm)
                return;

            // DoJob 内部不再 Done()，执行成功后由这里刷新整窗：
            // 左侧列表（国库 / 武将身上已变）、右侧装备概览、底部技巧点余额
            currentSystem.DoJob();
            RefreshAll();
        }

        /// <summary>取消（退出当前工作）</summary>
        public void OnCancel()
        {
            if (currentSystem == null)
            {
                Close();
                return;
            }

            currentSystem.Exit();
        }

        // ==================== 文本拼装 ====================

        /// <summary>五维加成文本（只列非 0 项；全部为 0 时显示"无"）</summary>
        static string BuildBonusText(ItemType item)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (item.commandBonus != 0) sb.Append($"统率 {Format(item.commandBonus)}   ");
            if (item.strengthBonus != 0) sb.Append($"武力 {Format(item.strengthBonus)}   ");
            if (item.intelligenceBonus != 0) sb.Append($"智力 {Format(item.intelligenceBonus)}   ");
            if (item.politicsBonus != 0) sb.Append($"政治 {Format(item.politicsBonus)}   ");
            if (item.glamourBonus != 0) sb.Append($"魅力 {Format(item.glamourBonus)}   ");

            if (sb.Length == 0)
                return "无能力加成";

            return sb.ToString().TrimEnd();
        }

        /// <summary>附加特技文本（features 未配时显示"无"）</summary>
        static string BuildFeatureText(ItemType item)
        {
            Sango.Core.SangoObjectList<Feature> features = item.features;
            if (features == null || features.Count == 0)
                return "无附加特技";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < features.Count; i++)
            {
                Feature feature = features[i];
                if (feature == null)
                    continue;
                if (sb.Length > 0)
                    sb.Append("、");
                sb.Append(feature.Name);
            }

            return sb.Length == 0 ? "无附加特技" : sb.ToString();
        }

        /// <summary>目标武将当前装备概览</summary>
        static string BuildEquipText(Person person)
        {
            return $"武器：{NameOf(person.EquippedWeapon)}   铠甲：{NameOf(person.EquippedArmor)}\n" +
                   $"马匹：{NameOf(person.EquippedHorse)}   宝物：{NameOf(person.EquippedTreasure)}";
        }

        /// <summary>取道具名；为空时显示"－"</summary>
        static string NameOf(ItemType item)
        {
            return item != null ? item.Name : "－";
        }

        /// <summary>带正负号显示加成值</summary>
        static string Format(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString();
        }
    }
}
