using LuaInterface;
using Sango.Game.Render.Model;
using Sango.Game.Render.UI;
using Sango.Loader;
using Sango.Render;
using UnityEngine;

namespace Sango.Game.Render
{
    public class TroopRender : ObjectRender
    {
        Troop Troop { get; set; }
        UnityEngine.UI.Text textInfo { get; set; }
        UGUIWindow HeadBar { get; set; }
        TroopModel TroopModel { get; set; }
        string headbarKey = "troops_head_bar";

        public TroopRender(Troop troop)
        {
            Owener = troop;
            Troop = troop;

            MapObject = MapObject.Create(Troop.Name + "队");
            MapObject.objType = Troop.TroopType.Id;
            MapObject.modelId = Troop.TroopType.Id;
            MapObject.modelAsset = Troop.TroopType.model;
            MapObject.transform.position = troop.cell.Position;
            MapObject.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            MapObject.transform.localScale = Vector3.one;
            MapObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
            MapObject.modelLoadedCallback = OnModelLoaded;
            MapObject.onModelVisibleChange = OnModelVisibleChange;
            MapRender.Instance.AddDynamic(MapObject);

            //GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("TroopName")) as GameObject;
            //obj.transform.SetParent(MapObject.transform, false);
            //obj.transform.localPosition = new Vector3(0, 20, 0);
            //UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
            //textInfo = text;
            UpdateInfo();
        }

        void OnModelLoaded(GameObject obj)
        {
            TroopModel = obj.GetComponent<TroopModel>();
            if (TroopModel != null)
            {
                TroopModel.Init(Troop);
            }
            TroopModel.SetSmokeShow(false);
            GameObject headBar = PoolManager.Create(GameRenderHelper.TroopHeadbarRes);
            if (headBar != null)
            {
                headBar.transform.SetParent(obj.transform, false);
                headBar.transform.localPosition = new Vector3(0, 25, 0);

                UGUIWindow uGUIWindow = headBar.GetComponent<UGUIWindow>();
                if (uGUIWindow != null)
                {
                    string windowName = System.IO.Path.GetFileNameWithoutExtension(GameRenderHelper.TroopHeadbarRes);
                    LuaTable table = Window.Instance.FindPeerTable(new Window.WindowInfo()
                    {
                        name = windowName,
                        packageName = null,
                        resName = null,
                        scriptName = null
                    });

                    if (table != null)
                    {
                        LuaFunction call = uGUIWindow.GetFunction("Create");
                        if (call != null)
                        {
                            LuaTable instance = call.Invoke<LuaTable>();
                            if (instance != null)
                            {
                                uGUIWindow.AttachScript(instance, true);
                                uGUIWindow.CallFunction("Init", Troop);
                            }
                        }
                    }
                    else
                    {
                        UITroopHeadbar uITroopHeadbar = uGUIWindow as UITroopHeadbar;
                        if (uITroopHeadbar != null)
                        {
                            uITroopHeadbar.Init(Troop);
                        }
                    }
                }
                HeadBar = uGUIWindow;
            }

        }

        void OnModelVisibleChange(MapObject obj)
        {
            if (obj.visible == false)
            {
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
                if (HeadBar.HasScript())
                {
                    HeadBar.CallFunction("UpdateState", Troop);
                }
                else
                {
                    UITroopHeadbar uITroopHeadbar = HeadBar as UITroopHeadbar;
                    if (uITroopHeadbar != null)
                    {
                        uITroopHeadbar.UpdateState(Troop);
                    }
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
                PoolManager.Recycle(HeadBar.gameObject);
                HeadBar = null;
            }
            base.Clear();
        }

        public void SetAniShow(int name)
        {
            if (TroopModel != null)
            {
                TroopModel.SetAniShow(name);
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

          public override void ShowDamage(int damage, int damageType)
        {
            if (HeadBar != null)
            {
                UITroopHeadbar uITroopHeadbar = HeadBar as UITroopHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.ShowDamage(damage, damageType);
                }
            }
        }
    }
}
