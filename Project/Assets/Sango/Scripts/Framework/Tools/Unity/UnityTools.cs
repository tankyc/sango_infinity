/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using LuaInterface;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango
{
    public static class UnityTools
    {
        static public GameObject CreateGameObject(string name, bool dontDestory = false)
        {
            GameObject o = new GameObject(name);
            if (dontDestory)
                GameObject.DontDestroyOnLoad(o);
            return o;
        }

        static public GameObject CreateGameObject(bool dontDestory = false)
        {
            GameObject o = new GameObject();
            if (dontDestory)
                GameObject.DontDestroyOnLoad(o);
            return o;
        }

        static public Component AddComponent(GameObject o, System.Type classType)
        {
            return o.AddComponent(classType);
        }

        static public Component TryAddComponent(GameObject o, System.Type classType)
        {
            Component c = o.GetComponent(classType);
            if (c == null)
                c = o.AddComponent(classType);
            return c;
        }

        static public Component TryAddComponent(Transform parent, string path, System.Type classType, float x, float y, float z)
        {
            if (parent == null)
            {
                return null;
            }
            var obj = parent.Find(path);
            if (obj == null)
            {
                obj = new GameObject(path).transform;
                obj.SetParent(parent);
                obj.localScale = Vector3.one;
                obj.localRotation = Quaternion.identity;
                obj.localPosition = new Vector3(x, y, z);
            }
            else
            {
                obj.localPosition = new Vector3(x, y, z);
                obj.gameObject.SetActive(true);
            }
            var script = obj.GetComponent(classType);
            if (script == null)
            {
                script = obj.gameObject.AddComponent(classType);
            }
            return script;
        }

        //static public Component tryAddImage(Transform parent, string path, string sprName, bool useSpriteSize, float x, float y)
        //{
        //    if (parent == null)
        //    {
        //        return null;
        //    }
        //    var obj = parent.Find(path);
        //    if (obj == null)
        //    {
        //        obj = new GameObject(path).transform;
        //        obj.SetParent(parent);
        //        obj.localScale = Vector3.one;
        //        obj.localRotation = Quaternion.identity;
        //        obj.localPosition = new Vector3(x, y, 0);
        //    }
        //    else
        //    {
        //        obj.localPosition = new Vector3(x, y, 0);
        //        obj.gameObject.SetActive(true);
        //    }
        //    var image = obj.GetComponent<UnityEngine.UI.Image>();
        //    if (image == null)
        //    {
        //        image = obj.gameObject.AddComponent<UnityEngine.UI.Image>();
        //    }
        //    if (image != null && !string.IsNullOrEmpty(sprName))
        //    {
        //        if (image.sprite == null || !string.Equals(image.sprite.name, sprName))
        //        {
        //            ImageNode.Load<ImageNode>(image, sprName);
        //            if (useSpriteSize)
        //                image.SetNativeSize();
        //        }
        //    }
        //    return image;
        //}

        static public T AddComponent<T>(GameObject o) where T : Component
        {
            return o.AddComponent<T>();
        }

        static public void DontDestroyOnLoad(GameObject obj)
        {
            GameObject.DontDestroyOnLoad(obj);
        }

        static public void DontDestroyOnLoad(UnityEngine.Object obj)
        {
            UnityEngine.Object.DontDestroyOnLoad(obj);
        }

        static public UnityEngine.Object Instantiate(UnityEngine.Object obj)
        {
            if (obj == null) return null;
            return UnityEngine.Object.Instantiate(obj);
        }

        static public UnityEngine.Object Instantiate(UnityEngine.Object obj, Vector3 pos, Quaternion rot)
        {
            if (obj == null) return null;
            return UnityEngine.Object.Instantiate(obj, pos, rot);
        }


        /// <summary>
        /// 尝试实例化一个资源,并更名为指定名字
        /// 封装GameObject.Instantiate
        /// </summary>
        /// <param name="obj">源资源</param>
        /// <param name="name">指定名字</param>
        /// <returns>实例化后的资源</returns>
        public static UnityEngine.Object Instantiate(UnityEngine.Object obj, string name)
        {
            if (obj == null) return null;
            string saveName = obj.name;
            obj.name = name;
            UnityEngine.Object result = UnityEngine.Object.Instantiate(obj);
            obj.name = saveName;
            return result;
        }

        static public UnityEngine.Object Instantiate(UnityEngine.Object obj, string name, Vector3 pos, Quaternion rot)
        {
            if (obj == null) return null;
            string saveName = obj.name;
            obj.name = name;
            UnityEngine.Object result = UnityEngine.Object.Instantiate(obj, pos, rot);
            obj.name = saveName;
            return result;
        }

        /// <summary>
        /// 尝试去销毁一个资源,可能不会立即销毁
        /// </summary>
        /// <param name="obj">待销毁资源</param>
        public static void DeleteObject(UnityEngine.Object obj)
        {
            if (obj != null) GameObject.Destroy(obj);
        }

        /// <summary>
        /// 尝试去销毁一个资源,可能不会立即销毁
        /// </summary>
        /// <param name="obj">待销毁资源</param>
        public static void DeleteChildObjects(Transform trans)
        {
            List<Transform> l = new List<Transform>();
            for (int i = 0; i < trans.childCount; ++i)
                l.Add(trans.GetChild(i));

            for (int i = 0; i < l.Count; ++i)
                GameObject.Destroy(l[i].gameObject);
        }

        /// <summary>
        /// 尝试去销毁一个资源,可能不会立即销毁
        /// </summary>
        /// <param name="obj">待销毁资源</param>
        public static void DeleteChildObjects(GameObject gobj)
        {
            DeleteChildObjects(gobj.transform);
        }

        /// <summary>
        /// 尝试去立即销毁一个资源
        /// </summary>
        /// <param name="obj">待销毁资源</param>
        public static void DeleteObjImmediate(UnityEngine.Object obj, bool isUnloadRes)
        {
            if (Platform.targetPlatform == Platform.PlatformName.Ios)
            {
                if (obj != null) GameObject.Destroy(obj);
            }
            else
            {
                if (obj != null) GameObject.DestroyImmediate(obj, isUnloadRes);
            }
        }


        /// <summary>
        /// 尝试去立即销毁一个资源
        /// </summary>
        /// <param name="obj">待销毁资源</param>
        public static void DeleteObjImmediate(UnityEngine.Object obj)
        {
            DeleteObjImmediate(obj, false);
        }


        /// <summary>
        /// 初始化transform
        /// </summary>
        /// <param name="trans"></param>
        public static void ToInitTransform(Transform trans)
        {
            trans.localPosition = Vector3.zero;
            trans.localRotation = Quaternion.identity;
            trans.localScale = Vector3.one;
        }


        /// <summary>
        /// 初始化transform
        /// </summary>
        /// <param name="trans"></param>
        public static void SetTransformParent(Transform trans, Transform p)
        {
            trans.parent = p;
        }

        /// <summary>
        /// 初始化transform
        /// </summary>
        /// <param name="trans"></param>
        public static void ClearTransformParent(Transform trans)
        {
            trans.parent = null;
        }

        /// <summary>
        /// 获取对象, 传入对象路径
        /// </summary>
        /// <param name="namePath"> e.g: UI/nameLabel</param>
        /// <returns></returns>
        public static Transform GetTransform(Transform transform, string namePath)
        {
            return transform.Find(namePath);
        }

        /// <summary>
        /// 获取对象, 传入对象路径
        /// </summary>
        /// <param name="namePath"> e.g: UI/nameLabel</param>
        /// <returns></returns>
        public static Transform GetTransform(GameObject obj, string namePath)
        {
            return obj.transform.Find(namePath);
        }

        /// <summary>
        /// 获取对象, 传入对象路径
        /// </summary>
        /// <param name="namePath"> e.g: UI/nameLabel</param>
        /// <returns></returns>
        public static GameObject GetObject(Transform transform, string namePath)
        {
            Transform trans = GetTransform(transform, namePath);
            if (trans != null)
                return trans.gameObject;
            return null;
        }

        /// <summary>
        /// 获取对象, 传入对象路径
        /// </summary>
        /// <param name="namePath"> e.g: UI/nameLabel</param>
        /// <returns></returns>
        public static GameObject GetObject(GameObject obj, string namePath)
        {
            Transform trans = GetTransform(obj, namePath);
            if (trans != null)
                return trans.gameObject;
            return null;
        }

        /// <summary>
        /// 获取对象上的T接口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T GetComponent<T>(Transform transform, string namePath) where T : Component
        {
            Transform trans = GetTransform(transform, namePath);
            if (trans != null)
                return trans.GetComponent<T>();
            return null;
        }

        /// <summary>
        /// 获取对象上的T接口
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public static T GetComponent<T>(GameObject obj, string namePath) where T : Component
        {
            Transform trans = GetTransform(obj, namePath);
            if (trans != null)
                return trans.GetComponent<T>();
            return null;
        }


        /// <summary>
        /// 获取对象上的接口,传入接口type
        /// </summary>
        /// <param name="namePath"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [NoToLua]
        public static Component GetComponent(Transform transform, string namePath, string typeName)
        {
            Transform trans = GetTransform(transform, namePath);
            if (trans != null)
                return trans.GetComponent(typeName);
            else
            {
                if (Config.isDebug)
                    Log.Error("在 " + transform.name + " 中无法找到节点:" + namePath);
            }
            return null;
        }

        /// <summary>
        /// 获取对象上的接口,传入接口type
        /// </summary>
        /// <param name="namePath"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        [NoToLua]
        public static Component GetComponent(GameObject obj, string namePath, string typeName)
        {
            Transform trans = GetTransform(obj, namePath);
            if (trans != null)
                return trans.GetComponent(typeName);
            return null;
        }


        /// <summary>
        /// 获取对象上的接口,传入接口type
        /// </summary>
        /// <param name="namePath"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Component GetComponent(Transform transform, string namePath, Type typeName)
        {
            Transform trans = GetTransform(transform, namePath);
            if (trans != null)
                return trans.GetComponent(typeName);
            else
            {
                if (Config.isDebug)
                    Log.Error("在 " + transform.name + " 中无法找到节点:" + namePath);
            }
            return null;
        }

        /// <summary>
        /// 获取对象上的接口,传入接口type
        /// </summary>
        /// <param name="namePath"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Component GetComponent(GameObject obj, string namePath, Type typeName)
        {
            Transform trans = GetTransform(obj, namePath);
            if (trans != null)
                return trans.GetComponent(typeName);
            return null;
        }

        public static UnityEngine.Object FindComponent(System.Type t)
        {
            return GameObject.FindObjectOfType(t);
        }

        public static UnityEngine.Object[] FindComponents(System.Type t)
        {
            return GameObject.FindObjectsOfType(t);
        }

        public static GameObject FindGameObject(string name)
        {
            return GameObject.Find(name);
        }

        public static Renderer[] GetRenderers(GameObject obj)
        {
            return obj.GetComponentsInChildren<Renderer>(true);
        }

        public static Component[] GetComponentsInChild(Transform transform, System.Type type, bool includeInactive)
        {
            return transform.GetComponentsInChildren(type, includeInactive);
        }

        public static Component[] GetComponentsInChild(GameObject obj, System.Type type, bool includeInactive)
        {
            return obj.GetComponentsInChildren(type, includeInactive);
        }

        public static Component GetComponentInChild(GameObject obj, System.Type type)
        {
            return obj.GetComponentInChildren(type);
        }

        static public int Random(int min, int max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        static public float Random(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        static public float RandomFloat(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        static public int RandomInt(int min, int max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        static public void SetLayer(GameObject go, int layer)
        {
            if (go == null)
                return;

            go.layer = layer;

            Transform t = go.transform;

            for (int i = 0, imax = t.childCount; i < imax; ++i)
            {
                Transform child = t.GetChild(i);
                SetLayer(child.gameObject, layer);
            }
        }

        static public void ScaleAnimation(Animation ani, float s)
        {
            foreach (AnimationState state in ani)
                state.speed = s;
        }

        static public bool HasData(string k)
        {
            return PlayerPrefs.HasKey(k);
        }

        static public void SetDataStr(string k, string v)
        {
            PlayerPrefs.SetString(k, WWW.EscapeURL(v));
        }

        static public void SetDataInt(string k, int v)
        {
            PlayerPrefs.SetInt(k, v);
        }

        static public void SetDataFloat(string k, float v)
        {
            PlayerPrefs.SetFloat(k, v);
        }

        static public void SaveData()
        {
            PlayerPrefs.Save();
        }

        static public string GetDataStr(string k)
        {
            string v = PlayerPrefs.GetString(k);
            if (!string.IsNullOrEmpty(v))
                return WWW.UnEscapeURL(v);
            return v;
        }

        static public int GetDataInt(string k)
        {
            return PlayerPrefs.GetInt(k);
        }

        static public float GetDataFloat(string k)
        {
            return PlayerPrefs.GetFloat(k);
        }

        public const float KBSIZE = 1024.0f * 1024.0f;

        static public long GetTotalAllocatedMemory()
        {
#if UNITY_5_5 
            return (long)UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory();
#elif UNITY_5_3
            return (long)UnityEngine.Profiler.GetTotalAllocatedMemory();
#elif UNITY_5_6_OR_NEWER
            return UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
#endif
        }

        static public long GetTotalReservedMemory()
        {
#if UNITY_5_5 
            return (long)UnityEngine.Profiling.Profiler.GetTotalReservedMemory();
#elif UNITY_5_3
            return (long)UnityEngine.Profiler.GetTotalReservedMemory();
#elif  UNITY_5_6_OR_NEWER
            return UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
#endif
        }

        static public long GetTotalUnusedReservedMemory()
        {
#if UNITY_5_5
            return (long)UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemory();
#elif UNITY_5_3
            return (long)UnityEngine.Profiler.GetTotalUnusedReservedMemory();
#elif UNITY_5_6_OR_NEWER
            return UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong();
#endif
        }



        static public void SetFogEnabled(bool b)
        {
            RenderSettings.fog = b;
        }


        static public Vector3 SampleNavMeshPosition(Vector3 pos, float maxDis = 1000.0f, int mask = -1)
        {
#if UNITY_5_6_OR_NEWER
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out hit, maxDis, mask))
                return hit.position;
#elif UNITY_5_3
            UnityEngine.NavMeshHit hit;
            if (UnityEngine.NavMesh.SamplePosition(pos, out hit, maxDis, mask))
                return hit.position;
#endif
            return pos;
        }

        static public bool IsVector3Zero(Vector3 vec)
        {
            return vec == Vector3.zero;
        }

        static public AnimationClip GetAnimationClip(Animator ani, string name)
        {
            AnimatorOverrideController aoc = ani.runtimeAnimatorController as AnimatorOverrideController;
            if (aoc != null)
                return aoc[name];
            return null;
        }

        static public AnimationClip GetAnimationClip(Animation ani, string name)
        {
            AnimationState state = ani[name];
            if (state != null)
                return ani[name].clip;
            return null;
        }

        /// <summary>
        /// 为一个GameObject添加一个唯一的脚本,重复添加则会删除之前的脚本
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        static public Component AddOnlyComponent(GameObject obj, System.Type type)
        {
            Component c = obj.GetComponent(type);
            if (c != null)
                GameObject.Destroy(c);
            c = obj.AddComponent(type);
            return c;
        }


        /// <summary>
        /// 将navmesh模型转为可以检测碰撞的模型数据
        /// </summary>
        /// <returns></returns>
        static public Collider NavMeshToMeshCollider()
        {
            GameObject obj = new GameObject("navMesh");
            Mesh mesh = new Mesh();
#if UNITY_5_6_OR_NEWER
            UnityEngine.AI.NavMeshTriangulation tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
#elif UNITY_5_3
            UnityEngine.NavMeshTriangulation tri = UnityEngine.NavMesh.CalculateTriangulation();
#endif
            mesh.vertices = tri.vertices;
            mesh.SetIndices(tri.indices, MeshTopology.Triangles, 0);
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.mesh = mesh;

            MeshCollider collider = obj.AddComponent<MeshCollider>();
            return collider;

        }

        static public string GetSystemTimeString()
        {
            return System.DateTime.Now.Ticks.ToString();
        }

        static public void Gc()
        {
            System.GC.Collect();
        }

        /// <summary>
        /// 重新加载shader,在编辑器模式下用于还原真机shader
        /// </summary>
        /// <param name="name"></param>
        static public void ReimportShader()
        {
            if (Platform.isEditorMode)
            {
                Renderer[] rs = GameObject.FindObjectsOfType<Renderer>();
                for (int i = 0; i < rs.Length; ++i)
                {
                    Material[] mats = rs[i].sharedMaterials;
                    for (int j = 0; j < mats.Length; ++j)
                    {
                        mats[j].shader = Shader.Find(mats[j].shader.name);
                    }
                }
            }
        }

        static private List<CombineInstance> s_staticCombineInstance;
        static public void BeginCombineMesh()
        {
            if (s_staticCombineInstance == null)
                s_staticCombineInstance = new List<CombineInstance>();
            s_staticCombineInstance.Clear();
        }

        static public void AddCombineMesh(Mesh mesh, Transform trans)
        {
            CombineInstance ci = new CombineInstance();
            ci.mesh = mesh;
            ci.transform = trans.localToWorldMatrix;
            s_staticCombineInstance.Add(ci);
        }

        static public CombineInstance[] EndCombineMesh()
        {
            CombineInstance[] rt = s_staticCombineInstance.ToArray();
            s_staticCombineInstance.Clear();
            return rt;
        }

        static public void UnloadAsset(UnityEngine.Object o)
        {
            if (o is GameObject || o is AssetBundle || o is Component)
            {
                //Object.DestroyImmediate(o, true);
            }
            else
            {
                Resources.UnloadAsset(o);
            }
        }

        static public void SetRenderQueue(GameObject obj, int value)
        {
            Renderer[] rs = obj.GetComponentsInChildren<Renderer>(true);
            if (rs != null)
            {
                for (int i = 0; i < rs.Length; ++i)
                {
                    Renderer r = rs[i];
                    if (r != null)
                    {
                        for (int j = 0; j < r.sharedMaterials.Length; ++j)
                        {
                            Material m = r.sharedMaterials[j];
                            if (m != null)
                                m.renderQueue = value;
                        }
                    }
                }
            }
        }

        static public void RenderQueueIncreaseTo(GameObject obj, int value)
        {
            Renderer[] rs = obj.GetComponentsInChildren<Renderer>(true);
            if (rs != null)
            {
                for (int i = 0; i < rs.Length; ++i)
                {
                    Renderer r = rs[i];
                    if (r != null)
                    {
                        for (int j = 0; j < r.sharedMaterials.Length; ++j)
                        {
                            Material m = r.sharedMaterials[j];
                            if (m != null && m.renderQueue < value)
                                m.renderQueue = value;
                        }
                    }
                }
            }
        }

        static public System.Type FindType(string typeFullName)
        {
            // 导出类
            System.Reflection.Assembly[] ass = System.AppDomain.CurrentDomain.GetAssemblies();
            System.Type t = null;
            for (int i = 0; i < ass.Length; ++i)
            {
                t = ass[i].GetType(typeFullName);
                if (t != null)
                    break;
            }
            return t;
        }

        public static System.Type[] GetAllSubTypes(System.Type aBaseClass)
        {
            var result = new System.Collections.Generic.List<System.Type>();
            System.Reflection.Assembly[] AS = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var A in AS)
            {
                try
                {
                    System.Type[] types = A.GetTypes();
                    foreach (var T in types)
                    {
                        if (T.IsSubclassOf(aBaseClass))
                            result.Add(T);
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException) {; }
            }
            return result.ToArray();
        }

        public static T GetSubObject<T>() where T : new()
        {
            var types = UnityTools.GetAllSubTypes(typeof(T));
            if (types != null && types.Length > 0)
            {
                return (T)System.Activator.CreateInstance(types[types.Length - 1]);
            }
            return default(T);
        }

        public static void EnableCameraDepthMode(bool enable)
        {
            var cam = Camera.main;
            if (cam)
            {
                if (enable)
                    cam.depthTextureMode = DepthTextureMode.Depth;
                else
                    cam.depthTextureMode = DepthTextureMode.None;
            }
        }

        static int _grayScaleID = Shader.PropertyToID("_grayScale");
        public static void SetGray(GameObject go, bool gray)
        {
            if (go == null)
                return;

            var renders = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renders.Length; ++i)
            {
                var r = renders[i];
                var mats = r.materials;
                for (int j = 0; j < mats.Length; ++j)
                {
                    var mat = mats[j];
                    if (mat && mat.HasProperty(_grayScaleID))
                    {
                        mat.SetFloat(_grayScaleID, gray ? 1 : 0);
                    }
                }
                //if (r.sharedMaterial.HasProperty(_grayScaleID))
                //{
                //    if (gray)
                //    {
                //        r.material.SetFloat(_grayScaleID, 1);
                //    }
                //    else
                //    {
                //        r.material.SetFloat(_grayScaleID, 0);
                //    }
                //}
            }

        }


        public static void SwitchShadow(GameObject go, bool on)
        {
            var mode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var renders = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renders.Length; ++i)
            {
                var r = renders[i];
                r.shadowCastingMode = mode;
            }
        }

        static int __alphaPropID = Shader.PropertyToID("_alpha");
        public static void SetAlpha(GameObject go, float alpha)
        {
            if (go == null)
                return;

            var renders = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renders.Length; ++i)
            {
                var r = renders[i];
                var mats = r.materials;
                for (int j = 0; j < mats.Length; ++j)
                {
                    var mat = mats[j];
                    if (mat && mat.HasProperty(__alphaPropID))
                    {
                        mat.SetFloat(__alphaPropID, alpha);
                    }
                }
            }

        }

        public static void PlayAnimationOnTransform(Transform trans, string name)
        {
            if (trans)
            {
                var anims = trans.GetComponentsInChildren<Animation>();
                for (int i = 0; i < anims.Length; ++i)
                {
                    var ani = anims[i];
                    if (ani)
                    {
                        ani.Play(name);
                    }
                }
                var anim1s = trans.GetComponentsInChildren<Animator>();
                for (int i = 0; i < anim1s.Length; ++i)
                {
                    var ani = anim1s[i];
                    if (ani)
                    {
                        ani.Play(name, 0, 0);
                    }
                }
            }

        }

    }
}