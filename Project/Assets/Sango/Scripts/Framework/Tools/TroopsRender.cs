using Sango.Hexagon;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango
{
    /// <summary>
    /// 集群2D小兵部队渲染器,采用GUPInstance渲染
    /// </summary>
    public class TroopsRender : MonoBehaviour
    {
        public enum TroopAniType : int
        {
            Move = 0,
            Attack = 1,
            Max = 2,
        }

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
        public Material[] aniMaterials = new Material[(int)TroopAniType.Max];
        public Transform[] aniNode = new Transform[60];

        public float meshScale = 1;
        public float totalScale = 1;
        private int elementCount = 60;
        public int showCount = 60;
        public ParticleSystem smoke;
        private bool isAniEnabled = false;

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
            Vector3 srcScale = transform.lossyScale * totalScale;
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
            Vector3 srcScale = transform.lossyScale * totalScale;
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

            //Vector3 srcPosition = transform.position;
            Vector3 srcScale = transform.lossyScale * totalScale;
            for (int i = 0; i < _matrixes.Length; i++)
            {
                var mx = _matrixes[i];
                Transform node = aniNode[i];
                mx.SetTRS(
                    node.position,
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
            Vector3 srcScale = transform.lossyScale * totalScale;

            mSmoothPosition = srcPosition;

            int count = elementCount;
            mPositions = new Vector3[count];
            mHexPositions = new Vector3[count];
            mRandomSmoothTime = new float[count];
            hexList = new List<Hexagon.Hex>();
            GetTargetHex(count, hexList);

            UpdateHexPosition();
            //UpdateTroopsPosition();
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

        public void SetSmokeShow(bool b)
        {
            if (smoke != null)
            {
                if (b)
                {
                    //if (!smoke.isPlaying || smoke.isStopped)
                        smoke.Play();

                }
                else
                {
                    //if (!smoke.isStopped)
                        smoke.Stop();
                }
            }
        }

        void UpdatePosition()
        {
            Vector3 nowPosition = transform.position;
            if (lastPosition != nowPosition || material == aniMaterials[1])
            {
                Vector3 srcScale = transform.lossyScale * totalScale;

                for (int i = 0; i < showCount; i++)
                {
                    var mx = _matrixes[i];
                    Transform node = aniNode[i];
                    Vector3 targetPos = node.position;
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
                //Add(showCount);
            }

            UpdatePosition();

            Vector3 dirForCamera = (Camera.main.transform.position - transform.position);
            dirForCamera.y = 0;
            dirForCamera.Normalize();
            float dir = Vector3.Dot(transform.forward, dirForCamera);
            //Debug.LogError("dir ="+dir);
            //Debug.LogError("side ="+ side);
            if (dir > 0.6f)
            {
                material.SetFloat("_VerticalIndex", 3);     // 背面
            }
            else if (dir < -0.6f)
            {
                material.SetFloat("_VerticalIndex", 0);    // 正前

            }
            else
            {
                float side = Vector3.Cross(transform.forward, dirForCamera).y;
                if (side >= 0)
                {
                    material.SetFloat("_VerticalIndex", 1);  // 左
                }
                else
                {
                    material.SetFloat("_VerticalIndex", 2); // 右
                }
            }
            DrawMesh();
        }



        public Vector2 size = new Vector2(0.34f, 0.34f);
        public Vector2 origin = new Vector2(-0.5f, -0.5f);

        public Renderer initTroop;
        public Transform initTroopRoot;
        public SpriteRenderer mainTroop;
        public bool test = false;
        public int testCount = 30;
        public List<UnityEngine.Renderer> initTroopsRenders = new List<UnityEngine.Renderer>();
        private void Awake()
        {
            origin = size * -0.5f;
            for (int i = 0; i < aniMaterials.Length; i++)
            {
                Material mat = new Material(aniMaterials[i]);
                aniMaterials[i] = mat;
            }
            SetAniType(0);
        }

        public void SetAniType(int id)
        {
            if (id < 0 || id >= aniMaterials.Length)
                return;
            material = aniMaterials[id];
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
            List<Hex> hexList = new List<Hex>();
            GetTargetHex(count, hexList);
            Vector3 srcScale = new Vector3(totalScale, totalScale, totalScale);
            for (int i = 0; i < count; ++i)
            {
                GameObject go = GameObject.Instantiate(initTroop.gameObject);
                go.name = i.ToString();
                go.transform.SetParent(initTroopRoot, false);
                go.transform.localPosition = Vector3.Scale(HexToPosition(hexList[i]), srcScale);
                go.transform.localScale = srcScale;
                go.SetActive(true);
                initTroopsRenders.Add(go.GetComponent<Renderer>());
                //float height = NewMap.Map.QueryHeight(go.transform.position);
                //Vector3 pos = go.transform.position;
                //pos.y = height;
                //go.transform.position = pos;
                //Animator anim = go.GetComponent<Animator>();
                //anim.playbackTime = Random.Range(0f, 1f);
                //anim.Play(0, -1, Random.Range(0f, 1f));
            }
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


        [ContextMenu("test")]
        private void Test()
        {
            Add(showCount);
        }
    }
}
