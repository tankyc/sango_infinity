namespace Sango.Core
{
    public enum MissionType : int
    {
        None = 0,

        // ==================== 部队任务：与原版 UnitOrder 的对应关系 ====================
        //
        // 原版命令表（s11_enum_scen.h:970-988）：
        //   Wait(0) Move(1) Build(2) Attack(3) Retreat(4) Expel(5) Escort(6)
        //   Siege(7) Repair(8) Subjugate(9) Supply(0xa) ComBuild(0xb) None(0xc)
        //
        // ⚠ 凡标「原版无对应」的，都是本项目自创任务，**严格对齐模式下不会被
        //   UpdateAIOrder 生成**（原版改派表里没有它们），只能由玩家指令或旧 AI 路径触发。
        // ⚠ 凡标「未实现」的，行为类是空壳，严格对齐下若被选中会出现部队原地不动。

        /// <summary>
        /// 返城（回到所属据点后解散）。
        /// 【原版对应】无独立 order —— 原版 Retreat(4) 就是把命令目标设成所属据点
        /// （unit_retreat，s11_game_ai_unit.cpp:378-399），到位后由据点收编。
        /// 本项目拆成独立任务，语义等价。
        /// </summary>
        TroopReturnCity,

        /// <summary>
        /// 移动到指定城。
        /// 【原版对应】Move(1)（目标是建筑）；同时承担原版 Retreat(4) 的"撤退途中"语义
        /// —— 两者在原版是两个 order（Move / Retreat），本项目合并为一个任务，
        /// 因此 <see cref="Troop.UpdateAIOrder"/> 判断"是否已在移动"时两种意图都要放行。
        /// </summary>
        TroopMovetoCity,

        /// <summary>
        /// 攻击敌方部队。
        /// 【原版对应】Attack(3)（目标是 Unit）。
        /// </summary>
        TroopDestroyTroop,

        /// <summary>
        /// 攻击敌方建筑。
        /// 【原版对应】Attack(3)（目标是 Building），与原版同一个 order 的不同目标类型。
        /// </summary>
        TroopDestroyBuilding,

        /// <summary>
        /// 占领敌方据点。
        /// 【原版对应】Subjugate(9)。
        /// </summary>
        TroopOccupyCity,

        /// <summary>
        /// 骚扰敌方城池。
        /// 【原版无对应】原版没有"骚扰"这个 order。
        /// 【本项目状态】**死枚举**：<see cref="TroopMissionBehaviour.Create"/> 没有为它映射行为类，
        /// 一旦被设置会拿不到行为对象（严格对齐下会走"命令执行不出来 → 撤退"兜底）。
        /// </summary>
        TroopHarassCity,

        /// <summary>
        /// 驱逐目标部队出境（打出对方领地即完成）。
        /// 【原版无对应】原版 Escort(6) 在 run_order 里直接 <c>result = false</c>
        /// （s11_sys_unit_order.cpp:56-58），是死代码，没有"驱逐"语义。
        /// 本项目的驱逐是自创任务，直接继承 <see cref="TroopDestroyTroop"/> 的攻击评分。
        /// </summary>
        TroopBanishTroop,

        /// <summary>
        /// 保护建筑。**已停用**（保留枚举值以兼容旧存档）。
        ///
        /// 【原版对应】Expel(5)（驱逐靠近的敌军）。
        /// 【为什么停用】行为类 `TroopProtectBuilding` 只有空壳（`DoAI` 直接 return true、
        /// `IsMissionComplete` 抛 `NotImplementedException`），部队会原地不动。
        /// Expel 的语义已由 <see cref="TroopProtectCity"/> 承担（守城即驱逐来犯之敌）。
        /// **`Troop.SetMission` 会把本任务统一改判为 `TroopProtectCity`**，因此不会产生死任务。
        /// </summary>
        TroopProtectBuilding,

        /// <summary>
        /// 护卫友军部队。**已停用**（保留枚举值以兼容旧存档）。
        ///
        /// 【原版对应】Escort(6) —— 原版本身就是死代码（`run_order` 直接返回 false）。
        /// **`Troop.SetMission` 会把本任务统一改判为 `TroopProtectCity`**。
        /// </summary>
        TroopProtectTroop,

        /// <summary>
        /// 守城。
        /// 【原版对应】Expel(5) —— 原版 Defense 意图下改派的就是 Expel
        /// （s11_game_ai_unit.cpp:249-261），即"在自己的据点附近驱逐来犯之敌"。
        /// </summary>
        TroopProtectCity,

        /// <summary>
        /// 建造建筑。
        /// 【原版对应】Build(2)（随军工程队）+ ComBuild(0xb)（设置防御设施）。
        /// 原版两个 order 的差别在于建的是普通设施还是防御设施，本项目用
        /// <c>missionTargetCell</c> + 建筑类型区分，合并为一个任务。
        /// </summary>
        TroopBuildBuilding,

        /// <summary>
        /// 修补建筑。
        /// 【原版对应】Repair(8)。
        /// </summary>
        TroopFixBuilding,

        /// <summary>
        /// 物资运输（把资源运到目标城）。
        /// 【原版对应】Supply(0xa) 的"运输"分支。
        /// </summary>
        TroopTransformGoodsToCity,

        /// <summary>
        /// 待命。
        /// 【原版对应】Wait(0)。
        /// </summary>
        TroopStay,

        PersonBuild,
        PersonCreateBoat,
        PersonCreateMachine,
        PersonWork,
        PersonInTroop,
        PersonResearch,

        /// <summary>
        /// 移动
        /// </summary>
        PersonTransform,

        /// <summary>
        /// 返回所在城市
        /// </summary>
        PersonReturn,

        /// <summary>
        /// 招募
        /// </summary>
        PersonRecruitPerson,

        /// <summary>
        /// 外交任务
        /// </summary>
        PersonDiplomacy,


        /// <summary>
        /// 移动到指定坐标。
        /// 【原版对应】Move(1)（<c>order_target_type = Pos</c>）。
        /// </summary>
        TroopMovetoCell,

        /// <summary>
        /// 移动到指定建筑。
        /// 【原版无对应】原版 Move 只记坐标（<c>order_target_pos</c>），不记"要去的建筑对象"。
        /// 本项目多存一个建筑引用，属于实现差异，不改变行为。
        /// </summary>
        TroopMovetoBuild,

        /// <summary>
        /// 部队补给:补给队自动为前线友军补充粮草 / 兵力,并尽量保持在友军后方规避威胁。
        /// 【原版对应】Supply(0xa) 的"给友军补给"分支。
        /// </summary>
        TroopSupplyTroop,

        /// <summary>
        /// 部队求援:状态不佳的部队主动接近友方补给队,并请求其对自己执行一次补给。
        /// 目标必须是运输队(补给队)。
        /// 【原版无对应】原版只有**势力级**宏观求援（<c>request_force</c>，
        /// s11_game_ai_unit.cpp:114-123），没有"部队主动找补给队"这一层。
        /// 这是本项目自创任务，严格对齐模式下不会被 UpdateAIOrder 生成，
        /// 只能由旧 AI 路径（AIConfig.useStrictOpen311UnitAI = false）或玩家指令触发。
        /// </summary>
        TroopAskSupply,

        // ==================== 委任部队（玩家第一军团）专用任务 ====================
        //
        // 【为什么要单独一套】
        // 上面的 TroopXXX 面向势力 AI，其行为类内部带有大量"战场态势判断"与"任务自动改派"
        // （主动撤退、求援、随军增筑、自动寻找下一个目标等）。而玩家对自家第一军团部队的
        // "委任"语义是**只把指定任务执行掉**，不应有任何自作主张。
        //
        // 因此为委任部队单独设一套枚举 + 单独一套行为类（继承势力 AI 版，仅覆写"准备"阶段），
        // 使两套逻辑在物理上彻底分开，日后可以各自演化而互不影响。
        //
        // 【重要】以下枚举值必须**追加在末尾**：missionType 以 int 落盘进存档，
        // 若插入到中间会与旧存档的既有数值错位。
        //
        // 【映射】Troop.SetMission 会在部队属于玩家第一军团时自动把 TroopXXX 映射为
        // 对应的 PlayerTroopXXX（见 TroopMissionBehaviour.ToPlayerMission），
        // 因此所有派发点（UI 指令 / 行为类内部改派）都无需改动。

        /// <summary>委任：返回所在城市（由其它委任任务完成后的收尾改派而来）</summary>
        PlayerTroopReturnCity,

        /// <summary>委任：移动到己方城池</summary>
        PlayerTroopMovetoCity,

        /// <summary>委任：消灭指定敌方部队</summary>
        PlayerTroopDestroyTroop,

        /// <summary>委任：摧毁指定敌方建筑</summary>
        PlayerTroopDestroyBuilding,

        /// <summary>委任：占领敌方城池</summary>
        PlayerTroopOccupyCity,

        /// <summary>委任：驱逐边境上的敌方部队</summary>
        PlayerTroopBanishTroop,

        /// <summary>委任：建造建筑</summary>
        PlayerTroopBuildBuilding,

        /// <summary>委任：修复建筑</summary>
        PlayerTroopFixBuilding,

        /// <summary>委任：运输物资到城池</summary>
        PlayerTroopTransformGoodsToCity,

        /// <summary>委任：移动到指定格子</summary>
        PlayerTroopMovetoCell,

        /// <summary>
        /// 城市计略任务:武将被派往目标据点,抵达后再结算二虎竞食/流言等计略效果。
        /// 必须追加在枚举末尾,插在中间会整体移位并破坏旧存档里的 missionType 数值。
        /// </summary>
        PersonCityStrategy,

        /// <summary>
        /// 能力培育任务:武将闭关 3 旬研习能力,到期由 <see cref="AbilityStudySystem.FinishStudyMission"/> 结算
        /// （missionParams1 = 能力 Id）。对齐 Open311 的 LearnBaseStat / LearnHeishuTekisei / LearnSkill
        /// 三个 person order（person_set_order_timer(actor, 3) + action_done）。
        /// 必须追加在枚举末尾,插在中间会整体移位并破坏旧存档里的 missionType 数值。
        /// </summary>
        PersonStudyAbility,

        /// <summary>
        /// 异族 / 贼"烧城"任务：攻陷目标城后继续攻击白城直到夷平，再找下一座城。
        /// 【原版对应】Subjugate(9) 的异族专用分支——subjugate 循环
        /// （s11_sys_unit_order.cpp:683-711）+ update_bandits（s11_sys_event_kanraku.cpp:99-152）。
        /// 普通势力的占领走 <see cref="TroopOccupyCity"/>；异族不占地，改走本任务烧城。
        /// 必须追加在枚举末尾（missionType 以 int 落盘，插中间会破坏旧存档数值）。
        /// </summary>
        TroopBanditRazeCity,
    }
}
