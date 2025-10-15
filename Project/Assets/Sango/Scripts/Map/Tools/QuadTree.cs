using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Sango.Tools
{
    public struct Rect : IEquatable<Rect>, IFormattable
    {
        private float m_XMin;

        private float m_YMin;

        private float m_Width;

        private float m_Height;

        public static Rect zero => new Rect(0f, 0f, 0f, 0f);

        public float x
        {
            get
            {
                return m_XMin;
            }
            set
            {
                m_XMin = value;
            }
        }

        public float y
        {
            get
            {
                return m_YMin;
            }
            set
            {
                m_YMin = value;
            }
        }

        public Vector2 position
        {
            get
            {
                return new Vector2(m_XMin, m_YMin);
            }
            set
            {
                m_XMin = value.x;
                m_YMin = value.y;
            }
        }

        public Vector2 center
        {
            get
            {
                return new Vector2(x + m_Width / 2f, y + m_Height / 2f);
            }
            set
            {
                m_XMin = value.x - m_Width / 2f;
                m_YMin = value.y - m_Height / 2f;
            }
        }

        public Vector2 min
        {
            get
            {
                return new Vector2(xMin, yMin);
            }
            set
            {
                xMin = value.x;
                yMin = value.y;
            }
        }

        public Vector2 max
        {
            get
            {
                return new Vector2(xMax, yMax);
            }
            set
            {
                xMax = value.x;
                yMax = value.y;
            }
        }

        public float width
        {
            get
            {
                return m_Width;
            }
            set
            {
                m_Width = value;
            }
        }

        public float height
        {
            get
            {
                return m_Height;
            }
            set
            {
                m_Height = value;
            }
        }

        public Vector2 size
        {
            get
            {
                return new Vector2(m_Width, m_Height);
            }
            set
            {
                m_Width = value.x;
                m_Height = value.y;
            }
        }

        public float xMin
        {
            get
            {
                return m_XMin;
            }
            set
            {
                float xMax = this.xMax;
                m_XMin = value;
                m_Width = xMax - m_XMin;
            }
        }

        public float yMin
        {
            get
            {
                return m_YMin;
            }
            set
            {
                float yMax = this.yMax;
                m_YMin = value;
                m_Height = yMax - m_YMin;
            }
        }

        public float xMax
        {
            get
            {
                return m_Width + m_XMin;
            }
            set
            {
                m_Width = value - m_XMin;
            }
        }

        public float yMax
        {
            get
            {
                return m_Height + m_YMin;
            }
            set
            {
                m_Height = value - m_YMin;
            }
        }

        public Rect(float x, float y, float width, float height)
        {
            m_XMin = x;
            m_YMin = y;
            m_Width = width;
            m_Height = height;
        }

        public Rect(Vector2 position, Vector2 size)
        {
            m_XMin = position.x;
            m_YMin = position.y;
            m_Width = size.x;
            m_Height = size.y;
        }

        public Rect(Rect source)
        {
            m_XMin = source.m_XMin;
            m_YMin = source.m_YMin;
            m_Width = source.m_Width;
            m_Height = source.m_Height;
        }

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax)
        {
            return new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
        }

        public void Set(float x, float y, float width, float height)
        {
            m_XMin = x;
            m_YMin = y;
            m_Width = width;
            m_Height = height;
        }

        public bool Contains(Vector2 point)
        {
            return point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
        }

        public bool Contains(Vector3 point)
        {
            return point.x >= xMin && point.x < xMax && point.y >= yMin && point.y < yMax;
        }

        public bool Contains(Rect other)
        {
            return other.xMin >= xMin && other.yMin >= yMin && other.xMax <= xMax && other.yMax <= yMax;
        }

        public bool Contains(Vector3 point, bool allowInverse)
        {
            if (!allowInverse) {
                return Contains(point);
            }

            bool flag = (width < 0f && point.x <= xMin && point.x > xMax) || (width >= 0f && point.x >= xMin && point.x < xMax);
            bool flag2 = (height < 0f && point.y <= yMin && point.y > yMax) || (height >= 0f && point.y >= yMin && point.y < yMax);
            return flag && flag2;
        }

        private static Rect OrderMinMax(Rect rect)
        {
            if (rect.xMin > rect.xMax) {
                float xMin = rect.xMin;
                rect.xMin = rect.xMax;
                rect.xMax = xMin;
            }

            if (rect.yMin > rect.yMax) {
                float yMin = rect.yMin;
                rect.yMin = rect.yMax;
                rect.yMax = yMin;
            }

            return rect;
        }

        public bool Overlaps(Rect other)
        {
            return other.xMax > xMin && other.xMin < xMax && other.yMax > yMin && other.yMin < yMax;
        }

        public bool Overlaps(Rect other, bool allowInverse)
        {
            Rect rect = this;
            if (allowInverse) {
                rect = OrderMinMax(rect);
                other = OrderMinMax(other);
            }

            return rect.Overlaps(other);
        }

        public static Vector2 NormalizedToPoint(Rect rectangle, Vector2 normalizedRectCoordinates)
        {
            return new Vector2(Mathf.Lerp(rectangle.x, rectangle.xMax, normalizedRectCoordinates.x), Mathf.Lerp(rectangle.y, rectangle.yMax, normalizedRectCoordinates.y));
        }

        public static Vector2 PointToNormalized(Rect rectangle, Vector2 point)
        {
            return new Vector2(Mathf.InverseLerp(rectangle.x, rectangle.xMax, point.x), Mathf.InverseLerp(rectangle.y, rectangle.yMax, point.y));
        }

        public static bool operator !=(Rect lhs, Rect rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator ==(Rect lhs, Rect rhs)
        {
            return lhs.x == rhs.x && lhs.y == rhs.y && lhs.width == rhs.width && lhs.height == rhs.height;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ (width.GetHashCode() << 2) ^ (y.GetHashCode() >> 2) ^ (height.GetHashCode() >> 1);
        }

        public override bool Equals(object other)
        {
            if (!(other is Rect)) {
                return false;
            }

            return Equals((Rect)other);
        }

        public bool Equals(Rect other)
        {
            return x.Equals(other.x) && y.Equals(other.y) && width.Equals(other.width) && height.Equals(other.height);
        }

        public override string ToString()
        {
            return ToString(null, null);
        }

        public string ToString(string format)
        {
            return ToString(format, null);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format)) {
                format = "F2";
            }

            if (formatProvider == null) {
                formatProvider = CultureInfo.InvariantCulture.NumberFormat;
            }

            return String.Format("(x:{0}, y:{1}, width:{2}, height:{3})", x.ToString(format, formatProvider), y.ToString(format, formatProvider), width.ToString(format, formatProvider), height.ToString(format, formatProvider));
        }
    }

    internal class QuadTree2DNode<T>
    {
        public const int LT = 0;
        public const int RT = 1;
        public const int LB = 2;
        public const int RB = 3;
        public const int MAX = 4;
        public Rect bound;
        public List<T> dataList = new List<T>();
        public List<Rect> boundList = new List<Rect>();
        public QuadTree2DNode<T> parent;
        public QuadTree2DNode<T>[] childs;
        public QuadTree2DNode(Rect bound, QuadTree2DNode<T> parent = null)
        {
            this.bound = bound;
            this.parent = parent;
        }
    }

    public class QuadTree2D<T>
    {
        private int maxDeep;

        private QuadTree2DNode<T> root;

        public QuadTree2D(Rect bound, int maxDeep = 31) //todo 可以改成传一个Rect
        {
            this.maxDeep = maxDeep;
            root = new QuadTree2DNode<T>(bound);
        }
        private void _Add(QuadTree2DNode<T> node, T data, Rect rect, int deep) //放到最深可以完全包含Rect的QuadTree2DNode里
        {
            Rect pRect = node.bound;
            if (deep < maxDeep) {
                //创建子节点
                if (node.childs == null) {
                    node.childs = new QuadTree2DNode<T>[4];
                    float nwidth = pRect.width / 2;
                    float nheight = pRect.height / 2;

                    node.childs[QuadTree2DNode<T>.LT] = new QuadTree2DNode<T>(new Rect(pRect.xMin, pRect.yMin, nwidth, nheight), node);
                    node.childs[QuadTree2DNode<T>.RT] = new QuadTree2DNode<T>(new Rect(pRect.xMin + nwidth, pRect.yMin, nwidth, nheight), node);
                    node.childs[QuadTree2DNode<T>.LB] = new QuadTree2DNode<T>(new Rect(pRect.xMin, pRect.yMin + nheight, nwidth, nheight), node);
                    node.childs[QuadTree2DNode<T>.RB] = new QuadTree2DNode<T>(new Rect(pRect.xMin + nwidth, pRect.yMin + nheight, nwidth, nheight), node);
                }

                for (int i = 0; i < 4; i++) {
                    QuadTree2DNode<T> child = node.childs[i];
                    if (child.bound.Contains(rect)) {
                        //被该子节点完全覆盖，就放到当前节点
                        _Add(child, data, rect, deep + 1);
                        return;
                    }
                }
            }
            //不能被任何一个子节点完全覆盖，就放到当前节点
            node.dataList.Add(data);
            node.boundList.Add(rect);
        }

        private void _Remove(QuadTree2DNode<T> node, T data, Rect rect, int deep) //放到最深可以完全包含Rect的QuadTree2DNode里
        {
            Rect pRect = node.bound;
            if (deep < maxDeep)
            {
                //创建子节点
                if (node.childs != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        QuadTree2DNode<T> child = node.childs[i];
                        if (child.bound.Contains(rect))
                        {
                            //被该子节点完全覆盖，就放到当前节点
                            _Remove(child, data, rect, deep + 1);
                            return;
                        }
                    }
                }
            }
            //不能被任何一个子节点完全覆盖，就放到当前节点
            int objIndex = node.dataList.IndexOf(data);
            if (objIndex >= 0)
            {
                node.dataList.RemoveAt(objIndex);
                node.boundList.RemoveAt(objIndex);
            }
        }

        private void _Find(QuadTree2DNode<T> node, Rect rect, List<T> list, bool accuracy) //把所有和Rect相交的QuaTreeNode里的T加入list
        {
            int dataCount = node.dataList.Count;
            if (dataCount > 0) {
                if (accuracy) {
                    for (int i = 0; i < dataCount; i++) {
                        if (node.boundList[i].Overlaps(rect)) {
                            list.Add(node.dataList[i]);
                        }
                    }
                }
                else {
                    for (int i = 0; i < dataCount; i++) {
                        list.Add(node.dataList[i]);
                    }
                }

            }
            //创建子节点
            if (node.childs == null) {
                return;
            }

            //如果相交，就继续递归
            for (int i = 0; i < 4; i++) {
                QuadTree2DNode<T> child = node.childs[i];
                if (child.bound.Overlaps(rect)) {
                    _Find(child, rect, list, accuracy);
                }
            }
        }
        
        private void _Find(QuadTree2DNode<T> node, Rect rect, ref T[] list, ref int totalCount, bool accuracy) //把所有和Rect相交的QuaTreeNode里的T加入list
        {
            int dataCount = node.dataList.Count;
            if (dataCount > 0) {
                if (accuracy) {
                    for (int i = 0; i < dataCount; i++) {
                        if (node.boundList[i].Overlaps(rect)) {
                            if (totalCount >= list.Length) {
                                Array.Resize(ref list, list.Length * 2);
                            }
                            list[totalCount] = node.dataList[i];
                            totalCount++;
                        }
                    }
                }
                else {
                    for (int i = 0; i < dataCount; i++) {
                        if (totalCount >= list.Length) {
                            Array.Resize(ref list, list.Length * 2);
                        }
                        list[totalCount] = node.dataList[i];
                        totalCount++;
                    }
                }

            }
            //创建子节点
            if (node.childs == null) {
                return;
            }

            //如果相交，就继续递归
            for (int i = 0; i < 4; i++) {
                QuadTree2DNode<T> child = node.childs[i];
                if (child != null && child.bound.Overlaps(rect)) {
                    _Find(child, rect, ref list, ref totalCount, accuracy);
                }
            }
        }

        public void Add(T data, Rect rect)
        {
            _Add(root, data, rect, 1);
        }

        public void Remove(T data, Rect rect)
        {
            _Remove(root, data, rect, 1);
        }

        public void Find(Rect rect, List<T> list, bool accuracy = false) //找到的结果不代表一定相交，只是可能相交
        {
            _Find(root, rect, list, accuracy);
        }
        
        public int Find(Rect rect, ref T[] list, bool accuracy = false) //找到的结果不代表一定相交，只是可能相交
        {
            int count = 0;
            _Find(root, rect, ref list, ref count, accuracy);
            return count;
        }
    }
}