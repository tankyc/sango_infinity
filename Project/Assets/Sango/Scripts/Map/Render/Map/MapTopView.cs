using System.Collections.Generic;
using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 顶视图（全局俯瞰）：把相机缓动到地图正上方垂直俯视，并暂时隐藏地形之外的所有渲染。
    ///
    /// 【四个职责，一一对应四条需求】
    ///   ① 算地形范围 → <see cref="CalcTopViewPose"/>：按 fov / aspect 反解"整张地图刚好装进屏幕"的相机高度；
    ///   ② 记录 + 缓动 → <see cref="Enter"/> / <see cref="Exit"/>：进入前快照原始姿态，SmoothStep 插值；
    ///   ③ 顶视图事件 → <see cref="MapRender.onTopViewChange"/> + <see cref="ApplySceneVisible"/>；
    ///   ④ 一动就恢复 → <see cref="HandleCameraInput"/>：任何镜头输入立刻开始退出缓动，并吞掉这次输入。
    ///
    /// 【原版无对应实现】Open311 没有独立的顶视图状态机。
    ///   本类只做表现层装配，不碰任何业务数据，也不改变地形/模型的数据结构。
    /// </summary>
    public class MapTopView : MapProperty
    {
        /// <summary>顶视图状态机。</summary>
        public enum ViewState
        {
            /// <summary>普通视角</summary>
            Idle,
            /// <summary>正在缓动进入</summary>
            Entering,
            /// <summary>已停在地图正上方</summary>
            Holding,
            /// <summary>正在缓动恢复</summary>
            Exiting,
        }

        /// <summary>相机姿态快照：观察点（XZ 平面）、相机到观察点的距离、欧拉角。</summary>
        public struct CameraPose
        {
            /// <summary>观察点世界坐标（y 恒为 0，地形高度由距离覆盖）</summary>
            public Vector3 lookPosition;
            /// <summary>相机到观察点的距离</summary>
            public float distance;
            /// <summary>欧拉角：x = 俯仰（90 = 垂直向下），y = 朝向</summary>
            public Vector3 rotate;
        }

        /// <summary>缓动时长（秒）。用 unscaledTime，暂停 / 慢动作下也能正常走完。</summary>
        public float duration = 0.7f;

        /// <summary>
        /// 画面留白系数。1.03 = 四周各留 3% 余量。
        /// 为什么要留：地形边缘的理论位置是数学上的精确边界，透视投影 + 屏幕像素取整会让它
        /// 恰好压到屏幕最外一列像素上，看起来像被裁掉了一条边。
        /// </summary>
        public float margin = 1.03f;

        /// <summary>俯仰角到这个值就认为"完全垂直"，此时跳过 LookAt（见 MapCamera.Update）。</summary>
        public const float VerticalPitch = 90f;

        /// <summary>当前状态。</summary>
        public ViewState state { get; private set; } = ViewState.Idle;

        /// <summary>是否处于顶视图（含进出缓动）。</summary>
        public bool isActive => state != ViewState.Idle;

        /// <summary>缓动起点姿态。</summary>
        private CameraPose fromPose;

        /// <summary>缓动终点姿态。</summary>
        private CameraPose toPose;

        /// <summary>进入顶视图前的原始姿态，退出时回到这里。</summary>
        private CameraPose savedPose;

        /// <summary>缓动进度 0~1。</summary>
        private float progress;

        /// <summary>
        /// 进入顶视图时被失活的场景根特效。
        /// 特效由 GameParticales.PlayEfect 创建后 SetParent(null)，游离在场景根，
        /// 不随 modelRoot 一起隐藏，必须单独记录、单独恢复。
        /// </summary>
        private readonly List<GameObject> hiddenEffects = new List<GameObject>();

        public MapTopView(MapRender map) : base(map)
        {
        }

        public override void Init()
        {
            base.Init();

            // 镜头输入拦截：顶视图（含进出缓动）期间任何操作都视为"我要回去"
            if (map != null && map.mapCamera != null)
                map.mapCamera.interceptInput = HandleCameraInput;
        }

        public override void Clear()
        {
            base.Clear();

            state = ViewState.Idle;
            progress = 0f;
            RestoreSceneVisible();
            hiddenEffects.Clear();

            if (map != null && map.mapCamera != null)
                map.mapCamera.interceptInput = null;
        }

        /// <summary>切换顶视图 / 普通视角。</summary>
        public void Toggle()
        {
            if (isActive)
                Exit();
            else
                Enter();
        }

        /// <summary>进入顶视图：快照当前姿态，缓动到地图正上方，并隐藏地形之外的渲染。</summary>
        public void Enter()
        {
            if (map == null || map.mapCamera == null || map.mapData == null)
                return;
            if (state == ViewState.Entering || state == ViewState.Holding)
                return;

            // 先停掉"镜头平滑移动到某点"，否则它会和顶视图缓动抢同一个 position
            map.StopSmoothMove();

            // 恢复途中又被要求进顶视图（Exiting 状态下点按钮）：savedPose 仍是原始姿态，
            // 不能被"恢复了一半的中间位置"覆盖，否则这一次退出就回不到原点了
            bool resuming = state == ViewState.Exiting;
            fromPose = CapturePose();
            if (!resuming)
                savedPose = fromPose;

            toPose = CalcTopViewPose();

            // 朝向取"离当前朝向最近的 -90"：直接写 -90 会让转了 170° 的镜头绕大半个圈回来
            toPose.rotate.y = NearestAngle(fromPose.rotate.y, toPose.rotate.y);

            progress = 0f;
            state = ViewState.Entering;

            // 显隐立即生效，不等缓动结束：顶视图看的是地形轮廓，模型留在画面上反而挡视线
            ApplySceneVisible(false);

            map.onTopViewChange?.Invoke(true);
        }

        /// <summary>
        /// 立即结束顶视图（不缓动），并恢复地形之外的渲染。
        /// 用于"定位到某处""直接设置视角"这类需要当前帧就接管相机的操作 ——
        /// 走 <see cref="Exit"/> 的话缓动会在同一帧把位置改回去，表现为"点了没反应"。
        /// </summary>
        public void Cancel()
        {
            if (state == ViewState.Idle)
                return;

            progress = 0f;
            state = ViewState.Idle;
            RestoreSceneVisible();

            if (map != null)
                map.onTopViewChange?.Invoke(false);
        }

        /// <summary>退出顶视图：缓动回进入前的姿态，缓动结束后恢复地形之外的渲染。</summary>
        public void Exit()
        {
            if (map == null || map.mapCamera == null)
                return;
            if (state == ViewState.Idle || state == ViewState.Exiting)
                return;

            fromPose = CapturePose();
            toPose = savedPose;
            progress = 0f;
            state = ViewState.Exiting;

            map.onTopViewChange?.Invoke(false);
        }

        public override void Update()
        {
            if (state != ViewState.Entering && state != ViewState.Exiting)
                return;

            progress += Time.unscaledDeltaTime / Mathf.Max(duration, 0.01f);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));

            ApplyPose(Lerp(fromPose, toPose, k));

            if (progress < 1f)
                return;

            // 收尾：把终点值原样写一遍，避免插值误差让相机停在"差一点点"的位置
            ApplyPose(toPose);

            if (state == ViewState.Entering)
            {
                state = ViewState.Holding;
            }
            else
            {
                state = ViewState.Idle;
                // 缓动结束再恢复可见：恢复途中模型逐渐出现又立刻被镜头带走，观感更差
                RestoreSceneVisible();
            }
        }

        /// <summary>
        /// 计算"整张地图刚好装进屏幕"的相机姿态。
        ///
        /// 【轴向】相机取 <c>Euler(90, -90, 0)</c>：forward 垂直向下，right 指向世界 +Z，up 指向世界 -X。
        ///   而地图的世界范围是 X ∈ [0, world_height]、Z ∈ [0, world_width]
        ///   （与 MapCamera.position 的钳制范围 world_height / world_width 完全一致），于是：
        ///     · 屏幕水平方向要覆盖 world_width（世界 Z）；
        ///     · 屏幕垂直方向要覆盖 world_height（世界 X）。
        /// 【距离】透视相机在距离 d 处的半高 = d·tan(fov/2)、半宽 = 半高 × aspect，
        ///   反解出两个方向各自需要的最小距离，取较大者再乘留白系数。
        ///   竖屏手机上 aspect &lt; 1，水平方向通常是瓶颈，取 max 正好覆盖。
        /// </summary>
        public CameraPose CalcTopViewPose()
        {
            CameraPose pose = new CameraPose();

            if (map == null || map.mapData == null || map.mapCamera == null || map.mapCamera.camera == null)
                return pose;

            float worldWidth = map.mapData.world_width;    // 世界 Z 方向边长
            float worldHeight = map.mapData.world_height;  // 世界 X 方向边长

            // 观察点放地图正中心。注意不能用 MapCamera.position 的 setter：
            // 它会把观察点钳在 [safeBoder, world - safeBoder]，小地图上中心点会被推偏
            pose.lookPosition = new Vector3(worldHeight * 0.5f, 0f, worldWidth * 0.5f);
            pose.rotate = new Vector3(VerticalPitch, -90f, 0f);

            Camera cam = map.mapCamera.camera;
            float halfTan = Mathf.Tan(Mathf.Clamp(cam.fieldOfView, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
            if (halfTan <= 0.0001f)
                halfTan = 0.0001f;

            float aspect = Mathf.Max(cam.aspect, 0.1f);

            float distanceForHeight = (worldHeight * 0.5f) / halfTan;
            float distanceForWidth = (worldWidth * 0.5f) / (halfTan * aspect);

            pose.distance = Mathf.Max(distanceForHeight, distanceForWidth) * Mathf.Max(margin, 1f);

            return pose;
        }

        /// <summary>
        /// 镜头输入拦截。顶视图（含缓动）期间玩家的任何镜头操作都立刻开始恢复，
        /// 并吞掉这一次输入 —— 否则恢复途中 cur_distance / look_rotate 会被输入改写，
        /// 表现为"一滚轮镜头就跳一下再慢慢回"。
        /// </summary>
        private bool HandleCameraInput()
        {
            if (state == ViewState.Idle)
                return false;

            if (state == ViewState.Entering || state == ViewState.Holding)
                Exit();

            return true;
        }

        /// <summary>地形之外的所有渲染显隐开关。</summary>
        private void ApplySceneVisible(bool visible)
        {
            // ① 建筑 / 部队 / 地图标注 / 地图建筑：统一挂在 modelRoot 下，整棵树一次开关
            if (MapRender.modelRoot != null)
                MapRender.modelRoot.gameObject.SetActive(visible);

            // ② 植被走 Graphics.DrawMeshInstanced，不受 GameObject 失活影响，必须单独关
            if (map != null && map.mapModels != null)
                map.mapModels.renderEnabled = visible;

            // ③ 特效游离在场景根，单独收集；同时掐掉"新特效"的产生源，
            //    否则顶视图期间打一场仗就会有一堆特效突然冒在空旷的地形上
            SetEffectsVisible(visible);
            Sango.Core.GameParticales.EffectEnabled = visible;
            SpriteSequenceEffect.EffectEnabled = visible;
        }

        /// <summary>恢复地形之外的渲染。</summary>
        private void RestoreSceneVisible()
        {
            ApplySceneVisible(true);
        }

        /// <summary>
        /// 场景根特效显隐。
        ///
        /// 【为什么不能只关 modelRoot】技能特效由 GameParticales.PlayEfect 创建后
        /// <c>SetParent(null)</c>，是场景根下的独立对象；挂在部队节点下的那部分会随模型树一起隐藏。
        /// 这里在进入时把场景根上的特效收进 <see cref="hiddenEffects"/> 并失活，退出时原样恢复。
        /// </summary>
        private void SetEffectsVisible(bool visible)
        {
            if (visible)
            {
                for (int i = 0; i < hiddenEffects.Count; i++)
                {
                    GameObject go = hiddenEffects[i];
                    if (go != null)
                        go.SetActive(true);
                }
                hiddenEffects.Clear();
                return;
            }

            hiddenEffects.Clear();

            ParticleSystem[] particles = UnityEngine.Object.FindObjectsOfType<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                CollectEffectRoot(particles[i] != null ? particles[i].gameObject : null);
            }

            // 序列帧特效不一定带 ParticleSystem（见 SpriteSequenceEffect），单独补一遍
            SpriteSequenceEffect[] sequences = UnityEngine.Object.FindObjectsOfType<SpriteSequenceEffect>(true);
            for (int i = 0; i < sequences.Length; i++)
            {
                CollectEffectRoot(sequences[i] != null ? sequences[i].gameObject : null);
            }
        }

        /// <summary>把一个候选特效根收进隐藏列表并失活（已在 modelRoot 下 / 非场景根 / 已失活的跳过）。</summary>
        private void CollectEffectRoot(GameObject go)
        {
            if (go == null)
                return;

            // 挂在模型树下的会随 modelRoot 一起隐藏，不需要单独记录
            if (MapRender.modelRoot != null && go.transform.IsChildOf(MapRender.modelRoot))
                return;

            // 只处理场景根上的特效根节点：子节点会随父节点一起失活，重复记录反而会漏恢复
            if (go.transform.parent != null)
                return;

            if (!go.activeSelf)
                return;

            if (hiddenEffects.Contains(go))
                return;

            hiddenEffects.Add(go);
            go.SetActive(false);
        }

        /// <summary>快照当前相机姿态。</summary>
        private CameraPose CapturePose()
        {
            CameraPose pose = new CameraPose();
            if (map != null && map.mapCamera != null)
                map.mapCamera.GetPose(out pose.lookPosition, out pose.distance, out pose.rotate);
            return pose;
        }

        /// <summary>把姿态写回相机，并按新距离刷新雾效 / 远裁剪 / 天空盒缩放。</summary>
        private void ApplyPose(CameraPose pose)
        {
            if (map == null || map.mapCamera == null)
                return;

            map.mapCamera.SetPoseRaw(pose.lookPosition, pose.distance, pose.rotate);
            // 顶视图距离远高于常规缩放上限，必须按新距离重算 farClipPlane，否则地形被远裁剪面切掉
            map.mapCamera.ApplyViewDistance();
        }

        private static CameraPose Lerp(CameraPose a, CameraPose b, float t)
        {
            CameraPose pose;
            pose.lookPosition = Vector3.LerpUnclamped(a.lookPosition, b.lookPosition, t);
            pose.distance = Mathf.LerpUnclamped(a.distance, b.distance, t);
            pose.rotate = new Vector3(
                Mathf.LerpUnclamped(a.rotate.x, b.rotate.x, t),
                Mathf.LerpUnclamped(a.rotate.y, b.rotate.y, t),
                Mathf.LerpUnclamped(a.rotate.z, b.rotate.z, t));
            return pose;
        }

        /// <summary>把 to 规范成与 from 最接近的等价角，避免缓动时绕远路转整圈。</summary>
        private static float NearestAngle(float from, float to)
        {
            float delta = Mathf.Repeat(to - from + 180f, 360f) - 180f;
            return from + delta;
        }
    }
}
