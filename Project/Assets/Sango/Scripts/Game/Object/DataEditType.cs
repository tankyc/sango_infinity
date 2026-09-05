using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 值修改类型
    /// 用于定义ObjectSortTitle对应属性的编辑方式，决定UIDataEdit使用哪种交互控件
    /// </summary>
    public enum DataEditType : int
    {
        /// <summary>未定义，不可编辑</summary>
        None = 0,

        /// <summary>文本修改（InputField输入字符串）</summary>
        Text = 1,

        /// <summary>int类型，使用下拉菜单（人物状态/势力集合/城市集合等）</summary>
        IntDropdown = 2,

        /// <summary>int类型，使用文本输入</summary>
        IntInput = 3,

        /// <summary>int类型，使用UICalculator输入</summary>
        IntCalculator = 4,

        /// <summary>头像选择修改</summary>
        HeadIcon = 5,

        /// <summary>对象类型，复合修改（打开对象编辑器或对象选择器）</summary>
        Object = 6,

        /// <summary>城池选择（打开世界地图选城，默认所有城池均可选取，通过UISelectCityWorldMap点击选择）</summary>
        CitySelect = 7,

        /// <summary>配偶列表编辑（多选武将，写回时自动解除原配偶关系并建立新关系，维持“一个人最多被登记为一个其他武将的配偶”）</summary>
        SpouseList = 8,

        /// <summary>特技列表编辑（调用特技选择器多选特技并整体写回）</summary>
        FeatureList = 9,

        /// <summary>多行文本修改（多行InputField，用于长文本描述）</summary>
        TextArea = 10,

        /// <summary>布尔修改（下拉：是/否，当前值为空时额外提供“无”）</summary>
        BoolDropdown = 11,

        /// <summary>float类型，使用文本输入（支持小数）</summary>
        FloatInput = 12,

        /// <summary>float类型，使用计算器输入（与FloatInput共用输入控件，按整数计算器放大100倍输入）</summary>
        FloatCalculator = 13,

        /// <summary>颜色修改（RGBA分别输入并实时预览，兼容Color与Color32）</summary>
        ColorPicker = 14,

        /// <summary>对象列表修改（多选对象，通过对应数据集的对象选择器选择，整体写回）</summary>
        ObjectList = 15,

        /// <summary>Id集合数组修改（多选对象，写回对象的Id数组int[]）</summary>
        IdArray = 16,

        /// <summary>数值数组修改（int[]/float[]，以逗号或换行分隔编辑）</summary>
        ArrayEdit = 17,

        /// <summary>Json配置修改（多行富文本，按原类型解析回JArray/JObject）</summary>
        JsonEdit = 18,

        /// <summary>对象Id下拉（下拉选择对象，写回该对象的Id，用于外键字段）</summary>
        IdDropdown = 19,

    }

    /// <summary>
    /// 候选数据集类型
    /// 用于定义ObjectSortTitle编辑时获取可选项的数据集来源
    /// </summary>
    public enum DataSetType : int
    {
        /// <summary>未定义</summary>
        None = 0,

        /// <summary>自定义选项（通过customData传入List&lt;string&gt;或List&lt;DataEditOption&gt;）</summary>
        Custom = 1,

        /// <summary>武将集合（Scenario的武将集）</summary>
        Person = 2,

        /// <summary>势力集合（Scenario的势力集）</summary>
        Force = 3,

        /// <summary>城市集合（Scenario的城市集）</summary>
        City = 4,

        /// <summary>军团集合（Scenario的军团集）</summary>
        Corps = 5,

        /// <summary>部队集合（Scenario的部队集）</summary>
        Troop = 6,

        /// <summary>特技集合（Scenario的特技集）</summary>
        Feature = 7,

        /// <summary>性格集合（Scenario.CommonData.Personalities）</summary>
        Personality = 8,

        /// <summary>官职集合（Scenario.CommonData.Officials）</summary>
        Official = 9,

        /// <summary>能力改变类型集合（Scenario.CommonData.AttributeChangeTypes）</summary>
        AttributeChangeType = 10,

        /// <summary>义理集合（Scenario.CommonData.Argumentations）</summary>
        Argumentation = 11,

        /// <summary>州集合（Scenario.CommonData.Provinces，用于出生州等）</summary>
        Province = 12,

        /// <summary>旗帜集合（Scenario.CommonData.Flags）</summary>
        Flag = 13,

        /// <summary>爵位集合（Scenario.CommonData.Titles）</summary>
        Title = 14,

        /// <summary>科技集合（Scenario.CommonData.Techniques）</summary>
        Technique = 15,

        /// <summary>地形类型集合（Scenario.CommonData.TerrainTypes）</summary>
        TerrainType = 16,

        /// <summary>建筑类型集合（Scenario.CommonData.BuildingTypes）</summary>
        BuildingType = 17,

        /// <summary>兵种类型集合（Scenario.CommonData.TroopTypes）</summary>
        TroopType = 18,

        /// <summary>道具类型集合（Scenario.CommonData.ItemTypes）</summary>
        ItemType = 19,

        /// <summary>兵种动画集合（Scenario.CommonData.TroopAnimations）</summary>
        TroopAnimation = 20,

        /// <summary>城市等级集合（Scenario.CommonData.CityLevelTypes）</summary>
        CityLevelType = 21,

        /// <summary>区域集合（Scenario.CommonData.Regions）</summary>
        Region = 22,

        /// <summary>技能集合（Scenario.CommonData.Skills）</summary>
        Skill = 23,

        /// <summary>状态集合（Scenario.CommonData.Buffs）</summary>
        Buff = 24,

        /// <summary>工作类型集合（Scenario.CommonData.JobTypes）</summary>
        JobType = 25,

        /// <summary>武将等级集合（Scenario.CommonData.PersonLevels）</summary>
        PersonLevel = 26,

        /// <summary>武将属性类型集合（Scenario.CommonData.PersonAttributeTypes）</summary>
        PersonAttributeType = 27,

    }

    /// <summary>
    /// 下拉菜单编辑的单个选项
    /// label用于显示，value为确认后回写的值
    /// </summary>
    public class DataEditOption
    {
        /// <summary>显示文本</summary>
        public string label;

        /// <summary>回写值（可以为int、string、SangoObject引用等）</summary>
        public object value;

        public DataEditOption()
        {
        }

        public DataEditOption(string label, object value)
        {
            this.label = label;
            this.value = value;
        }
    }

    /// <summary>
    /// 常用下拉选项预设，供ObjectSortTitle的customData直接复用
    /// 由于选项为只读共享列表，使用时不会被修改，可安全挂到多个标题上
    /// </summary>
    public static class DataEditPresetOptions
    {
        /// <summary>
        /// 性别选项：男=0，女=1
        /// </summary>
        public static readonly List<DataEditOption> SexOptions = new List<DataEditOption>
        {
            new DataEditOption("男", 0),
            new DataEditOption("女", 1),
        };

        /// <summary>
        /// 人物身份选项：君主=1、都督=2、太守=3、一般=4、在野=5、俘虏=6、未登场=7、未发现=8、死亡=9
        /// 与Person/PersonLib的state显示映射保持一致
        /// </summary>
        public static readonly List<DataEditOption> PersonStateOptions = new List<DataEditOption>
        {
            new DataEditOption("君主", 1),
            new DataEditOption("都督", 2),
            new DataEditOption("太守", 3),
            new DataEditOption("一般", 4),
            new DataEditOption("在野", 5),
            new DataEditOption("俘虏", 6),
            new DataEditOption("未登场", 7),
            new DataEditOption("未发现", 8),
            new DataEditOption("死亡", 9),
        };

        /// <summary>
        /// 音声选项：0男鲁莽、1男刚胆、2男冷静、3男小心、4女刚胆、5女冷静、6吕布、7诸葛亮
        /// 与Person.voice取值以及GameMedia.PlayPersonSay的语音映射保持一致
        /// </summary>
        public static readonly List<DataEditOption> VoiceOptions = new List<DataEditOption>
        {
            new DataEditOption("男鲁莽", 0),
            new DataEditOption("男刚胆", 1),
            new DataEditOption("男冷静", 2),
            new DataEditOption("男小心", 3),
            new DataEditOption("女刚胆", 4),
            new DataEditOption("女冷静", 5),
            new DataEditOption("吕布", 6),
            new DataEditOption("诸葛亮", 7),
        };

        /// <summary>
        /// 由枚举类型生成下拉选项（显示枚举项名称，值为枚举的int值）
        /// 用于IntDropdown/EnumDropdown为枚举码字段快速构建候选
        /// </summary>
        /// <typeparam name="TEnum">枚举类型</typeparam>
        /// <param name="nameGetter">名称转换委托，为空时直接使用枚举项名称</param>
        /// <returns>下拉选项列表</returns>
        public static List<DataEditOption> FromEnum<TEnum>(System.Func<TEnum, string> nameGetter = null) where TEnum : struct, System.Enum
        {
            List<DataEditOption> result = new List<DataEditOption>();
            System.Array values = System.Enum.GetValues(typeof(TEnum));
            for (int i = 0; i < values.Length; i++)
            {
                TEnum value = (TEnum)values.GetValue(i);
                string label = nameGetter != null ? nameGetter(value) : value.ToString();
                result.Add(new DataEditOption(label, System.Convert.ToInt32(value)));
            }
            return result;
        }
    }
}
