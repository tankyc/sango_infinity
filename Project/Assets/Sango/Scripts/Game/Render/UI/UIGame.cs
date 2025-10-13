using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Game.Render.UI
{
    public class UIGame : UGUIWindow, LoopScrollPrefabSource, LoopScrollDataSource
    {
        public Text forceText;
        public Text dateText;

        public bool gridShow = true;
        public bool troopListShow = false;
        public GameObject troopListObj;
        public Text troopListShowText;

        public LoopScrollRect loopScrollRect;
        public GameObject troopListItemObj;
        public Transform troopListContent;
        public int totalCount = -1;
        Stack<Transform> pool = new Stack<Transform>();
        //List<Troop> troops_list = new List<Troop>();
        List<SangoObject> item_list = new List<SangoObject>();
        public Type itemType;
        bool needUpdateItem = true;

        public GameObject pauseObj;
        public GameObject resumeObj;

        public GameObject GetObject(int index)
        {
            if (pool.Count == 0)
            {
                GameObject obj = Instantiate(troopListItemObj);
                UITroopListItem uITroopListItem = obj.GetComponent<UITroopListItem>();
                if (uITroopListItem != null)
                {
                    uITroopListItem.onSelected = OnTroopListSelected;
                    uITroopListItem.onShow = OnTroopListShow;
                }
                return obj;
            }
            Transform candidate = pool.Pop();
            candidate.gameObject.SetActive(true);
            return candidate.gameObject;
        }

        public void ReturnObject(Transform trans)
        {
            // Use `DestroyImmediate` here if you don't need Pool
            trans.SendMessage("ScrollCellReturn", SendMessageOptions.DontRequireReceiver);
            trans.gameObject.SetActive(false);
            trans.SetParent(transform, false);
            pool.Push(trans);
        }

        public void ProvideData(Transform transform, int idx)
        {
            transform.SendMessage("ScrollCellIndex", idx);
        }

        void Start()
        {
            Scenario.Cur.Event.OnTroopCreated += OnTroopChange;
            Scenario.Cur.Event.OnTroopDestroyed += OnTroopChange;
            Scenario.Cur.Event.OnForceStart += OnForceStart;
            Scenario.Cur.Event.OnDayUpdate += OnDayUpdate;
            Scenario.Cur.Event.OnCityFall += OnCityFall;

            loopScrollRect.prefabSource = this;
            loopScrollRect.dataSource = this;

            itemType = typeof(Troop);
            needUpdateItem = true;

            //loopScrollRect.totalCount = totalCount;
            //loopScrollRect.RefillCells();
        }

        public void OnCityFall(City city, Troop atker, Scenario scenario)
        {
            if (itemType == typeof(City))
            {
                loopScrollRect.RefreshCells();
            }

        }

        public void OnTroopChange(Troop troop, Scenario scenario)
        {
            if (itemType == typeof(Troop))
            {
                needUpdateItem = true;
            }

        }

        public void OnForceStart(Force force, Scenario scenario)
        {
            forceText.text = force.Name;
        }

        public void OnDayUpdate(Scenario scenario)
        {
            dateText.text = scenario.GetDateStr();
        }

        public void OnBtnPause()
        {
            pauseObj.SetActive(false);
            resumeObj.SetActive(true);
            Sango.Game.Scenario.Pause();
        }

        public void OnBtnResume()
        {
            pauseObj.SetActive(true);
            resumeObj.SetActive(false);
            Sango.Game.Scenario.Resume();
        }


        public void OnBtnNextForce()
        {
            pauseObj.SetActive(false);
            resumeObj.SetActive(true);
            Sango.Game.Scenario.NextForce();
        }

        public void OnBtnNextTurn()
        {
            pauseObj.SetActive(false);
            resumeObj.SetActive(true);
            Sango.Game.Scenario.NextTurn();
        }

        public void OnBtnDebugAI()
        {

        }

        public void OnBtnGirdShow()
        {
            gridShow = !gridShow;
            MapRender.Instance.ShowGrid(gridShow);
        }

        public void OnTroopListShow()
        {
            troopListShow = !troopListShow;
            troopListObj.SetActive(troopListShow);
            troopListShowText.text = troopListShow ? "隐藏" : "显示";
        }

        public void OnTroopListSelected(int index)
        {
            if (index < 0 || index >= item_list.Count)
                return;

            SangoObject obj = item_list[index];
            if (obj is Troop)
            {
                Troop troop = (Troop)obj;
                Vector3 position = troop.cell.Position;
                MapRender.Instance.MoveCameraTo(position);
            }
            else if (obj is City)
            {
                City troop = (City)obj;
                Vector3 position = troop.CenterCell.Position;
                MapRender.Instance.MoveCameraTo(position);
            }
        }

        public void OnTroopListShow(UITroopListItem item)
        {
            if (item.index < 0 || item.index >= item_list.Count)
            {
                item.name.text = "无效";
                return;
            }
            SangoObject obj = item_list[item.index];
            if (obj is Troop)
            {
                Troop troop = (Troop)obj;
                item.name.text = $"[{troop.BelongForce.Name}]<{troop.BelongCity.Name}>{troop.Name}队";
                item.name.color = troop.BelongForce.Flag.color;
            }
            else if (obj is City)
            {
                City city = (City)obj;
                if (city.BelongForce != null)
                {
                    item.name.text = $"[{city.BelongForce.Name}]{city.Name}";
                    item.name.color = city.BelongForce.Flag.color;

                }
                else
                {
                    item.name.text = $"{city.Name}";
                    item.name.color = Color.white;
                }

            }
        }

        public void OnTroopTab(Toggle b)
        {
            if (b.isOn)
            {
                itemType = typeof(Troop);
                needUpdateItem = true;
            }
        }

        public void OnCityTab(Toggle b)
        {
            if (b.isOn)
            {
                itemType = typeof(City);
                needUpdateItem = true;
            }
        }

        public void Update()
        {
            if (needUpdateItem)
            {
                needUpdateItem = false;
                if (itemType == typeof(Troop))
                {

                    item_list.Clear();
                    Scenario.Cur.troopsSet.ForEach(t =>
                    {
                        if (t.IsAlive)
                        {
                            item_list.Add(t);
                        }
                    });

                    loopScrollRect.totalCount = item_list.Count;
                    loopScrollRect.RefillCells(loopScrollRect.GetFirstItem(out _));
                }
                else if (itemType == typeof(City))
                {

                    item_list.Clear();
                    Scenario.Cur.citySet.ForEach(t =>
                    {
                        if (t.IsAlive)
                        {
                            item_list.Add(t);
                        }
                    });

                    loopScrollRect.totalCount = item_list.Count;
                    loopScrollRect.RefillCells(loopScrollRect.GetFirstItem(out _));
                }
            }
        }
    }
}
