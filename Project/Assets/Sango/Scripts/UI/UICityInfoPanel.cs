using Sango.Core.Player;
using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityInfoPanel : UGUIWindow
    {
        public RectTransform root;
        public Text name;
        public Image[] icon_special;
        private int delayOneFrame = 1;

        public RectTransform line;
        List<RectTransform> line_pool = new List<RectTransform>();
        List<RectTransform> line_used = new List<RectTransform>();

        public UITextField one;
        List<UITextField> oneFieldList_pool = new List<UITextField>();
        List<UITextField> oneFieldList_used = new List<UITextField>();

        public UIDoubleTextField two;
        List<UIDoubleTextField> twoFieldList_pool = new List<UIDoubleTextField>();
        List<UIDoubleTextField> twoFieldList_used = new List<UIDoubleTextField>();

        List<ObjectSortTitle> objectSortTitles;

        public override void OnOpen(params object[] objects)
        {
            base.OnOpen();
            City city = (City)objects[0];
            city.CalculateHarvest();
            ResetPool();
            objectSortTitles = new List<ObjectSortTitle>()
            {
                CitySortFunction.SortByGold.Copy().SetAlignment((int)TextAnchor.MiddleRight),
                CitySortFunction.SortByFood.Copy().SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(0),
                CitySortFunction.SortBySecurity_SecurityLimit.Copy().SetAlignment((int)TextAnchor.MiddleRight),
                CitySortFunction.SortByDurability_DurabilityLimit.Copy().SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(0),

                CitySortFunction.SortByTroops.Copy().SetAlignment((int)TextAnchor.MiddleRight),
                CitySortFunction.SortByMorale_MoraleLimit.Copy().SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(0),

                CitySortFunction.GetSortByItemId(2).SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(CitySortFunction.GetSortByItemId(6).SetAlignment((int)TextAnchor.MiddleRight)),
                CitySortFunction.GetSortByItemId(3).SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(CitySortFunction.GetSortByItemId(8).SetAlignment((int)TextAnchor.MiddleRight)),
                CitySortFunction.GetSortByItemId(4).SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(CitySortFunction.GetSortByItemId(11).SetAlignment((int)TextAnchor.MiddleRight)),
                CitySortFunction.GetSortByItemId(5).SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(1),

                CitySortFunction.SortByTotalGainGold.Copy().SetAlignment((int)TextAnchor.MiddleRight),
                CitySortFunction.SortByTotalGainFood.Copy().SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(0),

                CitySortFunction.SortByHasBusiness.Copy().SetAlignment((int)TextAnchor.MiddleRight).SetCustomData(0),

                CitySortFunction.SortByAllPersonCountInfo.Copy().SetAlignment((int)TextAnchor.MiddleRight),
                CitySortFunction.SortByCaptiveCount.Copy().SetAlignment((int)TextAnchor.MiddleRight),
                CitySortFunction.SortByWildCount.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            };

            float dep = (float)Screen.width / Screen.height;
            Vector2 pos = Camera.main.WorldToScreenPoint(city.CenterCell.Position);
            bool showLeft = pos.x > (Screen.width / 2 - 100);
            pos = root.anchoredPosition;
            if (showLeft)
                pos.x = -(dep * 1080f - root.sizeDelta.x) / 2;
            else
                pos.x = (dep * 1080f - root.sizeDelta.x) / 2;
            root.anchoredPosition = pos;

            name.text = city.Name;

            GameEvent.OnInitCityInfoPanel?.Invoke(city, objectSortTitles);
            for (int i = 0; i < objectSortTitles.Count; i++)
            {
                ObjectSortTitle title = objectSortTitles[i];

                if (title.customData == null)
                {
                    AddInfo_One(title.name, title.GetValueStr(city), title.alignment);
                }
                else
                {
                    ObjectSortTitle second = title.customData as ObjectSortTitle;
                    // customData 为 ObjectSortTitle
                    if (second != null)
                    {
                        AddInfo_Two(title.name, title.GetValueStr(city),
                            second.name, second.GetValueStr(city),
                            title.alignment);

                        if (second.customData != null)
                        {
                            AddLine();
                        }
                    }
                    else
                    {
                        int lineType = (int)title.customData;

                        // customData 为 0
                        if (lineType == 0)
                        {
                            AddInfo_One(title.name, title.GetValueStr(city), title.alignment);
                        }
                        else
                        {
                            // customData 为 1
                            AddInfo_Two(title.name, title.GetValueStr(city), null, null, title.alignment);
                        }
                        AddLine();
                    }
                }

            }
            delayOneFrame = 1;
        }
        void Update()
        {
            if (delayOneFrame > 0)
            {
                delayOneFrame--;
            }
            else if (delayOneFrame == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
            }
        }

        public override void OnClose()
        {
            base.OnClose();
        }

        protected void ResetPool()
        {
            oneFieldList_pool.AddRange(oneFieldList_used);
            foreach (var v in oneFieldList_pool)
                v.gameObject.SetActive(false);
            oneFieldList_used.Clear();

            twoFieldList_pool.AddRange(twoFieldList_used);
            foreach (var v in twoFieldList_pool)
                v.gameObject.SetActive(false);
            twoFieldList_used.Clear();

            line_pool.AddRange(line_used);
            foreach (var v in line_pool)
                v.gameObject.SetActive(false);
            line_used.Clear();
        }

        RectTransform Create_Line()
        {
            RectTransform lineRs;
            if (line_pool.Count > 0)
            {
                lineRs = line_pool[0];
                line_pool.RemoveAt(0);
                line_used.Add(lineRs);
            }
            else
            {
                lineRs = GameObject.Instantiate(line.gameObject, line.parent).GetComponent<RectTransform>();
                line_used.Add(lineRs);
            }
            lineRs.gameObject.SetActive(true);
            return lineRs;
        }

        UITextField Create_OneField()
        {
            UITextField textField;
            if (oneFieldList_pool.Count > 0)
            {
                textField = oneFieldList_pool[0];
                oneFieldList_pool.RemoveAt(0);
                oneFieldList_used.Add(textField);
            }
            else
            {
                textField = GameObject.Instantiate(one.gameObject, one.transform.parent).GetComponent<UITextField>();
                oneFieldList_used.Add(textField);
            }
            textField.gameObject.SetActive(true);
            return textField;
        }

        UIDoubleTextField Create_TwoField()
        {
            UIDoubleTextField textField;
            if (twoFieldList_pool.Count > 0)
            {
                textField = twoFieldList_pool[0];
                twoFieldList_pool.RemoveAt(0);
                twoFieldList_used.Add(textField);
            }
            else
            {
                textField = GameObject.Instantiate(two.gameObject, two.transform.parent).GetComponent<UIDoubleTextField>();
                twoFieldList_used.Add(textField);
            }
            textField.gameObject.SetActive(true);
            return textField;
        }

        public void AddLine()
        {
            Create_Line();
        }

        public void AddInfo_One(string title, string content)
        {
            UITextField textField = Create_OneField();
            textField.text = content;
            textField.titleLabel.text = title;
            textField.label.alignment = TextAnchor.MiddleCenter;
        }

        public void AddInfo_One(string title, string content, int alignment)
        {
            UITextField textField = Create_OneField();
            textField.text = content;
            textField.titleLabel.text = title;
            textField.label.alignment = (TextAnchor)alignment;
        }

        public void AddInfo_Two(string title, string content, string title2, string content2)
        {
            UIDoubleTextField textField = Create_TwoField();
            textField.field1.text = content;
            textField.field1.titleLabel.text = title;
            textField.field1.label.alignment = TextAnchor.MiddleCenter;

            if (!string.IsNullOrEmpty(title2))
            {
                textField.field2.gameObject.SetActive(true);
                textField.field2.text = content2;
                textField.field2.titleLabel.text = title2;
                textField.field2.label.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                textField.field2.gameObject.SetActive(false);
            }

        }

        public void AddInfo_Two(string title, string content, string title2, string content2, int alignment)
        {
            UIDoubleTextField textField = Create_TwoField();
            textField.field1.text = content;
            textField.field1.titleLabel.text = title;
            textField.field1.label.alignment = (TextAnchor)alignment;

            if (!string.IsNullOrEmpty(title2))
            {
                textField.field2.gameObject.SetActive(true);
                textField.field2.text = content2;
                textField.field2.titleLabel.text = title2;
                textField.field2.label.alignment = (TextAnchor)alignment;
            }
            else
            {
                textField.field2.gameObject.SetActive(false);
            }
        }

    }
}
