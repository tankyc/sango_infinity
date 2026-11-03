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
                if (x.BelongCorps.IsCaptainCorps && x != targetForce.CapitalCity)
                    validCityList.Add(x);
            });
            if (targetCorps.Id > 0)
            {
                targetPersonList.Add(targetCorps.mComander);
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
            commander.SetPerson(targetCorps.mComander);
            commanderStatus.SetPerson(targetCorps.mComander);
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

            CheckSure();
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
        /// 取本势力第 1 军团当前所辖的城数（用于"主军团必须保留 ≥2 城"的判定）。
        /// 找不到第 1 军团时返回 -1，调用方需按"数据不可信"处理，不要据此禁用按钮。
        /// </summary>
        /// <returns>第 1 军团的城数；无法确定时返回 -1</returns>
        int GetFirstCorpsCityCount()
        {
            if (Scenario.Cur == null || targetCorps == null || targetCorps.BelongForce == null)
                return -1;

            Corps firstCorps = null;
            Scenario.Cur.corpsSet.ForEach(x =>
            {
                if (x != null && x.IsAlive
                    && x.BelongForce == targetCorps.BelongForce && x.number == 1)
                    firstCorps = x;
            });
            if (firstCorps == null)
                return -1;

            int count = 0;
            Scenario.Cur.citySet.ForEach(x =>
            {
                if (x != null && x.IsAlive
                    && x.BelongForce == targetCorps.BelongForce && x.BelongCorps == firstCorps)
                    count++;
            });
            return count;
        }

        /// <summary>
        /// 编制都市按钮点击事件
        /// </summary>
        public void OnOrganizeCityButtonClick()
        {
            // 如果首都不是城池,则需要留下一个城市不能编入
            int count = validCityList.Count;
            if (!targetForce.CapitalCity.IsCity())
            {
                count--;
            }

            // 【新建军团：从源头限制可选城数】
            // 主军团至少要留 1 城（不能把第 1 军团拆成 0 城）。
            // 旧实现只在 CheckSure 里做"事后否决"：玩家可以一直选到超量，
            // 确定按钮却是灰的、界面上又没有任何提示，看起来就是"设置好了点不了确定"。
            // 这里把选择上限直接压到 firstCityCount - 1，玩家选不出会失败的组合。
            if (targetCorps.number != 1)
            {
                int firstCityCount = GetFirstCorpsCityCount();
                if (firstCityCount > 0)
                {
                    int maxTake = firstCityCount - 1;
                    if (maxTake < 1)
                        maxTake = 1;
                    if (count > maxTake)
                        count = maxTake;
                }
            }

            GameSystem.GetSystem<CitySelectSystem>().Start(validCityList,
             targetCityList, count, OnCityChange, CitySortFunction.DefaultSortList, "军团城池选择");
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
                targetCorps.mComander = targetPersonList[0];
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

        void CheckSure()
        {
            bool val = (targetCorps.mComander != null && targetCityList.Count > 0 && targetPersonList.Count > 0);

            // 【新建军团：主军团必须留 ≥2 城】
            // 对齐 can_district_new_command（s11_sys_person_order_kunshu.cpp:204-205）。
            // 若不拦，玩家能把主军团的城市全划走，留下一个 0 城军团
            //（无法征兵 / 无法反击 / AI 调度与都督任命都会失效）。
            if (val && targetCorps.number != 1)
            {
                int firstCityCount = GetFirstCorpsCityCount();

                // firstCityCount <= 0 表示"统计不到"（第 1 军团不存在，或城的 BelongCorps
                // 没挂上引用）。这种数据不可信的情况下不能据此禁用按钮 ——
                // 否则整个"新建军团"会被永久锁死成一个灰按钮，
                // 这正是玩家反馈的"设置好了也点不了确定"。
                // 【只防"0 城军团"，不要求保留 2 城】
                // 原版 can_district_new_command（s11_sys_person_order_kunshu.cpp:204-205）
                // 校验的是**进入命令那一刻**第 1 军团有 ≥2 城，并未要求"拆完之后仍 ≥2"。
                // 本项早前按"拆后仍 ≥2"实现，导致 2 城势力永远无法新建军团
                //（拆 1 城就剩 1 城，确定键永久变灰），与实际可玩性冲突。
                if (firstCityCount > 0 && firstCityCount - targetCityList.Count < 1)
                {
                    Sango.Log.Warning($"*[军团]新建第 {targetCorps.number} 军团被拦截："
                        + $"主军团把 {firstCityCount} 城全部划走后将没有城池");
                    val = false;
                }
            }

            if(val)
            {
                switch (targetCorps.appoint)
                {
                    case 1:
                    case 2:
                        val = targetCorps.appoint_target > 0;
                        break;
                    default:
                        break;
                }
            }
            sureButton.interactable = val;
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
                                CheckSure();
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
                                CheckSure();
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
