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
            GameObject.DontDestroyOnLoad(this.gameObject);
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
            tex = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB4444);
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
