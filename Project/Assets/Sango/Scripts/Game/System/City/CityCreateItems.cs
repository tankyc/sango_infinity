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
            customMenuOrder = 30;
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
            sortTitle.personSortFunc = (a, b) => b.HasFeatrue(featureId).CompareTo(a.HasFeatrue(featureId));
            return sortTitle;
        }

        /// <summary>创建生产兵装选择器排序：特性武将优先，组内按智力倒序。</summary>
        public ObjectSortTitle GetProductionPersonSortTitle()
        {
            int featureId = GetProductionFeatureId();
            PersonSortFunction.SortTitle sortTitle = CreateFeaturePrioritySort(featureId);
            sortTitle.personSortFunc = (a, b) =>
            {
                // 先将能直接提升当前兵装生产效果的特性武将排在前面，再比较智力。
                int featureCompare = b.HasFeatrue(featureId).CompareTo(a.HasFeatrue(featureId));
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

        protected virtual void InitItem()
        {
            ItemTypes.Clear();
            Scenario scenario = Scenario.Cur;
            Dictionary<int, ItemType> itemMap = new Dictionary<int, ItemType>();
            scenario.CommonData.ItemTypes.ForEach(it =>
            {
                if (it.cost > 0 && it.IsValid(TargetCity.mBelongForce))
                {
                    ItemType itemType;
                    if (itemMap.TryGetValue(it.storeKind, out itemType))
                    {
                        if (it.Id > itemType.Id)
                        {
                            itemMap[it.storeKind] = it;
                        }
                    }
                    else
                    {
                        itemMap[it.storeKind] = it;
                    }
                }
            });

            foreach (ItemType itemType in itemMap.Values)
            {
                ItemTypes.Add(new ItemTypeInfo()
                {
                    itemType = itemType,
                    targetBuilding = TargetCity.GetFreeBuilding(itemType.createBuildingKind)
                });
            }

            ItemTypes.Sort((a, b) => a.itemType.Id.CompareTo(b.itemType.Id));
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
                return TargetCity.FreePersonCount > 0 && TargetCity.itemStore.TotalNumber < TargetCity.StoreLimit &&
                    TargetCity.CheckJobCost(CityJobType.CreateItems)
                    && (TargetCity.GetFreeBuilding((int)BuildingKindType.BlacksmithShop) != null ||
                        TargetCity.GetFreeBuilding((int)BuildingKindType.Stable) != null ||
                        TargetCity.GetFreeBuilding((int)BuildingKindType.BoatFactory) != null ||
                        TargetCity.GetFreeBuilding((int)BuildingKindType.MechineFactory) != null)
                    &&
                    TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.CreateItems);

            }
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
