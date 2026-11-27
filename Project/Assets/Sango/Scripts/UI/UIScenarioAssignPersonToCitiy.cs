using System.Collections.Generic;
using Sango.Core;
using UnityEngine.UI;

namespace Sango.UI
{

    public class UIScenarioAssignPersonToCitiy : UGUIWindow
    {
        public UIEditWorldMap uIEditWorldMap;
        public UIObjectDisplayPlane uiForceView;
        public List<SangoObject> sangoObjects = new List<SangoObject>();
        public Button sureButton;
        List<int> LastSel = new List<int>();

        /// <summary>
        /// 本次编辑是否已产生需要回写的结果。
        ///
        /// 【为什么需要】「确定」按钮的可用性不能用"是否有选中项"单独判断：
        /// 随机分配 / 手选分配完成后 <see cref="LastSel"/> 会被清空，若此时按选中项置灰确定键，
        /// 已分配的结果就再也保存不了。因此确定键 = 有选中项 || <see cref="hasChanged"/>。
        /// </summary>
        bool hasChanged;
        public ShortScenario scenario;
        public ShortScenario src_scenario;

        public List<ObjectSortTitle> SortList;
        public Text assignCountLabel;
        public Text noAssignCountLabel;

        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            hasChanged = false;
            sureButton.interactable = false;
            uIEditWorldMap = (UIEditWorldMap)objects[0];
            sangoObjects.Clear();
            LastSel.Clear();

            src_scenario = (ShortScenario)objects[1];
            scenario = src_scenario.Copy();

            uIEditWorldMap.SetScenario(scenario);
            SortList = new List<ObjectSortTitle>
            {
                    PersonLibSortFunction.SortByName,
                    PersonLibSortFunction.SortByBelongForce(scenario),
                    PersonLibSortFunction.SortByBelongCity(scenario),
                    PersonLibSortFunction.SortByYearBorn,
                    PersonLibSortFunction.SortByYearDead,
                    PersonLibSortFunction.SortBySex ,
                    PersonLibSortFunction.SortByCommand,
                    PersonLibSortFunction.SortByStrength,
                    PersonLibSortFunction.SortByIntelligence,
                    PersonLibSortFunction.SortByPolitics,
                    PersonLibSortFunction.SortByGlamour,
            };

            scenario.personSet.ForEach(person =>
            {
                // 指派了,但是不是主公,都可以重新指派
                if (person.PersonLib != null && person.state != (int)PersonStateType.Governor)
                {
                    sangoObjects.Add(person.PersonLib);
                }
            });
            uiForceView.Init(sangoObjects, SortList);
            uiForceView.OnMultiSelectCall = OnMultiSelectCall;
            RefreshAssignCounts();
        }

        /// <summary>
        /// 刷新"已配置 / 未配置"武将数量。
        ///
        /// 统计口径与列表保持一致：只看 <see cref="sangoObjects"/> 里可重新指派的武将
        /// （已排除君主），配置与否按 <see cref="ShortPerson.BelongCityId"/> 是否为 0 判断。
        /// 不能用 state 判断：该字段不参与序列化，读档后不可靠。
        /// </summary>
        void RefreshAssignCounts()
        {
            int assigned = 0;
            int unassigned = 0;

            if (sangoObjects != null && scenario != null)
            {
                for (int i = 0; i < sangoObjects.Count; i++)
                {
                    PersonLib personLib = sangoObjects[i] as PersonLib;
                    if (personLib == null)
                        continue;

                    if (personLib.GetBelongCityId(scenario) != 0)
                        assigned++;
                    else
                        unassigned++;
                }
            }

            if (assignCountLabel != null)
                assignCountLabel.text = assigned.ToString();
            if (noAssignCountLabel != null)
                noAssignCountLabel.text = unassigned.ToString();
        }

        public void OnMultiSelectCall(List<int> index)
        {
            LastSel.Clear();
            LastSel.AddRange(index);
            if (LastSel.Count > 0)
            {
                sureButton.interactable = true;
                uIEditWorldMap.SetSelectAllCity(null);
                uIEditWorldMap.OnSelectCity = OnSelectCity;
                uIEditWorldMap.maxSelectCount = 1;
            }
            else
            {
                // 取消选中不等于"没有改动"：随机分配 / 手选分配过的结果仍要能保存
                sureButton.interactable = hasChanged;
                uIEditWorldMap.RefreshCity();
                uIEditWorldMap.OnSelectCity = null;
            }
        }

