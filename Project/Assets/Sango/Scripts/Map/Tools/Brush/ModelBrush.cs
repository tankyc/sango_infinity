using Sango.Game;
using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using UnityEngine;

namespace Sango.Tools
{
    public class ModelBrush : BrushBase
    {
        public class EditModelConfig
        {
            public Sango.Game.ModelConfig modelConfig;
            public string mod;
            public string shaderName;
            public bool isShardMat;
            // public Texture texture;
            public List<MapObject> instanceList = new List<MapObject>();

            //public string modelPath
            //{
            //    get { return string.Format("{0}/Mods/{1}/Assets/{2}", Path.ContentRootPath, mod, model); }
            //}
            //public string texturePath
            //{
            //    get { return string.Format("{0}/Mods/{1}/Assets/{2}", Path.ContentRootPath, mod, textureNmae); }
            //}
        }


        List<ModelConfig> configList = new List<ModelConfig>();
        string default_data_save_path;// = XPath.ContentRootPath + "/Mod/Map/Scripts/Data/data_model.lua";

        List<ModelConfig> currentConfigList;

        int objectIndex = -1;
        List<IMapManageObject> currentStaticModelList;
        bool isShowModelConfig = true;

        List<ModelShowInfo> currenShowModelInfo = new List<ModelShowInfo>();


        public class ModelShowInfo
        {
            public ModelConfig bindConfig;
            public IMapManageObject bidMapObject;
            public string[] showContent;

            public void Draw(ModelBrush brush, ModelConfig c)
            {
                if (bindConfig != c)
                {
                    bindConfig = c;

                    showContent = new string[5];
                    showContent[0] = c.Id.ToString();
                    showContent[1] = c.Name;
                    //showContent[2] = c.mod;
                    showContent[3] = c.model;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(showContent[0], GUILayout.Width(64)))
                {
                    brush.SelectModel(bindConfig);
                }
                if (GUILayout.Button("修改", GUILayout.Width(48)))
                {
                    brush.ModifyModelConfig(bindConfig);
                }

                GUILayout.Label(showContent[1]);
                GUILayout.Label(showContent[2]);
                GUILayout.Label(showContent[3]);

                GUILayout.EndHorizontal();
            }

            public void Draw(ModelBrush brush, IMapManageObject m)
            {
                if (bidMapObject != m)
                {
                    bidMapObject = m;

                    showContent = new string[3];
                    showContent[0] = m.bindId.ToString();
                    showContent[1] = m.objId.ToString();
                    showContent[2] = m.modelId.ToString();
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("选择", GUILayout.Width(48)))
                {
                    // 镜头切换至模型处,并选择模型
                    brush.editor.ForceCameraToGameObject(bidMapObject.GetGameObject());
                }
                GUILayout.Space(3);
                // 可手动修改bindId,可以用于绑定城池
                GUI.changed = false;
                GUILayout.Label("绑定ID:", GUILayout.Width(48));
                string bindIdStr = GUILayout.TextField(showContent[0], GUILayout.Width(40));
                if (GUI.changed)
                {
                    int bindId;
                    if (int.TryParse(bindIdStr, out bindId))
                    {

                        if (bindId != bidMapObject.bindId)
                        {
                            bidMapObject.bindId = bindId;
                            showContent[0] = m.bindId.ToString();
                        }
                    }
                }
                GUILayout.Space(3);
                GUILayout.Label(showContent[1], GUILayout.Width(20));
                GUILayout.Space(3);
                GUILayout.Label(showContent[2]);

                GUILayout.EndHorizontal();
            }
        }

        public bool randomDir = false;
        public GameObject model = null;
        public ModelConfig modelConfig = null;
        public bool anchorByGrid = false;

        private string[] objectTypeTitle = new string[] { "所有", "城", "关", "港", "内", "军", "植", "其他" };
        private int currentObjectType = 1;
        private UnityEngine.Rect windowRect = new UnityEngine.Rect(20, 20, 120, 50);

        public ModelBrush(MapEditor e) : base(e)
        {
            GameData.Instance.ModelConfigs.ForEach(x =>
            {
                configList.Add(x);
            });
        }

        public int CheckModelIndex()
        {
            if (objectIndex == -1)
            {
                foreach (IMapManageObject obj in editor.map.mapModels.staticObjects)
                {
                    objectIndex = Math.Max(objectIndex, obj.objId);
                }
            }
            return objectIndex++;
        }

