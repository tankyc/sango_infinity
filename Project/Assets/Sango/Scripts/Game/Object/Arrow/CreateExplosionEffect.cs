using UnityEngine;

namespace Sango.Core.Object.Arrow
{
    /// <summary>
    /// 创建爆炸特效预制体
    /// </summary>
    public class CreateExplosionEffect : MonoBehaviour
    {
        [SerializeField]
        private string prefabPath = "Assets/Sango/Prefabs/Arrow/ExplosionEffect.prefab";

        [ContextMenu("Create Explosion Effect Prefab")]
        public void CreatePrefab()
        {
            // 创建爆炸特效GameObject
            GameObject explosionEffect = new GameObject("ExplosionEffect");
            
            // 添加粒子系统
            ParticleSystem particleSystem = explosionEffect.AddComponent<ParticleSystem>();
            
            // 配置粒子系统
            var main = particleSystem.main;
            main.startSize = 0.5f;
            main.startSpeed = 5f;
            main.startLifetime = 1f;
            main.maxParticles = 100;
            main.duration = 1f;
            main.loop = false;
            
            // 配置发射模块
            var emission = particleSystem.emission;
            emission.rateOverTime = 0;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 50));
            
            // 配置形状模块
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            
            // 配置颜色模块
            var color = particleSystem.colorOverLifetime;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(Color.red, 0.5f), new GradientColorKey(Color.black, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            color.color = gradient;
            
            // 配置大小模块
            var size = particleSystem.sizeOverLifetime;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0f);
            var minmax = size.size;
            minmax.curve = sizeCurve;
            size.size = minmax;
            
            // 保存为预制体
            #if UNITY_EDITOR
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(explosionEffect, prefabPath);
            Sango.Log.Info("Explosion effect prefab created at: " + prefabPath, Sango.Log.LogType.World);
            #endif
            
            // 销毁临时对象
            DestroyImmediate(explosionEffect);
        }
    }
}
