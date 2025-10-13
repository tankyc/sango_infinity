// Generated code -- CC0 -- No Rights Reserved -- http://www.redblobgames.com/grids/hexagons/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Hexagon
{
    
    /// <summary>
    /// 采用的 odd-q 排列
    ///   col------------z---------->
    ///  row  
    ///  |
    ///  |
    ///  |          y is up
    ///  x
    ///  |
    ///  |
    ///  v
    /// </summary>
    public struct Point
    {
        public Point(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
        public readonly float x;
        public readonly float y;
    }

    public struct Hex
    {
        public Hex(int q, int r, int s)
        {
            this.q = q;
            this.r = r;
            this.s = s;
            if (q + r + s != 0) throw new ArgumentException("q + r + s must be 0");
        }
        public readonly int q;
        public readonly int r;
        public readonly int s;

        public bool IsSame(Hex other)
        {
            return (q == other.q && r == other.r && s == other.s);
        }

        public Hex Add(Hex b)
        {
            return new Hex(q + b.q, r + b.r, s + b.s);
        }


        public Hex Subtract(Hex b)
        {
            return new Hex(q - b.q, r - b.r, s - b.s);
        }


        public Hex Scale(int k)
        {
            return new Hex(q * k, r * k, s * k);
        }


        public Hex RotateLeft()
        {
            return new Hex(-s, -q, -r);
        }


        public Hex RotateRight()
        {
            return new Hex(-r, -s, -q);
        }

        static public Hex[] directions = new Hex[] { 
            new Hex(1, 0, -1), 
            new Hex(1, -1, 0), 
            new Hex(0, -1, 1), 
            new Hex(-1, 0, 1), 
            new Hex(-1, 1, 0), 
            new Hex(0, 1, -1) };

        static public Hex Direction(int direction)
        {
            return Hex.directions[direction];
        }
        public int DirectionTo(Hex to)
        {
            Hex dirHex = to.Subtract(this);
            int q_r = dirHex.q - dirHex.r;
            int r_s= dirHex.r - dirHex.s;
            int s_q = dirHex.s - dirHex.q;
            int abs_q_r = Math.Abs(q_r);
            int abs_r_s = Math.Abs(r_s);
            int abs_s_q = Math.Abs(s_q);
            int max = Math.Max(Math.Max(abs_q_r, abs_r_s), abs_s_q);
            if(max == abs_q_r)
            {
                if(q_r > 0)
                {
                    return 1;
                }
                else
                {
                    return 4;
                }
            }
            else if(max == abs_r_s)
            {
                if (r_s > 0)
                {
                    return 5;
                }
                else
                {
                    return 2;
                }
            }
            else
            {
                if (s_q > 0)
                {
                    return 3;
                }
                else
                {
                    return 0;
                }
            }
        }

        public Hex Neighbor(int direction)
        {
            return Add(Hex.Direction(direction));
        }
        static public Hex[] diagonals = new Hex[] { new Hex(2, -1, -1), new Hex(1, -2, 1), new Hex(-1, -1, 2), new Hex(-2, 1, 1), new Hex(-1, 2, -1), new Hex(1, 1, -2) };

        public Hex DiagonalNeighbor(int direction)
        {
            return Add(Hex.diagonals[direction]);
        }


        public int Length()
        {
            return (int)((Math.Abs(q) + Math.Abs(r) + Math.Abs(s)) / 2);
        }


        public int Distance(Hex b)
        {
            return Math.Max(Math.Max(Math.Abs(q - b.q), Math.Abs(r - b.r)), Math.Abs(s - b.s));
        }

        public void Ring(int radius, List<Hex> result, int startDir = 4)
        {
            Hex start = Add(Hex.Direction(startDir).Scale(radius));
            int dir = startDir - 4;
            for (int i = 1; i < 6; i++)
            {
                int dir_i = dir + i;
                if (dir_i < 0)
                {
                    dir_i += 6;
                }
                else if (dir_i > 5)
                {
                    dir_i -= 6;
                }
                for (int j = 0; j < radius; j++)
                {
                    result.Add(start);
                    start = start.Neighbor(dir_i);
                }
            }
        }

        public void Ring(int radius, Action<Hex> action, int startDir = 3)
        {
            if (radius == 0)
            {
                action?.Invoke(this);
                return;
            }

            Hex start = Add(Hex.Direction(startDir).Scale(radius));
            int dir = startDir - 4;
            for (int i = 0; i < 6; i++)
            {
                int dir_i = dir + i;
                if (dir_i < 0)
                {
                    dir_i += 6;
                }
                else if (dir_i > 5)
                {
                    dir_i -= 6;
                }
                for (int j = 0; j < radius; j++)
                {
                    action?.Invoke(start);
                    start = start.Neighbor(dir_i);
                }
            }
        }

        public void Spiral(int radius, List<Hex> result, int startDir = 4)
        {
            result.Add(this);
            for (int i = 1; i <= radius; i++)
            {
                Ring(i, result, startDir);
            }
        }

        public void Spiral(int radius, Action<Hex> action, int startDir = 4)
        {
            action?.Invoke(this);
            for (int i = 1; i <= radius; i++)
            {
                Ring(i, action, startDir);
            }
        }
    }
    public struct Coord
    {
        public Coord(int col, int row)
        {
            this.col = col;
            this.row = row;
        }
        public readonly int col;
        public readonly int row;

        static public Coord OffsetFromCube(Hex h)
        {
            int col = h.q;
            int row = h.r + ((h.q - (h.q & 1)) / 2);
            return new Coord(col, row);
        }
        static public Hex OffsetToCube(Coord h)
        {
            int q = h.col;
            int r = h.row - ((h.col - (h.col & 1)) / 2);
            int s = -q - r;
            return new Hex(q, r, s);
        }
        static public Hex OffsetToCube(int col, int row)
        {
            int q = col;
            int r = row - ((col - (col & 1)) / 2);
            int s = -q - r;
            return new Hex(q, r, s);
        }
    }
    public struct Layout
    {
        public Layout(Point size, Point origin)
        {
            this.size = size;
            this.origin = origin;
        }
        public readonly Point size;
        public readonly Point origin;
        public Point HexToPixel(Hex h)
        {
            Coord offset = Coord.OffsetFromCube(h);
            float x = offset.col * size.x + size.y * 0.5f;
            float y = offset.row * size.y + (offset.col & 1) * size.y * 0.5f + size.y * 0.5f;
            return new Point(x + origin.x, y + origin.y);
        }

        public Coord PixelToOffset(Point p)
        {
            int col = (int)Math.Floor((p.x - origin.x) / size.x);
            int row = (int)Math.Floor((p.y - origin.y - (col & 1) * size.y * 0.5) / size.y);
            return new Coord(col, row);
        }

        public Hex PixelToHex(Point p)
        {
            return Coord.OffsetToCube(PixelToOffset(p));
        }
    }

    public class HexWorld
    {
        public Layout layout;
        public HexWorld(Point size, Point origin)
        {
            InitLayout(size, origin);
        }
        public void InitLayout(Point size, Point origin)
        {
            layout = new Layout(size, origin);
        }

        public Hex CoordsToCube(int x, int y)
        {
            return Coord.OffsetToCube(x, y);
        }
        public Vector3 CubToPosition(Hex h)
        {
            Point p = layout.HexToPixel(h);
            return new Vector3((float)p.y, 0, (float)p.x);
        }
        public Vector3 CoordsToPosition(int x, int y)
        {
            Point p = layout.HexToPixel(CoordsToCube(x, y));
            return new Vector3((float)p.y, 0, (float)p.x);
        }
        public Vector2Int PositionToCoords(Vector3 pos)
        {
            Coord coords = layout.PixelToOffset(new Point(pos.z, pos.x));
            return new Vector2Int(coords.col, coords.row);
        }
        public Hex PositionToHex(Vector3 pos)
        {
            return layout.PixelToHex(new Point(pos.z, pos.x));
        }
    }
}