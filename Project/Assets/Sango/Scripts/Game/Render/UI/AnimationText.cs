using System.Collections.Generic;
using LuaInterface;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.UI
{
    [ExecuteInEditMode]
    public class AnimationText : MonoBehaviour
    {
        public AnimationCurve offsetCurveX = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(3f, 40f) });
        public AnimationCurve offsetCurveY = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(3f, 40f) });
        public AnimationCurve alphaCurve = new AnimationCurve(new Keyframe[] { new Keyframe(1f, 1f), new Keyframe(3f, 0f) });
        public AnimationCurve scaleCurve = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(0.25f, 1f) });

        //[System.NonSerialized]
        //private AnimationCurve randomCurveX;

        //[System.NonSerialized]
        //private AnimationCurve randomCurveY;

        public Text label;

        [System.NonSerialized]
        public float time = 0;

        [System.NonSerialized]
        public float maxTime = 1;

        //[System.NonSerialized]
        //private float flip = 1;

        public bool useRandomX = false;
        public Vector2 xRandomRange = new Vector2(-50, -10);

        public bool useRandomY = false;
        public Vector2 yRandomRange = new Vector2(50, 200);

        public bool autoCurveX = false;

        public bool useRandomFlipX = false;
        public bool useRandomFlipY = false;

        protected class Entry
        {
            public float time;
            public Text label;
            public Text head_label;
            //public Image image;
            //public Gradient gradient;
            public AnimationCurve randomCurveX;
            public AnimationCurve randomCurveY;
            public float flipX = 1;
            public float flipY = 1;
            public float scale = 1;
        }

        [System.NonSerialized]
        List<Entry> mList = new List<Entry>();

        [System.NonSerialized]
        List<Entry> mUnused = new List<Entry>();

        [System.NonSerialized]
        int counter = 0;

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= UpdateEditor;
                EditorApplication.update += UpdateEditor;
            }
#endif

            //time = 0;
            //float rx = Random.Range(xRandomRange.x, xRandomRange.y);
            //randomCurveX = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(0.25f, rx) });

            //float ry = Random.Range(yRandomRange.x, yRandomRange.y);
            //randomCurveY = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(0.25f, ry) });

            //if (useRandomFlip)
            //    flip = Random.Range(0, 100) > 50 ? -1 : 1;
            //else
            //    flip = 1;

        }

        void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;

            //InvokeRepeating("dump", 0, 0.5f);
#endif
        }

#if UNITY_EDITOR
        [NoToLua]
        public void Dump()
        {
            Create(label.text + Random.Range(1, 10000).ToString(), label.color, label.rectTransform.localScale.x);
        }
