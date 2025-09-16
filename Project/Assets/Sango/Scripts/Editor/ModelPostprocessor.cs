using UnityEngine;
using UnityEditor;

public class ModelPostprocessor : AssetPostprocessor
{
    /// <summary>
    /// 处理模型的默认材质
    /// </summary>
    /// <param name="m"></param>
    /// <param name="r"></param>
    /// <returns></returns>
    Material OnAssignMaterialModel(Material m, Renderer r)
    {
        return AssetDatabase.LoadAssetAtPath<Material>("Assets/emptyformat.mat");
    }
}