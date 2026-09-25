namespace Sango.Core
{
    /// <summary>
    /// 执行层：AI 部署范畴内**唯一**允许调动武将的地方（Phase B）。
    ///
    /// 所有调动都必须先过 <see cref="CanTransfer"/> 的合法性闸门，再由 <see cref="Transfer"/> 执行；
    /// 这样"守备下限 / 被围禁入 / 在途 / 主公军团长 / 玩家开关"这些约束就只有一处实现，
    /// 不会再出现"某条路径绕过去把人抽空"的情况（旧代码里 `CityAI.cs` 的迁人就绕过了）。
    ///
    /// 影子模式（<c>DeploymentWeights.shadowOnly</c>）下闸门照常判定，但**不执行**——
    /// 因此报告里能看到"如果执行会怎样、被谁否决"，而局面不变。
    /// </summary>
    public static class DeploymentExecutor
    {
        /// <summary>判定结果（含否决原因）。</summary>
        public struct Gate
        {
            /// <summary>是否允许调动</summary>
            public bool allowed;
            /// <summary>否决原因（allowed 为 true 时为空）</summary>
            public string reason;
        }

        /// <summary>与 CitySituation.isUnderSiege 同口径的"兵临城下"距离</summary>
        const int BesiegeRange = 6;

        /// <summary>
        /// 合法性闸门：判断能否把 <paramref name="person"/> 调到 <paramref name="dest"/>。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="dest">目标城</param>
        /// <param name="weights">部署参数</param>
        /// <returns>判定结果</returns>
        public static Gate CanTransfer(Person person, City dest, DeploymentWeights weights)
        {
            return CanTransfer(person, dest, weights, false);
        }

        /// <summary>
        /// 合法性闸门（带"仅参考"模式）。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="dest">目标城</param>
        /// <param name="weights">部署参数</param>
        /// <param name="adviceMode">
        /// 是否"仅参考"模式：跳过**管理者层面**的两条否决（"人才"开关 / 第一军团直辖）。
        ///
        /// 用于玩家直辖军团的参考报告 —— 它要回答的是"如果允许 AI 管，它会怎么调"，
        /// 若照常否决，报告会被自己的直辖规则清空成"零建议"。位置 / 在途 / 被围 / 军团边界 /
        /// 源城守备下限等**硬规则仍然照常生效**。
        /// </param>
        /// <returns>判定结果</returns>
        public static Gate CanTransfer(Person person, City dest, DeploymentWeights weights, bool adviceMode)
        {
            if (person == null || dest == null)
                return Deny("空引用");
            if (person.IsDead)
                return Deny("武将已阵亡");
            if (person.IsPrisoner)
                return Deny("武将是俘虏");
            if (person.BelongForce == null || dest.BelongForce == null)
                return Deny("归属缺失");
            if (person.BelongForce != dest.BelongForce)
                return Deny("势力不同");
            if (person.BelongCity == dest)
                return Deny("已在目标城");
            if (!person.IsFree)
                return Deny("在途 / 部队中 / 有任务");

            bool isPlayerForce = person.BelongForce.IsPlayer;

            // ---------- 军团边界（**只约束玩家势力**） ----------
            // 玩家军团之间不互相调人 —— 跨团会打乱玩家自己排好的部署。
            // 非玩家势力以**势力**为边界：势力内跨军团调动是允许的（AI 在自己地盘里怎么调人是它自己的事）。
            City from = person.BelongCity;
            if (isPlayerForce && (weights == null || weights.enforceCorpsBoundary))
            {
                Corps fromCorps = from != null ? from.BelongCorps : null;
                Corps destCorps = dest.BelongCorps;
                if (fromCorps != null && destCorps != null && fromCorps != destCorps)
                    return Deny("跨军团：目标城属于其他军团");
            }

            // ---------- 军团委任条件（**只约束玩家势力**；"仅参考"模式跳过） ----------
            // "人才"开关未开启（玩家自己管）的军团，AI 不得代管其人员调动（决策 4）。
            // 注意：入口层（DeploymentShadow）已按此开关整块跳过未开启的军团，
            // 这里是**兜底**——防止将来出现绕过入口的调用路径。
            if (isPlayerForce && !adviceMode)
            {
                if (dest.BelongCorps != null && !IsPersonManaged(dest.BelongCorps))
                    return Deny("目标军团的人才由玩家自己管理");

                // 第一军团（君主所在军团）由玩家直辖，AI 一律不介入：
                // 玩家自己排好的部署不能被自动调度打乱。
                if (dest.BelongCorps != null && dest.BelongCorps == person.BelongForce.CapitalCorps)
                    return Deny("第一军团由玩家直辖");
            }

            // 目标城被围：不派人进去送死
            if (dest.EnemyCount > 0 && dest.NearestEnemyDistance <= BesiegeRange)
                return Deny("目标城被围");

            // 源城守备下限：港关 / 前线不能把人抽空（旧 CityAI 迁人路径的元凶）
            if (from != null && weights != null)
            {
                bool isPortGate = from.IsPort() || from.IsGate();
                int floor;
                if (isPortGate)
                    floor = weights.minPortGateGuard;
                else if (from.IsBorderCity)
                    floor = weights.minMilitarySeatAtBorder;
                else
                    floor = CityEstablishment.MinRearSeats(from, weights);   // 后方城：1~5（运输 + 资源积累保底）
                // 【口径修正 · 关键】保底必须按"**可用（空闲）**人数"算，而不是户口人数。
                // 户口（allPersons）里可能大多数人已在部队 / 正在执行内政任务，于是出现
                // "在册 10 人、却一个都派不出运输队" → 表现就是"后方被调到 0、资源满了运不出去"。
                // freePersons 才是"此刻能派出去干活的人"，与"最低运转"的语义一致。
                int remain = from.freePersons != null ? from.freePersons.Count : 0;
                if (remain <= floor)
                {
                    // 【轮换放行】港关守将允许"向更前线"调动：
                    // 目标是比自己更靠近边境的城 / 港时放行 —— 决策 3 明确"港关最低可到 0 人"，
                    // 把守将从后方港调去更吃紧的前线港是有意义的；反向（往后方撤）才拒绝。
                    bool forwardRotation = isPortGate
                        && CityEstablishment.ResolveRing(dest) < CityEstablishment.ResolveRing(from);
                    if (!forwardRotation)
                        return Deny(isPortGate ? "源港关守备已达下限" : "源前线守备已达下限");
                }
            }

            Gate gate;
            gate.allowed = true;
            gate.reason = null;
            return gate;
        }

        /// <summary>
        /// 执行调动：过闸门 → 下发户口变更（`Person.TransformToCity`，非瞬移）。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="dest">目标城</param>
        /// <param name="weights">部署参数</param>
        /// <param name="execute">是否真正执行（影子模式 / 仅参考传 false，只判定）</param>
        /// <param name="turn">当前势力回合号（用于防抖）</param>
        /// <param name="adviceMode">是否"仅参考"模式（见 <see cref="CanTransfer(Person, City, DeploymentWeights, bool)"/>）</param>
        /// <param name="reason">返回：失败原因；成功为 null</param>
        /// <returns>是否真的执行了调动</returns>
        public static bool Transfer(Person person, City dest, DeploymentWeights weights, bool execute, int turn,
            bool adviceMode, out string reason)
        {
            reason = null;

            Gate gate = CanTransfer(person, dest, weights, adviceMode);
            if (!gate.allowed)
            {
                reason = gate.reason;
                return false;
            }

            if (!execute)
                return false;                       // 影子模式：只判定

            person.TransformToCity(dest);
            DeploymentState.MarkTransfer(person.Id, turn);
            return true;
        }

        /// <summary>
        /// 该军团的"人才"委任开关是否开启（开启 = 允许 AI 代管其人员调动）。
        ///
        /// `Corps.AppointContentType.Person`：**0 = 允许**（AI 代管），**1 = 玩家自己管**。
        /// **只对玩家势力的军团有意义** —— AI 势力以势力为边界，不受此开关约束。
        /// 入口层（<see cref="DeploymentShadow.RunPlayerCorps"/>）用它决定"这个军团要不要跑调度"，
        /// 闸门用它做兜底 —— 两处共用本方法，避免规则分裂。
        /// </summary>
        /// <param name="corps">军团</param>
        /// <returns>是否允许 AI 代管其人员</returns>
        public static bool IsPersonManaged(Corps corps)
        {
            if (corps == null)
                return false;
            return corps.GetAppointValue(Corps.AppointContentType.Person) != 1;
        }

        static Gate Deny(string reason)
        {
            Gate gate;
            gate.allowed = false;
            gate.reason = reason;
            return gate;
        }
    }
}
