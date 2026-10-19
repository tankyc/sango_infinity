using RTEditor;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Tools.UndoRedo
{
    /// <summary>
    /// 模型编辑命令
    /// </summary>
    public class ModelEditCommand : IUndoableCommand
    {
        public class ObjectCreateAction : IUndoableAndRedoableAction, IAction
        {
            /// <summary>
            /// 被创建的地图物件
            /// </summary>
            MapObject _mapObject;

            public ObjectCreateAction(MapObject sourceObject)
            {
                _mapObject = sourceObject;
            }

            /// <summary>
            /// 执行：把物件登记进地图（首次创建时 manager 为空，必须走 AddToMap 才能入册）
            /// </summary>
            public void Execute()
            {
                if (RuntimeEditorApplication.Instance.EnableUndoRedo) EditorUndoRedoSystem.Instance.RegisterAction(this);
                RestoreToMap();
            }

            /// <summary>
            /// 撤销：只把物件移出地图并隐藏，不销毁——重做时还要复用同一个对象
            /// </summary>
            public void Undo()
            {
                RemoveFromMap();
            }

            /// <summary>
            /// 重做：把物件重新登记进地图并显示
            /// </summary>
            public void Redo()
            {
                RestoreToMap();
            }

            /// <summary>
            /// 命令被移出历史栈时调用（超过历史上限被裁剪、清空历史、或重做栈被丢弃）。
            ///
            /// 这里绝不能在"物件仍在地图上"的情况下销毁它：命令只是历史记录，
            /// 而物件本身是地图数据的一部分（保存地图时就是写 dynamicObjects 这张表）。
            /// 以前的实现无条件 Clear + Destroy，导致批量放置植被时——每点一下就会产生一条本命令——
            /// 历史记录一旦超过上限，最早放置的那批植被就会被连 GameObject 带地图数据一起删掉，
            /// 表现就是"刚放上去的植被过一会儿自己消失了，保存下来也没有"。
            /// 只有处于撤销态（已经移出地图、不在 dynamicObjects 里）的物件才需要真正销毁，避免泄漏。
            /// </summary>
            public void OnRemovedFromUndoRedoStack()
            {
                MapObject mapObject = _mapObject;
                _mapObject = null;

                if (mapObject == null || mapObject.gameObject == null)
                    return;

                // 仍在地图数据里：保留物件，直接丢弃命令对它的引用即可
                if (mapObject.isAdded && mapObject.manager != null)
                    return;

                mapObject.Clear();
                GameObject.Destroy(mapObject.gameObject);
            }

            /// <summary>
            /// 把物件登记进地图并显示。
            /// 首次创建时 manager 为空，AddToMap 会完成入册；
            /// 撤销过一次后 manager 已经存在，AddToMap 不再生效，需要补一次入册。
            /// </summary>
            void RestoreToMap()
            {
                if (_mapObject == null) return;

                if (_mapObject.gameObject != null)
                    _mapObject.gameObject.SetActive(true);

                _mapObject.AddToMap(true);

                // 撤销后重做：manager 已存在时 AddToMap 是空操作，这里补回渲染/保存列表
                if (!_mapObject.isAdded && _mapObject.manager != null)
                    _mapObject.manager.AddStatic(_mapObject);
            }

            /// <summary>
            /// 把物件移出地图并隐藏。
            /// 移出动态物件表后，保存地图时就不会再写入它，撤销语义才是完整的。
            /// </summary>
            void RemoveFromMap()
            {
                if (_mapObject == null) return;

                if (_mapObject.manager != null)
                    _mapObject.manager.Remove(_mapObject);

                if (_mapObject.gameObject != null)
                    _mapObject.gameObject.SetActive(false);
            }
        }

        MapEditor editor;
        IUndoableAndRedoableAction urAction;
        string _desc;
        public string Description => _desc;

        /// <summary>
        /// 构造函数（添加模型）
        /// </summary>
        /// <param name="editor">地图编辑器</param>
        /// <param name="mapObject">模型对象</param>
        /// <param name="description">命令描述</param>
        public ModelEditCommand(MapEditor editor, IUndoableAndRedoableAction urAction, string description)
        {
            this.editor = editor;
            this.urAction = urAction;
            this._desc = description;
        }
        
        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute()
        {
            IAction action = urAction as IAction;
            if (action != null)
            {
                action.Execute();
            }
        }
        
        /// <summary>
        /// 撤销命令
        /// </summary>
        public void Undo()
        {
            urAction?.Undo();
        }
        
        /// <summary>
        /// 重做命令
        /// </summary>
        public void Redo()
        {
            urAction?.Redo();
        }

        public void Destroy()
        {
            // 释放资源
            urAction.OnRemovedFromUndoRedoStack();
            editor = null;
            urAction = null;
        }

    }
}