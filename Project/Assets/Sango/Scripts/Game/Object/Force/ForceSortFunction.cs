using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
   
    public enum ForceSortGroupType : int
    {
        //自定义,功能独有
        Custom = 0,
        //状态
        State,
        //战力
        FightPower,
        //兵装
        Item,
        //资金
        Gold,
        //兵粮
        Food,
        //灾害
        Disaster,

        Max
    }

    public class ForceSortFunction : Singleton<ForceSortFunction>
    {
        public delegate string ForceValueStrGet(Force force);
        public delegate int ForceValueGet(Force force);
        public delegate int ForceSortFunc(Force force1, Force force2);

        /// <summary>
        /// 获取Force对象属性值的object类型代理
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <returns>属性值</returns>
        public delegate object ForceValueObjGet(Force force);

        /// <summary>
        /// 设置Force对象属性值的代理
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void ForceValueObjSet(Force force, object value);

        public Force CurForce;

        public class SortTitle : ObjectSortTitle
        {
            public ForceValueStrGet valueStrGetCall;
            public ForceSortFunc valueSortFunc;
            public ForceValueObjGet valueObjGet;
            public ForceValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Force)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Force)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Force)obj);
            } 

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Force)a, (Force)b);
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueStrGetCall = valueStrGetCall,
                    valueSortFunc = valueSortFunc,
                    valueObjGet = valueObjGet,
                    valueObjSet = valueObjSet,
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
                };
            }
        }

        public void GetSortTitleGroup(ForceSortGroupType forceSortTileGroupType, List<ObjectSortTitle> titleList)
        {
            switch (forceSortTileGroupType)
            {
                case ForceSortGroupType.State:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case ForceSortGroupType.FightPower:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case ForceSortGroupType.Item:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case ForceSortGroupType.Gold:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case ForceSortGroupType.Food:
                    {

                        titleList.Add(SortByName);
                        break;
                    }
                case ForceSortGroupType.Disaster:
                    {

                        titleList.Add(SortByName);
                        break;
                    }
            }
        }

        public string GetSortTitleGroupName(ForceSortGroupType forceSortTileGroupType)
        {
            switch (forceSortTileGroupType)
            {
                case ForceSortGroupType.State: return "状态";
                case ForceSortGroupType.FightPower: return "战力";
                case ForceSortGroupType.Item: return "兵装";
                case ForceSortGroupType.Gold: return "资金";
                case ForceSortGroupType.Food: return "兵粮";
                case ForceSortGroupType.Disaster: return "灾害";
            }

            return "";
        }
        public static SortTitle SortById = new SortTitle()
        {
            name = "编号",
            width = 2.5f,
            valueStrGetCall = x => x.Id.ToString(),
            valueSortFunc = (a, b) => a.Id.CompareTo(b.Id),
            valueObjGet = x => x.Id,
            valueObjSet = (x, v) => x.Id = (int)v,
        };

        public static SortTitle SortByName = new SortTitle()
        {
            name = "势力",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle SortByLeader = new SortTitle()
        {
            name = "主公",
            width = 4.00f,
            valueStrGetCall = x => x.mGovernor?.Name ?? "---",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mGovernor, b.mGovernor),
            valueObjGet = x => x.mGovernor,
            valueObjSet = (x, v) => x.mGovernor = (Person)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.Person,
        };


        public static SortTitle GetSortByDistanceDay(City where)
        {
            return new SortTitle()
            {
                name = "期间",
                width = 2.00f,
                valueStrGetCall = x => $"{x.mGovernor.DistanceDays(where)}0日",
                valueSortFunc = (a, b) => a.mGovernor.DistanceDays(where).CompareTo(b.mGovernor.DistanceDays(where)),
                valueObjGet = x => x.mGovernor.DistanceDays(where),
                valueObjSet = null,
            };
        }

        /// <summary>
        /// 首都排序标题（首都由君主所在城市决定，为只读派生值，不支持直接编辑）
        /// </summary>
        public static SortTitle SortByCapitalCity = new SortTitle()
        {
            name = "首都",
            width = 3.00f,
            valueStrGetCall = x => x == null || x.CapitalCity == null ? "—" : x.CapitalCity.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a.CapitalCity, b.CapitalCity),
            valueObjGet = x => x == null ? null : x.CapitalCity,
            valueObjSet = null,
        };

        /// <summary>
        /// 旗帜排序标题（显示势力旗帜名，下拉从剧本旗帜集合中选值修改）
        /// </summary>
        public static SortTitle SortByFlag = new SortTitle()
        {
            name = "旗帜",
            width = 2.60f,
            valueStrGetCall = x => x == null || x.mFlag == null ? "—" : x.mFlag.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a.mFlag, b.mFlag),
            valueObjGet = x => x == null ? null : x.mFlag,
            valueObjSet = (x, v) =>
            {
                if (x == null) return;
                Flag flag = v as Flag;
                x.mFlag = flag;
                // 旗帜运行时对象引用与持久化字段需同步更新，否则保存剧本时会丢失修改
                x.Flag = flag == null ? 0 : flag.Id;
            },
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Flag,
        };

        /// <summary>
        /// 爵位排序标题（显示势力爵位名，下拉从剧本爵位集合中选值修改）
        /// </summary>
        public static SortTitle SortByTitle = new SortTitle()
        {
            name = "爵位",
            width = 3.00f,
            valueStrGetCall = x => x == null || x.Title == null ? "—" : x.Title.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a.Title, b.Title),
            valueObjGet = x => x == null ? null : x.Title,
            valueObjSet = (x, v) =>
            {
                if (x == null) return;
                x.Title = v as Title;
            },
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Title,
        };

        /// <summary>
        /// 联盟排序标题（显示与各势力的同盟/停战/通商关系文本，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByAlliance = new SortTitle()
        {
            name = "联盟",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetAllianceText(x),
            valueSortFunc = (a, b) =>
            {
                int aCount = a == null || a.AllianceList == null ? 0 : a.AllianceList.Count;
                int bCount = b == null || b.AllianceList == null ? 0 : b.AllianceList.Count;
                return aCount.CompareTo(bCount);
            },
            valueObjGet = x => x == null ? null : x.AllianceList,
            valueObjSet = null,
        };

        /// <summary>
        /// 科技树排序标题（显示势力初始开放的科技树列表，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortBInitTechniques = new SortTitle()
        {
            name = "科技树",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetTechniqueListText(x == null ? null : x.InitTechniques),
            valueSortFunc = (a, b) => CompareTechniqueCount(a == null ? null : a.InitTechniques, b == null ? null : b.InitTechniques),
            valueObjGet = x => x == null ? null : x.InitTechniques,
            valueObjSet = null,
        };

        /// <summary>
        /// 科技排序标题（显示势力已掌握的科技列表，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByTechniques = new SortTitle()
        {
            name = "科技",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetTechniqueListText(x == null ? null : x.Techniques),
            valueSortFunc = (a, b) => CompareTechniqueCount(a == null ? null : a.Techniques, b == null ? null : b.Techniques),
            valueObjGet = x => x == null ? null : x.Techniques,
            valueObjSet = null,
        };

        /// <summary>
        /// 技巧点排序标题（技巧点只能通过游戏逻辑增减，不支持直接编辑，仅展示）
        /// </summary>
        public static SortTitle SortByTechniquePoint = new SortTitle()
        {
            name = "技巧点",
            width = 2.00f,
            valueStrGetCall = x => x.TechniquePoint.ToString(),
            valueSortFunc = (a, b) => a.TechniquePoint.CompareTo(b.TechniquePoint),
            valueObjGet = x => x.TechniquePoint,
            valueObjSet = null,
        };

        /// <summary>
        /// 霸业点排序标题（按霸业点数值显示与排序，点击后通过数字计算器修改）
        /// </summary>
        public static SortTitle SortByHegemonyPoint = new SortTitle()
        {
            name = "霸业点",
            width = 2.00f,
            valueStrGetCall = x => x.HegemonyPoint.ToString(),
            valueSortFunc = (a, b) => a.HegemonyPoint.CompareTo(b.HegemonyPoint),
            valueObjGet = x => x.HegemonyPoint,
            valueObjSet = (x, v) => x.HegemonyPoint = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 方针排序标题（按方针数值显示与排序，方针选项语义未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByPolicyType = new SortTitle()
        {
            name = "方针",
            width = 2.00f,
            valueStrGetCall = x => x.PolicyType.ToString(),
            valueSortFunc = (a, b) => a.PolicyType.CompareTo(b.PolicyType),
            valueObjGet = x => x.PolicyType,
            valueObjSet = null,
        };

        /// <summary>
        /// 国库排序标题（显示国库兵装等道具内容，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByStroe = new SortTitle()
        {
            name = "国库",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetItemStoreText(x == null ? null : x.Stroe),
            valueSortFunc = (a, b) =>
            {
                int aCount = a == null || a.Stroe == null ? 0 : a.Stroe.TotalNumber;
                int bCount = b == null || b.Stroe == null ? 0 : b.Stroe.TotalNumber;
                return aCount.CompareTo(bCount);
            },
            valueObjGet = x => x == null ? null : x.Stroe,
            valueObjSet = null,
        };

        // ==================== 排序标题辅助方法 ====================

        /// <summary>
        /// 生成势力结盟关系显示文本（同盟/停战/通商:对方势力名，多条以顿号分隔）
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <returns>结盟关系文本，无结盟时返回—</returns>
        private static string GetAllianceText(Force force)
        {
            if (force == null || force.AllianceList == null || force.AllianceList.Count == 0) return "—";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < force.AllianceList.Count; i++)
            {
                Alliance alliance = force.AllianceList[i];
                if (alliance == null || alliance.ForceList == null) continue;
                string typeName;
                switch (alliance.allianceType)
                {
                    case AllianceType.Truce: typeName = "停战"; break;
                    case AllianceType.Trade: typeName = "通商"; break;
                    default: typeName = "同盟"; break;
                }
                for (int j = 0; j < alliance.ForceList.Count; j++)
                {
                    Force other = alliance.ForceList[j];
                    if (other == null || other == force) continue;
                    if (sb.Length > 0) sb.Append("，");
                    sb.Append(typeName).Append(":").Append(other.Name);
                }
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

        /// <summary>
        /// 生成科技/科技树列表的显示文本（科技名以顿号分隔）
        /// </summary>
        /// <param name="list">科技列表</param>
        /// <returns>显示文本，空列表返回—</returns>
        private static string GetTechniqueListText(SangoObjectList<Technique> list)
        {
            if (list == null || list.Count == 0) return "—";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                Technique technique = list[i];
                if (technique == null) continue;
                if (sb.Length > 0) sb.Append("，");
                sb.Append(technique.Name);
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

        /// <summary>
        /// 比较两个科技列表的长度（用于列表列排序）
        /// </summary>
        /// <param name="a">科技列表a</param>
        /// <param name="b">科技列表b</param>
        /// <returns>比较结果</returns>
        private static int CompareTechniqueCount(SangoObjectList<Technique> a, SangoObjectList<Technique> b)
        {
            int aCount = a == null ? 0 : a.Count;
            int bCount = b == null ? 0 : b.Count;
            return aCount.CompareTo(bCount);
        }

        /// <summary>
        /// 生成道具栏显示文本（每类道具名x数量，顿号分隔）
        /// 道具栏按道具类型的storeKind存储数量，名称需到当前剧本CommonData的道具类型集中按storeKind匹配
        /// </summary>
        /// <param name="itemStore">道具栏（国库）</param>
        /// <returns>显示文本，空道具栏返回—</returns>
        private static string GetItemStoreText(ItemStore itemStore)
        {
            if (itemStore == null || itemStore.Items == null || itemStore.Items.Count == 0)
                return "—";
            Scenario scenario = Scenario.Cur;
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, int> kv in itemStore.Items)
            {
                int number = kv.Value;
                if (number <= 0) continue;
                string itemName = null;
                if (scenario != null && scenario.CommonData != null)
                {
                    ItemType itemType = scenario.CommonData.ItemTypes.Find(t => t != null && t.storeKind == kv.Key);
                    if (itemType != null) itemName = itemType.Name;
                }
                if (string.IsNullOrEmpty(itemName)) itemName = kv.Key.ToString();
                if (sb.Length > 0) sb.Append("，");
                sb.Append(itemName);
                if (number > 1) sb.Append("x").Append(number);
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

    }
}
