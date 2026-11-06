using System;
using System.Collections.Generic;
using System.IO;
// 见 Terrain314Types.cs：屏蔽 Sango.Path / Sango.Directory / Sango.File 的遮蔽
using UnityEngine;
// GraphicsFormat 与 TextureCreationFlags 在 Unity 2021.3 仍位于 Experimental.Rendering 下
using UnityEngine.Experimental.Rendering;

namespace Sango.Render.Terrain314
{
    using Path = System.IO.Path;
    using Directory = System.IO.Directory;
    using File = System.IO.File;

    /// <summary>
    /// 314 地形材质组。
    ///
    /// 314 的地形材质由 5 组贴图构成（实测自原版 FieldEditor4 归档）：
    ///   Field      slot0 2049² 4Bpp（高度/浮点）、slot1 2049² 4Bpp（索引图）、
    ///              slot2/3/4 4096² BC3（material field 三层）
    ///   Root       slot0/1/2 1024² × 19 层（季节 × 调色板分组）
    ///   Auxiliary  slot0 4096² BC1
    ///   Control    initial control 2049² RGBA8
    ///
    /// 本组件只负责把导出资源挂进 Unity：
    ///   - 在 Inspector 里点"自动绑定"从 Textures 目录按文件名装配；
    ///   - Root 的 19 层按 layer 顺序进 Layer0..LayerN，运行时再用 Texture2DArray 组装。
    ///
    /// 语义尚未逐一确认，因此不在此处推断"哪个通道是权重"，
    /// 具体解释交给 Terrain314Debug.shader 逐通道验证。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Terrain314MaterialSet : MonoBehaviour
    {
        /// <summary>纹理目录（相对 StreamingAssets 或绝对路径）。</summary>
        [Header("资源目录")]
        public string TextureDirectory =
            "Assets/Sango/Terrain314/Textures";

        [Header("Field 组（4096² BC3，material field 三层）")]
        public Texture2D Field0;
        public Texture2D Field1;
        public Texture2D Field2;

        [Header("Root 组（1024² × 19 层）")]
        [Tooltip("slot0：albedo 数组（对应 314 AlbedoOcclusion）")]
        public Texture2D[] RootLayers = Array.Empty<Texture2D>();

        [Tooltip("slot1：normal 数组（对应 314 NormalHeight）")]
        public Texture2D[] RootNormalLayers = Array.Empty<Texture2D>();

        [Tooltip("slot2：reflectance 数组（对应 314 ReflectanceLinearRoughness）")]
        public Texture2D[] RootReflectanceLayers = Array.Empty<Texture2D>();

        [Header("Auxiliary 与 Control")]
        public Texture2D Auxiliary;
        /// <summary>initial control：每像素一个调色板索引（RGBA8）。</summary>
        public Texture2D ControlInitial;

        /// <summary>Field slot0/slot1 的 4 字节原始图（尚未确认像素语义，先按 RGBA8 载入）。</summary>
        public Texture2D FieldSlot0;
        public Texture2D FieldSlot1;

        /// <summary>调色板条目数。</summary>
        [Header("调色板")]
        public int PaletteEntryCount;

        /// <summary>
        /// 调色板入口固定容量。着色器里的数组按这个长度声明，
        /// 两侧不一致会让 SetFloatArray 被截断，所以做成常量而不是运行时长度。
        /// </summary>
        public const int PaletteCapacity = 32;

        /// <summary>
        /// control 索引 → root 数组层号，对应 314 常量 vDtlTexIdx。
        /// 原版 control 的 R 通道给的是"调色板参数索引"，不是纹理层号，
        /// 必须经这一列映射后才能拿去索引 Texture2DArray。
        /// </summary>
        [Tooltip("control 的 R 值 → root 数组层号（vDtlTexIdx）")]
        public float[] PaletteTexIndex = new float[PaletteCapacity];

        /// <summary>该层的世界 UV 缩放（世界单位／贴图一次平铺），对应 vDtlScaleU。</summary>
        [Tooltip("该层 detail 贴图覆盖的世界单位数（vDtlScaleU）")]
        public float[] PaletteScaleU = new float[PaletteCapacity];

        /// <summary>同上，V 方向，对应 vDtlScaleV。</summary>
        [Tooltip("该层 detail 贴图覆盖的世界单位数（vDtlScaleV）")]
        public float[] PaletteScaleV = new float[PaletteCapacity];

        /// <summary>该层的混合锐度，对应 vDtlBldSrp。原版按"第二层的索引"取，不是全局常量。</summary>
        [Tooltip("该层的 height blend 锐度（vDtlBldSrp）")]
        public float[] PaletteSharpness = new float[PaletteCapacity];

        /// <summary>按文件名装配全部贴图。</summary>
        [ContextMenu("自动绑定贴图")]
        public void BindFromDirectory()
        {
            string directory = TextureDirectory;
            if (!Path.IsPathRooted(directory))
            {
                // 必须 GetFullPath 消掉 ".."，否则拼出来的路径里含
                // "Project/Assets/../Assets/..."，ToAssetPath 会切出非法资产路径，
                // AssetDatabase.LoadAssetAtPath 全部返回 null（贴图一张都绑不上）。
                directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", directory));
            }
            if (!Directory.Exists(directory))
            {
                Log.Error($"Terrain314 材质目录不存在: {directory}");
                return;
            }

#if UNITY_EDITOR
            var field = new List<Texture2D>();
            var albedo = new List<Texture2D>();
            var normal = new List<Texture2D>();
            var reflectance = new List<Texture2D>();
            string[] files = Directory.GetFiles(directory, "*.png");
            Array.Sort(files, StringComparer.Ordinal);

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string assetPath = ToAssetPath(file);
                Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null) continue;

                // 顺序注意：field_slot0x 的短前缀会命中 slot00/01，必须先判 02/03/04
                if (name.StartsWith("field_slot02")) field.Add(texture);
                else if (name.StartsWith("field_slot03")) field.Add(texture);
                else if (name.StartsWith("field_slot04")) field.Add(texture);
                else if (name.StartsWith("field_slot00")) FieldSlot0 = texture;
                else if (name.StartsWith("field_slot01")) FieldSlot1 = texture;
                else if (name.StartsWith("root_slot00")) albedo.Add(texture);
                else if (name.StartsWith("root_slot01")) normal.Add(texture);
                else if (name.StartsWith("root_slot02")) reflectance.Add(texture);
                else if (name.StartsWith("auxiliary")) Auxiliary = texture;
                else if (name.StartsWith("control_initial")) ControlInitial = texture;
            }

