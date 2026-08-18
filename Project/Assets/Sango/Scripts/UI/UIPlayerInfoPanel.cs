using Sango.Core.Player;
using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    public class UIPlayerInfoPanel : UGUIWindow
    {
        public UIForceElementItem governorItem;
        public UIForceElementItem[] uIForceElementItems;

        public GameObject[] showTypeSelectObjs;

        public GameObject maxBtn;
        public GameObject miniBtn;
        public GameObject longBtn;
        public GameObject shortBtn;

        public RectTransform itemRoot;
        public RectTransform sliderRoot;

        public Scrollbar scrollbar;

        SangoObject governorObj;
        List<SangoObject> curDataList = new List<SangoObject>();
        int startIndex = 0;

        private void Start()
        {
            OnDestroy();
            GameEvent.OnTroopCreated += OnTroopCreated;
            GameEvent.OnTroopDestroyed += OnTroopDestroyed;
            GameEvent.OnCityFall += OnCityFall;
            GameEvent.OnTroopActionOver += OnTroopActionOver;
            GameEvent.OnPersonActionOver += OnPersonActionOver;
            
        }

        protected override void OnDestroy()
        {
            GameEvent.OnTroopCreated -= OnTroopCreated;
            GameEvent.OnTroopDestroyed -= OnTroopDestroyed;
            GameEvent.OnCityFall -= OnCityFall;
            GameEvent.OnTroopActionOver -= OnTroopActionOver;
            GameEvent.OnPersonActionOver -= OnPersonActionOver;
            
        }

        void OnPersonActionOver(Person person)
        {
            if ((curShowType == ShowType.Troop || curShowType == ShowType.Person) && person.mBelongForce != null && person.IsPlayerControl && person.mBelongForce == Scenario.Cur.CurRunForce)
            {
                UpdateShowType();
            }
        }

        void OnTroopActionOver(Troop troop)
        {
            if (troop == null)
                return;

            if ((curShowType == ShowType.Troop || curShowType == ShowType.Person) && troop.mBelongForce != null && troop.IsPlayerControl && troop.mBelongForce == Scenario.Cur.CurRunForce)
            {
                UpdateShowType();
            }
        }


        void OnCityFall(City city, Force lastForce, Troop atk)
        {
            if (curShowType == ShowType.City && city.IsPlayerControl && city.mBelongForce == Scenario.Cur.CurRunForce)
            {
                UpdateShowType();
            }
        }

        void OnTroopCreated(Troop troop, Scenario scenario)
        {
            if (curShowType == ShowType.Troop && troop.IsPlayerControl && troop.mBelongForce == scenario.CurRunForce)
            {
                UpdateShowType();
            }
        }

        void OnTroopDestroyed(Troop troop, Scenario scenario)
        {
            if (curShowType == ShowType.Troop && troop.IsPlayerControl && troop.mBelongForce == scenario.CurRunForce)
            {
                UpdateShowType();
            }
        }

        public enum ShowType
        {
            City = 0,
            Person,
            Troop
        }

        ShowType curShowType = ShowType.City;

        public void UpdateShowType()
        {
            ChangeShowType(curShowType, true);
        }
        List<Troop> sorted_list_Troop = new List<Troop>();
        List<City> sorted_list_City = new List<City>();
        List<Person> sorted_list_Person = new List<Person>();

        public void ChangeShowType(ShowType showType, bool forceUpdate = false)
        {
            if (!forceUpdate && showType == curShowType)
                return;
            curShowType = showType;

            for (int i = 0; i < showTypeSelectObjs.Length; i++)
                showTypeSelectObjs[i].SetActive(i == (int)showType);

            Force force = Scenario.Cur.CurRunForce;
            curDataList.Clear();
            startIndex = 0;

            switch (curShowType)
            {
                case ShowType.City:
                    {
                        sorted_list_City.Clear();
                        governorObj = force.CapitalCity;
                        force.ForEachCityBase(obj =>
                        {
                            if (governorObj != obj && obj.mBelongCorps.IsPlayerControl)
                                sorted_list_City.Add(obj);
                        });

                        sorted_list_City.Sort((a, b) =>
                        {
                            int aKind = a.BuildingType.kind;
                            int bKind = b.BuildingType.kind;
                            if (aKind == bKind)
                            {
                                return -a.FreePersonCount.CompareTo(b.FreePersonCount);
                            }
                            else
                                return a.BuildingType.kind.CompareTo(b.BuildingType.kind);
                        }

                        );
                        curDataList.AddRange(sorted_list_City);
                    }
                    break;
                case ShowType.Person:
                    {
                        sorted_list_Person.Clear();
                        governorObj = force.mGovernor;
                        force.ForEachPerson(obj =>
                        {
                            if (governorObj != obj && obj.mBelongCorps.IsPlayerControl)
                                sorted_list_Person.Add(obj);
                        });
                        sorted_list_Person.Sort((a, b) =>
                        {
                            bool action_over_a = a.mTroop != null ? a.mTroop.ActionOver : a.ActionOver;
                            bool action_over_b = b.mTroop != null ? b.mTroop.ActionOver : b.ActionOver;

                            if (action_over_a == action_over_b)
                                return a.Name.CompareTo(b.Name);
                            else
                                return action_over_a.CompareTo(action_over_b);

                        });
                        curDataList.AddRange(sorted_list_Person);
                    }
                    break;
                case ShowType.Troop:
                    {
                        sorted_list_Troop.Clear();
                        governorObj = force.mGovernor.mTroop;
                        force.ForEachTroop(obj =>
                        {
                            if (governorObj != obj && obj.mBelongCorps.IsPlayerControl && !string.IsNullOrEmpty(obj.Name))
                                sorted_list_Troop.Add(obj);
                        });
                        sorted_list_Troop.Sort((a, b) =>
                        {
                            if (a.ActionOver == b.ActionOver)
                                return a.Name.CompareTo(b.Name);
                            else
                                return a.ActionOver.CompareTo(b.ActionOver);
                        });
                        curDataList.AddRange(sorted_list_Troop);
                    }
                    break;
            }

            governorItem.SetSangoObject(governorObj);

            if (curDataList.Count < uIForceElementItems.Length)
            {
                sliderRoot.gameObject.SetActive(false);
                Vector2 size = itemRoot.sizeDelta;
                size.x = 150;
                itemRoot.sizeDelta = size;

                for (int i = 0; i < uIForceElementItems.Length; i++)
                {
                    UIForceElementItem uIForceElement = uIForceElementItems[i];
                    if (i < curDataList.Count)
                    {
                        uIForceElement.SetSangoObject(curDataList[i]);
                    }
                    else
                    {
                        uIForceElement.SetSangoObject(null);
                    }
                }
            }
            else
            {
                sliderRoot.gameObject.SetActive(true);
                Vector2 size = itemRoot.sizeDelta;
                size.x = 132;
                itemRoot.sizeDelta = size;

                scrollbar.size = (float)uIForceElementItems.Length / (float)curDataList.Count;
                scrollbar.SetValueWithoutNotify(0);
                OnScrollBarValueChange(0);
            }
        }

        public void ShowCities()
        {
            ChangeShowType(ShowType.City);
        }

        public void ShowPersons()
        {
            ChangeShowType(ShowType.Person);
        }

        public void ShowTroops()
        {
            ChangeShowType(ShowType.Troop);
        }


        public void MaxSize()
        {

        }

        public void MiniSize()
        {

        }

        public void LongSize() { }
        public void ShortSize() { }

        public void UpShow()
        {
            if (startIndex > 0)
                startIndex--;
            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (curDataList.Count - uIForceElementItems.Length));
        }

        public void DownShow()
        {
            if (startIndex < curDataList.Count - uIForceElementItems.Length)
                startIndex++;
            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (curDataList.Count - uIForceElementItems.Length));
        }

        public void OnScrollBarValueChange(float value)
        {
            startIndex = (int)UnityEngine.Mathf.Lerp(0, curDataList.Count - uIForceElementItems.Length, value);
            UpdateItemStartIndex(startIndex);
        }
        public void UpdateItemStartIndex(int startIndex)
        {
            for (int i = 0; i < uIForceElementItems.Length; i++)
            {
                SangoObject sango = curDataList[i + startIndex];
                UIForceElementItem uIForceElement = uIForceElementItems[i];
                uIForceElement.SetSangoObject(sango);
            }
        }
    }
}
