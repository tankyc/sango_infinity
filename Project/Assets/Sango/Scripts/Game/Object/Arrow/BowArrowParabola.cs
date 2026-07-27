using UnityEngine;

namespace Sango.Core.Object.Arrow
{
    /// <summary>
    /// 弓箭抛物线飞行
    /// </summary>
    public class BowArrowParabola : MonoBehaviour
    {
        /// <summary>
        /// 爆炸特效预制体
        /// </summary>
        [SerializeField]
        public GameObject explosionEffectPrefab;

        /// <summary>
        /// 命中音效
        /// </summary>
        public int hitSoundId = 71;


        /// <summary>
        /// 飞行时间
        /// </summary>
        private float flightTime;
        /// <summary>
        /// 起点位置
        /// </summary>
        private Vector3 startPosition;
        /// <summary>
        /// 终点位置
        /// </summary>
        private Vector3 endPosition;
        /// <summary>
        /// 已经飞行的时间
        /// </summary>
        private float elapsedTime = 0f;
        /// <summary>
        /// 是否正在飞行
        /// </summary>
        private bool isFlying = false;
        /// <summary>
        /// 是否正在消失
        /// </summary>
        private bool isDisappearing = false;
        /// <summary>
        /// 消失计时器
        /// </summary>
        private float disappearTimer = 0f;
        /// <summary>
        /// 停留时间
        /// </summary>
        private float stayTime = 1f;
        /// <summary>
        /// 消失时间
        /// </summary>
        private float disappearDuration = 1f;
        /// <summary>
        /// 渲染器组件
        /// </summary>
        private Renderer[] renderers;
        /// <summary>
        /// 初始颜色
        /// </summary>
        //private Color[] initialColors;

        private void Awake()
        {
            // 获取所有渲染器组件
            renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// 开始抛物线飞行
        /// </summary>
        /// <param name="time">飞行时间</param>
        /// <param name="start">起点位置</param>
        /// <param name="end">终点位置</param>
        public void StartFlight(float time, Vector3 start, Vector3 end)
        {
            flightTime = time;
            startPosition = start;
            endPosition = end;
            elapsedTime = 0f;
            isFlying = true;
            isDisappearing = false;
            disappearTimer = 0f;

            
            //initialColors = new Color[renderers.Length];
            
            // 保存初始颜色
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material != null)
                {
                    //initialColors[i] = renderers[i].material.color;
                    renderers[i].material.SetFloat("_Alpha", 1f);
                }
            }

            // 设置初始位置
            transform.position = startPosition;
        }

        private void Update()
        {
            if (isFlying)
            {
                // 更新飞行时间
                elapsedTime += Time.deltaTime;

                // 计算飞行进度（0到1之间）
                float t = Mathf.Clamp01(elapsedTime / flightTime);

                // 计算当前位置（抛物线）
                Vector3 currentPosition = CalculateParabolaPosition(t);

                // 更新位置
                transform.position = currentPosition;

                // 计算方向并旋转弓箭
                if (t < 1f)
                {
                    Vector3 nextPosition = CalculateParabolaPosition(t + 0.01f);
                    Vector3 direction = nextPosition - currentPosition;
                    if (direction != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(direction);
                    }
                }

                // 飞行结束
                if (t >= 1f)
                {
                    // 确保到达终点
                    transform.position = endPosition;
                    isFlying = false;
                    
                    // 播放爆炸特效
                    PlayExplosionEffect();
                }
            }
            else if (!isDisappearing)
            {
                // 停留时间倒计时
                disappearTimer += Time.deltaTime;
                if (disappearTimer >= stayTime)
                {
                    // 开始消失
                    isDisappearing = true;
                    disappearTimer = 0f;
                }
            }
            else
            {
                // 消失过程
                disappearTimer += Time.deltaTime;
                float disappearProgress = Mathf.Clamp01(disappearTimer / disappearDuration);
                
                // 逐渐减少透明度
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].material != null)
                    {
                        //Color newColor = initialColors[i];
                        //newColor.a = 1f - disappearProgress;
                        //renderers[i].material.color = newColor;
                        renderers[i].material.SetFloat("_Alpha", 1f - disappearProgress);
                    }
                }
                
                // 消失完成
                if (disappearProgress >= 1f)
                {
                    // 禁用弓箭
                    gameObject.SetActive(false);
                    PoolManager.Recycle(gameObject);
                }
            }
        }

        /// <summary>
        /// 计算抛物线位置
        /// </summary>
        /// <param name="t">进度（0到1）</param>
        /// <returns>当前位置</returns>
        private Vector3 CalculateParabolaPosition(float t)
        {
            // 线性插值计算水平位置
            Vector3 linearPosition = Vector3.Lerp(startPosition, endPosition, t);

            // 计算抛物线高度
            // 最高点在中间，高度为两点距离的1/4
            float height = Vector3.Distance(startPosition, endPosition) * 0.25f;
            float parabolaHeight = height * 4 * t * (1 - t);

            // 加上高度分量
            return linearPosition + new Vector3(0, parabolaHeight, 0);
        }
        /// <summary>
        /// 播放爆炸特效
        /// </summary>
        private void PlayExplosionEffect()
        {
            if (explosionEffectPrefab != null)
            {
                // 实例化爆炸特效
                GameObject explosion = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);

                GameMedia.Instance.PlaySfx(hitSoundId);

                // 查找特效中的粒子系统或动画组件
                ParticleSystem particleSystem = explosion.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    // 获取粒子系统的持续时间
                    float duration = particleSystem.main.duration;
                    // 延迟销毁特效
                    Destroy(explosion, duration);
                }
                else
                {
                    // 如果没有粒子系统，默认2秒后销毁
                    Destroy(explosion, 2f);
                }
            }
        }
    }
}
