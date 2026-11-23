using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sango.Core;
using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>原生 UGUI 人口总表；按可见行复用对象，城关港数量不影响节点数量。</summary>
    public sealed class UIHantuPopulationOverview : MonoBehaviour
    {
        const float RowHeight = 22.5f;
        const string CityWindowAsset = "Assets/UI/Prefab/window_information_city.prefab";
        static readonly Color Ink = new Color(.93f, .93f, .93f);
        static readonly Color Heading = new Color(.74f, .89f, .93f);
        static readonly Color Surface = new Color(.06f, .17f, .22f, .95f);
        static readonly Color SelectedRow = new Color(.55f, .84f, 1);
        static readonly string[] Filters = { "全图", "城池", "关隘", "港口" };
        static readonly string[] SortLabels = { "总人口 ↓", "城市人口 ↓", "农村人口 ↓" };

        sealed class Row
        {
            public RectTransform rect;
            public Image background;
            public Text label;
            public City city;
        }

        sealed class TrendDetails
        {
            public readonly List<long> values = new List<long>();
            public bool hasCurrentPoint;
            public string dates, change, historyText;
            public int direction;
            public float measuredWidth, preferredHeight;
        }

        Scenario scenario;
        Action onClose;
        Font font;
        Transform nativeRoot;
        int filter;
        int sort;
        City selected;
        readonly List<City> cities = new List<City>();
        readonly List<Row> rows = new List<Row>();
        readonly List<Button> filterButtons = new List<Button>();
        Text summary, detailTitle, detailText, chartCaption, chartChange, development;
        Text scaleMaximum, scaleMiddle, scaleMinimum;
        readonly Text[] populationValues = new Text[3];
        long worldUrban, worldRural;
        Button sortButton;
        RectTransform listViewport, listContent, detailContent;
        ScrollRect listScroll, detailScroll;
        HantuPopulationTrendGraphic chart;
        TrendDetails worldTrend, displayedTrend;
        HantuPopulationHistory cachedWorldHistory;
        int cachedWorldRevision, cachedWorldTurn;
        long cachedWorldPopulation;
        Vector2 previousViewportSize, previousDetailSize;
        bool closing;

        public static UIHantuPopulationOverview Create(RectTransform parent, Scenario scenario, Action onClose)
        {
            GameObject go = new GameObject("HantuPopulationOverview", typeof(RectTransform));
            try
            {
                go.transform.SetParent(parent, false);
                // Game's UI camera renders the UI layer, even though UIRoot is on Default.
                int uiLayer = LayerMask.NameToLayer("UI");
                go.layer = uiLayer >= 0 ? uiLayer : parent.gameObject.layer;
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
            GameObject nativeWindow = ObjectLoader.LoadObject<GameObject>(CityWindowAsset);
            nativeRoot = nativeWindow?.transform.Find("root");
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "Normal";
            canvas.sortingOrder = 1000;
            gameObject.AddComponent<GraphicRaycaster>();
            font = GameSetting.Instance.Font;
            if (font == null) font = nativeRoot?.Find("win_frame/title")?.GetComponent<Text>()?.font;
            if (font == null) font = Resources.Load<Font>("siyuancn");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (nativeWindow != null) CopyVisual(nativeWindow.transform.Find("mask"), root);
            else gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .5f);

            RectTransform panel = Rect(root, "Panel");
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(990.5474f, 750.035f);
            panel.anchoredPosition = new Vector2(-18.953491f, 2.1676025f);
            if (nativeRoot != null) CopyRect((RectTransform)nativeRoot, panel);
            Transform frame = CopyVisual(nativeRoot?.Find("win_frame"), panel);
            if (frame != null)
            {
                Text title = frame.Find("title").GetComponent<Text>();
                title.font = font;
                title.text = "人口总览";
                Button close = frame.Find("button_cancel").GetComponent<Button>();
                close.GetComponentInChildren<Text>().font = font;
                Bind(close, Close);
            }
            else
            {
                panel.gameObject.AddComponent<Image>().color = Surface;
                Label(panel, "Title", "人口总览", 32, Ink, new Vector2(0, 1), Vector2.one, new Vector2(16, -54), new Vector2(-16, -12));
                ButtonAt(panel, "Close", "返回", 20, 44, 96, Close, new Vector2(0, 0));
            }

            RectTransform listPanel = Section(panel, "ListPanel", "area_2", 16, 106, 64, 567);
            RectTransform summaryPanel = Section(panel, "SummaryPanel", "area_city_info", 133, 837, 64, 187);
            RectTransform detailPanel = Section(panel, "DetailPanel", "area_all", 133, 837, 263, 380);
            summary = Label(summaryPanel, "Summary", "", 18, Ink, new Vector2(0, 1), Vector2.one,
                new Vector2(12, -36), new Vector2(-12, -6));
            for (int i = 0; i < Filters.Length; i++)
            {
                int index = i;
                filterButtons.Add(ButtonAt(summaryPanel, "Filter" + i, Filters[i], 12 + i * 76, 40, 68, () => SetFilter(index)));
            }
            sortButton = ButtonAt(summaryPanel, "Sort", SortLabels[0], 331, 40, 126, () =>
            {
                sort = (sort + 1) % SortLabels.Length;
                RefreshRanking();
            });
            ButtonAt(summaryPanel, "Refresh", "刷新", 470, 40, 68, Refresh);
            ButtonAt(summaryPanel, "WorldTrend", "全图趋势", 550, 40, 96, () => Select(null));
            detailTitle = Label(summaryPanel, "DetailTitle", "", 21, Heading,
                new Vector2(0, 1), Vector2.one, new Vector2(12, -103), new Vector2(-12, -77));
            string[] titles = { "人口合计", "城市人口", "农村人口" };
            for (int i = 0; i < titles.Length; i++)
            {
                RectTransform stat = Box(summaryPanel, titles[i], new Color(.08f, .18f, .24f, .82f),
                    new Vector2(i / 3f, 1), new Vector2((i + 1) / 3f, 1));
                stat.offsetMin = new Vector2(12, -140);
                stat.offsetMax = new Vector2(-12, -106);
                Label(stat, "Label", titles[i], 18, Heading, Vector2.zero, new Vector2(.52f, 1),
                    new Vector2(5, 0), Vector2.zero);
                populationValues[i] = Label(stat, "Value", "", 20, Ink, new Vector2(.52f, 0), Vector2.one,
                    Vector2.zero, new Vector2(-6, 0));
                populationValues[i].alignment = TextAnchor.MiddleRight;
            }
            development = Label(summaryPanel, "Development", "", 17, Ink, new Vector2(0, 1), Vector2.one,
                new Vector2(12, -176), new Vector2(-12, -145));

            listScroll = MakeScroll(listPanel, "List", out listViewport, out listContent);
            ((RectTransform)listScroll.transform).offsetMin = new Vector2(3, 4);
            ((RectTransform)listScroll.transform).offsetMax = new Vector2(-3, -4);
            listScroll.onValueChanged.AddListener(_ => RenderRows());

            Label(detailPanel, "TrendTitle", "人口趋势", 21, Heading, new Vector2(0, 1), new Vector2(.52f, 1),
                new Vector2(12, -40), new Vector2(-12, -6));
            Label(detailPanel, "TrendUp", "↑ 上升", 15, HantuPopulationTrendGraphic.Rising, new Vector2(.22f, 1), new Vector2(.32f, 1),
                new Vector2(0, -39), new Vector2(0, -6));
            Label(detailPanel, "TrendDown", "↓ 下降", 15, HantuPopulationTrendGraphic.Falling, new Vector2(.32f, 1), new Vector2(.42f, 1),
                new Vector2(0, -39), new Vector2(0, -6));
            Label(detailPanel, "TrendFlat", "— 持平", 15, HantuPopulationTrendGraphic.Steady, new Vector2(.42f, 1), new Vector2(.52f, 1),
                new Vector2(0, -39), new Vector2(0, -6));
            Label(detailPanel, "HistoryTitle", "人口变动记录", 21, Heading, new Vector2(.52f, 1), Vector2.one,
                new Vector2(12, -40), new Vector2(-12, -6));
            RectTransform chartArea = Box(detailPanel, "Trend", new Color(.04f, .10f, .14f, .82f),
                Vector2.zero, new Vector2(.52f, 1));
            chartArea.offsetMin = new Vector2(12, 61);
            chartArea.offsetMax = new Vector2(-8, -45);
            RectTransform trendLine = Rect(chartArea, "Line");
            Stretch(trendLine);
            chart = trendLine.gameObject.AddComponent<HantuPopulationTrendGraphic>();
            chart.raycastTarget = false;
            scaleMaximum = MakeScaleLabel(chartArea, "ScaleMaximum", 1, -16);
            scaleMiddle = MakeScaleLabel(chartArea, "ScaleMiddle", .5f, 0);
            scaleMinimum = MakeScaleLabel(chartArea, "ScaleMinimum", 0, 16);
            chartCaption = Label(detailPanel, "TrendCaption", "", 15, Ink, Vector2.zero, new Vector2(.52f, 0),
                new Vector2(12, 32), new Vector2(-8, 58));
            chartChange = Label(detailPanel, "TrendChange", "", 16, Ink, Vector2.zero, new Vector2(.52f, 0),
                new Vector2(12, 8), new Vector2(-8, 33));
            detailScroll = MakeScroll(detailPanel, "History", out _, out detailContent);
            RectTransform historyRect = (RectTransform)detailScroll.transform;
            historyRect.anchorMin = new Vector2(.52f, 0);
            historyRect.offsetMin = new Vector2(12, 12);
            historyRect.offsetMax = new Vector2(-12, -45);
            detailText = Label(detailContent, "Text", "", 17, Ink,
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            detailText.rectTransform.pivot = new Vector2(.5f, 1);
            detailText.alignment = TextAnchor.UpperLeft;
            detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailText.verticalOverflow = VerticalWrapMode.Overflow;
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
            }
            if (detailScroll.viewport.rect.size != previousDetailSize)
            {
                previousDetailSize = detailScroll.viewport.rect.size;
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
            RefreshRanking();
        }

        public void Refresh()
        {
            worldTrend = null;
            RefreshRanking();
        }

        void RefreshRanking()
        {
            cities.Clear();
            worldUrban = worldRural = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || city.Id <= 0 || !city.IsAlive) continue;
                worldUrban += Urban(city);
                worldRural += Rural(city);
                if (filter == 0 || (filter == 1 && !(city is Port) && !(city is Gate))
                    || (filter == 2 && city is Gate && !(city is Port)) || (filter == 3 && city is Port)) cities.Add(city);
            }
            cities.Sort((a, b) =>
            {
                int comparison = RankedPopulation(b).CompareTo(RankedPopulation(a));
                return comparison != 0 ? comparison : a.Id.CompareTo(b.Id);
            });
            if (!cities.Contains(selected)) selected = null;
            UpdateSummary();
            for (int i = 0; i < filterButtons.Count; i++)
                filterButtons[i].targetGraphic.color = i == filter ? new Color(.59f, .91f, .99f) : Color.white;
            sortButton.GetComponentInChildren<Text>().text = SortLabels[sort];
            listScroll.StopMovement();
            listContent.sizeDelta = new Vector2(0, cities.Count * RowHeight);
            listContent.anchoredPosition = Vector2.zero;
            EnsureRows();
            RenderRows();
            RefreshDetails();
        }

        void UpdateSummary()
        {
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
            summary.text = "全图总人口 " + Number(worldUrban + worldRural) + "    " + scenario.GetDateStr()
                + "    " + delta + "    当前筛选 " + cities.Count + " 处";
        }

        void EnsureRows()
        {
            int count = Mathf.CeilToInt(Mathf.Max(1, listViewport.rect.height) / RowHeight) + 2;
            Image rowSkin = nativeRoot?.Find("object_list/content/listItem/bg")?.GetComponent<Image>();
            while (rows.Count < count)
            {
                Row row = new Row();
                row.rect = Rect(listContent, "Row" + rows.Count);
                row.rect.anchorMin = new Vector2(0, 1);
                row.rect.anchorMax = new Vector2(1, 1);
                row.rect.pivot = new Vector2(.5f, 1);
                row.rect.sizeDelta = new Vector2(-2, RowHeight);
                row.background = row.rect.gameObject.AddComponent<Image>();
                CopyImageStyle(rowSkin, row.background);
                Button button = row.rect.gameObject.AddComponent<Button>();
                button.targetGraphic = row.background;
                button.onClick.AddListener(() =>
                {
                    if (row.city != null) Select(row.city);
                });
                row.label = Label(row.rect, "Column0", "", 18, Ink,
                    Vector2.zero, Vector2.one, new Vector2(3, 0), new Vector2(-2, 0));
                row.label.resizeTextForBestFit = true;
                row.label.resizeTextMinSize = 10;
                row.label.resizeTextMaxSize = 18;
                rows.Add(row);
            }
        }

        void RenderRows()
        {
            int first = Mathf.FloorToInt(Mathf.Max(0, listContent.anchoredPosition.y) / RowHeight);
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                int index = first + i;
                row.city = index < cities.Count ? cities[index] : null;
                row.rect.gameObject.SetActive(row.city != null);
                if (row.city == null) continue;
                row.rect.anchoredPosition = new Vector2(0, -index * RowHeight);
                row.background.color = row.city == selected ? SelectedRow : Color.white;
                row.label.text = (index + 1) + ". " + row.city.Name;
            }
        }

        void Select(City city)
        {
            if (city == null && !IsWorldTrendCurrent(scenario.hantuPopulationHistory))
            {
                worldUrban = worldRural = 0;
                for (int i = 0; i < scenario.citySet.Count; i++)
                {
                    City settlement = scenario.citySet[i];
                    if (settlement == null || settlement.Id <= 0 || !settlement.IsAlive) continue;
                    worldUrban += Urban(settlement);
                    worldRural += Rural(settlement);
                }
                UpdateSummary();
            }
            else if (city == selected) return;
            City previous = selected;
            selected = city;
            foreach (Row row in rows)
                if (row.city != null && (row.city == previous || row.city == selected))
                    row.background.color = row.city == selected ? SelectedRow : Color.white;
            RefreshDetails();
        }

        void RefreshDetails()
        {
            int cityId = selected?.Id ?? 0;
            detailTitle.text = selected == null ? "全图人口" : selected.Name + " · " + Kind(selected)
                + "     " + (selected.BelongForce?.Name ?? "无所属");
            long urbanPopulation = selected == null ? worldUrban : Urban(selected);
            long ruralPopulation = selected == null ? worldRural : Rural(selected);
            populationValues[0].text = Number(urbanPopulation + ruralPopulation);
            populationValues[1].text = Number(urbanPopulation);
            populationValues[2].text = Number(ruralPopulation);
            development.text = selected != null && selected.IsCity()
                ? "商业发展度 " + Number(selected.commerce) + " / " + Number(selected.CommerceLimit)
                + "      农业发展度 " + Number(selected.agriculture) + " / " + Number(selected.AgricultureLimit)
                : "";
            TrendDetails trend = GetTrendDetails(cityId, urbanPopulation + ruralPopulation);
            if (ReferenceEquals(displayedTrend, trend)) return;
            displayedTrend = trend;
            chart.SetValues(trend.values, trend.hasCurrentPoint);
            bool range = chart.Maximum != chart.Minimum;
            scaleMaximum.text = range ? Number(chart.Maximum) : "";
            scaleMiddle.text = range ? "" : Number(chart.Maximum);
            scaleMinimum.text = range ? Number(chart.Minimum) : "";
            chartCaption.text = trend.dates;
            chartChange.text = trend.change;
            chartChange.color = HantuPopulationTrendGraphic.DirectionColor(trend.direction);
            detailScroll.StopMovement();
            detailText.text = trend.historyText;
            detailContent.anchoredPosition = Vector2.zero;
            FitDetail();
        }

        bool IsWorldTrendCurrent(HantuPopulationHistory history)
        {
            return worldTrend != null && cachedWorldHistory == history && cachedWorldRevision == (history?.Revision ?? 0)
                && cachedWorldTurn == scenario.TurnCount && cachedWorldPopulation == worldUrban + worldRural;
        }

        TrendDetails GetTrendDetails(int cityId, long currentPopulation)
        {
            HantuPopulationHistory history = scenario.hantuPopulationHistory;
            if (cityId != 0) return BuildTrendDetails(cityId, currentPopulation, history);
            if (!IsWorldTrendCurrent(history))
            {
                worldTrend = BuildTrendDetails(0, currentPopulation, history);
                cachedWorldHistory = history;
                cachedWorldRevision = history?.Revision ?? 0;
                cachedWorldTurn = scenario.TurnCount;
                cachedWorldPopulation = currentPopulation;
            }
            return worldTrend;
        }

        TrendDetails BuildTrendDetails(int cityId, long currentPopulation, HantuPopulationHistory history)
        {
            var text = new StringBuilder();
            var trend = new TrendDetails();
            List<long> values = trend.values;
            long previous = 0;
            string firstDate = null, lastDate = null;
            text.AppendLine("季度记录  ·  合计 / 较上条净变");
            if (history?.quarters != null)
            {
                for (int i = 0; i < history.quarters.Count; i++)
                {
                    HantuPopulationQuarter quarter = history.quarters[i];
                    long urban, rural;
                    if (!HantuPopulationHistory.TryGetPopulation(quarter, cityId, out urban, out rural)) continue;
                    long total = urban + rural;
                    if (values.Count == 0) firstDate = quarter.date;
                    lastDate = quarter.date;
                    text.Append(quarter.date).Append("  ").Append(Number(total));
                    text.Append(values.Count != 0 ? "  " + Signed(total - previous) : "  首条记录").AppendLine();
                    text.Append("城市 ").Append(Number(urban)).Append(" / 农村 ").Append(Number(rural)).AppendLine();
                    values.Add(total);
                    previous = total;
                }
            }
            if (values.Count == 0) text.AppendLine("尚无季度记录。旧档启用后的实际记录会从本局开始积累。");
            string currentDate = scenario.GetDateStr();
            trend.hasCurrentPoint = values.Count == 0 || lastDate != currentDate || previous != currentPopulation;
            if (trend.hasCurrentPoint) values.Add(currentPopulation);
            if (values.Count == 1)
            {
                trend.dates = (trend.hasCurrentPoint ? currentDate + "  当前值" : firstDate + "  季度基线");
                trend.change = "人口 " + Number(values[0]) + " · 暂无可比较记录";
            }
            else
            {
                trend.dates = firstDate + " → " + (trend.hasCurrentPoint ? "当前 " + currentDate : lastDate);
                long delta = values[values.Count - 1] - values[values.Count - 2];
                trend.direction = delta.CompareTo(0L);
                trend.change = delta > 0 ? "↑ 上升 " + Signed(delta) : delta < 0 ? "↓ 下降 " + Signed(delta) : "— 持平 0";
                trend.change += trend.hasCurrentPoint ? "  较上条季度记录" : "  较上一季度记录";
            }
            if (cityId == 0)
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
            trend.historyText = text.ToString();
            return trend;
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
            if (displayedTrend == null) return;
            float width = detailText.rectTransform.rect.width;
            if (width <= 0) return;
            if (!Mathf.Approximately(width, displayedTrend.measuredWidth))
            {
                displayedTrend.preferredHeight = detailText.preferredHeight + 12;
                displayedTrend.measuredWidth = width;
            }
            float height = Mathf.Max(detailScroll.viewport.rect.height, displayedTrend.preferredHeight);
            detailContent.sizeDelta = new Vector2(0, height);
            detailText.rectTransform.sizeDelta = new Vector2(0, height);
        }

        Text MakeScaleLabel(Transform parent, string name, float anchor, float offset)
        {
            Vector2 position = new Vector2(0, anchor);
            Text label = Label(parent, name, "", 14, Ink, position, position,
                new Vector2(3, offset - 11), new Vector2(HantuPopulationTrendGraphic.LabelWidth - 5, offset + 11));
            label.alignment = TextAnchor.MiddleRight;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 14;
            return label;
        }

        long RankedPopulation(City city)
        {
            switch (sort)
            {
                case 1: return Urban(city);
                case 2: return Rural(city);
                default: return (long)Urban(city) + Rural(city);
            }
        }

        static int Urban(City city) { return Math.Max(0, city.population); }
        static int Rural(City city) { return Math.Max(0, city.ruralPopulation.GetValueOrDefault()); }
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

        Transform CopyVisual(Transform template, Transform parent)
        {
            if (template == null) return null;
            Transform copy = Instantiate(template, parent, false);
            copy.name = template.name;
            UnityTools.SetLayer(copy.gameObject, gameObject.layer);
            return copy;
        }

        static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition;
        }

        RectTransform Section(Transform parent, string name, string nativeName, float left, float width, float top, float height)
        {
            RectTransform rect = Rect(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            RectTransform native = nativeRoot?.Find(nativeName) as RectTransform;
            if (native != null) CopyRect(native, rect);
            if (CopyVisual(native?.Find("bg"), rect) == null)
                rect.gameObject.AddComponent<Image>().color = Surface;
            return rect;
        }

        static void CopyImageStyle(Image source, Image target)
        {
            if (source == null) return;
            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
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

        Button ButtonAt(Transform parent, string name, string title, float x, float y, float width, Action action,
            Vector2? anchor = null)
        {
            RectTransform rect = CopyVisual(nativeRoot?.Find("port_gate_btn"), parent) as RectTransform;
            Button button;
            Text label;
            if (rect == null)
            {
                rect = Box(parent, name, new Color(.20f, .34f, .41f), Vector2.zero, Vector2.one);
                button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = rect.GetComponent<Image>();
                label = Label(rect, "Label", title, 18, Ink, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
                label.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                rect.name = name;
                button = rect.GetComponent<Button>();
                label = rect.GetComponentInChildren<Text>();
                label.font = font;
                label.text = title;
                label.fontSize = 18;
            }
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, anchor.HasValue ? y : -y);
            rect.sizeDelta = new Vector2(width, 32);
            Bind(button, action);
            return button;
        }

        static void Bind(Button button, Action action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => action());
        }

        ScrollRect MakeScroll(Transform parent, string name, out RectTransform viewport, out RectTransform content)
        {
            RectTransform root = Rect(parent, name);
            Stretch(root);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            viewport = Box(root, "Viewport", new Color(0, 0, 0, .01f), Vector2.zero, Vector2.one);
            viewport.offsetMax = new Vector2(-16, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f; scroll.inertia = true;
            RectTransform track = CopyVisual(nativeRoot?.Find("object_list/slider_v/Scrollbar"), root) as RectTransform;
            Scrollbar scrollbar;
            if (track != null) scrollbar = track.GetComponent<Scrollbar>();
            else
            {
                track = Box(root, "Scrollbar", new Color(.14f, .18f, .21f), new Vector2(1, 0), Vector2.one);
                RectTransform handle = Box(track, "Handle", new Color(.50f, .54f, .45f), Vector2.zero, Vector2.one);
                scrollbar = track.gameObject.AddComponent<Scrollbar>();
                scrollbar.handleRect = handle;
                scrollbar.targetGraphic = handle.GetComponent<Image>();
            }
            track.anchorMin = new Vector2(1, 0);
            track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(-16, 0);
            track.offsetMax = Vector2.zero;
            // ScrollRect uses 1 at the top, opposite to the native object-list scrollbar.
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.onValueChanged = new Scrollbar.ScrollEvent();
            scroll.verticalScrollbar = scrollbar;
            return scroll;
        }
    }

    /// <summary>按实际记录绘制折线，虚线连接至本回合人口。</summary>
    public sealed class HantuPopulationTrendGraphic : MaskableGraphic
    {
        internal const float LabelWidth = 96;
        internal static readonly Color Rising = new Color(.51f, .90f, .67f);
        internal static readonly Color Falling = new Color(1f, .52f, .48f);
        internal static readonly Color Steady = new Color(.54f, .81f, .94f);
        static readonly Color Grid = new Color(.43f, .65f, .73f, .23f);
        static readonly Color Axis = new Color(.55f, .73f, .81f, .55f);
        static readonly Color Point = new Color(.84f, .95f, .98f);
        readonly List<long> values = new List<long>();
        bool hasCurrentPoint;
        public long Minimum { get; private set; }
        public long Maximum { get; private set; }

        public void SetValues(IReadOnlyList<long> source, bool hasCurrentPoint = false)
        {
            values.Clear();
            this.hasCurrentPoint = hasCurrentPoint;
            Minimum = long.MaxValue; Maximum = long.MinValue;
            if (source != null)
                for (int i = 0; i < source.Count; i++)
                {
                    long value = source[i];
                    values.Add(value);
                    Minimum = Math.Min(Minimum, value);
                    Maximum = Math.Max(Maximum, value);
                }
            if (values.Count == 0) Minimum = Maximum = 0;
            SetVerticesDirty();
        }

        internal static Color DirectionColor(int direction)
        {
            return direction > 0 ? Rising : direction < 0 ? Falling : Steady;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (values.Count == 0) return;
            Rect rect = rectTransform.rect;
            Rect plot = Rect.MinMaxRect(rect.xMin + LabelWidth, rect.yMin + 16, rect.xMax - 16, rect.yMax - 16);
            if (plot.width <= 0 || plot.height <= 0) return;
            double range = (double)Maximum - Minimum;
            for (int i = 0; i <= 4; i++)
            {
                float y = plot.yMin + plot.height * i / 4;
                Line(helper, new Vector2(plot.xMin, y), new Vector2(plot.xMax, y), 1, i == 0 ? Axis : Grid);
            }
            Line(helper, new Vector2(plot.xMin, plot.yMin), new Vector2(plot.xMin, plot.yMax), 1, Axis);

            Vector2 previous = default;
            for (int i = 0; i < values.Count; i++)
            {
                float fraction = range > 0 ? (float)((values[i] - (double)Minimum) / range) : .5f;
                float progress = values.Count == 1 ? .5f : (float)i / (values.Count - 1);
                Vector2 point = new Vector2(plot.xMin + plot.width * progress, plot.yMin + plot.height * fraction);
                bool current = hasCurrentPoint && i == values.Count - 1;
                Color pointColor = Point;
                if (i > 0)
                {
                    pointColor = DirectionColor(values[i].CompareTo(values[i - 1]));
                    if (current) DashedLine(helper, previous, point, pointColor);
                    else Line(helper, previous, point, 2.5f, pointColor);
                }
                int radius = current || values.Count == 1 ? 5 : 3;
                Quad(helper, point + Vector2.up * radius, point + Vector2.right * radius,
                    point + Vector2.down * radius, point + Vector2.left * radius, pointColor);
                previous = point;
            }
        }

        static void DashedLine(VertexHelper helper, Vector2 from, Vector2 to, Color color)
        {
            Vector2 offset = to - from;
            float length = offset.magnitude;
            if (length <= 0) return;
            Vector2 direction = offset / length;
            for (float position = 0; position < length; position += 12)
                Line(helper, from + direction * position, from + direction * Mathf.Min(position + 7, length), 2.5f, color);
        }

        static void Line(VertexHelper helper, Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = to - from;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
            Quad(helper, from - normal, from + normal, to + normal, to - normal, color);
        }

        static void Quad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = helper.currentVertCount;
            helper.AddVert(a, color, Vector2.zero);
            helper.AddVert(b, color, Vector2.zero);
            helper.AddVert(c, color, Vector2.zero);
            helper.AddVert(d, color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
