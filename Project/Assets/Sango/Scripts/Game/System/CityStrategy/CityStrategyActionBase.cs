using System;
using Sango.Core.Duel;
using Sango.Core.Player;

namespace Sango.Core
{
    /// <summary>
    /// 城市计略类型。
    /// 与 CityJobType 的对应关系由各计略行为自身的 JobType 属性维护，本枚举只保留自身的连续小编号，
    /// 编号会写入存档的 missionParams2，因此不可重排，新增类型只能追加在末尾。
    /// </summary>
    public enum CityStrategyType : int
    {
        /// <summary>
        /// 二虎竞食：挑拨两个第三方势力的交情
        /// </summary>
        TwoTigers = 0,

        /// <summary>
        /// 驱虎吞狼：本期暂不实现，仅占位以保持与 CityJobType 的对齐关系
        /// </summary>
        TigerDevour = 1,

        /// <summary>
        /// 流言：降低敌方据点武将忠诚，目标为都市时同时降低治安
        /// </summary>
        Rumor = 2,
    }

    /// <summary>
    /// 城市计略的结算结果。
    /// 四态来自原版流言结算钩子的返回值约定（解密脚本 721 AI优化-流言.cpp:19-50 标注"原游戏写法"：
    /// 0 成功未发现 / 1 成功被发现 / 2 失败 / 3 失败被捕），驱虎吞狼的"反被所捕"文案（s11msg01.cpp:8821）
    /// 说明"被捕"是这一类计略共有的结果维度，因此放在基类而不是只给流言用。
    /// 该枚举只存在于单次结算的调用栈内，不写入存档。
    /// </summary>
    public enum CityStrategyResult : int
    {
        /// <summary>
        /// 成功且使者未被发现：效果全额落地，无关系反噬
        /// </summary>
        SucceededUndetected = 0,

        /// <summary>
        /// 成功但使者露馅（险胜）：效果照常落地，另按 cityStrategyDetectedPenalty 反噬关系
        /// </summary>
        SucceededDetected = 1,

        /// <summary>
        /// 失败：效果为零，按 cityStrategyFailedPenalty 反噬关系
        /// </summary>
        Failed = 2,

        /// <summary>
        /// 失败且使者被当场收押：除关系反噬外，使者成为目标城俘虏，不再生还
        /// </summary>
        FailedCaptured = 3,
    }

    /// <summary>
    /// 城市计略行为基类。
    /// 职责：承载一次计略所需的全部上下文（发起方、出发城、使者、目标据点、目标势力对），
    /// 并把"能不能放""成功率多少""抵达后产生什么结果"三件事交给具体计略子类实现。
    /// 与外交行为基类（DiplomacyActionBase）的区别：计略没有接收方意志，结果完全由掷骰决定；
    /// 且计略的金钱与行动力在派遣时即扣除（视为使者出行的消耗），不在抵达时扣除。
    /// 由 CityStrategyManager 负责创建、派遣和抵达结算，子类不自己驱动流程。
    /// </summary>
    public abstract class CityStrategyActionBase
    {
        /// <summary>
        /// 义理档位的最高值。工程内 Argumentation.kind 取 1..5（1=容易背叛，5=高义理），
        /// 流言按"义理越低越容易被谣言动摇"逐人加权，故以最高档做差取惩罚值（对齐 155 的 (义理_高 - giri)/2）。
        /// </summary>
        protected const int MaxGiriTier = 5;

        /// <summary>
        /// 计略类型
        /// </summary>
        public CityStrategyType StrategyType { get; protected set; }

        /// <summary>
        /// 本计略对应的城市工作类型，用于读取 JobTypes.json 中的金钱/行动力消耗与菜单配置
        /// </summary>
        public CityJobType JobType { get; protected set; }

        /// <summary>
        /// 发起方势力
        /// </summary>
        public Force Sender { get; protected set; }

        /// <summary>
        /// 出发据点（使者所在、扣钱扣行动力的城市）
        /// </summary>
        public City FromCity { get; protected set; }

