using Sango.Render;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango
{
    /// <summary>
    /// 集群2D小兵部队渲染器,采用GUPInstance渲染
    /// </summary>
    public class TroopsRender : MonoBehaviour
    {

        /// <summary>
        /// 顶点偏移,以底部中间为基准
        /// </summary>
        public static Vector3[] offset =
        {
            new Vector2(-0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(-0.5f, 1f)
        };
        public static Vector2[] uvs =
        {
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        private float[] mRandomSmoothTime;
        private bool mSmoothFlag = false;
        private Vector3 mSmoothPosition;
        private float mSmoothTime = 0.3f;
        private float mCurSmoothTime = 0f;

        public static int[] tangents = { 0, 1, 3, 1, 2, 3 };
        private Vector3 lastPosition = Vector3.zero;
        private Vector3[] mPositions;
        private Vector3[] mHexPositions;
        Matrix4x4[] _matrixes;
        float[] _ani_start_time;
        MaterialPropertyBlock _mpb;
        private Mesh mesh;
        public Material material;
        Material _instanceMaterial;
        private float meshScale = 1;
        private int elementCount = 60;
        public int showCount = 60;
        public ParticleSystem smoke;

        public void SetShowCount(int count)
        {
            if (count < 0 || count >= elementCount)
                return;
            showCount = count;
        }
        public void SetShowPercent(float p)
        {
            p = Mathf.Clamp01(p);
            int count = Mathf.CeilToInt(p * elementCount);
            if (count < 0 || count >= elementCount)
                return;
            showCount = count;
        }
        public void SetMat(Material mat)
        {
            material = mat;
        }
        public void SetMeshScale(float s)
        {
            meshScale = s;
            UpdateMatrixes();
        }
        public void SetSize(Vector2 s)
        {
            size = s;
            origin = size * -0.5f;

            if (mesh == null) return;

            int count = elementCount;
            Vector3 srcScale = transform.lossyScale;
            for (int i = 0; i < count; ++i)
            {
                mPositions[i] = transform.rotation * Vector3.Scale(HexToPosition(hexList[i]), srcScale);
            }
            UpdateMatrixes();
        }

        public void UpdateHexPosition()
        {
            if (mesh == null) return;
            int count = elementCount;
            Vector3 srcScale = transform.lossyScale;
            for (int i = 0; i < count; ++i)
            {
                mHexPositions[i] = Vector3.Scale(HexToPosition(hexList[i]), srcScale);
            }
        }

        public void UpdateTroopsPosition()
        {
            if (mesh == null) return;
            int count = elementCount;
            for (int i = 0; i < count; ++i)
            {
                mPositions[i] = transform.rotation * mHexPositions[i];
            }
        }

        public void SetMainTexture(Texture tex)
        {
            material.mainTexture = tex;
        }
        public void SetMaskTexture(Texture tex)
        {
            material.SetTexture("_MaskTex", tex);
        }
        public void SetAlpha(float a)
        {
            material.SetFloat("_Alpha", a);
        }
        public void SetMaskColor(Color c)
        {
            material.SetColor("_MaskColor", c);
        }

        public void SetGrid(int c, int cmax, int r)
        {
            material.SetFloat("_HorizontalAmount", c);
            material.SetFloat("_HorizontalMax", cmax);
            material.SetFloat("_VerticalAmount", r);
        }

        public void SetSpeed(float s)
        {
            material.SetFloat("_Speed", s);
        }

        public void SetAniData(Texture main, Texture mask, int c, int cmax, int r, float time, float scale)
        {
            SetMainTexture(main);
            SetMaskTexture(mask);
            SetGrid(c, cmax, r);
            SetSpeed(time);
            SetMeshScale(scale);
        }

        void UpdateMatrixes()
        {
            if (mesh == null) return;

            Vector3 srcPosition = transform.position;
            Vector3 srcScale = transform.lossyScale;
            for (int i = 0; i < _matrixes.Length; i++)
            {
                var mx = _matrixes[i];
                mx.SetTRS(
                    srcPosition + mPositions[i],
                    Quaternion.identity,
                    srcScale * meshScale
                    );
                _matrixes[i] = mx;
            }
        }

        List<Hexagon.Hex> hexList;
        void InitMesh()
        {
            if (SystemInfo.supportsInstancing)
            {
                material.enableInstancing = true;
            }

            mesh = new Mesh();
            mesh.vertices = offset;
            mesh.uv = uvs;
            mesh.triangles = tangents;
            mesh.RecalculateBounds();

            Vector3 srcPosition = transform.position;
            Vector3 srcScale = transform.lossyScale;

            mSmoothPosition = srcPosition;

            int count = elementCount;
            mPositions = new Vector3[count];
            mHexPositions = new Vector3[count];
            mRandomSmoothTime = new float[count];
            hexList = new List<Hexagon.Hex>();
            GetTargetHex(count, hexList);

            UpdateHexPosition();
            UpdateTroopsPosition();
            _matrixes = new Matrix4x4[count];
            for (int i = 0; i < _matrixes.Length; i++)
            {
                _matrixes[i] = new Matrix4x4();
            }
            UpdateMatrixes();
            _ani_start_time = new float[count];
            for (int i = 0; i < count; i++)
            {
                _ani_start_time[i] = UnityEngine.Random.Range(0.0f, 0.5f);
                mRandomSmoothTime[i] = UnityEngine.Random.Range(0.0f, mSmoothTime);
            }

            _mpb = new MaterialPropertyBlock();
            _mpb.SetFloatArray("_StartTime", _ani_start_time);
        }

        void DrawMesh()
        {
            if (mesh == null) return;

            if (SystemInfo.supportsInstancing)
            {
                Graphics.DrawMeshInstanced(mesh, 0, material, _matrixes, showCount, _mpb, ShadowCastingMode.Off, false, 0);
            }
            else
            {
                for (int i = 0; i < showCount; i++)
                {
                    material.SetFloat("_StartTime", _ani_start_time[i]);
                    Graphics.DrawMesh(mesh, _matrixes[i], material, 0);
                }
            }
        }

        public void SetForword(Vector3 forward)
        {
            if (transform.forward != forward)
            {
                transform.forward = forward;
                UpdatePosition();
            }
        }
        bool isInitPos = false;
        private void OnEnable()
        {
            isInitPos = true;
        }

        public void SetSmokeShow(bool b)
        {
            if (smoke != null)
            {
                if (b)
                {
                    if (!smoke.isPlaying)
                        smoke.Play();
                }
                else
                {
                    if (smoke.isPlaying)
                        smoke.Stop();
                }
            }
        }

        void UpdatePosition()
        {
            Vector3 nowPosition = transform.position;
            //if (isInitPos)
            //{
            //    mSmoothPosition = nowPosition;
            //    isInitPos = false;
            //}


            ////if (lastPosition != nowPosition)
            //{
            //    if (!mSmoothFlag && Vector3.Distance(mSmoothPosition, nowPosition) > 1f)
            //    {
            //        mSmoothFlag = true;
            //        mCurSmoothTime = 0;
            //        for (int i = 0; i < mRandomSmoothTime.Length; i++)
            //        {
            //            mRandomSmoothTime[i] = UnityEngine.Random.Range(0.0f, mSmoothTime);
            //        }
            //    }

            //    if (mSmoothFlag)
            //    {
            //        mCurSmoothTime = mCurSmoothTime + Time.deltaTime;
            //        Vector3 srcScale = transform.lossyScale;

            //        for (int i = 0; i < showCount; i++)
            //        {
            //            var mx = _matrixes[i];
            //            Vector3 targetPos = nowPosition + mPositions[i];
            //            Vector3 srcPos = mSmoothPosition + mPositions[i];

            //            float p = mCurSmoothTime / mRandomSmoothTime[i];
            //            Vector3 destP = Vector3.Lerp(srcPos, targetPos, Mathf.Pow(p, 2));

            //            float height;
            //            if (!Map.Draw.Map.QueryHeight(destP, out height))
            //            {
            //                return;
            //            }
            //            destP.y = height;

            //            mx.SetTRS(destP, Quaternion.identity, srcScale * meshScale);
            //            _matrixes[i] = mx;
            //        }

            //        if (mCurSmoothTime > mSmoothTime)
            //        {
            //            mSmoothPosition = nowPosition;
            //            mCurSmoothTime = 0;
            //            mSmoothFlag = false;
            //        }
            //    }
            //    // lastPosition = nowPosition;
            //}




            if (lastPosition != nowPosition)
            {
                Vector3 srcScale = transform.lossyScale;

                for (int i = 0; i < showCount; i++)
                {
                    var mx = _matrixes[i];
                    Vector3 targetPos = nowPosition + mPositions[i];
                    float height;
                    if (!MapRender.QueryHeight(targetPos, out height))
                    {
                        return;
                    }
                    targetPos.y = height;
                    mx.SetTRS(targetPos, Quaternion.identity, srcScale * meshScale);
                    _matrixes[i] = mx;
                }
                lastPosition = nowPosition;
            }
        }

        void Update()
        {
            if (mesh == null || test == true)
            {
                test = false;
                InitMesh();
            }

            UpdatePosition();

            Vector3 dirForCamera = (transform.position - Camera.main.transform.position);
            dirForCamera.y = 0;
            dirForCamera.Normalize();
            float dir = Vector3.Dot(transform.forward, dirForCamera);
            //Debug.LogError("dir ="+dir);
            float side = Vector3.Cross(transform.forward, dirForCamera).y;
            //Debug.LogError("side ="+ side);
            if (dir > 0.6f)
            {
                material.SetFloat("_VerticalIndex", 3);
            }
            else if (dir < -0.6f)
            {
                material.SetFloat("_VerticalIndex", 2);

            }
            else if (side >= 0)
            {
                material.SetFloat("_VerticalIndex", 0);
            }
            else
            {
                material.SetFloat("_VerticalIndex", 1);
            }

            DrawMesh();
        }



        public Vector2 size = new Vector2(1, 1);
        public Vector2 origin = new Vector2(-0.5f, -0.5f);

        public Renderer initTroop;
        public SpriteRenderer mainTroop;
        public bool test = false;
        public int testCount = 30;
        private void Awake()
        {
            origin = size * -0.5f;
            //test = true;
            _instanceMaterial = new Material(material);
            material = _instanceMaterial;
        }
        void Add()
        {
            //Hex.Hex hex = GetTargetHex(count++);
            //GameObject go = GameObject.Instantiate(initTroop.gameObject);
            //go.transform.SetParent(transform, false);
            //go.transform.localPosition = HexToPosition(hex);
            //go.SetActive(true);
        }

        void Add(int count)
        {
            //List<Hex.Hex> hexList = new List<Hex.Hex>();
            //GetTargetHex(count, hexList);
            //for (int i = 0; i < count; ++i) {
            //    GameObject go = GameObject.Instantiate(initTroop.gameObject);
            //    go.transform.SetParent(transform, false);
            //    go.transform.localPosition = HexToPosition(hexList[i]);
            //    go.SetActive(true);
            //    float height = NewMap.Map.QueryHeight(go.transform.position);
            //    Vector3 pos = go.transform.position;
            //    pos.y = height;
            //    go.transform.position = pos;
            //    //Animator anim = go.GetComponent<Animator>();
            //    //anim.playbackTime = Random.Range(0f, 1f);
            //    //anim.Play(0, -1, Random.Range(0f, 1f));
            //}
        }

        public Vector3 HexToPosition(Hexagon.Hex hex)
        {
            int col = hex.q;
            int row = hex.r + (int)Mathf.Floor((hex.q - (hex.q & 1)) / 2.0f);
            float px = col * size.x + size.y * 0.5f;
            float py = row * size.y + (col & 1) * size.y * 0.5f + size.y * 0.5f;
            return new Vector3(px + origin.x, 0, py + origin.y);
        }
        public void GetTargetHex(int count, List<Hexagon.Hex> list)
        {
            int ringIndex = count;
            int ringCount = 1;
            while (ringIndex > 6 * ringCount)
            {
                ringIndex -= 6 * ringCount;
                ringCount++;
            }
            int dir = 4;
            for (int k = 0; k < ringCount; k++)
            {
                Hexagon.Hex hex = Hexagon.Hex.Direction(dir).Scale(k + 1);
                int d = dir - 4;
                for (int i = 0; i < 6; ++i)
                {
                    int dir_i = d + i;
                    if (dir_i < 0)
                        dir_i = dir_i + 6;
                    else if (dir_i > 5)
                        dir_i = dir_i - 6;
                    for (int j = 0; j < k + 1; ++j)
                    {
                        if (k < ringCount - 1)
                        {
                            list.Add(hex);
                        }
                        else
                        {
                            ringIndex--;
                            if (ringIndex >= 0)
                            {
                                list.Add(hex);
                            }
                        }
                        hex = hex.Neighbor(dir_i);
                    }
                }
            }
        }
        public Hexagon.Hex GetTargetHex(int index)
        {
            int ringIndex = index;
            int ringCount = 1;
            while (ringIndex > 6 * ringCount)
            {
                ringIndex -= 6 * ringCount;
                ringCount++;
            }
            int dir = 4;
            Hexagon.Hex hex = Hexagon.Hex.Direction(dir).Scale(ringCount);
            int d = dir - 4;
            for (int i = 0; i < 6; ++i)
            {
                int dir_i = d + i;
                if (dir_i < 0)
                    dir_i = dir_i + 6;
                else if (dir_i > 5)
                    dir_i = dir_i - 6;
                for (int j = 0; j < ringCount; ++j)
                {
                    ringIndex--;
                    if (ringIndex < 0)
                        return hex;
                    hex = hex.Neighbor(dir_i);
                }
            }

            return hex;
        }


        //private void Update()
        //{
        //    if (test) {
        //        test = false;
        //        Add(testCount);
        //    }
        // }
    }
}