#endif

        public void Create(string tex, float scale, string sprName = null)
        {
            Entry ne = CreateEntry();
            if (ne == null) return;
            ne.scale = scale;
            Color color = ne.label.color;
            color.a = 0;
            ne.label.color = color;
            ne.label.text = tex;
            if (!autoCurveX && useRandomX)
            {
                float rx = Random.Range(xRandomRange.x, xRandomRange.y);
                ne.randomCurveX = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(0.25f, rx) });
            }
            else if (autoCurveX)
            {
                //UnityEngine.UI.Follow3DTarget ft = gameObject.GetComponent<UnityEngine.UI.Follow3DTarget>();
                //if (ft == null || ft.target == null || ft.gameCamera == null || ft.uiCamera == null)
                //{

                //}
                //else
                //{
                //    Vector3 localPos = ft.GetCacheTransLocalPosition();
                //    float offset = 0f;
                //    if (localPos.x > 0)
                //    {
                //        offset = 30f;
                //    }
                //    else if (localPos.x < 0)
                //    {
                //        offset = -30f;
                //    }

                //    offsetCurveX = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, offset) });
                //}
            }
            if (useRandomY)
            {
                float ry = Random.Range(yRandomRange.x, yRandomRange.y);
                ne.randomCurveY = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(0.25f, ry) });
            }

            if (useRandomFlipX)
                ne.flipX = Random.Range(0, 100) > 50 ? -1 : 1;
            else
                ne.flipX = 1;

            if (useRandomFlipY)
                ne.flipY = Random.Range(0, 100) > 50 ? -1 : 1;
            else
                ne.flipY = 1;
        }

        public void Create(string tex, string head_tex, float scale)
        {
            Create(tex, head_tex, Color.white, scale);
        }
        public void Create(string tex, float scale)
        {
            Create(tex, "", Color.white, scale);
        }
        public void Create(string tex, Color c, float scale)
        {
            Create(tex, "", c, scale);
        }

        public void Create(string tex, string head_tex, Color c1, float scale)
        {
            Entry ne = CreateEntry();
            if (ne == null) return;
            ne.scale = scale;
            c1.a = 0;
            ne.label.color = c1;
            ne.label.text = tex;
            ne.head_label.text = head_tex;
            if (!autoCurveX && useRandomX)
            {
                float rx = Random.Range(xRandomRange.x, xRandomRange.y);
                ne.randomCurveX = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(0.25f, rx) });
            }
            else if (autoCurveX)
            {
                //UnityEngine.UI.Follow3DTarget ft = gameObject.GetComponent<UnityEngine.UI.Follow3DTarget>();
                //if (ft == null || ft.target == null || ft.gameCamera == null || ft.uiCamera == null)
                //{

                //}
                //else
                //{
                //    Vector3 localPos = ft.GetCacheTransLocalPosition();
                //    float offset = 0f;
                //    if (localPos.x > 0)
                //    {
                //        offset = 30f;
                //    }
                //    else if (localPos.x < 0)
                //    {
                //        offset = -30f;
                //    }

                //    offsetCurveX = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(1f, offset) });
                //}
            }
            if (useRandomY)
            {
                float ry = Random.Range(yRandomRange.x, yRandomRange.y);
                ne.randomCurveY = new AnimationCurve(new Keyframe[] { new Keyframe(0f, 0f), new Keyframe(0.25f, ry) });
            }

            if (useRandomFlipX)
                ne.flipX = Random.Range(0, 100) > 50 ? -1 : 1;
            else
                ne.flipX = 1;

            if (useRandomFlipY)
                ne.flipY = Random.Range(0, 100) > 50 ? -1 : 1;
            else
                ne.flipY = 1;

            //if (!string.IsNullOrEmpty(sprName))
            //{
            //    if (ne.image == null)
            //    {
            //        GameObject go = new GameObject();
            //        go.transform.SetParent(ne.label.transform);
            //        go.transform.localPosition = Vector3.zero;
            //        go.transform.localScale = Vector3.one;
            //        ne.image = go.AddComponent<Image>();
            //    }
            //    ne.image.gameObject.SetActive(true);
            //    ne.image.transform.localScale = Vector3.one;
            //    ImageNode.Load<ImageNode>(ne.image, sprName, (x) =>
            //         {
            //             x.SetNativeSize();
            //             x.transform.localPosition = new Vector3(-0.5f * ne.label.preferredWidth - 0.5f * x.preferredWidth, 0, 0);

            //         });
            //}
            //else if (ne.image != null)
            //{
            //    ne.image.gameObject.SetActive(false);
            //}
        }



        Entry CreateEntry()
        {
            if (mList.Count >= 20)
                return null;

            if (mUnused.Count > 0)
            {
                Entry ent = mUnused[mUnused.Count - 1];
                mUnused.RemoveAt(mUnused.Count - 1);
                ent.time = 0;
                ent.label.gameObject.SetActive(true);
                ent.label.transform.SetAsLastSibling();
                mList.Add(ent);
                return ent;
            }
            Entry ne = new Entry();
            ne.time = 0;// Time.realtimeSinceStartup;
            GameObject go = GameObject.Instantiate(label.gameObject);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            //ne.gradient = go.GetComponent<Gradient>();
            ne.label = go.GetComponent<Text>();
            ne.head_label = go.transform.GetChild(0).GetComponent<Text>();
            ne.label.name = counter.ToString();
            ne.label.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            go.SetActive(true);

#if UNITY_EDITOR
            go.hideFlags = HideFlags.DontSave;
#endif

            mList.Add(ne);
            ++counter;
            return ne;
        }

        void Delete(Entry ent)
        {
            mList.Remove(ent);
            mUnused.Add(ent);
            ent.label.gameObject.SetActive(false);
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update -= UpdateEditor;
#endif
            for (int i = mList.Count; i > 0;)
            {
                Entry ent = mList[--i];
                if (ent.label != null) ent.label.enabled = false;
                else mList.RemoveAt(i);
            }
        }

        void UpdateEditor()
        {
            for (int i = mList.Count; i > 0;)
            {
                Entry ent = mList[--i];
                ent.time = ent.time + Time.deltaTime;

                UpdateEntry(ent, ent.time);

                // Delete the entry when needed
                if (ent.time > maxTime) Delete(ent);
                else ent.label.enabled = true;
            }
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            //updateByTime(time);
            //time = time + Time.deltaTime;
            //if (time >= maxTime)
            //    enabled = false;

            // Adjust alpha and delete old entries
            for (int i = mList.Count; i > 0;)
            {
                Entry ent = mList[--i];
                ent.time = ent.time + Time.deltaTime;

                UpdateEntry(ent, ent.time);

                // Delete the entry when needed
                if (ent.time > maxTime) Delete(ent);
                else ent.label.enabled = true;
            }

        }

        public void UpdateByTime(float t)
        {
            //Color c = label.color;
            //c.a = alphaCurve.Evaluate(t);
            //Transform trans = label.transform;
            //// Make the label scale in
            //float s = scaleCurve.Evaluate(t);
            //if (s < 0.001f) s = 0.001f;
            //
            //trans.localScale = new Vector3(s, s, s);
            //trans.localPosition = new Vector3((offsetCurveX.Evaluate(t) + randomCurveX.Evaluate(t)) * flip, offsetCurveY.Evaluate(t) + randomCurveY.Evaluate(t), 0f);
        }

        void UpdateEntry(Entry en, float t)
        {
            Color c = en.label.color;
            c.a = alphaCurve.Evaluate(t);
            en.label.color = c;
            Transform trans = en.label.transform;
            // Make the label scale in
            float s = scaleCurve.Evaluate(t);
            if (s < 0.001f) s = 0.001f;
            trans.localScale = new Vector3(s, s, s) * en.scale;

            float x = useRandomX && en.randomCurveX != null ? offsetCurveX.Evaluate(t) + en.randomCurveX.Evaluate(t) : offsetCurveX.Evaluate(t);
            float y = useRandomY && en.randomCurveY != null ? offsetCurveY.Evaluate(t) + en.randomCurveY.Evaluate(t) : offsetCurveY.Evaluate(t);

            trans.localPosition = new Vector3(x * en.flipX, y * en.flipY, 0f);
        }
    }
}