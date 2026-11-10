using UnityEngine;
using Sango.Core;
using Sango.Tools;

namespace Sango.Render
{
    /// <summary>
    /// 地图标注类
    /// </summary>
    public class MapLabel : MonoBehaviour, IMapManageObject
    {
        /// <summary>
        /// 地图渲染管理器
        /// </summary>
        public MapRender manager { get; set; }
        /// <summary>
        /// 是否可保存至地图
        /// </summary>
        public bool canSave { get; set; } = false;

        /// <summary>
        /// 边界
        /// </summary>
        public Sango.Tools.Rect bounds { get; set; }

        /// <summary>
        /// 世界边界
        /// </summary>
        public Sango.Tools.Rect worldBounds
        {
            get
            {
                Vector3 pos = position;
                return new Tools.Rect(pos.z + bounds.x - bounds.width / 2,
                pos.x + bounds.y - bounds.height / 2,
                bounds.width, bounds.height);
            }
        }

        /// <summary>
        /// 对象ID
        /// </summary>
        public int objId { get; set; }

        /// <summary>
        /// 对象类型
        /// </summary>
        public int objType { get; set; } = 99; // 标注类型

        /// <summary>
        /// 绑定ID
        /// </summary>
        public int bindId { get; set; }

        /// <summary>
        /// 模型资源
        /// </summary>
        public string modelAsset { get; set; }

        /// <summary>
        /// 是否已经添加
        /// </summary>
        public bool isAdded { get; set; }

        /// <summary>
        /// 模型ID
        /// </summary>
        public int modelId { get; set; }

        /// <summary>
        /// 是否可见
        /// </summary>
        private bool _visible = true;
        public bool visible
        {
            get { return _visible; }
            set
            {
                if (_visible != value)
                {
                    _visible = value;

                    if (_visible)
                    {
                        // 从false变为true，创建资源
                        CreateLabelResource();
                    }
                    else
                    {
                        // 从true变为false，销毁资源
                        DestroyLabelResource();
                    }
                }
            }
        }

        /// <summary>
        /// 是否静态
        /// </summary>
        public bool isStatic { get; set; } = true;

        /// <summary>
        /// 是否可选择
        /// </summary>
        public bool selectable { get; set; } = false;

        /// <summary>
        /// 是否保持在视图中
        /// </summary>
        public bool remainInView { get; set; } = true;

        /// <summary>
        /// 位置（世界坐标）。
        /// 标注本身是一个空节点，<see cref="MapLabelRenderer"/> 每帧读这里的世界坐标去同步视图，
        /// 所以不需要在这里额外通知刷新。
        /// </summary>
        public Vector3 position
        {
            get { return transform.position; }
            set { transform.position = value; }
        }

        /// <summary>
        /// 旋转（对标注显示无影响：视图朝向由 BillboardUI 每帧面向相机决定）
        /// </summary>
        public Vector3 rotation
        {
            get { return transform.rotation.eulerAngles; }
            set { transform.rotation = Quaternion.Euler(value); }
        }

        /// <summary>
        /// 前向方向
        /// </summary>
        public Vector3 forward
        {
            get { return transform.forward; }
            set { transform.forward = value; }
        }

        /// <summary>
        /// 缩放（对标注显示无影响：视图缩放由 BillboardUI 随镜头距离决定）
        /// </summary>
        public Vector3 scale
        {
            get { return transform.localScale; }
            set { transform.localScale = value; }
        }

        /// <summary>
        /// 坐标
        /// </summary>
        public Vector2Int coords { get; set; }

        /// <summary>
        /// 标注文字
        /// </summary>
        public string labelText { get; set; }

        /// <summary>
        /// 文字颜色
        /// </summary>
        public Color32 textColor { get; set; }

        /// <summary>
        /// 字号大小
        /// </summary>
        public int fontSize { get; set; }

