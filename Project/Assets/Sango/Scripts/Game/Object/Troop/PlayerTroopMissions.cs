/*
 * 文件名：PlayerTroopMissions.cs
 * 描述：委任部队（玩家第一军团）专用的部队任务行为。
 *
 * ============================ 设计说明 ============================
 *
 * 【为什么需要这一套】
 * 面向势力 AI 的 TroopXXX 行为类里带有大量"自作主张"的逻辑：
 *   · 任务完成后自动改派（返城 / 协防 / 追打别的目标）
 *   · 战场态势判断（主动撤退、补给队大劣就跑）
 *   · 自动搜寻下一个目标（工程队连建）
 *
 * 而玩家对自家第一军团部队的"委任"语义是：**只把指定任务执行掉**，
 * 不应有任何自作主张。
 *
 * 【此前是怎么做的】
 * 历史上这些差异被写成散落在行为类里的 `if (troop.IsPlayerControl)` 补丁
 * （见 TroopReturnCity / TroopMovetoCell / TroopDestroyTroop 的历史版本）。
 * 每多一个"完成后的收尾动作"，就要多一处这样的判断，最终会遍布所有行为类。
 *
 * 【现在的做法：物理分离】
 * 为委任部队单独设一套 MissionType（PlayerTroopXXX）+ 一套行为类。
 * 每个类是**薄壳**：继承对应的势力 AI 版以复用"找目标 / 移动 / 攻击"等核心行为，
 * 只声明两件事：
 *   · Prepare —— 切断"自动改派"（父类一旦改派即视为任务结束 → 交回玩家）
 *   · DoAI    —— 切断"完成后的自动收尾"（父类会改派返城 → 改为交回玩家）
 *
 * 于是行为类里不再需要 IsPlayerControl / AIType 之类的条件判断，
 * 原势力 AI 版中的 `IsPlayerControl` 补丁也随之删除（语义已由本套类型承担）。
 *
 * 【枚举映射】
 * Troop.SetMission 会在部队属于玩家第一军团时自动把 TroopXXX 映射为 PlayerTroopXXX
 * （见 TroopMissionBehaviour.ToPlayerMission），因此所有派发点
 * （UI 指令 / 行为类内部改派）都无需改动。
 *
 * 【收尾语义】
 * 委任部队任务完成后统一 ClearMission()，把控制权交回玩家（部队原地待命），
 * 而不是像势力 AI 那样自动跑去执行下一个任务。
 *
 * 【未迁移的旧存档】
 * 旧存档里玩家部队的 missionType 仍是 TroopXXX。读档时由
 * <c>Troop.NormalizeAppointMission</c> 统一转换为 PlayerTroopXXX
 * （在 Troop.Init 与首次 DoAI 各兜底一次），因此不会出现
 * "旧任务落到势力 AI 版行为类、完成后自动改派返城"的问题。
 */

namespace Sango.Core
{
    /// <summary>
    /// 委任行为薄壳的公共实现辅助。
    ///
    /// 说明：由于需要继承各自的势力 AI 版行为类（C# 单继承），
    /// 无法再抽一个共同基类，因此把统一的收尾逻辑写成静态辅助方法。
    /// </summary>
    internal static class PlayerTroopMissionHelper
    {
        /// <summary>
        /// 【委任收尾·Prepare 阶段】若父类的 Prepare 改派了任务
        /// （说明它认为原任务已完成 / 失效），则清空任务，把部队交回玩家。
        ///
        /// 检测方式：对比 Prepare 前后的 missionType。
        /// 之所以用"对比"而不是各自实现完成判定，是因为完成条件分散在各父类里
        /// （目标不存在 / 目标已死 / 目标不再是敌人 / 城池易主 …），
        /// 复用它比自己重写一套更准确，也不会随父类演进而失配。
        ///
        /// 例外：若父类的改派目标与自身任务相同（如 TroopReturnCity 改派回
        /// TroopReturnCity），映射后 missionType 不变，本方法检测不到——
        /// 这类行为需在子类里显式判定，见 <see cref="PlayerTroopReturnCity"/>。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="missionBefore">Prepare 之前的任务枚举（int）</param>
        internal static void FinishIfMissionChanged(Troop troop, int missionBefore)
        {
            if (troop == null)
                return;
            if (troop.missionType == missionBefore)
                return;

            ClearAndRelease(troop);
        }

        /// <summary>
        /// 【委任收尾·DoAI 阶段】任务完成时清空任务，把部队交回玩家。
        ///
        /// 势力 AI 版行为类在 DoAI 开头遇到 IsMissionComplete 时会改派"返城"；
        /// 委任部队改为在此收尾。
        /// </summary>
        /// <param name="behaviour">当前行为（用于取完成判定）</param>
        /// <param name="troop">部队</param>
        /// <returns>是否已收尾（调用方应直接返回 true）</returns>
        internal static bool FinishIfComplete(TroopMissionBehaviour behaviour, Troop troop)
        {
            if (behaviour == null || troop == null)
                return false;
            if (!behaviour.IsMissionComplete)
                return false;

            ClearAndRelease(troop);
            return true;
        }

