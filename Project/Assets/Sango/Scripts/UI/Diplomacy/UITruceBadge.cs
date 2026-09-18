using System;
using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    public class UITruceBadge : MonoBehaviour
    {
        static Scenario observedScenario;
        static Force observer;
        Func<Force> owner;
        GameObject badge;
        float nextRefresh;
        public bool IsVisible => badge != null && badge.activeSelf;

        public static Force Observer
        {
            get
            {
                var scenario = Scenario.Cur;
                if (scenario == null) return null;
                if (observedScenario != scenario) { observedScenario = scenario; observer = null; }
                if (scenario.CurRunForce != null && scenario.CurRunForce.IsPlayer) observer = scenario.CurRunForce;
                if (observer == null || !observer.IsAlive)
                    scenario.forceSet.ForEach(force => { if ((observer == null || !observer.IsAlive) && force.IsPlayer && force.IsAlive) observer = force; });
                return observer;
            }
        }

        public static bool ShouldShow(Force owner, Force viewer)
        {
            return owner != null && viewer != null && owner != viewer && owner.IsAlive && viewer.IsAlive && viewer.IsTruce(owner);
        }

        public static void Bind(Component headbar, Text nameLabel, Func<Force> owner)
        {
            if (nameLabel == null) return;
            var marker = headbar.GetComponent<UITruceBadge>() ?? headbar.gameObject.AddComponent<UITruceBadge>();
            marker.owner = owner;
            if (marker.badge == null)
            {
                marker.badge = new GameObject("TruceBadge", typeof(RectTransform), typeof(Image));
                marker.badge.layer = nameLabel.gameObject.layer;
                var rect = (RectTransform)marker.badge.transform;
                rect.SetParent(nameLabel.rectTransform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 0);
                rect.anchoredPosition = new Vector2(3, 2);
                rect.sizeDelta = new Vector2(24, 24);
                var background = marker.badge.GetComponent<Image>();
                background.color = new Color(0.08f, 0.3f, 0.25f, 0.96f);
                background.raycastTarget = false;
                var outline = marker.badge.AddComponent<Outline>();
                outline.effectColor = new Color(0.88f, 0.88f, 0.73f, 1);
                outline.effectDistance = Vector2.one;
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObject.layer = nameLabel.gameObject.layer;
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var label = labelObject.GetComponent<Text>();
                label.font = nameLabel.font;
                label.fontSize = 20;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = new Color(1, 0.95f, 0.7f);
                label.raycastTarget = false;
                label.text = "停";
            }
            marker.Refresh();
        }

        void OnEnable()
        {
            GameEvent.OnDiplomacyTruce += OnTruce;
            GameEvent.OnForceTurnStart += OnForceTurn;
            nextRefresh = 0;
        }
        void OnDisable()
        {
            GameEvent.OnDiplomacyTruce -= OnTruce;
            GameEvent.OnForceTurnStart -= OnForceTurn;
            if (badge != null) badge.SetActive(false);
        }
        void LateUpdate()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }
        void OnTruce(Force a, Force b, bool accepted) { Refresh(); }
        void OnForceTurn(Force force, Scenario scenario)
        {
            if (force.IsPlayer) { observedScenario = scenario; observer = force; }
            Refresh();
        }
        public void Refresh()
        {
            if (badge != null) badge.SetActive(ShouldShow(owner?.Invoke(), Observer));
        }
    }
}
