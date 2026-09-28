using Sango.Core;
using Sango.Tools.UndoRedo;
using System;
using System.Collections.Generic;
using System.Reflection;
using TKNewtonsoft.Json;
using UnityEngine;

namespace Sango.Tools
{
    /// <summary>
    /// 城池属性编辑窗口
    /// 功能说明:
    /// 1. 窗口左侧提供城池列表,支持点击切换编辑目标;
    /// 2. 支持从地图笔刷打开窗口,左侧列表默认选中当前城池;
    /// 3. 只暴露带[JsonProperty]标记的字段,字段名使用翻译后的中文显示;
    /// 4. 带明显数据ID的字段(所属势力/军团/城池/建筑类型/所属州/城市等级等)提供下拉菜单做映射选取。
    /// </summary>
    public class CityPropertyEditorWindow : EditorWindow
    {
        // ==================== 布局常量 ====================

        /// <summary>
        /// 左侧城池列表宽度
        /// </summary>
        private const float CITY_LIST_WIDTH = 200f;

        /// <summary>
        /// 属性名标签宽度
        /// </summary>
        private const float LABEL_WIDTH = 110f;

        /// <summary>
        /// 属性值控件宽度
        /// </summary>
        private const float VALUE_WIDTH = 170f;

        // ==================== 窗口状态 ====================

        /// <summary>
        /// 当前剧本引用
        /// </summary>
        private Scenario scenario;

        /// <summary>
        /// 撤销重做管理器引用
        /// </summary>
        private UndoRedoManager undoRedoManager;

        /// <summary>
        /// 当前选中的城池
        /// </summary>
        private City selectedCity;

        /// <summary>
        /// 左侧城池列表滚动位置
        /// </summary>
        private Vector2 cityListScroll = Vector2.zero;

        /// <summary>
        /// 右侧属性面板滚动位置
        /// </summary>
        private Vector2 propertyScroll = Vector2.zero;

        /// <summary>
        /// 添加库存时选择的道具下拉索引
        /// </summary>
        private int addItemIndex = 0;

        /// <summary>
        /// 添加库存时输入的数量文本
        /// </summary>
        private string addItemCountText = "1";

        // ==================== 字段中文名翻译表 ====================

        /// <summary>
        /// 字段名到中文显示名的翻译表
        /// </summary>
        private static readonly Dictionary<string, string> FieldDisplayNames = new Dictionary<string, string>
        {
            // 基础信息(SangoObject)
            { "Id", "ID" },
            { "Name", "名称" },
            { "IsAlive", "是否存活" },
            { "ActionOver", "是否行动完毕" },

            // 建筑属性(BuildingBase)
            { "BelongForce", "所属势力" },
            { "BelongCorps", "所属军团" },
            { "BelongCity", "所属城池" },
            { "BuildingType", "建筑类型" },
            { "durability", "当前耐久" },
            { "x", "X坐标" },
            { "y", "Y坐标" },
            { "rot", "旋转值" },
            { "heightOffset", "高度偏移" },
            { "model", "模型" },
            { "isComplate", "是否建造完成" },
            { "isUpgrading", "是否升级中" },
            { "isWorking", "是否工作中" },

            // 城市属性(City)
            { "food", "粮食" },
            { "gold", "金钱" },
            { "population", "人口" },
            { "troopPopulation", "兵役人口" },
            { "workingAppointType", "工作委任类型" },
            { "itemStore", "库存" },
            { "commerce", "商业值" },
            { "agriculture", "农业值" },
            { "popularSupport", "民心" },
            { "security", "治安" },
            { "energy", "战意" },
            { "morale", "士气" },
            { "hasBusiness", "商人比例" },
            { "troops", "兵力" },
            { "woundedTroops", "伤兵" },
            { "troopsLimit", "可容纳兵力" },
            { "storeLimit", "仓库大小" },
            { "goldLimit", "金库大小" },
            { "foodLimit", "粮仓大小" },
            { "baseGainGold", "基础金钱收入" },
            { "baseGainFood", "基础粮食收入" },
            { "commerceLimit", "最大商业值" },
            { "agricultureLimit", "最大农业值" },
            { "durabilityLimit", "最大耐久" },
            { "province", "所属州" },
            { "NeighborList", "相邻城市" },
            { "CityLevelType", "城市等级" },
            { "jobCounter", "工作计数" },
        };

