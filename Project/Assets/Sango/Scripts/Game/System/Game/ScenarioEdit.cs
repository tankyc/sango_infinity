using Sango.Core.Player;
using System;
using System.Collections.Generic;
using System.IO;
using TKNewtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 剧本编辑器系统 - 剧本编辑器的逻辑栈系统
    /// 负责剧本的新建、加载、保存、城池数据导入,
    /// 以及势力创建/删除、军团自动创建、武将登场等结构性编辑
    /// 关联窗口: window_scenario_edit
    /// </summary>
    [GameSystem]
    public class ScenarioEdit : GameSystem
    {
        /// <summary>
        /// 当前编辑的剧本
        /// </summary>
        public Scenario Scenario { get; private set; }

        /// <summary>
        /// 窗口名称 - 剧本编辑器主窗口
        /// </summary>
        protected string windowName = "window_scenario_edit";

        /// <summary>
        /// 启动剧本编辑器
        /// </summary>
        public void Start()
        {
            Push();
        }

        /// <summary>
        /// 初始化系统
        /// </summary>
        public override void Init()
        {
            Name = "剧本编辑";
        }

        /// <summary>
        /// 进入编辑状态 - 打开主窗口
        /// </summary>
        public override void OnEnter()
        {
            Window.Instance.Open(windowName);
        }

        /// <summary>
        /// 子命令返回时恢复窗口可见
        /// </summary>
        /// <param name="whoGone">返回的命令</param>
        public override void OnBack(ICommandEvent whoGone)
        {
            //Window.Instance.SetVisible(windowName, true);
        }

        /// <summary>
        /// 退出编辑状态 - 隐藏主窗口
        /// </summary>
        public override void OnExit()
        {
            //Window.Instance.SetVisible(windowName, false);
        }

        /// <summary>
        /// 销毁系统 - 关闭主窗口
        /// </summary>
        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        /// <summary>
        /// 处理输入事件 - 取消/右键返回
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="cell">格子</param>
        /// <param name="clickPosition">点击位置</param>
        /// <param name="isOverUI">是否在UI上</param>
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            if (eventType == CommandEventType.Cancel || eventType == CommandEventType.RClick)
            {
                Back();
            }
        }

        #region 剧本生命周期
        /// <summary>
        /// 初始化数据路径与公共数据
        /// </summary>
        private void EnsureContentInit()
        {
            if (Sango.Path.ContentRootPath == null)
            {
                Sango.Path.Init();
            }
            if (GameData.Instance.ScenarioCommonData == null)
            {
                GameData.Instance.Init();
            }
        }

        /// <summary>
        /// 新建空白剧本 - 从基础武将表加载武将并清空所属关系
        /// </summary>
        public void NewScenario()
        {
            EnsureContentInit();
            Scenario = new Scenario();
            Scenario.Info = new ScenarioInfo
            {
                id = 1,
                name = "新剧本",
                tag = "new",
                description = "",
                year = 190,
                month = 1,
                day = 1,
                curForceId = 0,
                mapType = "Default",
                turnCount = 0,
                priority = 0,
                isSave = false,
                playerForceList = new int[0],
                dateTime = DateTime.Now.ToFileTime()
            };
            Scenario.CommonData = GameData.Instance.ScenarioCommonData;
            Scenario.Variables = new ScenarioVariables();
            Scenario.View = new ScenarioView();
            Scenario.Map = null;
            LoadBasePersons();
            Log.Info("新建空白剧本完成");
        }

        /// <summary>
        /// 从文件加载剧本
        /// </summary>
        /// <param name="path">剧本文件路径</param>
        public void LoadScenario(string path, bool isNew = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (!File.Exists(path))
            {
                Log.Error("剧本文件不存在:" + path);
                return;
            }
            EnsureContentInit();
            Scenario = new Scenario(path);
            Scenario.CommonData = GameData.Instance.ScenarioCommonData;
            Scenario.LoadContent();
            Scenario.Prepare();
            if (isNew)
                Scenario.FilePath = "xx";
            Log.Info("剧本加载完成:" + Scenario.Info.name);
        }

        /// <summary>
        /// 将当前剧本保存到指定路径
        /// </summary>
        /// <param name="path">保存路径</param>
        public void SaveScenario(string path)
        {
            if (Scenario == null)
            {
                Log.Warning("当前没有可保存的剧本");
                return;
            }
            if (string.IsNullOrEmpty(path))
            {
                Log.Warning("保存路径为空");
                return;
            }
            Scenario.FilePath = path;
            Scenario.Info.dateTime = DateTime.Now.ToFileTime();
            Scenario.Export(path);
            Log.Info("剧本已保存:" + System.IO.Path.GetFileName(path));
        }

        public void SaveScenario()
        {
            string fileName = System.IO.Path.GetFileName(Scenario.FilePath);
            string saveFile = $"{Path.CustomEditRootPath}/Scenario/{fileName}";
            if(File.Exists(saveFile))
            {
                SaveScenario(saveFile);
                return;
            }

            int id = 1;
            while (true)
            {
                saveFile = $"{Path.CustomEditRootPath}/Scenario/Scenario{id}.json";
                if (File.Exists(saveFile))
                {
                    id++;
                    continue;
                }

                try
                {
                    SaveScenario(saveFile);
                }
                catch (Exception e)
                {
                    Sango.Log.Error(e + e.StackTrace);
                }
                break;
            }
        }

        /// <summary>
        /// 导入城池基础数据 - 从外部文件导入城池集合
        /// 支持两种格式: 纯城池集合JSON / 完整剧本JSON(取其citySet)
        /// 导入的城池会重新分配Id并清空归属关系
        /// </summary>
        /// <param name="path">城池数据文件路径</param>
        public void ImportCityData(string path)
        {
            if (Scenario == null)
            {
                Log.Error("请先新建或加载剧本,再导入城池数据");
                return;
            }
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Log.Error("城池数据文件不存在:" + path);
                return;
            }

            string text = File.ReadAllText(path);
            SangoObjectSet<City> citySet = null;

            // 尝试作为完整剧本解析,取其citySet
            try
            {
                Scenario temp = new Scenario();
                JsonConvert.PopulateObject(text, temp);
                if (temp.citySet != null && temp.citySet.Count > 0)
                {
                    citySet = temp.citySet;
                }
            }
            catch (Exception e)
            {
                Log.Warning("作为完整剧本解析失败,尝试纯城池集合格式:" + e.Message);
            }

            // 尝试作为纯城池集合解析
            if (citySet == null)
            {
                try
                {
                    JsonSerializerSettings settings = new JsonSerializerSettings();
                    settings.Converters.Add(new SangoObjectSetCityConverter());
                    citySet = JsonConvert.DeserializeObject<SangoObjectSet<City>>(text, settings);
                }
                catch (Exception e)
                {
                    Log.Error("城池数据解析失败:" + e.Message);
                    return;
                }
            }

            if (citySet == null || citySet.Count == 0)
            {
                Log.Warning("城池数据文件中没有有效的城池数据");
                return;
            }

            // 导入到当前剧本 - 重新分配Id并清空归属
            int count = 0;
            citySet.ForEach(city =>
            {
                if (city != null && city.IsCity())
                {
                    city.Id = -1;
                    city.BelongForce = 0;
                    city.mBelongForce = null;
                    city.BelongCorps = 0;
                    city.mBelongCorps = null;
                    Scenario.citySet.Add(city);
                    count++;
                }
            });
            Log.Info("城池数据导入完成,共导入 " + count + " 座城池");
        }

        /// <summary>
        /// 基础武将数据包装类 - 用于反序列化基础武将JSON
        /// </summary>
        private class PersonSetWrapper
        {
            [JsonProperty]
            public SangoObjectSet<Person> personSet = new SangoObjectSet<Person>();
        }

        /// <summary>
        /// 从基础武将数据表加载武将并清空所属关系
        /// </summary>
        private void LoadBasePersons()
        {
            string personsPath = Sango.Path.FindFile("Data/Common/Persons.json");
            if (string.IsNullOrEmpty(personsPath))
            {
                personsPath = Sango.Path.ContentRootPath + "/Data/Common/Persons.json";
            }
            if (!File.Exists(personsPath))
            {
                Log.Error("未找到基础武将数据: " + personsPath);
                return;
            }

            PersonSetWrapper wrapper = JsonConvert.DeserializeObject<PersonSetWrapper>(File.ReadAllText(personsPath));
            if (wrapper == null || wrapper.personSet == null)
            {
                Log.Warning("基础武将数据解析为空");
                return;
            }

            Scenario.personSet = wrapper.personSet;
            Scenario.personSet.ForEach(person =>
            {
                if (person == null)
                {
                    return;
                }
                person.mBelongForce = null;
                person.mBelongCorps = null;
                person.mBelongCity = null;
                person.mCurrentCity = null;
                person.mTroop = null;
                // 默认状态为未登场
                person.state = (int)PersonStateType.Invalid;
            });

            Log.Info("已加载基础武将数量: " + Scenario.personSet.Count);
        }
        #endregion

        #region 势力编辑
        /// <summary>
        /// 新建势力 - 需要指定一个无势力的武将作为君主,以及一座无势力的城市作为都城
        /// 会自动创建对应的主军团(编号为1,军团长为势力主公)
        /// </summary>
        /// <param name="governor">君主武将(必须无势力)</param>
        /// <param name="capitalCity">都城(必须无势力)</param>
        /// <returns>创建成功的势力,失败返回null</returns>
        public Force CreateForce(Person governor, City capitalCity)
        {
            if (Scenario == null)
            {
                Log.Error("请先新建或加载剧本");
                return null;
            }
            if (governor == null)
            {
                Log.Error("新建势力需要指定一个君主武将");
                return null;
            }
            if (capitalCity == null)
            {
                Log.Error("新建势力需要指定一座都城");
                return null;
            }
            if (governor.mBelongForce != null)
            {
                Log.Warning("君主武将 " + governor.Name + " 已有势力,无法创建新势力");
                return null;
            }
            if (capitalCity.mBelongForce != null)
            {
                Log.Warning("都城 " + capitalCity.Name + " 已有势力,无法创建新势力");
                return null;
            }

            // 创建势力
            Force force = new Force();
            force.Id = -1;
            force.Governor = governor.Id;
            force.mGovernor = governor;
            force.Counsellor = 0;
            force.mCounsellor = null;
            Flag flag = GetEmptyFlag();
            force.Flag = flag != null ? flag.Id : 0;
            force.mFlag = flag;
            Scenario.forceSet.Add(force);

            // 自动创建主军团 - 编号为1,军团长为势力主公
            Corps mainCorps = new Corps();
            mainCorps.Id = -1;
            mainCorps.BelongForce = force.Id;
            mainCorps.mBelongForce = force;
            mainCorps.Comander = governor.Id;
            mainCorps.mComander = governor;
            mainCorps.number = 1;
            Scenario.corpsSet.Add(mainCorps);

            // 君主归属势力与主军团
            governor.BelongForce = force.Id;
            governor.mBelongForce = force;
            governor.BelongCorps = mainCorps.Id;
            governor.mBelongCorps = mainCorps;
            governor.state = (int)PersonStateType.Governor;

            // 都城归属势力与主军团
            capitalCity.BelongForce = force.Id;
            capitalCity.mBelongForce = force;
            capitalCity.BelongCorps = mainCorps.Id;
            capitalCity.mBelongCorps = mainCorps;

            Log.Info("新建势力完成: " + force.Name + " ,并自动创建主军团");
            return force;
        }

        /// <summary>
        /// 删除势力 - 同时删除相关军团,所有所属都市和所属武将去势力化
        /// </summary>
        /// <param name="force">要删除的势力</param>
        public void DeleteForce(Force force)
        {
            if (Scenario == null || force == null)
            {
                return;
            }

            // 1. 删除相关军团
            List<Corps> corpsList = new List<Corps>();
            Scenario.corpsSet.ForEach(corps =>
            {
                if (corps != null && corps.mBelongForce == force)
                {
                    corpsList.Add(corps);
                }
            });
            for (int i = 0; i < corpsList.Count; i++)
            {
                Scenario.corpsSet.Remove(corpsList[i]);
            }

            // 2. 所有所属都市去势力化
            List<City> cityList = new List<City>();
            Scenario.citySet.ForEach(city =>
            {
                if (city != null && city.mBelongForce == force)
                {
                    cityList.Add(city);
                }
            });
            for (int i = 0; i < cityList.Count; i++)
            {
                City city = cityList[i];
                city.BelongForce = 0;
                city.mBelongForce = null;
                city.BelongCorps = 0;
                city.mBelongCorps = null;
            }

            // 3. 所有所属武将去势力化 - 状态设置为在野
            List<Person> personList = new List<Person>();
            Scenario.personSet.ForEach(person =>
            {
                if (person != null && person.mBelongForce == force)
                {
                    personList.Add(person);
                }
            });
            for (int i = 0; i < personList.Count; i++)
            {
                Person person = personList[i];
                person.BelongForce = 0;
                person.mBelongForce = null;
                person.BelongCorps = 0;
                person.mBelongCorps = null;
                person.state = (int)PersonStateType.Unemployed;
            }

            // 4. 删除势力
            Scenario.forceSet.Remove(force);
            Log.Info("删除势力完成: " + force.Name);
        }

        /// <summary>
        /// 设置势力的旗帜颜色
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="flag">新的旗帜</param>
        public void SetForceFlag(Force force, Flag flag)
        {
            if (force == null || flag == null)
            {
                return;
            }
            // 检查旗帜是否已被其他势力使用
            bool used = false;
            Scenario.forceSet.ForEach(f =>
            {
                if (f != null && f != force && f.mFlag == flag)
                {
                    used = true;
                }
            });
            if (used)
            {
                Log.Warning("旗帜 " + flag.Id + " 已被其他势力使用");
                return;
            }
            force.Flag = flag.Id;
            force.mFlag = flag;
            Log.Info("势力 " + force.Name + " 的旗帜已设置");
        }

        /// <summary>
        /// 获取一个未被任何势力使用的旗帜
        /// </summary>
        /// <returns>未使用的旗帜,无可用返回null</returns>
        public Flag GetEmptyFlag()
        {
            if (Scenario == null || Scenario.CommonData == null || Scenario.CommonData.Flags == null)
            {
                return null;
            }
            List<Flag> usedFlags = new List<Flag>();
            Scenario.forceSet.ForEach(force =>
            {
                if (force != null && force.mFlag != null)
                {
                    usedFlags.Add(force.mFlag);
                }
            });
            Flag result = null;
            Scenario.CommonData.Flags.ForEach(flag =>
            {
                if (flag != null && !usedFlags.Contains(flag) && result == null)
                {
                    result = flag;
                }
            });
            return result;
        }

        /// <summary>
        /// 将城市分配给势力 - 城市归入势力的主军团
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="city">要分配的城市(必须无势力)</param>
        public void AssignCityToForce(Force force, City city)
        {
            if (Scenario == null || force == null || city == null)
            {
                return;
            }
            if (city.mBelongForce != null && city.mBelongForce != force)
            {
                Log.Warning("城市 " + city.Name + " 已属于势力 " + city.mBelongForce.Name);
                return;
            }
            Corps mainCorps = GetMainCorps(force);
            if (mainCorps == null)
            {
                Log.Warning("势力 " + force.Name + " 没有主军团,无法分配城市");
                return;
            }
            city.BelongForce = force.Id;
            city.mBelongForce = force;
            city.BelongCorps = mainCorps.Id;
            city.mBelongCorps = mainCorps;
            Log.Info("城市 " + city.Name + " 已加入势力 " + force.Name);
        }

        /// <summary>
        /// 将城市从势力中移除 - 城市去势力化,城内武将同步去势力化
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="city">要移除的城市</param>
        public void RemoveCityFromForce(Force force, City city)
        {
            if (Scenario == null || force == null || city == null)
            {
                return;
            }
            if (city.mBelongForce != force)
            {
                return;
            }
            city.BelongForce = 0;
            city.mBelongForce = null;
            city.BelongCorps = 0;
            city.mBelongCorps = null;

            // 城内武将同步去势力化 - 状态设置为在野
            Scenario.personSet.ForEach(person =>
            {
                if (person != null && person.mBelongCity == city && person.mBelongForce == force)
                {
                    person.BelongForce = 0;
                    person.mBelongForce = null;
                    person.BelongCorps = 0;
                    person.mBelongCorps = null;
                    person.state = (int)PersonStateType.Unemployed;
                }
            });
            Log.Info("城市 " + city.Name + " 已从势力 " + force.Name + " 移除");
        }

        /// <summary>
        /// 获取势力的主军团 - 优先君主所在军团,其次第一军团,最后势力下第一个军团
        /// </summary>
        /// <param name="force">所属势力</param>
        /// <returns>主军团,无则返回null</returns>
        public Corps GetMainCorps(Force force)
        {
            if (force == null)
            {
                return null;
            }
            // 优先君主所在军团(第一军团)
            if (force.CapitalCorps != null)
            {
                return force.CapitalCorps;
            }
            // 其次第一军团,最后势力下第一个军团
            Corps captain = null;
            Corps first = null;
            Scenario.corpsSet.ForEach(corps =>
            {
                if (corps != null && corps.mBelongForce == force)
                {
                    if (corps.IsCaptainCorps)
                    {
                        captain = corps;
                    }
                    else if (first == null)
                    {
                        first = corps;
                    }
                }
            });
            return captain != null ? captain : first;
        }

        /// <summary>
        /// 获取势力下的所有军团列表
        /// </summary>
        /// <param name="force">所属势力,为空时返回全部军团</param>
        /// <returns>军团列表</returns>
        public List<Corps> GetForceCorpsList(Force force)
        {
            List<Corps> corpsList = new List<Corps>();
            if (Scenario == null || Scenario.corpsSet == null)
            {
                return corpsList;
            }
            Scenario.corpsSet.ForEach(corps =>
            {
                if (corps != null && (force == null || corps.mBelongForce == force))
                {
                    corpsList.Add(corps);
                }
            });
            return corpsList;
        }
        #endregion

        #region 军团编辑
        /// <summary>
        /// 新建军团 - 为指定势力创建非第一军团,自动分配空闲编号(从2开始),并任命军团长
        /// </summary>
        /// <param name="force">所属势力,必须已存在第一主军团</param>
        /// <param name="commander">军团长,必须属于该势力且不能是君主</param>
        /// <returns>创建成功的军团,失败返回null</returns>
        public Corps CreateCorps(Force force, Person commander)
        {
            if (Scenario == null)
            {
                Log.Error("请先新建或加载剧本");
                return null;
            }
            if (force == null)
            {
                Log.Warning("新建军团需要指定所属势力");
                return null;
            }
            if (commander == null)
            {
                Log.Warning("新建军团需要指定军团长");
                return null;
            }
            if (commander.mBelongForce != force)
            {
                Log.Warning("军团长 " + commander.Name + " 不属于势力 " + force.Name);
                return null;
            }
            if (force.mGovernor == commander)
            {
                Log.Warning("君主 " + commander.Name + " 不能离开第一军团,请选择其他武将担任军团长");
                return null;
            }
            if (GetMainCorps(force) == null)
            {
                Log.Warning("势力 " + force.Name + " 没有主军团,无法新建军团");
                return null;
            }
            // 自动分配一个未被占用的军团编号(从2开始,1为第一军团编号)
            int number = GetFreeCorpsNumber(force);
            if (number <= 1)
            {
                Log.Warning("势力 " + force.Name + " 已无可用的军团编号");
                return null;
            }
            // 若武将当前是其他军团的军团长,先解除其原军团长职位
            Scenario.corpsSet.ForEach(corps =>
            {
                if (corps != null && corps.mComander == commander)
                {
                    corps.Comander = 0;
                    corps.mComander = null;
                }
            });
            // 创建军团并加入剧本
            Corps newCorps = new Corps();
            newCorps.Id = -1;
            newCorps.number = number;
            newCorps.BelongForce = force.Id;
            newCorps.mBelongForce = force;
            newCorps.Comander = commander.Id;
            newCorps.mComander = commander;
            Scenario.corpsSet.Add(newCorps);
            // 军团长归属新军团
            commander.BelongCorps = newCorps.Id;
            commander.mBelongCorps = newCorps;
            commander.SetStateCommander();
            Log.Info("新建军团完成: " + newCorps.Name + " ,军团长为 " + commander.Name);
            return newCorps;
        }

        /// <summary>
        /// 获取势力下可用的军团编号 - 从2开始查找第一个未被占用的编号
        /// </summary>
        /// <param name="force">所属势力</param>
        /// <returns>可用编号,无可用时返回-1</returns>
        public int GetFreeCorpsNumber(Force force)
        {
            if (Scenario == null || Scenario.corpsSet == null || force == null)
            {
                return -1;
            }
            for (int number = 2; number < Corps.numberTxt.Length; number++)
            {
                bool used = false;
                Scenario.corpsSet.ForEach(corps =>
                {
                    if (corps != null && corps.mBelongForce == force && corps.number == number)
                    {
                        used = true;
                    }
                });
                if (!used)
                {
                    return number;
                }
            }
            return -1;
        }

        /// <summary>
        /// 删除军团 - 不可删除第一主军团;删除后原军团的所属武将与城池同步转入主军团
        /// </summary>
        /// <param name="corps">要删除的军团</param>
        /// <returns>是否删除成功</returns>
        public bool DeleteCorps(Corps corps)
        {
            if (Scenario == null || corps == null)
            {
                return false;
            }
            // 未归属势力的游离军团直接移除
            if (corps.mBelongForce == null)
            {
                Scenario.corpsSet.Remove(corps);
                Log.Info("删除军团完成: " + corps.Name);
                return true;
            }
            // 第一主军团不可删除
            if (corps.IsCaptainCorps || corps == corps.mBelongForce.CapitalCorps)
            {
                Log.Warning("第一军团不可删除,只能删除分军团");
                return false;
            }
            Force force = corps.mBelongForce;
            // 势力主军团,删除后原军团的武将与城池转入主军团
            Corps mainCorps = GetMainCorps(force);
            if (mainCorps == null)
            {
                Log.Warning("势力 " + force.Name + " 没有主军团,无法删除军团");
                return false;
            }
            // 原军团所属城市转入主军团
            Scenario.citySet.ForEach(city =>
            {
                if (city != null && city.mBelongCorps == corps)
                {
                    city.BelongCorps = mainCorps.Id;
                    city.mBelongCorps = mainCorps;
                }
            });
            // 原军团所属武将转入主军团
            Scenario.personSet.ForEach(person =>
            {
                if (person != null && person.mBelongCorps == corps)
                {
                    person.BelongCorps = mainCorps.Id;
                    person.mBelongCorps = mainCorps;
                }
            });
            // 军团长恢复为普通状态
            if (corps.mComander != null && corps.mComander.IsCommander)
            {
                corps.mComander.SetStateNormal();
            }
            Scenario.corpsSet.Remove(corps);
            Log.Info("删除军团完成: " + corps.Name + " ,所属武将与城池已转入主军团");
            return true;
        }
        #endregion

        #region 武将编辑
        /// <summary>
        /// 新建武将 - 创建一个未登场的自定义武将并加入剧本,可后续登场或编辑属性
        /// </summary>
        /// <returns>创建成功的武将,失败返回null</returns>
        public Person CreatePerson()
        {
            if (Scenario == null)
            {
                Log.Error("请先新建或加载剧本");
                return null;
            }
            Person person = new Person();
            person.Id = -1;
            person.Name = "新武将";
            person.familyName = "新";
            person.giveName = "武将";
            person.nickName = "";
            person.state = (int)PersonStateType.Invalid;
            Scenario.personSet.Add(person);
            Log.Info("新建武将完成: " + person.Name);
            return person;
        }

        /// <summary>
        /// 删除武将 - 解除其势力/军团/城池等从属关系后从剧本移除
        /// 君主(势力主公)不可直接删除,需先在势力页删除其势力
        /// </summary>
        /// <param name="person">要删除的武将</param>
        /// <returns>是否删除成功</returns>
        public bool DeletePerson(Person person)
        {
            if (Scenario == null || person == null)
            {
                return false;
            }
            // 君主不可直接删除,需要先删除其势力
            if (person.mBelongForce != null && person.mBelongForce.mGovernor == person)
            {
                Log.Warning("君主 " + person.Name + " 不可删除,请先在势力页删除其势力");
                return false;
            }
            // 解除其担任的军团军团长职位
            Scenario.corpsSet.ForEach(corps =>
            {
                if (corps != null && corps.mComander == person)
                {
                    corps.Comander = 0;
                    corps.mComander = null;
                }
            });
            // 解除其担任的势力军师职位
            Scenario.forceSet.ForEach(force =>
            {
                if (force != null && force.mCounsellor == person)
                {
                    force.Counsellor = 0;
                    force.mCounsellor = null;
                }
            });
            // 解除势力/军团/城池等从属关系
            person.BelongForce = 0;
            person.mBelongForce = null;
            person.BelongCorps = 0;
            person.mBelongCorps = null;
            person.BelongCity = 0;
            person.mBelongCity = null;
            person.CurrentCity = 0;
            person.mCurrentCity = null;
            person.BelongTroop = 0;
            person.mTroop = null;
            Scenario.personSet.Remove(person);
            Log.Info("删除武将完成: " + person.Name);
            return true;
        }

        /// <summary>
        /// 让武将登场 - 将未登场(无效状态)的武将设置为在野状态登场,并放入指定城市
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="city">登场所在城市,可为null</param>
        public void MakePersonAppear(Person person, City city)
        {
            if (Scenario == null || person == null)
            {
                return;
            }
            if (person.IsValid)
            {
                Log.Warning("武将 " + person.Name + " 已经登场");
                return;
            }
            // 以在野状态登场
            person.state = (int)PersonStateType.Unemployed;
            if (city != null)
            {
                person.BelongCity = city.Id;
                person.mBelongCity = city;
                person.CurrentCity = city.Id;
                person.mCurrentCity = city;
            }
            Log.Info("武将 " + person.Name + " 已登场");
        }

        /// <summary>
        /// 获取当前剧本中的所有无势力武将(包括未登场武将)
        /// </summary>
        /// <returns>无势力武将列表</returns>
        public List<Person> GetFreePersons()
        {
            List<Person> persons = new List<Person>();
            if (Scenario == null || Scenario.personSet == null)
            {
                return persons;
            }
            Scenario.personSet.ForEach(person =>
            {
                if (person != null && person.mBelongForce == null)
                {
                    persons.Add(person);
                }
            });
            return persons;
        }

        /// <summary>
        /// 获取当前剧本中的所有无势力城市
        /// </summary>
        /// <returns>无势力城市列表</returns>
        public List<City> GetFreeCities()
        {
            List<City> cities = new List<City>();
            if (Scenario == null || Scenario.citySet == null)
            {
                return cities;
            }
            Scenario.citySet.ForEach(city =>
            {
                if (city != null && city.mBelongForce == null)
                {
                    cities.Add(city);
                }
            });
            return cities;
        }
        #endregion
    }
}
