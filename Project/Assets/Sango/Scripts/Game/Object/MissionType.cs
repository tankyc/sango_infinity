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

    }
}