        // ==================== 字段显示顺序 ====================

        /// <summary>
        /// 字段显示顺序(未在此列表中的字段排在最后)
        /// </summary>
        private static readonly string[] FieldOrder =
        {
            "Id", "Name",
            "BelongForce", "BelongCorps", "BelongCity", "BuildingType",
            // 耐久与最大耐久紧邻显示
            "durability", "durabilityLimit", "model",
            "food", "gold", "population", "troopPopulation",
            "itemStore",
            "security", "morale", "troops",
            "troopsLimit", "storeLimit", "goldLimit", "foodLimit",
            "baseGainGold", "baseGainFood",
            "province", "CityLevelType",
        };

        // ==================== 数值范围表 ====================

        /// <summary>
        /// 整型字段的取值范围(未配置的字段使用默认大范围)
        /// </summary>
        private static readonly Dictionary<string, Vector2Int> FieldIntRanges = new Dictionary<string, Vector2Int>
        {
            { "popularSupport", new Vector2Int(0, 100) },
            { "hasBusiness", new Vector2Int(0, 100) },
            { "energy", new Vector2Int(0, 100) },
            { "morale", new Vector2Int(0, 100) },
            { "security", new Vector2Int(0, 100) },
        };

        /// <summary>
        /// 浮点字段的取值范围(未配置的字段使用默认大范围)
        /// </summary>
        private static readonly Dictionary<string, Vector2> FieldFloatRanges = new Dictionary<string, Vector2>
        {
            { "rot", new Vector2(0f, 360f) },
            { "heightOffset", new Vector2(-100f, 100f) },
        };

        // ==================== ID 映射字段配置 ====================

        /// <summary>
        /// 存储为int但对应场景对象ID的字段名集合,编辑时提供下拉映射
        /// </summary>
        private static readonly HashSet<string> IntIdFieldNames = new HashSet<string>
        {
            "BelongForce", "BelongCorps", "BelongCity",
        };

        /// <summary>
        /// 不需要在窗口中编辑的字段名集合(如位置坐标、相邻城市、运行时状态等)
        /// </summary>
        private static readonly HashSet<string> HiddenFieldNames = new HashSet<string>
        {
            // 基础信息(SangoObject)
            "IsAlive", "ActionOver",
            // 建筑属性(BuildingBase)
            "x", "y", "rot", "heightOffset", "isComplate", "isUpgrading", "isWorking",
            // 城市属性(City)
            "workingAppointType", "commerce", "agriculture", "commerceLimit", "agricultureLimit",
            "woundedTroops", "hasBusiness", "popularSupport", "energy", "jobCounter",
            // 相邻城市列表
            "NeighborList",
        };

        /// <summary>
        /// 对象引用类型到公共数据集合字段名的映射
        /// </summary>
        private static readonly Dictionary<Type, string> CommonDataFieldMap = new Dictionary<Type, string>
        {
            { typeof(BuildingType), "BuildingTypes" },
            { typeof(CityLevelType), "CityLevelTypes" },
            { typeof(Province), "Provinces" },
        };

        /// <summary>
        /// 需要紧跟上一个字段显示、不插入分组标题的字段名集合(如最大耐久紧跟当前耐久)
        /// </summary>
        private static readonly HashSet<string> FollowFieldNames = new HashSet<string>
        {
            "durabilityLimit",
        };

        // ==================== JsonProperty 成员缓存 ====================

        /// <summary>
        /// 城市类所有带[JsonProperty]标记的字段和属性缓存
        /// </summary>
        private static MemberInfo[] cachedJsonMembers;

        /// <summary>
        /// 获取城市类所有带[JsonProperty]标记的成员
        /// </summary>
        private static MemberInfo[] JsonMembers
        {
            get
            {
                if (cachedJsonMembers == null)
                {
                    cachedJsonMembers = CollectJsonMembers();
                }
                return cachedJsonMembers;
            }
        }

        // ==================== 公共接口 ====================

