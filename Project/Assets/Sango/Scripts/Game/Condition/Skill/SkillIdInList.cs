using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    public class SkillIdInList : Condition
    {
        List<int> ids;
        // 比较结果: 0不包含 1包含
        int result;
        /// <summary>ids 缺失的告警是否已打印过（避免每支部队重算都刷一条）</summary>
        static bool warnedMissingIds;

        public override bool Check(IConditionDatabase database)
        {
            SkillInstance skill = database.ActionSkill;
            if (skill != null)
            {
                // 【防御】ids 为 null 时原实现会直接抛 NullReferenceException：
                //   实测栈 TroopChangeSkillAttackRange.OnTroopCalculateAttribute → SkillIdInList.Check，
                //   一旦触发会让整支部队属性重算（乃至出征流程）中断。
                // 这里按"不包含"处理并打印一次诊断信息，区分两种成因：
                //   result == 0 → Init 根本没跑完（被异常中断），ids 从未被赋值；
                //   result != 0 → Init 跑完了，但传入的 condition 对象里没有 ids 字段。
                if (ids == null)
                {
                    if (!warnedMissingIds)
                    {
                        warnedMissingIds = true;
                        Sango.Log.Error(
                            $"SkillIdInList 条件缺少 ids 配置（result={result}）：" +
                            (result == 0
                                ? "Init 可能未执行或被异常中断，已按'不包含'处理"
                                : "condition 对象里没有 ids 字段，已按'不包含'处理"));
                    }
                    return false;
                }
                return ids.Contains(skill.Id) == (result == 1);
            }
            return false;
        }

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (p == null)
            {
                Sango.Log.Error("SkillIdInList 初始化失败：传入的配置对象为 null，已按空列表处理");
                ids = new List<int>();
                return;
            }

            JArray kindsArray = p.Value<JArray>("ids");
            if (kindsArray != null)
            {
                ids = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    ids.Add(kindsArray[i].ToObject<int>());
            }
            else
            {
                // 配置里没有 ids（或类型不是数组）时，必须兜成空列表：
                // 否则 Check 里的 ids.Contains 会抛 NullReferenceException
                Sango.Log.Error($"SkillIdInList 配置缺少 ids 字段或类型不是数组，已按空列表处理。配置内容：{p.ToString(Newtonsoft.Json.Formatting.None)}");
                ids = new List<int>();
            }
            result = p.Value<int>("result");
        }
    }
}