        public void OnSelectCity(List<ShortCity> shortCities)
        {
            ShortCity city = shortCities[0];
            if (city != null)
            {
                if (city.BelongForceId == 0)
                {
                    for (int i = 0; i < LastSel.Count; i++)
                    {
                        PersonLib sangoObject = sangoObjects[LastSel[i]] as PersonLib;
                        if (sangoObject != null)
                        {
                            ShortPerson person = scenario.personSet[sangoObject.targetShortPersonId];
                            person.BelongForceId = city.BelongForceId;
                            person.BelongCityId = city.Id;
                            person.state = (int)PersonStateType.Unemployed;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < LastSel.Count; i++)
                    {
                        PersonLib sangoObject = sangoObjects[LastSel[i]] as PersonLib;
                        if (sangoObject != null)
                        {
                            ShortPerson person = scenario.personSet[sangoObject.targetShortPersonId];
                            person.BelongForceId = city.BelongForceId;
                            person.BelongCityId = city.Id;
                            person.state = (int)PersonStateType.Normal;
                        }
                    }
                }
                hasChanged = true;
                sureButton.interactable = true;
                LastSel.Clear();
                uiForceView.Init(sangoObjects, SortList);
                RefreshAssignCounts();
            }
            uIEditWorldMap.RefreshCity();
        }

        public void OnSure()
        {
            hasChanged = false;
            src_scenario.personSet = scenario.personSet;
            src_scenario.citySet = scenario.citySet;
            src_scenario.forceSet = scenario.forceSet;
            Close();
        }

        /// <summary>
        /// 一键全选：把当前列表里的所有武将一次性加入待指派队列。
        ///
        /// 选中状态由 <see cref="UIObjectDisplayPlane"/> 统一维护 —— 列表会按表头排序按钮重排，
        /// 但它的索引与 <see cref="sangoObjects"/> 指向同一个 List，所以索引不会错位。
        /// 全选后 <see cref="UIObjectDisplayPlane"/> 会回调 <see cref="OnMultiSelectCall"/>，
        /// 由它清空并重建 <see cref="LastSel"/>、点亮确定键、把世界地图切到"选 1 座城"的模式。
        /// </summary>
        public void SelectAll()
        {
            if (uiForceView == null)
                return;

            // 列表为空时全选没有意义，直接返回避免点亮确定键后无处指派
            if (sangoObjects == null || sangoObjects.Count == 0)
                return;

            uiForceView.SelectAll();
        }

        /// <summary>
        /// 一键取消：清空当前全部选中（对应 prefab 上"一并取消"按钮 button_unsel_all）。
        ///
        /// 选中状态由 <see cref="UIObjectDisplayPlane"/> 统一维护，清空后会回调
        /// <see cref="OnMultiSelectCall"/> 并传入**空列表**；该方法的空列表分支负责：
        /// 清空 <see cref="LastSel"/>、按 <see cref="hasChanged"/> 恢复确定键的可用性、
        /// 刷新世界地图、解除城市选择回调，因此这里不需要重复处理这些状态。
        /// </summary>
        public void UnSelectAll()
        {
            if (uiForceView == null)
                return;

            uiForceView.UnSelectAll();
        }

        /// <summary>
        /// 随机分配（全部未分配武将）。具体规则见 <see cref="RandomAssignInternal"/>。
        /// </summary>
        public void RandomAssign()
        {
            RandomAssignInternal(0);
        }

        /// <summary>随机分配 10 名未分配武将（不足 10 人时按实际人数，不报错）</summary>
        public void RandomAssign10()
        {
            RandomAssignInternal(10);
        }

        /// <summary>随机分配 20 名未分配武将（不足 20 人时按实际人数，不报错）</summary>
        public void RandomAssign20()
        {
            RandomAssignInternal(20);
        }

        /// <summary>随机分配 50 名未分配武将（不足 50 人时按实际人数，不报错）</summary>
        public void RandomAssign50()
        {
            RandomAssignInternal(50);
        }

        /// <summary>随机分配 100 名未分配武将（不足 100 人时按实际人数，不报错）</summary>
        public void RandomAssign100()
        {
            RandomAssignInternal(100);
        }

        /// <summary>
        /// 随机分配的主体：把尚未分配城池的武将均匀随机地分进各座城池。
        ///
        /// 【"未分配"的判定口径】只认 <see cref="ShortPerson.BelongCityId"/> == 0，与
        /// <see cref="UIScenarioSelectGovernorAndCity"/> 选君主时的判断一致。
        /// 不能用 <see cref="ShortPerson.state"/> 判：该字段没有 [JsonProperty]，不参与存档，
        /// 读档后一律是 0(Error)，拿它过滤会把所有人都当成"未初始化"。
        ///
        /// 【目标城池】优先只用有势力归属的城（BelongForceId != 0），分配后 state = Normal；
        /// 若一座有主城都没有，退回全部城池，并按 <see cref="OnSelectCity"/> 的空城分支
        /// 记为在野(Unemployed)，保证任何剧本状态下都能做完这次分配。
        ///
        /// 【为什么要洗牌 + 轮转】先洗牌决定"先分谁"，再按 i % 城数轮转，各城人数最多差 1；
        /// 若对每名武将独立随机抽城，会出现"某座城几十人、隔壁城没人"。
        /// </summary>
        /// <param name="count">分配人数上限；&lt;= 0 表示分配全部未分配武将</param>
        void RandomAssignInternal(int count)
        {
            if (scenario == null || scenario.personSet == null || scenario.citySet == null
                || scenario.forceSet == null)
                return;

            // ---------- ① 收集未分配武将 ----------
            // 君主必须留在自己的都城：ShortPerson.state 不序列化（读档后全是 0），
            // 不能靠 state 识别君主，改为按 forceSet 里的 Governor 反查，避免把君主
            // 当普通武将随机分走、把主君身份改成 Normal。
            HashSet<int> governorIds = new HashSet<int>();
            scenario.forceSet.ForEach(force =>
            {
                if (force != null && force.Governor > 0)
                    governorIds.Add(force.Governor);
            });

            List<ShortPerson> unassigned = new List<ShortPerson>();
            scenario.personSet.ForEach(person =>
            {
                // PersonLib 为空的是临时 / 占位对象，不在"可指派武将"范围内
                if (person == null || person.PersonLib == null) return;
                if (governorIds.Contains(person.Id)) return;
                // 已经有户口城的不动，避免把玩家手工排好的结果冲掉
                if (person.BelongCityId != 0) return;
                unassigned.Add(person);
            });

            if (unassigned.Count == 0)
            {
                Sango.Log.Info("随机分配：没有未分配的武将");
                return;
            }

            // ---------- ② 收集目标城池 ----------
            List<ShortCity> targets = new List<ShortCity>();
            scenario.citySet.ForEach(city =>
            {
                // 只分到城市 / 关港（IsCity 判定 BuildingType 1 与 65），排除一般建筑
                if (city == null || !city.IsCity()) return;
                if (city.BelongForceId > 0) targets.Add(city);
            });

            bool ownedOnly = targets.Count > 0;
            if (!ownedOnly)
            {
                // 剧本还没建任何势力：退回全部城池，按"在野"归属处理
                scenario.citySet.ForEach(city =>
                {
                    if (city != null && city.IsCity()) targets.Add(city);
                });
            }

            if (targets.Count == 0)
            {
                Sango.Log.Warning("随机分配：剧本中没有任何城池，无法分配");
                return;
            }

            // ---------- ③ 洗牌（Fisher-Yates，复用项目统一随机源） ----------
            for (int i = unassigned.Count - 1; i > 0; i--)
            {
                int j = GameRandom.Range(0, i + 1);
                ShortPerson tmp = unassigned[i];
                unassigned[i] = unassigned[j];
                unassigned[j] = tmp;
            }

            // ---------- ④ 轮转分配 ----------
            // count <= 0 表示全部；count > 0 时最多分 count 人。
            // 洗牌已把整个未分配列表打乱，取前 assignCount 个即是"随机抽 N 人"。
            int assignCount = count > 0 ? System.Math.Min(count, unassigned.Count) : unassigned.Count;
            for (int i = 0; i < assignCount; i++)
            {
                ShortCity city = targets[i % targets.Count];
                ShortPerson person = unassigned[i];

                person.BelongForceId = city.BelongForceId;
                person.BelongCityId = city.Id;
                // 与 OnSelectCity 的两个分支保持同一口径：有主城 = 一般武将，空城 = 在野
                person.state = city.BelongForceId > 0
                    ? (int)PersonStateType.Normal
                    : (int)PersonStateType.Unemployed;
            }

            // 日志同时给出"实际分配 / 可分配总数"，配置上限大于剩余人数时一眼可见
            Sango.Log.Info($"随机分配：{assignCount}/{unassigned.Count} 名未分配武将 → {targets.Count} 座{(ownedOnly ? "有主" : "空")}城池");

            // ---------- ⑤ 刷新界面 ----------
            // UIObjectDisplayPlane.Init 只清内部多选列表、不会回调 OnMultiSelectCall，
            // 所以 LastSel 必须在这里自己清空、确定键按改动状态自行设置。
            // 随机分配完成即产生待保存结果：确定键必须可点，否则分配白做。
            hasChanged = true;
            LastSel.Clear();
            uiForceView.Init(sangoObjects, SortList);
            sureButton.interactable = true;
            uIEditWorldMap.RefreshCity();
            RefreshAssignCounts();
        }
    }
}
