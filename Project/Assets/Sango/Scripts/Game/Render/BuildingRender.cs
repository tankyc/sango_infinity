
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
            MapObject = MapObject.Create($"{Building.BelongCity.Name}-{Building.Name}");
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
                HeadBar = uGUIWindow;
            }

        }

        public override void OnModelVisibleChange(MapObject obj)
        {
            base.OnModelVisibleChange(obj);

            if (obj.visible == false)
            {
                BuildingModel = null;
                if (HeadBar != null)
                {
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
                UIBuildingHeadbar uITroopHeadbar = HeadBar as UIBuildingHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.UpdateState(Building);
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
