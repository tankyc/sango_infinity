
using Sango.Render.Model;
using Sango.UI;
using Sango.Core;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Sango.Render
{
    public class BuildingRender : ObjectRender
    {
        Building Building { get; set; }
        BuildingModel BuildingModel { get; set; }
        UGUIWindow HeadBar { get; set; }
        bool isComplate = false;
        bool isUpgrading = false;

        protected struct LinkData
        {
            public Building start;
            public Building end;
            public GameObject model;
        }

        protected List<LinkData> linkDatas;

        public BuildingRender()
        {

        }

        public BuildingRender(Building building)
        {
            Owener = building;
            Building = building;
            // 【必须判空】异族 / 贼的根据地（建筑类型 15/16）不属于任何城，BelongCity 为 null。
            // 原版这类建筑的单位是「势力」（s11_game_month.cpp:1663 的 create_building(facility, pos, force_id)），
            // 而本项目 BelongCity 是"所属城"语义（用于 city.allBuildings 登记），
            // 所以这类建筑只能用势力名兜底，不能直接解引用 BelongCity。
            string ownerName = Building.BelongCity?.Name ?? Building.BelongForce?.Name ?? "据点";
            MapObject = MapObject.Create($"{ownerName}-{Building.Name}");
            MapObject.objType = Building.BuildingType.kind;
            MapObject.modelId = Building.BuildingType.Id;
            isComplate = building.isComplate;
            isUpgrading = building.isUpgrading;
            if (!building.isComplate || building.isUpgrading)
                MapObject.modelAsset = building.BuildingType.modelCreate;
            else
                MapObject.modelAsset = building.BuildingType.model;

            MapObject.transform.position = Building.CenterCell.Position;
            MapObject.transform.rotation = Quaternion.Euler(new Vector3(0, Building.rot * Mathf.Rad2Deg, 0));
            MapObject.transform.localScale = Vector3.one;
            MapObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
            MapObject.onModelLoadedCallback = OnModelLoaded;
            MapObject.onModelVisibleChange = OnModelVisibleChange;
            MapRender.Instance.AddStatic(MapObject);
            UpdateInfo();
        }

        public void Init()
        {

        }


        public void OnModelLoaded(GameObject obj)
        {
            BuildingModel = MapObject.GetComponentInChildren<BuildingModel>(true);
            if (BuildingModel != null)
            {
                BuildingModel.Init(Building);
            }
            BuildingModel.SetFlash(false);
            if (HeadBar != null)
            {
                MapHeadbarManager.Detach(HeadBar.gameObject);
                PoolManager.Recycle(HeadBar.gameObject);
                HeadBar = null;
            }

            GameObject headBar = PoolManager.Create(GameRenderHelper.BuildingHeadbarRes, (x) =>
            {
                GameSetting.Instance.UpdateTargetGameObject(x);


            });
            if (headBar != null)
            {
                headBar.transform.SetParent(obj.transform, false);
                headBar.transform.localPosition = new Vector3(0, 25, 0);

                UGUIWindow uGUIWindow = headBar.GetComponent<UGUIWindow>();
                if (uGUIWindow != null)
                {
                    string windowName = System.IO.Path.GetFileNameWithoutExtension(GameRenderHelper.BuildingHeadbarRes);

                    UIBuildingHeadbar uITroopHeadbar = uGUIWindow as UIBuildingHeadbar;
                    if (uITroopHeadbar != null)
                    {
                        uITroopHeadbar.Init(Building);
                    }
                }
                // 集中驱动：见 MapHeadbarManager 的类注释
                MapHeadbarManager.Attach(headBar);
                HeadBar = uGUIWindow;

                // 立即套用显隐规则，避免"满耐久且没有工人"的空条闪一帧
                bool show = NeedHeadBar(Building);
                if (headBar.activeSelf != show)
                    headBar.SetActive(show);
            }

        }

        /// <summary>
        /// 是否需要显示头顶条。
        ///
        /// 【规则】普通设施在"耐久满 + 没有工人 + 不在建造/升级中"时整条隐藏，
        /// 否则地图上会铺满一排"满血且无事可做"的空条，既挡地形，又要不断重算各自的
        /// World Space Canvas 批次。
        ///
        /// 以下任一情况必须显示（也就是隐藏后能自动恢复显示的触发条件）：
        ///   · 建造中 / 升级中 —— 玩家需要看进度，此时耐久本来也不满；
        ///   · 有工人入驻（内政设施）或有建造者 —— 需要看到人；
        ///   · 耐久未满 —— 说明被打过或还没修好，血条必须可见。
        ///
        /// 城 / 港 / 关 各有自己的 Headbar（UICityHeadbar / UIPortHeadbar / UIGateHeadbar），
        /// 不经过本方法，这里保留判断只是防御脏数据。
        /// </summary>
        /// <param name="building">目标建筑</param>
        /// <returns>true 需要显示</returns>
        public static bool NeedHeadBar(Building building)
        {
            if (building == null)
                return false;

            // 城 / 港 / 关 交给各自的 Headbar，绝不在普通设施这一层隐藏
            if (building.IsCityBase())
                return true;

            // 建造中 / 升级中：需要进度感
            if (!building.isComplate || building.isUpgrading)
                return true;

            // 有工人或有建造者：需要看到人
            if (building.Workers != null && building.Workers.Count > 0)
                return true;
            if (building.Builder != null && building.Builder.Count > 0)
                return true;

            // 耐久未满：必须能看见血条；满耐久 → 整条隐藏
            return building.durability < building.DurabilityLimit;
        }

        public override void OnModelVisibleChange(MapObject obj)
        {
            base.OnModelVisibleChange(obj);

            if (obj.visible == false)
            {
                BuildingModel = null;
                if (HeadBar != null)
                {
                    MapHeadbarManager.Detach(HeadBar.gameObject);
                    PoolManager.Recycle(HeadBar.gameObject);
                    HeadBar = null;
                }
                return;
            }
        }

        public void UpdateInfo()
        {
            if (HeadBar != null)
            {
                // 满耐久且没有工人的普通设施整条隐藏；有工人 / 掉耐久 / 建造升级中会自动恢复（见 NeedHeadBar）
                bool show = NeedHeadBar(Building);
                if (HeadBar.gameObject.activeSelf != show)
                    HeadBar.gameObject.SetActive(show);

                if (show)
                {
                    UIBuildingHeadbar uITroopHeadbar = HeadBar as UIBuildingHeadbar;
                    if (uITroopHeadbar != null)
                    {
                        uITroopHeadbar.UpdateState(Building);
                    }
                }
            }


            //if (Building.isComplte || Building.isUpgrading)
            //{
            //    MapObject.ChangeModel(Building.BuildingType.modelCreate);
            //}

            //if (BuildingModel != null)
            //{
            //    BuildingModel.Init(Building);
            //}
            if (isComplate == false && Building.isComplate)
            {
                isComplate = Building.isComplate;
                MapObject.ChangeModel(Building.BuildingType.model);
            }

            if (isUpgrading != Building.isUpgrading)
            {
                isUpgrading = Building.isUpgrading;
                if (isUpgrading)
                    MapObject.ChangeModel(Building.BuildingType.modelCreate);
                else
                    MapObject.ChangeModel(Building.BuildingType.model);
            }

            if (BuildingModel != null)
            {
                if (BuildingModel.maxLevelEffect != null)
                {
                    BuildingModel.maxLevelEffect.SetActive(Building.BuildingType.nextId == 0);
                }
            }

        }

        public override void UpdateRender()
        {
            base.UpdateRender();
            UpdateInfo();
        }

        public override void Clear()
        {
            BuildingModel = null;
            if (HeadBar != null)
            {
                MapHeadbarManager.Detach(HeadBar.gameObject);
                PoolManager.Recycle(HeadBar.gameObject);
                HeadBar = null;
            }
            base.Clear();
        }

        public override void ShowInfo(int damage, int damageType)
        {
            if (HeadBar != null)
            {
                UIBuildingHeadbar uITroopHeadbar = HeadBar as UIBuildingHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.ShowInfo(damage, damageType);
                }
            }
        }
        public override void SetFlash(bool b)
        {
            if(BuildingModel != null)
                BuildingModel.SetFlash(b);
        }
    }
}