        /// <summary>
        /// 执行计略的使者武将，由 CityStrategyManager.Dispatch 置空校验
        /// </summary>
        public Person Diplomat { get; set; }

        /// <summary>
        /// 使者目的地据点。二虎竞食取第一个目标势力的治所，流言取被施法的敌方据点；
        /// 该值同时作为武将任务的 missionTarget
        /// </summary>
        public City TargetCity { get; protected set; }

        /// <summary>
        /// 目标势力A：二虎竞食为被挑拨的第一方，流言为被施法据点的所属势力
        /// </summary>
        public Force TargetForceA { get; protected set; }

        /// <summary>
        /// 目标势力B：仅二虎竞食使用，其余计略为 null
        /// </summary>
        public Force TargetForceB { get; protected set; }

        /// <summary>
        /// 最近一次 CalculateSuccessRate 的结果（0-100 百分比）
        /// </summary>
        public int SuccessRate { get; protected set; }

        /// <summary>
        /// 最近一次结算中真正负责识破本计略的抵抗者，供日志与广播复述。
        /// 由 GetResistance 赋值，可能为空（对方既无军师也无太守也无君主，理论上只出现在灭亡边缘的势力）
        /// </summary>
        public Person Resister { get; protected set; }

        /// <summary>
        /// 本次结算里使者是否已被敌方收押。
        /// 由流言的捕俘分支置位，CityStrategyManager 据此把"失败被擒"与"普通失败"区分给上层：
        /// 被擒时使者不能再挂返程任务（City.AddCaptive 会把 mBelongCity 置空）。
        /// </summary>
        public bool EnvoyCaptured { get; protected set; }

        /// <summary>
        /// 任务参数3，随计略类型而定：二虎竞食为目标势力对中 Id 较小的一方，流言为目标势力 Id。
        /// 派遣时写入 missionParams3，抵达后据此还原本次计略的作用对象
        /// </summary>
        public virtual int MissionParam3 => 0;

        /// <summary>
        /// 任务参数4，随计略类型而定：二虎竞食为目标势力对中 Id 较大的一方，其余计略恒为 0
        /// </summary>
        public virtual int MissionParam4 => 0;

        /// <summary>
        /// 构造计略行为
        /// </summary>
        /// <param name="strategyType">计略类型</param>
        /// <param name="jobType">对应的城市工作类型</param>
        /// <param name="sender">发起方势力</param>
        /// <param name="fromCity">出发据点</param>
        /// <param name="diplomat">使者武将，可为 null（由 Dispatch 前补齐）</param>
        /// <param name="targetCity">使者目的地据点，不可为 null，SetMission 会直接解引用</param>
        /// <param name="targetForceA">目标势力A</param>
        /// <param name="targetForceB">目标势力B，仅二虎竞食使用</param>
        protected CityStrategyActionBase(CityStrategyType strategyType, CityJobType jobType, Force sender, City fromCity, Person diplomat, City targetCity, Force targetForceA, Force targetForceB)
        {
            StrategyType = strategyType;
            JobType = jobType;
            Sender = sender;
            FromCity = fromCity;
            Diplomat = diplomat;
            TargetCity = targetCity;
            TargetForceA = targetForceA;
            TargetForceB = targetForceB;
        }

        /// <summary>
        /// 检查当前上下文是否允许发起本计略。
        /// 只判断"目标是否成立"这一层（势力存活、据点有效、是否已有同目标计略在途等由调用方处理），
        /// 金钱与行动力是否足够由 CityStrategyManager.Dispatch 统一判断，避免各子类重复实现。
        /// </summary>
        /// <returns>可以发起返回 true</returns>
        public abstract bool CanPerform();

        /// <summary>
        /// 计算本计略的成功率，同时把结果写入 SuccessRate
        /// </summary>
        /// <returns>成功率（0-100 百分比，已按配置的上下限钳制）</returns>
        public abstract int CalculateSuccessRate();