        /// <summary>
        /// 是否应该显示。
        ///
        /// 【数据与视图分离】本类只描述"标注是什么"（文字 / 颜色 / 字号 / 坐标），
        /// 真正的 Text 由 <see cref="MapLabelRenderer"/> 创建并统一挂在同一个 World Space Canvas 下。
        /// 因此这里不再持有 <c>TextMesh</c> / <c>labelResource</c> / <c>textComponent</c> 之类视图对象。
        /// </summary>
        public bool CanRenderLabel
        {
            get { return _visible && !string.IsNullOrEmpty(labelText); }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="map">地图渲染器</param>
        /// <param name="text">标注文字</param>
        /// <param name="pos">位置</param>
        /// <param name="color">文字颜色</param>
        /// <param name="size">字号大小</param>
        public void Initialize(MapRender map, string text, Vector3 pos, Color32 color, int size)
        {
            manager = map;
            labelText = text;
            position = pos;
            textColor = color;
            fontSize = size;
            bounds = new Sango.Tools.Rect(0, 0, 20, 20);

            // 如果可见，注册到自绘合批（不再实例化 prefab）
            if (visible)
            {
                CreateLabelResource();
            }
        }

        /// <summary>
        /// 点击事件
        /// </summary>
        public void OnClick() { }

        /// <summary>
        /// 检查是否与矩形重叠
        /// </summary>
        /// <param name="rect">矩形</param>
        /// <returns>是否重叠</returns>
        public bool Overlaps(Sango.Tools.Rect rect)
        {
            return worldBounds.Overlaps(rect);
        }

        /// <summary>
        /// 鼠标进入事件
        /// </summary>
        public void OnPointerEnter() { }

        /// <summary>
        /// 鼠标退出事件
        /// </summary>
        public void OnPointerExit() { }

        /// <summary>
        /// 设置轮廓显示
        /// </summary>
        /// <param name="material">材质</param>
        public void SetOutlineShow(Material material) { }

        /// <summary>
        /// 编辑器显示
        /// </summary>
        /// <param name="b">是否显示</param>
        public void EditorShow(bool b) { }

        /// <summary>
        /// 设置父对象
        /// </summary>
        /// <param name="parent">父对象</param>
        public void SetParent(Transform parent)
        {
            transform.SetParent(parent);
        }

        /// <summary>
        /// 设置父对象
        /// </summary>
        /// <param name="parent">父对象</param>
        /// <param name="worldPositionStays">是否保持世界位置</param>
        public void SetParent(Transform parent, bool worldPositionStays)
        {
            transform.SetParent(parent, worldPositionStays);
        }

        /// <summary>
        /// 销毁对象
        /// </summary>
        public void Destroy()
        {
            // 销毁标注资源
            DestroyLabelResource();

            if (gameObject != null)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        /// <summary>
        /// 获取游戏对象
        /// </summary>
        /// <returns>游戏对象</returns>
        public GameObject GetGameObject()
        {
            return gameObject;
        }

        /// <summary>
        /// 创建模型
        /// </summary>
        /// <param name="meshFile">网格文件</param>
        /// <param name="textureFile">纹理文件</param>
        /// <param name="shaderName">着色器名称</param>
        /// <param name="isShareMat">是否共享材质</param>
        public void CreateModel(string meshFile, string textureFile, string shaderName, bool isShareMat = true) { }

        /// <summary>
        /// 创建模型
        /// </summary>
        /// <param name="packagePath">包路径</param>
        /// <param name="assetName">资源名称</param>
        public void CreateModel(string packagePath, string assetName) { }

        /// <summary>
        /// 创建模型
        /// </summary>
        /// <param name="assetName">资源名称</param>
        public void CreateModel(string assetName) { }

        /// <summary>
        /// 创建模型
        /// </summary>
        /// <param name="modelObj">模型对象</param>
        public void CreateModel(UnityEngine.Object modelObj) { }

        /// <summary>
        /// 更改模型
        /// </summary>
        /// <param name="newAsset">新资源</param>
        public void ChangeModel(string newAsset) { }

        /// <summary>
        /// 重新加载模型
        /// </summary>
        /// <param name="checkAsset">是否检查资源</param>
        public void ReLoadModels(bool checkAsset = true) { }

        /// <summary>
        /// 清除模型
        /// </summary>
        public void ClearModels() { }

        /// <summary>
        /// 重新检查可见性
        /// </summary>
        public void ReCheckVisible() { }

        /// <summary>
        /// 更新标注
        /// </summary>
        /// <param name="text">新的文字</param>
        /// <param name="color">新的颜色</param>
        /// <param name="size">新的字号</param>
        public void UpdateLabel(string text, Color32 color, int size)
        {
            labelText = text;
            textColor = color;
            fontSize = size;

            // 内容（文字 / 颜色 / 字号）变了，让渲染器下一帧重写一次视图
            MapLabelRenderer.MarkDirty();
        }

        /// <summary>
        /// 注册到渲染器（可见时调用）。
        ///
        /// 渲染器会给本标注分配一个视图节点，挂在**所有标注共享**的那个 World Space Canvas 下，
        /// 于是同屏 N 个标注只需要 1 个 DrawCall（同字体同材质，uGUI 自动合批）。
        /// 视图的创建是延迟的（在渲染器的 LateUpdate 里），此时 prefab / Canvas 未必就绪。
        /// </summary>
        private void CreateLabelResource()
        {
            MapLabelRenderer.Register(this);
        }

        /// <summary>
        /// 从渲染器注销（不可见 / 销毁时调用），视图节点退回对象池复用。
        /// </summary>
        private void DestroyLabelResource()
        {
            MapLabelRenderer.Unregister(this);
        }
    }
}