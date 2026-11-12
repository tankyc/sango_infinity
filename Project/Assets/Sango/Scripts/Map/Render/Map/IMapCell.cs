using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 地形块公共接口 —— 两套块实现的唯一交换点。
    ///
    /// 【为什么需要】
    ///   <see cref="MapCellBatch"/>（控制贴图 + Texture2DArray + 共享材质）依赖 2D 纹理数组，
    ///   部分设备 / 图形 API 不支持（<see cref="SystemInfo.supports2DArrayTextures"/> = false），
    ///   必须回退到老实现 <see cref="MapCell"/>（逐图层拆网格）。
    ///   两条实现长期并存，<see cref="MapTerrain.terrainCells"/> 里装的可能是任意一种，
    ///   调用方（MapTerrain / TerrainCellBatchRenderer / 地形笔刷 / 撤销重做）只依赖本接口。
    ///
    /// 【为什么继承 IMapManageObject】
    ///   地形块本来就登记在 <see cref="MapRender"/> 的静态对象表里（map.AddStatic），
    ///   直接继承可以省掉一次接口转换；position / visible / Overlaps / Destroy 等
    ///   公共成员也由它提供，本接口只补充"地形块特有"的部分。
    ///
    /// 【原版无对应实现】Open311 无块级地形渲染，此为渲染层自主设计。
    /// </summary>
    public interface IMapCell : Sango.Core.IMapManageObject
    {
        /// <summary>当前网格精度档位（0 = 原始，l = 步长 1&lt;&lt;l 抽稀）。</summary>
        int lod { get; set; }

        /// <summary>离开视野后的累计时长（秒），由 MapTerrain 驱动，用于空闲卸载网格。</summary>
        float idleTime { get; set; }

        /// <summary>是否已登记进 MapTerrain 的逐帧处理表（防止重复登记）。</summary>
        bool inFrameUpdateList { get; set; }

        /// <summary>创建节点与网格资源（重复调用应当是安全的）。</summary>
        void Init();

        /// <summary>逐帧处理：同步可见性、消费 LOD 待重建标记、应用已完成的异步构建结果。</summary>
        void Update();

        /// <summary>销毁节点与全部运行时资源。</summary>
        void Clear();

        /// <summary>
        /// 按当前档位（重新）构建网格。
        /// </summary>
        /// <param name="needInit">true = 未创建时先建节点；false = 只重建已有网格（笔刷改地形后用）</param>
        void PrepareDatas(bool needInit = true);

        /// <summary>是否已经建出可渲染的网格。</summary>
        bool HasMesh();

        /// <summary>释放网格实体（保留节点），用于离开视野后的空闲卸载。</summary>
        void ReleaseMesh();

        /// <summary>切换精度档位。不可见 / 未建网格时只记待重建标记，等重新可见再补建。</summary>
        /// <param name="lod">精度档位</param>
        void SetLod(int lod);

        /// <summary>切换"是否描边"的物理层（远距离时让 URP 描边 Feature 跳过地形）。</summary>
        /// <param name="noOutline">true 切到 MapNoOutLine 层</param>
        void SetNoOutLineLayer(bool noOutline);

        /// <summary>是否已建过节点（可用于安全调用 PrepareDatas）。</summary>
        bool IsValid();

        /// <summary>是否正在后台异步构建。</summary>
        bool IsInThreadLoad();
    }
}
