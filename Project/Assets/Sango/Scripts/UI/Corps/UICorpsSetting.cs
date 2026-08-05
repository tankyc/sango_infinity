using Sango.Core.Player;
using UnityEngine.UI;
using System.Collections.Generic;

using Sango.Core;
using System.Text;

namespace Sango.UI
{
    /// <summary>
    /// 军团菜单UI类
    /// </summary>
    public class UICorpsSetting : UGUIWindow
    {
        /// <summary>
        /// 窗口标题
        /// </summary>
        public Text windowTitle;

        /// <summary>
        /// 都市数量显示文本
        /// </summary>
        public UITextField cityCountText;

        /// <summary>
        /// 军团方针显示文本
        /// </summary>
        public UITextField policyText;

        /// <summary>
        /// 军团名字
        /// </summary>
        public UITextField corpsNameText;

        /// <summary>
        /// 攻略对象显示文本
        /// </summary>
        public UITextField targetText;

        public UIPersonItem commander;
        public UIStatusItem commanderStatus;
        public Button commanderButton;
        public Button sureButton;
        public Button targetButton;

        public System.Action onSure;
        public System.Action onCancel;

        public List<ObjectSortTitle> forceSelectTitleList;

        /// <summary>
        /// 当前选中的军团
        /// </summary>
        Corps targetCorps;
        Force targetForce;
        public List<City> targetCityList = new List<City>();
        List<Person> targetPersonList = new List<Person>();
        List<City> validCityList = new List<City>();
        List<Person> validPersonList = new List<Person>();

        public static List<ObjectSortTitle> PersonSortList = new List<ObjectSortTitle>
        {
            PersonSortFunction.SortByName,
            PersonSortFunction.SortByBelongCity,
            PersonSortFunction.SortByState,
            PersonSortFunction.SortByLoyalty,
            PersonSortFunction.SortByMerit,
            PersonSortFunction.SortByLevel,
        };

        /// <summary>
        /// 窗口显示时调用
        /// </summary>
        public override void OnOpen(params object[] objects)
        {
            forceSelectTitleList = new List<ObjectSortTitle>()
            {
                ForceSortFunction.SortByName,
                ForceSortFunction.SortByLeader,
            };

            targetCorps = objects[0] as Corps;
            targetForce = targetCorps.BelongForce;
            if (objects.Length > 1)
                windowTitle.text = objects[1] as string;
            if (objects.Length > 2)
                onSure = objects[2] as System.Action;
            if (objects.Length > 3)
                onCancel = objects[3] as System.Action;
            targetCityList.Clear();
            validCityList.Clear();
            targetPersonList.Clear();
            validPersonList.Clear();
            targetForce.ForEachCity(x =>
            {
                if (x.BelongCorps.number == 1 && x != targetForce.CapitalCity)
                    validCityList.Add(x);
            });
            if (targetCorps.Id > 0)
            {
                targetPersonList.Add(targetCorps.Comander);
                targetCityList.AddRange(targetCorps.inti_cities);
                validCityList.AddRange(targetCorps.inti_cities);
            }

            if (targetCityList.Count > 0)
            {
                foreach (City c in targetCityList)
                {
                    c.allPersons.ForEach(x =>
                    {
                        if (!x.IsPrisoner)
                        {
                            validPersonList.Add(x);
                        }
                    });
                }
            }

            UpdateContent();
        }

        /// <summary>
        /// 更新内容
        /// </summary>
        public void UpdateContent()
        {
            if (targetCorps == null) return;
            corpsNameText.text = targetCorps.Name;
            commander.SetPerson(targetCorps.Comander);
            commanderStatus.SetPerson(targetCorps.Comander);
            commanderButton.interactable = targetCityList.Count > 0;
            int cityCount = targetCityList.Count;
            if (cityCount == 0)
            {
                cityCountText.text = $"{cityCount}都市";
            }
            else
            {
                StringBuilder stringBuilder = new StringBuilder();
                for (int i = 0; i < cityCount; i++)
                {
                    City c = targetCityList[i];
                    stringBuilder.Append(c.Name);
                    if (i < cityCount - 2)
                        stringBuilder.Append("，");
                }
                if (cityCount >= 6)
                {
                    stringBuilder.Append($"等{cityCount}都市");
                }
                cityCountText.text = stringBuilder.ToString();
            }
            policyText.text = GetPolicyText(targetCorps.appoint);
            sureButton.interactable = targetCityList.Count > 0 && targetPersonList.Count > 0;
            targetButton.interactable = targetCorps.appoint == (int)Corps.AppointType.OccupyCity || targetCorps.appoint == (int)Corps.AppointType.DestroyForce;
            switch (targetCorps.appoint)
            {
                case 1:

                    targetText.text = "";
                    if (targetCorps.appoint_target > 0)
                    {
                        Force force = Scenario.Cur.forceSet.Get(targetCorps.appoint_target);
                        if (force != null)
                            targetText.text += force.Name;
                    }

                    break;
                case 2:
                    targetText.text = "";
                    if (targetCorps.appoint_target > 0)
                    {
                        City force = Scenario.Cur.citySet.Get(targetCorps.appoint_target);
                        if (force != null)
                            targetText.text += force.Name;
                    }
                    break;
                default:
                    targetText.text = "";
                    break;
            }
        }

