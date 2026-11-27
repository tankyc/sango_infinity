using Sango.Core.Player;
using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIObjectPopInfo : UGUIWindow
    {
        public UIMiniBuldingInfoPanel buildingInfoPanel;
        public UIMiniCityInfoPanel cityInfoPanel;
        public UIMiniPortInfoPanel portInfoPanel;
        public UIMiniGateInfoPanel gateInfoPanel;
        public UIMiniTroopInfoPanel troopInfoPanel;

        UIMiniInfoPanel currentPanel;
        SangoObject currentObject;

        public override void OnOpen()
        {
            base.OnOpen();
            ResetAllPanel(null);
            GameController.Instance.onCellOverEnter += OnCellOverEnter;
            GameController.Instance.onCellOverExit += OnCellOverExit;
        }

        void ResetAllPanel(UIMiniInfoPanel except, UIMiniInfoPanel[] uIMiniInfoPanels)
        {
            if (uIMiniInfoPanels == null)
                return;
            for (int i = 0; i < uIMiniInfoPanels.Length; i++)
            {
                UIMiniInfoPanel panel = uIMiniInfoPanels[i];
                if (panel != null && except != panel && panel.gameObject != null && panel.gameObject.activeSelf)
                    panel.gameObject.SetActive(false);
            }
        }

        void ResetAllPanel(UIMiniInfoPanel except)
        {
            // 预制体引用缺失 / 窗口初始化失败时，这些字段可能为 null；之前的实现直接解引用
            // buildingInfoPanel.gameObject，悬停格子就抛 NRE。
            if (buildingInfoPanel != null && except != buildingInfoPanel
                && buildingInfoPanel.gameObject != null && buildingInfoPanel.gameObject.activeSelf)
                buildingInfoPanel.gameObject.SetActive(false);
            if (cityInfoPanel != null && except != cityInfoPanel
                && cityInfoPanel.gameObject != null && cityInfoPanel.gameObject.activeSelf)
                cityInfoPanel.gameObject.SetActive(false);
            if (portInfoPanel != null && except != portInfoPanel
                && portInfoPanel.gameObject != null && portInfoPanel.gameObject.activeSelf)
                portInfoPanel.gameObject.SetActive(false);
            if (troopInfoPanel != null && except != troopInfoPanel
                && troopInfoPanel.gameObject != null && troopInfoPanel.gameObject.activeSelf)
                troopInfoPanel.gameObject.SetActive(false);
            if (gateInfoPanel != null && except != gateInfoPanel
                && gateInfoPanel.gameObject != null && gateInfoPanel.gameObject.activeSelf)
                gateInfoPanel.gameObject.SetActive(false);
            currentPanel = null;
            currentObject = null;
        }

        public override void OnClose()
        {
            GameController.Instance.onCellOverEnter -= OnCellOverEnter;
            GameController.Instance.onCellOverExit -= OnCellOverExit;
            base.OnClose();
        }

        void OnCellOverEnter(Cell cell)
        {
            if (cell == null)
            {
                if (currentPanel != null)
                {
                    ResetAllPanel(null);
                }
                return;
            }


            if (ContextMenu.IsVisible())
            {
                if (currentPanel != null)
                {
                    currentPanel.gameObject.SetActive(false);
                    currentPanel = null;
                    currentObject = null;
                }
                return;
            }


            Vector3 worldPos = cell.Position;

            if (cell.building != null)
            {
                if (currentObject == cell.building)
                    return;
                // 建筑可能处于"已拆除 / 数据残留"状态，CenterCell 为空时退回格子坐标
                worldPos = cell.building.CenterCell != null ? cell.building.CenterCell.Position : cell.Position;
                if (cell.building.IsCity())
                {
                    ResetAllPanel(cityInfoPanel);
                    if (cityInfoPanel != null)
                    {
                        cityInfoPanel.Show(cell.building as City);
                        currentPanel = cityInfoPanel;
                    }
                }
                else if (cell.building.IsPort())
                {
                    ResetAllPanel(portInfoPanel);
                    if (portInfoPanel != null)
                    {
                        portInfoPanel.Show(cell.building as Port);
                        currentPanel = portInfoPanel;
                    }
                }
                else if (cell.building.IsGate())
                {
                    ResetAllPanel(gateInfoPanel);
                    if (gateInfoPanel != null)
                    {
                        gateInfoPanel.Show(cell.building as Gate);
                        currentPanel = gateInfoPanel;
                    }
                }
                else
                {
                    ResetAllPanel(buildingInfoPanel);
                    if (buildingInfoPanel != null)
                    {
                        buildingInfoPanel.Show(cell.building as Building);
                        currentPanel = buildingInfoPanel;
                    }
                }
                currentObject = cell.building;
            }
            else if (cell.troop != null)
            {
                if (currentObject == cell.troop)
                    return;
                ResetAllPanel(troopInfoPanel);
                if (troopInfoPanel != null)
                {
                    troopInfoPanel.Show(cell.troop);
                    currentPanel = troopInfoPanel;
                }
                currentObject = cell.troop;
            }
            else
            {
                ResetAllPanel(null);
            }

            if (currentPanel == null || currentPanel.gameObject == null || currentPanel.root == null)
            {
                return;
            }

            if (!currentPanel.gameObject.activeSelf)
                currentPanel.gameObject.SetActive(true);


            Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            //Sango.Log.Error($"screenPos : {screenPos}");
            Vector2 anchorPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(GetComponent<RectTransform>(),
                screenPos, Game.Instance.UICamera, out anchorPos);

            //Sango.Log.Error($"anchorPos : {anchorPos}");

            bool showUp = screenPos.y < Screen.height / 2;
            bool showLeft = screenPos.x <= (Screen.width / 2);
            //Sango.Log.Error($"showUp : {showUp}");
            //Sango.Log.Error($"showLeft : {showLeft}");
            int offset = 20;
            if (showUp)
            {
                anchorPos.y += offset;
            }
            else
            {
                anchorPos.y -= (currentPanel.root.sizeDelta.y + offset) * currentPanel.root.localScale.y;
            }

            if (showLeft)
            {
                anchorPos.x += offset;
            }
            else
            {
                anchorPos.x -= (currentPanel.root.sizeDelta.x + offset) * currentPanel.root.localScale.x;
            }

            currentPanel.root.anchoredPosition = anchorPos;
        }

        void OnCellOverExit(Cell cell)
        {

        }

        private void Update()
        {
            if (GameSystemManager.Instance.CurrentCommand != null
#if UNITY_ANDROID || UNITY_IPHONE
                && !(GameSystemManager.Instance.CurrentCommand is BuildingSystem)
#endif
                )
            {
                ResetAllPanel(null);
            }
        }

    }
}