            if (field.Count >= 3)
            {
                Field0 = field[0];
                Field1 = field[1];
                Field2 = field[2];
            }
            RootLayers = albedo.ToArray();
            RootNormalLayers = normal.ToArray();
            RootReflectanceLayers = reflectance.ToArray();
            BindPalette(directory);
            // 贴图/调色板变了，已缓存的材质必须作废，否则预览还是旧数据
            Terrain314Renderer owner = GetComponent<Terrain314Renderer>();
            if (owner != null) owner.InvalidateMaterial();
            UnityEditor.EditorUtility.SetDirty(this);
            Log.Info($"Terrain314 材质绑定完成: Field {field.Count}, " +
                     $"Root albedo {RootLayers.Length} / normal {RootNormalLayers.Length} / " +
                     $"reflectance {RootReflectanceLayers.Length}, " +
                     $"Auxiliary {(Auxiliary != null ? 1 : 0)}, Control {(ControlInitial != null ? 1 : 0)}",
                Sango.Log.LogType.World);
#endif
        }

        /// <summary>
        /// 读取 palette.json，把 314 的四列调色板常量填进 Inspector 字段。
        ///
        /// 314 的 PS13 用 control 的 R 值去索引 vDtlTexIdx / vDtlBldSrp / vDtlScaleU / vDtlScaleV
        /// 四个常量数组（列顺序见 San14 TerrainRendererPalette.cs:36-44）；
        /// 直接拿 R 当纹理层号会让整片地表串层。
        /// </summary>
        public bool BindPalette(string directory)
        {
            ResetPaletteDefaults();
            string path = Path.Combine(directory, "palette.json");
            if (!File.Exists(path))
            {
                Log.Warning($"Terrain314 调色板文件不存在，已回退为恒等映射: {path}");
                return false;
            }

            Terrain314PaletteFile file =
                JsonUtility.FromJson<Terrain314PaletteFile>(File.ReadAllText(path));
            if (file == null || file.Entries == null || file.Entries.Length == 0)
            {
                Log.Warning("Terrain314 调色板解析为空，已回退为恒等映射");
                return false;
            }

            PaletteEntryCount = Mathf.Min(file.Entries.Length, PaletteCapacity);
            for (int i = 0; i < file.Entries.Length; i++)
            {
                Terrain314PaletteEntryJson entry = file.Entries[i];
                int parameter = entry.ParameterIndex;
                if (parameter < 0 || parameter >= PaletteCapacity) continue;
                PaletteTexIndex[parameter] = entry.DetailTextureNameIndex;
                PaletteScaleU[parameter] = entry.ScaleU;
                PaletteScaleV[parameter] = entry.ScaleV;
                PaletteSharpness[parameter] = entry.BlendSharpness;
            }
            return true;
        }

        /// <summary>调色板缺失时的兜底：层号恒等、每 6000 世界单位平铺一次、不做高度锐化。</summary>
        private void ResetPaletteDefaults()
        {
            for (int i = 0; i < PaletteCapacity; i++)
            {
                PaletteTexIndex[i] = i;
                PaletteScaleU[i] = 6000f;
                PaletteScaleV[i] = 6000f;
                PaletteSharpness[i] = 0f;
            }
            PaletteEntryCount = 0;
        }

