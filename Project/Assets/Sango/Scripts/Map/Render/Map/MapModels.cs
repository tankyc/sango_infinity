using LuaInterface;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Sango;

namespace Sango.Render
{
    // 光照
    public class MapModels : MapProperty
    {
        public List<IMapManageObject> dynamicObjects = new List<IMapManageObject>();
        public List<IMapManageObject> staticObjects = new List<IMapManageObject>();
        //public Dictionary<int, IMapManageObject> staticObjectMap = new Dictionary<int, IMapManageObject>();
        public Sango.Tools.QuadTree2D<IMapManageObject> staticObjectsQuadTree;
        private Tools.Rect curViewRect = new Tools.Rect();

        public MapModels(MapRender map) : base(map)
        {
        }

        public override void Init()
        {
            base.Init();

            Tools.Rect bounds = new Tools.Rect(0, 0, map.mapData.world_width, map.mapData.world_height);

            // 初始化quadTree
            staticObjectsQuadTree = new Tools.QuadTree2D<IMapManageObject>(bounds, 8);
        }

        public override void Clear()
        {
            base.Clear();
            ClearAllModels();
            foreach (IMapManageObject o in dynamicObjects) {
                o.visible = false;
            }
            dynamicObjects.Clear();
        }

        public void SetOutLineShow(Material terrainOutlineMat)
        {
            foreach (IMapManageObject obj in staticObjects) {
                obj.SetOutlineShow(terrainOutlineMat);
            }
            foreach (IMapManageObject obj in dynamicObjects) {
                obj.SetOutlineShow(terrainOutlineMat);
            }
        }

        public void AddDynamic(IMapManageObject obj)
        {
            obj.manager = map;
            obj.isStatic = false;
            dynamicObjects.Add(obj);
            obj.visible = obj.Overlaps(curViewRect);
        }

        public void AddStatic(IMapManageObject obj)
        {
            //if (staticObjectMap.TryAdd(obj.objId, obj)) {
            staticObjects.Add(obj);
            obj.isStatic = true;
            obj.manager = map;
            staticObjectsQuadTree.Add(obj, obj.worldBounds);
            obj.visible = obj.Overlaps(curViewRect);
            //}
        }

        public void RemoveStatic(IMapManageObject obj)
        {
            if (staticObjects.Remove(obj)) {
                staticObjectsQuadTree.Remove(obj, obj.worldBounds);
                //staticObjectMap.Remove(obj.objId);
            }
        }

        public void Remove(IMapManageObject obj)
        {
            if(obj.isStatic)
            {
                if (staticObjects.Remove(obj))
                {
                    staticObjectsQuadTree.Remove(obj, obj.worldBounds);
                    //staticObjectMap.Remove(obj.objId);
                }
            }
            else
            {
                dynamicObjects.Remove(obj);
            }
        }
        public void RemoveDynamic(IMapManageObject obj)
        {
            dynamicObjects.Remove(obj);
        }

        public bool IsInView(IMapManageObject obj)
        {
            return obj.Overlaps(curViewRect);
        }

        public void CreateModel(int id, int objType, int bindId, int modelId, Vector3 position, Vector3 rot, Vector3 scale)
        {
            if (modelId == 0) return;
            MapObject obj = MapObject.Create(bindId.ToString());
            obj.objId = id;
            obj.objType = objType;
            obj.bindId = bindId;
            obj.modelId = modelId;
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(rot);
            obj.transform.localScale = scale;
            obj.bounds = new Sango.Tools.Rect( 0, 0, 32, 32);
            //map.LoadModel(obj);
            if (bindId > 0)
                map.BindModel(obj);

            AddStatic(obj);
        }

        public void InitModel(int id, Vector2Int coords, float height, Vector3 rot, Vector3 scale)
        {

        }

        public void ClearAllModels()
        {
            foreach (IMapManageObject o in staticObjects) {
                staticObjectsQuadTree.Remove(o, o.worldBounds);
                o.visible = false;
            }
            staticObjects.Clear();
        }

        internal override void OnSave(BinaryWriter writer)
        {
            List<IMapManageObject> invalidList = staticObjects.FindAll(x => x.objId == 0 && x.objType == 0 && x.modelId == 0);

            foreach (IMapManageObject o in invalidList)
                RemoveStatic(o);

            writer.Write(staticObjects.Count);
            for (int i = 0; i < staticObjects.Count; ++i) {
                IMapManageObject obj = staticObjects[i];
                writer.Write(obj.objId);
                writer.Write(obj.objType);
                writer.Write(obj.bindId);
                writer.Write(obj.modelId);
                writer.Write(obj.position.x);
                writer.Write(obj.position.y);
                writer.Write(obj.position.z);
                writer.Write(obj.rotation.x);
                writer.Write(obj.rotation.y);
                writer.Write(obj.rotation.z);
                writer.Write(obj.scale.x);
                writer.Write(obj.scale.y);
                writer.Write(obj.scale.z);
            }

        }

        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            float x, y, w, h;
            if (map.mapCamera.GetViewRect(map.showLimitLength, out x, out y, out w, out h))
            {
                curViewRect.Set(x, y, w, h);
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i) {
                int objId = reader.ReadInt32();
                int objType = reader.ReadInt32();
                int bindId = 0;
                if (versionCode >= 4)
                    bindId = reader.ReadInt32();
                int modelId = reader.ReadInt32();
                Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3 rot = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3 scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                CreateModel(objId, objType, bindId, modelId, pos, rot, scale);
            }
        }

        public override void UpdateRender()
        {

        }

        public void EditorShow(bool b)
        {
            for (int i = 0; i < staticObjects.Count; ++i) {
                IMapManageObject obj = staticObjects[i];
                obj.EditorShow(b);
            }
        }

        private IMapManageObject[][] mapManageObjects = new IMapManageObject[2][]
        {
              new IMapManageObject[8096],
              new IMapManageObject[8096]
        };
        private int[] mapManageObjectsCount = { 0, 0 };
        private int manageLastIndex = 0;


        public void Update(Tools.Rect rect)
        {
            curViewRect = rect;

            int lastCount = mapManageObjectsCount[manageLastIndex];
            IMapManageObject[] lastList = mapManageObjects[manageLastIndex];
            for (int i = 0; i < lastCount; i++) {
                lastList[i].remainInView = false;
            }

            int newIndex = (manageLastIndex + 1) % 2;
            IMapManageObject[] newList = mapManageObjects[newIndex];

            int newCount = staticObjectsQuadTree.Find(rect, ref newList, true);
            mapManageObjectsCount[newIndex] = newCount;
            for (int i = 0; i < newCount; i++) {
                newList[i].remainInView = true;
            }

            for (int i = 0; i < lastCount; i++) {
                IMapManageObject obj = lastList[i];
                if (obj.manager != null && !obj.remainInView) {
                    obj.visible = false;
                }
            }

            for (int i = 0; i < newCount; i++) {
                newList[i].visible = true;
            }
            manageLastIndex = newIndex;

            for (int i = 0; i < dynamicObjects.Count; i++) {
                IMapManageObject dynamicObj = dynamicObjects[i];
                if (dynamicObj != null) {
                    dynamicObj.visible = dynamicObj.Overlaps(rect);
                }
            }
        }

        public override void UpdateImmediate()
        {
            foreach (IMapManageObject obj in staticObjects)
            {
                obj.EditorShow(true);
                obj.visible = true;
            }
            foreach (IMapManageObject obj in dynamicObjects)
            {
                obj.EditorShow(true);
                obj.visible = true;
            }
        }
    }
}
