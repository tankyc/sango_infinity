using Sango.Core.Tools;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    [GameSystem]
    internal class CellSelector : GameSystem
    {
        Cell selected;
        Mesh selectMesh;

        Transform selectObjectTrans;

        Vector3[] mesh_vertexs = new Vector3[25];
        int[] mesh_triangles = new int[96];
        float yOffset = 0.2f;
#if UNITY_ANDROID || UNITY_IPHONE
        public int lod = 1;
#else
        public int lod = 0;
#endif
        public bool enabled = true;
        Vector2 flashFactor = new Vector2(0f, 0.4f);
        float flashTime = 0.5f;
        string propertyName = "_Alpha";
        float curFactor = 0;
        float factorDir = 1;
        MeshRenderer meshRenderer;

        public override void Init()
        {
            GameController.Instance.onCellOverEnter += OnCellOverEnter;
            GameController.Instance.onCellOverExit += OnCellOverExit;
            GameEvent.OnScenarioStart += OnScenarioStart;
            GameEvent.OnScenarioEnd += OnScenarioEnd;

            GameObject gameObject = new GameObject("selectMesh");
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            selectObjectTrans = gameObject.transform;
            selectObjectTrans.localScale = new Vector3(-1, 1, 1);
            selectObjectTrans.rotation = Quaternion.Euler(0, 90, 0);
            GameObject.DontDestroyOnLoad(gameObject);
            meshRenderer.sharedMaterial = Resources.Load<Material>("cellSelectedMat");
            int w = 4, h = 4;
            int v_w = (w + 1);
            int v_h = (h + 1);
            int vCount = v_w * v_h;
            int tCount = w * h * 6;
            selectMesh = new Mesh();
            selectMesh.MarkDynamic();
            mesh_vertexs = new Vector3[vCount];
            mesh_triangles = new int[tCount];
            int lodL = 1 << lod;
            int start = 0;
            for (int j = 0; j < h; j += lodL)
            {
                for (int i = 0; i < w; i += lodL)
                {
                    int vStart = j * v_w + i;
                    mesh_triangles[start++] = vStart;
                    mesh_triangles[start++] = vStart + (v_w * lodL);
                    mesh_triangles[start++] = vStart + (v_w + 1) * lodL;

                    mesh_triangles[start++] = vStart;
                    mesh_triangles[start++] = vStart + (v_w + 1) * lodL;
                    mesh_triangles[start++] = vStart + lodL;
                }
            }
            meshFilter.mesh = selectMesh;
            selectMesh.vertices = mesh_vertexs;
            selectMesh.triangles = mesh_triangles;
        }

        public override void Clear()
        {
            GameObject.Destroy(selectObjectTrans.gameObject);
            GameObject.Destroy(selectMesh);
            selectMesh = null;
            GameController.Instance.onCellOverEnter -= OnCellOverEnter;
            GameController.Instance.onCellOverExit -= OnCellOverExit;
            GameEvent.OnScenarioStart -= OnScenarioStart;
            GameEvent.OnScenarioStart -= OnScenarioEnd;
        }

        void OnScenarioStart(Scenario scenario)
        {
            int quadSize = MapRender.Instance.mapData.quadSize;
            int start = 0;
            for (int j = -2; j < 3; j++)
            {
                for (int i = -2; i < 3; i++)
                {
                    mesh_vertexs[start++] = new Vector3(i * quadSize, 0, j * quadSize);
                }
            }
            GameEvent.OnScenarioTick += OnTick;
        }

        void OnScenarioEnd(Scenario scenario)
        {
            GameEvent.OnScenarioTick -= OnTick;
        }

        void OnCellOverEnter(Cell cell)
        {
            selected = cell;
            UpdateSelectMesh();
        }

        void OnCellOverExit(Cell cell)
        {

        }

        void UpdateSelectMesh()
        {
            if (selected == null)
                return;

            MapRender mapRender = MapRender.Instance;
            int x_start = selected.x * 4;
            int y_start = selected.y * 4 + (selected.x % 2) * 2;

            for (int j = 0; j < 5; j++)
            {
                for (int i = 0; i < 5; i++)
                {
                    int vIndex = j * 5 + i;
                    MapData.VertexData vertexData = mapRender.mapData.GetVertexData(x_start + i, y_start + j);
                    Vector3 v = mesh_vertexs[vIndex];
                    v.y = vertexData.height * 0.5f;
                    mesh_vertexs[vIndex] = v;
                }
            }
            selectMesh.vertices = mesh_vertexs;
            selectMesh.RecalculateBounds();
            //selectObjectTrans.position = selected.Position + offset;
            Vector3 pos = selected.Position;
            pos.y = yOffset;
            selectObjectTrans.position = pos;
        }

        void OnTick(Scenario scenario, float deltaTime)
        {
            if (!enabled || meshRenderer == null) return;
            curFactor += deltaTime * factorDir;
            if (curFactor > flashTime && factorDir > 0)
            {
                curFactor = flashTime;
                factorDir = -1;
            }
            else if (curFactor < 0 && factorDir < 0)
            {
                curFactor = 0;
                factorDir = 1;
            }
            float v = Mathf.Lerp(flashFactor.x, flashFactor.y, curFactor / flashTime);
#if UNITY_EDITOR
            meshRenderer.material.SetFloat(propertyName, v);
#else
            meshRenderer.sharedMaterial.SetFloat(propertyName, v);
#endif
        }
    }
}
