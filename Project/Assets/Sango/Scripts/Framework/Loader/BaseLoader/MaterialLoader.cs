using LuaInterface;
using UnityEngine;

namespace Sango.Loader
{
    public class MaterialLoader : ObjectLoader
    {
        private static Material m_Material;
        private static Material defaultMaterial
        {
            get { 
                if(m_Material == null) {
                    m_Material = new Material(Shader.Find("Diffuse"));
                }
                return m_Material;
            }
        }
        
       static public Material LoadMaterial(string matname, bool share = false)
        {
            CheckHelper();

            Material obj = AssetStore.Instance.CheckAsset<Material>(matname);
            if(obj != null) {
                if (share)
                    return obj;

                return GameObject.Instantiate(obj);
            }
            Material mat = null;
            // 以shader来创建材质球
            if(matname.EndsWith(".shader")) {
                mat = new Material(Shader.Find(matname.Substring(0, matname.Length - 7)));
                AssetStore.Instance.StoreAsset(matname, mat);
                if (share)
                    return mat;

                return GameObject.Instantiate(mat);
            }

            return defaultMaterial;
        }

    }
}
