namespace Sango.Game
{
    public enum MissionType : int
    {
        None = 0,
        ReturnCity,
        MovetoCity,
        DestroyTroop,
        DestroyBuilding,
        OccupyCity,
        BanishTroop,
        ProtectBuilding,
        ProtectTroop,
        ProtectCity,
        PersonBuild,
        PersonWork,
        PersonInTroop,

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
        PersonRecruitPerson

    }
}