        /// <summary>
        /// 获取军团方针文本
        /// </summary>
        /// <param name="policyType">方针类型</param>
        /// <returns>方针文本</returns>
        private string GetPolicyText(int policyType)
        {
            switch (policyType)
            {
                case 0: return "委任";
                case 1: return "攻略势力";
                case 2: return "攻略都市";
                default: return "委任";
            }
        }

        /// <summary>
        /// 编制都市按钮点击事件
        /// </summary>
        public void OnOrganizeCityButtonClick()
        {
            GameSystem.GetSystem<CitySelectSystem>().Start(validCityList,
             targetCityList, validCityList.Count, OnCityChange, CitySortFunction.DefaultSortList, "军团城池选择");
        }

        /// <summary>
        /// 设置都督按钮点击事件
        /// </summary>
        public void OnSelectCommanderButtonClick()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(validPersonList,
             targetPersonList, 1, OnCommanderChange, PersonSortList, "都督选择");
        }

        /// <summary>
        /// 军团方针按钮点击事件
        /// </summary>
        public void OnPolicyButtonClick()
        {

            PlayerChoice.ChoiceData[] choiceDatas = new PlayerChoice.ChoiceData[]
            {
                new PlayerChoice.ChoiceData()
                {
                    lab = $"攻略势力",
                    call = () =>
                    {
                        targetCorps.appoint = 1;
                        targetCorps.appoint_target = 0;
                        UpdateContent();
                    }
                },
                new PlayerChoice.ChoiceData()
                {
                    lab = $"攻略都市",
                    call = () =>
                    {
                        targetCorps.appoint = 2;
                        targetCorps.appoint_target = 0;
                        UpdateContent();
                    }
                },
                new PlayerChoice.ChoiceData()
                {
                    lab = $"委任",
                    call = () =>
                    {
                        targetCorps.appoint = 3;
                        targetCorps.appoint_target = 0;
                        UpdateContent();
                    }
                }
            };
            GameSystem.GetSystem<PlayerChoice>().Start(choiceDatas);
        }
        public void OnCityChange(List<City> cities)
        {
            targetCityList = cities;
            validPersonList.Clear();
            if (targetCityList.Count > 0)
            {
                foreach (City c in targetCityList)
                {
                    c.allPersons.ForEach(x =>
                    {
                        if (!x.IsPrisoner)
                        {
                            validPersonList.Add(x);
                        }
                    });
                }
            }
            UpdateContent();
        }

        public void OnCommanderChange(List<Person> people)
        {
            targetPersonList = people;
            if (targetPersonList.Count > 0)
            {
                targetCorps.Comander = targetPersonList[0];
            }
            UpdateContent();
        }

        /// <summary>
        /// 委任内容按钮点击事件
        /// </summary>
        public void OnAppointmentButtonClick()
        {
            // 此处应该调用委任内容设置的逻辑
            Window.Instance.Open("window_corps_appoint", targetCorps, this);
        }

        /// <summary>
        /// 军团方针按钮点击事件
        /// </summary>
        public void OnTargetClick()
        {
            if (targetCorps.appoint == (int)Corps.AppointType.DestroyForce)
            {
                List<Force> selectForces = new List<Force>();
                if (targetCorps.appoint_target > 0)
                {
                    Force force = Scenario.Cur.forceSet.Get(targetCorps.appoint_target);
                    if (force != null)
                        selectForces.Add(force);
                }
                GameSystem.GetSystem<ForceSelectSystem>().Start(targetForce.NeighborForceList,
                        selectForces, 1, (forceList) =>
                        {
                            if (forceList.Count > 0)
                            {
                                targetCorps.appoint_target = forceList[0].Id;
                                targetText.text = forceList[0].Name;
                            }
                        }, forceSelectTitleList, "选择攻略势力");
            }
            else if (targetCorps.appoint == (int)Corps.AppointType.OccupyCity)
            {
                List<City> selectCity = new List<City>();
                if (targetCorps.appoint_target > 0)
                {
                    City city = Scenario.Cur.citySet.Get(targetCorps.appoint_target);
                    if (city != null)
                        selectCity.Add(city);
                }
                GameSystem.GetSystem<CitySelectSystem>().Start(targetForce.NeighborCityList,
                        selectCity, 1, (cList) =>
                        {
                            if (cList.Count > 0)
                            {
                                targetCorps.appoint_target = cList[0].Id;
                                targetText.text = cList[0].Name;
                            }
                        }, CitySortFunction.DefaultSortList, "选择攻略都市");
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        public void OnCancel()
        {
            Close();
            onCancel?.Invoke();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        public void OnSure()
        {
            targetCorps.inti_cities = targetCityList;
            Close();
            onSure?.Invoke();
        }
    }
}
