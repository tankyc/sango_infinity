﻿/*
 * 文件名：GameController.cs
 * 描述：游戏控制器类，处理游戏中的输入事件和相机控制
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Render;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sango.Core
{
    /// <summary>
    /// 游戏控制器类，处理游戏中的输入事件和相机控制
    /// </summary>
    public class GameController : Singleton<GameController>
    {
        /// <summary>
        /// 是否启用控制器
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 是否启用键盘移动
        /// </summary>
        public bool KeyboardMoveEnabled { get; set; }

        /// <summary>
        /// 是否启用视角旋转
        /// </summary>
        public bool RotateViewEnabled { get; set; }

        /// <summary>
        /// 是否启用拖动移动视角
        /// </summary>
        public bool DragMoveViewEnabled { get; set; }

        /// <summary>
        /// 是否启用缩放视角
        /// </summary>
        public bool ZoomViewEnabled { get; set; }

        /// <summary>
        /// 是否启用边界移动视角
        /// </summary>
        public bool BorderMoveViewEnabled { get; set; }

        /// <summary>
        /// 控制类型枚举
        /// </summary>
        enum ControlType : byte
        {
            /// <summary>
            /// 无控制
            /// </summary>
            None = 0,
            /// <summary>
            /// 移动控制
            /// </summary>
            Move,
            /// <summary>
            /// 旋转控制
            /// </summary>
            Rotate,
        }

        /// <summary>
        /// 当前控制类型
        /// </summary>
        ControlType controlType = ControlType.None;

        //public delegate void OnClickCell(Cell cell);
        //public delegate void OnDoubleClickCell(Cell cell);

        /// <summary>
        /// 单元格进入事件委托
        /// </summary>
        public delegate void OnCellOverEnter(Cell cell);

        /// <summary>
        /// 单元格退出事件委托
        /// </summary>
        public delegate void OnCellOverExit(Cell cell);

        /// <summary>
        /// 单元格停留事件委托
        /// </summary>
        public delegate void OnCellOverStay(Cell cell, Vector3 hitPoint, bool isOverUI);

        /// <summary>
        /// 取消处理事件委托
        /// </summary>
        public delegate void OnCancelHandle();

        /// <summary>
        /// 点击处理事件委托
        /// </summary>
        public delegate void OnClickHandle(Cell cell);

        //public delegate void OnKeyDown(KeyCode keyCode);

        //public OnClickCell onClickCell;
        //public OnDoubleClickCell onDoubleClickCell;

        /// <summary>
        /// 单元格进入事件
        /// </summary>
        public OnCellOverEnter onCellOverEnter;

        /// <summary>
        /// 单元格退出事件
        /// </summary>
        public OnCellOverExit onCellOverExit;

        /// <summary>
        /// 单元格停留事件
        /// </summary>
        public OnCellOverStay onCellOverStay;

        /// <summary>
        /// 取消处理事件
        /// </summary>
        public OnCancelHandle onCancelHandle;

        /// <summary>
        /// 点击处理事件
        /// </summary>
        public OnClickHandle onClickHandle;

        /// <summary>
        /// 右键点击处理事件
        /// </summary>
        public OnClickHandle onRClickHandle;

        /// <summary>
        /// 视角平面
        /// </summary>
        private Plane viewPlane = new Plane(Vector3.up, Vector3.zero);

        /// <summary>
        /// 构造函数
        /// </summary>
        public GameController()
        {
            KeyboardMoveEnabled = true;
            RotateViewEnabled = true;
            DragMoveViewEnabled = true;
            ZoomViewEnabled = true;
            BorderMoveViewEnabled = true;
        }

        /// <summary>
        /// 重置控制器设置
        /// </summary>
        public void Reset()
        {
            KeyboardMoveEnabled = true;
            RotateViewEnabled = true;
            DragMoveViewEnabled = true;
            ZoomViewEnabled = true;
            BorderMoveViewEnabled = true;
        }

        /// <summary>
        /// 检查是否在UI上
        /// </summary>
        /// <returns>是否在UI上</returns>
        public bool IsOverUI()
        {
            return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())/* || FairyGUI.Stage.isTouchOnUI*/;
        }

        /// <summary>
        /// 检查是否在UI上
        /// </summary>
        /// <param name="fingerId">触摸ID</param>
        /// <returns>是否在UI上</returns>
        public bool IsOverUI(int fingerId)
        {
            return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId))/* || FairyGUI.Stage.isTouchOnUI*/;
        }

        /// <summary>
        /// 鼠标悬停的单元格
        /// </summary>
        public Cell mouseOverCell;

        /// <summary>
        /// 触摸开始的单元格
        /// </summary>
        public Cell touchBeganCell;

        /// <summary>
        /// 点击位置
        /// </summary>
        public Vector3 clickPosition;

        /// <summary>
        /// 射线
        /// </summary>
        private Ray ray;

        /// <summary>
        /// 射线命中
        /// </summary>
        private RaycastHit hit;

        /// <summary>
        /// 射线投射层
        /// </summary>
        private int rayCastLayer = LayerMask.GetMask("Map");

        /// <summary>
        /// 检查鼠标是否在地图单元格上
        /// </summary>
        /// <param name="ray">射线</param>
        /// <param name="hitPoint">命中点</param>
        /// <returns>单元格</returns>
        public Cell CheckMouseIsOnMapCell(Ray ray, out Vector3 hitPoint)
        {
            if (Physics.Raycast(ray, out hit, 2000, rayCastLayer))
            {
                hitPoint = hit.point;
                return Scenario.Cur.Map.GetCell(hitPoint);
            }
            hitPoint = Vector3.zero;
            return null;
        }

        /// <summary>
        /// 检查鼠标是否在地图单元格上
        /// </summary>
        /// <param name="mousePosition">鼠标位置</param>
        /// <param name="hitPoint">命中点</param>
        /// <returns>单元格</returns>
        public Cell CheckMouseIsOnMapCell(Vector3 mousePosition, out Vector3 hitPoint)
        {
            ray = Camera.main.ScreenPointToRay(mousePosition);
            return CheckMouseIsOnMapCell(ray, out hitPoint);
        }

        /// <summary>
        /// 触摸位置数组
        /// </summary>
        private Vector2[] touchPos = new Vector2[2];

        /// <summary>
        /// 是否正在拖动
        /// </summary>
        bool isDragMoving = false;

        /// <summary>
        /// 是否正在旋转
        /// </summary>
        bool isRotateMoving = false;

        /// <summary>
        /// 旋转位置
        /// </summary>
        Vector3 rotatePosition;

        /// <summary>
        /// 拖动位置
        /// </summary>
        Vector3 dragPosition;

        /// <summary>
        /// 世界平面拖动位置
        /// </summary>
        Vector3 worldPlaneDragPosition;

        /// <summary>
        /// 处理单元格悬停
        /// </summary>
        public void HandleOverCell()
        {
            if (Scenario.Cur == null) return;
            if (Scenario.Cur.Map == null) return;

            Cell overCell = CheckMouseIsOnMapCell(Input.mousePosition, out Vector3 hitPoint);
            if (overCell != mouseOverCell)
            {
                if (mouseOverCell != null)
                    onCellOverExit?.Invoke(mouseOverCell);
                mouseOverCell = overCell;
                if (mouseOverCell != null)
                    onCellOverEnter?.Invoke(mouseOverCell);
            }
            else if (overCell == null && overCell == mouseOverCell)
            {
                onCellOverExit?.Invoke(overCell);
            }
            else
            {
                onCellOverStay?.Invoke(overCell, hitPoint, IsOverUI());
            }
        }

        /// <summary>
        /// 点击安全距离平方
        /// </summary>
        float clickSafeSqrDistance = 100;

        /// <summary>
        /// 处理Windows事件
        /// </summary>
        public void HandleWindowsEvent()
        {
            if (Input.GetMouseButton(0) && !isRotateMoving)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    bool isOverUI = IsOverUI();

                    clickPosition = Input.mousePosition;
                    GameSystemManager.Instance.HandleEvent(CommandEventType.ClickDown, mouseOverCell, clickPosition, isOverUI);

                    if (controlType != ControlType.None)
                        return;

                    dragPosition = Input.mousePosition;

                    if (isOverUI)
                    {
                        isDragMoving = true;
                        return;
                    }

                    if (mouseOverCell == null) return;

                    if (!mouseOverCell.IsEmpty())
                        return;

                    //downSelectMapObject = CheckMouseIsOnMapObject(Input.mousePosition, out Vector3 hitPiont);
                    //if (downSelectMapObject != null)
                    //    return;

                    controlType = ControlType.Move;
                    ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    float dis;
                    if (viewPlane.Raycast(ray, out dis))
                        worldPlaneDragPosition = ray.GetPoint(dis);
                }
                else
                {
                    if (controlType != ControlType.Move)
                        return;

                    if (dragPosition != Input.mousePosition && (Input.mousePosition - dragPosition).sqrMagnitude >= clickSafeSqrDistance)
                    {
                        isDragMoving = true;

                        if (DragMoveViewEnabled && GameSetting.Instance.MovementMode == 0)
                        {
                            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                            float dis;
                            if (viewPlane.Raycast(ray, out dis))
                            {
                                Vector3 newDragPos = ray.GetPoint(dis);
                                Vector3 offset = worldPlaneDragPosition - newDragPos;
                                MapRender.Instance.OffsetCamera(offset);
                            }
                        }
                        dragPosition = Input.mousePosition;
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0) && !isRotateMoving)
            {
                //if (controlType != ControlType.Move)
                //    return;
                controlType = ControlType.None;
                bool isOverUI = IsOverUI();
                clickPosition = Input.mousePosition;

                if (isDragMoving)
                {
                    isDragMoving = false;
                    GameSystemManager.Instance.HandleEvent(CommandEventType.ClickUp, mouseOverCell, clickPosition, isOverUI);
                    return;
                }
                OnClickWorld(isOverUI);
                GameSystemManager.Instance.HandleEvent(CommandEventType.ClickUp, mouseOverCell, clickPosition, isOverUI);

            }
            else if (Input.GetMouseButton(1) && !isDragMoving)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    bool isOverUI = IsOverUI();
                    clickPosition = Input.mousePosition;
                    GameSystemManager.Instance.HandleEvent(CommandEventType.RClickDown, mouseOverCell, clickPosition, isOverUI);

                    rotatePosition = Input.mousePosition;
                    if (controlType != ControlType.None)
                        return;

                    if (isOverUI) return;

                    if (mouseOverCell != null && !mouseOverCell.IsEmpty())
                        return;

                    //downSelectMapObject = CheckMouseIsOnMapObject(Input.mousePosition, out Vector3 hitPiont);
                    //if (downSelectMapObject != null)
                    //    return;

                    controlType = ControlType.Rotate;
                }
                else
                {
                    if (controlType != ControlType.Rotate)
                        return;

                    if (rotatePosition != Input.mousePosition)
                    {
                        isRotateMoving = true;
                        if (RotateViewEnabled)
                        {
                            Vector3 delta = Input.mousePosition - rotatePosition;
                            MapRender.Instance.RotateCamera(delta);
                        }
                        rotatePosition = Input.mousePosition;
                    }
                }
            }
            else if (Input.GetMouseButtonUp(1) && !isDragMoving)
            {
                bool isOverUI = IsOverUI();
                clickPosition = Input.mousePosition;

                if (controlType != ControlType.Rotate)
                {
                    GameSystemManager.Instance.HandleEvent(CommandEventType.RClick, mouseOverCell, clickPosition, isOverUI);
                    GameSystemManager.Instance.HandleEvent(CommandEventType.RClickUp, mouseOverCell, clickPosition, isOverUI);
                    return;
                }

                controlType = ControlType.None;

                if (isRotateMoving)
                {
                    isRotateMoving = false;
                    GameSystemManager.Instance.HandleEvent(CommandEventType.RClickUp, mouseOverCell, clickPosition, isOverUI);
                    return;
                }
                OnRClickWorld();
                GameSystemManager.Instance.HandleEvent(CommandEventType.RClickUp, mouseOverCell, clickPosition, isOverUI);
            }
            else
            {
                if (ZoomViewEnabled)
                {
                    Vector2 scrollWheel = Input.mouseScrollDelta;
                    if (scrollWheel.y != 0)
                    {
                        MapRender.Instance.ZoomCamera(scrollWheel.y);
                    }
                }
            }
        }

        /// <summary>
        /// 处理移动事件
        /// </summary>
        public void HandleMobileEvent()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    bool isOverUI = IsOverUI(touch.fingerId);
                    GameSystemManager.Instance.HandleEvent(CommandEventType.ClickDown, mouseOverCell, dragPosition, isOverUI);

                    dragPosition = touch.position;
                    isDragMoving = false;

                    if (isOverUI)
                    {
                        isDragMoving = true;
                        return;
                    }

                    touchBeganCell = CheckMouseIsOnMapCell(touch.position, out Vector3 hitPoint);
                    if (touchBeganCell != null && !touchBeganCell.IsEmpty())
                    {
                        return;
                    }

                    controlType = ControlType.Move;
                    ray = Camera.main.ScreenPointToRay(touch.position);
                    float dis;
                    if (viewPlane.Raycast(ray, out dis))
                        worldPlaneDragPosition = ray.GetPoint(dis);
                }
                else if (touch.phase == TouchPhase.Moved)
                {
                    if (controlType != ControlType.Move)
                        return;

                    if (!dragPosition.Equals(touch.position))
                    {
                        Vector3 touchPos = touch.position;
                        if ((dragPosition - touchPos).sqrMagnitude >= clickSafeSqrDistance)
                        {
                            isDragMoving = true;

                            if (DragMoveViewEnabled)
                            {
                                ray = Camera.main.ScreenPointToRay(touch.position);
                                float dis;
                                if (viewPlane.Raycast(ray, out dis))
                                {
                                    Vector3 newDragPos = ray.GetPoint(dis);
                                    Vector3 offset = worldPlaneDragPosition - newDragPos;
                                    MapRender.Instance.OffsetCamera(offset);
                                }
                            }
                            dragPosition = touch.position;
                        }
                    }
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    //if (controlType != ControlType.Move)
                    //    return;

                    bool isOverUI = IsOverUI(touch.fingerId);

                    controlType = ControlType.None;
                    if (isDragMoving)
                    {
                        isDragMoving = false;
                        GameSystemManager.Instance.HandleEvent(CommandEventType.ClickUp, mouseOverCell, dragPosition, isOverUI);
                        return;
                    }

                    touchBeganCell = CheckMouseIsOnMapCell(touch.position, out Vector3 hitPoint);
                    if (touchBeganCell != mouseOverCell)
                    {
                        if (mouseOverCell != null)
                            onCellOverExit?.Invoke(mouseOverCell);
                        mouseOverCell = touchBeganCell;
                        if (mouseOverCell != null)
                            onCellOverEnter?.Invoke(mouseOverCell);
                    }
                    else if (touchBeganCell != null && touchBeganCell == mouseOverCell)
                    {
                        onCellOverExit?.Invoke(touchBeganCell);
                    }
                    clickPosition = touch.position;
                    OnClickWorld(isOverUI);
                    GameSystemManager.Instance.HandleEvent(CommandEventType.ClickUp, mouseOverCell, dragPosition, isOverUI);

                }
            }
            else if (Input.touchCount == 2)
            {
                Touch touch1 = Input.GetTouch(0);
                Touch touch2 = Input.GetTouch(1);

                if (touch1.phase == TouchPhase.Began || touch2.phase == TouchPhase.Began)
                {
                    // 两只手指操作可以无视界面旋转和缩放相机
                    //if ((touch1.phase == TouchPhase.Began && IsOverUI(touch1.fingerId)) || (touch2.phase == TouchPhase.Began && IsOverUI(touch2.fingerId)))
                    //{
                    //    controlType = ControlType.None;
                    //    return;
                    //}
                    touchPos[0] = touch1.position;
                    touchPos[1] = touch2.position;
                    controlType = ControlType.Rotate;
                }
                // 需要先检测,不然会由于有一个是Move而导致检测不到End
                else if (touch1.phase == TouchPhase.Ended || touch2.phase == TouchPhase.Ended /*|| touch1.phase == TouchPhase.Canceled || touch2.phase == TouchPhase.Canceled*/)
                {
                    // 返回单手指移动
                    controlType = ControlType.Move;

                    // 不再触发Click
                    isDragMoving = true;

                    Vector3 touchPosition;
                    if (touch1.phase != TouchPhase.Ended /*&& touch1.phase != TouchPhase.Canceled*/)
                    {
                        touchPosition = touch1.position;
                    }
                    else
                    {
                        touchPosition = touch2.position;
                    }
                    ray = Camera.main.ScreenPointToRay(touchPosition);
                    float dis;
                    if (viewPlane.Raycast(ray, out dis))
                        worldPlaneDragPosition = ray.GetPoint(dis);

                    if (isRotateMoving)
                    {
                        isRotateMoving = false;
                        return;
                    }

                    // 移动端不提供右键
                    //OnRClickWorld();
                }
                else if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
                {
                    //if (controlType != ControlType.Rotate)
                    //    return;

                    isRotateMoving = true;
                    Vector2 touch1Dirction = touch1.position - touchPos[0];
                    Vector2 touch2Dirction = touch2.position - touchPos[1];
                    float dotAngle = Vector2.Dot(touch1Dirction, touch2Dirction);
                    if (dotAngle > 0)
                    {
                        // rotate
                        if (RotateViewEnabled)
                            MapRender.Instance.RotateCamera(touch1.deltaPosition);
                    }
                    else
                    {
                        float len = (touch1.position - touch2.position).sqrMagnitude;
                        float srcLen = (touchPos[0] - touchPos[1]).sqrMagnitude;
                        float delta = Mathf.Max(Mathf.Abs(touch2.deltaPosition.x), Mathf.Abs(touch1.deltaPosition.x));
                        if (len < srcLen)
                            delta = -delta;
                        if (ZoomViewEnabled)
                            MapRender.Instance.ZoomCamera(delta / 100f);
                    }
                    touchPos[0] = touch1.position;
                    touchPos[1] = touch2.position;
                }
            }
        }

        /// <summary>
        /// 按键标志数组
        /// </summary>
        bool[] keyFlags = new bool[4];

        /// <summary>
        /// 是否有按键按下
        /// </summary>
        bool hasKey = false;

        /// <summary>
        /// 键盘移动相机
        /// </summary>
        /// <returns>是否有按键按下</returns>
        private bool MoveCameraKeyBoard()
        {
            if (!KeyboardMoveEnabled) return true;

            if (hasKey)
            {
                Array.Clear(keyFlags, 0, 4);
                hasKey = false;
            }

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))//(Input.GetAxis("Horizontal")<0)
            {
                keyFlags[0] = true;
                hasKey = true;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                keyFlags[1] = true;
                hasKey = true;
            }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                keyFlags[2] = true;
                hasKey = true;
            }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                keyFlags[3] = true;
                hasKey = true;
            }

            if (hasKey)
            {
                MapRender.Instance.MoveCameraKeyBoard(keyFlags);
                if (controlType == ControlType.Move)
                    controlType = ControlType.None;
            }

            return hasKey;
        }

        /// <summary>
        /// 边界宽度
        /// </summary>
        float boderWidth = 10;

        /// <summary>
        /// 处理边界移动相机
        /// </summary>
        public void HandleBorderMoveCamera()
        {
#if UNITY_EDITOR || !UNITY_STANDALONE_WIN
            return;
#endif

            if (!BorderMoveViewEnabled || GameSetting.Instance.MovementMode == 0) return;

            int sWidth = UnityEngine.Screen.width;
            int sHeight = UnityEngine.Screen.height;

            if (hasKey)
            {
                Array.Clear(keyFlags, 0, 4);
                hasKey = false;
            }

            // 左下0,0 右上max max
            // 上 height > 
            if (Input.mousePosition.y > sHeight - boderWidth)
            {
                // 上
                keyFlags[2] = true;
                hasKey = true;
            }
            else if (Input.mousePosition.y < boderWidth)
            {
                // 下
                keyFlags[3] = true;
                hasKey = true;
            }

            if (Input.mousePosition.x > sWidth - boderWidth)
            {
                // 右
                keyFlags[1] = true;
                hasKey = true;
            }
            else if (Input.mousePosition.x < boderWidth)
            {
                // 左
                keyFlags[0] = true;
                hasKey = true;
            }

            if (hasKey)
            {
                MapRender.Instance.MoveCameraKeyBoard(keyFlags);
                if (controlType == ControlType.Move)
                    controlType = ControlType.None;
            }

        }

        /// <summary>
        /// 更新
        /// </summary>
        public void Update()
        {
            GameSystemManager.Instance.Update();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            HandleOverCell();
#endif
            if (!Enabled) return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            HandleWindowsEvent();
            MoveCameraKeyBoard();
            HandleBorderMoveCamera();
#else
            HandleMobileEvent();
#endif

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnCancel();
            }

        }

        /// <summary>
        /// 点击世界
        /// </summary>
        public void OnClickWorld(bool isOverUI)
        {
            if (onClickHandle != null && mouseOverCell != null)
            {
                onClickHandle.Invoke(mouseOverCell);
                return;
            }
            if (mouseOverCell != null)
                GameSystemManager.Instance.HandleEvent(CommandEventType.Click, mouseOverCell, clickPosition, isOverUI);
        }

        /// <summary>
        /// 右键点击世界
        /// </summary>
        public void OnRClickWorld()
        {
            if (onRClickHandle != null && mouseOverCell != null)
            {
                onRClickHandle.Invoke(mouseOverCell);
                return;
            }

            if (mouseOverCell != null)
                GameSystemManager.Instance.HandleEvent(CommandEventType.RClick, mouseOverCell, clickPosition, IsOverUI());
        }

        /// <summary>
        /// 取消操作
        /// </summary>
        public void OnCancel()
        {
            if (onCancelHandle != null)
            {
                onCancelHandle.Invoke();
                return;
            }

            GameSystemManager.Instance.HandleEvent(CommandEventType.Cancel, null, clickPosition, false);
            //Sango.Render.UI.ContextMenu.Close();
        }

    }
}
