
using Sango.Core;
using Sango.Tools;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sango.Render
{

    public class MapGrid : MapProperty
    {
        /// 我们的地图是一个odd-q排列的地格
        public Sango.Hexagon.HexWorld hexWorld;
        public Vector2Int bounds;
        public int gridSize = -1;
        public int gridVertexCount;
        public Texture gridTexture;
        public string gridTextureName = "grid";
        private int quadSize = 20;
        public Texture2D GridMaskTexture;   // a为是否显示r红色格子g绿色格子b蓝色格子
        public Texture2D RangeMaskTexture;  // a为是否显示r为当前选择地格g为我方范围b为敌方范围
        public Texture2D DarkMaskTexture;  // a为是否显示

        private GridData[][] gridDatas;
        public enum GridState : int
        {
            None = 0,
            Defence = 1,
            Interior = 2,
            Thief = 3,
        }

        public class San11GridData
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
            public int terrainState;
            public ushort areaId;


            public UnityEngine.UI.Text textObj;
            public bool visible;

            //public int terrainState;

            public San11GridData san11GridData;
            public GridData()
            {
                san11GridData = new San11GridData()
                {
                    tType = 31,
                    dir = 6
                };
            }
            public void SetTerrainType(byte v)
            {
                terrainType = v;
            }


            public void SetGridState(GridState state, bool b)
            {
                int stateValue = (1 << (int)state);
                if (b)
                {
                    if ((terrainState & stateValue) == stateValue)
                        return;
                    terrainState |= (1 << (int)state);
                }
                else
                {
                    if ((terrainState & stateValue) != stateValue)
                        return;
                    terrainState ^= (1 << (int)state);
                }
            }
            public bool HasGridState(GridState state)
            {
                int stateValue = (1 << (int)state);
                return (terrainState & stateValue) == stateValue;
            }

            public void OnLoad(int versionCode, BinaryReader reader)
            {
                terrainType = reader.ReadByte();
                if (versionCode >= 7)
                    terrainState = reader.ReadInt32();
                if (versionCode >= 8)
                    areaId = reader.ReadUInt16();

                if (versionCode < 5)
                {
                    reader.ReadInt32();
                    if (versionCode > 0)
                    {
                        San11GridData sanData = new San11GridData();
                        sanData.OnLoad(versionCode, reader);
                        terrainType = (byte)(sanData.tType + 1);
                        areaId = (ushort)(sanData.areaId + 1);
                        if (sanData.defence > 0)
                            SetGridState(GridState.Defence, true);
                        if (sanData.thief > 0)
                            SetGridState(GridState.Thief, true);
                        if (sanData.interior > 0)
                            SetGridState(GridState.Interior, true);
                    }
                }
            }
            public void OnSave(BinaryWriter writer)
            {
                writer.Write(terrainType);
                writer.Write(terrainState);
                writer.Write(areaId);
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
                Create(quadSize);
            SetGridTexture("grid");
            ShowGrid(true);
            SetDarkMask(false);
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
            gridVertexCount = gridSize / map.mapData.quadSize;
            bounds = new Vector2Int(map.mapData.world_width / gridSize, map.mapData.world_height / gridSize);
            hexWorld.InitLayout(new Hexagon.Point(gridSize, gridSize), new Hexagon.Point(0, 0));

            if (GridMaskTexture != null)
            {
                GameObject.Destroy(GridMaskTexture);
            }
            GridMaskTexture = new Texture2D(bounds.x, bounds.y, TextureFormat.ARGB32, false);
            GridMaskTexture.wrapMode = TextureWrapMode.Clamp;
            GridMaskTexture.filterMode = FilterMode.Point;

            for (int i = 0; i < bounds.x; ++i)
            {
                for (int j = 0; j < bounds.y; ++j)
                {
                    GridMaskTexture.SetPixel(i, j, Color.clear);
                }
            }
            GridMaskTexture.Apply(false);

            if (RangeMaskTexture != null)
            {
                GameObject.Destroy(RangeMaskTexture);
            }

            RangeMaskTexture = new Texture2D(bounds.x, bounds.y, TextureFormat.ARGB32, false);
            RangeMaskTexture.wrapMode = TextureWrapMode.Clamp;
            RangeMaskTexture.filterMode = FilterMode.Point;

            for (int i = 0; i < bounds.x; ++i)
            {
                for (int j = 0; j < bounds.y; ++j)
                {
                    RangeMaskTexture.SetPixel(i, j, Color.clear);
                }
            }
            RangeMaskTexture.Apply(false);


            DarkMaskTexture = new Texture2D(bounds.x, bounds.y, TextureFormat.ARGB32, false);
            DarkMaskTexture.wrapMode = TextureWrapMode.Clamp;
            DarkMaskTexture.filterMode = FilterMode.Point;

            for (int i = 0; i < bounds.x; ++i)
            {
                for (int j = 0; j < bounds.y; ++j)
                {
                    DarkMaskTexture.SetPixel(i, j, Color.clear);
                }
            }
            DarkMaskTexture.Apply(false);

            Shader.SetGlobalTexture("_GridMask", GridMaskTexture);
            Shader.SetGlobalTexture("_RangeMask", RangeMaskTexture);
            Shader.SetGlobalTexture("_DarkMask", DarkMaskTexture);
            Shader.SetGlobalFloat("_GridSize", gridSize);




            gridDatas = new GridData[bounds.x][];
            for (int x = 0; x < gridDatas.Length; ++x)
            {
                GridData[] yTable = new GridData[bounds.y];
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
            Texture tex = map.CreateTexture("Grid/" + gridTextureName);
            gridTexture = tex;
            tex.mipMapBias = -1110.4f;
            Shader.SetGlobalTexture("_GridTex", gridTexture);
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
        public void SaveTo311GridData(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            BinaryWriter binr = new BinaryWriter(fs);
            //SHEX0008
            binr.Write('S');
            binr.Write('H');
            binr.Write('E');
            binr.Write('X');
            binr.Write('0');
            binr.Write('0');
            binr.Write('0');
            binr.Write('8');
            for (int i = 0; i < 40000; ++i)
            {
                int y = i % 200;
                int x = i / 200;
                GridData data = gridDatas[x + 28][y + 28];

                if (data.san11GridData != null)
                {
                    data.san11GridData.tType = (byte)(data.terrainType - 1);
                    data.san11GridData.areaId = (byte)(data.areaId - 1);
                    data.san11GridData.defence = (byte)(data.HasGridState(GridState.Defence) ? 1 : 0);
                    data.san11GridData.thief = (byte)(data.HasGridState(GridState.Thief) ? 1 : 0);
                    data.san11GridData.interior = (byte)(data.HasGridState(GridState.Interior) ? 1 : 0);
                    data.san11GridData.OnSave(binr);
                }
            }
        }

        public void LoadFrom311GridData(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            BinaryReader binr = new BinaryReader(fs);
            binr.ReadBytes(8);
            for (int i = 0; i < 40000; ++i)
            {
                int y = i % 200;
                int x = i / 200;
                GridData data = gridDatas[x + 28][y + 28];
                San11GridData sanData = new San11GridData();
                sanData.OnLoad(0, binr);
                data.terrainType = (byte)(sanData.tType + 1);
                data.areaId = (ushort)(sanData.areaId + 1);
                data.SetGridState(GridState.Defence, sanData.defence > 0);
                data.SetGridState(GridState.Thief, sanData.thief > 0);
                data.SetGridState(GridState.Interior, sanData.interior > 0);
                data.san11GridData = sanData;
            }
        }

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

        internal override void OnSaveScale(BinaryWriter writer, int scale)
        {
            writer.Write(gridSize);

            int scaleLength = gridDatas.Length * scale;
            for (int x = 0; x < scaleLength; ++x)
            {
                GridData[] yTable = gridDatas[x / 2];
                int y_scaleLength = yTable.Length * scale;
                for (int y = 0; y < y_scaleLength; ++y)
                {
                    GridData data = yTable[y / 2];
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
                    TerrainType terrainType = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(data.terrainType);
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
            TerrainType terrainType = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(data.terrainType);
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
        public void SetDarkMaskColor(int x, int y, Color c)
        {
            y = DarkMaskTexture.height - y - 1;
            DarkMaskTexture.SetPixel(x, y, c);
        }
        public void ApplyDarkMask()
        {
            DarkMaskTexture.Apply(false);
        }

        public GridData GetGridData(int x, int y)
        {
            return gridDatas[x][y];
        }
        public float GetGridHeight(int x, int y)
        {
            int xCount = gridVertexCount * x + gridVertexCount / 2;
            int yCount = gridVertexCount * y + (x % 2) * gridVertexCount / 2 + gridVertexCount / 2;
            return map.mapData.GetHeight(xCount, yCount);
        }

        public float GetGridWaterHeight(int x, int y)
        {
            int xCount = gridVertexCount * x + gridVertexCount / 2;
            int yCount = gridVertexCount * y + (x % 2) * gridVertexCount / 2 + gridVertexCount / 2;
            return map.mapData.GetWaterHeight(xCount, yCount);
        }

        public Vector3 CoordsToPosition(int c, int r)
        {
            return hexWorld.CoordsToPosition(c, r);
        }
        public Vector2Int PositionToCoords(float x, float y)
        {
            return hexWorld.PositionToCoords(new Vector3(x, 0, y));
        }

        public Vector2Int PositionToCoords(Vector3 vector3)
        {
            return hexWorld.PositionToCoords(vector3);
        }

        //#if SANGO_DEBUG

        List<GridData> last;
        List<GridData> temp1 = new List<GridData>();
        List<GridData> temp2 = new List<GridData>();

        int switchIndex = 1;
        Transform textROOT;

        public void Update(Tools.Rect rect)
        {
            if (!MapEditor.IsEditOn) return;
            //if (GameDebug.enabled == false) return;
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
            StringBuilder stringBuilder = new StringBuilder();
            for (int x = lt.x; x <= rb.x; ++x)
            {
                for (int y = lt.y; y <= rb.y; ++y)
                {
                    if (x >= 0 && x < bounds.x && y >= 0 && y < bounds.y)
                    {
                        GridData gridData = GetGridData(x, y);
                        bool has = false;
                        stringBuilder.Clear();
                        if (gridData.HasGridState(GridState.Defence))
                        {
                            stringBuilder.AppendLine("<color=#1100ff>防</color>");
                            has = true;
                        }
                        if (gridData.HasGridState(GridState.Interior))
                        {
                            stringBuilder.AppendLine("<color=#11ff00>内</color>");
                            has = true;
                        }
                        if (gridData.HasGridState(GridState.Thief))
                        {
                            stringBuilder.AppendLine("<color=#ff0011>贼</color>");
                            has = true;
                        }
                        TerrainType terrainType = Sango.Core.GameData.Instance.ScenarioCommonData.TerrainTypes.Get(gridData.terrainType);
                        if (terrainType == null)
                            terrainType = Sango.Core.GameData.Instance.ScenarioCommonData.TerrainTypes[0];
                        if (terrainType.moveable && has)
                        {
                            if (gridData.textObj == null)
                            {
                                if (textROOT == null)
                                {
                                    textROOT = GameObject.Find("GridTextRoot").transform;
                                }

                                GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("GridText")) as GameObject;
                                obj.transform.SetParent(textROOT, false);
                                UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();

                                gridData.textObj = text;
                                Vector3 pos = map.CoordsToPosition(x, y);
                                pos.y = GetGridHeight(x, y) + 5f;
                                obj.transform.localPosition = pos;
                                temp.Add(gridData);
                                gridData.visible = true;
                                gridData.textObj.text = stringBuilder.ToString();
                            }
                            else
                            {
                                if (!gridData.textObj.gameObject.activeInHierarchy)
                                    gridData.textObj.gameObject.SetActive(true);
                                gridData.visible = true;
                                temp.Add(gridData);
                                gridData.textObj.text = stringBuilder.ToString();
                            }
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
        }
        //#endif
    }
}
