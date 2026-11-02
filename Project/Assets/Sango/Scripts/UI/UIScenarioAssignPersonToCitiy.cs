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
        public ShortScenario scenario;
        public ShortScenario src_scenario;

        public List<ObjectSortTitle> SortList;


        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
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
                sureButton.interactable = false;
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
                sureButton.interactable = true;
                LastSel.Clear();
                uiForceView.Init(sangoObjects, SortList);
            }
            uIEditWorldMap.RefreshCity();
        }

        public void OnSure()
        {
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
        /// 清空 <see cref="LastSel"/>、把确定键置灰、刷新世界地图、解除城市选择回调，
        /// 因此这里不需要重复处理这些状态。
        /// </summary>
        public void UnSelectAll()
        {
            if (uiForceView == null)
                return;

            uiForceView.UnSelectAll();
        }
    }
}
