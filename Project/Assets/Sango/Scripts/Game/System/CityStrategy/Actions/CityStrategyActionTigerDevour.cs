namespace Sango.Core
{
    /// <summary>
    /// 驱虎吞狼：策反敌方据点太守，使其带着该城独立成一个新势力。
    ///
    /// 对齐出处：
    ///   · 判定与派遣 —— Open311 `s11_game_ai_kyoten_cmd_strategy.cpp:184-302`
    ///   · 结算效果   —— `s11_sys_person_order_strategy.cpp:739-776` 的 on_collaborate_success
    ///                   （核心是 :769 的 <c>create_force_independence(target, false, false)</c>）
    ///   · 露馅分支   —— `:778-781` on_collaborate_detected 直接复用成功逻辑，不额外降关系
    ///
    /// 【语义要点】原版的效果是"太守独立建国"，**不是**"带城投奔发起方"。
    /// 发起方只获得"敌人分裂"这一战略收益，并不会白得一座城。
    /// </summary>
    public class CityStrategyActionTigerDevour : CityStrategyActionBase
    {
        /// <summary>
        /// 构造驱虎吞狼行为
        /// </summary>
        /// <param name="sender">发起方势力</param>
        /// <param name="fromCity">出发据点</param>
        /// <param name="diplomat">使者武将</param>
        /// <param name="targetCity">被策反的敌方据点，同时是使者目的地</param>
        /// <param name="targetForce">被策反据点的所属势力，用于取抵抗者与失败反噬</param>
        public CityStrategyActionTigerDevour(Force sender, City fromCity, Person diplomat, City targetCity, Force targetForce)
            : base(CityStrategyType.TigerDevour, CityJobType.TigerDevour, sender, fromCity, diplomat, targetCity, targetForce, null)
        {
        }

        /// <summary>
        /// 任务参数3：被策反据点的所属势力 Id（派遣时快照）。
        /// </summary>
        public override int MissionParam3 => TargetForceA?.Id ?? 0;

        /// <summary>
        /// 检查是否具备施放条件：目标据点有效、归属其他存活势力、且城中有太守。
        /// </summary>
        /// <returns>可以施放返回 true</returns>
        public override bool CanPerform()
        {
            if (Sender == null || TargetCity == null)
                return false;

            Force owner = TargetCity.BelongForce;
            if (owner == null || !owner.IsAlive)
                return false;
            // 对自己人"策反"没有意义
            if (owner == Sender)
                return false;
            // 没有太守就没有策反对象
            Person taishu = TargetCity.Leader;
            if (taishu == null || !taishu.IsAlive)
                return false;
            // 君主不可被策反（他独立出去也只是原势力的延续，原版同样排除）
            if (taishu.IsGovernor)
                return false;
            return true;
        }

        /// <summary>
        /// 使者需要亲身潜入敌城，因此有被捕风险（与流言同口径）。
        /// </summary>
        /// <returns>可能被捕返回 true</returns>
        protected override bool CanBeCaptured()
        {
            return Diplomat != null;
        }

        /// <summary>
        /// 计算成功率：抵抗者按"目标势力军师 → 目标据点太守 → 目标势力君主"的回退链取智力。
        /// </summary>
        /// <returns>成功率（0-100 百分比）</returns>
        public override int CalculateSuccessRate()
        {
            Resister = GetResistance(TargetForceA, TargetCity);
            SuccessRate = CalculateRate(Diplomat?.Intelligence ?? 0, Resister?.Intelligence ?? 0);
            return SuccessRate;
        }

        /// <summary>
        /// 结算四种结果。
        /// 成功（无论是否露馅）都让太守独立；露馅额外付一份轻的关系代价（与其他计略同口径）；
        /// 失败与失败被擒走基类的反噬，被擒另把使者收进目标城俘虏名单。
        /// </summary>
        /// <param name="result">本次计略的四态结果</param>
        public override void Perform(CityStrategyResult result)
        {
            switch (result)
            {
                case CityStrategyResult.SucceededUndetected:
                    BroadcastEffect(ApplyIndependence(), false);
                    break;
                case CityStrategyResult.SucceededDetected:
                    BroadcastEffect(ApplyIndependence(), true);
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

            // 【已移除】与流言同口径：势力级主动施计已删除，不再记"计略仇"。
        }

        /// <summary>
        /// 执行"太守带城独立"。
        ///
        /// 两道兜底：① 抵达时目标城若已易主，本次不生效；② 太守已换人则按当前太守处理
        /// （原版 on_collaborate_timer_finished 也是抵达时重新取太守）。
        /// </summary>
        /// <returns>是否成功建立新势力</returns>
        private bool ApplyIndependence()
        {
            if (TargetCity == null || TargetForceA == null)
                return false;
            // 抵达时已易主：效果不落地
            if (TargetCity.BelongForce != TargetForceA)
                return false;

            Person taishu = TargetCity.Leader;
            if (taishu == null || !taishu.IsAlive || taishu.IsGovernor)
                return false;

            Force newForce = CityStateSystem.CreateIndependence(taishu, CityChangeReason.Event);
            return newForce != null;
        }

        /// <summary>
        /// 广播驱虎吞狼的效果。
        /// </summary>
        /// <param name="succeeded">是否真的建立了新势力</param>
        /// <param name="detected">是否露馅</param>
        private void BroadcastEffect(bool succeeded, bool detected)
        {
            if (!succeeded)
            {
                BroadcastMessage($"{Sender?.ColorName}派{Diplomat?.ColorName ?? "使者"}策反{TargetCity?.ColorName}守将，" +
                    $"但对方不为所动。");
                return;
            }

            string headline = detected ? "奏效，但使者露了行迹" : "奏效";
            BroadcastMessage($"{Sender?.ColorName}派{Diplomat?.ColorName ?? "使者"}施放的{GetActionName()}{headline}，" +
                $"{TargetCity?.ColorName}守将拥城自立，{TargetCity?.BelongForce?.ColorName ?? "新势力"}就此割据一方。");

#if SANGO_DEBUG
            Sango.Log.Info($"@计略@{Sender?.Name} 的{GetActionName()}对 <{TargetCity?.Name}> 生效" +
                $"（露馅：{(detected ? "是" : "否")}，抵抗者：{Resister?.Name ?? "无"}）");
#endif
        }

        /// <summary>
        /// 尝试把使者收进目标据点的俘虏名单（失败被擒时）。
        /// </summary>
        private void TryCaptureEnvoy()
        {
            if (TargetCity == null || Diplomat == null)
                return;

            if (TargetCity.AddCaptive(Diplomat) != null)
            {
                EnvoyCaptured = true;
                BroadcastMessage($"{Diplomat.ColorName} 策反{TargetCity.ColorName}守将事败被擒，当场收押。");
            }
        }
    }
}
