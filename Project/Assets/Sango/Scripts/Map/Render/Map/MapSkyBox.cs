using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Render
{

    public class MapSkyBox : MapProperty
    {
        public float sky_radius_len = 140;
        public float sky_height = 70;
        public float sky_offset = -2f;
        public string sky_material_shader = "Sango/skybox";

        // ---------- 高度融合（世界高度，单位与地图一致） ----------
        /// <summary>高度融合起点：世界高度低于此值完全透明（埋进地形以下）</summary>
        public float sky_blend_start = -200f;
        /// <summary>高度融合终点：世界高度高于此值完全保留</summary>
        public float sky_blend_end = -150f;
        /// <summary>高度融合曲线强度（越大过渡越"陡"，底部越不容易露出天空）</summary>
        public float sky_height_power = 18f;

        // ---------- 深度融合（相机空间距离，米） ----------
        // 默认值与 MapCamera 的远裁剪面口径对齐（400 ~ 1800，起点 ≈ 终点 × 0.26）。
        // 运行中这两个值会被 SyncDepthFadeWithCamera 按实际 farClipPlane 覆盖，
        // 这里给配套默认值只是保证"同步逻辑尚未跑"的那一帧天空盒也可见。
        /// <summary>深度融合起点：场景距离近于此值完全透明（露出真实地形）</summary>
        public float sky_depth_fade_begin = 468f;
        /// <summary>深度融合终点：场景距离远于此值完全不透明（用天空图盖住远景）</summary>
        public float sky_depth_fade_end = 1600f;
        /// <summary>
        /// 深度融合曲线强度（shader 内对"距离在融合区间内的比值"取 pow）。
        /// 1 = 线性；值越大过渡越"陡"（更靠近终点才出现天空），值越小天空越早显现。
        /// 默认 4：配合 400 ~ 1800 的远裁剪面，alpha 由近到远平滑抬起，
        /// 中景不会因曲线过陡而突然出现天空块。
        /// </summary>
        public float sky_depth_fade_power = 10f;

        // ---------- 自身高度融合（局部高度比例：0 = 原始网格底边，1 = 原始网格顶边） ----------
        // 网格上下各延伸 50% 后，延伸段的 localY01 会超出 0~1：
        //   下方延伸段 ∈ [-0.5, 0)、上方延伸段 ∈ (1, 1.5]。
        // 默认 begin = 0 时下方延伸段 saturate 后恒为 0（完全透明，只作几何冗余，不会盖住地形）；
        // 若要让下方延伸段也参与渐变，把 begin 调成负值（如 -0.5）即可。
        /// <summary>自身高度融合起点：低于此比例完全透明</summary>
        public float sky_local_fade_begin = 0f;
        /// <summary>自身高度融合终点：到此比例完全保留（0.25 = 原网格底边往上 1/4 高度内完成渐变）</summary>
        public float sky_local_fade_end = 0.25f;
        /// <summary>自身高度融合曲线强度（1 = 线性）</summary>
        public float sky_local_fade_power = 1f;
        internal class SkyArea
        {
            public UnityEngine.Rect bounds;
            private MapSkyBox skyBox;
            public string[] seasonTextureNames = new string[4];
            public Texture[] seasonTextures = new Texture[4];

            public SkyArea(MapSkyBox master)
            {
                skyBox = master;
            }

            public void SetTextrueNames(string[] names)
            {
                if (names == null || names.Length != 4)
                    return;
                seasonTextureNames = names;
                for (int i = 0; i < seasonTextureNames.Length; ++i)
                {
                    string seasonName = MapRender.SeasonNames[i];

                    Texture tex = skyBox.map.CreateTexture($"Sky/{seasonName}/{seasonTextureNames[i]}");
                    seasonTextures[i] = tex;
                    if (this == skyBox.curArea)
                    {
                        skyBox.UpdateRender();
                    }
                }
            }

            public void SetTextrueNames(string[] names, Texture[] tex)
            {
                if (names == null || names.Length != 4)
                    return;

                if (tex == null || tex.Length != 4)
                    return;

                seasonTextureNames = names;
                seasonTextures = tex;
            }

            public Texture GetTexture(int season)
            {
                return seasonTextures[season];
            }

            public Texture GetTexture()
            {
                return seasonTextures[skyBox.curSeason];
            }

            internal void OnSave(BinaryWriter writer)
            {
                for (int i = 0; i < 4; ++i)
                {
                    if (!string.IsNullOrEmpty(seasonTextureNames[i]))
                        writer.Write(seasonTextureNames[i]);
                    else
                        writer.Write("");
                }
            }

            internal void OnLoad(int versionCode, BinaryReader reader)
            {
                if (versionCode < 2) return;

                for (int i = 0; i < 4; ++i)
                {
                    seasonTextureNames[i] = reader.ReadString();
                    string seasonName = MapRender.SeasonNames[i];
                    if (!string.IsNullOrEmpty(seasonTextureNames[i]))
                    {

                        Texture tex = skyBox.map.CreateTexture($"Sky/{seasonName}/{seasonTextureNames[i]}");
                        seasonTextures[i] = tex;
                        if (this == skyBox.curArea)
                        {
                            skyBox.UpdateRender();
                        }
                    }
                }
            }
        }

        internal SkyArea curArea;
        internal List<SkyArea> allAreas = new List<SkyArea>();

        Material skyMat;
        Mesh[] skyMeshes;
        public Transform skyTrans;

        public MapSkyBox(MapRender map) : base(map)
        {


        }

        public override void Init()
        {
            base.Init();

            Transform trans = Create();
            trans.SetParent(map.mapCamera.GetCenterTransform(), false);
        }
        public override void Clear()
        {
            base.Clear();
            allAreas.Clear();
            // 【区域引用也要归零】
            // 只清 allAreas 会留下一个"指向已不在列表里的旧 SkyArea"的 curArea，
            // 重载地图后若新的 skyTrans 恰好落在旧 bounds 内，Update 会误判
            // "当前区域没变"而跳过换图，天空盒继续用上一张地图的纹理。
            // OnLoad 里同样是先 curArea = null 再重建，两处口径保持一致。
            curArea = null;
            if (skyTrans != null)
            {
                GameObject.Destroy(skyTrans.gameObject);
                skyTrans = null;
            }
            if (skyMat != null)
                GameObject.Destroy(skyMat);
        }

        internal override void OnSave(BinaryWriter writer)
        {
            writer.Write(sky_blend_start);
            writer.Write(sky_blend_end);

            writer.Write(allAreas.Count);
            for (int i = 0; i < allAreas.Count; i++)
            {
                SkyArea area = allAreas[i];
                writer.Write(area.bounds.x);
                writer.Write(area.bounds.y);
                writer.Write(area.bounds.width);
                writer.Write(area.bounds.height);

                for (int j = 0; j < 4; j++)
                {
                    string s = area.seasonTextureNames[j];
                    if (!string.IsNullOrEmpty(s))
                        writer.Write(s);
                    else
                        writer.Write("");
                }
            }

        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            curArea = null;
            allAreas.Clear();
            // 版本1的加载
            if (versionCode == 1)
            {

                blendStart = reader.ReadSingle();
                blendEnd = reader.ReadSingle();
                int length = reader.ReadInt32();
                //string[] texNames = new string[length];
                for (int i = 0; i < length; ++i)
                {
                    //texNames[i] = reader.ReadString();
                    reader.ReadString();
                }
                //skyTextures = texNames;
                //textrueIndex = reader.ReadInt32();
                reader.ReadInt32();
            }
            else
            {
                blendStart = reader.ReadSingle();
                blendEnd = reader.ReadSingle();
                int length = reader.ReadInt32();
                for (int i = 0; i < length; ++i)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float w = reader.ReadSingle();
                    float h = reader.ReadSingle();
                    string[] texNames = new string[4];
                    for (int j = 0; j < 4; ++j)
                        texNames[j] = reader.ReadString();

                    SkyArea area = new SkyArea(this)
                    {
                        bounds = new UnityEngine.Rect(x, y, w, h),
                    };
                    allAreas.Add(area);
                    area.SetTextrueNames(texNames);
                }
            }
        }

        public float blendStart
        {
            get; set;
        }
        public float blendEnd
        {
            get; set;
        }


        void _CreateMaterial()
        {
            skyMat = new Material(Shader.Find(sky_material_shader));
            ApplyMaterialParams();
            // 天空盒必须画在地形之后（Transparent 队列靠后），且 shader 内部是 ZTest Off，
            // 可见性完全由深度融合 / 高度融合算出的 alpha 决定
            skyMat.renderQueue = 3001;
        }

        /// <summary>
        /// 把本类配置的融合参数写入材质。
        ///
        /// 【为什么单独抽出来】材质属性只在这里写；一旦有人（或编辑器脚本）在 Inspector 里
        /// 换 shader / 重新赋值材质，属性会退回 shader 的 Properties 默认值，
        /// 深度融合与高度融合的区间都会失真（典型表现是 col.a 恒为 0、天空盒"凭空消失"）。
        /// 所以 <see cref="UpdateRender"/> 与 <see cref="EnsureMaterialParams"/> 都会调用它补写。
        /// </summary>
        void ApplyMaterialParams()
        {
            if (skyMat == null)
                return;

            skyMat.SetColor("_Color", UnityEngine.Color.white);

            // 深度融合：相机空间距离区间（米）
            skyMat.SetFloat("_DepthFadeBegin", sky_depth_fade_begin);
            skyMat.SetFloat("_DepthFadeEnd", sky_depth_fade_end);
            skyMat.SetFloat("_DepthPower", sky_depth_fade_power);

            // 世界高度融合：世界高度区间
            skyMat.SetFloat("_HeightFadeBegin", sky_blend_start);
            skyMat.SetFloat("_HeightFadeEnd", sky_blend_end);
            skyMat.SetFloat("_HeightPower", sky_height_power);

            // 自身高度融合：局部高度比例（0 = 底边，1 = 顶边）
            skyMat.SetFloat("_LocalFadeBegin", sky_local_fade_begin);
            skyMat.SetFloat("_LocalFadeEnd", sky_local_fade_end);
            skyMat.SetFloat("_LocalPower", sky_local_fade_power);
        }

        /// <summary>天空盒网格竖向的原始层数（原模型局部 y ∈ [0, 4]，4 层 5 行顶点）。</summary>
        const int SkyRows = 4;

        /// <summary>网格每一层的高度（局部单位）。原模型总高 = SkyRows × SkyRowHeight = 4。</summary>
        const float SkyRowHeight = 1f;

        /// <summary>UV 竖直方向每层推进的步长（原实现 0.25 = 1/4 张图）。</summary>
        const float SkyRowUVStep = 0.25f;

        /// <summary>
        /// 天空盒网格上、下各延伸的比例（0.5 = 各延伸原有高度的 50%）。
        ///
        /// 【为什么要延伸】原网格竖向只有 0~4 个局部单位，相机拉远或俯仰变化时天空盒的
        /// 顶边 / 底边会进入视野，露出几何硬边。上下各延伸 50% 后竖向覆盖范围翻倍。
        ///
        /// 【UV 口径】原有 5 行顶点的 UV 一个不改（纹理在原高度上的映射尺度不变），
        /// 延伸段做<b>镜像（倒影）UV</b>：以原有顶边 / 底边为镜面，v 反向取值
        /// （上延伸段 0 → -0.25 → -0.5，下延伸段 -1 → -0.75 → -0.5）。
        /// 这样延伸段显示的是原天空的镜像倒影，纹理在镜面处连续、不会重复出现第二遍天空。
        /// </summary>
        const float SkyExtendRatio = 0.5f;

        public Transform Create()
        {
            // 上下各延伸的层数（每侧 2 层）：总层数 8、总行数 9
            int extendRows = Mathf.Max(0, Mathf.RoundToInt(SkyRows * SkyExtendRatio));
            int rowCount = SkyRows + 1 + extendRows * 2;   // 9
            int rowFirst = -extendRows;                    // -2

            skyMeshes = new Mesh[4];
            Vector3[] vertex = new Vector3[17 * rowCount];
            Vector2[] uv = new Vector2[17 * rowCount];
            int[] indexs = new int[16 * (rowCount - 1) * 2 * 3];

            skyTrans = new GameObject("skyBox").transform;
            GameObject sky = new GameObject("skyBox");
            sky.transform.SetParent(skyTrans, false);

            _CreateMaterial();

            sky.transform.localScale = new Vector3(sky_radius_len, sky_height, sky_radius_len);
            for (int i = 0; i < skyMeshes.Length; i++)
            {

                Mesh mesh = new Mesh();
                for (int j = 0; j < 17; ++j)
                {
                    Vector3 pos = Quaternion.Euler(0, 90.0f * j / 16.0f, 0) * Vector3.forward * 5;
                    for (int k = 0; k < rowCount; ++k)
                    {
                        // 行号从 -extendRows 排到 SkyRows + extendRows（-2 ~ 6）
                        int row = rowFirst + k;
                        pos.y = row * SkyRowHeight;
                        vertex[j * rowCount + k] = pos;

                        // u：环向 16 等分，与原实现完全一致
                        // v：原有区段 row = 0/1/2/3/4 → -1/-0.75/-0.5/-0.25/0，逐值与旧实现相同；
                        //    延伸段做镜像（倒影）UV，以原有边缘为镜面反向取值：
                        //      上方 row > 4 → 以顶边 v = 0 反射：v = -(row - 4) × 0.25
                        //                     （row 5/6 → -0.25/-0.5）
                        //      下方 row < 0 → 以底边 v = -1 反射：v = -1 - row × 0.25
                        //                     （row -1/-2 → -0.75/-0.5）
                        //    镜像后 v 仍落在原有 [-1, 0] 区间内，纹理在镜面处连续，
                        //    延伸段显示的是原天空的倒影，而不是重复或纯色拉伸。
                        float bottomV = -SkyRows * SkyRowUVStep;
                        float v;
                        if (row > SkyRows)
                            v = -(row - SkyRows) * SkyRowUVStep;
                        else if (row < 0)
                            v = bottomV - row * SkyRowUVStep;
                        else
                            v = (row - SkyRows) * SkyRowUVStep;
                        uv[j * rowCount + k] = new Vector2(j / 16.0f, v);
                    }
                }

                int tIndex = 0;
                for (int j = 0; j < 16; ++j)
                {
                    for (int k = 0; k < rowCount - 1; ++k)
                    {
                        // 每格两个三角形；索引按行数 rowCount 步进
                        indexs[tIndex++] = (j * rowCount) + k;
                        indexs[tIndex++] = (j * rowCount) + k + 1;
                        indexs[tIndex++] = (j + 1) * rowCount + k;

                        indexs[tIndex++] = (j * rowCount) + k + 1;
                        indexs[tIndex++] = (j + 1) * rowCount + k + 1;
                        indexs[tIndex++] = (j + 1) * rowCount + k;
                    }
                }

                mesh.vertices = vertex;
                mesh.triangles = indexs;
                mesh.uv = uv;
                mesh.RecalculateBounds();
                skyMeshes[i] = mesh;

                GameObject sky2 = new GameObject("sky");
                sky2.transform.SetParent(sky.transform, false);
                MeshFilter mf = sky2.AddComponent<MeshFilter>();
                MeshRenderer mR = sky2.AddComponent<MeshRenderer>();
                mf.mesh = mesh;
                mR.sharedMaterial = skyMat;

                sky2.transform.localRotation = Quaternion.Euler(0, 90 * i, 0);
                sky2.transform.localPosition = new Vector3(0, sky_offset, 0);
            }

            SkyArea area = new SkyArea(this)
            {
                bounds = new UnityEngine.Rect(0, 0, map.mapData.world_width, map.mapData.world_height),
                seasonTextures = new Texture[4]
                {
                    Texture2D.whiteTexture,
                    Texture2D.whiteTexture,
                    Texture2D.whiteTexture,
                    Texture2D.whiteTexture
                },

            };

            curArea = area;
            allAreas.Add(curArea);

            skyMat.mainTexture = curArea.GetTexture();

            return skyTrans;
        }
        public void SetVisible(bool b)
        {
            if (skyTrans != null)
            {
                skyTrans.gameObject.SetActive(b);
            }
        }
        public override void UpdateRender()
        {
            if (skyMat == null || curArea == null)
                return;

            skyMat.mainTexture = curArea.GetTexture();

            // 材质可能在 Inspector 里被换过 shader / 重新赋值，属性会退回 shader 默认值
            //（_HeightFadeBegin / _HeightFadeEnd 变成 0/0 → alpha NaN → 天空盒"凭空消失"）。
            // 切换天空区域 / 季节时顺手补回代码配置的融合参数。
            ApplyMaterialParams();
            lastDepthFadeEnd = -1f;      // 让下一次 SyncDepthFadeWithCamera 重新写入
        }

        /// <summary>上一次写入材质的深度融合终点（判脏：材质属性每帧写有额外开销）</summary>
        float lastDepthFadeEnd = -1f;

        /// <summary>
        /// 深度融合起点占终点的比例（旧美术参数 900 / 3400 ≈ 0.26）。
        ///
        /// 【为什么要按比例跟随远裁剪面】shader 的深度融合是"场景相机空间距离落在
        /// [起点, 终点] 内渐入"。如果起点/终点写死（900 / 3400），而游戏内远裁剪面只有
        /// 400 ~ 1800，场景距离永远到不了 3400，曲线一压整屏 alpha ≈ 0 —— 天空盒
        /// 完全看不见（表现就是"天空球被地形覆盖"）。反过来远裁剪面到 30000 时，
        /// 近处大片区域就开始不透明、盖住地形。按比例跟随才能在任何视距下保持同一观感。
        /// </summary>
        const float DepthFadeBeginRatio = 0.26f;

        /// <summary>
        /// 按当前相机的远裁剪面同步深度融合区间（起点按比例、终点贴齐裁剪面）。
        ///
        /// 【为什么放在天空盒自己身上】视距参数由 <see cref="MapCamera"/> 决定，
        /// 天空盒只需要读到最终的 farClipPlane；写在这里可以避免两处各算一套、
        /// 出现"相机改了但天空盒没跟上"的错位。
        /// </summary>
        void SyncDepthFadeWithCamera()
        {
            if (map == null || map.mapCamera == null || skyMat == null)
                return;

            Camera cam = map.mapCamera.camera != null ? map.mapCamera.camera : Camera.main;
            if (cam == null)
                return;

            float end = Mathf.Max(cam.farClipPlane, 10f);
            if (Mathf.Abs(end - lastDepthFadeEnd) < 1f)
                return;

            lastDepthFadeEnd = end;
            skyMat.SetFloat("_DepthFadeEnd", end);
            skyMat.SetFloat("_DepthFadeBegin", end * DepthFadeBeginRatio);
        }

        /// <summary>
        /// 检查材质上的高度融合参数是否被重置（Inspector 换 shader / 重新赋值材质都会退回
        /// shader 默认值），被重置时调用 <see cref="UpdateRender"/> 补回代码配置值。
        ///
        /// 【为什么用高度参数做判据】_HeightFadeBegin / _HeightFadeEnd 只有本类会写；
        /// 一旦不再等于 <see cref="sky_blend_start"/> / <see cref="sky_blend_end"/>，
        /// 就说明材质被换过（此时深度融合参数也一定丢了）。
        /// 每两帧读两个 float，开销可忽略。
        /// </summary>
        void EnsureMaterialParams()
        {
            if (skyMat == null)
                return;

            // 用"自身高度融合"做判据：这组参数只有本类会写，
            // 而深度融合/世界高度融合可能被视距逻辑改动，不适合当重置信号
            if (Mathf.Approximately(skyMat.GetFloat("_LocalFadeEnd"), sky_local_fade_end)
                && Mathf.Approximately(skyMat.GetFloat("_LocalFadeBegin"), sky_local_fade_begin))
                return;

            UpdateRender();
        }

        public override void Update()
        {
            // 【未创建 / 已清理就直接返回】
            // 驱动链是 MapLooper(MonoBehaviour).Update → MapRender.Update → 本方法，
            // 每帧无条件调用，不关心天空盒此刻是否还活着。
            // 而 Clear() 会 Destroy 天空盒节点并把 skyTrans 置 null ——
            // 切地图 / 重载地图 / 卸载地图时，"Clear 之后、Init 之前"必然存在若干帧空档
            // （新地图资源是异步加载的），这段空档里下面取 skyTrans.position 就会抛
            // NullReferenceException，而且是每帧一条，把控制台刷满。
            // 天空盒还没建好 / 已经拆掉 = 没有任何东西需要更新，直接返回。
            if (skyTrans == null)
                return;

            if (Time.frameCount % 2 == 0)
                return;

            // 材质参数被重置（Inspector 换 shader）→ 补回；相机视距变了 → 深度融合区间跟随
            EnsureMaterialParams();
            SyncDepthFadeWithCamera();

            // 优先判断后面的区域
            Vector3 centerPos = skyTrans.position;
            Vector2 point = new Vector2(centerPos.z, centerPos.x);
            if (curArea != null && curArea.bounds.Contains(point))
                return;

            for (int i = allAreas.Count - 1; i >= 0; i--)
            {
                SkyArea area = allAreas[i];
                if (area == curArea) continue;
                if (area.bounds.Contains(point))
                {
                    if (area != curArea)
                    {
                        curArea = area;
                        UpdateRender();
                        return;
                    }
                }
            }
        }
    }
}
