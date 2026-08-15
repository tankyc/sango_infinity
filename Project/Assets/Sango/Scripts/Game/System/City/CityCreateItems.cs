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

        protected virtual void InitItem()
        {
            ItemTypes.Clear();
            Scenario scenario = Scenario.Cur;
            Dictionary<int, ItemType> itemMap = new Dictionary<int, ItemType>();
            scenario.CommonData.ItemTypes.ForEach(it =>
            {
                if (it.cost > 0 && it.IsValid(TargetCity.mForce))
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
                    TargetCity.mCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.CreateItems);

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
            Person[] people = ForceAI.CounsellorRecommendCreateItems(TargetCity.freePersons);
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