        public void ExportConfigTo()
        {
            WindowDialog.SaveFileDialog("保存", System.IO.Path.GetDirectoryName(default_data_save_path), "ModelConfig.xml", "数据文件(*.xml)|*.xml\0");
        }
        public void SaveConfig()
        {
            SaveConfig(default_data_save_path);
        }
        public void SaveConfig(string fileName)
        {
            //StringBuilder sb = new StringBuilder();
            //sb.AppendLine("local data_model={");
            //for (int i = 0; i < configList.Count; ++i) {
            //    ModelConfig config = configList[i];
            //    sb.AppendLine(config.GetFormatString());
            //}
            //sb.AppendLine("}");
            //sb.AppendLine("return data_model");

            //using (StreamWriter textWriter = new StreamWriter(fileName, false, new UTF8Encoding(false))) {
            //    // 去BOM
            //    string s = sb.ToString();
            //    byte[] bs = Encoding.UTF8.GetBytes(s);
            //    byte[] bomBuffer = new byte[] { 0xef, 0xbb, 0xbf };

            //    if (bs[0] == bomBuffer[0]
            //        && bs[1] == bomBuffer[1]
            //        && bs[2] == bomBuffer[2]) {
            //        s = new UTF8Encoding(false).GetString(bs, 3, bs.Length - 3);
            //    }
            //    else
            //        s = new UTF8Encoding(false).GetString(bs);

            //    textWriter.Write(s);
            //    textWriter.Flush();
            //    textWriter.Close();
            //}
        }


        public override void OnBrushTypeChange()
        {

        }

        public void OnObjectTypeChange()
        {
            if (currentObjectType == 0)
            {
                currentConfigList = configList;
            }
            else
            {
                currentConfigList = configList.FindAll(x => x.modelType == currentObjectType);
            }

            if (currentObjectType >= 1 && currentObjectType <= 3)
            {
                currentStaticModelList = editor.map.mapModels.staticObjects.FindAll(x => x.objType == currentObjectType);
            }
            else
            {
                currentStaticModelList = new List<IMapManageObject>();
            }

        }


        public override void OnSeasonChanged(int curSeason)
        {

        }

        public override void Clear()
        {
            ClearModel();
        }

