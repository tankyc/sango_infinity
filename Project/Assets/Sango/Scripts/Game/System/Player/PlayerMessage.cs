using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace Sango.Core.Player
{
    /// <summary>
    /// 玩家消息系统
    /// 负责管理和处理游戏中的消息，包括文本消息和人物消息
    /// </summary>
    [GameSystem]
    public class PlayerMessage : GameSystem
    {
        /// <summary>
        /// 文本消息回调委托
        /// </summary>
        /// <param name="msg">文本消息对象</param>
        /// <param name="message">消息系统实例</param>
        public delegate void PlayerTextMessageCallback(TextMessage msg, PlayerMessage message);
        
        /// <summary>
        /// 人物消息回调委托
        /// </summary>
        /// <param name="msg">人物消息对象</param>
        /// <param name="message">消息系统实例</param>
        public delegate void PlayerPersonMessageCallback(PersonMessage msg, PlayerMessage message);

        /// <summary>
        /// 最大消息保存数量
        /// </summary>
        private int maxSaveCount = 100;
        
        /// <summary>
        /// 消息窗口名称
        /// </summary>
        private string windowName = "window_player_message";
        
        /// <summary>
        /// 是否是新的一天
        /// 用于标记是否需要在消息中添加日期信息
        /// </summary>
        private bool newDay = true;
        
        /// <summary>
        /// 消息系统单例实例
        /// </summary>
        private static PlayerMessage Instance;

        /// <summary>
        /// 窗口可见性变化回调
        /// </summary>
        public System.Action<bool> onVisibleChange;

        /// <summary>
        /// 文本消息类
        /// 用于存储游戏中的文本消息，包含消息内容、关联势力、坐标位置和日期信息
        /// </summary>
        public class TextMessage
        {
            /// <summary>
            /// 消息文本内容
            /// </summary>
            public string text;
            
            /// <summary>
            /// 关联的势力
            /// </summary>
            public Force force;
            
            /// <summary>
            /// 消息关联的X坐标
            /// </summary>
            public int x;
            
            /// <summary>
            /// 消息关联的Y坐标
            /// </summary>
            public int y;
            
            /// <summary>
            /// 消息发生的年份
            /// </summary>
            public int year;
            
            /// <summary>
            /// 消息发生的月份
            /// </summary>
            public int month;
            
            /// <summary>
            /// 消息发生的日期
            /// </summary>
            public int day;
        }

        /// <summary>
        /// 人物消息类
        /// 用于存储游戏中与人物相关的消息，包含消息内容和关联人物
        /// </summary>
        public class PersonMessage
        {
            /// <summary>
            /// 关联的人物
            /// </summary>
            public Person person;
            
            /// <summary>
            /// 消息文本内容
            /// </summary>
            public string text;
        }

        /// <summary>
        /// 文本消息列表
        /// </summary>
        public List<TextMessage> textMessages = new List<TextMessage>();
        
        /// <summary>
        /// 人物消息列表
        /// </summary>
        public List<PersonMessage> personMessages = new List<PersonMessage>();
        
        /// <summary>
        /// 游戏场景实例
        /// </summary>
        private Scenario scenario;

        /// <summary>
        /// 文本消息添加回调
        /// 当添加新的文本消息时触发
        /// </summary>
        public PlayerTextMessageCallback onTextMessageAdd;
        
        /// <summary>
        /// 人物消息添加回调
        /// 当添加新的人物消息时触发
        /// </summary>
        public PlayerPersonMessageCallback onPersonMessageAdd;

        /// <summary>
        /// 系统初始化方法
        /// 注册游戏事件监听
        /// </summary>
        public override void Init()
        {
            GameEvent.OnScenarioInit += OnScenarioInit;
            GameEvent.OnDayUpdate += OnDayUpdate;
            GameEvent.OnScenarioStart += OnScenarioStart;
            GameEvent.OnScenarioEnd += OnScenarioEnd;
            GameEvent.OnDiscoverEnemyTroop += OnDiscoverEnemyTroop;
            GameEvent.OnBuildingComplete += OnBuildingComplete;
            GameEvent.OnBuildingUpgradeComplete += OnBuildingUpgradeComplete;
            GameEvent.OnPersonChangeBelongCity += OnPersonChangeCityComplete;
            GameEvent.OnCityFall += OnCityFall;
            GameEvent.OnPersonEscape += OnPersonEscape;
            GameEvent.OnPersonRelease += OnPersonRelease;
            GameEvent.OnPersonExecute += OnPersonExecute;
            GameEvent.OnPersonLevelUp += OnPersonLevelUp;
            GameEvent.OnPersonUpgradeOfficial += OnPersonUpgradeOfficial;
            GameEvent.OnForceFall += OnForceFall;
        }
        
        /// <summary>
        /// 系统清理方法
        /// 移除游戏事件监听
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnScenarioInit -= OnScenarioInit;
            GameEvent.OnDayUpdate -= OnDayUpdate;
            GameEvent.OnScenarioStart -= OnScenarioStart;
            GameEvent.OnScenarioEnd -= OnScenarioEnd;
            GameEvent.OnDiscoverEnemyTroop -= OnDiscoverEnemyTroop;
            GameEvent.OnBuildingComplete -= OnBuildingComplete;
            GameEvent.OnBuildingUpgradeComplete -= OnBuildingUpgradeComplete;
            GameEvent.OnPersonChangeBelongCity -= OnPersonChangeCityComplete;
            GameEvent.OnCityFall -= OnCityFall;
            GameEvent.OnPersonEscape -= OnPersonEscape;
            GameEvent.OnPersonRelease -= OnPersonRelease;
            GameEvent.OnPersonExecute -= OnPersonExecute;
            GameEvent.OnPersonLevelUp -= OnPersonLevelUp;
            GameEvent.OnPersonUpgradeOfficial -= OnPersonUpgradeOfficial;
            GameEvent.OnForceFall -= OnForceFall;
        }

        /// <summary>
        /// 场景开始时的回调
        /// 打开消息窗口
        /// </summary>
        /// <param name="scenario">游戏场景实例</param>
        private void OnScenarioStart(Scenario scenario)
        {
            Window.Instance.Open(windowName);
        }

        /// <summary>
        /// 场景结束时的回调
        /// 关闭消息窗口
        /// </summary>
        /// <param name="scenario">游戏场景实例</param>
        private void OnScenarioEnd(Scenario scenario)
        {

            Window.Instance.Close(windowName);
        }

        /// <summary>
        /// 设置消息窗口的可见性
        /// </summary>
        /// <param name="b">是否可见</param>
        public void SetVisible(bool b)
        {
            Window.Instance.SetVisible(windowName, b);
        }

        /// <summary>
        /// 日期更新时的回调
        /// 标记为新的一天，以便在消息中添加日期信息
        /// </summary>
        /// <param name="scenario">游戏场景实例</param>
        private void OnDayUpdate(Scenario scenario)
        {
            newDay = true;
        }

        /// <summary>
        /// 场景初始化时的回调
        /// 清空消息列表，更新场景引用和单例实例
        /// </summary>
        /// <param name="scenario">游戏场景实例</param>
        private void OnScenarioInit(Scenario scenario)
        {
            textMessages.Clear();
            personMessages.Clear();
            this.scenario = scenario;
            // 更新实例
            Instance = GameSystem.GetSystem<PlayerMessage>();
        }

        /// <summary>
        /// 添加文本消息的内部方法
        /// 处理消息的创建、存储和回调触发
        /// </summary>
        /// <param name="text">消息文本内容</param>
        /// <param name="force">关联的势力</param>
        /// <param name="x">消息关联的X坐标</param>
        /// <param name="y">消息关联的Y坐标</param>
        private void _AddTextMessage(string text, Force force, int x, int y)
        {
            TextMessage message;
            if(textMessages == null)
            {
                return;
            }


            // 检查消息数量是否超过上限
            if (textMessages.Count >= maxSaveCount)
            {
                // 移除最旧的消息，重用消息对象
                message = textMessages[0];
                textMessages.RemoveAt(0);
                message.text = text;
                message.force = force;
                message.x = x; message.y = y;
            }
            else
            {
                // 创建新的消息对象
                message = new TextMessage()
                {
                    text = text,
                    force = force,
                    x = x,
                    y = y
                };
            }

            // 如果是新的一天，添加日期信息
            if (newDay)
            {
                message.year = scenario.Info.year;
                message.month = scenario.Info.month;
                message.day = scenario.Info.day;
                newDay = false;
            }
            else
            {
                message.year = 0;
            }
            GameMedia.Instance.PlaySfx(38);
            // 添加消息到列表并触发回调
            textMessages.Add(message);
            onTextMessageAdd?.Invoke(message, this);
        }

        /// <summary>
        /// 添加文本消息的静态方法
        /// 外部调用的入口点
        /// </summary>
        /// <param name="text">消息文本内容</param>
        /// <param name="force">关联的势力</param>
        /// <param name="x">消息关联的X坐标</param>
        /// <param name="y">消息关联的Y坐标</param>
        public static void AddTextMessage(string text, Force force, int x, int y)
        {
            Instance._AddTextMessage(text, force, x, y);
        }

        /// <summary>
        /// 添加人物消息的内部方法
        /// 处理消息的创建、存储和回调触发
        /// </summary>
        /// <param name="text">消息文本内容</param>
        /// <param name="person">关联的人物</param>
        private void _AddPersonMessage(string text, Person person)
        {
            PersonMessage message;
            // 检查消息数量是否超过上限
            if (personMessages.Count >= maxSaveCount)
            {
                // 移除最旧的消息，重用消息对象
                message = personMessages[0];
                personMessages.RemoveAt(0);
                message.text = text;
                message.person = person;
            }
            else
            {
                // 创建新的消息对象
                message = new PersonMessage()
                {
                    text = text,
                    person = person,
                };
            }
            // 添加消息到列表并触发回调
            personMessages.Add(message);
            onPersonMessageAdd?.Invoke(message, this);
        }

        /// <summary>
        /// 添加人物消息的静态方法
        /// 外部调用的入口点
        /// </summary>
        /// <param name="text">消息文本内容</param>
        /// <param name="person">关联的人物</param>
        public static void AddPersonMessage(string text, Person person)
        {
            Instance._AddPersonMessage(text, person);
        }
        
        /// <summary>
        /// 发现敌方部队事件处理方法
        /// </summary>
        /// <param name="force">发现方势力</param>
        /// <param name="targetCity">目标城池</param>
        /// <param name="troop">敌方部队</param>
        /// <param name="counsellor">军师</param>
        private void OnDiscoverEnemyTroop(Force force, City targetCity, Troop troop, Person counsellor)
        {
            if (counsellor != null && force.IsCurPlayer)
            {
                string message = $"敌军正在向我方{targetCity.ColorName}进军，还请主公早做准备！";
                _AddPersonMessage(message, counsellor);
            }
        }
        
        /// <summary>
        /// 建筑建造完成事件处理方法
        /// </summary>
        /// <param name="building">完成的建筑</param>
        /// <param name="builders">建造者</param>
        private void OnBuildingComplete(Building building, SangoObjectList<Person> builders)
        {
            if (building.IsCurPlayer)
            {
                string message = $"我军在{building.BelongCity.ColorName}成功建造了{building.Name}！";
                _AddTextMessage(message, building.BelongForce, building.x, building.y);
            }
        }
        
        /// <summary>
        /// 建筑升级完成事件处理方法
        /// </summary>
        /// <param name="building">升级的建筑</param>
        /// <param name="builders">升级者</param>
        private void OnBuildingUpgradeComplete(Building building, SangoObjectList<Person> builders)
        {
            if (building.IsCurPlayer)
            {
                string message = $"我军在{building.BelongCity.ColorName}成功将{building.Name}升级完成！";
                _AddTextMessage(message, building.BelongForce, building.x, building.y);
            }
        }
        
        /// <summary>
        /// 武将转移成功事件处理方法
        /// </summary>
        /// <param name="person">转移的武将</param>
        /// <param name="fromCity">来源城池</param>
        /// <param name="toCity">目标城池</param>
        private void OnPersonChangeCityComplete(Person person, City fromCity, City toCity)
        {
            if (person.IsCurPlayer)
            {
                if(fromCity == toCity)
                {
                    string message = $"{person.ColorName}已经回到{toCity.ColorName}！";
                    _AddTextMessage(message, person.BelongForce, toCity.x, toCity.y);
                }
                else
                {
                    string message = $"{person.ColorName}已成功从{fromCity.ColorName}转移到{toCity.ColorName}！";
                    _AddTextMessage(message, person.BelongForce, toCity.x, toCity.y);
                }
            }
        }
        
        /// <summary>
        /// 城池攻陷事件处理方法
        /// </summary>
        /// <param name="city">被攻陷的城池</param>
        /// <param name="lastForce">原所属势力</param>
        /// <param name="troop">进攻部队</param>
        private void OnCityFall(City city, Force lastForce, Troop troop)
        {
            Force attackForce = troop.BelongForce;
            bool isAttackCurPlayer = attackForce.IsCurPlayer;
            bool isDefendCurPlayer = lastForce?.IsCurPlayer ?? false;
            
            string message;
            if (isAttackCurPlayer)
            {
                message = $"我军成功攻陷了{city.ColorName}！";
                _AddTextMessage(message, attackForce, city.x, city.y);
            }
            else if (isDefendCurPlayer)
            {
                message = $"我方{city.ColorName}被{attackForce.ColorName}攻陷了！";
                _AddTextMessage(message, lastForce, city.x, city.y);
            }
            else
            {
                message = $"{city.ColorName}被{attackForce.ColorName}攻陷了！";
                _AddTextMessage(message, attackForce, city.x, city.y);
            }
        }
        
        /// <summary>
        /// 武将逃跑事件处理方法
        /// </summary>
        /// <param name="person">逃跑的武将</param>
        /// <param name="location">逃跑的位置</param>
        private void OnPersonEscape(Person person, SangoObject location)
        {
            // 检查俘虏是否为当前玩家势力
            bool isCaptiveCurPlayer = person.IsCurPlayer;
            
            // 检查执行方（关押方）是否为当前玩家势力
            bool isCaptorCurPlayer = false;
            if (location is City city)
            {
                isCaptorCurPlayer = city.IsCurPlayer;
            }
            else if (location is Troop troop)
            {
                isCaptorCurPlayer = troop.IsCurPlayer;
            }
            
            string message;
            if (isCaptiveCurPlayer)
            {
                message = $"{person.ColorName}成功逃脱了！";
                _AddTextMessage(message, person.BelongForce, 0, 0);
            }
            else if (isCaptorCurPlayer)
            {
                message = $"我方关押的{person.ColorName}逃跑了！";
                Force captorForce = location is City ? (location as City).BelongForce : (location as Troop).BelongForce;
                _AddTextMessage(message, captorForce, 0, 0);
            }
        }
        
        /// <summary>
        /// 俘虏被释放事件处理方法
        /// </summary>
        /// <param name="person">被释放的俘虏</param>
        /// <param name="releaseForce">释放方势力</param>
        private void OnPersonRelease(Person person, Force releaseForce)
        {
            // 检查俘虏是否为当前玩家势力
            bool isCaptiveCurPlayer = person.IsCurPlayer;
            // 检查释放方是否为当前玩家势力
            bool isReleaseCurPlayer = releaseForce.IsCurPlayer;
            
            string message;
            if (isCaptiveCurPlayer)
            {
                message = $"{person.ColorName}被{releaseForce.ColorName}释放了！";
                _AddTextMessage(message, person.BelongForce, 0, 0);
            }
            else if (isReleaseCurPlayer)
            {
                message = $"我方释放了{person.ColorName}！";
                _AddTextMessage(message, releaseForce, 0, 0);
            }
        }
        
        /// <summary>
        /// 俘虏被斩杀事件处理方法
        /// </summary>
        /// <param name="person">被斩杀的俘虏</param>
        /// <param name="executeForce">斩杀方势力</param>
        private void OnPersonExecute(Person person, Force executeForce)
        {
            // 检查俘虏是否为当前玩家势力
            bool isCaptiveCurPlayer = person.IsCurPlayer;
            // 检查斩杀方是否为当前玩家势力
            bool isExecuteCurPlayer = executeForce.IsCurPlayer;
            
            string message;
            if (isCaptiveCurPlayer)
            {
                message = $"{person.ColorName}被{executeForce.ColorName}斩杀了！";
                _AddTextMessage(message, person.BelongForce, 0, 0);
            }
            else if (isExecuteCurPlayer)
            {
                message = $"我方斩杀了{person.ColorName}！";
                _AddTextMessage(message, executeForce, 0, 0);
            }
        }

        /// <summary>
        /// 俘虏被斩杀事件处理方法
        /// </summary>
        /// <param name="person">被斩杀的俘虏</param>
        /// <param name="executeForce">斩杀方势力</param>
        private void OnPersonLevelUp(Person person)
        {
            // 检查是否为当前玩家势力
            string message;
            if (person.IsCurPlayer)
            {
                message = $"{person.ColorName}升级到{person.Level.Id}级！";
                int x, y = 0;
                if(person.BelongTroop != null)
                {
                    x = person.BelongTroop.x;
                    y = person.BelongTroop.y;
                }
                else
                {
                    x = person.CurrentCity.x;
                    y = person.CurrentCity.y;
                }

                _AddTextMessage(message, person.BelongForce, x, y);
            }
        }

        /// <summary>
        /// 俘虏被斩杀事件处理方法
        /// </summary>
        /// <param name="person">被斩杀的俘虏</param>
        /// <param name="executeForce">斩杀方势力</param>
        private void OnPersonUpgradeOfficial(Person person, Official official)
        {
            // 检查是否为当前玩家势力
            string message;
            if (person.IsCurPlayer)
            {
                message = $"{person.ColorName}官职升到[{person.Official.Name}]";
                _AddTextMessage(message, person.BelongForce, 0, 0);
            }
        }

        /// <summary>
        /// 俘虏被斩杀事件处理方法
        /// </summary>
        /// <param name="person">被斩杀的俘虏</param>
        /// <param name="executeForce">斩杀方势力</param>
        private void OnForceFall(Force force, City city, Troop troop)
        {
            // 检查是否为当前玩家势力
            string message = $"{force.ColorName}势力灭亡!!!!";
            _AddTextMessage(message, force, 0, 0);
        }
    }
}