        /// <summary>
        /// 按给定结果结算计略效果，不做任何条件检查。
        /// 由 CityStrategyManager.ExecuteMission 在使者抵达后调用。
        /// </summary>
        /// <param name="result">本次计略的四态结果</param>
        public abstract void Perform(CityStrategyResult result);

        /// <summary>
        /// 用已算好的成功率掷一次骰，得出四态结果。
        /// 整局一次计略只掷一次成功判定；只有落到"失败"分支时才会再掷一次被捕判定，
        /// 因此成功概率严格等于 SuccessRate，不会被被捕概率稀释。
        /// </summary>
        /// <param name="rate">成功率（百分比）</param>
        /// <returns>四态结果之一</returns>
        public virtual CityStrategyResult RollResult(int rate)
        {
            if (!GameRandom.Chance(rate))
            {
                // 失败分支才再掷一次被捕骰：成功概率严格等于 SuccessRate，不会被被捕概率稀释
                if (CanBeCaptured() && GameRandom.Chance(CalculateCaptureRate()))
                    return CityStrategyResult.FailedCaptured;
                return CityStrategyResult.Failed;
            }

            // 险胜即露馅：以成功率而非效果量作暴露判据，等价于原版用 wisdom_diff 大小区分 0/1 两态
            return rate < Scenario.Cur.Variables.cityStrategyDetectedRateLine
                ? CityStrategyResult.SucceededDetected
                : CityStrategyResult.SucceededUndetected;
        }

        /// <summary>
        /// 失败时使者是否会被收押。默认 false：原版只有针对具体据点/具体武将的计略（流言、驱虎吞狼）
        /// 才有"反被所捕"的表现，二虎竞食只是挑拨关系，不存在潜入现场被抓的环节。
        /// </summary>
        /// <returns>可能被捕返回 true</returns>
        protected virtual bool CanBeCaptured()
        {
            return false;
        }

        /// <summary>
        /// 计算失败时的被捕概率（百分比），并做"只降不增"的智力减免与豁免判定。
        /// 原版脚本的被捕判据方向自相矛盾（抵抗值越高反而越易被捕，见 721:49 与 01 破坏:183），
        /// 故本实现改为按使者自身智力给减免：智力越高越不容易失手被擒，低于起算线不额外加罚，
        /// 并且始终保留一个概率下限，避免流言退化成零成本指令。
        /// </summary>
        /// <returns>被捕概率（0-100 百分比）</returns>
        protected int CalculateCaptureRate()
        {
            if (Diplomat == null)
                return 0;

            // 原版三条豁免（721:47）：君主亲往不会被俘、持强运、骑名马。
            // 工程内马具在单挑层一律按名马归类（DuelItemMap.GetCategory 对任何马都返回 EliteHorse），
            // 因此这里直接用"是否坐骑"作名马豁免，不再新造第二套名马判定；君主本身也不可被收押（City.AddCaptive 会拒绝）
            if (Sender != null && Diplomat == Sender.mGovernor)
                return 0;
            if (Diplomat.HasFeatrue(DuelFeatureId.Lucky))
                return 0;
            if (Diplomat.EquippedHorse != null)
                return 0;

            ScenarioVariables variables = Scenario.Cur.Variables;
            int rate = variables.cityStrategyRumorCaptureBaseRate;
            // 智力减免：只对超出减免线的部分逐点递减，绝不反向加罚
            rate -= Math.Max(0, Diplomat.Intelligence - variables.cityStrategyRumorCaptureReliefLine)
                * variables.cityStrategyRumorCaptureReliefPerPoint;
            return Math.Max(variables.cityStrategyRumorCaptureMin, Math.Min(variables.cityStrategyRumorCaptureBaseRate, rate));
        }