#if UNITY_EDITOR
        private static string ToAssetPath(string absolute)
        {
            string normalized = absolute.Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", StringComparison.Ordinal);
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }
#endif

        /// <summary>
        /// 从 StreamingAssets 读取 height.r32 并建立 R32F 高度纹理。
        ///
        /// 用 RFloat 而不是 8 位 PNG：314 的 MHK 高度场原生是 24 位量化，
        /// 降到 8 位会让 0..6966 的范围粗化成约 27 米一档，远看会出现阶梯。
        /// </summary>
        public Texture2D BuildHeightTexture(string streamingDirectory, int sampleWidth, int sampleHeight)
        {
            string path = Path.Combine(streamingDirectory, "height.r32");
            if (!File.Exists(path))
            {
                Log.Warning($"Terrain314 高度场不存在: {path}");
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            int expected = sampleWidth * sampleHeight * sizeof(float);
            if (bytes.Length != expected)
            {
                Log.Error($"Terrain314 高度场长度不符: 期望 {expected}, 实际 {bytes.Length}");
                return null;
            }

            var texture = new Texture2D(sampleWidth, sampleHeight, TextureFormat.RFloat, false, true)
            {
                name = "Terrain314Height",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.LoadRawTextureData(bytes);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>用当前绑定的贴图创建 314 地形材质。</summary>
        public Material CreateMaterial(Texture2D heightTexture, Texture2DArray albedoArray,
            Texture2DArray normalArray, Texture2DArray reflectanceArray)
        {
            Shader shader = Shader.Find("Sango/Terrain314");
            if (shader == null)
            {
                Log.Error("找不到着色器 Sango/Terrain314");
                return null;
            }

            var material = new Material(shader) { name = "Terrain314Runtime" };
            if (ControlInitial != null) material.SetTexture("_ControlTex", ControlInitial);
            if (albedoArray != null) material.SetTexture("_AlbedoArray", albedoArray);
            if (normalArray != null) material.SetTexture("_NormalArray", normalArray);
            if (reflectanceArray != null) material.SetTexture("_ReflectanceArray", reflectanceArray);
            // Field 组的 slot2/3/4 对应本类 Field0/1/2，其中 slot3 的 A 通道是 height
            if (Field1 != null) material.SetTexture("_FieldHeight", Field1);
            if (heightTexture != null) material.SetTexture("_HeightTex", heightTexture);

            // 四列调色板常量：control 的 R 值要靠它们才能定到层号、UV 缩放与锐度
            material.SetFloatArray("_PaletteTexIndex", PaletteTexIndex);
            material.SetFloatArray("_PaletteScaleU", PaletteScaleU);
            material.SetFloatArray("_PaletteScaleV", PaletteScaleV);
            material.SetFloatArray("_PaletteSharpness", PaletteSharpness);
            material.SetFloat("_PaletteCount", Mathf.Max(1, PaletteEntryCount));
            return material;
        }

        /// <summary>把 Root slot0 的 19 层组装成 albedo 数组。</summary>
        public Texture2DArray BuildRootArray() => BuildArray(RootLayers, "Terrain314AlbedoArray");

        /// <summary>把 Root slot1 的 19 层组装成 normal 数组。</summary>
        public Texture2DArray BuildNormalArray() => BuildArray(RootNormalLayers, "Terrain314NormalArray");

        /// <summary>把 Root slot2 的 19 层组装成 reflectance 数组。</summary>
        public Texture2DArray BuildReflectanceArray() =>
            BuildArray(RootReflectanceLayers, "Terrain314ReflectanceArray");

        /// <summary>palette.json 顶层结构（对应导出器 WritePalette 的输出）。</summary>
        [Serializable]
        private class Terrain314PaletteFile
        {
            public bool Resolved;
            public Terrain314PaletteEntryJson[] Entries;
        }

        /// <summary>palette.json 的一条调色板条目。</summary>
        [Serializable]
        private class Terrain314PaletteEntryJson
        {
            public int ParameterIndex;
            public int DetailTextureNameIndex;
            public float BlendSharpness;
            public float ScaleU;
            public float ScaleV;
            public float TessScale;
            public float TessBias;
        }

        /// <summary>通用：把一组同尺寸贴图组装成 Texture2DArray。</summary>
        private Texture2DArray BuildArray(Texture2D[] layers, string name)
        {
            if (layers == null || layers.Length == 0) return null;
            Texture2D first = layers[0];
            if (first == null) return null;

            // 用 graphicsFormat 构造：贴图可能已被导入器压成 BC 格式，
            // TextureFormat 枚举在部分平台上无法覆盖，graphicsFormat 才是权威。
            // mip 链必须与源一致，否则 CopyTexture 会因 mip 不兼容直接报错。
            int mipCount = first.mipmapCount;
            TextureCreationFlags flags = mipCount > 1
                ? TextureCreationFlags.MipChain
                : TextureCreationFlags.None;

            // Graphics.CopyTexture 会校验源与目标的 masterTextureLimit 必须一致，
            // 否则报 "different master texture limits. Source 1, Destination 0"。
            // Texture.masterTextureLimit 是 static（等同 QualitySettings 的同名项），
            // 导入的贴图跟随它，而运行时 new 出来的 Texture2DArray 固定按 0 记录。
            // 该值无法按纹理单独设置，只能在整个构建期间把全局限制临时降到 0，构建完还原。
            int previousLimit = QualitySettings.masterTextureLimit;
            Texture2DArray array;
            try
            {
                if (previousLimit != 0) QualitySettings.masterTextureLimit = 0;

                array = new Texture2DArray(first.width, first.height, layers.Length,
                    first.graphicsFormat, flags)
                {
                    name = name,
                    filterMode = FilterMode.Trilinear,
                    // detail 贴图按世界坐标平铺（ScaleU 覆盖几千单位就得重复几十次），
                    // 用 Clamp 会让整片地形都采样到贴图边缘那一列像素
                    wrapMode = TextureWrapMode.Repeat,
                    anisoLevel = 4
                };

                for (int layer = 0; layer < layers.Length; layer++)
                {
                    Texture2D source = layers[layer];
                    if (source == null || source.width != first.width || source.height != first.height)
                    {
                        Log.Warning($"{name} 第 {layer} 层尺寸不符，已跳过");
                        continue;
                    }

                    // 逐 mip 复制：源与目标的 mip 数可能不同，整链复制会失败
                    int copyMips = Mathf.Min(source.mipmapCount, mipCount);
                    for (int mip = 0; mip < copyMips; mip++)
                    {
                        Graphics.CopyTexture(source, 0, mip, array, layer, mip);
                    }
                }
            }
            finally
            {
                if (previousLimit != 0) QualitySettings.masterTextureLimit = previousLimit;
            }
            return array;
        }
    }
}
