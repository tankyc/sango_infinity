using UnityEngine;
namespace Sango.Render
{
    public interface IMapManageObject
    {
        Sango.Render.MapRender manager { get; set; }
        Sango.Tools.Rect bounds { get; set; }
        Sango.Tools.Rect worldBounds { get;}
        int objId { get; set; }
        int objType { get; set; }
        int bindId { get; set; }

        int modelId { get; set; }
        bool visible { get; set; }
        bool isStatic { get; set; }
        bool selectable { get; set; }
        bool remainInView { get; set; }
        Vector3 position { get; set; }
        Vector3 rotation { get; set; }
        Vector3 forward { get; set; }
        Vector3 scale { get; set; }
        Vector2Int coords { get; set; }
        void OnClick();
        bool Overlaps(Sango.Tools.Rect rect);
        void OnPointerEnter();
        void OnPointerExit();
        void SetOutlineShow(Material material);
        void EditorShow(bool b);
        void SetParent(Transform parent);
        void SetParent(Transform parent, bool worldPositionStays);
        void Destroy();
        GameObject GetGameObject();
    }
}
