using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 地图标注管理类
    /// </summary>
    public class MapLabelSet : MapProperty
    {
        /// <summary>
        /// 标注集合
        /// </summary>
        public List<MapLabel> labels = new List<MapLabel>();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="map">地图渲染器</param>
        public MapLabelSet(MapRender map) : base(map)
        {
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public override void Init()
        {
            base.Init();
            // 初始化标注集合
            labels.Clear();
        }

        /// <summary>
        /// 添加标注
        /// </summary>
        /// <param name="text">标注文字</param>
        /// <param name="position">位置</param>
        /// <param name="color">文字颜色</param>
        /// <param name="fontSize">字号大小</param>
        /// <returns>创建的标注对象</returns>
        public MapLabel AddLabel(string text, Vector3 position, Color32 color, int fontSize)
        {
            // 创建标注游戏对象
            GameObject labelObj = new GameObject($"MapLabel_{labels.Count}");
            labelObj.transform.SetParent(MapRender.modelRoot, false);

            // 添加MapLabel组件
            MapLabel label = labelObj.AddComponent<MapLabel>();
            label.Initialize(map, text, position, color, fontSize);
            // 添加到集合
            labels.Add(label);

            // 添加到地图静态对象
            map.AddStatic(label);

            return label;
        }

        /// <summary>
        /// 移除标注
        /// </summary>
        /// <param name="label">要移除的标注</param>
        public void RemoveLabel(MapLabel label)
        {
            if (labels.Contains(label))
            {
                // 从地图静态对象中移除
                map.Remove(label);

                // 从集合中移除
                labels.Remove(label);

                // 销毁标注对象
                label.Destroy();
            }
        }

        /// <summary>
        /// 移除所有标注
        /// </summary>
        public void ClearLabels()
        {
            foreach (var label in labels)
            {
                map.Remove(label);
                label.Destroy();
            }
            labels.Clear();
        }

        /// <summary>
        /// 根据位置查找标注
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="distance">搜索距离</param>
        /// <returns>找到的标注，未找到返回null</returns>
        public MapLabel FindLabelAtPosition(Vector3 position, float distance = 10f)
        {
            foreach (var label in labels)
            {
                if (Vector3.Distance(label.position, position) <= distance)
                {
                    return label;
                }
            }
            return null;
        }

        /// <summary>
        /// 保存标注数据
        /// </summary>
        /// <param name="writer">二进制写入器</param>
        internal override void OnSave(BinaryWriter writer)
        {
            // 写入标注数量
            writer.Write(labels.Count);

            // 写入每个标注的数据
            foreach (var label in labels)
            {
                writer.Write(label.labelText);
                writer.Write(label.position.x);
                writer.Write(label.position.y);
                writer.Write(label.position.z);
                writer.Write(label.textColor.r);
                writer.Write(label.textColor.g);
                writer.Write(label.textColor.b);
                writer.Write(label.fontSize);
            }
        }

        internal override void OnSaveScale(BinaryWriter writer, int scale)
        {
            // 写入标注数量
            writer.Write(labels.Count);

            // 写入每个标注的数据
            foreach (var label in labels)
            {
                writer.Write(label.labelText);
                writer.Write(label.position.x * scale);
                writer.Write(label.position.y);
                writer.Write(label.position.z * scale);
                writer.Write(label.textColor.r);
                writer.Write(label.textColor.g);
                writer.Write(label.textColor.b);
                writer.Write(label.fontSize);
            }
        }

        /// <summary>
        /// 加载标注数据
        /// </summary>
        /// <param name="versionCode">版本号</param>
        /// <param name="reader">二进制读取器</param>
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            // 清除现有标注
            ClearLabels();

            // 只有版本号>=10时才加载标注数据
            if (versionCode >= 10)
            {
                // 读取标注数量
                int count = reader.ReadInt32();

                // 读取每个标注的数据
                for (int i = 0; i < count; i++)
                {
                    string text = reader.ReadString();
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    byte r = reader.ReadByte();
                    byte g = reader.ReadByte();
                    byte b = reader.ReadByte();
                    int fontSize = reader.ReadInt32();

                    Color32 color = new Color32(r, g, b, 255);
                    Vector3 position = new Vector3(x, y, z);

                    // 创建标注
                    AddLabel(text, position, color, fontSize);
                }
            }
        }

        /// <summary>
        /// 清除资源
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            ClearLabels();
        }

        /// <summary>
        /// 更新渲染
        /// </summary>
        public override void UpdateRender()
        {
            // 可以在这里添加渲染更新逻辑
        }
    }
}