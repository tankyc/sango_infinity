namespace Sango.Core
{
    public enum MissionType : int
    {
        None = 0,
        TroopReturnCity,
        TroopMovetoCity,
        TroopDestroyTroop,
        TroopDestroyBuilding,
        TroopOccupyCity,
        TroopHarassCity,
        TroopBanishTroop,
        TroopProtectBuilding,
        TroopProtectTroop,
        TroopProtectCity,
        TroopBuildBuilding,
        TroopFixBuilding,
        TroopTransformGoodsToCity,
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


        TroopMovetoCell,
        TroopMovetoBuild,

        /// <summary>
        /// 部队补给:补给队自动为前线友军补充粮草 / 兵力,并尽量保持在友军后方规避威胁
        /// </summary>
        TroopSupplyTroop,

        /// <summary>
        /// 部队求援:状态不佳的部队主动接近友方补给队,并请求其对自己执行一次补给。
        /// 目标必须是运输队(补给队)。
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

    }
}
