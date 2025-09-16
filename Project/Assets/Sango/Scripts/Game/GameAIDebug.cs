using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Game
{
    public class GameAIDebug : Singletion<GameAIDebug>
    {
        public static bool Enabled = false;


        bool checkInNextFrame = false;
        public int EnabledForceId = 4;
        public TroopMissionBehaviour troopMissionBehaviour { get; set; }
        List<Cell> moveRange = new List<Cell>();
        Transform textROOT;

        public bool waitForNextStep = false;
        public bool waitForNextTroop = false;
        public void Render()
        {

        }

        public void Init()
        {
            if (!Enabled) return;
            Scenario.Cur.Event.OnTroopAIStart += OnTroopAIStart;
        }
        public bool waitForAIPrepare = false;
        public bool waitForTargetDirectPath = false;
        public void OnTroopAIStart(Troop troop, Scenario scenario)
        {
            if (troop.BelongForce.Id == EnabledForceId)
            {
                waitForAIPrepare = true;
                waitForTargetDirectPath = true;
            }
        }

        public bool WaitForNextStep()
        {
            return waitForNextStep;
        }
        public bool WaitForShowAIPrepare()
        {
            if (!Enabled) return false;

            if (checkInNextFrame)
            {
                waitForAIPrepare = false;
                checkInNextFrame = false;
            }

            if (UnityEngine.Input.GetKeyUp(KeyCode.Return))
            {
                checkInNextFrame = true;
                ExitShowAIPrepare();
            }

            return waitForAIPrepare;
        }
        public void ExitShowAIPrepare()
        {
            if (!Enabled) return;
            foreach (Cell cell in moveRange)
            {
                if (cell != null)
                {
                    MapRender.Instance.SetGridMaskColor(cell.x, cell.y, Color.black);
                }
            }
            MapRender.Instance.EndSetGridMask();
            foreach (CellTextInfo info in usedTextList)
            {
                infoTextList.Add(info.text);
                info.text.gameObject.SetActive(false);
            }
            usedTextList.Clear();
        }

        public void ShowMoveRange(List<Cell> moveRange, Troop troop)
        {
            if (!Enabled) return;
            if (troop.BelongForce.Id != EnabledForceId) return;
            this.moveRange = moveRange;
            foreach (Cell cell in moveRange)
            {
                if (cell != null)
                {
                    MapRender.Instance.SetGridMaskColor(cell.x, cell.y, Color.green);
                }
            }
            MapRender.Instance.EndSetGridMask();
        }


        public bool WaitForTargetDirectPath()
        {
            if (!Enabled) return false;
            if (checkInNextFrame)
            {
                waitForTargetDirectPath = false;
                checkInNextFrame = false;
            }

            if (UnityEngine.Input.GetKeyUp(KeyCode.Return))
            {
                checkInNextFrame = true;
                ExitShowTargetDirectPath();
            }
            return waitForTargetDirectPath;
        }


        public void ShowTargetDirectPath(List<Cell> moveRange, Troop troop)
        {
            if (!Enabled) return;
            if (troop.BelongForce.Id != EnabledForceId) return;
            this.moveRange = moveRange;
            foreach (Cell cell in moveRange)
            {
                if (cell != null)
                {
                    MapRender.Instance.SetRangeMaskColor(cell.x, cell.y, Color.blue);
                }
            }
            MapRender.Instance.EndSetRangeMask();
        }

        public void ExitShowTargetDirectPath()
        {
            if (!Enabled) return;
            foreach (Cell cell in moveRange)
            {
                if (cell != null)
                {
                    MapRender.Instance.SetRangeMaskColor(cell.x, cell.y, Color.clear);
                }
            }
            MapRender.Instance.EndSetRangeMask();
        }

        List<Text> infoTextList = new List<Text>();
        List<CellTextInfo> usedTextList = new List<CellTextInfo>();

        struct CellTextInfo
        {
            public Cell cell;
            public Text text;
        }

        Text CreateInfoObject(Cell cell, int cost)
        {
            Text dest = null;
            if (infoTextList.Count > 0)
            {
                dest = infoTextList[0];
                infoTextList.RemoveAt(0);
                usedTextList.Add(new CellTextInfo { cell = cell, text = dest });
            }
            else
            {
                if (textROOT == null)
                    textROOT = GameObject.Find("GridTextRoot").transform;
                GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("GridText")) as GameObject;
                obj.transform.SetParent(textROOT, false);
                dest = obj.GetComponent<UnityEngine.UI.Text>();
                dest.text = $"cost: {cost}";
                Vector3 pos = cell.Position;
                dest.color = Color.green;
                pos.y = 8f;
                obj.transform.localPosition = pos;
                usedTextList.Add(new CellTextInfo { cell = cell, text = dest });
            }
            return dest;
        }

        public void ShowCellCost(Cell cell, int cost, Troop troop)
        {
            if (!Enabled) return;
            if (troop.BelongForce.Id != EnabledForceId) return;

            bool find = false;
            foreach (CellTextInfo info in usedTextList)
            {
                if (info.cell == cell)
                {
                    info.text.text = $"cost: {cost}";
                    find = true;
                    break;
                }
            }
            if (!find)
                CreateInfoObject(cell, cost);
        }

        public void Update()
        {
        }



    }

}
