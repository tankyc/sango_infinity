
using Sango.Core;
using Sango.Render.Model;
using Sango.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Render
{
    public class TroopRender : ObjectRender
    {
        Troop Troop { get; set; }
        UGUIWindow HeadBar { get; set; }
        TroopModel TroopModel { get; set; }

        bool headbarCreate;
        public TroopRender(Troop troop)
        {
            Owener = troop;
            Troop = troop;
            headbarCreate = true;
            MapObject = MapObject.Create(Troop.Name, "Troops");
            MapObject.objType = Troop.TroopType.Id;
            MapObject.modelId = Troop.TroopType.Id;
            MapObject.modelAsset = Troop.TroopType.model;
            MapObject.transform.position = troop.cell.Position;
            MapObject.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            MapObject.transform.localScale = Vector3.one;
            MapObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
            MapObject.onModelLoadedCallback = OnModelLoaded;
            MapObject.onModelVisibleChange = OnModelVisibleChange;
            MapObject.onModelAssetCheck = OnModelAssetCheck;
            MapRender.Instance.AddDynamic(MapObject);

            //GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("TroopName")) as GameObject;
            //obj.transform.SetParent(MapObject.transform, false);
            //obj.transform.localPosition = new Vector3(0, 20, 0);
            //UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
            //textInfo = text;
            UpdateInfo();
        }

        public TroopRender(Troop troop, bool headbar)
        {
            Owener = troop;
            Troop = troop;
            headbarCreate = headbar;
            MapObject = MapObject.Create(Troop.Name, "Troops");
            MapObject.objType = Troop.TroopType.Id;
            MapObject.modelId = Troop.TroopType.Id;
            MapObject.modelAsset = Troop.TroopType.model;
            MapObject.transform.position = troop.cell.Position;
            MapObject.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            MapObject.transform.localScale = Vector3.one;
            MapObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
            MapObject.onModelLoadedCallback = OnModelLoaded;
            MapObject.onModelVisibleChange = OnModelVisibleChange;
            MapObject.onModelAssetCheck = OnModelAssetCheck;
            MapRender.Instance.AddDynamic(MapObject);

            //GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("TroopName")) as GameObject;
            //obj.transform.SetParent(MapObject.transform, false);
            //obj.transform.localPosition = new Vector3(0, 20, 0);
            //UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
            //textInfo = text;
            UpdateInfo();
        }

        public string OnModelAssetCheck(int currentModelId, string currentAsset)
        {
            if (Troop.cell.TerrainType.isWater)
            {
                return Troop.WaterTroopType.model;
            }
            else
            {
                return Troop.TroopType.model;
            }
        }


        void OnModelLoaded(GameObject obj)
        {
            UnityTools.SetLayer(obj, obj.layer);

            TroopModel = obj.GetComponent<TroopModel>();
            if (TroopModel != null)
            {
                TroopModel.Init(Troop);
            }
            TroopModel.SetSmokeShow(false);

            if (HeadBar != null)
            {
                PoolManager.Recycle(HeadBar.gameObject);
                HeadBar = null;
            }

            Troop.buffManager.OnModelLoaded(obj);

            if (!headbarCreate) return;

            GameObject headBar = PoolManager.Create(GameRenderHelper.TroopHeadbarRes, (x) =>
            {
                GameSetting.Instance.UpdateTargetGameObject(x);


            });
            if (headBar != null)
            {
               
                headBar.transform.SetParent(obj.transform, false);
                headBar.transform.localPosition = Vector3.zero;
                BillboardUI billboardUI = headBar.GetComponent<BillboardUI>();
                if (billboardUI != null)
                {
                    //billboardUI.cacheOffset = new Vector3(0, 30, 0);
                    billboardUI.Update();
                }

                UGUIWindow uGUIWindow = headBar.GetComponent<UGUIWindow>();
                if (uGUIWindow != null)
                {
                    string windowName = System.IO.Path.GetFileNameWithoutExtension(GameRenderHelper.TroopHeadbarRes);
                    UITroopHeadbar uITroopHeadbar = uGUIWindow as UITroopHeadbar;
                    if (uITroopHeadbar != null)
                    {
                        uITroopHeadbar.Init(Troop);
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
                Troop.buffManager.OnModelClear();
                TroopModel = null;
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
            //textInfo.color = Troop.BelongForce.Flag.color;
            //textInfo.text = $"<{Troop.BelongForce.Name}>\n[{Troop.Name}队 - {Troop.TroopType.Name}]\n [{Troop.troops}] \n -{Troop.food}-";

            if (HeadBar != null)
            {
                UITroopHeadbar uITroopHeadbar = HeadBar as UITroopHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.UpdateState(Troop);
                }
            }

            if (TroopModel != null)
            {
                TroopModel.UpdateTroop(Troop);
            }
        }

        public override void UpdateRender()
        {
            base.UpdateRender();
            UpdateInfo();
        }

        public override void Clear()
        {
            TroopModel = null;
            if (HeadBar != null)
            {
                HeadBar.OnClose();
                PoolManager.Recycle(HeadBar.gameObject);
                HeadBar = null;
            }
            base.Clear();
        }

        public void SetAniShow(int name, bool onlyRenderAni = false)
        {
            if (TroopModel != null)
            {
                TroopModel.SetAniShow(name, onlyRenderAni);
            }
        }

        public void FaceTo(Vector3 dest)
        {
            if (MapObject != null)
            {
                Vector3 forward = dest - MapObject.transform.position;
                forward.y = 0;
                forward.Normalize();
                SetForward(forward);
            }
        }

        public void SetSmokeShow(bool b)
        {
            if (TroopModel != null)
            {
                TroopModel.SetSmokeShow(b);
            }
        }

        public override void ShowInfo(int damage, int damageType)
        {
            if (HeadBar != null)
            {
                UITroopHeadbar uITroopHeadbar = HeadBar as UITroopHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.ShowInfo(damage, damageType);
                }
            }
        }

        public override void ShowInfo(int damage, int damageType, bool isCrit)
        {
            if (HeadBar != null)
            {
                UITroopHeadbar uITroopHeadbar = HeadBar as UITroopHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.ShowInfo(damage, isCrit, damageType);
                }
            }
        }

        public override void ShowSkill(SkillInstance skill, bool isFail, bool isCritical)
        {
            if (HeadBar != null)
            {
                UITroopHeadbar uITroopHeadbar = HeadBar as UITroopHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.ShowSkill(skill, isFail, isCritical);
                }
            }
        }

        public void UpdateModelByCell(Cell destCell)
        {
            if (!IsVisible()) return;
            if (destCell.TerrainType.isWater)
            {
                MapObject.ChangeModel(Troop.WaterTroopType.model);
            }
            else
            {
                MapObject.ChangeModel(Troop.LandTroopType.model);
            }
        }

        public override void SetFlash(bool b)
        {
            TroopModel?.SetFlash(b);
        }

    }
}
