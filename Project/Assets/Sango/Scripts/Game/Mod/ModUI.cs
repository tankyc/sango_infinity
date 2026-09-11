//using HybridCLR;

using Sango.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Mod
{
    public class ModUI : MonoBehaviour
    {
        [SerializeField]
        public struct ResourceMap
        {
            public string key;
            public string value;
        }

        [SerializeField]
        public struct ResourceData
        {
            public List<ResourceMap> res_map;
            public int type;
            public object dst;
        }

        [SerializeField]
        List<ResourceData> resources;

        public enum ResourceType
        {
            None = 0,
            Image = 1,
            RawImage = 2,
            Button = 3,
            Mask = 4,
        }


        string Path2ModPath(string resPath)
        {
            if (resPath.Contains("/Resources/"))
            {
                return $"Resources:{resPath.Split("/Resources/")[1]}";
            }
            else if (resPath.Contains("Assets/Mods/"))
            {
                resPath = resPath.Substring(12);
                string modName = resPath.Split('/')[0];
                return $"{modName}:{resPath.Substring(modName.Length + 1)}";
            }
            else
                return resPath;
        }

#if UNITY_EDITOR
        [ContextMenu("export")]
#endif
        public void Save()
        {
#if UNITY_EDITOR
            resources = new List<ResourceData>();
            Image[] img_dst_list = GetComponentsInChildren<Image>();
            if (img_dst_list != null)
            {
                foreach (var dst in img_dst_list)
                {
                    UnityEngine.Sprite sprite = dst.sprite;
                    if (sprite != null)
                    {
                        string src_path = Path2ModPath(AssetDatabase.GetAssetPath(sprite));
                        Sango.Log.Error(src_path);
                        resources.Add(new ResourceData()
                        {
                            res_map = new List<ResourceMap>()
                            {
                                new ResourceMap()
                                {
                                    key = "sprite",
                                    value = sprite.name
                                }
                            },
                            type = 1,
                            dst = dst,
                        });
                    }
                }
            }

            RawImage[] rawimg_dst_list = GetComponentsInChildren<RawImage>();
            if (rawimg_dst_list != null)
            {
                foreach (var dst in rawimg_dst_list)
                {
                    UnityEngine.Texture texture = dst.texture;
                    if (texture != null)
                    {
                        string src_path = Path2ModPath(AssetDatabase.GetAssetPath(texture));
                        Sango.Log.Error(src_path);
                        resources.Add(new ResourceData()
                        {
                            res_map = new List<ResourceMap>()
                            {
                                new ResourceMap()
                                {
                                    key = "texture",
                                    value = texture.name
                                }
                            },
                            type = 2,
                            dst = dst,
                        });
                    }
                }
            }
#endif
        }

        public void Load()
        {

        }
    }
}
