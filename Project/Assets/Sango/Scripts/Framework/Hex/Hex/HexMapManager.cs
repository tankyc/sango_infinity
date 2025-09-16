using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class HexMapManager
{
    public int m_Row;
    public int m_Col;
    public float m_Size;
    public List<HexCell> m_CellList;
    public Dictionary<int, HexCell> m_cellDic = new Dictionary<int, HexCell>();
    public Transform m_parent;
    public HexCell[,] m_XCells;
    public HexMesh m_XHexMesh;

    private static HexMapManager m_Instance;
    public static HexMapManager Instance
    {
        get
        {
            if (m_Instance == null)
            {
                m_Instance = new HexMapManager();
            }
            return m_Instance;
        }
        set
        {
            m_Instance = value;
        }

    }

    public HexMapManager()
    {
        m_CellList = new List<HexCell>();
        //m_parent = GameObject.Find("HexList").transform;
        //m_XHexMesh = GameObject.Find("HexMesh").transform.GetComponent<XHexMesh>();
    }


    public HexCell GetHexCellByIndex(int index)
    {
        if (m_cellDic.ContainsKey(index))
        {
            return m_cellDic[index];
        }

        return null;
       
    }

    public void CreatXHexCell(int row, int col, float size, HEX_STYLE style = HEX_STYLE.HORIZONTAL, HEX_LAYOUT layout = HEX_LAYOUT.ODD)
    {
        m_CellList.Clear();
        HexCommon.HEX_STYLE = style;
        HexCommon.HEX_LAYOUT = layout;
        m_Row = row;
        m_Col = col;
        m_Size = size;


        for (int i = 0; i < row; i++)
        {
            for (int j = 0; j < col; j++)
            {

                int index=i*m_Col + j;
                HexCell xHexCell = new HexCell(i, j, index, m_Size);
                m_CellList.Add(xHexCell);
                m_cellDic[index] = xHexCell;
            }
        }

       


        //m_XCells = new XHexCell[row, col];
        //XHexCommon.m_OuterRadius = size;
        //for (int z = 0; z < row; z++)
        //{
        //    for (int x = 0; x < col; x++)
        //    {
        //        int index = z * col + x;
        //        CreatXHexRender(x, z, index);

        //    }
        //}
        //m_XHexMesh.Triangulate(m_XCells);
    }

    //public void CreatXHexRender(int x, int z,int index)
    //{
    //    Vector3 position = Vector3.zero;
    //    XHexCell cell = m_XCells[z, x] = new XHexCell(z, x, index, true);
    //    m_CellList.Add(cell);
    //    position.y = 0f;

    //    if (XHexCommon.HEX_STYLE == HEX_STYLE.HORIZONTAL)
    //    {
    //        if (XHexCommon.HEX_LAYOUT == HEX_LAYOUT.ODD)
    //        {
    //            //基数行
    //            if (z % 2 != 0)
    //            {
    //                position.x = XHexCommon.m_InnerRadius + x * XHexCommon.m_InnerRadius * 2;
    //                position.z = z * (XHexCommon.m_OuterRadius * 1.5f);
    //            }
    //            else
    //            {
    //                position.x = x * XHexCommon.m_InnerRadius * 2;
    //                position.z = z * (XHexCommon.m_OuterRadius * 1.5f);

    //            }
    //        }
    //        else
    //        {
    //            //基数行
    //            if (z % 2 != 0)
    //            {
    //                position.x = x * XHexCommon.m_InnerRadius * 2 - XHexCommon.m_InnerRadius;
    //                position.z = z * (XHexCommon.m_OuterRadius * 1.5f);
    //            }
    //            else
    //            {
    //                position.x = x * XHexCommon.m_InnerRadius * 2;
    //                position.z = z * (XHexCommon.m_OuterRadius * 1.5f);

    //            }
    //        }
    //    }
    //    else
    //    {
    //        if (XHexCommon.HEX_LAYOUT == HEX_LAYOUT.ODD)
    //        {
    //            //基数列
    //            if (x % 2 != 0)
    //            {

    //                position.x = 1.5f * XHexCommon.m_OuterRadius + (x / 2) * 3 * XHexCommon.m_OuterRadius;
    //                position.z = -XHexCommon.m_InnerRadius + z * (2 * XHexCommon.m_InnerRadius);
    //            }
    //            else
    //            {
    //                position.x = (x + x / 2) * XHexCommon.m_OuterRadius;
    //                position.z = z * (2 * XHexCommon.m_InnerRadius);
    //            }
    //        }
    //        else
    //        {
    //           // 基数列
    //            if (x % 2 != 0)
    //            {

    //                position.x = 1.5f * XHexCommon.m_OuterRadius + (x / 2) * 3 * XHexCommon.m_OuterRadius;
    //                position.z = XHexCommon.m_InnerRadius + z * (2 * XHexCommon.m_InnerRadius);
    //            }
    //            else
    //            {
    //                position.x = (x + x / 2) * XHexCommon.m_OuterRadius;
    //                position.z = z * (2 * XHexCommon.m_InnerRadius);
    //            }
    //        }
    //    }
    //    cell.m_Pos = position;
    //}

    public void Draw()
    {
        //if (m_XCells == null)
        //{
        //    return;
        //}
        //if (m_XCells.GetLength(0) != 0 && m_XCells.GetLength(1) != 0)
        //{
        //    for (int i = 0; i < m_Col; i++)
        //    {
        //        for (int j = 0; j < m_Row; j++)
        //        {

        //            //UnityEditor.Handles.Label(m_XCells[i, j].m_Pos, j + "  " + i);
        //            UnityEditor.Handles.Label(m_XCells[i, j].m_Pos, m_XCells[i, j].m_Q + " " + m_XCells[i, j].m_R);
        //        }
        //    }

        //    for (int z = 0; z < m_Row; z++)
        //    {
        //        for (int x = 0; x < m_Col; x++)
        //        {
        //            UnityEditor.Handles.Label(m_XCells[z, x].m_Pos, m_XCells[z, x].m_Q + " " + m_XCells[z, x].m_R);
        //        }
        //    }
        //}



    }
}