        /// <summary>
        /// 初始化窗口(从地图笔刷打开时调用)
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="undoRedoManager">撤销重做管理器</param>
        /// <param name="city">默认选中的城池</param>
        public void Initialize(Scenario scenario, UndoRedoManager undoRedoManager, City city)
        {
            this.scenario = scenario;
            this.undoRedoManager = undoRedoManager;

            // 先设置窗口矩形,保证左侧列表滚动计算使用正确的可视区域高度
            float windowWidth = 840f;
            float windowHeight = 640f;
            windowRect = new UnityEngine.Rect(
                Screen.width / 2 - windowWidth / 2,
                Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - windowHeight / 2),
                windowWidth,
                windowHeight
            );

            SetCity(city);
            this.visible = true;
        }

        /// <summary>
        /// 切换当前编辑的城池
        /// </summary>
        /// <param name="city">新的城池</param>
        public void SetCity(City city)
        {
            if (selectedCity != null && selectedCity.Render != null)
            {
                selectedCity.Render.SetFlash(false);
            }
            selectedCity = city;
            if (selectedCity != null && selectedCity.Render != null)
            {
                selectedCity.Render.SetFlash(true);
            }
            // 滚动左侧列表,确保选中的城池显示在可视区域内
            cityListScroll = ComputeCityListScroll();
            propertyScroll = Vector2.zero;
        }

        // ==================== 窗口绘制 ====================

        /// <summary>
        /// 绘制窗口主循环
        /// </summary>
        private new void OnGUI()
        {
            if (visible)
            {
                windowRect = GUILayout.Window(windowId, windowRect, OnDrawWindow, windowName);
                ConstrainWindowToScreen();
            }
        }

