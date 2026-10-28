using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 给部队设置寻路 / 地形免疫标记位（<see cref="PathFlag"/>）。
    ///
    /// 参数：
    ///   flag   —— <see cref="PathFlag"/> 的位值（1 毒泉 / 2 栈道 / 4 陆 ZOC / 8 水 ZOC / 16 火焰）
    ///   value  —— 1 = 置位（免疫），0 = 清位
    ///
    /// 接入事件：OnTroopCalculateAttribute（属性重算时写入，供寻路与移动伤害读取）。
    ///
    /// 【用途】原版的"有某特技/科技就无视某惩罚"都是打标记，不是写死分支：
    ///   · 解毒（特技 9）      → flag 1，免毒泉移动伤害与寻路代价
    ///   · 踏破（特技 7）      → flag 2，免栈道移动伤害与寻路代价
    ///   · 藤甲（特技 32）     → flag 2（藤甲同样不怕栈道），受火伤害加倍由别的 Action 处理
    /// 新增"沼泽免疫"之类的机制只需在 PathFlag 加一位，代码不用改。
    /// </summary>
    public class TroopSetPathFlag : TroopActionBase
    {
        /// <summary>要设置的标记位（PathFlag 的位值）</summary>
        int flag;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            flag = p != null && p["flag"] != null ? p.Value<int>("flag") : 0;

            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;
            if (flag == 0) return;

            if (value == 1)
                troop.pathFlags |= flag;
            else
                troop.pathFlags &= ~flag;
        }
    }
}
