using LuaInterface;
using Sango.Game;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Render
{

    public class MapGrid : MapProperty
    {
        /// 我们的地图是一个odd-q排列的地格
        public Sango.Hexagon.HexWorld hexWorld;
        public Vector2Int bouns;
        public int gridSize = -1;
        public int gridVertexCuont;
        public Texture gridTexture;
        public string gridTextureName = "grid";
        public Texture2D GridMaskTexture;   // a为是否显示r红色格子g绿色格子b蓝色格子
        public Texture2D RangeMaskTexture;  // a为是否显示r为当前选择地格g为我方范围b为敌方范围
        private GridData[][] gridDatas;
        public enum GridState : int
        {
            Moveable = 0,
        }


        public struct San11GridData
        {
            // HeadInfo = "SHEX0008";

            // 311结构
            public byte tType;     // 地形类型 (值)
            public byte areaId;    // 区域ID (值)
            public byte lpB;       // lpB (值)
            public byte trap;      // 陷阱 0:无 1堤防 2落石
            public byte dir;       // 方向 0:左上 1:上 2右上 3左下 4下 5右下 6无
            public byte interior;  // 内政
            public byte defence;   // 防守
            public byte thief;     // 贼
            public byte flood;     // 水淹
            public byte fire;      // 火焰的智力(值),为0则没有火,否则为放火者的智力值0-255
            public byte ruins;     // 庙与遗迹 

            public void OnLoad(int versionCode, BinaryReader reader)
            {
                tType = reader.ReadByte();
                areaId = reader.ReadByte();
                lpB = reader.ReadByte();
                trap = reader.ReadByte();
                dir = reader.ReadByte();
                interior = reader.ReadByte();
                defence = reader.ReadByte();
                thief = reader.ReadByte();
                flood = reader.ReadByte();
                fire = reader.ReadByte();
                ruins = reader.ReadByte();
            }
            public void OnSave(BinaryWriter writer)
            {
                writer.Write(tType);
                writer.Write(areaId);
                writer.Write(lpB);
                writer.Write(trap);
                writer.Write(dir);
                writer.Write(interior);
                writer.Write(defence);
                writer.Write(thief);
                writer.Write(flood);
                writer.Write(fire);
                writer.Write(ruins);
            }
        }

        public class GridData
        {
            public byte terrainType;


//#if UNITY_EDITOR
            [NoToLua]
            public UnityEngine.UI.Text textObj;
            [NoToLua]
            public bool visible;
//#endif

            //public int terrainState;
            //public San11GridData san11GridData;
            public GridData()
            {
                //san11GridData = new San11GridData()
                //{
                //    tType = 31,
                //    dir = 6
                //};
            }
            //public void SetGridState(GridState state, bool b)
            //{
            //    int stateValue = (1 << (int)state);
            //    if (b) {
            //        if ((terrainState & stateValue) == stateValue)
            //            return;
            //        terrainState |= (1 << (int)state);
            //    }
            //    else {
            //        if ((terrainState & stateValue) != stateValue)
            //            return;
            //        terrainState ^= (1 << (int)state);
            //    }
            //}
            //public bool HasGridState(GridState state)
            //{
            //    int stateValue = (1 << (int)state);
            //    return (terrainState & stateValue) == stateValue;
            //}
            public void OnLoad(int versionCode, BinaryReader reader)
            {
                terrainType = reader.ReadByte();

                if (versionCode < 5)
                {
                    reader.ReadInt32();
                    if (versionCode > 0)
                    {
                        new San11GridData().OnLoad(versionCode, reader);
                    }
                }
            }
            public void OnSave(BinaryWriter writer)
            {
                writer.Write(terrainType);
                //writer.Write(terrainState);
                //san11GridData.OnSave(writer);
            }
        }

        public MapGrid(MapRender map) : base(map)
        {
            hexWorld = new Hexagon.HexWorld(new Hexagon.Point(gridSize, gridSize), new Hexagon.Point(0, 0));
        }
        public override void Init()
        {
            base.Init();
            if (gridSize <= 0)
                Create(20);
            SetGridTexture("grid");
            ShowGrid(true);
        }
        public override void Clear()
        {
            base.Clear();
            Shader.SetGlobalTexture("_GridMask", null);
            Shader.SetGlobalTexture("_RangeMask", null);
            gridDatas = null;
        }

        public void Create(int size)
        {
            Debug.Log(string.Format("创建格子: size: {0}", size));
            gridSize = Mathf.Max(size, map.mapData.quadSize);
            gridVertexCuont = gridSize / map.mapData.quadSize;
            bouns = new Vector2Int(map.mapData.wrold_width / gridSize, map.mapData.wrold_height / gridSize);
            hexWorld.InitLayout(new Hexagon.Point(gridSize, gridSize), new Hexagon.Point(0, 0));

            if (GridMaskTexture != null)
            {
                GameObject.Destroy(GridMaskTexture);
            }
            GridMaskTexture = new Texture2D(bouns.x, bouns.y, TextureFormat.ARGB32, false);
            GridMaskTexture.wrapMode = TextureWrapMode.Clamp;
            GridMaskTexture.filterMode = FilterMode.Point;

            for (int i = 0; i < bouns.x; ++i)
            {
                for (int j = 0; j < bouns.y; ++j)
                {
                    GridMaskTexture.SetPixel(i, j, Color.clear);
                }
            }
            GridMaskTexture.Apply(false);

            if (RangeMaskTexture != null)
            {
                GameObject.Destroy(RangeMaskTexture);
            }

            RangeMaskTexture = new Texture2D(bouns.x, bouns.y, TextureFormat.ARGB32, false);
            RangeMaskTexture.wrapMode = TextureWrapMode.Clamp;
            RangeMaskTexture.filterMode = FilterMode.Point;

            for (int i = 0; i < bouns.x; ++i)
            {
                for (int j = 0; j < bouns.y; ++j)
                {
                    RangeMaskTexture.SetPixel(i, j, Color.clear);
                }
            }
            RangeMaskTexture.Apply(false);

            Shader.SetGlobalTexture("_GridMask", GridMaskTexture);
            Shader.SetGlobalTexture("_RangeMask", RangeMaskTexture);
            Shader.SetGlobalFloat("_GridSize", gridSize);


            gridDatas = new GridData[bouns.x][];
            for (int x = 0; x < gridDatas.Length; ++x)
            {
                GridData[] yTable = new GridData[bouns.y];
                for (int y = 0; y < yTable.Length; ++y)
                {
                    yTable[y] = new GridData
                    {
                        terrainType = 31,
                        //terrainState = 0
                    };
                }
                gridDatas[x] = yTable;
            }

        }
        public void ClearGridData()
        {
            for (int x = 0; x < gridDatas.Length; ++x)
            {
                GridData[] yTable = gridDatas[x];
                for (int y = 0; y < yTable.Length; ++y)
                {
                    GridData data = yTable[y];
                    data.terrainType = 0;
                    //data.terrainState = 0;
                    yTable[y] = data;
                    SetMovable(x, y, false);
                }
            }
        }
        public void SetGridTexture(string name)
        {
            gridTextureName = name;
            Loader.TextureLoader.LoadFromFile(map.FindTexture("Grid/" + gridTextureName), null, (UnityEngine.Object tex, object obj) =>
            {
                Texture t = tex as Texture;
                gridTexture = t;
                t.mipMapBias = -1110.4f;
                Shader.SetGlobalTexture("_GridTex", gridTexture);

            }, true);
        }
        //public void LoadFrom311GridData(string filename)
        //{
        //    FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
        //    BinaryReader binr = new BinaryReader(fs);
        //    binr.ReadBytes(8);
        //    for (int i = 0; i < 40000; ++i) {
        //        int y = i % 200;
        //        int x = i / 200;
        //        GridData data = gridDatas[x + 28][y + 28];
        //        data.san11GridData.OnLoad(0, binr);
        //    }
        //}
        //public void SaveTo311GridData(string filename)
        //{
        //    FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
        //    BinaryWriter binr = new BinaryWriter(fs);
        //    //SHEX0008
        //    binr.Write('S');
        //    binr.Write('H');
        //    binr.Write('E');
        //    binr.Write('X');
        //    binr.Write('0');
        //    binr.Write('0');
        //    binr.Write('0');
        //    binr.Write('8');
        //    for (int i = 0; i < 40000; ++i) {
        //        int y = i % 200;
        //        int x = i / 200;
        //        GridData data = gridDatas[x + 28][y + 28];
        //        data.san11GridData.OnSave(binr);
        //    }
        //}
        internal override void OnSave(BinaryWriter writer)
        {
            writer.Write(gridSize);
            for (int x = 0; x < gridDatas.Length; ++x)
            {
                GridData[] yTable = gridDatas[x];
                for (int y = 0; y < yTable.Length; ++y)
                {
                    GridData data = yTable[y];
                    data.OnSave(writer);
                }
            }
            if (string.IsNullOrEmpty(gridTextureName))
                writer.Write("");
            else
                writer.Write(gridTextureName);
        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            string gridTexName = null;
            if (versionCode < 6)
                gridTexName = reader.ReadString();
            int size = reader.ReadInt32();

            Create(size);
            map.OnInitGrid();

            //Create(5, "grid");
            for (int x = 0; x < gridDatas.Length; ++x)
            {
                GridData[] yTable = gridDatas[x];
                for (int y = 0; y < yTable.Length; ++y)
                {
                    GridData data = yTable[y];
                    data.OnLoad(versionCode, reader);
                    yTable[y] = data;
                    Game.TerrainType terrainType = Game.GameData.Instance.ScenarioCommonData.TerrainTypes.Get(data.terrainType);
                    if (terrainType != null && terrainType.moveable)
                    {
                        int py = GridMaskTexture.height - y - 1;
                        Color c = GridMaskTexture.GetPixel(x, py);
                        c.a = 1;
                        GridMaskTexture.SetPixel(x, py, c);
                    }

                    map.OnBindGridData(x, y, data);

                }
            }
            if (versionCode >= 6)
                gridTexName = reader.ReadString();
            SetGridTexture(string.IsNullOrEmpty(gridTexName) ? "grid" : gridTexName);
            ApplyGridMask();
        }
        public void ShowGrid(bool b)
        {
            Shader.SetGlobalFloat("_GridFlag", b ? 1 : 0);
        }
        public void SetDarkMask(bool b)
        {
            Shader.SetGlobalFloat("_DarkFlag", b ? 1 : 0);
        }
        public void SetGridEnable(int x, int y, bool b, bool init = false)
        {
            y = GridMaskTexture.height - y - 1;
            if (!init)
            {
                Color c = GridMaskTexture.GetPixel(x, y);
                c.a = b ? 1 : 0;
                GridMaskTexture.SetPixel(x, y, c);
            }
            else
            {
                GridMaskTexture.SetPixel(x, y, b ? Color.black : Color.clear);
            }
        }
        public bool GetMovable(GridData data)
        {
            Game.TerrainType terrainType = Game.GameData.Instance.ScenarioCommonData.TerrainTypes.Get(data.terrainType);
            return terrainType.moveable;
        }

        public void BeginUpdateMovable(int x, int y)
        {
            GridData data = gridDatas[x][y];
            bool moveable = GetMovable(data);
            y = GridMaskTexture.height - y - 1;
            Color c = GridMaskTexture.GetPixel(x, y);
            c.a = moveable ? 1 : 0;
            GridMaskTexture.SetPixel(x, y, c);
        }

        public void SetMovable(int x, int y, bool b)
        {
            //GridData data = gridDatas[x][y];
            //data.SetGridState(GridState.Moveable, b);
            //gridDatas[x][y] = data;

            y = GridMaskTexture.height - y - 1;
            Color c = GridMaskTexture.GetPixel(x, y);
            c.a = b ? 1 : 0;
            GridMaskTexture.SetPixel(x, y, c);
        }
        public void EndUpdateMovable()
        {
            ApplyGridMask();
        }

        public void SetTerrainType(int x, int y, int t)
        {
            GridData data = gridDatas[x][y];
            data.terrainType = (byte)t;
            gridDatas[x][y] = data;
        }
        public void SetGridMaskColor(int x, int y, Color c)
        {
            y = GridMaskTexture.height - y - 1;
            GridMaskTexture.SetPixel(x, y, c);
        }
        public void ApplyGridMask()
        {
            GridMaskTexture.Apply(false);
        }
        public void SetRangMaskColor(int x, int y, Color c)
        {
            y = RangeMaskTexture.height - y - 1;
            RangeMaskTexture.SetPixel(x, y, c);
        }
        public void ApplyRangMask()
        {
            RangeMaskTexture.Apply(false);
        }
        public GridData GetGridData(int x, int y)
        {
            return gridDatas[x][y];
        }
        public float GetGridHeight(int x, int y)
        {
            int xCount = gridVertexCuont * x + gridVertexCuont / 2;
            int yCount = gridVertexCuont * y + (x % 2) * gridVertexCuont / 2 + gridVertexCuont / 2;
            return map.mapData.GetHeight(xCount, yCount);
        }
        public Vector3 CoordsToPosition(int c, int r)
        {
            return hexWorld.CoordsToPosition(c, r);
        }
        public Vector2Int PositionToCoords(float x, float y)
        {
            int c = Mathf.FloorToInt((x + gridSize / 2) / gridSize);
            int r = Mathf.FloorToInt((y + gridSize / 2 + (c % 2) * gridSize / 2) / gridSize);
            return new Vector2Int(c, r);
        }


        List<GridData> last;
        List<GridData> temp1 = new List<GridData>();
        List<GridData> temp2 = new List<GridData>();

        int switchIndex = 1;
        Transform textROOT;
        public void Update(Tools.Rect rect)
        {

//#if UNITY_EDITOR
            if (last != null)
            {
                for (int i = 0; i < last.Count; i++)
                {
                    last[i].visible = false;
                }
            }

            List<GridData> temp = switchIndex % 2 == 0 ? temp1 : temp2;
            Vector2Int lt = map.PositionToCoords(rect.xMin, rect.yMin);
            Vector2Int rb = map.PositionToCoords(rect.xMax, rect.yMax);

            for (int x = lt.x; x <= rb.x; ++x)
            {
                for (int y = lt.y; y <= rb.y; ++y)
                {
                    if (x >= 0 && x < bouns.x && y >= 0 && y < bouns.y)
                    {
                        GridData gridData = GetGridData(x, y);
                        if (gridData.textObj == null)
                        {
                            TerrainType terrainType = Sango.Game.GameData.Instance.ScenarioCommonData.TerrainTypes.Get(gridData.terrainType);
                            if (terrainType == null)
                                terrainType = Sango.Game.GameData.Instance.ScenarioCommonData.TerrainTypes[0];

                            if (terrainType.moveable)
                            {
                                if (textROOT == null)
                                {
                                    textROOT = GameObject.Find("GridTextRoot").transform;
                                }

                                GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("GridText")) as GameObject;
                                obj.transform.SetParent(textROOT, false);
                                UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
                                text.text = $"{x},{y} \n{terrainType.Name}:{terrainType.baseCost}";
                                gridData.textObj = text;
                                Vector3 pos = map.CoordsToPosition(x, y);
                                pos.y = GetGridHeight(x, y) + 5f;
                                obj.transform.localPosition = pos;
                                temp.Add(gridData);
                                gridData.visible = true;
                            }
                        }
                        else
                        {
                            if (!gridData.textObj.gameObject.activeInHierarchy)
                                gridData.textObj.gameObject.SetActive(true);
                            gridData.visible = true;
                            temp.Add(gridData);
                        }
                    }
                }
            }

            if (last != null)
            {
                for (int i = 0; i < last.Count; i++)
                {
                    GridData gridData = last[i];
                    if (!gridData.visible)
                    {
                        gridData.textObj.gameObject.SetActive(false);
                    }
                }
                last.Clear();
            }
            if (switchIndex == 1)
                switchIndex = 2;
            else
                switchIndex = 1;
            last = temp;
//#endif
        }
    }
}
