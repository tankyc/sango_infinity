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
                        // 【只在方针真的变了时才清目标】
                        // 原版命令参数（s11_sys_person_order_kunshu.cpp:283-303）里
                        // policy_target 是由玩家在界面上单独选的一项，切换方针本身不会把它抹掉。
                        // 之前无条件清 0，导致"点开方针菜单又选了同一项"也会丢掉已选目标，
                        // 随后 CheckSure 判定"方针需要目标但没有目标"，确定键直接变灰。
                        if (targetCorps.appoint != (int)Corps.AppointType.DestroyForce)
                            targetCorps.appoint_target = 0;
                        targetCorps.appoint = (int)Corps.AppointType.DestroyForce;
                        UpdateContent();
                    }
                },
                new PlayerChoice.ChoiceData()
                {
                    lab = $"攻略都市",
                    call = () =>
                    {
                        if (targetCorps.appoint != (int)Corps.AppointType.OccupyCity)
                            targetCorps.appoint_target = 0;
                        targetCorps.appoint = (int)Corps.AppointType.OccupyCity;
                        UpdateContent();
                    }
                },
                new PlayerChoice.ChoiceData()
                {
                    lab = $"委任",
                    call = () =>
                    {
                        // 委任不需要目标，这里必须清掉，否则会留下一个"委任但挂着攻略目标"的脏状态
                        targetCorps.appoint = (int)Corps.AppointType.Auto;
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
                // 记录"玩家手动指定"：Corps.UpdateCommander 每次触发都会重新择优，
                // 不记这一笔的话，玩家选的人只要不是官阶/统率最优，过一会儿就会被换掉。
                targetCorps.playerCommanderId = targetPersonList[0].Id;
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

            // 【诊断】确定键变灰时必须把原因打出来。
            // 之前这里静默置灰，玩家只看到"设置完了点不了确定"，日志里什么都没有。
            if (!val)
            {
                string reason;
                if (targetCorps == null)
                    reason = "军团对象为空";
                else if (targetCorps.mComander == null)
                    reason = "未选择军团长";
                else if (targetCityList.Count == 0)
                    reason = "未选择任何都市";
                else if (targetPersonList.Count == 0)
                    reason = "军团内没有可用武将";
                else if (targetCorps.appoint == (int)Corps.AppointType.DestroyForce
                      || targetCorps.appoint == (int)Corps.AppointType.OccupyCity)
                    reason = $"方针「{GetPolicyText(targetCorps.appoint)}」需要攻略目标，当前未选择目标";
                else
                    reason = $"会把第 1 军团的城全部划走（主军团现有 {GetFirstCorpsCityCount()} 城，本次划走 {targetCityList.Count} 城）";

                Sango.Log.Warning($"*[军团]确定不可用：{reason}");
            }
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
                // 【候选兜底】没有接壤势力（开局第一回合、势力被完全孤立）时，
                // NeighborForceList 是空的，选择器里一个可选项都没有 → 永远选不到目标
                // → CheckSure 判定"方针需要目标"把确定键锁死。
                // 原版命令参数的候选由 UI 层决定，这里退回"全部其他势力"保证可用。
                List<Force> forceCandidates = targetForce.NeighborForceList;
                if (forceCandidates == null || forceCandidates.Count == 0)
                {
                    Sango.Log.Warning("*[军团]本势力没有接壤势力，攻略势力候选回退为全部其他势力");
                    forceCandidates = new List<Force>();
                    Scenario.Cur.forceSet.ForEach(f =>
                    {
                        if (f != null && f.IsAlive && f != targetForce)
                            forceCandidates.Add(f);
                    });
                }

                GameSystem.GetSystem<ForceSelectSystem>().Start(forceCandidates,
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
                // 【候选兜底】理由同"攻略势力"分支：NeighborCityList 只在
                // Force.UpdateTurnInfo 里按"接壤都市"装填，为空时选择器无法选目标，
                // 确定键会被 CheckSure 永久锁死。这里退回"全部敌方都市"。
                List<City> cityCandidates = targetForce.NeighborCityList;
                if (cityCandidates == null || cityCandidates.Count == 0)
                {
                    Sango.Log.Warning("*[军团]本势力没有接壤都市，攻略都市候选回退为全部敌方都市");
                    cityCandidates = new List<City>();
                    Scenario.Cur.citySet.ForEach(c =>
                    {
                        if (c != null && c.IsAlive && c.IsCity() && c.BelongForce != targetForce)
                            cityCandidates.Add(c);
                    });
                }

                GameSystem.GetSystem<CitySelectSystem>().Start(cityCandidates,
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
