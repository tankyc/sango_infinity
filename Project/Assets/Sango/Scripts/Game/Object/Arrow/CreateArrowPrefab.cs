using UnityEngine;

namespace Sango.Core.Object.Arrow
{
    /// <summary>
    /// 创建弓箭预制体
    /// </summary>
    public class CreateArrowPrefab : MonoBehaviour
    {
        [SerializeField]
        private string prefabPath = "Assets/Sango/Prefabs/Arrow/Arrow.prefab";

        [ContextMenu("Create Arrow Prefab")]
        public void CreatePrefab()
        {
            // 创建弓箭GameObject
            GameObject arrow = new GameObject("Arrow");
            
            // 添加BowArrowParabola脚本
            BowArrowParabola parabola = arrow.AddComponent<BowArrowParabola>();
            
            // 设置爆炸特效预制体路径
            #if UNITY_EDITOR
            string explosionEffectPath = "Assets/Sango/Prefabs/Arrow/ExplosionEffect.prefab";
            UnityEngine.Object explosionEffectObj = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(explosionEffectPath);
            if (explosionEffectObj != null)
            {
                parabola.explosionEffectPrefab = explosionEffectObj as GameObject;
            }
            #endif
            
            // 添加碰撞器
            SphereCollider collider = arrow.AddComponent<SphereCollider>();
            collider.radius = 0.1f;
            
            // 添加刚体
            Rigidbody rigidbody = arrow.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            
            // 创建一个简单的箭头模型
            GameObject arrowModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowModel.transform.SetParent(arrow.transform, false);
            arrowModel.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
            arrowModel.transform.localPosition = new Vector3(0, 0.25f, 0);
            
            // 创建箭头
            GameObject arrowHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowHead.transform.SetParent(arrow.transform, false);
            arrowHead.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            arrowHead.transform.localPosition = new Vector3(0, 0.6f, 0);
            arrowHead.transform.localRotation = Quaternion.Euler(0, 0, 180);
            
            // 保存为预制体
            #if UNITY_EDITOR
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(arrow, prefabPath);
            Sango.Log.Info("Arrow prefab created at: " + prefabPath, Sango.Log.LogType.World);
            #endif
            
            // 销毁临时对象
            DestroyImmediate(arrow);
        }
    }
}
