using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 兵种解析：统一"科技 + 特殊兵道具 + 用户偏好"的判定口径。
    ///
    /// <para>
    /// 【为什么要统一】出征界面、外交补援、AI 出征此前各自拼"可用兵种列表 + 取第一个"，
    /// 判定顺序稍有差异就会导致"界面能选、AI 选不出"或"特殊兵道具武将在城却编不出特殊兵"。
    /// 这里把三处收敛到同一组函数。
    /// </para>
    /// <para>
    /// 【性能】只在出征界面打开 / AI 决策 / 补援时调用，属低频路径；
    /// 内部不用 LINQ、不产生闭包，列表由调用方复用。
    /// </para>
    /// </summary>
    public static class TroopTypeResolver
    {
        /// <summary>
        /// 按武将列表构建陆 / 水可用兵种（科技 + 特殊兵道具过滤）。
        /// </summary>
        /// <param name="persons">判定用武将（通常为空闲武将或已上阵武将）</param>
        /// <param name="land">输出：陆战兵种（可为 null）</param>
        /// <param name="water">输出：水战兵种（可为 null）</param>
        public static void BuildActiveTypes(List<Person> persons, List<TroopType> land, List<TroopType> water)
        {
            if (land != null) land.Clear();
            if (water != null) water.Clear();
            if (persons == null || persons.Count == 0)
                return;

            List<TroopType> all = new List<TroopType>();
            TroopType.CheckActivTroopTypeList(persons, all);

            for (int i = 0; i < all.Count; i++)
            {
                TroopType type = all[i];
                if (type == null)
                    continue;
                if (type.isLand)
                    land?.Add(type);
                else
                    water?.Add(type);
            }
        }

        /// <summary>
        /// 解析最终兵种：优先用户指定 Id，不可用时退回第一个可用兵种。
        /// </summary>
        /// <param name="persons">判定用武将</param>
        /// <param name="force">势力（科技判定；可为 null）</param>
        /// <param name="preferredId">用户选择的兵种 Id（0 = 不指定）</param>
        /// <param name="water">true 取水战，false 取陆战</param>
        /// <returns>可用兵种；一个都没有时返回 null</returns>
        public static TroopType Resolve(List<Person> persons, Force force, int preferredId, bool water)
        {
            return Resolve(persons, force, preferredId, water, 0);
        }

        /// <summary>
        /// 解析最终兵种：玩家选择 &gt; 主将装备提供的兵种 &gt; 第一个可用兵种。
        /// </summary>
        /// <param name="persons">判定用武将</param>
        /// <param name="force">势力（科技判定；可为 null）</param>
        /// <param name="preferredId">用户选择的兵种 Id（0 = 不指定）</param>
        /// <param name="water">true 取水战，false 取陆战</param>
        /// <param name="equipTroopTypeId">主将装备提供的兵种 Id（0 = 无）</param>
        /// <summary>已报过"装备兵种不可用"的 Id，避免每次出征刷屏</summary>
        static readonly HashSet<int> reportedEquipTypes = new HashSet<int>();

        public static TroopType Resolve(List<Person> persons, Force force, int preferredId, bool water, int equipTroopTypeId)
        {
            if (persons == null || persons.Count == 0)
                return null;

            List<TroopType> all = new List<TroopType>();
            TroopType.CheckActivTroopTypeList(persons, all);

            TroopType first = null;
            TroopType equip = null;
            for (int i = 0; i < all.Count; i++)
            {
                TroopType type = all[i];
                if (type == null || type.isLand == water)
                    continue;

                if (preferredId > 0 && type.Id == preferredId && IsUsable(force, persons, type))
                    return type;

                // 装备提供的兵种：只校验科技，不要求特殊兵道具（装备本身就是来源）
                if (equipTroopTypeId > 0 && type.Id == equipTroopTypeId && type.IsValid(force))
                    equip = type;

                if (first == null && IsUsable(force, persons, type))
                    first = type;
            }
            if (equipTroopTypeId > 0 && equip == null && reportedEquipTypes.Add(equipTroopTypeId))
            {
                Sango.Log.Warning($"[兵种] 装备提供的兵种 Id={equipTroopTypeId} 不在当前可用列表（科技未解锁或兵种不存在），已回退默认兵种");
            }

            return equip != null ? equip : first;
        }

        /// <summary>取武将装备提供的兵种 Id（武器 / 铠甲 / 马 / 宝物，优先非 0）</summary>
        public static int GetEquipTroopTypeId(Person person)
        {
            if (person == null)
                return 0;

            if (person.EquippedWeapon != null && person.EquippedWeapon.troopTypeId > 0)
                return person.EquippedWeapon.troopTypeId;
            if (person.EquippedArmor != null && person.EquippedArmor.troopTypeId > 0)
                return person.EquippedArmor.troopTypeId;
            if (person.EquippedHorse != null && person.EquippedHorse.troopTypeId > 0)
                return person.EquippedHorse.troopTypeId;
            if (person.EquippedTreasure != null && person.EquippedTreasure.troopTypeId > 0)
                return person.EquippedTreasure.troopTypeId;
            return 0;
        }

        /// <summary>
        /// 该兵种能否由这批武将组建（科技 + 特殊兵道具 + 特技解锁）。
        ///
        /// 【性能·原条件优先】科技满足且（无兵符 / 有人持有点）时直接返回 true，
        /// **不做任何特技展开** —— 这是出征界面每次换将、每次点兵种都会走的常见路径。
        /// 只有原条件不满足时才查一次特技权限，代价与改动前的最坏情况相同。
        /// </summary>
        /// <param name="force">势力（科技判定；可为 null）</param>
        /// <param name="persons">上阵武将</param>
        /// <param name="troopType">待判定兵种</param>
        /// <returns>可组建返回 true</returns>
        public static bool IsUsable(Force force, List<Person> persons, TroopType troopType)
        {
            if (troopType == null)
                return false;

            // ① 原路径：科技满足
            if (troopType.IsValid(force))
            {
                // 普通兵种：科技满足即可，直接返回（最常见分支，零额外开销）
                if (troopType.validItemId == 0)
                    return true;

                // 特殊兵：上阵武将中至少一人持有对应道具
                if (persons != null)
                {
                    for (int i = 0; i < persons.Count; i++)
                    {
                        Person person = persons[i];
                        if (person != null && person.HasItem(troopType.validItemId))
                            return true;
                    }
                }
            }

            // ② 特技路径：原条件不满足时才展开（额外解锁，口径与 CheckActivTroopTypeList 一致）
            return FeaturePermission.AnyPersonAllowsTroopType(persons, troopType);
        }
    }
}
