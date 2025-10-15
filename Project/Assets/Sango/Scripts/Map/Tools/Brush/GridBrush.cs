using Sango.Game;
using Sango.Render;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Tools
{
    /// <summary>
    /// 地格笔刷类GridBrush  继承BrushBase，主要用于地图编辑中的各种笔刷相关操作，例如设置不同类型的笔刷、处理笔刷绘制、加载保存地格数据等功能
    /// </summary>
    public class GridBrush : BrushBase
    {
        /// <summary>
        /// 定义笔刷类型的枚举，包含了多种地图编辑中可使用的笔刷类型，如地形类型、区域、陷阱等
        /// </summary>
        public enum BrushType : int
        {
            TerrainType,    // "类型"
            //Area,           // "区域"
            //Trap,           // "陷阱"
            //Dir,            // "方向"
            //Interior,       // "内政"
            //Defence,        // "防守"
            //Thief,          // "山寨（贼）"
            //Flood,          // "水淹"
            //Ruins,          // "遗迹（庙）"
            Unknown,        // "未知"
        }
        /// <summary>
        /// 笔刷的大小，默认为1，可用于控制笔刷作用范围等
        /// </summary>
        public int size = 1;
        /// <summary>
        /// 笔刷的不透明度（或可理解为强度等类似属性），用于影响笔刷操作的效果程度
        /// </summary>
        public int opacity;
        /// <summary>
        /// 工具栏标题数组，用于显示不同的编辑模式相关名称，例如"无"、"类型"等，对应不同的地图编辑功能选项
        /// </summary>
        private string[] toolbarTitle = new string[]
        {
            "无",
            "类型",
            //"区域",
            ////"lpB",
            //"陷阱",
            //"方向",
            //"内政",
            //"防守",
            //"山寨（贼）",
            //"水淹",
            ////"种类?",  火焰
            //"遗迹（庙）"
        };
        /// <summary>
        /// 当前编辑模式的索引，默认为0，对应不同的编辑功能状态，通过索引来切换不同的操作模式
        /// </summary>
        private int currentEditMode = 0;
        /// <summary>
        /// 当前笔刷的类型，初始化为BrushType.Unknown，后续会根据操作进行相应设置
        /// </summary>
        public BrushType brushType = BrushType.Unknown;
        /// <summary>
        /// 地形类型纹理名称数组，用于存储不同地形类型对应的纹理文件名，方便加载纹理资源
        /// </summary>
        public string[] terrainTypeTexNames = new string[]
		{
            "editor_terrain_type",      //1.editor_terrain_type.png 20种地格类型+12种未知预留类型
            //"editor_area_type",         //2.editor_area_type.png 16*7种不同势力区域颜色
            //"editor_trap_type",         //3.editor_trap_type.png 堤防、陷阱
            //"editor_dir_type",          //4.editor_dir_type.png 6个水流方向
            //"editor_interior_type",     //5.editor_interior_type.png 内政用地
            //"editor_defence_type",      //6.editor_defence_type.png 防守用地
            //"editor_thief_type",        //7.editor_thief_type.png 贼用地
            //"editor_flood_type",        //8.editor_flood_type.png 水淹用地
            //"editor_ruins_type",        //9.editor_ruins_type.png 遗迹（庙）
        };
        /// <summary>
        /// 地形类型纹理数组，用于存储对应地形类型的纹理资源，初始化为白色纹理占位，后续会加载真实纹理
        /// </summary>
        public Texture[] terrainTypeTexes = new Texture[]
        {
            Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
        };
        /// <summary>
        /// 地形类型的遮罩纹理，用于显示或处理特定地形相关的遮罩效果，可根据需要进行创建和更新
        /// </summary>
        public Texture2D terrainTypeMaskTex;
        /// <summary>
        /// 地形类型遮罩纹理的列数，用于划分纹理等操作，不同笔刷类型下可能有不同的值
        /// </summary>
        public int terrainTypeMaskCol = 4;
        /// <summary>
        /// 地形类型遮罩纹理的行数，用于划分纹理等操作，不同笔刷类型下可能有不同的值
        /// </summary>
        public int terrainTypeMaskRow = 8;
        /// <summary>
        /// 是否显示地形类型的布尔值，用于控制相关地形类型显示与否的效果
        /// </summary>
        public bool showTerrainType = false;
        /// <summary>
        /// 地形类型的区域颜色图例，area_type
        /// </summary>
        public Texture2D terrainTypeTex;
        /// <summary>
        /// 是否显示网格的布尔值，用于控制地图网格是否显示，方便编辑时查看布局等
        /// </summary>
        private bool showGrid = true;
        /// <summary>
        /// 用于信息窗口的矩形区域，定义了窗口的位置和大小，用于展示相关信息图等内容
        /// </summary>
        internal UnityEngine.Rect maskWindowRect = new UnityEngine.Rect(20, 20, 256, 256);

        /// <summary>
        /// 方向类型标题数组，用于显示不同方向的名称，例如"左上"、"上"等，对应地图中方向相关的编辑选项
        /// </summary>
        private string[] dirTypeTitle = new string[]
        {
            "左上", "上", "右上", "右下", "下", "左下", "无"
        };

        /// <summary>
        /// 陷阱类型标题数组，用于显示不同陷阱类型的名称，例如"无"、"堤防"、"落石"等，对应地图中陷阱相关的编辑选项
        /// </summary>
        private string[] trapTypeTitle = new string[]
        {
            "无", "堤防", "落石",
        };

        /// <summary>
        /// 地形类型标题数组，用于显示不同地形的名称，例如"草地"、"土地"等，对应地图中地形相关的编辑选项
        /// </summary>
        private string[] terrainTypeTitle = new string[]
        {
            "草地", "土地", "砂地", "湿地",
            "毒泉", "森林", "江河", "河道",
            "大海", "荒地", "道路", "栈道",
            "桥梁", "浅滩", "岸滩", "山崖",
            "城池", "港口", "关隘", "间道"
        };
		
        private string[] areaTypeTitle = new string[]
        {
        "01襄平  ", "02北平  ", "03蓟    ", "04南皮  ", "05平原  ", "06晋阳  ", "07邺    ",
        "08北海  ", "09下邳  ", "10小沛  ", "11寿春  ", "12濮阳  ", "13陈留  ", "14许昌  ",
        "15汝南  ", "16洛阳  ", "17宛    ", "18长安  ", "19上庸  ", "20安定  ", "21天水  ",
        "22武威  ", "23建业  ", "24吴    ", "25会稽  ", "26庐江  ", "27柴桑  ", "28江夏  ",
        "29新野  ", "30襄阳  ", "31江陵  ", "32长沙  ", "33武陵  ", "34桂阳  ", "35零陵  ",
        "36永安  ", "37汉中  ", "38梓潼  ", "39江州  ", "40成都  ", "41建宁  ", "42云南  ",
        "43壶关  ", "44虎牢关", "45潼关  ", "46函谷关", "47武关  ", "48阳平关", "49剑阁  ",
        "50葭萌关", "51涪水关", "52绵竹关", "53安平港", "54高唐港", "55西河港", "56白马港",
        "57昌阳港", "58临济港", "59海陵港", "60江都港", "61濡须港", "62顿丘港", "63官渡港",
        "64孟津港", "65解县港", "66新丰港", "67夏阳港", "68房陵港", "69芜湖港", "70虎林港",
        "71曲阿港", "72句章港", "73皖口港", "74九江港", "75陆口港", "76鄱阳港", "77卢陵港",
        "78夏口港", "79湖阳港", "80中庐港", "81乌林港", "82汉津港", "83江津港", "84罗县港",
        "85洞庭港", "86公安港", "87巫县港", "88      ", "89      ", "90      ", "91      ",
        "92      ", "93      ", "94      ", "95      ", "96      ", "97      ", "98      ",
        "99      ", "100      ", "101    ", "102     ", "103     ", "104     ", "105     ",
        "106     ", "107     ", "108     ", "109     ", "110     ", "111     ", "112     ",
        };
		
        EditorWindow infoWind;

        public GridBrush(MapEditor e) : base(e)
        {
            brushType = BrushType.TerrainType;
            infoWind = EditorWindow.AddWindow(1101, maskWindowRect, DrawWindow, "信息图");
            infoWind.visible = false;
            int count = 32;
            terrainTypeTitle = new string[count];
            for (int i = 0; i < count; ++i)
            {
                TerrainType terrainType = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(i);
                terrainTypeTitle[i] = terrainType.Name;
            }

            Game.Game.Instance.StartCoroutine(CreateLayerTexture());
        }

        // 准备图层贴图
        IEnumerator CreateLayerTexture()
        {
            int celSize = 128;
            GameObject texCreator = GameObject.Instantiate(Resources.Load("TerrainLayer")) as GameObject;
            UnityEngine.UI.Text text = texCreator.GetComponentInChildren<UnityEngine.UI.Text>();
            UnityEngine.UI.RawImage[] image = texCreator.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
            Camera cam = texCreator.GetComponent<Camera>();
            RenderTexture renderTexture = RenderTexture.GetTemporary(celSize, celSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            int count = 32;// GameData.Instance.ScenarioCommonData.TerrainTypes.Length;
            terrainTypeMaskCol = 8;
            while (terrainTypeMaskCol * terrainTypeMaskCol < count)
                terrainTypeMaskCol *= 2;

            terrainTypeTex = new Texture2D(terrainTypeMaskCol * celSize, terrainTypeMaskCol * celSize);
            terrainTypeMaskRow = terrainTypeMaskCol;
            Texture gridTex = Resources.Load<Texture>("layer_grid");
            for (int i = 0; i < count; ++i)
            {
                TerrainType terrainType = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(i);
                text.text = $"{terrainType.Name}\n{terrainType.Id}";
                text.color = terrainType.color;
                image[0].color = terrainType.color;
                image[0].texture = gridTex;
                cam.enabled = true;
                cam.targetTexture = renderTexture;
                cam.Render();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                cam.targetTexture = null;
                RenderTexture.active = renderTexture;
                Texture2D texture2D = new Texture2D(celSize, celSize);
                texture2D.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture2D.Apply(); // 应用更改
                RenderTexture.active = null; // 重置RenderTexture.active以避免潜在问题
                UnityEngine.Color[] pixels = texture2D.GetPixels();

                int xStart = i % terrainTypeMaskCol * texture2D.width;
                int yStart = (terrainTypeMaskCol - 1 - i / terrainTypeMaskCol) * texture2D.width;

                for (int x = 0; x < texture2D.width; ++x)
                    for(int y = 0; y < texture2D.height; ++y)
                    {
                        terrainTypeTex.SetPixel(xStart + x, yStart + y, pixels[x + y * texture2D.width]);
                    }

                GameObject.DestroyImmediate(texture2D);

            }
            RenderTexture.ReleaseTemporary(renderTexture);
            terrainTypeTex.Apply();
            Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            image[1].texture = terrainTypeTex;
            GameObject.Destroy(texCreator);
            yield return null;
        }

        /// <summary>
        /// 当进入相关编辑操作时调用的方法，用于设置全局地形类型透明度，并加载对应的地形类型纹理资源，加载完成后可能更新地形遮罩纹理
        /// </summary>
        public override void OnEnter()
        {
            // 这段代码在进入某个状态或操作开始时，设置了全局着色器属性，并通过循环加载多个纹理，将纹理存储到相应的数组中，并且在加载完成后，如果当前加载的纹理与 brushType 相关，会触发 UpdateTerrainMaskTex() 方法进行一些额外的处理，可能是更新地形的遮罩纹理以反映地形的变化
            Shader.SetGlobalFloat("_terrainTypeAlpha", 1);
            for (int i = 0; i < terrainTypeTexNames.Length; ++i)
            {
                terrainTypeTexes[i] = editor.map.CreateTexture("Editor/" + terrainTypeTexNames[i]);
                if (i == (int)brushType)
                    UpdateTerrainMaskTex();
            }
        }

        /// <summary>
        /// 根据给定的索引值转换为对应的颜色值，用于在地形类型遮罩等相关操作中确定颜色显示，基于遮罩纹理的行列数来计算颜色分量
        /// </summary>
        /// <param name="index">要转换的索引值，对应不同的地形类型等情况</param>
        /// <returns>计算得到的颜色值，以Color类型表示</returns>
        public UnityEngine.Color TypeIndexToColor(int index)
        {
            int col = index % terrainTypeMaskCol;
            int row = index / terrainTypeMaskCol;
            row = terrainTypeMaskRow - 1 - row;
            return new UnityEngine.Color((float)col / (float)terrainTypeMaskCol, (float)row / (float)terrainTypeMaskRow, 0);
        }

        /// <summary>
        /// 在地形类型遮罩纹理上设置指定坐标位置的像素颜色，基于给定的索引值转换得到颜色，同时会对y坐标进行反向处理（与纹理坐标系统相关）
        /// </summary>
        /// <param name="x">要设置颜色的像素的x坐标值</param>
        /// <param name="y">要设置颜色的像素的y坐标值</param>
        /// <param name="index">用于转换颜色的索引值，对应不同的地形类型等情况</param>
        public void SetTerrainTypeShow(int x, int y, int index)
        {
            y = terrainTypeMaskTex.height - y - 1;
            terrainTypeMaskTex.SetPixel(x, y, TypeIndexToColor(index));
        }

        /// <summary>
        /// 在地形类型遮罩纹理上设置指定坐标位置的像素颜色，基于给定的索引值、行列数等参数转换得到颜色，同时会对y坐标进行反向处理（与纹理坐标系统相关）
        /// </summary>
        /// <param name="x">要设置颜色的像素的x坐标值</param>
        /// <param name="y">要设置颜色的像素的y坐标值</param>
        /// <param name="index">用于转换颜色的索引值，对应不同的地形类型等情况</param>
        /// <param name="colCount">用于计算颜色的列数参数，一般对应遮罩纹理的列划分情况</param>
        /// <param name="rowCount">用于计算颜色的行数参数，一般对应遮罩纹理的行划分情况</param>
        public void SetTerrainMaskShowColor(int x, int y, int index, int colCount, int rowCount)
        {
            int col = index % colCount;
            int row = index / colCount;
            row = rowCount - 1 - row;
            UnityEngine.Color c = new UnityEngine.Color((float)col / (float)terrainTypeMaskCol, (float)row / (float)terrainTypeMaskRow, 0);
            y = terrainTypeMaskTex.height - y - 1;
            terrainTypeMaskTex.SetPixel(x, y, c);
        }

        /// <summary>
        /// 根据给定的笔刷类型，从地图地格数据中获取对应的数据属性值，例如获取地形类型、区域等具体属性值
        /// </summary>
        /// <param name="brushType">指定的笔刷类型，用于确定要获取的具体数据属性</param>
        /// <param name="data">地图地格数据实例，包含了各种相关的地图数据信息</param>
        /// <returns>获取到的对应属性值，以字节类型表示</returns>
        public byte GetGridDataProterty(BrushType brushType, MapGrid.GridData data)
        {
            switch (brushType)
            {
                case BrushType.TerrainType:
                    return data.terrainType;
                    //case BrushType.Area:
                    //    return data.areaId;
                    //case BrushType.Trap:
                    //    return data.trap;
                    //case BrushType.Dir:
                    //    return data.dir;
                    //case BrushType.Interior:
                    //    return data.interior;
                    //case BrushType.Defence:
                    //    return data.defence;
                    //case BrushType.Thief:
                    //    return data.thief;
                    //case BrushType.Flood:
                    //    return data.flood;
                    //case BrushType.Ruins:
                    //    return data.ruins;
            }
            return 0;
        }

        /// <summary>
        /// 根据给定的笔刷类型，设置地图地格数据中对应的数据属性值，例如设置地形类型、区域等具体属性值，返回更新后的地图地格数据实例
        /// </summary>
        /// <param name="brushType">指定的笔刷类型，用于确定要设置的具体数据属性</param>
        /// <param name="data">地图地格数据实例，包含了各种相关的地图数据信息，传入后会被修改对应属性值</param>
        /// <param name="value">要设置的属性值，以字节类型表示</param>
        /// <returns>更新后的地图地格数据实例，包含了设置后的属性值</returns>
        public MapGrid.GridData SetGridDataProterty(BrushType brushType, MapGrid.GridData data, byte value)
        {
            switch (brushType)
            {
                case BrushType.TerrainType:
                    data.terrainType = value;
                    break;
                    //case BrushType.Area:
                    //    data.areaId = value;
                    //    break;
                    //case BrushType.Trap:
                    //    data.trap = value;
                    //    break;
                    //case BrushType.Dir:
                    //    data.dir = value;
                    //    break;
                    //case BrushType.Interior:
                    //    data.interior = value;
                    //    break;
                    //case BrushType.Defence:
                    //    data.defence = value;
                    //    break;
                    //case BrushType.Thief:
                    //    data.thief = value;
                    //    break;
                    //case BrushType.Flood:
                    //    data.flood = value;
                    //    break;
                    //case BrushType.Ruins:
                    //    data.ruins = value;
                    //    break;
            }
            return data;
        }

        /// <summary>
        /// 更新地形类型遮罩纹理，根据当前的笔刷类型来设置相关参数（如行列数等），加载对应的纹理资源，设置全局纹理变量等操作，最后应用纹理更新
        /// </summary>
        public void UpdateTerrainMaskTex()
        {
            UpdateTerrainMaskTex(brushType);
        }

        /// <summary>
        /// 根据指定的笔刷类型更新地形类型遮罩纹理，设置纹理的相关参数（如行列数等），加载对应的纹理资源，设置全局纹理变量等操作，最后应用纹理更新
        /// </summary>
        /// <param name="b">指定的笔刷类型，用于确定如何更新地形类型遮罩纹理相关参数和操作</param>
        public void UpdateTerrainMaskTex(BrushType b)
        {
            if (terrainTypeMaskTex == null)
            {
                terrainTypeMaskTex = new Texture2D(editor.map.mapGrid.bounds.x, editor.map.mapGrid.bounds.y, TextureFormat.ARGB32, false);
                terrainTypeMaskTex.wrapMode = TextureWrapMode.Clamp;
                terrainTypeMaskTex.filterMode = FilterMode.Point;
            }

            switch (b)
            {
                case BrushType.TerrainType:
                    {
                        //terrainTypeMaskCol = 4;
                        //terrainTypeMaskRow = 8;
                    }
                    break;
                    //case BrushType.Area:
                    //    {
                    //        terrainTypeMaskCol = 16;
                    //        terrainTypeMaskRow = 16;
                    //    }
                    //    break;
                    //case BrushType.Dir:
                    //    {
                    //        terrainTypeMaskCol = 4;
                    //        terrainTypeMaskRow = 4;
                    //    }
                    //    break;
                    //case BrushType.Trap:
                    //    {
                    //        terrainTypeMaskCol = 2;
                    //        terrainTypeMaskRow = 2;
                    //    }
                    //    break;
                    //case BrushType.Interior:
                    //case BrushType.Defence:
                    //case BrushType.Thief:
                    //case BrushType.Flood:
                    //case BrushType.Ruins:
                    //    {
                    //        terrainTypeMaskCol = 2;
                    //        terrainTypeMaskRow = 1;
                    //    }
                    //    break;
            }
            //Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTexes[(int)b]);
            Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            Shader.SetGlobalFloat("_terrainTypeMaskCol", terrainTypeMaskCol);
            Shader.SetGlobalFloat("_terrainTypeMaskRow", terrainTypeMaskRow);

            for (int i = 0; i < editor.map.mapGrid.bounds.x; ++i)
            {
                for (int j = 0; j < editor.map.mapGrid.bounds.y; ++j)
                {
                    MapGrid.GridData data = editor.map.mapGrid.GetGridData(i, j);
                    SetTerrainMaskShowColor(i, j, GetGridDataProterty(b, data), terrainTypeMaskCol, terrainTypeMaskRow);
                }
            }

            terrainTypeMaskTex.Apply(false);
            Shader.SetGlobalTexture("_TerrainTypeMaskTex", terrainTypeMaskTex);
        }

        /// <summary>
        /// 当笔刷类型发生改变时调用的方法，主要用于初始化笔刷的不透明度为0，并更新地图地格的贴图（通过调用UpdateTerrainMaskTex方法）
        /// </summary>
        public override void OnBrushTypeChange()
        {
            //初始化笔刷
            opacity = 0;
            //更新地图地格贴图
            UpdateTerrainMaskTex();
        }

        /// <summary>
        /// 当季节发生改变时调用的方法，目前此方法为空实现，可能后续会添加与季节变化相关的地图编辑处理逻辑
        /// </summary>
        public override void OnSeasonChanged(int curSeason)
        {

        }

        /// <summary>
        /// 用于清除相关操作的方法，内部调用ClearBrushShow方法来执行具体的清除操作，比如清除笔刷显示相关效果等
        /// </summary>
        public override void Clear()
        {
            ClearBrushShow();
        }

        /// <summary>
        /// 执行清除笔刷显示相关效果的具体操作，包括清除临时六边形列表对应的范围遮罩颜色、清空临时列表，
        /// 若有颜色变化则应用范围遮罩，同时将地形类型显示标志的全局变量设置为0（即隐藏相关显示）
        /// </summary>
        public void ClearBrushShow()
        {
            bool changed = false;
            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                editor.map.mapGrid.SetRangMaskColor(coord.col, coord.row, UnityEngine.Color.clear);
                changed = true;
            }

            tempHexList.Clear();
            if (changed)
            {
                editor.map.mapGrid.ApplyRangMask();
            }
        }

        /// <summary>
        /// 打开文件对话框，加载指定格式（*.SHEX）的311地格数据文件，若成功选择文件，则调用地图地格数据的加载方法，并更新地形类型遮罩纹理
        /// </summary>
        //void Load311GridData()
        //{
        //    string[] path = WindowDialog.OpenFileDialog("地格文件(*.SHEX)|*.SHEX\0");
        //    if (path != null)
        //    {
        //        string fName = path[0];
        //        editor.map.mapGrid.LoadFrom311GridData(fName);
        //        UpdateTerrainMaskTex();
        //    }
        //}

        /// <summary>
        /// 打开保存文件对话框，以指定格式（*.SHEX）保存地图地格数据为311地格数据文件，若成功选择保存路径，则执行相应的保存操作
        /// </summary>
        //void SaveTo311GridData()
        //{
        //    string path = WindowDialog.SaveFileDialog("4791.SHEX", "地格文件(*.SHEX)|*.SHEX\0");
        //    if (path != null)
        //    {
        //        editor.map.mapGrid.SaveTo311GridData(path);
        //    }
        //}

        /// <summary>
        /// 在图形用户界面（GUI）上绘制相关的笔刷设置等控件，例如显示笔刷大小、可调节笔刷大小滑块、笔刷值输入框，
        /// 还有加载/保存311地格数据按钮、编辑模式选择、信息图显示切换、地格显示切换等功能选项，根据不同笔刷类型绘制相应的选择网格
        /// </summary>
        public override void OnGUI()
        {
            GUILayout.Label(String.Format("笔刷大小 [{0}]", size));
            float _size = GUILayout.HorizontalSlider(size, 0f, 12f);
            if ((int)_size != size)
            {
                size = (int)_size;
                OnBrushSizeChange();
            }

            //int _opacity = EditorUtility.IntField(opacity, "笔刷值");
            //if (_opacity != opacity) {
            //    opacity = _opacity;
            //}

            //GUILayout.BeginHorizontal();
            //if (GUILayout.Button("加载311地格数据"))
            //{
            //    Load311GridData();
            //}
            //if (GUILayout.Button("保存为311地格数据"))
            //{
            //    SaveTo311GridData();
            //}
            //GUILayout.EndHorizontal();


            UnityEngine.Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = UnityEngine.Color.cyan;
            int editMode = GUILayout.SelectionGrid(currentEditMode, toolbarTitle, 5, GUILayout.Height(60));
            if (editMode != currentEditMode)
            {
                currentEditMode = editMode;
                if (currentEditMode > 0)
                {
                    brushType = (BrushType)currentEditMode - 1;
                    Shader.SetGlobalFloat("_TerrainTypeShowFlag", 1);
                    OnBrushTypeChange();
                }
                else
                {
                    Shader.SetGlobalFloat("_TerrainTypeShowFlag", 0);
                }
            }

            GUI.backgroundColor = lastColor;
            infoWind.visible = GUILayout.Toggle(infoWind.visible, "信息视图");
            //infoLegend.visible = GUILayout.Toggle(infoLegend.visible, "颜色图例");
            bool show = GUILayout.Toggle(showGrid, "显示地格");
            if (show != showGrid)
            {
                showGrid = show;
                Shader.SetGlobalFloat("_GridFlag", showGrid ? 1 : 0);
            }

            if (currentEditMode <= 0) return;

            switch (brushType)
            {
                //case BrushType.Trap:
                //    DrawSelectionGrid(trapTypeTitle, 3);
                //    break;
                //case BrushType.Dir:
                //    DrawSelectionGrid(dirTypeTitle, 3);
                //    break;
                //case BrushType.Area:
                //    DrawSelectionGrid(moveStateTitle, 7);
                //    break;
                case BrushType.TerrainType:
                    DrawSelectionGrid(terrainTypeTitle, 4);
                    break;
                //case BrushType.Interior:
                //    DrawSelectionGrid(interiorTypeTitle, 2);
                //    break;
                //case BrushType.Defence:
                //    DrawSelectionGrid(defenceTypeTitle, 2);
                //    break;
                //case BrushType.Thief:
                //    DrawSelectionGrid(thiefTypeTitle, 2);
                //    break;
                //case BrushType.Flood:
                //    DrawSelectionGrid(floodTypeTitle, 2);
                //    break;
                //case BrushType.Ruins:
                //    DrawSelectionGrid(ruinsTypeTitle, 2);
                //    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 根据给定的类型标题数组和每行显示数量，绘制选择网格，根据用户选择更新笔刷的不透明度值
        /// </summary>
        void DrawSelectionGrid(string[] type, int count)
        {
            int state = GUILayout.SelectionGrid(opacity, type, count);
            if (state != opacity)
            {
                opacity = state;
            }
        }

        /// <summary>
        /// 在指定的编辑器窗口中绘制相关内容，目前主要是在窗口中以指定宽度和高度显示地形类型遮罩纹理（若窗口ID符合条件）
        /// </summary>
        void DrawWindow(int windowID, EditorWindow window)
        {
            //if (windowID != 1101) return;
            GUILayout.Label(terrainTypeMaskTex, GUILayout.Width(256), GUILayout.Height(256));
        }

        /// <summary>
        /// 更新方法，用于处理空格键按下/松开时地形类型显示标志的设置，
        /// 以及鼠标点击等交互操作，比如吸取目标值、修改地图数据、调整笔刷值等，同时根据鼠标位置绘制相关的辅助图形（Gizmos）
        /// </summary>
        public override void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Shader.SetGlobalFloat("_TerrainTypeShowFlag", 0);
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                Shader.SetGlobalFloat("_TerrainTypeShowFlag", 1);
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
            {
                if (hit.point != lastCenter)
                {
                    if (currentEditMode > 0 && !IsPointerOverUI())
                    {
                        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))
                        {
                            // 吸取目标值
                            SuckValue(hit.point, editor);
                            lastCenter = hit.point;
                        }
                        else if ((Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButton(0)) ||
                                 Input.GetMouseButtonDown(0))
                        {
                            Modify(hit.point, editor);
                            lastCenter = hit.point;
                        }
                        //当按下LeftAlt键 将笔刷值归0
                        else if (Input.GetKeyUp(KeyCode.LeftAlt))
                        {
                            opacity = 0;
                        }
                        //笔刷值减1事件
                        if (Input.GetKeyUp(KeyCode.KeypadMinus) || Input.GetKeyUp(KeyCode.Minus))
                        {
                            opacity = Math.Max(opacity - 1, 0);
                        }
                        //笔刷值加1事件
                        else if (Input.GetKeyUp(KeyCode.KeypadPlus) || Input.GetKeyUp(KeyCode.Equals))
                        {
                            opacity++;
                        }
                    }

                    DrawGizmos(hit.point);
                }
            }
        }

        List<Sango.Hexagon.Hex> tempHexList = new List<Hexagon.Hex>();
        Sango.Hexagon.Hex lastHexCenter = new Sango.Hexagon.Hex();

        /// <summary>
        /// 用于绘制辅助图形（Gizmos）的方法，根据给定的中心位置，先清除之前临时六边形列表对应的范围遮罩颜色，
        /// 然后基于新的六边形位置重新设置范围遮罩颜色，若有颜色变化则应用范围遮罩，实现辅助图形的动态更新显示效果
        /// </summary>
        public override void DrawGizmos(Vector3 center)
        {
            bool changed = false;
            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
            if (hex.IsSame(lastHexCenter))
            {
                return;
            }

            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                editor.map.mapGrid.SetRangMaskColor(coord.col, coord.row, UnityEngine.Color.clear);
                changed = true;
            }

            tempHexList.Clear();
            hex.Spiral(size, tempHexList);
            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                editor.map.mapGrid.SetRangMaskColor(coord.col, coord.row, UnityEngine.Color.cyan);
                changed = true;
            }

            if (changed)
            {
                editor.map.mapGrid.ApplyRangMask();
            }
        }

        /// <summary>
        /// 根据给定的笔刷类型，获取其相反操作值（针对部分特定笔刷类型，返回与当前不透明度相反的值，否则返回原值）
        /// </summary>
        /// <param name="brushType">笔刷类型，用于确定如何获取相反操作值</param>
        /// <param name="opacity">当前笔刷的不透明度值</param>
        /// <returns>获取到的相反操作值，以整数表示</returns>
        int InvertOpacity(BrushType brushType, int opacity)
        {
            switch (brushType)
            {
                //case BrushType.Interior:
                //case BrushType.Defence:
                //case BrushType.Thief:
                //case BrushType.Flood:
                //case BrushType.Ruins:
                //    {
                //        return Math.Abs(opacity - 1);
                //    }
            }

            return opacity;
        }

        /// <summary>
        /// 从地图指定位置吸取目标值，根据鼠标点击位置获取对应的六边形位置、地格数据，
        /// 然后获取该位置对应笔刷类型的属性值，并将其设置为笔刷的不透明度值
        /// </summary>
        /// <param name="center">鼠标点击的中心位置，用于确定吸取目标值的地图位置</param>
        /// <param name="editor">地图编辑器实例，用于获取相关的地图数据等操作</param>
        void SuckValue(Vector3 center, MapEditor editor)
        {
            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
            Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(hex);
            MapGrid.GridData data = editor.map.mapGrid.GetGridData(coord.col, coord.row);
            opacity = Mathf.Clamp(GetGridDataProterty(brushType, data), 0, 255);
        }

        /// <summary>
        /// 根据给定的地图位置，修改对应地格数据的属性值（基于笔刷类型和当前笔刷值等），
        /// 同时更新地形类型遮罩纹理显示颜色，若笔刷类型为地形类型还会进行一些额外的地图可移动性相关操作，
        /// 最后应用地形类型遮罩纹理更新
        /// </summary>
        /// <param name="center">地图中的位置，用于确定要修改的地格数据所在位置</param>
        /// <param name="editor">地图编辑器实例，用于获取和修改相关的地图数据等操作</param>
        public override void Modify(Vector3 center, MapEditor editor)
        {
            int value = opacity;
            if (Input.GetKey(KeyCode.LeftShift))
                value = InvertOpacity(brushType, opacity);

            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                MapGrid.GridData data = editor.map.mapGrid.GetGridData(coord.col, coord.row);
                SetGridDataProterty(brushType, data, (byte)value);
                SetTerrainMaskShowColor(coord.col, coord.row, GetGridDataProterty(brushType, data), terrainTypeMaskCol, terrainTypeMaskRow);
                if (brushType == BrushType.TerrainType)
                {
                    editor.map.mapGrid.BeginUpdateMovable(coord.col, coord.row);
                }
            }

            if (brushType == BrushType.TerrainType)
                editor.map.mapGrid.EndUpdateMovable();

            terrainTypeMaskTex.Apply(false);
        }
    }
}