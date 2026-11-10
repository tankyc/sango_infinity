using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango
{
    public class TextFactory : MonoBehaviour
    {
        public UnityEngine.Canvas canvas;
        public UnityEngine.UI.Text text;
        public Camera textCamera;
        public Font font;
        public Dictionary<string, Dictionary<int, RenderTexture>> textMap = new Dictionary<string, Dictionary<int, RenderTexture>>();

        /// <summary>
        /// 某张文字贴图渲染完成的通知（参数为该字对应的 RenderTexture）。
        ///
        /// 【用途】旗帜的文字图集（FlagTextAtlas）需要把 <see cref="GetTexture"/> 产出的
        /// 单字 RT 复制进自己的大图集。GetTexture 是"先返回空 RT、稍后由协程渲染"的异步语义，
        /// 所以图集必须等这个回调到了再拷，否则拷到的是一张空白图。
        /// </summary>
        public static event System.Action<RenderTexture> onTextureRendered;

        public static TextFactory _instance;
        public static TextFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = GameObject.Instantiate(Resources.Load("TextFactory")) as GameObject;
                    GameObject.DontDestroyOnLoad(go);
                    _instance = go.GetComponent<TextFactory>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            _instance = this;
            StartCoroutine("RenderText");
        }

        struct renderData
        {
            public string str;
            public int size;
            public RenderTexture tex;
        }

        Queue<renderData> renderList = new Queue<renderData>();

        IEnumerator RenderText()
        {
            while (true)
            {
                if(renderList.Count > 0)
                {
                    renderData data = renderList.Dequeue();
                    text.text = data.str;
                    text.font = font;
                    text.fontSize = data.size;
                    textCamera.enabled = true;
                    textCamera.targetTexture = data.tex;
                    textCamera.Render();
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                    textCamera.enabled = false;
                    textCamera.targetTexture = null;

                    // 此刻 RT 内容已经就绪，通知等待方（旗帜文字图集）
                    if (onTextureRendered != null)
                        onTextureRendered(data.tex);
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                }
            }
        }

        public Texture GetTexture(string str, int size)
        {
            Dictionary<int, RenderTexture> sizeDic = null;
            if (!textMap.TryGetValue(str, out sizeDic))
            {
                sizeDic = new Dictionary<int, RenderTexture>();
                textMap.Add(str, sizeDic);
            }

            RenderTexture tex;
            if (sizeDic.TryGetValue(size, out tex))
            {
                return tex;
            }
            tex = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGBHalf);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            renderList.Enqueue(new renderData
            {
                tex = tex,
                size = size,
                str = str,
            });
            sizeDic.Add(size, tex);
            return tex;
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, Dictionary<int, RenderTexture>> vs in textMap)
            {
                foreach (KeyValuePair<int, RenderTexture> ir in vs.Value)
                {
                    RenderTexture.Destroy(ir.Value);
                }
                vs.Value.Clear();
            }
            textMap.Clear();
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
