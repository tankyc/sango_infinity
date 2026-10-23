using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Sango.Core.Action;
using Sango.Render;
using UnityEngine;
using System.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 势力类，继承自SangoObject，用于管理游戏中的势力对象
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Force : SangoObjectExtensionData
    {
        /// <summary>
        /// 获取势力的对象类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.Force; } }

        /// <summary>
        /// 获取或设置AI是否完成
        /// </summary>
        public virtual bool AIFinished { get; set; }

        /// <summary>
        /// 获取或设置AI是否准备就绪
        /// </summary>
        public virtual bool AIPrepared { get; set; }

        /// <summary>
        /// 获取或设置是否为玩家势力
        /// </summary>
        public virtual bool IsPlayer { get; set; }

        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => IsPlayer && this == Scenario.Cur.CurRunForce;

        /// <summary>
        /// 控制权移交的"待生效"请求：-1 = 无；0 = 下个本势力回合交给AI（委托给AI）；1 = 下个本势力回合交给玩家（中途参加）。
        ///
        /// 为什么需要它：回合中途直接翻 <see cref="IsPlayer"/> 会出两类状态破坏 ——
        ///   · 玩家已经动过的回合里，<c>Force.Run</c> 会立刻转入 <c>DoAI</c>，
        ///     科技 / 官职 / 俘虏 / 计略在同一回合再结算一遍（一次回合两份收益）；
        ///   · AI 正在跨帧执行命令队列时被接管，半截队列会残留到下次 <c>AIPrepare</c> 之后被重复执行。
        /// 所以只允许在 <see cref="OnForceTurnStart"/>（AI 状态本来就在此整体复位）这个干净边界上生效。
        /// 不序列化：存档里的权威口径是 <c>ScenarioInfo.playerForceList</c>，读档时 <c>CheckPlayer</c> 直接按它重建。
        /// 唯一写入方是 <c>ForceControlService</c>。
        /// </summary>
        internal int pendingPlayerControl = -1;

        private bool isAlive;

        /// <summary>
        /// 获取或设置势力是否存活
        /// </summary>
        public override bool IsAlive
        {
            get
            {
                // 【与原版对齐】Open311 的 Force::is_alive() 只判"是否还有君主"（kunshu_ >= 0），
                // 与城池无关。原实现额外要求 mGovernor.BelongCity != null，
                // 会把以下三种正常状态误判成"势力已灭亡"：
                //   · 君主正在城与城之间移动（在途时户口/所在地可能短暂为空）；
                //   · 君主随军出征、主城刚失陷但势力还有其它城；
                //   · 君主刚死亡、继位者尚未接管的同一帧。
                // 一旦误判，该势力会从 UI 列表与 AI 循环里消失，城池却仍挂着它的军团。
                // 真正的灭亡由 City.OnFall 在"最后一座城陷落"时显式置 IsAlive = false。
                // 这里额外要求君主本人存活，避免"君主已死但 flag 还没翻"的窗口期。
                return isAlive && mGovernor != null && mGovernor.IsAlive && !mGovernor.IsDead;
            }
            set
            {
                isAlive = value;
            }
        }

        /// <summary>
        /// 势力是否还有首都（君主所在地）。
        ///
        /// 与 <see cref="IsAlive"/> 解耦：势力可以暂时没有首都
        /// （君主在途、随军出征、刚被攻陷主城）但依然存活。
        /// 需要"势力是否还有立足点"的判断请用这个，不要用 IsAlive。
        /// </summary>
        public bool HasCapital => mGovernor != null && mGovernor.BelongCity != null;

        /// <summary>
        /// 势力是否还有据点（对应 Open311 的 force_is_not_empty 的城池部分）。
        /// 与 IsAlive 解耦：原版靠它 + "有武将"共同判定势力是否该灭亡。
        /// </summary>
        public bool HasCity => CityCount > 0 || CityBaseCount > 0;

        /// <summary>
        /// 获取势力的名称
        /// </summary>
        public override string Name => mGovernor?.Name;

        /// <summary>
        /// 获取势力的颜色名称
        /// </summary>
        public virtual string ColorName => mGovernor?.ColorName;

        /// <summary>
        /// 首都, 必须是君主所在城市,无论港关
        /// </summary>
        public City CapitalCity => mGovernor?.BelongCity;

        /// <summary>
        /// 第一军团
        /// </summary>
        public Corps CapitalCorps => mGovernor?.BelongCorps;

        /// <summary>
        /// 主公
        /// </summary>
        [JsonProperty]
        public int Governor;

        public Person mGovernor;

        /// <summary>
        /// 军师
        /// </summary>
        [JsonProperty]
        public int Counsellor;
        public Person mCounsellor;

        /// <summary>
        /// 旗帜
        /// </summary>
        [JsonProperty]
        public int Flag;

        public Flag mFlag { get; set; }

        /// <summary>
        /// 爵位的 id（存档数值），读取 Title 时按需解析
        /// </summary>
        [JsonProperty("Title")]
        public int TitleId;

        Title mTitle;
        /// <summary>
        /// 爵位；写入时自动同步 TitleId
        /// </summary>
        public Title Title
        {
            get
            {
                if (mTitle == null && TitleId > 0) mTitle = IdRef.Resolve<Title>(TitleId);
                return mTitle;
            }
            set { mTitle = value; TitleId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 联盟信息（序列化形态 = int[]，键名不变；运行期列表在 OnScenarioPrepare 解析、OnScenarioSave 回写）
        /// </summary>
        [JsonProperty("AllianceList")]
        public int[] AllianceList_list;
        public SangoObjectList<Alliance> AllianceList = new SangoObjectList<Alliance>();

        /// <summary>
        /// 势力独有的初始化科技信息,以此为入口,且可以为势力设置独特的科技树
        /// </summary>
        [JsonProperty("InitTechniques")]
        public int[] InitTechniques_list;
        public SangoObjectList<Technique> InitTechniques = new SangoObjectList<Technique>();

        /// <summary>
        /// 已完成的科技信息
        /// </summary>
        [JsonProperty("Techniques")]
        public int[] Techniques_list;
        public SangoObjectList<Technique> Techniques = new SangoObjectList<Technique>();

        /// <summary>
        /// 本国被俘虏
        /// </summary>
        public List<Person> BeCaptiveList = new List<Person>();

        /// <summary>
        /// 技巧点数
        /// </summary>
        [JsonProperty] public int TechniquePoint { get; private set; }

        /// <summary>
        /// 霸业点数
        /// </summary>
        [JsonProperty] public int HegemonyPoint { get; set; }

        /// <summary>
        /// 势力方针
        /// </summary>
        [JsonProperty] public int PolicyType { get; set; }

        /// <summary>
        /// 国库（存放宝物与装备等珍品）。
        ///
        /// 与城市道具库的分工：兵装 / 器械 / 船存各城的 <c>City.itemStore</c>（生产、运输、出征消耗都在那边），
        /// 只有 kind = <see cref="ItemKindType.Treasure"/> / <see cref="ItemKindType.Equipment"/> 的珍品入国库。
        /// 珍品的 storeKind 即其自身 Id，因此每件道具在国库中独占一格（数量恒为 0 或 1），
        /// 授予 / 没收都只在本表上做加减。
        ///
        /// 老存档没有该字段，读档时缺省为空国库，无需迁移。
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(ItemStoreConverter))]
        public ItemStore Treasury = new ItemStore();

        /// <summary>
        /// 本势力上次产出宝物 / 装备的回合号（&lt;= 0 表示从未产出）。
        /// 与 <see cref="ScenarioVariables.itemDiscoverGain"/> 的势力冷却配合使用，见 <see cref="ForceItemDiscover"/>。
        /// </summary>
        [JsonProperty] public int lastItemDiscoverTurn = -1;

        /// <summary>
        /// 本势力历史城池数峰值。
        /// 产出累计上限按峰值算（<see cref="ForceItemDiscover"/> 第④层），避免被打残后又能重新刷满额度。
        /// </summary>
        [JsonProperty] public int peakCityCount = 0;

        /// <summary>
        /// 本势力已产出的珍品 Id 清单（保证每件道具本局只出现一次）。
        /// </summary>
        [JsonProperty] public List<int> discoveredItemIds = new List<int>();

        /// <summary>
        /// 当前研究的技术
        /// </summary>
        [JsonProperty]
        public int ResearchTechnique { get; set; }

        /// <summary>
        /// 当前研究的技术剩余回合
        /// </summary>
        [JsonProperty]
        public int ResearchLeftCounter { get; set; }

        /// <summary>
        /// AI指令集
        /// </summary>
        public List<System.Func<Force, Scenario, bool>> AICommandList = new List<System.Func<Force, Scenario, bool>>();

        /// <summary>
        /// AI 命令队列的读取游标。
        ///
        /// 旧实现每执行完一条命令就 <c>AICommandList.RemoveAt(0)</c>，整队跑完是 O(n²) 次数组搬移。
        /// 改成游标前进、跑完整队后一次性 <c>Clear</c>，摊还成 O(n)（与 RenderEvent 的事件队列同一取舍）。
        /// 语义不变：命令返回 false 时游标停在原地，下次进入从同一条命令继续。
        /// </summary>
        int aiCommandIndex = 0;

        /// <summary>
        /// 威胁过本势力的敌方部队ID列表(用于 AI 主动驱逐)。
        /// 敌方部队攻击本势力的城池 / 建筑 / 部队时会被记录,只要其仍存活且位于本势力领地内,
        /// 就会由距离最近的城市派兵歼灭。
        /// </summary>
        [JsonProperty]
        public List<int> threatTroopIds = new List<int>();

        /// <summary>
        /// 记录一个对本势力实施过攻击的敌方部队。
        /// </summary>
        /// <param name="troop">实施攻击的敌方部队</param>
        public void MarkThreatTroop(Troop troop)
        {
            if (troop == null || !troop.IsAlive)
                return;
            // 只记"现在仍敌对"的部队:登庸主将等换旗场景下它可能已经不是敌人了
            if (!IsHostile(troop))
                return;
            if (threatTroopIds == null)
                threatTroopIds = new List<int>();
            if (threatTroopIds.Contains(troop.Id))
                return;
            threatTroopIds.Add(troop.Id);
        }

        /// <summary>
        /// 把某支部队从威胁名单里移除。
        /// 用途:部队换旗(登庸主将)、双方结盟 / 停战之后,立刻停止把它当驱逐目标,
        /// 否则 AI 会在同一个回合里派兵去打"已经变成自己人"的部队。
        /// </summary>
        /// <param name="troop">要移除的部队,可为空</param>
        public void RemoveThreatTroop(Troop troop)
        {
            if (troop == null) return;
            if (threatTroopIds == null || threatTroopIds.Count == 0) return;
            threatTroopIds.Remove(troop.Id);
        }

        /// <summary>
        /// 该部队现在是否仍应被本势力视为驱逐目标:存活、有归属、且与本势力敌对。
        /// 部队换旗后这里立刻变为 false,驱逐类 AI(FindThreatTroopInRange / HasThreatTroop)就不会误伤友军。
        /// </summary>
        /// <param name="troop">待判定的部队</param>
        /// <returns>是否仍是威胁</returns>
        bool IsHostile(Troop troop)
        {
            if (troop == null || !troop.IsAlive) return false;
            // 无归属的部队(主将已脱离但尚未收尾)不属于任何势力,不做驱逐
            if (troop.BelongForce == null) return false;
            return IsEnemy(troop.BelongForce);
        }

        /// <summary>
        /// 清理已阵亡、不存在或已不再敌对的威胁部队记录。
        ///
        /// 【为什么还要判敌我】以前只按"是否存活"过滤,部队换个势力(登庸)之后仍留在名单里,
        /// 只能等到下一次势力回合开始时处理,于是当回合内 AI 会照着旧名单去打自己人/友军。
        /// </summary>
        public void CleanupThreatTroops()
        {
            if (threatTroopIds == null || threatTroopIds.Count == 0)
                return;
            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            for (int i = threatTroopIds.Count - 1; i >= 0; i--)
            {
                Troop troop = scenario.troopsSet.Get(threatTroopIds[i]);
                if (troop == null || !IsHostile(troop))
                    threatTroopIds.RemoveAt(i);
            }
        }

        /// <summary>
        /// 相邻势力
        /// </summary>
        public List<Force> NeighborForceList = new List<Force>();

        /// <summary>
        /// 相邻非本势力的城市
        /// </summary>
        public List<City> NeighborCityList = new List<City>();

        /// <summary>
        /// 本势力的城市
        /// </summary>
        public List<City> CityList = new List<City>();

        /// <summary>
        /// 国力值
        /// </summary>
        public int FightPower;

        /// <summary>
        /// 执行建筑行为的建筑列表(建筑攻击等)
        /// </summary>
        Queue<BuildingBase> buildingBaseList = new Queue<BuildingBase>();

        /// <summary>
        /// 当前可以研发的科技列表
        /// </summary>
        public List<Technique> canResearchTechniqueList = new List<Technique>();

        /// <summary>
        /// 国家科技树
        /// </summary>
        public List<ForceTechnique> techniqueTree = new List<ForceTechnique>();

        /// <summary>
        /// 科技树最大等级
        /// </summary>
        public int techniqueMaxLevel = 0;

        /// <summary>
        /// 科技树最大行数
        /// </summary>
        public int techniqueMaxRow = 0;

        /// <summary>
        /// 可创建的物品类型列表
        /// </summary>
        public List<ItemType> createdItemTypes = new List<ItemType>();

        /// <summary>
        /// 判断是否与另一势力为敌对关系
        /// </summary>
        /// <param name="force">另一势力</param>
        /// <returns>是否为敌对关系</returns>
        public bool IsEnemy(Force force) { return IsEnemy(this, force); }

        /// <summary>
        /// 势力拥有的武将数量
        /// </summary>
        public int PersonCount { get; set; }

        /// <summary>
        /// 势力拥有的城市数量
        /// </summary>
        public int CityCount { get; set; }

        /// <summary>
        /// 势力拥有的城寨数量
        /// </summary>
        public int CityBaseCount { get; set; }

        /// <summary>
        /// 势力的颜色
        /// </summary>
        public Color Color => mFlag.color;

        /// <summary>
        /// 当前运行的军团
        /// </summary>
        public Corps CurRunCorps { get; set; }

        /// <summary>
        /// 势力的行动列表
        /// </summary>
        public List<ActionBase> actionList;

        /// <summary>
        /// 外交失败次数记录 (key: 目标势力ID, value: 失败次数)
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> DiplomacyFailCount = new Dictionary<int, int>();

        /// <summary>
        /// 外交免疫时间记录 (key: 目标势力ID, value: 免疫结束时间)
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> DiplomacyImmunityTime = new Dictionary<int, int>();

        // ================= 原版外交计时器（对齐 Open311 s11_scen.h:882-900）=================
        //
        // 原版三套计时器都以「周」为单位并逐周递减（s11_game_week.cpp:259-319）；
        // 本项目一个回合 = 一旬，这里统一按「回合」递减，语义等价（换算只影响显示文案）。

        /// <summary>
        /// 我方请求援军的对象势力 Id（-1 = 无请求）。对应 Open311 的 request_force_。
        /// 只有「援军请求」成功时写入，到期或被撤回时清回 -1。
        /// </summary>
        [JsonProperty] public int requestForceId = -1;

        /// <summary>
        /// 援军请求的剩余回合数。归零时要撤回当初为此派出的部队。对应 request_timer_。
        /// </summary>
        [JsonProperty] public int requestTimer = 0;

        /// <summary>
        /// 外交禁令剩余回合数：我方（玩家）提案被 AI 拒绝后进入禁谈期，期间 AI 不再受理我方提案。
        /// 对应 Open311 的 diplomacy_ban_timer_（失败分支里的 force_set_diplomacy_ban_timer(9)）。
        /// </summary>
        [JsonProperty] public int diplomacyBanTimer = 0;

        /// <summary>
        /// 对指定势力的友好度（0~100，50 = 中立）。对齐 Open311 的 Force::get_like。
        ///
        /// 【为什么做换算而不是新增字段】本项目的关系值存在 Scenario.RelationMap
        /// （-5000~5000，且 Scenario.AddRelation 是**双向写入**，本身已经对称），
        /// 与 Open311 的 like（0~100、同样对称）只差一个尺度：
        ///     relation = (like - 50) * 100
        /// 保留原存储可以兼容老存档与现有 UI，同时让原版判定公式可以逐行照抄。
        /// </summary>
        /// <param name="other">目标势力</param>
        /// <returns>0~100 的友好度，取不到场景时返回中立值 50</returns>
        public int GetLike(Force other)
        {
            if (other == null || other == this) return 50;

            Scenario scenario = Scenario.Cur;
            if (scenario == null) return 50;

            return 50 + scenario.GetRelation(this, other) / 100;
        }

        /// <summary>
        /// 设置友好度（0~100），内部换算回 RelationMap。对应 Open311 的 Force::set_like。
        /// </summary>
        /// <param name="other">目标势力</param>
        /// <param name="like">目标友好度，超出 0~100 会被截断</param>
        public void SetLike(Force other, int like)
        {
            if (other == null || other == this) return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null) return;

            if (like < 0) like = 0;
            else if (like > 100) like = 100;

            int target = (like - 50) * 100;
            scenario.AddRelation(this, other, target - scenario.GetRelation(this, other));
        }

        /// <summary>
        /// 增减友好度（双向对称写入）。对应 Open311 的 force_add_like（s11_sys_scen_force.cpp:120-134）。
        /// </summary>
        /// <param name="other">目标势力</param>
        /// <param name="delta">变化量，可为负</param>
        public void AddLike(Force other, int delta)
        {
            if (other == null || other == this || delta == 0) return;
            SetLike(other, GetLike(other) + delta);
        }

        /// <summary>
        /// 被敌方城市计略（目前只有流言）命中的记录 (key: 施计方势力ID, value: 最近一次被施计的回合)。
        /// 沿用 DiplomacyImmunityTime 的既有写法：只存绝对回合数（Scenario.TurnCount），
        /// 过期靠比较而不是逐回合递减，因此不需要挂 OnTurnStart 也不会被读档顺序打乱。
        /// 用途：AI 在自己的回合里据此对施计方报复性施计，对应原版解密脚本 721 AI优化-流言 的 rumor_timer 语义。
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> CityStrategyGrudgeTurn = new Dictionary<int, int>();

        /// <summary>
        /// 被二虎竞食挑动的记录 (key: 被一起挑拨的对方势力ID, value: 挑拨落地的回合)。
        /// 二虎竞食只改关系数值，不直接动同盟结构；"关系跌破阈值后要不要破盟开战"留给被挑拨的
        /// 两方在各自 AI 回合里判定（CityStrategyAI），本字段就是那次计略的凭据，且是一次性消耗品。
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> StrategyWarSeedTurn = new Dictionary<int, int>();

        /// <summary>
        /// 本势力最近一次派遣流言使者的回合（Scenario.TurnCount）。小于等于 0 表示从未派遣。
        /// 用途：势力级流言配额（ScenarioVariables.cityStrategyAIMaxRumorPerTurns），
        /// 把"每个 AI 每回合都可能喷一次"降为"每若干回合才一次"，掐断 AI 互相刷流言的节奏。
        /// </summary>
        [JsonProperty] public int LastCityStrategyRumorTurn;

        /// <summary>
        /// 记下一笔计略仇：本势力被 attacker 施了计略。
        /// </summary>
        /// <param name="attacker">施计方势力，为空时不记录</param>
        public void AddCityStrategyGrudge(Force attacker)
        {
            if (attacker == null || attacker == this)
                return;
            CityStrategyGrudgeTurn[attacker.Id] = Scenario.Cur?.TurnCount ?? 0;
        }

        /// <summary>
        /// 取走一笔计略仇：一次性消费，取过就不再驱动第二次报复。
        /// 与 TakeStrategyWarSeed 同一思路，避免"你喷我、我喷你"的正反馈在后续每个 AI 回合里反复放大。
        /// 陈旧凭据同样消费掉（返回 false），只保留"最近一次"被施计的有效期。
        /// </summary>
        /// <param name="attacker">施计方势力</param>
        /// <returns>存在未消费且未过期的凭据返回 true</returns>
        public bool TakeCityStrategyGrudge(Force attacker)
        {
            if (attacker == null)
                return false;
            if (!CityStrategyGrudgeTurn.TryGetValue(attacker.Id, out int turn))
                return false;
            CityStrategyGrudgeTurn.Remove(attacker.Id);
            int keepTurns = Scenario.Cur?.Variables.cityStrategyGrudgeKeepTurns ?? 0;
            return (Scenario.Cur?.TurnCount ?? 0) - turn <= keepTurns;
        }

        /// <summary>
        /// 记下二虎竞食的挑拨凭据：本势力与 other 的关系刚刚被第三方打坏。
        /// 由 CityStrategyActionTwoTigers 在成功结算时调用，双方各记一份。
        /// </summary>
        /// <param name="other">一同被挑拨的对方势力</param>
        public void MarkStrategyWarSeed(Force other)
        {
            if (other == null || other == this)
                return;
            StrategyWarSeedTurn[other.Id] = Scenario.Cur?.TurnCount ?? 0;
        }

        /// <summary>
        /// 取走二虎竞食的挑拨凭据：一次性消费，取过就不再触发第二次破盟，
        /// 避免同一笔账在后续每个 AI 回合里反复放大。
        /// </summary>
        /// <param name="other">被一起挑拨的对方势力</param>
        /// <returns>存在未消费的凭据返回 true</returns>
        public bool TakeStrategyWarSeed(Force other)
        {
            if (other == null)
                return false;
            if (!StrategyWarSeedTurn.TryGetValue(other.Id, out int turn))
                return false;
            // 陈旧凭据同样消费掉：只保留"最近一次"挑拨的效力，过期即作废
            StrategyWarSeedTurn.Remove(other.Id);
            int keepTurns = Scenario.Cur?.Variables.cityStrategyGrudgeKeepTurns ?? 0;
            return (Scenario.Cur?.TurnCount ?? 0) - turn <= keepTurns;
        }

        /// <summary>
        /// 清掉过期的计略记录，防止两个字典在长局里无上限增长。
        /// 只由 AI 回合（CityStrategyAI）调用，不参与判定逻辑。
        /// </summary>
        /// <param name="keepTurns">超过该回合数的记录视为过期</param>
        public void CleanupCityStrategyMarks(int keepTurns)
        {
            if (keepTurns <= 0)
                return;
            PruneTurnDictionary(CityStrategyGrudgeTurn, keepTurns);
            PruneTurnDictionary(StrategyWarSeedTurn, keepTurns);
        }

        /// <summary>
        /// 按"记录时点 + 保留窗口"淘汰过期项。
        /// </summary>
        /// <param name="record">key 为对方势力ID、value 为落地回合的字典</param>
        /// <param name="keepTurns">保留窗口（回合）</param>
        private static void PruneTurnDictionary(Dictionary<int, int> record, int keepTurns)
        {
            if (record == null || record.Count == 0)
                return;
            int now = Scenario.Cur?.TurnCount ?? 0;
            List<int> expired = null;
            foreach (KeyValuePair<int, int> pair in record)
            {
                if (now - pair.Value > keepTurns)
                {
                    expired = expired ?? new List<int>();
                    expired.Add(pair.Key);
                }
            }
            if (expired == null)
                return;
            for (int i = 0; i < expired.Count; i++)
                record.Remove(expired[i]);
        }

        /// <summary>
        /// 能够建造的建筑集合
        /// </summary>
        public List<BuildingType> canBuildMilitaryBuildingType = new List<BuildingType>();


        public override void OnScenarioPrepare(Scenario scenario)
        {
            if (Governor > 0)
                mGovernor = scenario.personSet.Get(Governor);
            if (Counsellor > 0)
                mCounsellor = scenario.personSet.Get(Counsellor);
            mFlag = scenario.CommonData.Flags.Get(Flag);
            if (mFlag == null)
                mFlag = scenario.CommonData.Flags.Get(0);

            // 【int[] → 对象】本势力序列化列表的解析（追加进已有方法）
            if (AllianceList_list != null && AllianceList_list.Length > 0 && AllianceList.Count == 0)
                AllianceList.FromArray(AllianceList_list);
            if (InitTechniques_list != null && InitTechniques_list.Length > 0 && InitTechniques.Count == 0)
                InitTechniques.FromArray(InitTechniques_list);
            if (Techniques_list != null && Techniques_list.Length > 0 && Techniques.Count == 0)
                Techniques.FromArray(Techniques_list);
        }

        public override void OnScenarioSave(Scenario scenario)
        {
            Governor = mGovernor?.Id ?? 0;
            Counsellor = mCounsellor?.Id ?? 0;

            // 存档前回写 int[]（追加进已有方法；否则会把读档时的旧 id 存回去）
            AllianceList_list = AllianceList != null ? AllianceList.ToArray() : null;
            InitTechniques_list = InitTechniques != null ? InitTechniques.ToArray() : null;
            Techniques_list = Techniques != null ? Techniques.ToArray() : null;
        }

        /// <summary>
        /// 初始化势力
        /// </summary>
        /// <param name="scenario">当前场景</param>
        public override void Init(Scenario scenario)
        {
            if (mGovernor == null)
            {
                IsAlive = false;
                return;
            }

            actionList = new List<ActionBase>();
            Techniques.ForEach(x =>
            {
                x.InitActions(actionList, this);
            });
            InitTechniquesTree(scenario);
            UpdateTurnInfo(scenario, false);

            Sango.Log.Info($"{ColorName} CityCount = {CityCount}");
        }

        public override void Clear()
        {
            base.Clear();

            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
                actionList = null;
            }


        }

        public void UpdateCanBuildBuildingTypes()
        {
            canBuildMilitaryBuildingType.Clear();
            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
            {
                if (!x.IsIntrior && x.IsValid(this) && x.canBuild)
                {
                    if (x.level > 1)
                    {
                        canBuildMilitaryBuildingType.RemoveAll(b => b.kind == x.kind && b.level < x.level);
                        canBuildMilitaryBuildingType.Add(x);
                    }
                    else
                    {
                        // 如果有level比这个大的 则不添加
                        if (!canBuildMilitaryBuildingType.Exists(b => b.kind == x.kind && x.level > 1))
                            canBuildMilitaryBuildingType.Add(x);
                    }
                }
            });
        }

        /// <summary>
        /// 授予一项技巧（供剧本事件、外交请求等外部流程使用）。
        ///
        /// 【为什么必须封装在这里】授予技巧不是"往 Techniques 里加一条"那么简单：
        ///   · Techniques 是运行期对象列表，存档字段是 Techniques_list，两者要同步；
        ///   · 技巧的被动效果要靠 <see cref="Technique.InitActions"/> 注册进 actionList，
        ///     漏了就会出现"科技树里已点亮、实际效果却全都没有"；
        ///   · techniqueTree / canBuildMilitaryBuildingType 都是派生数据，必须重建。
        /// 这三件事都在 Force 内部，外部拼不出来，因此提供这个入口。
        /// </summary>
        /// <param name="technique">技巧</param>
        /// <returns>true = 本次新授予；false = 参数为空或已掌握</returns>
        public bool GainTechnique(Technique technique)
        {
            if (technique == null) return false;

            if (Techniques == null)
                Techniques = new SangoObjectList<Technique>();

            // 已掌握就不再重复注册被动效果，否则 actionList 会堆积重复项
            if (Techniques.Contains(technique.Id)) return false;

            Techniques.Add(technique);
            // 存档字段同步（OnScenarioSave 也会同步，但这里立刻刷一遍便于中途存档）
            Techniques_list = Techniques.ToArray();

            if (actionList == null) actionList = new List<ActionBase>();
            technique.InitActions(actionList, this);

            Scenario scenario = Scenario.Cur;
            if (scenario != null)
            {
                InitTechniquesTree(scenario);
                UpdateCanBuildBuildingTypes();
            }

            return true;
        }

        /// <summary>
        /// 让本势力灭亡（供剧本事件调用）。
        ///
        /// 【安全前置】要求本势力**已经没有任何城池**，否则直接拒绝。
        /// 理由：城池移交（ChangeCorps / UpdateCorps）牵动城内武将、建筑、俘虏三份名单，
        /// 必须先由事件逐城移交完毕，再走灭亡清理；把两件事揉在一起，
        /// 一旦中途失败就会留下"城已改属、人还在旧势力"的半死状态。
        /// 事件里的正确写法是：CityChangeBelong × N → ForceExtinguish。
        ///
        /// 【与城池陷落流程的关系】引擎在"最后一城陷落"时有一段内联的灭亡清理
        /// （City.cs 的 destroyedForce 段）。本方法是同一套语义的事件侧入口：
        /// 军团失效 → 势力失效 → 俘虏解绑 → 武将下野 → 从场景移除 → 抛 OnForceFall。
        /// 两处若改动必须同步，否则会出现"事件灭亡的势力残留军团""陷落的势力没清俘虏"。
        /// </summary>
        /// <returns>true = 已执行灭亡；false = 前置不满足</returns>
        public bool Extinguish()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return false;

            if (IsPlayer)
            {
                Sango.Log.Warning($"事件：拒绝灭亡玩家势力 {Name}（玩家势力灭亡请走游戏结束流程，不要用事件效果）");
                return false;
            }

            // 前置：必须已经无城
            bool hasCity = false;
            scenario.citySet.ForEach(x =>
            {
                if (x != null && x.BelongForce == this) hasCity = true;
            });
            if (hasCity)
            {
                Sango.Log.Warning($"事件：{Name} 名下仍有城池，拒绝灭亡。请先用城池易主效果逐城移交");
                return false;
            }

            Force destroyedForce = this;

            // ① 军团失效并移出场景
            scenario.corpsSet.ForEach(x =>
            {
                if (x != null && x.BelongForce == destroyedForce)
                {
                    x.IsAlive = false;
                    scenario.Remove(x);
                }
            });

            // ② 本势力关押的俘虏解除归属，避免指向一个已不存在的势力
            if (BeCaptiveList != null)
            {
                BeCaptiveList.ForEach(x =>
                {
                    if (x != null && x.BelongForce == destroyedForce)
                    {
                        x.BelongForce = null;
                        x.BelongCorps = null;
                    }
                });
                BeCaptiveList.Clear();
            }

            // ③ 全势力武将下野（与城池陷落时的"最后一城"分支同一口径）
            scenario.personSet.ForEach(x =>
            {
                if (x == null || x.BelongForce != destroyedForce) return;

                x.ClearMission();
                if (x.IsValid && !x.IsPrisoner)
                    x.LeaveToWild();
                x.workingBuilding = null;
                x.mBelongTroop = null;
                x.BelongForce = null;
                x.BelongCorps = null;
            });

            // ④ 本势力失效并移出场景
            IsAlive = false;
            scenario.Remove(this);

            // ⑤ 抛出灭亡信号（city / troop 传 null 表示"非城池陷落导致"，
            //    订阅方 ScenarioEventManager.OnForceFall 只用第一个参数，安全）
            GameEvent.OnForceFall?.Invoke(this, null, null);

            Sango.Log.Info($"事件：{Name} 势力灭亡");
            return true;
        }

        void InitTechniquesTree(Scenario scenario)
        {
            techniqueMaxLevel = 0;

            // 在这里初始化科技树
            techniqueTree.Clear();
            InitTechniques.ForEach(x =>
            {
                ForceTechnique forceTechnique = new ForceTechnique()
                {
                    technique = x,
                    y = 0,
                };
                techniqueMaxLevel = Mathf.Max(techniqueMaxLevel, forceTechnique.FillChildren(scenario.CommonData.Techniques));
                techniqueTree.Add(forceTechnique);
            });
            techniqueTree.Sort((a, b) => a.technique.Id.CompareTo(b.technique.Id));
            techniqueMaxLevel++;

            for (int i = 0; i < techniqueTree.Count; i++)
            {
                int upY = 0, downY = 0;
                techniqueTree[i].UpdateY(ref upY, ref downY);

            }

            int startY = 0;
            for (int i = 0; i < techniqueTree.Count; i++)
            {
                ForceTechnique forceTechnique = techniqueTree[i];
                int minY = 0, maxY = 0;
                forceTechnique.GetMinMaxY(ref minY, ref maxY);
                int total = maxY - minY;
                forceTechnique.y = startY - minY;
                startY += total + 1;
            }
            techniqueMaxRow = startY + 1;
        }

        /// <summary>
        /// 准备科技列表
        /// </summary>
        /// <param name="scenario">当前场景</param>
        void prepareTechniqueList(Scenario scenario)
        {
            // 初始化可研究的科技
            canResearchTechniqueList.Clear();
            scenario.CommonData.Techniques.ForEach(x =>
            {
                if (x.Id != ResearchTechnique)
                {
                    if (x.CanResearch(this))
                    {
                        canResearchTechniqueList.Add(x);
                    }
                }
            });
        }

        /// <summary>
        /// 判断是否与另一势力为同盟关系
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <returns>是否为同盟关系</returns>
        public bool IsAlliance(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.Contains(other) && alliance.allianceType == AllianceType.Alliance)
                    return true;
            }
            return false;
        }

        // HasActiveTradeAgreement 已删除：原版没有通商命令，AllianceType.Trade 随之移除。

        /// <summary>
        /// 判断是否与另一势力有任何协议
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <returns>是否有任何协议</returns>
        public bool HasActiveAgreement(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.IsAlive && alliance.leftCount > 0 && alliance.Contains(other))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查与另一势力的联盟关系
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <returns>联盟对象</returns>
        public Alliance CheckAlliance(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.Contains(other))
                    return alliance;
            }
            return null;
        }

        /// <summary>
        /// 检查与另一势力的特定类型联盟关系
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <param name="allianceType">联盟类型</param>
        /// <returns>联盟对象</returns>
        public Alliance CheckAlliance(Force other, AllianceType allianceType)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.IsAlive && alliance.leftCount > 0 && alliance.Contains(other) && alliance.allianceType == allianceType)
                    return alliance;
            }
            return null;
        }

        public bool IsTruce(Force other)
        {
            return CheckAlliance(other, AllianceType.Truce) != null;
        }

        //public Corps Add(Corps corps)
        //{
        //    allCorps.Add(Scenario.Cur.Add(corps));
        //    return corps;
        //}
        //public City Add(City city)
        //{
        //    allCities.Add(Scenario.Cur.Add(city));
        //    return city;
        //}
        //public Person Add(Person person)
        //{
        //    allPersons.Add(Scenario.Cur.Add(person));
        //    return person;
        //}
        //public Troop Add(Troop troops)
        //{
        //    allTroops.Add(Scenario.Cur.Add(troops));
        //    return troops;
        //}
        //public Building Add(Building building)
        //{
        //    allBuildings.Add(Scenario.Cur.Add(building));
        //    return building;
        //}
        //public Corps Remove(Corps corps)
        //{
        //    allCorps.Remove(Scenario.Cur.Remove(corps));
        //    return corps;
        //}
        //public City Remove(City city)
        //{
        //    allCities.Remove(Scenario.Cur.Remove(city));
        //    return city;
        //}
        //public Person Remove(Person person)
        //{
        //    allPersons.Remove(Scenario.Cur.Remove(person));
        //    return person;
        //}
        //public Troop Remove(Troop troops)
        //{
        //    allTroops.Remove(Scenario.Cur.Remove(troops));
        //    return troops;
        //}
        //public Building Remove(Building building)
        //{
        //    allBuildings.Remove(Scenario.Cur.Remove(building));
        //    return building;
        //}

        /// <summary>
        /// 运行势力逻辑
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool Run(Scenario scenario)
        {
            if (ActionOver)
                return true;

            if (!DoBuildingBehaviour(scenario))
                return false;

            // 非玩家才执行AI
            if (!IsPlayer && !DoAI(scenario))
                return false;

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps != null && corps.IsAlive && corps.BelongForce == this && !corps.ActionOver)
                {
                    CurRunCorps = corps;
                    if (!corps.Run(scenario))
                        return false;
                }
            }

            ActionOver = true;
            return true;
        }

        /// <summary>
        /// 执行AI逻辑
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                GameEvent.OnForceAIStart?.Invoke(this, scenario);
                AIPrepared = true;
            }

            // 用游标顺序推进命令队列，避免 RemoveAt(0) 的 O(n) 搬移（整队跑完由 O(n²) 降为 O(n)）。
            while (aiCommandIndex < AICommandList.Count)
            {
                System.Func<Force, Scenario, bool> CurrentCommand = AICommandList[aiCommandIndex];
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                aiCommandIndex++;
            }

            // 整队跑完：清空队列并复位游标，供下一回合 AIPrepare 重新装填
            AICommandList.Clear();
            aiCommandIndex = 0;

            GameEvent.OnForceAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            return true;
        }


        /// <summary>
        /// 执行建筑行为
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public bool DoBuildingBehaviour(Scenario scenario)
        {
            if (buildingBaseList.Count <= 0)
                return true;

            while (buildingBaseList.Count > 0)
            {
                BuildingBase currentBuilding = buildingBaseList.Peek();
                if (!currentBuilding.DoBuildingBehaviour(scenario))
                    return false;

                buildingBaseList.Dequeue();
            }

            return true;
        }

        /// <summary>
        /// AI准备
        /// </summary>
        private void AIPrepare(Scenario scenario)
        {
            // 外交 AI：已按原版 7 命令重写（同盟 / 送礼 / 停战三项落地，见 ForceAI.AIDiplomacy）。
            // 排在计略 AI 之前：先决定"要不要拉盟友/求和"，再决定是否用流言、二虎竞食。
            AICommandList.Add(ForceAI.AIDiplomacy);
            // 城市计略AI: 排在被屏蔽的外交界之后, 只处理"流言记仇报复""二虎竞食后的破盟"这类与计略直接相关的决策,
            // 不接管结盟/停战/通商, 因此不会把上面屏蔽掉的整套外交AI顺带放出来
            AICommandList.Add(CityStrategyAI.AICityStrategy);
            AICommandList.Add(ForceAI.AICaptives);
            AICommandList.Add(ForceAI.AITechniques);
            AICommandList.Add(ForceAI.AISetOfficial);
            // 【资源调度】城池之间的物资运输（圈层水位：前线防御/进攻 → 次前线支援 → 后方储备）。
            // 刻意排在人才调度**之前**：先算出"哪座城有货要走、缺运输主将"，
            // 人才调度同一回合就能把运输队主将派过去（人员需求衔接，见 ResourceDispatchState）。
            AICommandList.Add(ResourceDispatcher.Run);

            // 【人才部署 · Phase A 影子模式】只计算并输出部署计划，不执行任何调动。
            // 放在旧调人逻辑之前：报告反映的是本回合开始时"AI 看到的人力分布"。
            AICommandList.Add(DeploymentShadow.Run);

            // 【Phase C】旧调人逻辑（ForceAI.AITransfromPerson 的 ring 填法）已删除，
            // 人才部署统一由新系统负责：DeploymentSolver（求解） + DeploymentExecutor（执行）。

            GameEvent.OnForceAIPrepare?.Invoke(this, scenario);
        }

        /// <summary>
        /// 势力回合开始时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnForceTurnStart(Scenario scenario)
        {
            // 【外交计时器】每回合递减（对齐 Open311 的 s11_game_week.cpp:287-319）。
            //
            // 这三个字段都是"剩余回合数"，**只写不减会永久生效**：
            //   · diplomacyBanTimer 不归零 → AIDiplomacy 第一行就 return，该势力永远不再外交；
            //   · requestTimer 不归零 → 永远无法再次请求援军。
            // 早期实现只写了递增 / 赋值，没有递减点，等于给自己埋了个静默死锁。
            if (diplomacyBanTimer > 0)
                diplomacyBanTimer--;

            if (requestTimer > 0)
            {
                requestTimer--;
                if (requestTimer <= 0)
                {
                    // 请求到期：清掉对象（原版同时会撤回为此派出的援军部队，
                    // 本项目尚未真正派兵，因此这里只清状态）
                    requestForceId = -1;
                    Sango.Log.Info($"*[外交]{Name} 的援军请求已到期");
                }
            }

            // 控制权移交在这里生效（本方法下面就是 AI 状态的整体复位，是唯一的干净边界）。
            // 放在最前面：后面的建筑 / 人物 / 军团回合结算与 GameEvent.OnForceTurnStart 都要看到新归属。
            // pending 为 -1（绝大多数回合）时这里什么都不做，不影响任何原有行为。
            if (pendingPlayerControl >= 0)
            {
                bool playerControlled = pendingPlayerControl == 1;
                pendingPlayerControl = -1;
                if (IsPlayer != playerControlled)
                {
                    IsPlayer = playerControlled;
                    Sango.Log.Info($"控制权按时机生效:{Name} → {(playerControlled ? "玩家" : "AI")}，第 {scenario.Info.turnCount} 回合");
                }
            }

            buildingBaseList.Clear();
            // 复位 AI 命令队列：上一回合若被中断（未跑完整队）会残留条目，这里统一清空并复位游标，
            // 保证本回合 AIPrepare 从空队列重新装填。
            AICommandList.Clear();
            aiCommandIndex = 0;
            AIFinished = false;
            AIPrepared = false;
            FightPower = 0;
            PersonCount = 0;

            // 清理已阵亡的威胁部队记录
            CleanupThreatTroops();
            Sango.Log.Info($"==={Name} 回合===");


            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnStart(scenario);
                    buildingBaseList.Enqueue(c);
                }
            }

            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    PersonCount++;
                    c.OnForceTurnStart(scenario);
                }
            }

            // 检查军师情况，必要时清除失效军师或重新设置更合适的军师
            // 必须放在人物遍历之后，PersonCount 已更新，且军师失效状态（阵亡/被俘/叛离）已在本回合结算完毕
            CheckCounsellor(scenario);

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnStart(scenario);
                }
            }

            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnStart(scenario);
                }
            }

            UpdateTurnInfo(scenario);

            // 检查敌方新建部队是否有占领我方城池的任务
            if (IsPlayer)
            {
                List<City> checkedCity = null;
                foreach (Troop troop in scenario.troopsSet)
                {
                    if (troop != null && troop.IsAlive && troop.BelongForce != this && troop.IsNewTroop && troop.missionType == (int)MissionType.TroopOccupyCity)
                    {
                        // 检查任务目标是否为我方城池
                        var targetCity = scenario.GetObject<City>(troop.missionTarget);
                        if (targetCity != null && targetCity.BelongForce == this)
                        {
                            // 根据军师智力计算发现概率
                            int baseProbability = scenario.Variables.discoverEnemyTroopBaseProbability;
                            int intelligenceFactor = 0;
                            if (mCounsellor != null)
                            {
                                intelligenceFactor = mCounsellor.Intelligence * scenario.Variables.discoverEnemyTroopIntelligenceFactor;
                            }
                            int totalProbability = baseProbability + intelligenceFactor;

                            // 概率命中后生成相机移动事件
                            if (GameRandom.Chance(totalProbability, 10000))
                            {
                                // 获取部队所属城市的位置
                                var troopCity = troop.BelongCity;
                                if (troopCity != null)
                                {
                                    if (checkedCity == null)
                                        checkedCity = new List<City>();
                                    else
                                    {
                                        if (checkedCity.Contains(troopCity))
                                            continue;
                                    }

                                    checkedCity.Add(troopCity);

                                    // 创建相机移动事件
                                    CameraMoveEvent cameraMoveEvent = RenderEvent.Instance.Create<CameraMoveEvent>();
                                    cameraMoveEvent.Init(troop.cell.Position, 0.5f,
                                        GameDialog.DialogStyle.ClickPersonSay, $"{ColorName}大人，\n我军细作传来消息,有敌军正在往我方{targetCity.ColorName}靠近!!。", mCounsellor, null, null).donotReturn = true;
                                    RenderEvent.Instance.Add(cameraMoveEvent);

                                    // 触发发现敌方部队事件
                                    GameEvent.OnDiscoverEnemyTroop?.Invoke(this, targetCity, troop, mCounsellor);
                                }
                            }
                        }
                    }
                }

                if (checkedCity != null)
                {
                    CameraMoveEvent cameraMoveEvent = RenderEvent.Instance.Create<CameraMoveEvent>();
                    cameraMoveEvent.Init(MapRender.Instance.GetCameraPos(), 0.5f);
                    RenderEvent.Instance.Add(cameraMoveEvent);
                }

            }

            // 【Phase C】PrepareCityPersonHole 已删除（旧的"缺几人"标量模型），
            // 岗位编制改由 DeploymentSolver 按需计算（失效驱动增量）。

            // 【人才部署】推进本势力的部署回合号。
            // 必须在这里（每回合恰一次）推进，而不是在 Solve 里自增 ——
            // 玩家势力会按**军团**分别调度（方案 B），若在 Solve 里自增，一个势力有几个军团就会加几次，
            // 防抖(debounceTurns)与前期加成(earlyGameTurns)的回合标度会全部失真。
            DeploymentState.BeginForceTurn(Id);

            return base.OnForceTurnStart(scenario);
        }

        public void UpdateCityCount(Scenario scenario)
        {
            CityCount = 0;
            CityBaseCount = 0;
            CityList.Clear();
            scenario.citySet.ForEach(x =>
            {
                if (x.BelongForce == this)
                {
                    CityBaseCount++;
                    if (x.IsCity())
                    {
                        CityCount++;
                        CityList.Add(x);
                    }
                }
            });

            // 记录历史峰值：产出累计上限按峰值城数计算（掉城不回收额度）
            if (CityCount > peakCityCount)
                peakCityCount = CityCount;
        }

        // ==================== 国库（宝物 / 装备） ====================
        //
        // 只有珍品（kind = Treasure / Equipment）进出本表；兵装 / 器械 / 船一律走 City.itemStore。
        // 授予 / 没收 / 搜索产出的唯一收口点就是下面这几个方法，不要在别处直接动 Treasury.Items。

        /// <summary>
        /// 国库容量上限（宝物与装备合计件数）：基础容量 + 城数 × 每城增量。
        /// 参数取自剧本设置（<see cref="ScenarioVariables.treasuryLimitBase"/> /
        /// <see cref="ScenarioVariables.treasuryLimitPerCity"/>）。
        ///
        /// **返回值 0 表示不设上限**：两个配置项同时配置为 0 时走这条分支
        /// （策划案 4.2「国库暂不设上限」的默认口径）。
        /// </summary>
        public int TreasuryLimit
        {
            get
            {
                Scenario scenario = Scenario.Cur;
                int baseLimit = scenario != null ? scenario.Variables.treasuryLimitBase : 0;
                int perCity = scenario != null ? scenario.Variables.treasuryLimitPerCity : 0;

                // 两项都为 0：显式关闭容量限制（不是"容量为 0"，见上方注释）
                if (baseLimit <= 0 && perCity <= 0)
                    return 0;

                int limit = baseLimit + CityList.Count * perCity;
                return limit > 0 ? limit : 0;
            }
        }

        /// <summary>
        /// 国库是否还能再放入指定数量的珍品
        /// </summary>
        /// <param name="number">待放入数量</param>
        /// <returns>能放下返回 true（不设上限时恒为 true）</returns>
        public bool CanAddTreasury(int number = 1)
        {
            if (Treasury == null)
                return false;

            int limit = TreasuryLimit;
            // 0 = 不设上限（策划案 4.2），此时容量不构成拦截条件
            if (limit <= 0)
                return true;

            return Treasury.TotalNumber + number <= limit;
        }

        /// <summary>
        /// 向国库放入珍品（宝物 / 装备）
        /// </summary>
        /// <param name="item">道具类型，须为珍品</param>
        /// <param name="number">数量，默认 1</param>
        /// <returns>放入后的库存数量；道具非法或国库已满时返回 -1</returns>
        public int AddTreasury(ItemType item, int number = 1)
        {
            if (item == null || !item.IsRareItem() || number <= 0)
            {
                Sango.Log.Warning($"[国库]{Name}放入道具失败：道具非法或数量为0。");
                return -1;
            }

            if (!CanAddTreasury(number))
            {
                Sango.Log.Warning($"[国库]{Name}的国库已满（{Treasury.TotalNumber}/{TreasuryLimit}），无法放入{item.Name}。");
                return -1;
            }

            return Treasury.Add(item, number);
        }

        /// <summary>
        /// 从国库取出珍品
        /// </summary>
        /// <param name="item">道具类型</param>
        /// <param name="number">数量，默认 1</param>
        /// <returns>取出后的剩余数量</returns>
        public int RemoveTreasury(ItemType item, int number = 1)
        {
            if (item == null || Treasury == null)
                return 0;

            return Treasury.Remove(item, number);
        }

        /// <summary>
        /// 国库中某件珍品的数量
        /// </summary>
        /// <param name="item">道具类型</param>
        /// <returns>数量，无该道具返回 0</returns>
        public int GetTreasuryCount(ItemType item)
        {
            if (item == null || Treasury == null)
                return 0;

            return Treasury.GetNumber(item);
        }

        /// <summary>
        /// 国库中是否有该珍品
        /// </summary>
        /// <param name="item">道具类型</param>
        /// <returns>持有返回 true</returns>
        public bool HasTreasury(ItemType item)
        {
            return GetTreasuryCount(item) > 0;
        }

        /// <summary>
        /// 收集国库中的珍品列表（按 Id 升序，保证 UI 显示顺序稳定）
        /// </summary>
        /// <param name="result">结果列表（会先清空）</param>
        /// <param name="rareOnly">true = 只收宝物 / 装备（国库正常只会存这两类）</param>
        public void GetTreasuryItems(List<ItemType> result, bool rareOnly = true)
        {
            if (result == null)
                return;

            result.Clear();

            Scenario scenario = Scenario.Cur;
            if (Treasury == null || scenario == null || scenario.CommonData == null)
                return;

            foreach (KeyValuePair<int, int> kv in Treasury.Items)
            {
                if (kv.Value <= 0)
                    continue;

                ItemType item = scenario.CommonData.ItemTypes.Get(kv.Key);
                if (item == null)
                    continue;

                if (rareOnly && !item.IsRareItem())
                    continue;

                result.Add(item);
            }

            result.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        void UpdateTurnInfo(Scenario scenario, bool doTurnStart = true)
        {
            CityCount = 0;
            CityBaseCount = 0;

            prepareTechniqueList(scenario);
            UpdateValidCreatedItemTypes();
            UpdateCanBuildBuildingTypes();

            // 圈层计算的 BFS 队列（从所有"边境城"向外扩散，见下方）
            Queue<City> ringQueue = new Queue<City>();
            NeighborForceList.Clear();
            NeighborCityList.Clear();
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    if (doTurnStart)
                        c.OnForceTurnStart(scenario);
                    FightPower += c.FightPower;
                    buildingBaseList.Enqueue(c);
                    CityBaseCount++;

                    if (c.IsCity())
                    {
                        CityCount++;
                        CityList.Add(c);

                        // 计算相邻势力 / 邻城（外交等模块读这两个列表，保持"只对都市收集"的原语义）
                        foreach (City neighbor in c.NeighborList)
                        {
                            if (!neighbor.IsSameForce(c))
                            {
                                if (neighbor.BelongForce != null)
                                {
                                    if (!NeighborForceList.Contains(neighbor.BelongForce))
                                        NeighborForceList.Add(neighbor.BelongForce);
                                }

                                if (!NeighborCityList.Contains(neighbor))
                                    NeighborCityList.Add(neighbor);
                            }
                        }
                    }

                    // ---------- 圈层初始化 ----------
                    // 【明确范围】只有都市参与圈层计算；港关/关卡的 borderLine 由
                    // CityEstablishment.ResolveRing 按其归属都市推导（这里不给它们写值）。
                    if (c.IsCity())
                    {
                        c.borderLine = -1;
                        for (int n = 0; n < c.NeighborList.Count; n++)
                        {
                            City neighbor = c.NeighborList[n];
                            if (neighbor != null && !neighbor.IsSameForce(c))
                            {
                                c.borderLine = 0;              // 与不同势力接壤 = 边境
                                ringQueue.Enqueue(c);
                                break;
                            }
                        }
                    }
                }
            }

            // ---------- 圈层扩散（多源 BFS） ----------
            // 规则"离边境几跳" = 到最近边境城的跳数，因此等价于从所有边境城做多源 BFS：
            //   · 一趟 O(n)，结果与城市在 citySet 里的**顺序无关**
            //     （原来反复扫全表直到收敛：最坏要扫"圈层深度"遍，且一轮无进展时既不推进也不停）；
            //   · 不会给孤立城写假圈层（旧实现 `minBorder` 初值 99 也会落进 `>= 0` 分支，写出 100）。
            // 永远到不了边境的城保持 -1，由 CityEstablishment.ResolveRing /
            // DeploymentSolver.RingOf 按"最深层"兜底。
            while (ringQueue.Count > 0)
            {
                City cur = ringQueue.Dequeue();
                int nextRing = cur.borderLine + 1;

                for (int n = 0; n < cur.NeighborList.Count; n++)
                {
                    City neighbor = cur.NeighborList[n];
                    if (neighbor == null || !neighbor.IsAlive) continue;
                    if (neighbor.BelongForce != this) continue;    // 只扩散自己势力
                    if (!neighbor.IsCity()) continue;              // 港关不参与圈层（见上方初始化说明）
                    if (neighbor.borderLine >= 0) continue;        // 已定

                    neighbor.borderLine = nextRing;
                    ringQueue.Enqueue(neighbor);
                }
            }


        }


        /// <summary>
        /// 势力回合结束时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnForceTurnEnd(Scenario scenario)
        {
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            return base.OnForceTurnEnd(scenario);
        }

        /// <summary>
        /// 月份开始时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnMonthStart(Scenario scenario)
        {
            // 原版：俘虏每月都可能掉忠（在仕武将只有换季才结算，见 OnSeasonStart）
            ForceCaptiveLoyaltyChange();
            return base.OnMonthStart(scenario);
        }

        /// <summary>
        /// 季节开始时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnSeasonStart(Scenario scenario)
        {
            if (RollLoyaltyChangeSettlement())
            {
                ForcePersonLoyaltyChange();
            }

            return base.OnSeasonStart(scenario);
        }


        static int[] loyaltyWeight = new int[5] { 2, 3, 2, 1, 1 };

        /// <summary>
        /// 势力武将换季掉忠计算。
        ///
        /// 顺序：
        ///   1) 原版"必不掉忠"资格判定（君主 / 亲爱 / 夫妻 / 义兄弟 / 亲子 / 相性与义理野心 / 城内有仁政），
        ///      见 PersonLoyaltyRules.CanLoseLoyalty —— 不通过就直接跳过；
        ///   2) 通过者每人各掷一次掉忠值（原来只掷一次、全势力共用）；
        ///   3) 扣忠之前再发 GameEvent.OnForcePersonLoyaltyChange 征询监听者：
        ///      把 shouldLoseLoyalty 置 false 即可否决该武将本次扣忠
        ///     （例如"阻止本城武将掉忠"的 Action，见 CityPreventPersonLoyaltyLoss）。
        /// </summary>
        public void ForcePersonLoyaltyChange()
        {
            // TODO: 武将换季掉忠（掉忠量本身仍是占位公式）
            ForEachPerson(person =>
            {
                // 原版免掉忠条件：命中则本季不进入掉忠结算
                if (!PersonLoyaltyRules.CanLoseLoyalty(this, person)) return;

                // 每人各掷一次：本次该掉多少
                int v = GameRandom.RandomWeightIndex(loyaltyWeight, 9);

                // 先问监听者能否否决本次扣忠（默认 true = 照扣）
                Tools.OverrideData<bool> shouldLoseLoyalty = Tools.OverrideData<bool>.Create(true);
                GameEvent.OnForcePersonLoyaltyChange?.Invoke(this, person, shouldLoseLoyalty);
                if (!shouldLoseLoyalty.Value) return;

                // 走 AddLoyalty 而不是裸 `-=`：0 忠诚的人被连续扣忠会变成负数，
                // 而显示层只兜上限不兜下限，脏数据会静默留存（见 Person.AddLoyalty 注释）。
                int newLoyalty = person.AddLoyalty(-v);
                Sango.Log.Info($"势力：{Name}, 武将：{person.Name}, 忠诚度下降: {v}, 现有忠诚度:{newLoyalty}");
            });
        }

        /// <summary>
        /// 本势力是否要结算一次掉忠。
        /// 原版：掌握人心科技有 2/3 概率让武将不进入掉忠计算 —— 数据里给该科技挂一个
        /// value=33 的 ForcePersonLoyaltyChange（它把基础值 100 压成 33），事件一来即生效；
        /// 没有该科技时基础值仍是 100，等于必定结算。
        /// </summary>
        private bool RollLoyaltyChangeSettlement()
        {
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(100);
            GameEvent.OnForcePersonLoyaltyChangeProbability?.Invoke(this, overrideData);
            return GameRandom.Chance(overrideData.ValueAndRecycle);
        }

        /// <summary>
        /// 本势力被俘武将的月度掉忠（原版：俘虏每月都可能掉忠）。
        ///
        /// 与换季结算（ForcePersonLoyaltyChange）的区别：
        ///   · 频率：每月（换季是每 3 个月）；
        ///   · 免掉忠条件只有三条（亲爱君主 / 夫妻或义兄弟 / 子女或父母），不受「仁政」与
        ///     "相性差 ≤ 25"保护，见 PersonLoyaltyRules.CanCaptiveLoseLoyalty；
        ///   · 与原君主相性差 ≥ 25 会额外多掉，义理低掉得更快、义理高掉得慢；
        ///   · 掌握人心科技：每人各掷一次，本人有 2/3 概率跳过。
        ///
        /// 比较对象是被俘势力的君主（原君主）—— 俘虏的本势力归属与 Force.BeCaptiveList
        /// 都保留在原势力上，因此这里遍历 BeCaptiveList。
        /// </summary>
        public void ForceCaptiveLoyaltyChange()
        {
            if (BeCaptiveList == null || BeCaptiveList.Count == 0) return;

            Person governor = mGovernor;
            for (int i = BeCaptiveList.Count - 1; i >= 0; --i)
            {
                Person person = BeCaptiveList[i];
                if (person == null || !person.IsAlive || !person.IsPrisoner) continue;
                if (!PersonLoyaltyRules.CanCaptiveLoseLoyalty(person, governor)) continue;
                if (!RollLoyaltyChangeSettlement()) continue;      // 掌握人心：每人各掷一次

                int v = PersonLoyaltyRules.CalcCaptiveLoyaltyLoss(person, governor);
                person.loyalty = System.Math.Max(0, person.loyalty - v);
                Sango.Log.Info($"势力：{Name}, 俘虏：{person.Name}, 忠诚度下降: {v}, 现有忠诚度:{person.loyalty}");
            }
        }

        /// <summary>
        /// 对所有城寨执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachCityBase(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有城市执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachCity(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this && c.IsCity())
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有关卡执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachGate(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this && c.IsGate())
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有港口执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachPort(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this && c.IsPort())
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有武将执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachPerson(System.Action<Person> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有军团执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachCorps(System.Action<Corps> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有建筑执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachBuilding(System.Action<Building> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有部队执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachTroop(System.Action<Troop> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }


        /// <summary>
        /// 获得技巧点数
        /// </summary>
        /// <param name="value">获得的技巧点数</param>
        public void GainTechniquePoint(int value)
        {
            if (value == 0) return;

            TechniquePoint += value;

            if (IsPlayer && value > 0)
            {
                GameMedia.Instance.PlaySfx(61);
                GameMedia.Instance.PlayDelayedSfx(60, 1);
            }

            GameEvent.OnForceGainTechniquePoint?.Invoke(this, value);
        }

        /// <summary>
        /// 获得霸业点数
        /// </summary>
        /// <param name="value">获得的霸业点数</param>
        public void GainHegemonyPoint(int value)
        {
            HegemonyPoint += value;
            GameEvent.OnForceGainHegemonyPoint?.Invoke(this, value);
        }

        /// <summary>
        /// 检查是否拥有指定科技
        /// </summary>
        /// <param name="techId">科技ID</param>
        /// <returns>是否拥有该科技</returns>
        public bool HasTechnique(int techId)
        {
            return Techniques.Contains(techId);
        }

        /// <summary>
        /// 添加科技
        /// </summary>
        /// <param name="techId">科技ID</param>
        /// <returns>添加的科技对象</returns>
        public Technique AddTechnique(int techId)
        {
            if (ResearchTechnique == techId)
            {
                ResearchTechnique = 0;
                ResearchLeftCounter = 0;
            }

            Technique technique = Scenario.Cur.GetObject<Technique>(techId);
            if (technique == null) return null;
            Techniques.Add(technique);
            technique.InitActions(actionList, this);
            technique.DoActiveAction(this);
            UpdateCanBuildBuildingTypes();
            UpdateValidCreatedItemTypes();

            ForEachCityBase(x =>
            {
                x.UpdateCalculate();
            });

            ForEachTroop(x =>
            {
                x.CalculateAttribute(Scenario.Cur);
            });

            return technique;
        }

        /// <summary>
        /// 创建军团
        /// </summary>
        /// <param name="number"></param>
        /// <param name="commander"></param>
        /// <param name="cities"></param>
        public void CreateCorps(int number, Person commander, List<City> cities)
        {
            Corps corps = new Corps();
            corps.number = number;
            corps.BelongForce = this;
            corps.mComander = commander;
            Scenario.Cur.Add(corps);
            corps.Init(Scenario.Cur);
            ChangeCitiesCorps(corps, cities, this);
            GameEvent.OnCorpsCreate?.Invoke(corps, Scenario.Cur);
        }

        public void CreateCorps(Corps corps)
        {
            corps.BelongForce = this;
            Scenario.Cur.Add(corps);
            corps.Init(Scenario.Cur);

            // 【原版对齐】支军团继承主军团的方针与委任目标。
            // Open311 create_district 在 number != 1 时会复制主军团的
            // policy / policy_target 与 mid_objective（本项目对应 appoint / appoint_target）。
            // 不继承的话新军团是"无方针"，AI 第一回合会按默认发展处理，
            // 玩家刚设好的"攻略某势力"意图当场丢失。
            Corps mainCorps = CapitalCorps;
            if (mainCorps != null && mainCorps != corps)
            {
                corps.policy = mainCorps.policy;
                corps.policy_target = mainCorps.policy_target;
                corps.appoint = mainCorps.appoint;
                corps.appoint_target = mainCorps.appoint_target;
            }

            // 【原版对齐】都督所在城必须属于本军团。
            // Open311 create_district 里有一段：
            //     Building* city_building = city->get_building();
            //     if (not std::ranges::contains(building_list, city_building))
            //         building_list.push_back(city_building);
            // 即"玩家没勾选军团长所在城时自动补上"。
            // 缺了它会出现"军团长不在自己军团里"的分裂：都督的 BelongCorps 还是旧军团，
            // 而这个新军团的 mComander 指向他，两边互相矛盾。
            if (corps.mComander != null && corps.mComander.BelongCity != null)
            {
                if (corps.inti_cities == null)
                    corps.inti_cities = new List<City>();
                if (!corps.inti_cities.Contains(corps.mComander.BelongCity))
                    corps.inti_cities.Add(corps.mComander.BelongCity);
            }

            corps.mComander.state = (int)PersonStateType.Commander;
            ChangeCitiesCorps(corps, corps.inti_cities, this);

            corps.inti_cities = null;
            corps.mComander.BelongCity.UpdateNewLeader();
            corps.PrepareCityInfo();
            GameEvent.OnCorpsCreate?.Invoke(corps, Scenario.Cur);
        }

        /// <summary>
        /// 把一批都市（连同其下属港关）整体改旗到指定军团。
        ///
        /// 【为什么要统一收敛到这里】一个城池的"军团归属"散落在六处名单上：
        /// 城市自身、在籍武将、建筑、俘虏、下属港关、港关内武将。
        /// 只要漏掉任意一处，就会留下"城属新军团、人属旧军团"的分裂，
        /// 之后任何一次 AddPerson（入城 / 读档重登记 / 释放俘虏）都会打出"军团不一致"的错误日志。
        /// 因此这里不再手工逐个字段赋值，统一走 <see cref="City.ChangeCorps"/> + <see cref="City.UpdateCorps"/>，
        /// 与"重编军团"之外的其它改军团入口保持同一口径。
        /// </summary>
        /// <param name="corps">目标军团</param>
        /// <param name="cities">要划入该军团的都市列表，可为空</param>
        static void ChangeCitiesCorps(Corps corps, List<City> cities, Force force)
        {
            if (corps == null || cities == null)
                return;

            for (int i = 0; i < cities.Count; i++)
            {
                City city = cities[i];
                if (city == null)
                    continue;

                city.ChangeCorps(corps);
                city.UpdateCorps();
            }

            // 【原版对齐】把"选中都市的附庸港关"一并带走，但排除君主驻守的据点。
            //
            // 证据 1（新建）Open311 exec_district_new_command：
            //     for (i = 0; i < KyotenId_ExMax; i++) {
            //         Building* b = get_building(get_kyoten_building_id(i));
            //         if (!is_alive(b) || b->get_force_id() != force_id
            //             || kunshu->get_service_id() == i            // ← 排除君主驻地
            //             || center->get_building_id() == i)          // ← 排除都督城
            //             continue;
            //         District* d = get_district(b->get_district_id());
            //         if (is_alive(d) && d->get_number() != 1) continue;  // ← 限第一军团
            //         if (city_list[b->get_area_city_id()])              // ← 港关返回所属都市 id
            //             building_list.push_back(b);
            //     }
            //
            // 证据 2（重编）exec_district_reform_command 用同样的
            //   city_district_id[building->get_area_city_id()] 决定每个据点去向，
            //   同样以 kunshu->get_service_id() == i 排除君主驻地。
            //
            // 所以"主公在港关"不是靠"完全不带港关"来解决，而是靠
            // "跳过君主驻守的那一个"—— 其余港关照常跟随所属都市改编。
            int governorServiceId = 0;
            Person governor = force != null ? force.mGovernor : null;
            if (governor != null && governor.BelongCity != null)
                governorServiceId = governor.BelongCity.Id;

            Scenario.Cur.citySet.ForEach(x =>
            {
                if (x == null || !x.IsAlive)
                    return;
                // 都市已在上面处理过，这里只看港关
                if (x.IsCity())
                    return;
                if (x.BelongForce != corps.BelongForce)
                    return;
                if (x.BelongCity == null || !cities.Contains(x.BelongCity))
                    return;

                // ① 君主驻守的据点不参与改编（原版 kunshu->get_service_id() == i）
                if (governorServiceId > 0 && x.Id == governorServiceId)
                {
                    Sango.Log.Info($"*[军团]{x.Name} 由君主驻守，不参与本次军团改编");
                    return;
                }

                // ② 新建时只带走当前属第一军团的港关
                //    （原版 district->get_number() != 1 则跳过；重编入口自行放开这个限制）
                if (x.BelongCorps != null && !x.BelongCorps.IsCaptainCorps)
                    return;

                x.ChangeCorps(corps);
                x.UpdateCorps();
            });
        }

        /// <summary>
        /// 选择继任君主（对应 Open311 的 System::select_successor 的 AI 分支）。
        ///
        /// 原版的完整规则是：亲缘关系优先 → 官职 → Id，玩家势力还会弹窗让玩家挑。
        /// 这里简化成"血缘 &gt; 官职等级 &gt; 统率 &gt; Id"，不弹窗（玩家势力也自动选）。
        ///
        /// 【为什么必须有这一步】没有它，君主一死 mGovernor 就悬空，
        /// 而 Force.IsAlive 要求君主存活 → 势力从 UI 与 AI 循环里凭空消失，
        /// 城、军团、武将却都还在，形成无法自愈的悬空势力。
        /// </summary>
        /// <param name="oldGovernor">刚死亡的君主</param>
        /// <returns>继任者；无人可继时返回 null（应由调用方走 Ruin）</returns>
        public Person SelectSuccessor(Person oldGovernor)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.personSet == null)
                return null;

            Person best = null;
            long bestScore = long.MinValue;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;
                if (person == oldGovernor)
                    continue;
                if (person.BelongForce != this)
                    continue;

                // 原版候选身份：{Kunshu, Totoku, Taishu, Ippan}
                PersonStateType st = (PersonStateType)person.state;
                if (st != PersonStateType.Governor
                    && st != PersonStateType.Commander
                    && st != PersonStateType.Leader
                    && st != PersonStateType.Normal)
                    continue;

                long score = 0;
                if (IsRelative(person, oldGovernor))
                    score += 1000000L;
                score += (long)(person.Official != null ? person.Official.level : 0) * 10000L;
                score += person.Command;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = person;
                }
            }

            return best;
        }

        /// <summary>
        /// 君主继承（对应 Open311 的 System::force_koukei）。
        ///
        /// 顺序照搬原版：
        ///   ① 旧君主降为一般/太守（未死时；已死则跳过）
        ///   ② 新君主升为君主
        ///   ③ 解任新君主的军师身份、清官职
        ///   ④ 都城迁到新君主所在城
        ///   ⑤ 旧第一军团并入新君主所在军团
        ///   ⑥ 新君主所在城重选太守
        /// </summary>
        /// <param name="successor">继任者</param>
        /// <param name="oldCapitalCorps">
        /// 旧君主生前所属军团。必须由调用方传入：君主死亡路径里
        /// <see cref="Person.Dead"/> 已把 <c>BelongCorps</c> 置空，
        /// 这里再读 <c>oldGovernor.BelongCorps</c> 只会拿到 null，第 ⑤ 步就静默失效。
        /// </param>
        /// <returns>是否继承成功</returns>
        public bool Succeed(Person successor, Corps oldCapitalCorps = null)
        {
            // 注意用字段 isAlive 而不是属性 IsAlive：
            // 此刻 mGovernor 还指向已死的前任，属性会返回 false，导致继承永远走不进来。
            if (!isAlive || successor == null || successor.IsDead)
                return false;
            if (successor.BelongForce != this)
                return false;

            Person oldGovernor = mGovernor;

            // ① 旧君主降级（已死的不动，Dead() 已把身份置为 Dead）
            if (oldGovernor != null && oldGovernor != successor && !oldGovernor.IsDead)
                PersonStateSystem.SetState(oldGovernor, PersonStateType.Normal, PersonChangeReason.ForceChanged);

            // ② 新君主上位
            Governor = successor.Id;
            mGovernor = successor;
            PersonStateSystem.SetState(successor, PersonStateType.Governor, PersonChangeReason.ForceChanged);

            // ③ 解任军师 + 清官职
            if (mCounsellor == successor)
            {
                Counsellor = 0;
                mCounsellor = null;
            }

            Scenario scenario = Scenario.Cur;
            if (scenario != null && scenario.CommonData != null && scenario.CommonData.Officials != null)
                successor.UpgradeOfficial(scenario.CommonData.Officials.Get(0));

            // ④ 都城自动跟随
            //    CapitalCity 是派生属性（=> mGovernor?.BelongCity），
            //    ② 已经把 mGovernor 换成 successor，这里不需要也无法赋值。

            // ⑤ 军团收编：**保留第 1 军团**，把继任者原所在军团并入它。
            //
            // Open311 force_koukei：
            //     District* old_district = get_district(successor->get_district_id());  // 继任者原军团
            //     District* new_district = get_district(self->get_district_id());       // 势力第 1 军团
            //     district_dissolve(old_district, new_district);   // 第一参数被解散
            //     district_set_totoku_id(new_district, successor_id);
            // 即"继任者军团 → 第 1 军团"，方向不能反：
            // 第 1 军团承载势力主城与君主直辖部队，跨级合并会让这些城临时失去主军团编号。
            //
            // 军团来源优先用调用方传入的 oldCapitalCorps：君主死亡路径里
            // Dead() 早已把 oldGovernor.BelongCorps 置空，回读只会拿到 null。
            Corps firstCorps = oldCapitalCorps;
            if (firstCorps == null || !firstCorps.IsAlive)
                firstCorps = FindCorpsByNumber(1);

            Corps successorCorps = successor.BelongCorps;
            if (firstCorps != null && successorCorps != null && firstCorps != successorCorps)
            {
                Sango.Log.Info($"*[继承]{successorCorps.Name} 并入 {firstCorps.Name}");
                MergeCorps(successorCorps, firstCorps);

                // 合并后重新拿一次继任者的军团（MergeCorps 已把他改挂到 firstCorps）
                successorCorps = successor.BelongCorps;
            }

            // 新君主坐镇第 1 军团。第 1 军团在 Sango 不设都督（IsCaptainCorps 直接跳过
            // UpdateCommander），所以这里只需保证 mComander 不指向别人。
            if (firstCorps != null && firstCorps.IsAlive)
            {
                firstCorps.mComander = null;
                firstCorps.Comander = 0;
            }

            // ⑥ 新君主所在城重选太守（原版 building_change_taishu(service, successor) 的等价物）
            successor.BelongCity?.NeedUpdateLeader();

            Sango.Log.Info($"*[继承]{Name} 主公 {oldGovernor?.Name ?? "无"} -> {successor.Name}");
            return true;
        }

        /// <summary>
        /// 两名武将是否有血缘关系（父子/母子）。
        /// </summary>
        /// <param name="a">甲</param>
        /// <param name="b">乙</param>
        /// <returns>是否有血缘</returns>
        private static bool IsRelative(Person a, Person b)
        {
            if (a == null || b == null)
                return false;
            if (a.Father == b || a.Mother == b)
                return true;
            if (b.Father == a || b.Mother == a)
                return true;
            return a.Brother == b || b.Brother == a;
        }

        /// <summary>
        /// 势力是否还有任何存活武将（对应 Open311 的 force_is_not_empty 的武将部分）。
        /// </summary>
        /// <returns>是否还有存活武将</returns>
        public bool HasAnyAlivePerson()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.personSet == null)
                return false;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;
                if (person.BelongForce == this)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 取本势力的第一座城（用于灭亡时的演出上下文）。
        /// </summary>
        /// <returns>第一座城；没有时返回 null</returns>
        public City FirstCity()
        {
            City first = null;
            ForEachCity(c =>
            {
                if (first == null && c != null && c.IsAlive)
                    first = c;
            });
            return first;
        }

        /// <summary>
        /// 投降移交：把本势力全部据点并入胜方的第一军团
        /// （对应 Open311 的 System::force_surrender_all_buildings）。
        ///
        /// 与 <see cref="Ruin"/> 的区别：
        ///   · Ruin            = 彻底灭亡，城池已被打光，武将全部下野；
        ///   · SurrenderAll    = 劝降合并，城池、武将、资源整体并入胜方。
        ///
        /// 原版投降分支的顺序是"先移交城池 → 再 person_change_force 把武将转给胜方
        /// （君主先降为都督）→ 最后才标记灭亡"。这里只做第一步，
        /// 因为武将转籍必须走 <see cref="PersonStateSystem.Recruit"/>（要维护
        /// 势力俘虏索引、官职、忠诚、军团名册），由调用方按需逐个处理，
        /// 硬塞进本方法会把两件事的失败半径绑在一起。
        ///
        /// 【为什么并入第一军团】原版 district_dissolve 的固定目标就是 1 号军团；
        /// 本项目第一军团 = 君主所在军团（CapitalCorps），语义等价。
        /// </summary>
        /// <param name="winner">受降方</param>
        /// <returns>移交的据点数；受降方没有第一军团时返回 -1</returns>
        public int SurrenderAllBuildings(Force winner)
        {
            if (winner == null || winner == this || !IsAlive)
                return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return 0;

            Corps winnerCorps = winner.CapitalCorps;
            if (winnerCorps == null)
            {
                Sango.Log.Error($"*[投降]{Name} 无法移交：{winner.Name} 没有第一军团");
                return -1;
            }

            int moved = 0;
            scenario.citySet.ForEach(x =>
            {
                if (x == null || !x.IsAlive || x.BelongForce != this)
                    return;
                CityStateSystem.SetCorps(x, winnerCorps);
                moved++;
            });

            Sango.Log.Info($"*[投降]{Name} 的 {moved} 座据点移交给 {winner.Name}（{winnerCorps.Name}）");
            return moved;
        }

        /// <summary>
        /// 势力灭亡（对应 Open311 的 System::force_ruin，非投降分支）。
        ///
        /// 【为什么独立成方法】原先这段逻辑内联在 <see cref="City.OnFall"/> 的
        /// "最后一城"分支里，与城池陷落、战斗结算、演出回调缠在一起：
        ///   · 无法复用于"事件灭国""编辑器删除势力"等场景；
        ///   · 任何一处漏掉（比如新增一种攻城来源）就会留下半个灭亡的势力。
        ///
        /// 【处理顺序照搬原版】
        ///   ① 标记灭亡（先置位，后续遍历用 is_alive 过滤就不会再碰到它）
        ///   ② 释放本势力关押的俘虏：只清归属、不销毁对象
        ///   ③ 全势力武将下野（俘虏保留身份，只清归属）
        ///   ④ 解散全部军团
        ///   ⑤ 从剧本移除势力
        ///   ⑥ 广播灭亡事件
        ///   ⑦ 判定天下一统
        ///
        /// 注意：城池清空、据点改旗这些**不在这里做**，由调用方（城陷流程）负责 ——
        /// 原版 force_ruin 里的 force_empty_all_kyotens 处理的是"投降时剩余的城"，
        /// 本项目走到这里时已经是最后一座城陷落，城由 OnFall 自己收尾。
        /// </summary>
        /// <param name="by">导致灭亡的势力，可为 null（被火焰/事件灭亡）</param>
        /// <param name="fallCity">陷落的最后一座城，用于事件上下文</param>
        /// <param name="byObject">导致灭亡的具体对象（部队/火/事件），可为 null</param>
        /// <returns>true = 达成天下一统，调用方应立即中止后续流程</returns>
        public bool Ruin(Force by, City fallCity, SangoObject byObject)
        {
            Scenario scenario = Scenario.Cur;
            // 用字段 isAlive 而不是属性 IsAlive：君主刚死亡、继承尚未执行时，
            // 属性会因 mGovernor 指向死者而返回 false —— 那恰恰是需要走 Ruin 的时刻。
            if (scenario == null || !isAlive)
                return false;

#if SANGO_DEBUG
            // 【灭亡总览】把"谁灭了谁、最后丢的是哪座城"写清楚，
            // 否则城陷日志之后只剩一句"XX 灭亡!!!"，回溯不出上下文
            Sango.Log.Info($"*[灭亡]{Name} 势力灭亡（最后据点 {fallCity?.Name ?? "无"}" +
                           $"，被 {(by?.Name ?? byObject?.Name ?? "无")} 攻灭）");
#endif

            // ① 标记灭亡
            IsAlive = false;

            // ② 本势力关押的俘虏：解除归属（原版只清 force/district，不销毁对象）
            if (BeCaptiveList != null)
            {
                BeCaptiveList.ForEach(x =>
                {
                    if (x != null && x.BelongForce == this)
                    {
                        x.BelongForce = null;
                        x.BelongCorps = null;
                    }
                });
                BeCaptiveList.Clear();
            }

            // ③ 全势力武将下野
            scenario.personSet.ForEach(x =>
            {
                if (x == null || x.BelongForce != this)
                    return;

                x.ClearMission();
                // 俘虏不重复走 LeaveToWild（他们本来就没有户口）
                if (x.IsValid && !x.IsPrisoner)
                    x.LeaveToWild();
                x.workingBuilding = null;
                x.mBelongTroop = null;
                x.BelongForce = null;
                x.BelongCorps = null;
            });

            // ④ 解散全部军团
            scenario.corpsSet.ForEach(x =>
            {
                if (x == null || x.BelongForce != this)
                    return;
                x.IsAlive = false;
                scenario.Remove(x);
            });

            // ⑤ 从剧本移除势力
            scenario.Remove(this);

            // ⑥ 灭亡事件。委托签名是 (Force, City, Troop)，
            //    而 byObject 可能不是部队（火焰 / 事件 / 归属巡检），必须安全转换。
            GameEvent.OnForceFall?.Invoke(this, fallCity, byObject as Troop);

            // ⑦ 天下一统判定：除灭亡方外，所有城都归同一个势力
            if (by == null)
                return false;

            bool allInOne = true;
            scenario.citySet.ForEach(x =>
            {
                if (x != null && x.IsCity() && x.BelongForce != null && x.BelongForce != by)
                    allInOne = false;
            });

            if (!allInOne)
                return false;

            Sango.Log.Info($"{scenario.GetDateStr()} --> {by.Name} 统一!!!!!!!!!!!!!!");
            scenario.OnGamePause();

            // 终局演出放在 pause 之后：先停主循环，避免 AI 在后台把回合跑完
            GameEvent.OnWorldUnified?.Invoke(by);
            return true;
        }

        public void ResetCorps(Corps corps, Corps copy)
        {
            DeleteCorps(corps);
            CreateCorps(copy);
        }

        /// <summary>
        /// 解散军团
        /// </summary>
        /// <param name="number"></param>
        /// <param name="commander"></param>
        /// <param name="cities"></param>
        /// <summary>
        /// 把 <paramref name="from"/> 军团的全部据点 / 武将 / 建筑划给 <paramref name="to"/>，
        /// 然后解散 <paramref name="from"/>。
        ///
        /// 对应 Open311 的 `district_dissolve(self, merge_dest)` 的合并分支：
        /// 第一参数是被解散方，第二参数是接收方。原版 `force_koukei` 的调用是
        /// `district_dissolve(继任者原军团, 第1军团)` —— **保留第 1 军团**，
        /// 把继任者所在的军团并进去。
        ///
        /// 与 <see cref="DeleteCorps(Corps)"/> 的区别：DeleteCorps 硬编码接收方为
        /// `CapitalCorps`（= 君主所在军团），君主继承时会指向"即将被解散"的那个军团，
        /// 形成自我合并；这里允许显式指定接收方。
        /// </summary>
        /// <param name="from">被解散的军团</param>
        /// <param name="to">接收方军团</param>
        public void MergeCorps(Corps from, Corps to)
        {
            if (from == null || to == null || from == to || !from.IsAlive)
                return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            // 旧军团长卸任（原版 district_dissolve 开头：能当太守则太守，否则一般）。
            // SetStateNormal 对君主有保护，不会把君主降级。
            if (from.mComander != null)
            {
                if (from.mComander.IsAlive && !from.mComander.IsDead)
                    from.mComander.SetStateNormal();
                from.mComander = null;
                from.Comander = 0;
            }

            scenario.citySet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == from)
                    CityStateSystem.SetCorps(x, to);
            });

            scenario.personSet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == from)
                    x.ChangeCorps(to);
            });

            scenario.buildingSet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == from)
                {
                    x.BelongCorps = to;
                    x.BelongForce = to.BelongForce;
                }
            });

            from.IsAlive = false;
            scenario.Remove(from);
        }

        /// <summary>
        /// 按编号找本势力的军团。
        /// </summary>
        /// <param name="number">军团编号</param>
        /// <returns>军团；不存在时返回 null</returns>
        public Corps FindCorpsByNumber(int number)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.corpsSet == null)
                return null;

            Corps result = null;
            scenario.corpsSet.ForEach(x =>
            {
                if (result == null && x != null && x.IsAlive && x.BelongForce == this && x.number == number)
                    result = x;
            });
            return result;
        }

        public void DeleteCorps(int number)
        {
            if (number == 1) return;
            Corps corps = Scenario.Cur.corpsSet.Find((x) =>
            {
                return x.BelongForce == this && x.number == number;
            });
            DeleteCorps(corps);
        }

        /// <summary>
        /// 解散军团
        /// </summary>
        /// <param name="number"></param>
        /// <param name="commander"></param>
        /// <param name="cities"></param>
        public void DeleteCorps(Corps corps)
        {
            if (corps.BelongForce == null)
            {
                Scenario.Cur.corpsSet.Remove(corps);
                return;
            }

            if (corps == CapitalCorps)
                return;

            Corps governorCorps = CapitalCorps;
            if (governorCorps == null)
            {
                Sango.Log.Error($"*军团解散失败：势力{Name}找不到可接收的第一军团");
                return;
            }

            Scenario scenario = Scenario.Cur;

            // 【归属治理】军团解散 = 把本军团名下的据点/武将/建筑整体划归第一军团。
            // 原先三处都是裸赋 BelongCorps，不同步 BelongForce，也不清城内的
            // 名册与太守状态；一旦目标军团势力与当前记录不一致，
            // 就会留下"城属A团、人属B团"的分裂（正是本项目最常报的归属错误）。
            scenario.citySet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == corps)
                {
                    CityStateSystem.SetCorps(x, governorCorps);
                }
            });

            scenario.personSet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == corps)
                {
                    // 【原版对齐】军团解散只做"成员改挂"，不清官职、不解军师、不降级。
                    // Open311 district_dissolve 对成员只调 person_set_district_id 一步；
                    // 清官职（person_set_rank_id(RankId_None)）属于 person_change_district
                    // 的换势力语义，用在这里会让整团武将无端丢掉官职。
                    x.ChangeCorps(governorCorps);
                }
            });

            scenario.buildingSet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == corps)
                {
                    x.BelongCorps = governorCorps;
                    x.BelongForce = governorCorps.BelongForce;
                }
            });

            if (corps.mComander != null && corps.mComander.state == (int)PersonStateType.Commander)
                PersonStateSystem.SetState(corps.mComander, PersonStateType.Normal, PersonChangeReason.DistrictChanged);

            Scenario.Cur.corpsSet.Remove(corps);
            GameEvent.OnCorpsDelete?.Invoke(corps, Scenario.Cur);
        }

        /// <summary>
        /// 检查军师情况，必要时重新设置更好的军师
        /// 规则：
        /// 1. 当前军师已失效（阵亡/被俘/叛离势力/成为君主）时，先将其清除；
        /// 2. AI 势力：在军师空缺或出现智力更高的在籍人选时，自动重新任命；
        /// 3. 玩家势力：仅在军师空缺时自动补任，不覆盖玩家自己的选择。
        /// </summary>
        /// <param name="scenario">当前场景</param>
        public void CheckCounsellor(Scenario scenario)
        {
            // 第一步：校验当前军师是否仍然有效（存活、属于本势力、未被俘、且不是君主本人）
            if (mCounsellor != null &&
                (!mCounsellor.IsAlive || mCounsellor.BelongForce != this || mCounsellor.IsPrisoner || mCounsellor == mGovernor))
            {
                Sango.Log.Info($"{Name} 军师 {mCounsellor.Name} 已失效，撤销其军师职务");
                ChangeCounsellor(null);
            }

            // 君主之外没有可任军师的人选，无需继续处理
            if (PersonCount <= 1) return;

            // 第二步：遍历本势力在籍武将，寻找智力最高的军师候选（排除君主与俘虏）
            Person best = null;
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c == null || !c.IsAlive || c.BelongForce != this || c.IsPrisoner || c == mGovernor) continue;

                if (best == null || c.Intelligence > best.Intelligence)
                    best = c;
            }

            if (best == null) return;

            // 第三步：按势力类型决定是否重新任命
            if (!IsPlayer)
            {
                // AI 势力：军师空缺，或候选人的智力严格高于现任军师时，更换为更好的人选
                if (mCounsellor == null || best.Intelligence > mCounsellor.Intelligence)
                {
                    Sango.Log.Info($"{Name} 任命 {best.Name}（智力 {best.Intelligence}）为军师");
                    ChangeCounsellor(best);
                }
            }
            else if (mCounsellor == null)
            {
                // 玩家势力：只在空缺时自动补任，避免覆盖玩家的军师安排
                Sango.Log.Info($"{Name} 军师空缺，自动任命 {best.Name}（智力 {best.Intelligence}）为军师");
                ChangeCounsellor(best);
            }
        }

        /// <summary>
        /// 更换军师
        /// </summary>
        /// <param name="dest">新军师</param>
        public void ChangeCounsellor(Person dest)
        {
            Person old = mCounsellor;
            mCounsellor = dest;
            GameEvent.OnForceChangeCounsellor?.Invoke(this, old);
        }

        /// <summary>
        /// 更新有效的可创建物品类型
        /// </summary>
        public void UpdateValidCreatedItemTypes()
        {
            createdItemTypes.Clear();
            Scenario scenario = Scenario.Cur;
            Dictionary<int, ItemType> itemMap = new Dictionary<int, ItemType>();
            scenario.CommonData.ItemTypes.ForEach(it =>
            {
                if (it.cost > 0 && it.IsValid(this))
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

            createdItemTypes.AddRange(itemMap.Values.ToArray());
            createdItemTypes.Sort(SangoObject.Compare);
        }


    }
}