        /// <summary>
        /// 按统一口径计算成功率：平方和归一化主项 + 智力差线性修正，再钳到配置的上下限。
        /// 二虎竞食与流言共用本公式，差异只体现在抵抗方智力 D 的取法上。
        /// </summary>
        /// <param name="diplomatIntelligence">使者智力 E，使者为空时传 0</param>
        /// <param name="resistanceIntelligence">抵抗方智力 D</param>
        /// <returns>钳制后的成功率（0-100 百分比）</returns>
        protected int CalculateRate(int diplomatIntelligence, int resistanceIntelligence)
        {
            ScenarioVariables variables = Scenario.Cur.Variables;
            int diplomatValue = Math.Max(0, diplomatIntelligence);
            int resistanceValue = Math.Max(0, resistanceIntelligence);

            // 双方智力皆为 0 时平方和为 0，无法用归一化式，直接按下限给一个可判定的概率（不做除零兜底成 100）
            int squareSum = diplomatValue * diplomatValue + resistanceValue * resistanceValue;
            if (squareSum <= 0)
                return variables.cityStrategyRateMin;

            // 主项：E²/(E²+D²) 天然收敛，对等智力得 coefficient/2，双倍智力优势约 4/5 coefficient
            int rate = variables.cityStrategyRateCoefficient * diplomatValue * diplomatValue / squareSum;
            int edge = diplomatValue - resistanceValue;
            // 修正：领先按 1/系数 加分，落后按 系数 减分，保留原版"落后代价更大"的非对称方向
            rate += edge > 0 ? edge / variables.cityStrategyIntelligenceFactor : edge * variables.cityStrategyIntelligenceFactor;
            return Math.Max(variables.cityStrategyRateMin, Math.Min(variables.cityStrategyRateMax, rate));
        }

        /// <summary>
        /// 取本次计略的抵抗者：军师 → 目标据点太守 → 君主，第一个存活的有效者。
        /// 原版流言的识破者就是这条回退链（721:34-40 与 01 破坏:165-174 完全一致），
        /// 而不是"据点内最高智力"；无军师时也不能直接按 0 计，否则抵抗强度会被腰斩。
        /// </summary>
        /// <param name="force">被施计势力，可为空</param>
        /// <param name="city">被施计据点，可为空（二虎竞食没有单一被施计据点，传 null 后回退到君主）</param>
        /// <returns>抵抗者，全部缺位时返回 null</returns>
        protected Person GetResistance(Force force, City city)
        {
            if (force == null)
                return null;

            Person resister = force.mCounsellor;
            if (IsEffectiveResister(resister))
                return resister;

            resister = city != null ? city.Leader : null;
            if (IsEffectiveResister(resister))
                return resister;

            resister = force.mGovernor;
            return IsEffectiveResister(resister) ? resister : null;
        }

        /// <summary>
        /// 抵抗者是否可用：非空且存活。死亡武将不应替敌方识破谣言。
        /// </summary>
        /// <param name="person">候选抵抗者</param>
        /// <returns>可用返回 true</returns>
        protected static bool IsEffectiveResister(Person person)
        {
            return person != null && person.IsAlive;
        }

        /// <summary>
        /// 派遣时的回调，由 CityStrategyManager.Dispatch 在任务和消耗都落地之后调用
        /// </summary>
        public virtual void OnDispatch()
        {
#if SANGO_DEBUG
            Sango.Log.Info($"@计略@{Sender?.Name} 从 {FromCity?.Name} 派遣 {Diplomat?.Name ?? "使者"} 前往 {TargetCity?.Name} 执行{GetActionName()}");
#endif
        }

        /// <summary>
        /// 计略彻底失败的反噬：使者身份暴露，发起方与各目标方的交情下降，并把失败结果广播到左下角信息窗口。
        /// 反噬值为固定配置（cityStrategyFailedPenalty），不随成功率缩放；配置为 0 时只广播不扣关系。
        /// </summary>
        public virtual void OnFailed()
        {
            PenalizeSenderRelation(Scenario.Cur.Variables.cityStrategyFailedPenalty, "被识破");
        }

        /// <summary>
        /// 计略成功但使者露馅的轻反噬：效果已经落地，只是关系上付一点代价。
        /// </summary>
        protected void OnDetected()
        {
            PenalizeSenderRelation(Scenario.Cur.Variables.cityStrategyDetectedPenalty, "露了行迹");
        }

