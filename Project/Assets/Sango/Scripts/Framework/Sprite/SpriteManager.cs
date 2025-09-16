using LuaInterface;
using Sango.Loader;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Sprite
{

    public class SpriteManager : Sango.Singletion<SpriteManager>
    {
        public delegate void OnSpriteLoaded(string fileName, UnityEngine.Sprite[] obj);

        private Dictionary<string, UnityEngine.Sprite> spriteDic = new Dictionary<string, UnityEngine.Sprite>();
        public class CustomData
        {
            public string fileName;
            public string name;
            public List<SpriteData> spriteDatas = new List<SpriteData>();
            public LuaFunction onLoadedCall;
            public OnSpriteLoaded onCSharpCall;
            public void Add(string spriteName, Rect rect)
            {
                Add(spriteName, rect, new Vector2(0.5f, 0.5f), 100, Vector4.zero);
            }
            public void Add(string spriteName, Rect rect, Vector2 pivot)
            {
                Add(spriteName, rect, pivot, 100, Vector4.zero);

            }
            public void Add(string spriteName, Rect rect, Vector2 pivot, float pixelsPerUnit)
            {
                Add(spriteName, rect, pivot, pixelsPerUnit, Vector4.zero);

            }
            public void Add(string spriteName, Rect rect, Vector2 pivot, Vector4 border)
            {
                Add(spriteName, rect, pivot, 100, border);

            }
            public void Add(string spriteName, Rect rect, Vector4 border)
            {
                Add(spriteName, rect, new Vector2(0.5f, 0.5f), 100, border);
            }
            public void Add(string spriteName, Rect rect, Vector2 pivot, float pixelsPerUnit, Vector4 border)
            {
                spriteDatas.Add(new SpriteData()
                {
                    key = spriteName,
                    rect = rect,
                    pivot = pivot,
                    pixelsPerUnit = pixelsPerUnit,
                    border = border
                });
            }

            private void ProcessLine(string line)
            {
                string[] parts = line.Split(new char[] { ' ' });
                if (parts.Length > 2) {
                    SpriteData sdata = new SpriteData();
                    // ��ȡ��������
                    string[] parts2 = parts[0].Split(new char[] { ';' });
                    sdata.key = parts2[0];
                    sdata.rect = new Rect(int.Parse(parts2[1]), int.Parse(parts2[2]),
                        int.Parse(parts2[3]), int.Parse(parts2[4]));

                    parts2 = parts[1].Split(new char[] { ';' });
                    sdata.pivot = new Vector2(float.Parse(parts2[0]), float.Parse(parts2[1]));

                    parts2 = parts[2].Split(new char[] { ';' });
                    sdata.border = new Vector4(float.Parse(parts2[0]), float.Parse(parts2[1]),
                         float.Parse(parts2[2]), float.Parse(parts2[3]));

                    spriteDatas.Add(sdata);
                }
            }

            public void LoadFormTPSheet(string file)
            {
                using (StreamReader sr = new StreamReader(file)) {
                    string line;
                    // ���ļ���ȡ����ʾ�У�ֱ���ļ���ĩβ 
                    while ((line = sr.ReadLine()) != null) {
                        if (!string.IsNullOrEmpty(line)) {
                            if (line[0] == '#')
                                continue;
                            else if (line[0] == ':')
                                continue;
                            ProcessLine(line);
                        }
                    }
                }
            }

            public void LoadFormString(string text)
            {
                using (StringReader sr = new StringReader(text)) {
                    string line;
                    // ���ļ���ȡ����ʾ�У�ֱ���ļ���ĩβ 
                    while ((line = sr.ReadLine()) != null) {
                        if (!string.IsNullOrEmpty(line)) {
                            if (line[0] == '#')
                                continue;
                            else if (line[0] == ':')
                                continue;
                            ProcessLine(line);
                        }
                    }
                }
            }
        }
        public struct SpriteData
        {
            public string key;
            public Rect rect;
            public Vector2 pivot;
            public float pixelsPerUnit;
            public Vector4 border;
        }
        public static UnityEngine.Sprite Get(string key)
        {
            UnityEngine.Sprite sprite;
            if (SpriteManager.Instance.spriteDic.TryGetValue(key, out sprite))
                return sprite;
            return null;
        }
        public static CustomData CreateLoadData(string fileName)
        {
            CustomData data = new CustomData();
            data.fileName = fileName;
            return data;
        }
        public static void LoadSprite(CustomData data, LuaFunction onLoadedCall, bool needCompress = true)
        {
            data.onLoadedCall = onLoadedCall;
            TextureLoader.LoadFromFile(data.fileName, data, OnTextureLoaded, needCompress);
        }
        public static void LoadSprite(CustomData data, OnSpriteLoaded onLoadedCall, bool needCompress = true)
        {
            data.onCSharpCall = onLoadedCall;
            TextureLoader.LoadFromFile(data.fileName, data, OnTextureLoaded, needCompress);
        }
        public static void OnTextureLoaded(UnityEngine.Object obj, object customData)
        {
            CustomData data = customData as CustomData;
            Texture2D texture = obj as Texture2D;
            if (texture != null) {
                string formatKey = "{0}_{1}";
                List<UnityEngine.Sprite> sprites = new List<UnityEngine.Sprite>();
                for (int i = 0; i < data.spriteDatas.Count; ++i) {
                    SpriteData sd = data.spriteDatas[i];
                    UnityEngine.Sprite sp = UnityEngine.Sprite.Create(texture, sd.rect, sd.pivot, sd.pixelsPerUnit, 0, SpriteMeshType.FullRect, sd.border);
                    if(string.IsNullOrEmpty(sd.key))
                        sd.key = string.Format(formatKey, data.name, i);
                    if (SpriteManager.Instance.spriteDic.TryAdd(sd.key, sp)) {
                        sp.name = sd.key;
                        sprites.Add(sp);
                    }
                }

                if (data.onCSharpCall != null) {
                    data.onCSharpCall(data.fileName, sprites.ToArray());
                }

                if (data.onLoadedCall != null) {
                    data.onLoadedCall.BeginPCall();
                    data.onLoadedCall.Push(data.fileName);
                    data.onLoadedCall.Push(sprites.ToArray());
                    data.onLoadedCall.PCall();
                    data.onLoadedCall.EndPCall();
                }
            }
        }

    }
}