        /// <summary>
        /// 【委任收尾】清空任务并把控制权交回玩家。
        /// </summary>
        /// <param name="troop">部队</param>
        internal static void ClearAndRelease(Troop troop)
        {
            troop.ClearMission();
            troop.NeedPrepareMission();
        }
    }

    /// <summary>
    /// 委任：移动到指定格子。玩家指派后 AI 只负责走过去，抵达即交回玩家。
    /// </summary>
    public class PlayerTroopMovetoCell : TroopMovetoCell
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopMovetoCell; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            // 复用势力 AI 版：解析目标格 → 通路检查
            base.Prepare(troop, scenario);

            // 目标格无效时父类会改派返城；委任语义下改为交回玩家
            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            // 【委任】抵达目标格即交回玩家，不自动改派返城
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：移动到己方城池。抵达后由父类的 DoAI 执行进城（交还资源并解散）。
    /// </summary>
    public class PlayerTroopMovetoCity : TroopMovetoCity
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopMovetoCity; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：返回所在城市。
    ///
    /// 这是委任体系里的"收尾任务"——其它任务改派成返城时，映射后即落到本类。
    ///
    /// 【为何需要显式完成判定】
    /// 父类 TroopReturnCity 在任务完成时也改派 TroopReturnCity，映射后任务枚举不变，
    /// 无法靠"检测改派"识别，因此这里显式判定 IsMissionComplete。
    /// </summary>
    public class PlayerTroopReturnCity : TroopReturnCity
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopReturnCity; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;

            // 父类的 IsMissionComplete 依赖 TargetCity，故先绑定
            if (TargetCity == null || TargetCity.Id != troop.missionTarget)
                TargetCity = scenario.citySet.Get(troop.missionTarget);

            // 【委任】目标城已不适用（不再是归属城 / 已非己方）→ 直接交回玩家。
            // 不做"改去打该城 / 改回归属城"这类自动改派。
            if (IsMissionComplete)
            {
                PlayerTroopMissionHelper.ClearAndRelease(troop);
                return;
            }

            base.Prepare(troop, scenario);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：消灭指定敌方部队。目标被消灭 / 脱离敌对后交回玩家，
    /// 不会像势力 AI 那样自动追打别的敌人。
    /// </summary>
    public class PlayerTroopDestroyTroop : TroopDestroyTroop
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopDestroyTroop; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：驱逐边境上的敌方部队。
    /// </summary>
    public class PlayerTroopBanishTroop : TroopBanishTroop
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopBanishTroop; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：摧毁指定敌方建筑。建筑被毁后交回玩家。
    /// </summary>
    public class PlayerTroopDestroyBuilding : TroopDestroyBuilding
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopDestroyBuilding; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：占领敌方城池。城池易主（被己方或友军拿下）后交回玩家，
    /// 不会像势力 AI 那样自动改为"协防该城"或"前往该城"。
    /// </summary>
    public class PlayerTroopOccupyCity : TroopOccupyCity
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopOccupyCity; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：建造建筑。
    ///
    /// 【与势力 AI 版的关键差异】不做"连建"——玩家指派的是建这一座，
    /// 建完即交回玩家，不会自动去找下一个建址。
    /// </summary>
    public class PlayerTroopBuildBuilding : TroopBuildBuilding
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopBuildBuilding; } }

        /// <summary>
        /// 【委任】禁用连建：只建玩家指派的那一座。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 false（不寻找下一个建址）</returns>
        protected override bool TryFindNextSite(Scenario scenario)
        {
            return false;
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：修复己方建筑。建筑完工后交回玩家。
    /// </summary>
    public class PlayerTroopFixBuilding : TroopFixBuilding
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopFixBuilding; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }

    /// <summary>
    /// 委任：运输物资到指定城池。抵达后由父类的 DoAI 执行进城（物资归库）。
    /// </summary>
    public class PlayerTroopTransformGoodsToCity : TroopTransformGoodsToCity
    {
        public override MissionType MissionType { get { return MissionType.PlayerTroopTransformGoodsToCity; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            int missionBefore = troop.missionType;

            base.Prepare(troop, scenario);

            PlayerTroopMissionHelper.FinishIfMissionChanged(troop, missionBefore);
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (PlayerTroopMissionHelper.FinishIfComplete(this, troop))
                return true;

            return base.DoAI(troop, scenario);
        }
    }
}
