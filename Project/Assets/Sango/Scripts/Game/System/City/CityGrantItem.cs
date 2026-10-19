using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 都市人事「授予宝物 / 没收宝物」的**玩家侧公共基类**。
    ///
    /// 【职责边界】
    ///   本类只做三件事：登记菜单、维护窗口需要的两个候选列表（本城武将 / 珍品）、
    ///   在确认时把活派给数据侧（<see cref="City.JobGrantItem"/> /
    ///   <see cref="City.JobConfiscateItem"/>）。
    ///   道具、忠诚、特效的所有状态变更都在 <see cref="ItemGrantRules"/> 里，
    ///   本类绝不直接改国库 / 装备栏，否则窗口预览与实际结算会分叉。
    ///
    /// 【为什么两个 Job 共用一个窗口】
    ///   授予与没收是同一块界面的两个方向（左列表换来处、右列表选人），
    ///   共用 <c>window_item_grant</c> 与 <c>UICityGrantItem</c>，
    ///   由 <see cref="IsGrantMode"/> 切换模式，避免两份几乎一样的 prefab 与脚本。
    ///
    /// 【候选口径（与规则层完全一致）】
    ///   · 武将：<see cref="ItemGrantRules.IsGrantTarget"/> —— 本城在职、未随军出征
    ///   · 授予珍品：本势力国库里的全部珍品
    ///   · 没收珍品：**当前选中武将**身上的珍品（换人会重建列表）
    ///   IsValid（菜单置灰）与 OnEnter（进窗口）复用同一批判定，避免出现
    ///   "菜单能点、进去发现没东西可操作"。
    ///
    /// 【行动力与回合限制】
    ///   消耗与次数上限读 JobTypes.json 的 28 / 29 号行（costAP = 5、每势力每回合 1 次），
    ///   不占用被授予武将本人的行动（该武将当回合仍可内政 / 出征）。
    /// </summary>
    public abstract class CityItemGrantBase : CityBaseSystem
    {
        /// <summary>
        /// 左侧候选珍品列表。
        /// 授予模式 = 国库全部珍品；没收模式 = 当前选中武将身上的珍品。
        /// </summary>
        public List<ItemType> itemList = new List<ItemType>();

        /// <summary>本城可操作武将（右侧武将列表的候选）</summary>
        public List<Person> targetList = new List<Person>();

        /// <summary>当前选中的珍品；为空表示左侧没有任何可操作道具</summary>
        public ItemType selectedItem;

        /// <summary>
        /// 当前选中的珍品将带来的忠诚变化（正 = 提升、负 = 下降），供窗口显示"78 → 86 (+8)"。
        /// 未选中或不能操作时为 0。
        /// </summary>
        public int loyaltyPreview;

        /// <summary>
        /// 硬性不能确认时的中文原因（可确认时为空串）。
        /// 窗口用它决定确认按钮是否可点，并可直接当提示文案显示。
        /// </summary>
        public string blockReason = ItemGrantRules.ReasonNone;

        /// <summary>
        /// 软性提示文案（不阻止确认），例如"忠诚已满，本次授予没有额外收益"。
        /// </summary>
        public string hintText = "";

        /// <summary>本系统对应的城市工作类型（决定行动力消耗与每回合次数上限）</summary>
        public abstract CityJobType GrantJobType { get; }

        /// <summary>true = 授予（国库 → 武将）；false = 没收（武将 → 国库）</summary>
        public abstract bool IsGrantMode { get; }

        /// <summary>当前选中的武将（personList 的首个元素）</summary>
        public Person TargetPerson => personList.Count > 0 ? personList[0] : null;

        /// <summary>当前是否可以确认执行</summary>
        public bool CanConfirm => selectedItem != null && TargetPerson != null
            && string.IsNullOrEmpty(blockReason);

        protected override bool MenuCanShow()
        {
            // 都市 / 关 / 港都可以行使人事权
            return TargetCity.IsCityBase();
        }

        public override bool IsValid
        {
            get
            {
                if (!TargetCity.IsCityBase())
                    return false;

                Corps corps = TargetCity.BelongCorps;
                Force force = TargetCity.BelongForce;
                if (corps == null || force == null)
                    return false;

                // 每势力每回合各 1 次（批次计数由 JobTypes.json 的 limit 与 jobCounter 共同约束）
                if (corps.GetJobCounter((int)GrantJobType) > 0)
                    return false;

                if (corps.ActionPoint < JobType.GetJobCostAP((int)GrantJobType))
                    return false;

                if (!TargetCity.CheckJobCost(GrantJobType))
                    return false;

                if (IsGrantMode)
                {
                    // 授予：国库要有珍品、本城要有武将
                    return HasTreasuryItem() && HasTargetPerson();
                }

                // 没收：本城要有武将身上带珍品；国库还要放得下（放不下直接置灰，别让玩家白跑）
                return force.CanAddTreasury(1) && HasConfiscatableItem();
            }
        }

        /// <summary>国库里是否有珍品（授予模式的置灰判据）</summary>
        bool HasTreasuryItem()
        {
            return TargetCity.BelongForce.Treasury != null
                && TargetCity.BelongForce.Treasury.TotalNumber > 0;
        }

        /// <summary>本城是否存在可授予 / 没收的武将</summary>
        bool HasTargetPerson()
        {
            for (int i = 0; i < TargetCity.allPersons.Count; i++)
            {
                if (ItemGrantRules.IsGrantTarget(TargetCity.allPersons[i], TargetCity))
                    return true;
            }
            return false;
        }

        /// <summary>本城是否有武将身上带着可没收的珍品（没收模式的置灰判据）</summary>
        bool HasConfiscatableItem()
        {
            List<ItemType> temp = new List<ItemType>();
            for (int i = 0; i < TargetCity.allPersons.Count; i++)
            {
                Person person = TargetCity.allPersons[i];
                if (!ItemGrantRules.IsGrantTarget(person, TargetCity))
                    continue;

                ItemGrantRules.GetConfiscatableItems(person, temp);
                if (temp.Count > 0)
                    return true;
            }
            return false;
        }

        public override void OnEnter()
        {
            // 先备好武将候选，再交给基类：基类会清 personList → RecommandPersonList → 打开窗口
            targetList.Clear();
            for (int i = 0; i < TargetCity.allPersons.Count; i++)
            {
                Person person = TargetCity.allPersons[i];
                if (ItemGrantRules.IsGrantTarget(person, TargetCity))
                    targetList.Add(person);
            }
            targetList.Sort((a, b) => PersonSortFunction.SortByName.valueSortFunc.Invoke(a, b));

            base.OnEnter();
        }

        /// <summary>
        /// 默认选中：武将取列表首个，珍品按模式重建列表并取首个。
        /// 由基类 OnEnter 调用。
        /// </summary>
        public override void RecommandPersonList()
        {
            personList.Clear();
            if (targetList.Count > 0)
                personList.Add(targetList[0]);

            RefreshItemList();
        }

        /// <summary>
        /// 重建左侧候选珍品列表并重置选中项。
        /// 授予 = 国库全部珍品；没收 = 当前选中武将身上的珍品（所以换人后必须调本方法）。
        /// 排序：高品级优先（珍品越珍贵越靠前），同品级按 Id 稳定排序。
        /// </summary>
        public void RefreshItemList()
        {
            if (IsGrantMode)
                ItemGrantRules.GetGrantItemList(TargetCity.BelongForce, itemList);
            else
                ItemGrantRules.GetConfiscatableItems(TargetPerson, itemList);

            itemList.Sort((a, b) =>
            {
                if (a.grade != b.grade)
                    return b.grade.CompareTo(a.grade);
                return a.Id.CompareTo(b.Id);
            });

            selectedItem = itemList.Count > 0 ? itemList[0] : null;
            RefreshPreview();
        }

        /// <summary>
        /// 选择武将（窗口右侧列表用）。
        /// 没收模式下候选珍品跟着人走，必须重建列表；授予模式只需刷新预览。
        /// </summary>
        /// <param name="person">目标武将</param>
        public void SetTargetPerson(Person person)
        {
            personList.Clear();
            if (person != null)
                personList.Add(person);

            if (IsGrantMode)
                RefreshPreview();
            else
                RefreshItemList();
        }

        /// <summary>选择珍品（窗口左侧列表用）</summary>
        /// <param name="item">目标珍品</param>
        public void SetItem(ItemType item)
        {
            selectedItem = item;
            RefreshPreview();
        }

        /// <summary>
        /// 重新计算"忠诚变化预览 / 能否确认 / 提示文案"。
        /// 校验共用 <see cref="ItemGrantRules"/> 的口径，与真正执行时完全一致。
        /// </summary>
        public void RefreshPreview()
        {
            loyaltyPreview = 0;
            blockReason = ItemGrantRules.ReasonNone;
            hintText = "";

            if (selectedItem == null)
            {
                blockReason = IsGrantMode ? "国库里没有可授予的珍品" : "该武将身上没有可没收的珍品";
                return;
            }

            if (TargetPerson == null)
            {
                blockReason = "本城没有可作为对象的武将";
                return;
            }

            string reason;
            if (IsGrantMode)
            {
                if (!ItemGrantRules.CanGrant(TargetCity, TargetPerson, selectedItem, out reason))
                {
                    blockReason = reason;
                    return;
                }

                loyaltyPreview = ItemGrantRules.GetGrantLoyalty(selectedItem);

                // 忠诚已满：授予照常执行（道具要落地），只是不再有忠诚收益
                if (TargetPerson.loyalty >= 100)
                    hintText = "忠诚已满，本次授予没有额外忠诚收益";
            }
            else
            {
                if (!ItemGrantRules.CanConfiscate(TargetCity, TargetPerson, selectedItem, out reason))
                {
                    blockReason = reason;
                    return;
                }

                loyaltyPreview = -ItemGrantRules.GetConfiscateLoyalty(selectedItem);

                // 没收会掉忠，掉到什么程度由 AddLoyalty 钳制，这里只提示下限
                if (TargetPerson.loyalty + loyaltyPreview <= 0)
                    hintText = "忠诚将降至下限，注意可能触发出走";
            }
        }

        /// <summary>
        /// 执行工作：把当前"武将 + 珍品"交给数据侧落地。
        /// 执行前再刷新一次预览，防止窗口停留期间外部状态变化（如国库被别处动过）。
        /// </summary>
        public override void DoJob()
        {
            RefreshPreview();
            if (!CanConfirm)
                return;

            bool ok = IsGrantMode
                ? TargetCity.JobGrantItem(TargetPerson, selectedItem)
                : TargetCity.JobConfiscateItem(TargetPerson, selectedItem);

            if (!ok)
                return;

            Done();
            GameMedia.Instance.PlayDoAcitonSfx();
        }
    }

    /// <summary>
    /// 都市人事「授予宝物」：把国库里的一件珍品授予本城武将
    /// （装备类自动上栏、宝物类随身携带，被替换的旧件退回国库），并按品级提升忠诚。
    /// </summary>
    [GameSystem]
    public class CityGrantItem : CityItemGrantBase
    {
        public CityGrantItem()
        {
            customTitleName = "授予宝物";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
                PersonSortFunction.SortByFeatureList,
            };
            customMenuName = "人事/授予宝物";
            customMenuOrder = 2600;
            windowName = "window_item_grant";
        }

        public override CityJobType GrantJobType => CityJobType.GrantItem;

        public override bool IsGrantMode => true;
    }

    /// <summary>
    /// 都市人事「没收宝物」：把本城武将身上（含装备栏）的一件珍品收回国库，并按品级扣减忠诚。
    /// </summary>
    [GameSystem]
    public class CityConfiscateItem : CityItemGrantBase
    {
        public CityConfiscateItem()
        {
            customTitleName = "没收宝物";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
                PersonSortFunction.SortByFeatureList,
            };
            customMenuName = "人事/没收宝物";
            customMenuOrder = 2610;
            windowName = "window_item_grant";
        }

        public override CityJobType GrantJobType => CityJobType.ConfiscateItem;

        public override bool IsGrantMode => false;
    }
}