        public override void Modify(Vector3 center, MapEditor editor)
        {
            //if (GUILayout.Button("加载模型"))
            //{
            //    string[] path = WindowDialog.OpenFileDialog("模型", Path.GetDirectoryName(Tools.MapEditor.lastOpenFilePath), "模型文件(*.obj)|*.obj\0");
            //    if (path != null)
            //    {
            //        string fName = path[0];
            //    }
            //}
            if (modelConfig == null) return;

            MapObject mapObj = MapObject.Create(modelConfig.Id.ToString());
            mapObj.objId = CheckModelIndex();
            mapObj.objType = modelConfig.modelType;
            mapObj.modelId = modelConfig.Id;
            mapObj.position = model.transform.position;
            mapObj.rotation = model.transform.rotation.eulerAngles;
            mapObj.scale = model.transform.localScale;
            mapObj.coords = editor.map.mapGrid.PositionToCoords(model.transform.position.x, model.transform.position.z);

            mapObj.bounds = modelConfig.bounds;

            mapObj.CreateModel(model);

            editor.map.AddStatic(mapObj);
            //modelConfig.instanceList.Add(mapObj);

            if (!Input.GetKey(KeyCode.LeftShift))
            {
                ClearModel();
            }

        }
        Vector2 scrollPos;
        public override void OnGUI()
        {
            //GUILayout.Label(String.Format("笔刷大小 [{0}]", size));
            //float _size = GUILayout.HorizontalSlider(size, 0f, 12f);
            //if ((int)_size != size)
            //{
            //    size = (int)_size;
            //    OnBrushSizeChange();
            //}
            UnityEngine.Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = UnityEngine.Color.cyan;
            int typeIndex = GUILayout.SelectionGrid(currentObjectType, objectTypeTitle, 4, GUILayout.Height(60));
            if (typeIndex != currentObjectType)
            {
                currentObjectType = typeIndex;
                OnObjectTypeChange();
            }
            GUI.backgroundColor = lastColor;
            if (GUILayout.Button("加载原来模型"))
            {
                editor.map.mapModels.ClearAllModels();
                //editor.CallFunction("LoadDefaultModel");

                string dataModelFile = Path.FindFile("Data/Model/ModelList.xml");
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(dataModelFile);
                int count = xmlDocument.LastChild.ChildNodes.Count;
                for (int i = 0; i < count; i++)
                {
                    XmlNode xmlNode = xmlDocument.LastChild.ChildNodes[i];
                    int id = int.Parse(xmlNode["Id"].InnerText);
                    string name = xmlNode["Name"].InnerText;
                    int model = int.Parse(xmlNode["model"].InnerText);
                    int x = int.Parse(xmlNode["x"].InnerText);
                    int y = int.Parse(xmlNode["y"].InnerText);
                    int h = int.Parse(xmlNode["h"].InnerText);
                    float r = float.Parse(xmlNode["r"].InnerText);

                    MapObject o = MapObject.Create(name, editor.map.CoordsToPosition(x + 28, y + 28), new Vector3(0, r * Mathf.Rad2Deg - 90, 0), Vector3.one);
                    o.modelId = model;
                    o.objId = id;
                    editor.map.AddStatic(o);

                }

            }

            anchorByGrid = GUILayout.Toggle(anchorByGrid, "贴合格子中心");

            if (isShowModelConfig)
            {

                if (currentConfigList == null)
                    currentConfigList = configList;

                if (currentConfigList != null)
                {
                    if (GUILayout.Button("查看可绑定模型"))
                    {
                        isShowModelConfig = false;
                        scrollPos = new Vector2();
                        return;
                    }

                    if (currenShowModelInfo.Count < currentConfigList.Count)
                    {
                        for (int i = currenShowModelInfo.Count; i <= currentConfigList.Count; ++i)
                        {
                            currenShowModelInfo.Add(new ModelShowInfo());
                        }
                    }

                    scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(356), GUILayout.Height(456));
                    for (int i = 0; i < currentConfigList.Count; ++i)
                    {
                        ModelConfig config = currentConfigList[i];
                        currenShowModelInfo[i].Draw(this, config);
                    }
                    GUILayout.EndScrollView();
                }
            }
            else
            {
                if (GUILayout.Button("切换至模型库"))
                {
                    isShowModelConfig = true;
                    scrollPos = new Vector2();
                    return;
                }

                if (currentStaticModelList != null)
                {
                    if (currenShowModelInfo.Count < currentStaticModelList.Count)
                    {
                        for (int i = currenShowModelInfo.Count; i <= currentStaticModelList.Count; ++i)
                        {
                            currenShowModelInfo.Add(new ModelShowInfo());
                        }
                    }

                    scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(456), GUILayout.Height(456));
                    for (int i = 0; i < currentStaticModelList.Count; ++i)
                    {
                        IMapManageObject config = currentStaticModelList[i];
                        currenShowModelInfo[i].Draw(this, config);
                    }
                    GUILayout.EndScrollView();
                }
            }
        }

        public void AddModelConfig()
        {
        }

        public void ModifyModelConfig(ModelConfig config)
        {

        }

        public void SelectModel(ModelConfig config)
        {
            modelConfig = config;

            if (model != null)
            {
                GameObject.Destroy(model);
            }
            model = PoolManager.Create(modelConfig.model);
            if (model != null)
            {
                model.SetActive(true);
            }
        }

        protected virtual void OnModelLoaded(UnityEngine.Object obj, object customData)
        {

        }
        protected virtual void OnModelInit(GameObject model, object key)
        {

        }

        public override void OnEnter()
        {
            OnObjectTypeChange();
        }

        public override void DrawGizmos(Vector3 center)
        {
            if (model != null)
            {
                Vector3 pos = center;
                if (anchorByGrid)
                {
                    Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
                    Sango.Hexagon.Coord offset = Sango.Hexagon.Coord.OffsetFromCube(hex);
                    pos = editor.map.mapGrid.hexWorld.CoordsToPosition(offset.col, offset.row);
                    pos.y = editor.map.mapGrid.GetGridHeight(offset.col, offset.row);
                }
                model.transform.position = pos;
            }
            else
            {
            }
        }

        public void ClearModel()
        {
            if (model != null)
            {
                PoolManager.Recycle(model);
                model.SetActive(false);
                model = null;
            }

        }

        public override void Update()
        {
            if (model == null && modelConfig == null && Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastObjectLayer))
                {
                    MapObject mapObject = hit.collider.GetComponentInParent<MapObject>();
                    if (mapObject != null)
                    {
                        editor.ForceCameraToGameObject(mapObject.GetGameObject());
                    }
                }
                return;
            }

            if (model == null && modelConfig != null && Input.GetKeyDown(KeyCode.Space))
            {
                SelectModel(modelConfig);
            }

            if (model != null)
            {
                // 右键或者Esc取消模型
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    ClearModel();
                    return;
                }

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
                {
                    if (hit.point != lastCenter)
                    {
                        if (!IsPointerOverUI() && Input.GetMouseButtonDown(0))
                        {
                            Modify(hit.point, editor);
                            lastCenter = hit.point;
                        }
                        DrawGizmos(hit.point);
                    }
                }
            }
        }
    }

}