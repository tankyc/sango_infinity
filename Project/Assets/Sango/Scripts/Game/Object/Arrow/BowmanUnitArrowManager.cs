using UnityEngine;
using System.Collections.Generic;
using Sango.Loader;

namespace Sango.Core.Object.Arrow
{
    /// <summary>
    /// 弓兵部队弓箭管理器
    /// </summary>
    public class BowmanUnitArrowManager : MonoBehaviour
    {
        /// <summary>
        /// 弓箭预制体
        /// </summary>
        [SerializeField]
        private GameObject arrowPrefab;
        /// <summary>
        /// 单次发射弓箭数量
        /// </summary>
        [SerializeField]
        private int arrowsPerShot = 20;
        /// <summary>
        /// 目标点随机范围（半径）
        /// </summary>
        [SerializeField]
        private float targetRandomRadius = 2f;
        /// <summary>
        /// 弓箭飞行时间范围
        /// </summary>
        [SerializeField]
        private Vector2 flightTimeRange = new Vector2(1f, 1.5f);
        /// <summary>
        /// 弓箭生成位置偏移范围
        /// </summary>
        [SerializeField]
        private float spawnOffsetRadius = 0.5f;

        private int arrowSound = 94;

        /// <summary>
        /// 发射弓箭到目标点
        /// </summary>
        /// <param name="targetPosition">目标点位置</param>
        public void FireArrows(Vector3 targetPosition)
        {

            GameMedia.Instance.PlaySfx(arrowSound);
            GameMedia.Instance.PlayDelayedSfx(arrowSound, Random.Range(0, flightTimeRange.y - flightTimeRange.x));
            GameMedia.Instance.PlayDelayedSfx(arrowSound, Random.Range(0, flightTimeRange.y - flightTimeRange.x));
            GameMedia.Instance.PlayDelayedSfx(arrowSound, Random.Range(0, flightTimeRange.y - flightTimeRange.x));
            GameMedia.Instance.PlayDelayedSfx(arrowSound, Random.Range(0, flightTimeRange.y - flightTimeRange.x));

            for (int i = 0; i < arrowsPerShot; i++)
            {
                // 计算随机目标位置（在目标点周围随机范围）
                Vector3 randomTarget = GetRandomTargetPosition(targetPosition);

                // 计算弓箭生成位置（在部队周围随机偏移）
                Vector3 spawnPosition = GetRandomSpawnPosition();

                // 计算随机飞行时间
                float flightTime = Random.Range(flightTimeRange.x, flightTimeRange.y);
              
                // 获取或创建弓箭
                GameObject arrow = GetOrCreateArrow();
                arrow.transform.SetParent(null);
                arrow.transform.position = spawnPosition;
                arrow.SetActive(true);

                // 开始飞行
                BowArrowParabola parabola = arrow.GetComponent<BowArrowParabola>();
                if (parabola != null)
                {
                    parabola.StartFlight(flightTime, spawnPosition, randomTarget);
                }
            }
        }

        /// <summary>
        /// 获取或创建弓箭
        /// </summary>
        /// <returns>弓箭GameObject</returns>
        private GameObject GetOrCreateArrow()
        {
            PoolManager.Add(arrowPrefab.name + "_arrow_ppp", arrowPrefab);

            return PoolManager.Get(arrowPrefab.name + "_arrow_ppp");
        }

        /// <summary>
        /// 获取随机目标位置
        /// </summary>
        /// <param name="targetPosition">中心目标点</param>
        /// <returns>随机目标位置</returns>
        private Vector3 GetRandomTargetPosition(Vector3 targetPosition)
        {
            // 在目标点周围生成随机偏移
            float randomAngle = Random.Range(0f, Mathf.PI * 2f);
            float randomDistance = Random.Range(0f, targetRandomRadius);

            float offsetX = Mathf.Cos(randomAngle) * randomDistance;
            float offsetZ = Mathf.Sin(randomAngle) * randomDistance;

            return new Vector3(
                targetPosition.x + offsetX,
                targetPosition.y,
                targetPosition.z + offsetZ
            );
        }

        /// <summary>
        /// 获取随机生成位置
        /// </summary>
        /// <returns>随机生成位置</returns>
        private Vector3 GetRandomSpawnPosition()
        {
            // 在部队位置周围生成随机偏移
            float randomAngle = Random.Range(0f, Mathf.PI * 2f);
            float randomDistance = Random.Range(0f, spawnOffsetRadius);

            float offsetX = Mathf.Cos(randomAngle) * randomDistance;
            float offsetZ = Mathf.Sin(randomAngle) * randomDistance;

            return new Vector3(
                transform.position.x + offsetX,
                transform.position.y,
                transform.position.z + offsetZ
            );
        }
    }
}
