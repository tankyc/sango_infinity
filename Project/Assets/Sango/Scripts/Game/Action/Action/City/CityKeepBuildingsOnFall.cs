using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 城陷时保留更多内政设施（安民「占领城池保留建筑」）。
    ///
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnCityCalculateBuildingKeepCount"/>，
    /// 由 <c>City.OnFall</c> 在计算"攻方主将魅力 / 20"的保留名额时询问。
    /// 本类只追加名额，不改动原版的候选取舍流程（打乱、按名额摧毁、其余随城改属）。
    /// </para>
    /// <para>
    /// 【只对攻方生效】"保留建筑"是占领方视角：只有当持有者正作为攻方破城时才追加名额。
    /// 守方持有本特技不会产生任何效果（守方的设施本来就按原版规则处理）。
    /// </para>
    /// <para>
    /// 【宿主】可配在部队（特技）上，也可配在势力上（全势力攻城通用）。
    /// 两者都为空时打 Warning 并不订阅。
    /// </para>
    /// </summary>
    public class CityKeepBuildingsOnFall : TroopTroopActionBase
    {
        /// <summary>追加的保留座数</summary>
        int addCount;

        /// <summary>1 = 全部保留（优先于 addCount）</summary>
        int keepAll;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value（追加座数）、keepAll（0/1）、condition。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            addCount = value;
            keepAll = p.Value<int>("keepAll");

            if (Troop == null && Force == null)
            {
                Sango.Log.Warning("CityKeepBuildingsOnFall 既没有绑定部队也没有绑定势力，不会生效");
                return;
            }
            if (addCount <= 0 && keepAll == 0)
            {
                Sango.Log.Warning("CityKeepBuildingsOnFall 的 value / keepAll 都未配置，不会生效");
                return;
            }

            GameEvent.OnCityCalculateBuildingKeepCount += OnCityCalculateBuildingKeepCount;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCityCalculateBuildingKeepCount -= OnCityCalculateBuildingKeepCount;
        }

        /// <summary>
        /// 追加内政设施保留名额。
        /// </summary>
        /// <param name="city">被攻陷的城池</param>
        /// <param name="atk">攻方部队</param>
        /// <param name="keepData">可保留座数（初始 = 攻方主将魅力 / 20）</param>
        void OnCityCalculateBuildingKeepCount(City city, Troop atk, Tools.OverrideData<int> keepData)
        {
            if (atk == null || keepData == null)
                return;

            // 归属过滤：持有者必须是这次的攻方
            if (Troop != null)
            {
                if (atk != Troop)
                    return;
            }
            else if (Force != null)
            {
                if (atk.BelongForce != Force)
                    return;
            }
            else
            {
                return;
            }

            if (condition != null && !condition.Check(new TroopConditionDatabase(atk)))
                return;

            if (keepAll == 1)
            {
                // 用 int.MaxValue 表达"全部保留"：OnFall 里取 Min(devCount, keepCount)，
                // 结果恒为 devCount，摧毁 0 座，且不会溢出。
                keepData.Value = int.MaxValue;
                return;
            }

            keepData.Value += addCount;
        }
    }
}
