using Newtonsoft.Json;
using System;

namespace Sango.Core
{
    /// <summary>
    /// 角色兵种适应力（枪/戟/弩/骑/水/器各一份）
    ///
    /// 【三个字段的语义（不可混用）】
    /// <list type="bullet">
    /// <item><c>baseValue</c>：<b>原始适应力</b>，即武将初始的适应等级下标（0 = 最差）。
    /// 剧本编辑、武将库、事件效果都只写这个字段，它是所有折算的起点，任何时候都不要把它当成"当前等级"；</item>
    /// <item><c>valueExp</c>：累计获得的适应经验（总经验，只增不清零），
    /// 单次获取量见 <c>GainValueConfig</c> 的 AbilityExpAttack / AbilityExpDestroyTroop / AbilityExpDestroyCity；</item>
    /// <item><c>value</c>：只读派生量 = 从 baseValue 起、用累计经验逐级升上去之后的<b>当前等级</b>。
    /// 战斗与界面读的是它（见 <see cref="Person.SpearLv"/> 等），存档里只是冗余快照，读档后由 <see cref="Update"/> 重算。</item>
    /// </list>
    ///
    /// 【折算规则】
    /// 升级门槛来自剧本通用数据 <c>AbilityLevelTypes.json</c> 里的 <see cref="AbilityLevelType.exp"/>（每级一条）。
    /// 从 baseValue 开始，逐级扣掉"当前这一级"配置的经验，扣得动就升一级，直到经验不够或到达最高级。
    /// 这样在各级 exp 不相等时结果依然正确，且重复调用 <see cref="Update"/> 结果不变（幂等）。
    /// （旧实现是 <c>valueExp / 当前等级.exp</c> 一步折算：等价于假设所有等级 exp 相同，
    /// 且因为用的是"当前等级"的 exp，连续调用结果会来回跳，已废弃。）
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]

    public class PersonAbilityValue : IAarryDataObject
    {
        /// <summary>原始适应力等级下标（0 = 最差）</summary>
        public int baseValue;

        /// <summary>累计适应经验（总经验，不清零）</summary>
        public int valueExp;

        /// <summary>当前适应等级下标（= 原始适应力 + 经验折算出的提升级数，由 <see cref="Update"/> 重算）</summary>
        public int value;

        public override string ToString()
        {
            return $"{baseValue},{valueExp},{value}";
        }

        /// <summary>
        /// 当前适应等级的配置对象（按 <see cref="value"/> 解析）。
        /// 剧本未运行、数据集为空或下标越界时返回 null，调用方需判空。
        /// </summary>
        public AbilityLevelType AbilityLevelType
        {
            get { return GetLevelType(value); }
        }

        /// <summary>取能力等级数据集；剧本未运行或数据为空时返回 null</summary>
        static SangoObjectSet<AbilityLevelType> GetLevelTypeSet()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null)
                return null;
            SangoObjectSet<AbilityLevelType> set = scenario.CommonData.AbilityLevelTypes;
            if (set == null || set.Count <= 0)
                return null;
            return set;
        }

        /// <summary>
        /// 按等级下标取能力等级配置（不做任何兜底，仅做越界判断）。
        /// </summary>
        /// <param name="level">等级下标</param>
        /// <returns>配置对象；数据集未就绪或下标越界时为 null</returns>
        static AbilityLevelType GetLevelType(int level)
        {
            SangoObjectSet<AbilityLevelType> set = GetLevelTypeSet();
            if (set == null || level < 0 || level >= set.Count)
                return null;
            return set.Get(level);
        }

        /// <summary>
        /// 取指定等级下标的显示名称，供排序/编辑列直接使用（避免调用方解引用 null）。
        /// </summary>
        /// <param name="level">等级下标</param>
        /// <returns>等级名称；解析失败时返回空串</returns>
        public static string GetLevelName(int level)
        {
            AbilityLevelType type = GetLevelType(level);
            return type != null ? type.Name : string.Empty;
        }

        /// <summary>
        /// 重算 <see cref="value"/>：从 <see cref="baseValue"/> 起按累计经验逐级升级。
        /// 幂等：不改动 <see cref="valueExp"/>；只把 baseValue 钳制进 [0, 最高等级]。
        /// </summary>
        public void Update()
        {
            if (baseValue < 0) baseValue = 0;

            SangoObjectSet<AbilityLevelType> set = GetLevelTypeSet();
            if (set == null)
            {
                // 数据集未就绪（如还没进剧本）：退化为基础值，不抛空引用
                value = baseValue;
                return;
            }

            int maxLevel = set.Count - 1;
            if (baseValue > maxLevel) baseValue = maxLevel;

            int level = baseValue;
            int remainExp = valueExp;
            while (level < maxLevel)
            {
                AbilityLevelType cur = set.Get(level);
                int need = cur != null ? cur.exp : 0;
                // 配置缺失或为 0 时按 1 点处理，与旧实现 Math.Max(1, exp) 的口径保持一致；
                // 循环最多推进到 maxLevel，不存在死循环
                if (need < 1) need = 1;
                if (remainExp < need) break;
                remainExp -= need;
                level++;
            }
            value = level;
        }

        /// <summary>
        /// 写入累计经验并重算当前等级。
        /// 已达最高等级时直接返回（不再累计无意义的经验）。
        /// </summary>
        /// <param name="exp">新的累计经验值</param>
        public void SetExp(ushort exp)
        {
            SangoObjectSet<AbilityLevelType> set = GetLevelTypeSet();
            if (set != null && value >= set.Count - 1)
                return;

            if (valueExp != exp)
            {
                valueExp = exp;
                Update();
            }
        }

        public IAarryDataObject FromArray(int[] content)
        {
            int count = content.Length;
            if (count == 0) return this;
            if (count > 0) baseValue = content[0];
            if (count > 1) valueExp = content[1];
            if (count > 2) value = content[2];

            return this;
        }

        public int[] ToArray()
        {
            return new int[] { baseValue, valueExp, value };
        }

        //public override void Load(BinaryReader reader)
        //{
        //    baseValue = reader.ReadByte();
        //    valueExp = reader.ReadUInt16();
        //    Update();
        //}

        //public override void Save(BinaryWriter writer)
        //{
        //    writer.Write(baseValue);
        //    writer.Write(valueExp);
        //}
    }
}
