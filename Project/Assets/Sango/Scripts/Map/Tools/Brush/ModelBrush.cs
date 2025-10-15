using Sango.Game;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace Sango.Tools
{
    /// <summary>
    /// 模型笔刷类  继承 BrushBase，用于处理地图编辑中模型相关的操作，比如模型配置、加载、显示、修改以及保存等功能
    /// </summary>
    public class ModelBrush : BrushBase
    {
        /// <summary>
        /// 模型配置类，用于存储模型的各种属性信息以及相关操作方法，如模型的ID、名称、路径、材质等相关配置
        /// </summary>
        public class EditModelConfig
        {
            public Sango.Game.ModelConfig modelConfig;
            /// <summary>
            /// 模型所在的模组（用于资源管理）
            /// </summary>
            public string mod;
            /// <summary>
            /// 模型着色器名称
            /// </summary>
            public string shaderName;
            /// <summary>
            /// 是否共享材质
            /// </summary>
            public bool isShardMat;
            /// <summary>
            /// 模型纹理
            /// </summary>
            // public Texture texture;
            /// <summary>
            /// 存储该模型实例的列表，用于记录模型在地图中的多个实例情况
            /// </summary>
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

        /// <summary>
        /// 模型显示信息列表，用于存储多个模型的显示相关信息，方便在界面上展示模型的详情
        /// </summary>
        List<ModelConfig> configList = new List<ModelConfig>();
        /// <summary>
        /// 默认的数据保存路径，用于保存模型配置等相关数据，路径指向特定的文件位置（目前的目录示例：D:\sangov2\ClientData\Mods\Map\Assets\Data\data_model.txt）
        /// </summary>
        string default_data_save_path;// = XPath.ContentRootPath + "/Mod/Map/Scripts/Data/data_model.lua";

        /// <summary>
        /// 当前模型配置列表，可根据不同条件筛选得到，用于当前操作中涉及的模型配置管理
        /// </summary>
        List<ModelConfig> currentConfigList;
        /// <summary>
        /// 对象索引，用于记录相关对象的序号等情况，初始值为 -1，后续会根据操作进行更新
        /// </summary>
        int objectIndex = -1;
        /// <summary>
        /// 当前的静态模型列表，存储符合特定条件的地图管理对象，用于在界面展示或操作相关的静态模型
        /// </summary>
        List<IMapManageObject> currentStaticModelList;
        /// <summary>
        /// 布尔值，用于控制是否显示模型配置信息，决定界面上展示的内容是模型配置还是静态模型相关信息
        /// </summary>
        bool isShowModelConfig = true;
        /// <summary>
        /// 当前要显示的模型信息列表，每个元素包含了模型配置、关联的地图管理对象以及要展示的具体内容等信息，用于在界面上绘制展示模型详情
        /// </summary>
        List<ModelShowInfo> currenShowModelInfo = new List<ModelShowInfo>();

        /// <summary>
        /// 模型显示信息类，用于封装模型相关的展示信息以及对应的绘制操作方法，方便在界面上统一处理模型展示逻辑
        /// </summary>
        public class ModelShowInfo
        {
            /// <summary>
            /// 绑定的模型配置，用于关联具体的模型配置信息，确定要展示的模型相关属性
            /// </summary>
            public ModelConfig bindConfig;
            /// <summary>
            /// 绑定的地图管理对象，用于关联模型在地图中的管理对象，进行相关操作或展示对应的属性信息
            /// </summary>
            public IMapManageObject bindMapObject;
            /// <summary>
            /// 要展示的内容数组，根据绑定的对象不同，存储对应的展示信息，如模型ID、名称等
            /// </summary>
            public string[] showContent;

            /// <summary>
            /// 根据给定的模型笔刷和模型配置，绘制模型相关信息，包括按钮操作（如选择、修改模型等）以及对应配置信息的展示，若绑定配置变化则更新展示内容
            /// </summary>
            /// <param name="brush">模型笔刷实例，用于执行相关的模型操作，如选择、修改等</param>
            /// <param name="c">模型配置实例，提供要展示的模型配置信息</param>
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

                // 添加长度检查，确保数组长度足够（龍馬0623）等于5说明在显示模型，切换了这个就不能再显示
                if (showContent.Length == 5)
                {
                    GUILayout.Label(showContent[1]);
                    GUILayout.Label(showContent[2]);
                    GUILayout.Label(showContent[3]);
                }
                GUILayout.EndHorizontal();
            }

            /// <summary>
            /// 根据给定的模型笔刷和地图管理对象，绘制模型相关信息，包括按钮操作（如选择模型等）以及对应对象属性信息的展示，若绑定对象变化则更新展示内容
            /// </summary>
            /// <param name="brush">模型笔刷实例，用于执行相关的模型操作，如选择模型等</param>
            /// <param name="m">地图管理对象实例，提供要展示的对象相关属性信息</param>
            public void Draw(ModelBrush brush, IMapManageObject m)
            {
                if (bindMapObject != m)
                {
                    bindMapObject = m;

                    showContent = new string[3];
                    showContent[0] = m.bindId.ToString();
                    showContent[1] = m.objId.ToString();
                    showContent[2] = m.modelId.ToString();
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("选择", GUILayout.Width(48)))
                {
                    // 镜头切换至模型处，并选择模型
                    brush.editor.ForceCameraToGameObject(bindMapObject.GetGameObject());
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

                        if (bindId != bindMapObject.bindId)
                        {
                            bindMapObject.bindId = bindId;
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

        /// <summary>
        /// 布尔值，用于控制模型是否随机方向，可能影响模型在地图中的放置角度等显示效果
        /// </summary>
        public bool randomDir = false;
        /// <summary>
        /// 当前加载的游戏对象模型，用于在地图中显示和操作的实际模型实例
        /// </summary>
        public GameObject model = null;
        /// <summary>
        /// 当前选中的模型配置，用于确定当前操作所针对的模型相关属性设置等情况
        /// </summary>
        public ModelConfig modelConfig = null;
        /// <summary>
        /// 布尔值，用于控制模型是否根据网格进行定位，影响模型放置位置与地图网格的关联情况
        /// </summary>
        public bool anchorByGrid = false;

        /// <summary>
        /// 对象类型标题数组，用于在界面上展示不同的对象类型选项，方便用户选择过滤模型等操作
        /// </summary>
        private string[] objectTypeTitle = new string[] { "所有", "城池", "关隘", "港口", "内政", "军事", "植物", "其他" };
        /// <summary>
        /// 当前选择的对象类型索引，用于确定当前显示或操作的模型所属的类型分类情况
        /// </summary>
        private int currentObjectType = 1;
        /// <summary>
        /// 用于界面窗口的矩形区域，定义了窗口的位置和大小，用于展示模型相关信息等界面内容
        /// </summary>
        private UnityEngine.Rect windowRect = new UnityEngine.Rect(20, 20, 240, 100);

        /// <summary>
        /// 模型笔刷类的构造函数，用于初始化相关属性和关联地图编辑器
        /// </summary>
        /// <param name="e">地图编辑器实例，用于在模型笔刷操作中与地图相关功能进行交互等操作</param>
        public ModelBrush(MapEditor e) : base(e)
        {
            GameData.Instance.ModelConfigs.ForEach(x =>
            {
                configList.Add(x);
            });
        }

        /// <summary>
        /// 检查模型索引，若对象索引为 -1，则遍历地图中的静态对象，找到最大的objId并返回，同时自增对象索引，用于后续创建模型时分配唯一的对象ID
        /// </summary>
        /// <returns>当前的对象索引值，用于唯一标识模型对象等情况</returns>
        public int CheckModelIndex()
        {
            if (objectIndex == -1)
            {
                // 如果objectIndex为 -1，遍历地图中的静态对象，找到最大的objId
                foreach (IMapManageObject obj in editor.map.mapModels.staticObjects)
                {
                    objectIndex = Math.Max(objectIndex, obj.objId);
                }
            }
            // 返回objectIndex并自增
            return objectIndex++; 
        }

        public void ExportConfigTo()
        {
            WindowDialog.SaveFileDialog("导出", System.IO.Path.GetDirectoryName(default_data_save_path), "ModelConfig.xml", "模型配置数据文件(*.xml)|*.xml\0");
        }

        /// <summary>
        /// 调用SaveConfig方法，将模型配置保存到默认的数据保存路径下，方便进行配置数据的持久化存储
        /// </summary>
        public void SaveConfig()
        {
            SaveConfig(default_data_save_path); // 保存配置到默认路径
        }

        /// <summary>
        /// 将模型配置信息保存到指定的文件中，先构建包含配置信息的字符串内容，进行去除BOM（字节顺序标记）处理后，再写入到指定的文件中
        /// </summary>
        /// <param name="fileName">要保存的文件名及路径，指定了配置信息的具体存储位置</param>
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
                // 去除BOM
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

        /// <summary>
        /// 当画笔类型发生改变时调用的方法，目前此方法内暂时没有具体的实现逻辑，可能后续会添加相应的处理代码
        /// </summary>
        public override void OnBrushTypeChange()
        {

        }

        /// <summary>
        /// 根据当前选择的对象类型来更新当前的模型配置列表和静态模型列表，若选择“所有”类型则使用全部配置列表，否则按对象类型筛选相应列表
        /// </summary>
        public void OnObjectTypeChange()
        {
            if (currentObjectType == 0)
            {
                currentConfigList = configList;     // 如果当前对象类型为0，使用全部配置列表
            }
            else
            {
                currentConfigList = configList.FindAll(x => x.modelType == currentObjectType);  // 否则，根据对象类型筛选配置列表
            }

            if (currentObjectType >= 1 && currentObjectType <= 3)
            {
                currentStaticModelList = editor.map.mapModels.staticObjects.FindAll(x => x.objType == currentObjectType);   // 根据对象类型筛选静态模型列表
            }
            else
            {
                currentStaticModelList = new List<IMapManageObject>();  // 否则，清空静态模型列表
            }
        }

        /// <summary>
        /// 当季节发生改变时调用的方法，目前此方法内暂时没有具体的实现逻辑，可能后续会添加相应的处理代码
        /// </summary>
        public override void OnSeasonChanged(int curSeason)
        {
            // 季节改变时的处理
        }

        /// <summary>
        /// 清除相关操作，内部调用ClearModel方法来执行具体的清除模型相关的操作，比如回收模型、关闭显示等
        /// </summary>
        public override void Clear()
        {
            ClearModel();
        }

        /// <summary>
        /// 根据给定的地图中心位置，对模型进行修改相关操作，比如创建地图对象、设置对象属性、关联模型、添加到地图静态对象列表等，若未按下左Shift键则清除模型
        /// </summary>
        /// <param name="center">地图中的中心位置，用于确定模型放置等相关操作的位置信息</param>
        /// <param name="editor">地图编辑器实例，用于与地图相关功能进行交互，获取地图数据等操作</param>
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

            // 如果模型配置为空，则退出函数
            if (modelConfig == null) return;

            // 创建地图对象
            MapObject mapObj = MapObject.Create(modelConfig.Id.ToString());
            // 检查模型索引
            mapObj.objId = CheckModelIndex();
            // 设置地图对象类型和模型类型
            mapObj.objType = modelConfig.modelType;
            mapObj.modelId = modelConfig.Id;
            mapObj.modelAsset = modelConfig.model;
			// 设置地图对象的位置、旋转和缩放
            mapObj.position = model.transform.position;
            mapObj.rotation = model.transform.rotation.eulerAngles;
            mapObj.scale = model.transform.localScale;
            // 将模型位置转换为网格坐标
            mapObj.coords = editor.map.mapGrid.PositionToCoords(model.transform.position.x, model.transform.position.z);

            // 设置地图对象的边界
            mapObj.bounds = modelConfig.bounds;

            // 创建模型并关联到地图对象
            mapObj.CreateModel(model);

            // 将地图对象添加到地图的静态对象列表中
            editor.map.AddStatic(mapObj);
            // 将地图对象添加到模型配置的实例列表中
            //modelConfig.instanceList.Add(mapObj);

            // 如果没有按下左Shift键
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                // 清除模型
                ClearModel();
            }

        }
        Vector2 scrollPos;

        /// <summary>
        /// 用于在图形用户界面（GUI）上绘制相关的模型操作等控件，例如选择对象类型、加载默认模型、控制是否贴合格子中心，
        /// 还根据是否显示模型配置信息来展示不同的内容，如模型配置详情或静态模型列表等，并进行相应的绘制操作
        /// </summary>
        public override void OnGUI()
        {
            // 显示GUI界面
            //GUILayout.Label(String.Format("笔刷大小 [{0}]", size));
            //float _size = GUILayout.HorizontalSlider(size, 0f, 12f);
            //if ((int)_size != size)
            //{
            //    size = (int)_size;
            //    OnBrushSizeChange();
            //}
            UnityEngine.Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = UnityEngine.Color.cyan;   // cyan 青色
            // 在GUI中创建一个选择网格，用于选择当前对象类型
            int typeIndex = GUILayout.SelectionGrid(currentObjectType, objectTypeTitle, 4, GUILayout.Height(60));
            if (typeIndex != currentObjectType)
            {
                currentObjectType = typeIndex;
                OnObjectTypeChange(); // 当对象类型改变时的处理
            }
            GUI.backgroundColor = lastColor;

            // 按钮：加载原来模型
            if (GUILayout.Button("加载原来模型"))
            {
                editor.map.mapModels.ClearAllModels(); // 清空所有模型
                //editor.CallFunction("LoadDefaultModel"); // 调用加载默认模型的函数
 
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

            anchorByGrid = GUILayout.Toggle(anchorByGrid, "贴合地格中心"); // 创建一个开关，控制是否贴合地格中心

            if (isShowModelConfig)
            {
                // 如果需要显示模型配置信息
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

                    // 开始一个滚动视图
                    scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(356), GUILayout.Height(456));
                    for (int i = 0; i < currentConfigList.Count; ++i)
                    {
                        ModelConfig config = currentConfigList[i];
                        currenShowModelInfo[i].Draw(this, config); // 绘制模型信息
                    }
                    GUILayout.EndScrollView();
                }
            }
            else
            {
                // 如果需要显示静态模型列表
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

                    // 开始一个滚动视图
                    scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(456), GUILayout.Height(456));
                    for (int i = 0; i < currentStaticModelList.Count; ++i)
                    {
                        IMapManageObject config = currentStaticModelList[i];
                        currenShowModelInfo[i].Draw(this, config); // 绘制模型信息
                    }
                    GUILayout.EndScrollView();
                }
            }
        }

        /// <summary>
        /// 用于添加模型配置信息的方法，目前此方法内暂时没有具体的实现逻辑，可能后续会添加相应的添加配置操作代码
        /// </summary>
        public void AddModelConfig()
        {
            // 添加模型配置信息
        }

        /// <summary>
        /// 用于修改模型配置信息的方法，目前此方法内暂时没有具体的实现逻辑，可能后续会添加相应的修改配置操作代码
        /// </summary>
        public void ModifyModelConfig(ModelConfig config)
        {
            // 修改模型配置信息
        }

        /// <summary>
        /// 选择指定的模型配置，更新当前的模型配置对象，然后从文件加载对应的模型和纹理，若已有模型则先销毁再实例化新模型
        /// </summary>
        /// <param name="config">要选择的模型配置对象，包含了模型相关的属性信息，用于加载对应的模型</param>
        public void SelectModel(ModelConfig config)
        {
            // 选择指定模型配置
            modelConfig = config;
            // 从文件加载模型和纹理
            if (model != null)
            {
                GameObject.Destroy(model);// 销毁原有模型
            }
            model = PoolManager.Create(modelConfig.model);
            if (model != null)
            {
                model.SetActive(true);
            }
        }
		
        /// <summary>
        /// 当模型加载完成后调用的虚方法，目前此方法内暂时没有具体的实现逻辑，可能后续会添加相应的处理代码，用于在模型加载完成后的自定义操作
        /// </summary>
        protected virtual void OnModelLoaded(UnityEngine.Object obj, object customData)
        {

        }

        /// <summary>
        /// 当模型初始化完成后调用的虚方法，目前此方法内暂时没有具体的实现逻辑，可能后续会添加相应的处理代码，用于在模型初始化完成后的自定义操作
        /// </summary>
        protected virtual void OnModelInit(GameObject model, object key)
        {

        }

        /// <summary>
        /// 当进入相关操作时调用的方法，内部调用OnObjectTypeChange方法来根据当前对象类型更新相关的模型配置列表和静态模型列表
        /// </summary>
        public override void OnEnter()
        {
            OnObjectTypeChange();
        }

        /// <summary>
        /// 根据给定的中心点位置绘制模型相关的辅助图形（Gizmos），若模型存在且根据网格定位，则将位置转换为基于网格的坐标后设置模型位置，否则直接使用给定的中心点位置
        /// </summary>
        /// <param name="center">用于确定模型位置的中心点坐标，根据相关条件来设置模型的实际位置</param>
        public override void DrawGizmos(Vector3 center)
        {
            // 如果模型存在
            if (model != null)
            {
                Vector3 pos = center; // 初始化位置为传入的中心点
                if (anchorByGrid)
                {
                    // 如果根据网格进行定位，将中心点转换为六边形坐标
                    Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
                    // 根据六边形坐标计算偏移坐标
                    Sango.Hexagon.Coord offset = Sango.Hexagon.Coord.OffsetFromCube(hex);
                    // 将偏移坐标转换为世界坐标
                    pos = editor.map.mapGrid.hexWorld.CoordsToPosition(offset.col, offset.row);
                    // 设置模型的高度为网格高度
                    pos.y = editor.map.mapGrid.GetGridHeight(offset.col, offset.row);
                }
                // 将模型位置设置为计算得到的位置
                model.transform.position = pos;
            }
            else
            {
                // 如果模型为空，不执行任何操作
            }
        }

        /// <summary>
        /// 清除模型相关操作，若模型存在，则将其回收至对象池、关闭模型显示，并将模型引用设为null，释放相关资源
        /// </summary>
        public void ClearModel()
        {
            // 如果模型存在
            if (model != null)
            {
                // 回收模型对象到对象池中
                PoolManager.Recycle(model);
                // 关闭模型的显示
                model.SetActive(false);
                // 将模型引用设为null
                model = null;
            }

        }

        /// <summary>
        /// 更新方法，用于处理模型相关的操作逻辑，比如在特定条件下选择模型、检测右键或Esc键取消模型选择、根据鼠标点击等交互操作修改模型以及绘制相关的辅助图形（Gizmos）等功能
        /// </summary>
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
            // 在每帧更新中检查模型是否为空，模型配置是否存在，并且按下空格键
            if (model == null && modelConfig != null && Input.GetKeyDown(KeyCode.Space))
            {
                // 选择模型
                SelectModel(modelConfig);
            }

            // 如果模型存在
            if (model != null)
            {
                // 检测右键点击或按下Esc键来取消模型选择
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    // 清除模型
                    ClearModel();
                    return;
                }

                // 从主摄像机发射射线到鼠标位置
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                // 如果射线与指定的层发生碰撞
                if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
                {
                    // 如果碰撞点与上一次记录的中心点不同
                    if (hit.point != lastCenter)
                    {
                        // 如果鼠标不在UI上并且按下鼠标左键
                        if (!IsPointerOverUI() && Input.GetMouseButtonDown(0))
                        {
                            // 修改模型
                            Modify(hit.point, editor);
                            lastCenter = hit.point; // 更新中心点位置
                        }
                        // 绘制Gizmos
                        DrawGizmos(hit.point);
                    }
                }
            }
        }
    }
}