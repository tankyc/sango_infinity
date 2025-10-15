using UnityEngine;
using UnityEngine.EventSystems;

namespace Sango.Tools
{
    /// <summary>
    /// 笔刷基类BrushBase，其他具体画笔工具类的基类，提供了通用的框架和接口
    /// </summary>
    public class BrushBase
    {
        /// <summary>
        /// 对应当前的地图编辑器实例，用于在各种操作中与地图编辑器进行交互
        /// </summary>
        protected MapEditor editor;

        /// <summary>
        /// 构造函数，用于初始化BrushBase实例，将传入的地图编辑器实例赋值给editor字段
        /// </summary>
        /// <param name="e">地图编辑器实例</param>
        public BrushBase(MapEditor e)
        {
            editor = e;
        }

        /// <summary>
        /// 虚拟方法，需要根据具体的画笔功能来定义如何修改地图，例如绘制地形、放置物体等
        /// </summary>
        /// <param name="center">操作的中心位置，通常是鼠标在地图上的点击位置或相关坐标</param>
        /// <param name="map">地图对象，可能包含地图的各种属性和数据，用于在修改操作中获取或更新地图信息</param>
        public virtual void Modify(Vector3 center, MapEditor map)
        {

        }

        /// <summary>
        /// 虚拟方法，可以根据不同的季节来调整画笔的行为或地图的显示效果等
        /// </summary>
        /// <param name="curSeason">当前季节的整数值，具体数值的定义可能在其他地方确定</param>
        public virtual void OnSeasonChanged(int curSeason)
        {

        }

        /// <summary>
        /// 虚拟方法，可以用于在场景视图中显示画笔的作用范围、方向等辅助信息，方便开发者调试和观察
        /// </summary>
        /// <param name="center">绘制Gizmos的中心位置，通常与画笔的操作位置相关</param>
        public virtual void DrawGizmos(Vector3 center)
        {
        }

        /// <summary>
        /// 虚拟方法，当开始使用画笔时，可以进行一些资源的加载或状态的初始化
        /// </summary>
        public virtual void OnEnter()
        {

        }

        /// <summary>
        /// 记录上一次操作的中心位置，初始化为零向量。用于在Update方法中判断位置是否发生变化，以便决定是否执行某些操作
        /// </summary>
        protected Vector3 lastCenter = Vector3.zero;

        /// <summary>
        /// 虚拟方法，用于判断当前鼠标指针是否在UI上。如果鼠标在UI上，可能会影响画笔的操作，例如在UI上时不进行地图修改等操作
        /// </summary>
        /// <returns>如果鼠标在UI上，返回true；否则返回false</returns>
        public virtual bool IsPointerOverUI()
        {
            return EditorWindow.IsPointOverUI() || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        }
		
        UnityEngine.Rect labRect = new UnityEngine.Rect();
		
        /// <summary>
        /// 更新方法，用于处理画笔的实时操作
        /// </summary>
        public virtual void Update()
        {
            // 创建从相机位置出发的射线
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            // 检查射线是否与场景中的某个物体相交
            RaycastHit hit;
            // 如果相交，并且鼠标没有在UI上，同时按下左控制键或者鼠标左键，那么就会调用Modify方法修改地图，并更新lastCenter的值
            if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
            {
                if (hit.point != lastCenter)
                {
                    //if (Event.current!= null)
                    {
                        if (!IsPointerOverUI() &&
                        ((Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButton(0) ) || Input.GetMouseButtonDown(0)))
                        {
                            Modify(hit.point, editor);
                            lastCenter = hit.point;
                        }
                        DrawGizmos(hit.point);
                    }
                }
            }
        }

        /// <summary>
        /// 虚拟方法，用于在地图编辑工具中响应笔刷类型的变化
        /// 当笔刷类型发生改变时，可以在子类中重写此方法来执行相应的操作，例如更新画笔的显示、调整操作逻辑等
        /// </summary>
        public virtual void OnBrushTypeChange() { }

        /// <summary>
        /// 虚拟方法，用于在地图编辑工具中响应笔刷大小的变化
        /// 当笔刷大小改变时，可以在子类中重写此方法来更新与笔刷大小相关的参数或操作，例如调整绘制范围、影响量等
        /// </summary>
        public virtual void OnBrushSizeChange() { }

        /// <summary>
        /// 虚拟方法，用于在地图编辑工具中响应笔刷透明度的变化
        /// 当笔刷透明度改变时，可以在子类中重写此方法来调整绘制效果的透明度，例如使绘制的物体半透明或改变其显示强度
        /// </summary>
        public virtual void OnBrushOpacityChange() { }

        /// <summary>
        /// 虚拟方法，用于清除之前的绘制或者修改操作
        /// 子类可以重写此方法来实现具体的清除逻辑，例如删除之前绘制的地形、移除放置的物体等
        /// </summary>
        public virtual void Clear() { }

        /// <summary>
        /// 虚拟方法，用于在GUI上进行绘制或者交互操作
        /// 可以在子类中重写此方法来创建自定义的GUI界面元素，例如显示画笔的设置选项、提供操作按钮等，以便用户与画笔工具进行交互
        /// </summary>
        public virtual void OnGUI() { }
    }
}