        /// <summary>
        /// 对"发起方与各目标方"扣关系并广播，供失败与露馅两条分支复用。
        /// 逐个目标方处理：二虎竞食会同时得罪被挑拨的双方，流言只得罪被施法的一方。
        /// </summary>
        /// <param name="penalty">反噬值（正数，配 0 表示只广播不扣关系）</param>
        /// <param name="headline">广播文案里的结果短语</param>
        protected void PenalizeSenderRelation(int penalty, string headline)
        {
            if (Sender == null)
                return;

            // 逐个目标方给出"前值→现值"的具体数，而不是只说"关系下降"：
            // 关系刻度是 ±5000 的抽象值，玩家看不到扣完之后离翻脸还差多少
            string detail = null;
            if (penalty > 0)
            {
                DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                detail = ReduceSenderRelation(diplomacyManager, TargetForceA, penalty);
                string detailB = ReduceSenderRelation(diplomacyManager, TargetForceB, penalty);
                if (detailB != null)
                    detail = detail == null ? detailB : $"{detail}；{detailB}";
            }

            // 反噬配成 0 时也要广播：否则玩家只知道使者回来了，不知道白跑了一趟
            string headlineText = $"{Sender.ColorName}派{Diplomat?.ColorName ?? "使者"}施放的{GetActionName()}{headline}";
            BroadcastMessage(string.IsNullOrEmpty(detail) ? $"{headlineText}。" : $"{headlineText}，{detail}。");

#if SANGO_DEBUG
            Sango.Log.Info($"@计略@{Sender.Name} 的{GetActionName()}{headline}，与目标方关系扣减 {penalty}：{detail ?? "无（反噬为0或无有效目标）"}");
#endif
        }

        /// <summary>
        /// 把计略结果写入左下角信息窗口（玩家消息）。
        /// 消息归属势力取发起方，与外交事件的广播口径一致（DiplomacyEventManager）；
        /// 坐标取施法据点，玩家点击消息即可把镜头移到事发城市。玩家与 AI 势力施放的计略都会广播。
        /// </summary>
        /// <param name="text">广播正文，势力与武将名需自行用 ColorName 包色</param>
        protected void BroadcastMessage(string text)
        {
            // 目的地即施法现场；为空时退化为不绑定坐标，点击消息不跳转（先例 DebateGameSystem 传 0,0）
            int x = TargetCity != null ? TargetCity.x : 0;
            int y = TargetCity != null ? TargetCity.y : 0;
            PlayerMessage.AddTextMessage(text, Sender, x, y);
        }

        /// <summary>
        /// 对单个目标方扣减与发起方的关系，跳过空引用和自我反噬，并回一句可直接播报的前后值。
        /// 播报用"实扣"而不是配置值：关系值有 ±5000 钳制，逼近下限时的实扣会小于配置值，
        /// 只报配置值会让玩家以为还能继续扣下去。
        /// </summary>
        /// <param name="diplomacyManager">外交管理器</param>
        /// <param name="target">目标势力，可为 null</param>
        /// <param name="penalty">反噬值（正数）</param>
        /// <returns>可直接拼接的文案；目标无效时返回 null</returns>
        protected string ReduceSenderRelation(DiplomacyManager diplomacyManager, Force target, int penalty)
        {
            if (target == null || target == Sender)
                return null;
            int before = diplomacyManager.GetRelation(Sender, target);
            diplomacyManager.ReduceRelation(Sender, target, penalty);
            int after = diplomacyManager.GetRelation(Sender, target);
            return $"与{target.ColorName}的关系由 {before} 降至 {after}（实扣 {before - after}）";
        }

        /// <summary>
        /// 获取计略名称，用于日志与对话文案
        /// </summary>
        /// <returns>计略中文名</returns>
        public virtual string GetActionName()
        {
            switch (StrategyType)
            {
                case CityStrategyType.TwoTigers:
                    return "二虎竞食";
                case CityStrategyType.TigerDevour:
                    return "驱虎吞狼";
                case CityStrategyType.Rumor:
                    return "流言";
                default:
                    return "未知计略";
            }
        }
    }
}
