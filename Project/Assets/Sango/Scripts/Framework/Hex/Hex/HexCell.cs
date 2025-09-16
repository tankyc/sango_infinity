using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class HexCell
{

    float SIN30 = Mathf.Sin(30 * Mathf.Deg2Rad);
    float COS30 = Mathf.Cos(30 * Mathf.Deg2Rad);

    public Vector3 m_Pos;
    public int m_Col;
    public int m_Row;
    public int index;
    public int x;
    public int y;


    public bool m_IsRoadBlock = false;
    public bool canFormation=true;
    public bool canMove=true;
    public int tipId=-1;


    //public XHexCell(int q,int r)
    //{
    //    this.m_Q = q;
    //    this.m_R = r;
    //    this.m_S = -q - r ;
    //}

    //public XHexCell(int q, int r,Vector3 v )
    //{
    //    this.m_Pos = v;
    //}

    public HexCell(int row, int col ,int index,float size)
    {
        this.m_Col = col;
        this.m_Row = row;
        this.index = index;

        bool flag = row % 2 == 1;

        int x = col*2;
        if (flag)
        { 
            x=col*2+1;
        }
        int y = row;

        float px = x * size * COS30 + size * COS30;
        float py = y * (size +size*SIN30) + size;

        m_Pos = new Vector3(px, 0, py);
       }

    //public XHexCell Add(XHexCell b)
    //{
    //    return new XHexCell(m_Q + b.m_Q, m_R + b.m_R);
    //}

    //public XHexCell Subtract(XHexCell b)
    //{
    //    return new XHexCell(m_Q - b.m_Q, m_R - b.m_R);
    //}

    //public XHexCell Scale(int k)
    //{
    //    return new XHexCell(m_Q * k, m_R * k);
    //}

    //public XHexCell RotaLeft()
    //{
    //    return new XHexCell(-m_S, -m_Q);
    //}

    //public XHexCell RotateRight()
    //{
    //    return new XHexCell(-m_R, -m_S);
    //}

    //public XHexCell Left()
    //{
    //    return new XHexCell(-1, 0);
    //}

    //public XHexCell Right()
    //{
    //    return new XHexCell(1, 0);
    //}

    //public XHexCell UpLeft()
    //{
    //    return new XHexCell(0, -1);
    //}

    //public XHexCell UpRgiht()
    //{
    //    return new XHexCell(1, -1);
    //}

    //public XHexCell DownLeft()
    //{
    //    return new XHexCell(-1, 1);
    //}

    //public XHexCell DownRight()
    //{
    //    return new XHexCell(0, +1);
    //}
    //public int Length()
    //{
    //    return (int)((Math.Abs(m_Q) + Math.Abs(m_R) + Math.Abs(m_S)) / 2);
    //}

    //public int Distance(XHexCell h)
    //{
    //    return Subtract(h).Length();
    //}


















}
