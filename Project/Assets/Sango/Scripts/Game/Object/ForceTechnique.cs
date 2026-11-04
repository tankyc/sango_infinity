using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    public class ForceTechnique
    {
        public Technique technique;
        public List<ForceTechnique> children = new List<ForceTechnique>();
        public int y;
        public int x;
        public int size = 1;

        public ForceTechnique parent;
        int upY = 0;
        int downY = 0;

        public int FillChildren(SangoObjectMap<Technique> techniqueMap)
        {
            int level = technique.level;
            // 【性能】原 ForEach(lambda) 捕获 this / technique / level / upY / downY / children，
            // 这是递归构建科技树的最内层循环，改为遍历 map 的底层字典。
            foreach (KeyValuePair<int, Technique> kv in techniqueMap.objects)
            {
                Technique x = kv.Value;
                if (x != null && x != technique)
                {
                    if (x.needTech == technique.Id)
                    {

                        ForceTechnique forceTechnique = new ForceTechnique()
                        {
                            technique = x,
                            parent = this,
                        };

                        level = UnityEngine.Mathf.Max(level, forceTechnique.FillChildren(techniqueMap));

                        int targetY = 0;
                        int count = children.Count;
                        if (count == 0)
                        {
                            upY = 0;
                            downY = 0;
                        }
                        else if (count % 2 == 0)
                        {
                            targetY = count / 2;
                        }
                        else
                        {
                            targetY = count / 2 - count;
                        }

                        children.Add(forceTechnique);

                    }
                }
            }

            // 计算size
            if (children.Count > 0)
            {
                size = children[0].size;
                for (int i = 1; i < children.Count; i++)
                {
                    size += children[i].size;
                }
            }

            return level;
        }

        public void UpdateY(ref int upY, ref int downY)
        {
            if (children.Count > 0)
            {
                ForceTechnique forceTechnique = children[0];
                forceTechnique.x = x + 1;
                int size = forceTechnique.size;
                forceTechnique.y = y;
                forceTechnique.UpdateY(ref upY, ref downY);
                if (size > 1)
                {
                    if (size % 2 == 0)
                    {
                        int dst = -size / 2;
                        if (upY > dst)
                            upY = dst;
                        dst = size / 2 - 1;
                        if (downY < dst)
                            downY = dst;
                    }
                    else
                    {
                        int dst = size / 2;
                        if (upY > -dst)
                            upY = -dst;
                        if (downY < dst)
                            downY = dst;
                    }
                }
                else
                {
                    if (upY == 0)
                        upY -= 1;
                    if (downY == 0)
                        downY += 1;
                }

                for (int i = 1; i < children.Count; i++)
                {
                    forceTechnique = children[i];
                    forceTechnique.x = x + 1;
                    if (i % 2 == 0)
                    {
                        forceTechnique.y = upY - forceTechnique.size / 2;
                        forceTechnique.UpdateY(ref upY, ref downY);
                        upY -= forceTechnique.size;
                    }
                    else
                    {
                        forceTechnique.y = downY + forceTechnique.size / 2;
                        forceTechnique.UpdateY(ref upY, ref downY);
                        downY += forceTechnique.size;
                    }
                }
            }
        }

        public void GetMinMaxY(ref int min, ref int max)
        {
            int y = GetY();
            min = Mathf.Min(min, y);
            max = Mathf.Max(max, y);
            foreach (ForceTechnique forceTechnique in children)
                forceTechnique.GetMinMaxY(ref min, ref max);
        }

        public int GetY()
        {
            if (parent == null)
                return y;
            return parent.GetY() + y;
        }

        public void ForEach(Action<ForceTechnique> action)
        {
            action(this);
            // 【性能】原 children.ForEach(x => x.ForEach(action)) 的 lambda 捕获 action，
            // 每递归一层就分配一个闭包 + 委托；改为按下标遍历。
            for (int i = 0; i < children.Count; i++)
                children[i].ForEach(action);
        }
    }
}
