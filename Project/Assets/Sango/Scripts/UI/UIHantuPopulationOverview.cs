using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>原生 UGUI 人口总表；按可见行复用对象，城关港数量不影响节点数量。</summary>
    public sealed class UIHantuPopulationOverview : MonoBehaviour
    {
        const float RowHeight = 44f;
        static readonly Color Ink = new Color(.90f, .88f, .77f);
        static readonly Color Gold = new Color(.78f, .68f, .42f);
        static readonly Color Surface = new Color(.11f, .13f, .12f, .99f);
        static readonly string[] Filters = { "全图", "城池", "关隘", "港口" };
        static readonly float[] Columns = { 0, .34f, .56f, .78f, 1 };

        sealed class Row
        {
            public RectTransform rect;
            public Image background;
            public Text[] labels;
            public int index;
        }

        Scenario scenario;
        Action onClose;
        Font font;
        int filter;
        bool sortByPopulation = true;
        City selected;
        readonly List<City> cities = new List<City>();
        readonly List<Row> rows = new List<Row>();
        readonly List<Button> filterButtons = new List<Button>();
        Text summary, detailTitle, detailText, chartCaption;
        Button sortButton;
        RectTransform listViewport, listContent, detailContent;
        ScrollRect listScroll, detailScroll;
        HantuPopulationTrendGraphic chart;
        Vector2 previousViewportSize;
        bool closing;

        public static UIHantuPopulationOverview Create(RectTransform parent, Scenario scenario, Action onClose)
        {
            GameObject go = new GameObject("HantuPopulationOverview", typeof(RectTransform));
            try
            {
                go.transform.SetParent(parent, false);
                go.layer = parent.gameObject.layer;
                var window = go.AddComponent<UIHantuPopulationOverview>();
                window.scenario = scenario;
                window.onClose = onClose;
                window.Build();
                window.Refresh();
                return window;
            }
            catch
            {
                go.SetActive(false);
                Destroy(go);
                throw;
            }
        }

        void Build()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(root);
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 360;
            gameObject.AddComponent<GraphicRaycaster>();
            font = Resources.Load<Font>("siyuancn");
            if (font == null)
            {
                Text existing = Game.Instance.UIRoot.GetComponentInChildren<Text>(true);
                if (existing != null) font = existing.font;
            }
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .48f);

            RectTransform panel = Box(root, "Panel", Surface, new Vector2(.025f, .04f), new Vector2(.975f, .96f));
            Label(panel, "Title", "人口总览", 24, Gold, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18, -48), new Vector2(-150, -10));
            Button close = ButtonAt(panel, "Close", "返回", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-128, -46), new Vector2(-14, -10), Close);
            close.GetComponent<Image>().color = new Color(.30f, .27f, .18f);
            summary = Label(panel, "Summary", "", 17, Ink, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18, -107), new Vector2(-18, -52));
            for (int i = 0; i < Filters.Length; i++)
            {
                int index = i;
                filterButtons.Add(ButtonAt(panel, "Filter" + i, Filters[i], new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(18 + i * 78, -146), new Vector2(90 + i * 78, -112), () => SetFilter(index)));
            }
            sortButton = ButtonAt(panel, "Sort", "按人口 ↓", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(338, -146), new Vector2(456, -112), () =>
                {
                    sortByPopulation = !sortByPopulation;
                    Refresh();
                });
            ButtonAt(panel, "Refresh", "刷新", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(464, -146), new Vector2(534, -112), Refresh);

            RectTransform listPanel = Box(panel, "ListPanel", new Color(.07f, .09f, .08f),
                new Vector2(0, 0), new Vector2(.56f, 1));
            listPanel.offsetMin = new Vector2(16, 42);
            listPanel.offsetMax = new Vector2(-7, -156);
            RectTransform heading = Rect(listPanel, "Heading");
            heading.anchorMin = new Vector2(0, 1); heading.anchorMax = Vector2.one;
            heading.offsetMin = new Vector2(0, -34); heading.offsetMax = new Vector2(-12, 0);
            string[] titles = { "据点 / 势力", "城市人口", "农村人口", "合计" };
            for (int i = 0; i < titles.Length; i++)
            {
                Text title = Label(heading, "Column" + i, titles[i], 14, Gold,
                    new Vector2(Columns[i], 0), new Vector2(Columns[i + 1], 1), new Vector2(5, 2), new Vector2(-3, -2));
                title.alignment = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            }
            listScroll = MakeScroll(listPanel, "List", out listViewport, out listContent);
            ((RectTransform)listScroll.transform).offsetMin = new Vector2(0, 0);
            ((RectTransform)listScroll.transform).offsetMax = new Vector2(0, -36);
            listScroll.onValueChanged.AddListener(_ => RenderRows());

            RectTransform detailPanel = Box(panel, "DetailPanel", new Color(.08f, .10f, .09f),
                new Vector2(.56f, 0), new Vector2(1, 1));
            detailPanel.offsetMin = new Vector2(5, 42);
            detailPanel.offsetMax = new Vector2(-16, -156);
            detailTitle = Label(detailPanel, "DetailTitle", "全图季度趋势", 18, Gold,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -48), new Vector2(-12, -6));
            RectTransform chartArea = Box(detailPanel, "Trend", new Color(.05f, .075f, .065f),
                new Vector2(0, 1), new Vector2(1, 1));
            chartArea.offsetMin = new Vector2(12, -158);
            chartArea.offsetMax = new Vector2(-12, -54);
            RectTransform trendLine = Rect(chartArea, "Line");
            Stretch(trendLine);
            chart = trendLine.gameObject.AddComponent<HantuPopulationTrendGraphic>();
            chart.color = new Color(.65f, .82f, .53f);
            chart.raycastTarget = false;
            chartCaption = Label(detailPanel, "TrendCaption", "", 12, Ink, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -192), new Vector2(-12, -159));
            RectTransform unused;
            detailScroll = MakeScroll(detailPanel, "History", out unused, out detailContent);
            ((RectTransform)detailScroll.transform).offsetMin = new Vector2(8, 6);
            ((RectTransform)detailScroll.transform).offsetMax = new Vector2(-6, -196);
            detailText = Label(detailContent, "Text", "", 15, Ink,
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            detailText.rectTransform.pivot = new Vector2(.5f, 1);
            detailText.alignment = TextAnchor.UpperLeft;
            detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailText.verticalOverflow = VerticalWrapMode.Overflow;
            Label(panel, "Hint", "拖动或滚轮浏览  ·  点击据点查看变化来源  ·  返回 / Esc / 右键关闭", 14, Ink,
                Vector2.zero, new Vector2(1, 0), new Vector2(18, 8), new Vector2(-18, 36));
        }

        void Update()
        {
            if (scenario != Scenario.Cur)
            {
                Close();
                return;
            }
            if (listViewport != null && listViewport.rect.size != previousViewportSize)
            {
                previousViewportSize = listViewport.rect.size;
                EnsureRows();
                RenderRows();
                FitDetail();
            }
        }

        void Close()
        {
            if (closing) return;
            closing = true;
            onClose?.Invoke();
        }

        void SetFilter(int value)
        {
            filter = value;
            selected = null;
            Refresh();
        }

        public void Refresh()
        {
            cities.Clear();
            long urban = 0, rural = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || city.Id <= 0 || !city.IsAlive) continue;
                urban += Math.Max(0, city.population);
                rural += Math.Max(0, city.ruralPopulation.GetValueOrDefault());
                if (filter == 0 || (filter == 1 && !(city is Port) && !(city is Gate))
                    || (filter == 2 && city is Gate && !(city is Port)) || (filter == 3 && city is Port)) cities.Add(city);
            }
            cities.Sort((a, b) =>
            {
                if (sortByPopulation)
                {
                    int comparison = Total(b).CompareTo(Total(a));
                    if (comparison != 0) return comparison;
                }
                return a.Id.CompareTo(b.Id);
            });
            string delta = "季度记录不足两条";
            HantuPopulationHistory history = scenario.hantuPopulationHistory;
            if (history?.quarters != null && history.quarters.Count >= 2)
            {
                long u1, r1, u0, r0;
                int last = history.quarters.Count - 1;
                if (HantuPopulationHistory.TryGetPopulation(history.quarters[last], 0, out u1, out r1)
                    && HantuPopulationHistory.TryGetPopulation(history.quarters[last - 1], 0, out u0, out r0))
                    delta = "最近季度净变 " + Signed(u1 + r1 - u0 - r0);
            }
            summary.text = "全图总人口 " + Number(urban + rural) + "   城市 " + Number(urban) + "   农村 " + Number(rural)
                + "\n" + scenario.GetDateStr() + "   " + delta + "   当前筛选 " + cities.Count + " 处";
            for (int i = 0; i < filterButtons.Count; i++)
                filterButtons[i].GetComponent<Image>().color = i == filter
                    ? new Color(.35f, .31f, .19f) : new Color(.20f, .23f, .19f);
            sortButton.GetComponentInChildren<Text>().text = sortByPopulation ? "按人口 ↓" : "按据点序号";
            listContent.sizeDelta = new Vector2(0, cities.Count * RowHeight);
            listContent.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            EnsureRows();
            RenderRows();
            RefreshDetails();
        }

        void EnsureRows()
        {
            int count = Mathf.CeilToInt(Mathf.Max(1, listViewport.rect.height) / RowHeight) + 2;
            while (rows.Count < count)
            {
                Row row = new Row();
                row.rect = Rect(listContent, "Row" + rows.Count);
                row.rect.anchorMin = new Vector2(0, 1);
                row.rect.anchorMax = new Vector2(1, 1);
                row.rect.pivot = new Vector2(.5f, 1);
                row.rect.sizeDelta = new Vector2(-14, RowHeight - 2);
                row.background = row.rect.gameObject.AddComponent<Image>();
                Button button = row.rect.gameObject.AddComponent<Button>();
                button.targetGraphic = row.background;
                button.onClick.AddListener(() => Select(row.index));
                row.labels = new Text[4];
                for (int i = 0; i < 4; i++)
                {
                    row.labels[i] = Label(row.rect, "Column" + i, "", i == 0 ? 14 : 15, Ink,
                        new Vector2(Columns[i], 0), new Vector2(Columns[i + 1], 1), new Vector2(5, 2), new Vector2(-3, -2));
                    row.labels[i].alignment = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                }
                rows.Add(row);
            }
        }

        void RenderRows()
        {
            int first = Mathf.FloorToInt(Mathf.Max(0, listContent.anchoredPosition.y) / RowHeight);
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                row.index = first + i;
                bool visible = row.index < cities.Count;
                row.rect.gameObject.SetActive(visible);
                if (!visible) continue;
                City city = cities[row.index];
                row.rect.anchoredPosition = new Vector2(-4, -row.index * RowHeight);
                row.background.color = city == selected ? new Color(.31f, .29f, .18f)
                    : row.index % 2 == 0 ? new Color(.16f, .19f, .15f) : new Color(.12f, .15f, .12f);
                row.labels[0].text = city.Name + " · " + Kind(city) + "\n" + (city.BelongForce?.Name ?? "无所属");
                row.labels[1].text = Number(city.population);
                row.labels[2].text = Number(city.ruralPopulation.GetValueOrDefault());
                row.labels[3].text = Number(Total(city));
            }
        }

        void Select(int index)
        {
            if (index < 0 || index >= cities.Count) return;
            selected = cities[index];
            RenderRows();
            RefreshDetails();
        }

        void RefreshDetails()
        {
            int cityId = selected?.Id ?? 0;
            detailTitle.text = selected == null ? "全图季度趋势" : selected.Name + " · " + Kind(selected)
                + "\n当前 " + Number(Total(selected)) + " 人";
            var text = new StringBuilder();
            var values = new List<long>();
            HantuPopulationHistory history = scenario.hantuPopulationHistory;
            long previous = 0;
            bool hasPrevious = false;
            string firstDate = null, lastDate = null;
            if (selected != null && !(selected is Gate) && !(selected is Port))
                text.Append("商业 ").Append(selected.commerce).Append(" / 农业 ").Append(selected.agriculture).AppendLine().AppendLine();
            text.AppendLine("季度记录  ·  合计 / 较上条净变");
            if (history?.quarters != null)
            {
                for (int i = 0; i < history.quarters.Count; i++)
                {
                    HantuPopulationQuarter quarter = history.quarters[i];
                    long urban, rural;
                    if (!HantuPopulationHistory.TryGetPopulation(quarter, cityId, out urban, out rural)) continue;
                    long total = urban + rural;
                    values.Add(total);
                    if (firstDate == null) firstDate = quarter.date;
                    lastDate = quarter.date;
                    text.Append(quarter.date).Append("  ").Append(Number(total));
                    text.Append(hasPrevious ? "  " + Signed(total - previous) : "  首条记录").AppendLine();
                    text.Append("城市 ").Append(Number(urban)).Append(" / 农村 ").Append(Number(rural)).AppendLine();
                    previous = total;
                    hasPrevious = true;
                }
            }
            if (values.Count == 0) text.AppendLine("尚无季度记录。旧档启用后的实际记录会从本局开始积累。");
            chart.SetValues(values);
            chartCaption.text = values.Count < 2 ? "至少两条季度记录后显示趋势线"
                : firstDate + " → " + lastDate + "\n纵轴 " + Number(chart.Minimum) + " ～ " + Number(chart.Maximum);
            if (selected == null)
                AppendWorldChanges(text, history);
            else
            {
                text.AppendLine().AppendLine("最近人口变化  ·  同回合、同来源合并");
                IReadOnlyList<HantuPopulationChange> changes = history?.GetRecentChanges(cityId);
                if (changes == null || changes.Count == 0) text.AppendLine("暂无变化记录；不回推旧档中的历史。");
                else
                    for (int i = changes.Count - 1; i >= 0; i--)
                    {
                        HantuPopulationChange change = changes[i];
                        if (change == null) continue;
                        text.Append(change.date).Append("  ").Append(HantuPopulationHistory.SourceName(change.source)).AppendLine();
                        text.Append("城市 ").Append(Signed(change.urbanDelta)).Append(" / 农村 ").Append(Signed(change.ruralDelta))
                            .Append(" / 合计 ").Append(Signed(change.urbanDelta + change.ruralDelta)).AppendLine();
                    }
            }
            text.AppendLine().Append("保留最近 ").Append(HantuPopulationHistory.QuarterLimit).Append(" 季、每据点最近 ")
                .Append(HantuPopulationHistory.ChangesPerCityLimit).Append(" 项变化。");
            detailText.text = text.ToString();
            detailContent.anchoredPosition = Vector2.zero;
            FitDetail();
        }

        void AppendWorldChanges(StringBuilder text, HantuPopulationHistory history)
        {
            text.AppendLine().AppendLine("本季已记录变化  ·  按来源汇总");
            var totals = new SortedDictionary<HantuPopulationChangeSource, long[]>();
            if (history?.cities != null)
                for (int i = 0; i < history.cities.Count; i++)
                {
                    HantuPopulationCityChanges city = history.cities[i];
                    if (city?.changes == null) continue;
                    for (int j = 0; j < city.changes.Count; j++)
                    {
                        HantuPopulationChange change = city.changes[j];
                        if (change == null || change.season != scenario.SeasonStamp) continue;
                        long[] values;
                        if (!totals.TryGetValue(change.source, out values))
                        {
                            values = new long[2];
                            totals.Add(change.source, values);
                        }
                        values[0] += change.urbanDelta;
                        values[1] += change.ruralDelta;
                    }
                }
            if (totals.Count == 0) text.AppendLine("本季暂无已记录变化。");
            foreach (var total in totals)
            {
                text.Append(HantuPopulationHistory.SourceName(total.Key)).Append("  合计 ")
                    .Append(Signed(total.Value[0] + total.Value[1])).AppendLine();
                text.Append("城市 ").Append(Signed(total.Value[0])).Append(" / 农村 ").Append(Signed(total.Value[1])).AppendLine();
            }
            text.AppendLine("仅汇总仍留存的明细；每点超过上限的记录不计入。");
            text.AppendLine("点击左侧据点查看该点最近变化来源。");
        }

        void FitDetail()
        {
            Canvas.ForceUpdateCanvases();
            float height = Mathf.Max(detailScroll.viewport.rect.height, detailText.preferredHeight + 12);
            detailContent.sizeDelta = new Vector2(0, height);
            detailText.rectTransform.sizeDelta = new Vector2(0, height);
        }

        static long Total(City city) { return (long)Math.Max(0, city.population) + Math.Max(0, city.ruralPopulation.GetValueOrDefault()); }
        static string Kind(City city) { return city is Port ? "港口" : city is Gate ? "关隘" : "城池"; }
        static string Number(long value) { return value.ToString("N0", CultureInfo.InvariantCulture); }
        static string Signed(long value) { return (value > 0 ? "+" : "") + Number(value); }

        RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        RectTransform Box(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            RectTransform rect = Rect(parent, name);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        Text Label(Transform parent, string name, string value, int size, Color color,
            Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = Rect(parent, name);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = color;
            label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false;
            label.raycastTarget = false; label.text = value;
            return label;
        }

        Button ButtonAt(Transform parent, string name, string title, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Action action)
        {
            RectTransform rect = Box(parent, name, new Color(.20f, .23f, .19f), min, max);
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => action());
            Text label = Label(rect, "Label", title, 16, Ink, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        ScrollRect MakeScroll(Transform parent, string name, out RectTransform viewport, out RectTransform content)
        {
            RectTransform root = Rect(parent, name);
            Stretch(root);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            viewport = Box(root, "Viewport", new Color(0, 0, 0, .01f), Vector2.zero, Vector2.one);
            viewport.offsetMax = new Vector2(-12, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f; scroll.inertia = true;
            RectTransform track = Box(root, "Scrollbar", new Color(.14f, .17f, .14f), new Vector2(1, 0), Vector2.one);
            track.offsetMin = new Vector2(-10, 2); track.offsetMax = new Vector2(0, -2);
            RectTransform handle = Box(track, "Handle", new Color(.44f, .46f, .32f), Vector2.zero, Vector2.one);
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = scrollbar;
            return scroll;
        }
    }

    /// <summary>无额外贴图的原生 UGUI 折线；纵轴范围始终随说明文本显示。</summary>
    public sealed class HantuPopulationTrendGraphic : MaskableGraphic
    {
        readonly List<long> values = new List<long>();
        public long Minimum { get; private set; }
        public long Maximum { get; private set; }

        public void SetValues(IReadOnlyList<long> source)
        {
            values.Clear();
            if (source != null) for (int i = 0; i < source.Count; i++) values.Add(source[i]);
            Minimum = long.MaxValue; Maximum = long.MinValue;
            for (int i = 0; i < values.Count; i++) { Minimum = Math.Min(Minimum, values[i]); Maximum = Math.Max(Maximum, values[i]); }
            if (values.Count == 0) Minimum = Maximum = 0;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (values.Count < 2) return;
            Rect rect = rectTransform.rect;
            double range = (double)Maximum - Minimum;
            Vector2 previous = Vector2.zero;
            for (int i = 0; i < values.Count; i++)
            {
                float fraction = range > 0 ? (float)((values[i] - (double)Minimum) / range) : .5f;
                Vector2 point = new Vector2(rect.xMin + 10 + i * (rect.width - 20) / (values.Count - 1),
                    rect.yMin + 10 + fraction * (rect.height - 20));
                if (i > 0)
                {
                    Vector2 normal = new Vector2(-(point - previous).y, (point - previous).x).normalized * 1.5f;
                    int start = helper.currentVertCount;
                    helper.AddVert(previous - normal, color, Vector2.zero);
                    helper.AddVert(previous + normal, color, Vector2.zero);
                    helper.AddVert(point + normal, color, Vector2.zero);
                    helper.AddVert(point - normal, color, Vector2.zero);
                    helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
                }
                previous = point;
            }
        }
    }
}
