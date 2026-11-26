using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem(autoInit = false)]
    public class CityCreateItems : CityBaseSystem
    {
        public class ItemTypeInfo
        {
            public ItemType itemType;
            public Building targetBuilding;
        }

        public enum CreateType
        {
            Weapon,
            Horse,
            Machine,
            Boat
        }

        public List<ItemTypeInfo> ItemTypes = new List<ItemTypeInfo>();
        public int CurSelectedItemTypeIndex { get; set; }
        public ItemTypeInfo CurSelectedItemType { get; set; }
        public Building TargetBuilding { get; set; }
        public int[] TurnAndDestNumber { get; set; }

        public CreateType CurCreateType = CreateType.Weapon;

        public CityCreateItems()
        {
            customTitleName = "生产兵装";

            customMenuName = "都市/生产兵装";
            customMenuOrder = 500;
            windowName = "window_city_create_items";
        }

        public override void OnEnter()
        {
            if (customTitleList == null)
            {
                customTitleList = new List<ObjectSortTitle>()
                {
                    PersonSortFunction.SortByName,
                    PersonSortFunction.SortByIntelligence,
                    PersonSortFunction.GetSortByFeatrueId(81),
                };
            }

            InitItem();
            base.OnEnter();
        }

        /// <summary>
        /// 创建按指定特性优先的武将排序规则。
        /// 特性武将排在前面，同特性状态下保持原有列表顺序。
        /// </summary>
        /// <param name="featureId">特性编号。</param>
        /// <returns>能将拥有指定特性的武将排在前面的排序标题。</returns>
        private static PersonSortFunction.SortTitle CreateFeaturePrioritySort(int featureId)
        {
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.GetSortByFeatrueId(featureId);
            // SortTitle 的标准比较器字段为 valueSortFunc，生产特性武将优先。
            sortTitle.valueSortFunc = (a, b) => b.HasFeature(featureId).CompareTo(a.HasFeature(featureId));
            return sortTitle;
        }

        /// <summary>创建生产兵装选择器排序：特性武将优先，组内按智力倒序。</summary>
        public ObjectSortTitle GetProductionPersonSortTitle()
        {
            int featureId = GetProductionFeatureId();
            PersonSortFunction.SortTitle sortTitle = CreateFeaturePrioritySort(featureId);
            // SortTitle 的标准比较器字段为 valueSortFunc，特性相同时再比较智力。
            sortTitle.valueSortFunc = (a, b) =>
            {
                // 先将能直接提升当前兵装生产效果的特性武将排在前面，再比较智力。
                int featureCompare = b.HasFeature(featureId).CompareTo(a.HasFeature(featureId));
                return featureCompare != 0 ? featureCompare : b.Intelligence.CompareTo(a.Intelligence);
            };
            return sortTitle;
        }

        /// <summary>按当前兵装对应特性和智力倒序返回默认推荐武将，最多返回三人。</summary>
        public Person[] GetRecommendedCreatePersons()
        {
            List<Person> persons = new List<Person>(TargetCity.freePersons);
            ObjectSortTitle sortTitle = GetProductionPersonSortTitle();
            // 默认指派与手动选择器使用同一比较器，避免两处推荐结果不一致。
            persons.Sort((a, b) => sortTitle.Sort(a, b));
            if (persons.Count > 3)
                persons.RemoveRange(3, persons.Count - 3);
            return persons.ToArray();
        }

        /// <summary>根据当前生产的兵装仓储类型取得其对应的生产增益特性编号。</summary>
        private int GetProductionFeatureId()
        {
            if (CurSelectedItemType == null || CurSelectedItemType.itemType == null)
                return 81;
            switch ((ItemStoreKindType)CurSelectedItemType.itemType.storeKind)
            {
                // 枪、戟、弩默认使用能吏；其余类型按实际生产加成特性切换。
                case ItemStoreKindType.Horse: return 82;
                case ItemStoreKindType.Helepolis:
                case ItemStoreKindType.Catapult: return 83;
                case ItemStoreKindType.Boat: return 84;
                default: return 81;
            }
        }

        /// <summary>
        /// 城市当前"可制造道具"的特技权限集合（复用容器，避免每次打开界面都分配）。
        /// 由 <see cref="FeaturePermission.CollectCityCreateItemPermissions"/> 填充。
        /// </summary>
        readonly CreateItemPermissionSet createItemPermissions = new CreateItemPermissionSet();

        /// <summary>
        /// 生产项分组容器（storeKind → 该槽位最高级道具）。
        /// 复用容量而不是每次 InitItem 都 new —— 本方法由 IsValid / GetInvalidReason / OnEnter 反复触发。
        /// </summary>
        readonly Dictionary<int, ItemType> itemMapCache = new Dictionary<int, ItemType>();

        /// <summary>生产项排序比较器（静态，避免每次排序都分配 lambda 闭包）</summary>
        static readonly System.Comparison<ItemTypeInfo> ItemTypeInfoComparison =
            (a, b) => a.itemType.Id.CompareTo(b.itemType.Id);

        protected virtual void InitItem()
        {
            ItemTypes.Clear();

            // 【健壮性】MenuCanShow / 目标城市为空时不再往下走：
            // 原实现会在 TargetCity.GetFreeBuilding 处抛空引用（本方法由 IsValid 触发，
            // 菜单构建阶段并非总能保证 TargetCity 已赋值）。
            if (TargetCity == null)
                return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null)
                return;

            Dictionary<int, ItemType> itemMap = itemMapCache;
            itemMap.Clear();

            // 【特技解锁】一次性收集城内武将携带的制造权限（CityAllowCreateItem），
            // 避免对每条道具重复展开武将特技。
            createItemPermissions.Clear();
            FeaturePermission.CollectCityCreateItemPermissions(TargetCity, createItemPermissions);

            // 【性能】原实现用 ForEach(lambda) 捕获 itemMap，每次调用分配闭包；
            // 改为直接遍历字典，内层 return 一律改 continue。
            foreach (KeyValuePair<int, ItemType> kv in scenario.CommonData.ItemTypes.objects)
            {
                ItemType it = kv.Value;
                if (it == null)
                    continue;

                // 特技显式放开的道具：即使势力未研究所需科技也进入候选池。
                bool featureUnlocked = createItemPermissions.Allows(it);

                // 保留原口径：cost = 0 的标准装备（剑、走舸）不进生产列表；
                // 特技明确点名时例外 —— 否则"放开一条免费船"会被这条规则悄悄吃掉。
                if (!featureUnlocked && it.cost <= 0)
                    continue;

                // 科技条件（空势力安全版，替代 ItemType.IsValid 的空引用风险）
                if (!featureUnlocked && !FeaturePermission.TechAllowsCreateItem(TargetCity, it))
                    continue;

                ItemType existItemType;
                if (itemMap.TryGetValue(it.storeKind, out existItemType))
                {
                    // 同一仓储槽位只保留 Id 最大者（越大越高级），保持原规则
                    if (it.Id > existItemType.Id)
                        itemMap[it.storeKind] = it;
                }
                else
                {
                    itemMap[it.storeKind] = it;
                }
            }

            foreach (ItemType itemType in itemMap.Values)
            {
                ItemTypes.Add(new ItemTypeInfo()
                {
                    itemType = itemType,
                    targetBuilding = TargetCity.GetFreeBuilding(itemType.createBuildingKind)
                });
            }

            ItemTypes.Sort(ItemTypeInfoComparison);
            FindAndSelectFirstValidItemType();
        }

        void FindAndSelectFirstValidItemType()
        {
            for (int i = 0; i < ItemTypes.Count; i++)
            {
                ItemTypeInfo itemType = ItemTypes[i];
                TargetBuilding = itemType.targetBuilding;
                if (TargetBuilding != null)
                {
                    CurSelectedItemTypeIndex = i;
                    CurSelectedItemType = itemType;
                    return;
                }
            }
        }

        public override bool IsValid
        {
            get
            {
                if (TargetCity.FreePersonCount <= 0 || !TargetCity.CheckJobCost(CityJobType.CreateItems))
                    return false;
                
                if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.CreateItems))
                    return false;

                InitItem();
                for (int i = 0; i < ItemTypes.Count; i++)
                {
                    ItemTypeInfo itemType = ItemTypes[i];
                    TargetBuilding = itemType.targetBuilding;
                    if (TargetBuilding != null && TargetCity.itemStore.GetNumber(itemType.itemType.storeKind) < itemType.itemType.TransformLimit(TargetCity.StoreLimit))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            // IsValid 已跑过一次 InitItem（OnCityContextMenuShow 先取 IsValid 再取原因），
            // 这里不再重复调用，直接复用其结果。
            if (TargetCity.FreePersonCount <= 0)
                return "无空闲武将";
            if (!TargetCity.CheckJobCost(CityJobType.CreateItems))
                return "资金不足";
            if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.CreateItems))
                return "行动力不足";

            // 兵装已满：所有可产项都达到存储上限
            InitItem();
            for (int i = 0; i < ItemTypes.Count; i++)
            {
                ItemTypeInfo itemType = ItemTypes[i];
                if (itemType.targetBuilding != null
                    && TargetCity.itemStore.GetNumber(itemType.itemType.storeKind)
                        < itemType.itemType.TransformLimit(TargetCity.StoreLimit))
                {
                    return null;
                }
            }
            return "所有兵装已达存储上限";
        }

        public override int CalculateWonderNumber()
        {
            if (CurSelectedItemType != null && CurSelectedItemType.itemType.IsMachine())
            {
                TurnAndDestNumber = TargetCity.JobCreateMachine(personList.ToArray(), CurSelectedItemType.itemType, TargetBuilding, true);
            }
            else if (CurSelectedItemType != null && CurSelectedItemType.itemType.IsBoat())
            {
                TurnAndDestNumber = TargetCity.JobCreateBoat(personList.ToArray(), CurSelectedItemType.itemType, TargetBuilding, true);
            }
            else
            {
                TurnAndDestNumber = new int[2]{
                0,
                TargetCity.JobCreateItems(personList.ToArray(), CurSelectedItemType.itemType, TargetBuilding, true) };
            }

            if (TurnAndDestNumber == null)
                TurnAndDestNumber = new int[2] { 0, 0 };

            return TurnAndDestNumber[1];
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            Person[] people = GetRecommendedCreatePersons();
            if (people != null)
            {
                for (int i = 0; i < people.Length; ++i)
                {
                    Person p = people[i];
                    if (p != null)
                        personList.Add(p);
                }
            }
        }

        public override void DoJob()
        {
            if (personList.Count > 0)
            {
                if (CurSelectedItemType.itemType.IsMachine())
                {
                    TargetCity.JobCreateMachine(personList.ToArray(), CurSelectedItemType.itemType, TargetBuilding);
                }
                else if (CurSelectedItemType.itemType.IsBoat())
                {
                    TargetCity.JobCreateBoat(personList.ToArray(), CurSelectedItemType.itemType, TargetBuilding);
                }
                else
                {
                    TargetCity.JobCreateItems(personList.ToArray(), CurSelectedItemType.itemType, TargetBuilding);
                }
                Done();
                GameMedia.Instance.PlayDoAcitonSfx();
            }
        }
    }
}
