using Sango.Core.Action;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 模块管理器
    /// </summary>
    public class GameSystemManager : Singleton<GameSystemManager>
    {
        /// <summary>
        /// 模块集合
        /// </summary>
        public Dictionary<string, GameSystem> systemMap = new Dictionary<string, GameSystem>();
        static System.Collections.Generic.List<System.Type> GetAllSubTypes(System.Type aBaseClass)
        {
            var result = new System.Collections.Generic.List<System.Type>();
            System.Reflection.Assembly[] AS = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var A in AS)
            {
                try
                {
                    System.Type[] types = A.GetTypes();
                    foreach (var T in types)
                    {
                        if (T.IsSubclassOf(aBaseClass))
                            result.Add(T);
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException) {; }
            }
            return result;
        }
        class systemData
        {
            public System.Type t;
            public GameSystemAttribute attribute;
        }
        /// <summary>
        /// 这里会自动创建所有GameSystem子类的单例
        /// </summary>
        public void Init()
        {
            Type genericType = typeof(Singleton<>);
            List<System.Type> modules = GetAllSubTypes(typeof(GameSystem));
            List<systemData> moduleDatas = new List<systemData>();
            foreach (System.Type type in modules)
            {
                GameSystemAttribute moduleAttribute = type.GetCustomAttribute<GameSystemAttribute>(false);
                if (moduleAttribute == null)
                    continue;

                if (string.IsNullOrEmpty(moduleAttribute.nickName))
                    moduleAttribute.nickName = type.Name;

                systemData data = new systemData() { attribute = moduleAttribute, t = type };
                systemData exsist = moduleDatas.Find(x => x.attribute.nickName == moduleAttribute.nickName);
                if (exsist != null)
                {
                    if (exsist.attribute.order < data.attribute.order)
                    {
                        moduleDatas.Remove(exsist);
                        moduleDatas.Add(data);
                    }
                }
                else
                    moduleDatas.Add(data);
            }
            moduleDatas.Sort((a, b) => a.attribute.order.CompareTo(b.attribute.order));

            for (int i = 0; i < moduleDatas.Count; ++i)
            {
                systemData moduleData = moduleDatas[i];
                Type specificType = genericType.MakeGenericType(moduleData.t);
                PropertyInfo propertyInfo = specificType.GetProperty("Instance");
                // 获取并打印属性的值（对于静态属性，传递null作为实例）
                GameSystem value = propertyInfo.GetValue(null) as GameSystem; // 对于静态属性，此处为null
                if (value != null)
                {
                    if (moduleData.attribute.autoInit)
                        value.Init();
                    systemMap.Add(moduleData.attribute.nickName, value);
                }
            }
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        /// <param name="name"></param>
        /// <param name="system"></param>
        public void Register(string name, GameSystem system)
        {
            if (systemMap.TryGetValue(name, out GameSystem value))
                value.Clear();
            systemMap[name] = system;
        }

        /// <summary>
        /// 取消模块
        /// </summary>
        /// <param name="name"></param>
        /// <param name="system"></param>
        public void Unregister(string name)
        {
            if (systemMap.TryGetValue(name, out GameSystem value))
            {
                value.Clear();
                systemMap.Remove(name);
            }
        }

        public T GetSystem<T>(string name) where T : GameSystem
        {
            if (systemMap.TryGetValue(name, out GameSystem value))
                return value as T;
            return null;
        }

        public T GetSystem<T>() where T : GameSystem
        {
            string name = typeof(T).Name;
            if (systemMap.TryGetValue(name, out GameSystem value))
                return value as T;
            return null;
        }

        public T GetProperty<T>(string name, string propertName) where T : struct
        {
            if (systemMap.TryGetValue(name, out GameSystem value))
                return value.GetProperty<T>(propertName);

            return default(T);
        }

        public void PostMessage(string name, string message, params object[] objects)
        {
            if (systemMap.TryGetValue(name, out GameSystem value))
                value.OnMessage(message, objects);
        }

        List<ICommandEvent> commads = new List<ICommandEvent>();
        public ICommandEvent CurrentCommand { get; private set; }
        public void Push(ICommandEvent command)
        {
            if (CurrentCommand == null)
            {
                GameController.Instance.KeyboardMoveEnabled = false;
                GameController.Instance.RotateViewEnabled = false;
                GameController.Instance.DragMoveViewEnabled = false;
                GameController.Instance.ZoomViewEnabled = false;
                GameController.Instance.BorderMoveViewEnabled = false;
                GameEvent.OnSystemStart?.Invoke();
            }

            commads.Insert(0, command);
            CurrentCommand?.OnExit();
            CurrentCommand = command;
            command.OnEnter();
        }

        public bool BackTo(ICommandEvent commandEvent)
        {
            if (commads.Contains(commandEvent))
            {
                while (CurrentCommand != null && CurrentCommand != commandEvent)
                {
                    Back();
                }
                return true;
            }
            return false;
        }

        public void Back(ICommandEvent who = null)
        {
            if (who == null || who == CurrentCommand)
            {
                if (commads.Count == 0) return;

                ICommandEvent command = commads[0];
                commads.RemoveAt(0);
                command.OnDestroy();
                if (commads.Count > 0)
                {
                    CurrentCommand = commads[0];
                    CurrentCommand.OnBack(command);
                }
                else
                {
                    CurrentCommand = null;
                    GameController.Instance.KeyboardMoveEnabled = true;
                    GameController.Instance.RotateViewEnabled = true;
                    GameController.Instance.DragMoveViewEnabled = true;
                    GameController.Instance.ZoomViewEnabled = true;
                    GameController.Instance.BorderMoveViewEnabled = true;
                    GameEvent.OnSystemEnd?.Invoke();
                }
            }
            else
            {
                for (int i = 0; i < commads.Count; i++)
                {
                    ICommandEvent command = commads[i];
                    if (command == who)
                    {
                        commads.RemoveAt(i);
                        command.OnDestroy();
                        break;
                    }
                }
            }
        }
        public void Done(ICommandEvent who = null)
        {
            if (who == null || who == CurrentCommand)
            {
                CurrentCommand = null;
                while (commads.Count > 0)
                {
                    commads[0].OnDone();
                    commads.RemoveAt(0);
                }
                GameController.Instance.KeyboardMoveEnabled = true;
                GameController.Instance.RotateViewEnabled = true;
                GameController.Instance.DragMoveViewEnabled = true;
                GameController.Instance.ZoomViewEnabled = true;
                GameController.Instance.BorderMoveViewEnabled = true;
            }
            else
            {
                for (int i = 0; i < commads.Count; i++)
                {
                    ICommandEvent command = commads[i];
                    if (command == who)
                    {
                        command.OnDone();
                        int count = commads.Count;
                        for (int j = i + 1; j < count; j++)
                        {
                            command = commads[j];
                            command.OnDone();
                        }
                        commads.RemoveRange(i, count - i);
                        break;
                    }
                }
            }
        }
        public void Update()
        {
            CurrentCommand?.Update();
        }

        Cell RClickCell;

        public void HandleEvent(CommandEventType eventType, Cell clickCell, Vector3 clickPosition, bool isOverUI)
        {
            if (eventType == CommandEventType.RClickDown)
                RClickCell = clickCell;

            if (CurrentCommand != null)
            {
                CurrentCommand.HandleEvent(eventType, clickCell, clickPosition, isOverUI);
                return;
            }

            switch (eventType)
            {
                case CommandEventType.Click:
                    {
                        GameEvent.OnClick?.Invoke(clickCell, clickPosition, isOverUI);
                    }
                    break;
                case CommandEventType.RClick:
                    {
                        GameEvent.OnRClick?.Invoke(clickCell, clickPosition, isOverUI, RClickCell);
                    }
                    break;
                //case CommandEventType.RClickUp:
                //    {
                //        if (RClickCell == clickCell && !clickCell.IsEmpty())
                //            GameEvent.OnRClickObject?.Invoke(clickCell, clickPosition, isOverUI);
                //    }
                //    break;
                case CommandEventType.RClickDown:
                    {

                    }
                    break;
                case CommandEventType.Cancel:
                    {
                        GameEvent.OnCancel?.Invoke(clickCell, clickPosition, isOverUI);
                    }
                    break;
            }
        }
    }
}
