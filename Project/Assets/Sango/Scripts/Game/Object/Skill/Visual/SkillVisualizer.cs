using Sango.Render;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    public abstract class SkillVisualizer
    {
        public SkillInstance skillInstance;
        public virtual void Init(SkillInstance skillInstance) { this.skillInstance = skillInstance; }
        public abstract void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList);
        public virtual void StopSkillVisual() { }

        public delegate SkillVisualizer SkillVisualizerCreator();

        public static Dictionary<string, SkillVisualizerCreator> CreateMap = new Dictionary<string, SkillVisualizerCreator>();
        public static void Register(string name, SkillVisualizerCreator creator)
        {
            CreateMap[name] = creator;
        }
        public static SkillVisualizer CreateHandle<T>() where T : SkillVisualizer, new()
        {
            return new T();
        }
        public static SkillVisualizer Create(string name)
        {
            SkillVisualizerCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {
            Register("Default", CreateHandle<DefaultSkillVisualizer>);
            Register("Range", CreateHandle<RangeSkillVisualizer>);
            Register("Melee", CreateHandle<MeleeSkillVisualizer>);
            Register("Strategy", CreateHandle<StrategySkillVisualizer>);
            Register("CyclonSkill", CreateHandle<CyclonSkillVisualizer>);
            Register("ThunderboltSkill", CreateHandle<ThunderboltSkillVisualizer>);
        }
    }

    public class DefaultSkillVisualizer : SkillVisualizer
    {
        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 默认技能视觉效果
            troop.Render.SetAniShow(1);
            troop.Render.FaceTo(spellCell.Position);
        }
    }

    public class RangeSkillVisualizer : SkillVisualizer
    {
        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 远程技能视觉效果
            troop.Render.SetAniShow(1, true);
            troop.Render.FaceTo(spellCell.Position);
            // 发射箭头特效
            troop.Render.CastArrow(spellCell.Position);
        }
    }

    public class MeleeSkillVisualizer : SkillVisualizer
    {
        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 近战技能视觉效果
            troop.Render.SetAniShow(2);
            troop.Render.FaceTo(spellCell.Position);
        }
    }

    public class StrategySkillVisualizer : SkillVisualizer
    {
        // 计策特效预制体资源路径(可替换为其他特效)
        const string StrategyEffectAsset = "Assets/Effect/Prefab/ef_thunder_02.prefab";

        // 落雷第一段(全屏):挂 UIFullScreenFx 组件,16个图层在 Inspector 可视编辑
        const string FullScreenFxAsset = "Assets/Effect/Prefab/ef_fx_thunder_full.prefab";

        // 计略"落雷"技能Id：仅该技能播放全屏第一段特效
        const int ThunderSkillId = 29;
        // 落雷第二段(普通特效,非全屏):519/521/522 预制体(经 GameParticales 池化播放,挂 SpriteSequenceEffect)
        // 命名与素材编号一一对应:ef_thunder_20=519, ef_thunder_21=521, ef_thunder_22=522
        const string Bolt519EffectAsset = "Assets/Effect/Prefab/ef_thunder_20.prefab";
        const string Bolt521EffectAsset = "Assets/Effect/Prefab/ef_thunder_21.prefab";
        const string Bolt522EffectAsset = "Assets/Effect/Prefab/ef_thunder_22.prefab";
        // 落雷尺寸/高度:预制体 screenHeightFactor 屏幕关联(世界宽=相机可视高度×系数)+播放时按可视高度定高,不写死固定值
        const float Bolt519HeightRate = 0.5f; 
        const float Bolt522HeightRate = 0f;  
        static readonly float[] Bolt521HeightRates = { 0.1f, 0.25f, 0.5f };

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 计策技能视觉效果
            troop.Render.SetAniShow(3);
            troop.Render.FaceTo(spellCell.Position);
            bool isThunder = skillInstance != null && skillInstance.Id == ThunderSkillId;
            if (isThunder)
            {
                // === 落雷第一段(全屏) == //
                //   125-1        0.0~0.1  全屏白光
                //   125-32-2  0.0~0.2 缩到普通, 保持至0.725
                //   125-21 2行第1列  0.1~0.175 淡入
                //   125-21 2行第2列  0.175~0.25 淡出
                //   125-2  2行第1列  0.175~0.225 淡入
                //   125-2  2行第2列  0.225~0.375 淡出
                //   125-21 2行第4列  0.2~0.35 淡出
                //   497-1  第1次     0.55~1.45 (放大3倍,80%→30%)
                //   125-5           0.75~0.95 淡出
                //   125-3  依次4帧   0.8~1.0/1.0~1.2/1.2~1.4/1.4~1.6
                //   497-1  第2次     0.67~1.57 (放大3倍,80%→30%)
                //   497-1  第3次     0.79~1.69 (放大3倍,80%→30%)
                UIFullScreenFx.PlayFx(FullScreenFxAsset);

                // === 落雷第二段(普通) == //
                // 522:按命中目标格(目标+周围1格)各放一遍; 519/521:固定只播1次,以主目标格为中心
                // 尺寸: 预制体 screenHeightFactor 屏幕关联模式;   高度: 以相机可视高度为基准,相机缩放时自动贴合。
                float grid = Scenario.Cur != null && Scenario.Cur.Map != null && Scenario.Cur.Map.GridSize > 0f
                    ? Scenario.Cur.Map.GridSize : 20f;
                // 落雷不分敌我:收集范围内实际受影响的目标格(有部队或建筑,无论势力)
                List<Cell> targetCells = new List<Cell>();
                for (int i = 0; i < atkCellList.Count; i++)
                {
                    Cell c = atkCellList[i];
                    if (c != null && (c.troop != null || c.building != null))
                        targetCells.Add(c);
                }
                if (targetCells.Count == 0)
                    targetCells.Add(spellCell);
                // 522 按命中目标格各放一遍
                foreach (Cell atkCell in targetCells)
                {
                    float visH = SpriteSequenceEffect.ScreenVisibleWorldHeight(atkCell.Position);
                    if (visH <= 0f) visH = grid * 8f;
                    // 522
                    Vector3 boltPos = new Vector3(atkCell.Position.x, atkCell.Position.y + visH * Bolt522HeightRate, atkCell.Position.z);
                    GameParticales.Instance.PlayEfect(Bolt522EffectAsset, boltPos, 4f);
                }
                // 519:固定只播放1次
                float visH19 = SpriteSequenceEffect.ScreenVisibleWorldHeight(spellCell.Position);
                if (visH19 <= 0f) visH19 = grid * 8f;
                Vector3 bolt19Pos = new Vector3(spellCell.Position.x, spellCell.Position.y + visH19 * Bolt519HeightRate, spellCell.Position.z);
                GameParticales.Instance.PlayEfect(Bolt519EffectAsset, bolt19Pos, 3f);
                // 521:固定只播放1次
                float visH23 = SpriteSequenceEffect.ScreenVisibleWorldHeight(spellCell.Position);
                if (visH23 <= 0f) visH23 = grid * 8f;
                float offset23Mid = grid * 0.45f;  // 中处:右偏移0.45格
                float offset23Up = grid * 0.25f;   // 上处:左偏移0.25格
                float[] bolt23Delays = { 2.405f, 2.305f, 2.205f }; 
                Vector3[] bolt23Pos =
                {
                    new Vector3(spellCell.Position.x,             spellCell.Position.y + visH23 * Bolt521HeightRates[0], spellCell.Position.z), // 下水平不变
                    new Vector3(spellCell.Position.x + offset23Mid, spellCell.Position.y + visH23 * Bolt521HeightRates[1], spellCell.Position.z), // 中右偏移0.45格
                    new Vector3(spellCell.Position.x - offset23Up, spellCell.Position.y + visH23 * Bolt521HeightRates[2], spellCell.Position.z)  // 上左偏移0.25格
                };
                for (int i = 0; i < Bolt521HeightRates.Length; i++)
                {
                    GameObject g = GameParticales.Instance.PlayEfect(Bolt521EffectAsset, bolt23Pos[i], 6f);
                    if (g != null)
                    {
                        var seq = g.GetComponent<SpriteSequenceEffect>();
                        if (seq != null) seq.SetStartDelay(bolt23Delays[i]);
                    }
                }
                return;
            }
            foreach (var cell in atkCellList)
            {
                // 保留原计策特效预制体
                troop.Render.PlayEffect(StrategyEffectAsset);
            }
        }
    }


    public class CyclonSkillVisualizer : MeleeSkillVisualizer
    {
        // 近战命中特效预制体(普通攻击/近战战法通用)
        const string MeleeHitEffectAsset = "Assets/Effect/Prefab/ef_titans_attack_1.prefab";

        // 旋风战法(技能Id=8)特效：1张精灵图集(7列x4行=28帧)逐帧播放
        const int CycloneSkillId = 8;
        const string CycloneSheetPath = "Assets/Effect/Sprite/Cyclone/cyclone_sheet.png";
        const int CycloneSheetCols = 7;
        const int CycloneSheetRows = 4;
        const int CycloneFrameCount = 28;
        const float CycloneFps = 20f;
        const float CycloneWorldSize = 150f;

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 默认技能视觉效果
            troop.Render.SetAniShow(1);
            troop.Render.FaceTo(spellCell.Position);

            if (skillInstance != null && skillInstance.Id == CycloneSkillId)
            {
                // 旋风战法：在【施法者部队所在格子】只放 1 个大旋风（不再遍历受击目标）
                Cell casterCell = troop.cell ?? spellCell;
                Vector3 pos = casterCell.Position + Vector3.up * 3f;
                var eff = SpriteSequenceEffect.Play(CycloneSheetPath, CycloneSheetCols, CycloneSheetRows,
                                                     CycloneFrameCount, pos, fps: CycloneFps, worldSize: CycloneWorldSize);
                if (eff != null)
                {
                    eff.autoDestroy = true;
                }
                else
                    Debug.LogError($"旋风特效播放失败: sheet={CycloneSheetPath}");
                return;
            }

            // 在受击目标格子播放近战命中特效
            foreach (var cell in atkCellList)
            {
                PlayMeleeHitEffect(troop, cell);
            }
        }

        // 在目标格子播放近战命中特效
        void PlayMeleeHitEffect(Troop troop, Cell cell)
        {
            GameObject effect = troop.Render.PlayEffect(MeleeHitEffectAsset);
            if (effect != null)
            {
                // 挂载到部队节点后定位到目标格子世界坐标
                effect.transform.position = cell.Position;
            }
        }
    }

    public class ThunderboltSkillVisualizer : StrategySkillVisualizer
    {
        // 计策特效预制体资源路径(可替换为其他特效)
        const string StrategyEffectAsset = "Assets/Effect/Prefab/ef_thunder_02.prefab";

        // 落雷第一段(全屏):挂 UIFullScreenFx 组件,16个图层在 Inspector 可视编辑
        const string FullScreenFxAsset = "Assets/Effect/Prefab/ef_fx_thunder_full.prefab";

        // 落雷第二段(普通特效,非全屏):519/521/522 预制体(经 GameParticales 池化播放,挂 SpriteSequenceEffect)
        // 命名与素材编号一一对应:ef_thunder_20=519, ef_thunder_21=521, ef_thunder_22=522
        const string Bolt519EffectAsset = "Assets/Effect/Prefab/ef_thunder_20.prefab";
        const string Bolt521EffectAsset = "Assets/Effect/Prefab/ef_thunder_21.prefab";
        const string Bolt522EffectAsset = "Assets/Effect/Prefab/ef_thunder_22.prefab";
        // 落雷尺寸/高度:预制体 screenHeightFactor 屏幕关联(世界宽=相机可视高度×系数)+播放时按可视高度定高,不写死固定值
        const float Bolt519HeightRate = 0.5f;
        const float Bolt522HeightRate = 0f;
        static readonly float[] Bolt521HeightRates = { 0.1f, 0.25f, 0.5f };

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 计策技能视觉效果
            troop.Render.SetAniShow(3);
            troop.Render.FaceTo(spellCell.Position);

            // === 落雷第一段(全屏) == //
            //   125-1        0.0~0.1  全屏白光
            //   125-32-2  0.0~0.2 缩到普通, 保持至0.725
            //   125-21 2行第1列  0.1~0.175 淡入
            //   125-21 2行第2列  0.175~0.25 淡出
            //   125-2  2行第1列  0.175~0.225 淡入
            //   125-2  2行第2列  0.225~0.375 淡出
            //   125-21 2行第4列  0.2~0.35 淡出
            //   497-1  第1次     0.55~1.45 (放大3倍,80%→30%)
            //   125-5           0.75~0.95 淡出
            //   125-3  依次4帧   0.8~1.0/1.0~1.2/1.2~1.4/1.4~1.6
            //   497-1  第2次     0.67~1.57 (放大3倍,80%→30%)
            //   497-1  第3次     0.79~1.69 (放大3倍,80%→30%)
            UIFullScreenFx.PlayFx(FullScreenFxAsset);

            // === 落雷第二段(普通) == //
            // 522:按命中目标格(目标+周围1格)各放一遍; 519/521:固定只播1次,以主目标格为中心
            // 尺寸: 预制体 screenHeightFactor 屏幕关联模式;   高度: 以相机可视高度为基准,相机缩放时自动贴合。
            float grid = Scenario.Cur != null && Scenario.Cur.Map != null && Scenario.Cur.Map.GridSize > 0f
                ? Scenario.Cur.Map.GridSize : 20f;
            // 落雷不分敌我:收集范围内实际受影响的目标格(有部队或建筑,无论势力)
            List<Cell> targetCells = new List<Cell>();
            for (int i = 0; i < atkCellList.Count; i++)
            {
                Cell c = atkCellList[i];
                if (c != null && (c.troop != null || c.building != null))
                    targetCells.Add(c);
            }
            if (targetCells.Count == 0)
                targetCells.Add(spellCell);
            // 522 按命中目标格各放一遍
            foreach (Cell atkCell in targetCells)
            {
                float visH = SpriteSequenceEffect.ScreenVisibleWorldHeight(atkCell.Position);
                if (visH <= 0f) visH = grid * 8f;
                // 522
                Vector3 boltPos = new Vector3(atkCell.Position.x, atkCell.Position.y + visH * Bolt522HeightRate, atkCell.Position.z);
                GameParticales.Instance.PlayEfect(Bolt522EffectAsset, boltPos, 4f);
            }
            // 519:固定只播放1次
            float visH19 = SpriteSequenceEffect.ScreenVisibleWorldHeight(spellCell.Position);
            if (visH19 <= 0f) visH19 = grid * 8f;
            Vector3 bolt19Pos = new Vector3(spellCell.Position.x, spellCell.Position.y + visH19 * Bolt519HeightRate, spellCell.Position.z);
            GameParticales.Instance.PlayEfect(Bolt519EffectAsset, bolt19Pos, 3f);
            // 521:固定只播放1次
            float visH23 = SpriteSequenceEffect.ScreenVisibleWorldHeight(spellCell.Position);
            if (visH23 <= 0f) visH23 = grid * 8f;
            float offset23Mid = grid * 0.45f;  // 中处:右偏移0.45格
            float offset23Up = grid * 0.25f;   // 上处:左偏移0.25格
            float[] bolt23Delays = { 2.405f, 2.305f, 2.205f };
            Vector3[] bolt23Pos =
            {
                    new Vector3(spellCell.Position.x,             spellCell.Position.y + visH23 * Bolt521HeightRates[0], spellCell.Position.z), // 下水平不变
                    new Vector3(spellCell.Position.x + offset23Mid, spellCell.Position.y + visH23 * Bolt521HeightRates[1], spellCell.Position.z), // 中右偏移0.45格
                    new Vector3(spellCell.Position.x - offset23Up, spellCell.Position.y + visH23 * Bolt521HeightRates[2], spellCell.Position.z)  // 上左偏移0.25格
                };
            for (int i = 0; i < Bolt521HeightRates.Length; i++)
            {
                GameObject g = GameParticales.Instance.PlayEfect(Bolt521EffectAsset, bolt23Pos[i], 6f);
                if (g != null)
                {
                    var seq = g.GetComponent<SpriteSequenceEffect>();
                    if (seq != null) seq.SetStartDelay(bolt23Delays[i]);
                }
            }
        }
    }


}
