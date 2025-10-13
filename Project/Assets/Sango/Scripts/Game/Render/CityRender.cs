using LuaInterface;
using Sango.Game.Render.Model;
using Sango.Game.Render.UI;
using Sango.Loader;
using Sango.Render;
using UnityEngine;

namespace Sango.Game.Render
{
    public class CityRender : ObjectRender
    {
        City City { get; set; }
        CityModel CityModel { get; set; }
        UGUIWindow HeadBar { get; set; }

        UnityEngine.UI.Text textInfo { get; set; }
        string headbarKey = "city_head_bar";

        public CityRender(City city)
        {
            Owener = city;
            City = city;
            MapObject = MapObject.Create(city.Name);
            MapObject.objType = city.BuildingType.kind;
            MapObject.modelId = city.Id;
            MapObject.modelAsset = GameRenderHelper.GetCityModelAsset(city.model);
            MapObject.transform.position = city.CenterCell.Position;
            MapObject.transform.rotation = Quaternion.Euler(new Vector3(0, city.rot, 0));
            MapObject.transform.localScale = Vector3.one;
            MapObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
            MapObject.modelLoadedCallback = OnModelLoaded;
            MapObject.onModelVisibleChange = OnModelVisibleChange;
            MapRender.Instance.AddStatic(MapObject);


            //GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("CityName")) as GameObject;
            //obj.transform.SetParent(MapObject.transform, false);
            //obj.transform.localPosition = new Vector3(0, 20, 0);
            //UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
            //textInfo = text;
            UpdateInfo();
        }

        public void OnModelLoaded(GameObject obj)
        {
            CityModel = MapObject.GetComponentInChildren<CityModel>(true);
            if (CityModel != null)
            {
                CityModel.Init(City);
            }

            GameObject headBar = PoolManager.Create(GameRenderHelper.CityHeadbarRes);
            if (headBar != null)
            {
                headBar.transform.SetParent(obj.transform, false);
                headBar.transform.localPosition = new Vector3(0, 25, 0);

                UGUIWindow uGUIWindow = headBar.GetComponent<UGUIWindow>();
                if (uGUIWindow != null)
                {
                    string windowName = System.IO.Path.GetFileNameWithoutExtension(GameRenderHelper.CityHeadbarRes);
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
                                uGUIWindow.CallFunction("Init", City);
                            }
                        }
                    }
                    else
                    {
                        UICityHeadbar uITroopHeadbar = uGUIWindow as UICityHeadbar;
                        if (uITroopHeadbar != null)
                        {
                            uITroopHeadbar.Init(City);
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
                CityModel = null;
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
            //if (City.BelongForce != null)
            //{
            //    textInfo.color = City.BelongForce.Flag.color;
            //}
            //else
            //{
            //    textInfo.color = Color.white;
            //}
            //string cityInfo = $"{City.BelongForce?.Name}.{City.Name}[耐:{City.durability}](人:{City.allPersons.Count} 闲:{City.freePersons.Count})\n[商:{City.commerce},农:{City.agriculture},治:{City.security},建:{City.allBuildings.Count}/{City.CityLevelType.insideSlot + City.CityLevelType.outsideSlot}]\n[兵:{City.troops}]\n<金:{City.gold}+{City.totalGainGold}>\n<粮:{City.food}+{City.totalGainFood}>";
            //if (City.IsBorderCity)
            //    cityInfo = $"*{cityInfo}";
            //textInfo.text = cityInfo;
            if (HeadBar != null)
            {
                if (HeadBar.HasScript())
                {
                    HeadBar.CallFunction("UpdateState", City);
                }
                else
                {
                    UICityHeadbar uITroopHeadbar = HeadBar as UICityHeadbar;
                    if (uITroopHeadbar != null)
                    {
                        uITroopHeadbar.UpdateState(City);
                    }
                }
            }

            if (CityModel != null)
            {
                CityModel.Init(City);
            }
        }

        public override void UpdateRender()
        {
            base.UpdateRender();
            UpdateInfo();
        }

        public override void Clear()
        {
            CityModel = null;
            if (HeadBar != null)
            {
                PoolManager.Recycle(HeadBar.gameObject);
                HeadBar = null;
            }
            base.Clear();
        }

           public override void ShowDamage(int damage, int damageType)
        {
            if (HeadBar != null)
            {
                UICityHeadbar uITroopHeadbar = HeadBar as UICityHeadbar;
                if (uITroopHeadbar != null)
                {
                    uITroopHeadbar.ShowDamage(damage, damageType);
                }
            }
        }
    }
}
