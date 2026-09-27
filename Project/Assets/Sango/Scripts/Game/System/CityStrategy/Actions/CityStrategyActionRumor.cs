namespace Sango.Core
{
    /// <summary>
    /// 流言：使者抵达敌方据点后散布谣言，降低该据点全体武将的忠诚；
    /// 目标为都市（非港/关）时同时降低治安。
    /// 治安是都市独有属性，港口和关隘没有治安概念，因此治安下降只对都市生效。
    /// 结算是四态（成功未被发现 / 成功露馅 / 失败 / 失败被擒），对齐原版流言结算钩子的返回值契约。
    /// </summary>
    public class CityStrategyActionRumor : CityStrategyActionBase
    {
        /// <summary>
        /// 构造流言行为
        /// </summary>
        /// <param name="sender">发起方势力</param>
        /// <param name="fromCity">出发据点</param>
        /// <param name="diplomat">使者武将</param>
        /// <param name="targetCity">被施法的敌方据点，同时是使者目的地</param>
        /// <param name="targetForce">被施法据点的所属势力，用于取抵抗者和失败反噬</param>
        public CityStrategyActionRumor(Force sender, City fromCity, Person diplomat, City targetCity, Force targetForce)
            : base(CityStrategyType.Rumor, CityJobType.Rumor, sender, fromCity, diplomat, targetCity, targetForce, null)
        {
        }

        /// <summary>
        /// 任务参数3：被施法据点的所属势力 Id。
        /// 这里存的是派遣时的快照，抵达后据点若已易主，效果仍然落在据点本身（按城市 Id 结算），
        /// 只有抵抗智力和反噬关系沿用这个快照势力
        /// </summary>
        public override int MissionParam3 => TargetForceA?.Id ?? 0;

        /// <summary>
        /// 检查是否具备施放条件：目标据点有效且归属其他存活势力。
        /// </summary>
        /// <returns>可以施放返回 true</returns>
        public override bool CanPerform()
        {
            if (Sender == null || TargetCity == null)
                return false;
            Force owner = TargetCity.mBelongForce;
            if (owner == null || !owner.IsAlive)
                return false;
            // 流言是攻心术，对自己人施放没有意义，也会在客户端造成治安/忠诚的无端抖动
            if (owner == Sender)
                return false;
            return true;
        }

        /// <summary>
        /// 只有流言会让使者亲身潜入敌城，因此也只有它有被捕风险（二虎竞食只是挑拨关系，不存在潜入现场）。
        /// </summary>
        /// <returns>恒为 true</returns>
        protected override bool CanBeCaptured()
        {
            return Diplomat != null;
        }

        /// <summary>
        /// 计算成功率：抵抗者按"目标势力军师 → 被施法据点太守 → 目标势力君主"的回退链取单人智力，
        /// 与原版流言结算契约一致（721 AI优化-流言.cpp:34-40）。
        /// 不再取"据点内最高智力与军师的算术平均"，也不再把"没设军师"当成抵抗值 0。
        /// </summary>
        /// <returns>成功率（0-100 百分比）</returns>
        public override int CalculateSuccessRate()
        {
            Resister = GetResistance(TargetForceA, TargetCity);
            SuccessRate = CalculateRate(Diplomat?.Intelligence ?? 0, Resister?.Intelligence ?? 0);
            return SuccessRate;
        }

        /// <summary>
        /// 结算流言的四种结果：
        /// 成功（无论是否露馅）都照常掉忠诚与治安；露馅与失败才反噬关系；失败且被捕额外把使者收进敌城俘虏。
        /// </summary>
        /// <param name="result">本次计略的四态结果</param>
        public override void Perform(CityStrategyResult result)
        {
            switch (result)
            {
                case CityStrategyResult.SucceededUndetected:
                    BroadcastEffect(ApplyEffects(), false);
                    break;
                case CityStrategyResult.SucceededDetected:
                    // 事成但露馅：效果保留，另付一份较轻的关系代价，并给对方留下报复的因由
                    BroadcastEffect(ApplyEffects(), true);
                    OnDetected();
                    break;
                case CityStrategyResult.FailedCaptured:
                    OnFailed();
                    TryCaptureEnvoy();
                    break;
                default:
                    OnFailed();
                    break;
            }

            // 记仇：只有"对方查出了是谁干的"的三种结果才记账（露馅、失败、失败被擒）。
            // 成功且未暴露时受害者并不知道源头，按原版 721 的口径不给报复因由，
            // 否则流言会变成"零风险刷仇恨"，与未发现的语义冲突
            if (result != CityStrategyResult.SucceededUndetected)
                TargetCity?.mBelongForce?.AddCityStrategyGrudge(Sender);
        }

        /// <summary>
        /// 落地流言的数值效果：整城存活武将按同一个基准值掉忠诚（逐人按义理加权），都市另掉治安。
        /// 忠诚与治安的降幅各自只掷一次并整城共用（已确认口径），且不随成功率缩放。
        /// GameRandom.Range 的上界是开区间，配置里的 Max 表示闭区间上界，因此传 max + 1。
        /// </summary>
        /// <returns>供广播与日志复述的效果摘要</returns>
        private string ApplyEffects()
        {
            ScenarioVariables variables = Scenario.Cur.Variables;

            // 忠诚下降：整城同一基准值，走 Person.AddLoyalty 统一钳制到 0..100
            int baseDrop = GameRandom.Range(variables.cityStrategyRumorLoyaltyDropMin, variables.cityStrategyRumorLoyaltyDropMax + 1);
            int affectedCount = 0;
            int maxDrop = baseDrop;
            TargetCity.allPersons.ForEach(person =>
            {
                if (person == null || !person.IsAlive)
                    return;
                // 义理越低越容易被谣言动摇（对齐 155 每月忠诚减少的 (义理_高 - giri)/2 加权方向）
                int drop = baseDrop + GetGiriWeight(person, variables.cityStrategyRumorGiriDivisor);
                person.AddLoyalty(-drop);
                affectedCount++;
                if (drop > maxDrop)
                    maxDrop = drop;
            });

            // 治安下降：只对都市生效，港/关无治安属性
            int securityDrop = 0;
            if (TargetCity.IsCity())
            {
                securityDrop = GameRandom.Range(variables.cityStrategyRumorSecurityDropMin, variables.cityStrategyRumorSecurityDropMax + 1);
                TargetCity.AddSecurity(-securityDrop);
            }

            // 记仇不在这里做：是否记账取决于"有没有暴露发起方"，见 Perform 末尾

            // 广播里带上实际生效的武将在数与治安降幅：港/关不降治安，所以治安句必须按 securityDrop 条件拼接
            string effect = $"{affectedCount} 名武将忠诚下降 {baseDrop}" + (maxDrop > baseDrop ? $"~{maxDrop}" : "");
            if (securityDrop > 0)
                effect += $"，治安下降 {securityDrop}";
            return effect;
        }

        /// <summary>
        /// 单个武将的义理加权降幅。
        /// </summary>
        /// <param name="person">受影响的武将</param>
        /// <param name="giriDivisor">加权分母，配 0 表示不加权</param>
        /// <returns>在基准值之上额外扣除的忠诚点数（>=0）</returns>
        private static int GetGiriWeight(Person person, int giriDivisor)
        {
            if (giriDivisor <= 0 || person?.mArgumentation == null)
                return 0;
            // Argumentation.kind 越大越讲信义（1=容易背叛 … 5=高义理），故义理越低扣得越多
            int penalty = MaxGiriTier - person.mArgumentation.kind;
            return penalty > 0 ? penalty / giriDivisor : 0;
        }

        /// <summary>
        /// 广播流言效果。
        /// </summary>
        /// <param name="effect">效果摘要</param>
        /// <param name="detected">是否附带露馅说明</param>
        private void BroadcastEffect(string effect, bool detected)
        {
            string headline = detected ? "奏效，但使者露了行迹" : "奏效";
            BroadcastMessage($"{Sender?.ColorName}派{Diplomat?.ColorName ?? "使者"}施放的{GetActionName()}{headline}，{TargetCity?.ColorName}城中{effect}。");

#if SANGO_DEBUG
            Sango.Log.Info($"@计略@{Sender?.Name} 的{GetActionName()}对 <{TargetCity.Name}> 生效（露馅：{(detected ? "是" : "否")}，抵抗者：{Resister?.Name ?? "无"}）：{effect}");
#endif
        }

        /// <summary>
        /// 尝试把使者收进被施法据点的俘虏名单。
        /// 君主不可收押（City.AddCaptive 会拒绝），此时退回"普通失败"口径，让使者照常返程。
        /// </summary>
        private void TryCaptureEnvoy()
        {
            if (TargetCity == null || Diplomat == null)
                return;

            if (TargetCity.AddCaptive(Diplomat) != null)
            {
                EnvoyCaptured = true;
                BroadcastMessage($"{Diplomat.ColorName} 在散布流言情形败露，被{TargetCity.ColorName}当场收押。");
            }
#if SANGO_DEBUG
            Sango.Log.Info($"@计略@流言失败且使者被捕：{Diplomat?.Name} → 关押于 {TargetCity?.Name}，结果={EnvoyCaptured}");
#endif
        }
    }
}