        /// <summary>
        /// 绘制窗口主体内容
        /// </summary>
        private void OnDrawWindow(int winId)
        {
            // 同步int归属字段与对象引用,保证撤销/重做后引用一致
            SyncReferenceFields();

            DrawTitleBar();

            if (scenario == null || scenario.citySet == null)
            {
                GUILayout.Label("未加载剧本,无法编辑城池属性");
                return;
            }
            if (selectedCity == null)
            {
                GUILayout.Label("请选择城池");
                return;
            }

            GUILayout.BeginHorizontal();
            DrawCityListPanel();
            DrawPropertyPanel();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制窗口标题栏
        /// </summary>
        private void DrawTitleBar()
        {
            GUILayout.BeginHorizontal();
            string title = selectedCity != null ? $"城市属性编辑: {selectedCity.Name}" : "城市属性编辑";
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
            };
            GUILayout.Label(title, titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("x", GUILayout.Width(30)))
            {
                CloseWindow();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            GUILayout.Label("", GUI.skin.horizontalSlider);
            GUILayout.Space(5);
        }

        /// <summary>
        /// 绘制左侧城池列表
        /// </summary>
        private void DrawCityListPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(CITY_LIST_WIDTH));
            GUILayout.Label("城池列表", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13 });

            cityListScroll = GUILayout.BeginScrollView(cityListScroll, GUILayout.Width(CITY_LIST_WIDTH), GUILayout.ExpandHeight(true));

            List<City> cities = GetSortedCities();

            if (cities.Count == 0)
            {
                GUILayout.Label("暂无城池");
            }

            foreach (City city in cities)
            {
                bool isSelected = city == selectedCity;
                if (isSelected)
                {
                    GUI.backgroundColor = new Color(0.4f, 0.75f, 1f);
                }
                if (GUILayout.Button($"{city.Id}.{city.Name}", GUILayout.Width(CITY_LIST_WIDTH - 20)))
                {
                    SetCity(city);
                }
                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 获取按ID排序的城池列表
        /// </summary>
        private List<City> GetSortedCities()
        {
            List<City> cities = new List<City>();
            if (scenario == null || scenario.citySet == null)
            {
                return cities;
            }
            scenario.citySet.ForEach(city =>
            {
                if (city != null)
                {
                    cities.Add(city);
                }
            });
            cities.Sort((a, b) => a.Id.CompareTo(b.Id));
            return cities;
        }

        /// <summary>
        /// 计算左侧城池列表滚动位置,确保选中的城池显示在可视区域内
        /// </summary>
        private Vector2 ComputeCityListScroll()
        {
            if (selectedCity == null)
            {
                return cityListScroll;
            }
            List<City> cities = GetSortedCities();
            int index = cities.IndexOf(selectedCity);
            if (index < 0)
            {
                return cityListScroll;
            }
            // 列表项高度(按钮高度加间距),需与DrawCityListPanel保持一致
            const float itemHeight = 25f;
            float contentHeight = cities.Count * itemHeight;
            // 滚动区域可视高度估算(窗口高度减去标题栏和列表头部区域)
            float viewHeight = windowRect.height - MENU_BAR_HEIGHT - 60f;
            float targetY = index * itemHeight;
            float maxY = System.Math.Max(0f, contentHeight - viewHeight);
            return new Vector2(cityListScroll.x, Mathf.Clamp(targetY, 0f, maxY));
        }

        /// <summary>
        /// 绘制右侧属性面板
        /// </summary>
        private void DrawPropertyPanel()
        {
            propertyScroll = GUILayout.BeginScrollView(propertyScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            string currentGroup = null;
            MemberInfo previousMember = null;
            foreach (MemberInfo member in JsonMembers)
            {
                string group = GetGroupTitle(member);
                // 跳过不需要在窗口中编辑的字段(位置坐标、相邻城市等)
                if (HiddenFieldNames.Contains(member.Name))
                {
                    continue;
                }
                // 需要紧邻上一个字段显示的成员(如最大耐久)不再插入分组标题
                bool isFollow = previousMember != null && FollowFieldNames.Contains(member.Name);
                if (!isFollow && group != currentGroup)
                {
                    currentGroup = group;
                    DrawGroupTitle(group);
                }
                DrawMember(member);
                previousMember = member;
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制属性分组标题
        /// </summary>
        /// <param name="title">分组标题</param>
        private void DrawGroupTitle(string title)
        {
            GUILayout.Space(8);
            GUILayout.Label(title, new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = new Color(0.85f, 0.6f, 0.2f) },
            });
            GUILayout.Space(4);
        }

        // ==================== 成员绘制 ====================

        /// <summary>
        /// 绘制单个JsonProperty成员
        /// </summary>
        /// <param name="member">成员信息</param>
        private void DrawMember(MemberInfo member)
        {
            // 跳过不需要在窗口中编辑的字段(位置坐标、相邻城市等)
            if (HiddenFieldNames.Contains(member.Name))
            {
                return;
            }

            Type type = GetMemberType(member);
            object value = GetValue(member, selectedCity);
            string displayName = GetDisplayName(member);

            GUILayout.BeginHorizontal();
            GUILayout.Label(displayName, GUILayout.Width(LABEL_WIDTH));

            if (member.Name == "Id")
            {
                // 对象ID为标识字段,只读显示
                GUILayout.Label(value != null ? value.ToString() : "0");
            }
            else if (type == typeof(int))
            {
                if (IntIdFieldNames.Contains(member.Name))
                {
                    DrawIntIdPopup(member, (int)value);
                }
                else
                {
                    DrawIntField(member, (int)value);
                }
            }
            else if (type == typeof(float))
            {
                DrawFloatField(member, (float)value);
            }
            else if (type == typeof(byte))
            {
                DrawByteField(member, (byte)value);
            }
            else if (type == typeof(bool))
            {
                DrawBoolField(member, (bool)value);
            }
            else if (type == typeof(string))
            {
                DrawStringField(member, value as string);
            }
            else if (type == typeof(ItemStore))
            {
                DrawItemStoreSummary(value as ItemStore);
            }
            else if (type == typeof(Dictionary<int, int>))
            {
                DrawDictionarySummary(value as Dictionary<int, int>);
            }
            else if (typeof(SangoObject).IsAssignableFrom(type))
            {
                DrawObjectPopup(member, value as SangoObject);
            }
            else
            {
                // 其他复杂类型只读显示类型名
                GUILayout.Label($"[{type.Name}]");
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制整型字段输入框
        /// </summary>
        private void DrawIntField(MemberInfo member, int value)
        {
            // 值为0的治安/士气按默认值显示,避免新城市直接显示0
            int displayValue = GetDefaultDisplayValue(member.Name, value);
            string text = GUILayout.TextField(displayValue.ToString(), GUILayout.Width(VALUE_WIDTH));
            if (int.TryParse(text, out int newValue) && newValue != displayValue)
            {
                Vector2Int range = GetIntRange(member.Name);
                newValue = Mathf.Clamp(newValue, range.x, range.y);
                if (newValue != displayValue)
                {
                    CreateEditCommand(member.Name, value, newValue, $"修改城市{GetDisplayName(member)}: {displayValue} -> {newValue}");
                    SetValue(member, selectedCity, newValue);
                }
            }
        }

        /// <summary>
        /// 获取字段的默认显示值(值为0时按字段默认值显示,不修改实际数据)
        /// </summary>
        private static int GetDefaultDisplayValue(string fieldName, int value)
        {
            if (value != 0)
            {
                return value;
            }
            switch (fieldName)
            {
                case "security":
                    // 治安默认90
                    return 90;
                case "morale":
                    // 士气默认80
                    return 80;
            }
            return value;
        }

        /// <summary>
        /// 绘制浮点字段输入框
        /// </summary>
        private void DrawFloatField(MemberInfo member, float value)
        {
            string text = GUILayout.TextField(value.ToString("F2"), GUILayout.Width(VALUE_WIDTH));
            if (float.TryParse(text, out float newValue) && newValue != value)
            {
                Vector2 range = GetFloatRange(member.Name);
                newValue = Mathf.Clamp(newValue, range.x, range.y);
                if (newValue != value)
                {
                    CreateEditCommand(member.Name, value, newValue, $"修改城市{GetDisplayName(member)}: {value:F2} -> {newValue:F2}");
                    SetValue(member, selectedCity, newValue);
                }
            }
        }

        /// <summary>
        /// 绘制字节字段输入框
        /// </summary>
        private void DrawByteField(MemberInfo member, byte value)
        {
            string text = GUILayout.TextField(value.ToString(), GUILayout.Width(VALUE_WIDTH));
            if (int.TryParse(text, out int newValue) && newValue != value)
            {
                Vector2Int range = GetIntRange(member.Name);
                newValue = Mathf.Clamp(newValue, range.x, Mathf.Min(range.y, 255));
                byte newByteValue = (byte)newValue;
                if (newByteValue != value)
                {
                    CreateEditCommand(member.Name, value, newByteValue, $"修改城市{GetDisplayName(member)}: {value} -> {newByteValue}");
                    SetValue(member, selectedCity, newByteValue);
                }
            }
        }

        /// <summary>
        /// 绘制布尔字段开关
        /// </summary>
        private void DrawBoolField(MemberInfo member, bool value)
        {
            bool newValue = GUILayout.Toggle(value, "", GUILayout.Width(VALUE_WIDTH));
            if (newValue != value)
            {
                CreateEditCommand(member.Name, value, newValue, $"修改城市{GetDisplayName(member)}: {value} -> {newValue}");
                SetValue(member, selectedCity, newValue);
            }
        }

        /// <summary>
        /// 绘制字符串字段输入框
        /// </summary>
        private void DrawStringField(MemberInfo member, string value)
        {
            string safeValue = value ?? "";
            string newValue = GUILayout.TextField(safeValue, GUILayout.Width(VALUE_WIDTH));
            if (newValue != safeValue)
            {
                CreateEditCommand(member.Name, safeValue, newValue, $"修改城市{GetDisplayName(member)}: {safeValue} -> {newValue}");
                SetValue(member, selectedCity, newValue);
            }
        }

        /// <summary>
        /// 绘制int型ID字段的下拉菜单映射
        /// </summary>
        private void DrawIntIdPopup(MemberInfo member, int value)
        {
            List<SangoObject> candidates = GetIntIdCandidates(member.Name);
            List<string> names = new List<string> { "无" };
            foreach (SangoObject obj in candidates)
            {
                names.Add($"{obj.Id}.{obj.Name}");
            }

            int index = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Id == value)
                {
                    index = i + 1;
                    break;
                }
            }

            int newIndex = EditorUtility.Popup(index, names.ToArray(), GUILayout.Width(VALUE_WIDTH));
            if (newIndex != index)
            {
                int newId = newIndex == 0 ? 0 : candidates[newIndex - 1].Id;
                ApplyIntIdChange(member.Name, newId);
                CreateEditCommand(member.Name, value, newId, $"修改城市{GetDisplayName(member)}: {value} -> {newId}");
            }
        }

        /// <summary>
        /// 绘制对象引用字段的下拉菜单映射(建筑类型/所属州/城市等级)
        /// </summary>
        private void DrawObjectPopup(MemberInfo member, SangoObject value)
        {
            List<SangoObject> candidates = GetObjectCandidates(GetMemberType(member));
            List<string> names = new List<string> { "无" };
            foreach (SangoObject obj in candidates)
            {
                names.Add($"{obj.Id}.{obj.Name}");
            }

            int index = value == null ? 0 : System.Math.Max(0, candidates.IndexOf(value)) + 1;

            int newIndex = EditorUtility.Popup(index, names.ToArray(), GUILayout.Width(VALUE_WIDTH));
            if (newIndex != index)
            {
                SangoObject newValue = newIndex == 0 ? null : candidates[newIndex - 1];
                string oldName = value != null ? value.Name : "无";
                string newName = newValue != null ? newValue.Name : "无";
                CreateEditCommand(member.Name, value, newValue, $"修改城市{GetDisplayName(member)}: {oldName} -> {newName}");
                SetValue(member, selectedCity, newValue);
            }
        }

        /// <summary>
        /// 绘制库存编辑面板(道具列表展示与添加控件)
        /// </summary>
        private void DrawItemStoreSummary(ItemStore store)
        {
            if (store == null)
            {
                return;
            }

            GUILayout.BeginVertical();
            if (store.Items.Count == 0)
            {
                GUILayout.Label("空", GUILayout.Width(VALUE_WIDTH));
            }
            else
            {
                foreach (KeyValuePair<int, int> pair in store.Items)
                {
                    ItemType itemType = GetItemType(pair.Key);
                    string itemName = itemType != null ? itemType.Name : $"道具{pair.Key}";
                    GUILayout.Label($"{itemName} x{pair.Value}", GUILayout.Width(VALUE_WIDTH));
                }
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            List<ItemType> itemTypes = GetStoreItemTypes();
            if (itemTypes.Count == 0)
            {
                GUILayout.Label("无可用道具", GUILayout.Width(VALUE_WIDTH));
            }
            else
            {
                // 修正失效的下拉索引
                if (addItemIndex >= itemTypes.Count)
                {
                    addItemIndex = 0;
                }
                List<string> names = new List<string>();
                foreach (ItemType itemType in itemTypes)
                {
                    names.Add($"{itemType.Id}.{itemType.Name}");
                }
                addItemIndex = EditorUtility.Popup(addItemIndex, names.ToArray(), GUILayout.Width(110));
                addItemCountText = GUILayout.TextField(addItemCountText, GUILayout.Width(45));
                if (GUILayout.Button("添加", GUILayout.Width(45)))
                {
                    if (int.TryParse(addItemCountText, out int number) && number > 0)
                    {
                        AddItemToStore(itemTypes[addItemIndex], number);
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 获取全部道具类型列表
        /// </summary>
        private List<ItemType> GetStoreItemTypes()
        {
            List<ItemType> result = new List<ItemType>();
            if (scenario == null || scenario.CommonData == null)
            {
                return result;
            }
            scenario.CommonData.ItemTypes.ForEach(itemType =>
            {
                if (itemType != null)
                {
                    result.Add(itemType);
                }
            });
            return result;
        }

        /// <summary>
        /// 根据道具存储ID获取道具类型
        /// </summary>
        private ItemType GetItemType(int storeKindId)
        {
            if (scenario == null || scenario.CommonData == null)
            {
                return null;
            }
            return scenario.CommonData.ItemTypes.Get(storeKindId);
        }

        /// <summary>
        /// 向城池库存中添加道具并记录撤销命令
        /// </summary>
        private void AddItemToStore(ItemType itemType, int number)
        {
            if (selectedCity == null || itemType == null || number <= 0)
            {
                return;
            }
            ItemStore store = selectedCity.itemStore;
            if (store == null)
            {
                return;
            }
            ItemStore oldStore = store.Copy();
            store.Add(itemType, number);
            ItemStore newStore = store.Copy();
            CreateEditCommand("itemStore", oldStore, newStore, $"为城市{selectedCity.Name}添加道具 {itemType.Name} x{number}");
        }

        /// <summary>
        /// 绘制字典摘要(只读)
        /// </summary>
        private void DrawDictionarySummary(Dictionary<int, int> dict)
        {
            if (dict == null)
            {
                GUILayout.Label("空", GUILayout.Width(VALUE_WIDTH));
                return;
            }
            GUILayout.Label($"记录 {dict.Count} 条", GUILayout.Width(VALUE_WIDTH));
        }

        // ==================== 归属字段变更逻辑 ====================

        /// <summary>
        /// 应用int型ID字段的变更,同步对象引用与城内武将归属
        /// </summary>
        /// <param name="fieldName">字段名</param>
        /// <param name="newId">新的对象ID</param>
        private void ApplyIntIdChange(string fieldName, int newId)
        {
            switch (fieldName)
            {
                case "BelongForce":
                {
                    Force newForce = newId == 0 ? null : scenario.forceSet.Get(newId);
                    selectedCity.BelongForce = newId;
                    selectedCity.mBelongForce = newForce;
                    // 同步城内武将的归属势力
                    foreach (Person person in scenario.personSet)
                    {
                        if (person != null && person.mBelongCity == selectedCity)
                        {
                            person.mBelongForce = newForce;
                            person.BelongForce = newId;
                        }
                    }
                    break;
                }
                case "BelongCorps":
                {
                    Corps newCorps = newId == 0 ? null : scenario.corpsSet.Get(newId);
                    selectedCity.BelongCorps = newId;
                    selectedCity.mBelongCorps = newCorps;
                    // 同步城内武将的归属军团
                    foreach (Person person in scenario.personSet)
                    {
                        if (person != null && person.mBelongCity == selectedCity)
                        {
                            person.mBelongCorps = newCorps;
                            person.BelongCorps = newId;
                        }
                    }
                    break;
                }
                case "BelongCity":
                {
                    selectedCity.BelongCity = newId;
                    selectedCity.mBelongCity = newId == 0 ? null : scenario.citySet.Get(newId);
                    break;
                }
            }
        }

        /// <summary>
        /// 同步int归属字段与对象引用,保证任何修改路径(含撤销重做)下引用一致
        /// </summary>
        private void SyncReferenceFields()
        {
            if (selectedCity == null || scenario == null)
            {
                return;
            }
            if (selectedCity.BelongForce != (selectedCity.mBelongForce?.Id ?? 0))
            {
                selectedCity.mBelongForce = selectedCity.BelongForce > 0 ? scenario.forceSet.Get(selectedCity.BelongForce) : null;
            }
            if (selectedCity.BelongCorps != (selectedCity.mBelongCorps?.Id ?? 0))
            {
                selectedCity.mBelongCorps = selectedCity.BelongCorps > 0 ? scenario.corpsSet.Get(selectedCity.BelongCorps) : null;
            }
            if (selectedCity.BelongCity != (selectedCity.mBelongCity?.Id ?? 0))
            {
                selectedCity.mBelongCity = selectedCity.BelongCity > 0 ? scenario.citySet.Get(selectedCity.BelongCity) : null;
            }
        }

        // ==================== 撤销重做 ====================

        /// <summary>
        /// 创建并记录一个城市属性编辑命令
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        /// <param name="actionName">操作描述</param>
        private void CreateEditCommand(string propertyName, object oldValue, object newValue, string actionName)
        {
            if (undoRedoManager != null)
            {
                CityEditCommand command = new CityEditCommand(selectedCity, propertyName, oldValue, newValue, actionName);
                undoRedoManager.AddCommand(command, true);
            }
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 收集城市类所有带[JsonProperty]标记的字段和属性,并按显示顺序排序
        /// </summary>
        private static MemberInfo[] CollectJsonMembers()
        {
            Type type = typeof(City);
            List<MemberInfo> members = new List<MemberInfo>();

            // 收集带[JsonProperty]标记的字段(含私有字段)
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<JsonPropertyAttribute>() != null)
                {
                    members.Add(field);
                }
            }

            // 收集带[JsonProperty]标记的属性
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (property.GetCustomAttribute<JsonPropertyAttribute>() != null)
                {
                    members.Add(property);
                }
            }

            // 按FieldOrder定义顺序排序,未定义的排最后
            members.Sort(CompareMemberOrder);
            return members.ToArray();
        }

        /// <summary>
        /// 成员排序比较器
        /// </summary>
        private static int CompareMemberOrder(MemberInfo a, MemberInfo b)
        {
            int indexA = GetOrderIndex(a.Name);
            int indexB = GetOrderIndex(b.Name);
            if (indexA != indexB)
            {
                return indexA.CompareTo(indexB);
            }
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取字段名在FieldOrder中的顺序下标
        /// </summary>
        private static int GetOrderIndex(string fieldName)
        {
            for (int i = 0; i < FieldOrder.Length; i++)
            {
                if (FieldOrder[i] == fieldName)
                {
                    return i;
                }
            }
            return int.MaxValue;
        }

        /// <summary>
        /// 获取成员对应的分组标题(按声明类型分组)
        /// </summary>
        private static string GetGroupTitle(MemberInfo member)
        {
            if (member.DeclaringType == typeof(SangoObject))
            {
                return "基础信息";
            }
            if (member.DeclaringType == typeof(BuildingBase))
            {
                return "建筑属性";
            }
            if (member.DeclaringType == typeof(City))
            {
                return "城市属性";
            }
            return member.DeclaringType != null ? member.DeclaringType.Name : "其他";
        }

        /// <summary>
        /// 获取成员的中文显示名
        /// </summary>
        private string GetDisplayName(MemberInfo member)
        {
            if (FieldDisplayNames.TryGetValue(member.Name, out string displayName))
            {
                return displayName;
            }
            return member.Name;
        }

        /// <summary>
        /// 获取整型字段的取值范围
        /// </summary>
        private static Vector2Int GetIntRange(string fieldName)
        {
            if (FieldIntRanges.TryGetValue(fieldName, out Vector2Int range))
            {
                return range;
            }
            return new Vector2Int(-100000000, 100000000);
        }

        /// <summary>
        /// 获取浮点字段的取值范围
        /// </summary>
        private static Vector2 GetFloatRange(string fieldName)
        {
            if (FieldFloatRanges.TryGetValue(fieldName, out Vector2 range))
            {
                return range;
            }
            return new Vector2(-100000000f, 100000000f);
        }

        /// <summary>
        /// 获取int型ID字段的候选对象列表
        /// </summary>
        private List<SangoObject> GetIntIdCandidates(string fieldName)
        {
            List<SangoObject> result = new List<SangoObject>();
            if (scenario == null)
            {
                return result;
            }
            switch (fieldName)
            {
                case "BelongForce":
                    scenario.forceSet.ForEach(force =>
                    {
                        if (force != null)
                        {
                            result.Add(force);
                        }
                    });
                    break;
                case "BelongCorps":
                    scenario.corpsSet.ForEach(corps =>
                    {
                        if (corps != null)
                        {
                            result.Add(corps);
                        }
                    });
                    break;
                case "BelongCity":
                    scenario.citySet.ForEach(city =>
                    {
                        if (city != null)
                        {
                            result.Add(city);
                        }
                    });
                    break;
            }
            return result;
        }

        /// <summary>
        /// 获取对象引用类型的候选对象列表(来自公共数据集合)
        /// </summary>
        private List<SangoObject> GetObjectCandidates(Type type)
        {
            List<SangoObject> result = new List<SangoObject>();
            if (scenario == null || scenario.CommonData == null)
            {
                return result;
            }
            if (!CommonDataFieldMap.TryGetValue(type, out string fieldName))
            {
                return result;
            }
            FieldInfo field = typeof(ScenarioCommonData).GetField(fieldName);
            IDatabase set = field != null ? field.GetValue(scenario.CommonData) as IDatabase : null;
            if (set != null)
            {
                set.ForEach(obj =>
                {
                    if (obj != null)
                    {
                        result.Add(obj);
                    }
                });
            }
            return result;
        }

        /// <summary>
        /// 获取成员类型
        /// </summary>
        private static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo field)
            {
                return field.FieldType;
            }
            if (member is PropertyInfo property)
            {
                return property.PropertyType;
            }
            return typeof(object);
        }

        /// <summary>
        /// 读取成员值
        /// </summary>
        private static object GetValue(MemberInfo member, object target)
        {
            if (member is FieldInfo field)
            {
                return field.GetValue(target);
            }
            if (member is PropertyInfo property)
            {
                return property.GetValue(target, null);
            }
            return null;
        }

        /// <summary>
        /// 设置成员值
        /// </summary>
        private static void SetValue(MemberInfo member, object target, object value)
        {
            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
            }
            else if (member is PropertyInfo property && property.CanWrite)
            {
                property.SetValue(target, value, null);
            }
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        private void CloseWindow()
        {
            if (selectedCity != null && selectedCity.Render != null)
            {
                selectedCity.Render.SetFlash(false);
            }
            visible = false;
            Destroy(gameObject);
        }
    }
}
