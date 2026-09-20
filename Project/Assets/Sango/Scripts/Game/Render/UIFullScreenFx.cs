using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 全屏特效预制件组件:把 UIFullScreenSequence.Layer 图层配置序列化到预制件上,
    /// 可在 Unity 编辑器 Inspector 中可视编辑各图层的贴图/时间/缩放/层级,运行时实例化预制件即播放。
    /// 播放渲染逻辑完全复用 UIFullScreenSequence。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFullScreenFx : MonoBehaviour
    {
        /// <summary>全屏图层配置列表(Inspector 编辑)</summary>
        [SerializeField]
        UIFullScreenSequence.Layer[] layers = new UIFullScreenSequence.Layer[0];

        /// <summary>播放本预制件配置的全屏特效(渲染在动态 ScreenSpaceOverlay 画布上,播完画布自动销毁)</summary>
        public void Play()
        {
            if (layers == null || layers.Length == 0)
            {
                Debug.LogWarning($"UIFullScreenFx [{name}]: 未配置任何图层,无法播放");
                return;
            }
            UIFullScreenSequence.Play(layers);
        }

        /// <summary>全部图层播完所需总时长(用于定 PoolLife 回收时机),含 startDelay/duration/hold/fadeOut</summary>
        public float TotalDuration()
        {
            float max = 0f;
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    UIFullScreenSequence.Layer l = layers[i];
                    float end = l.startDelay + l.duration + l.holdAfterEnd + l.fadeOut;
                    if (end > max) max = end;
                }
            }
            return max;
        }

        /// <summary>
        /// 经 PoolManager 创建全屏特效预制件实例播放(预制件实例池化复用,播完自动回收再取复用)
        /// 预制件实例仅作为图层配置载体,实际渲染由 Play() 在动态 ScreenSpaceOverlay 画布上完成,
        /// 画布播完自行销毁;载体实例 PoolLife 到期回收回池,下次复用,不销毁不残留。
        /// </summary>
        /// <param name="prefabPath">预制件资源路径,如 "Assets/Effect/Prefab/ef_fx_thunder_full.prefab"</param>
        public static UIFullScreenFx PlayFx(string prefabPath)
        {
            GameObject go = PoolManager.Create(prefabPath);
            if (go == null)
            {
                Debug.LogError($"UIFullScreenFx: 预制件加载失败 {prefabPath}");
                return null;
            }
            go.transform.SetParent(null, false); // 从池父节点移出,恢复激活
            UIFullScreenFx fx = go.GetComponent<UIFullScreenFx>();
            if (fx == null)
            {
                Debug.LogError($"UIFullScreenFx: 预制件 {prefabPath} 未挂 UIFullScreenFx 组件,已回收");
                PoolManager.Recycle(go);
                return null;
            }
            fx.Play();
            // 播完自动回收复用(生命周期=最长图层结束时间+缓冲)
            PoolLife poolLife = go.GetComponent<PoolLife>();
            if (poolLife == null) poolLife = go.AddComponent<PoolLife>();
            poolLife.life = fx.TotalDuration() + 0.5f;
            return fx;
        }
    }
}