using RTEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditorCameraExtend : EditorCamera
{
    #region 单例
    private EditorCameraExtend() { }
    private static EditorCameraExtend _EditorCameraExtend;
    public new static EditorCameraExtend Instance
    {
        get
        {
            if(_EditorCameraExtend == null)
            {
                _EditorCameraExtend = GameObject.FindObjectOfType<EditorCameraExtend>();
            }
            return _EditorCameraExtend;
        }
    }
    #endregion
    //轨道相机半径
    public float OrbitOffsetAlongLook
    {
        get
        {
            return _orbitOffsetAlongLook;
        }
    }
    private void Start()
    {

    }

    protected new void Update()
    {
        base.Update();

        //只要有选择的物体不论镜头拉到多元轨道相机旋转半径都相对变化
        if(EditorObjectSelection.Instance.SelectedGameObjects.Count > 0)
        {
            if (Input.mouseScrollDelta.y != 0)
            {
                EditorCameraFocusOperationInfo focusOpInfo = EditorCameraFocus.GetFocusOperationInfo(Camera, _focusSettings);
                CalculateOrbitOffsetAlongLook(focusOpInfo);
            }
        }
    }

    /// <summary>
    /// 扩展一个相机旋转完的动作
    /// </summary>
    /// <param name="worldAxis"></param>
    /// <param name="negativeAxis"></param>
    /// <param name="duration"></param>
    /// <param name="action"></param>
    public void AlignLookWithWorldAxis(Axis worldAxis, bool negativeAxis, float duration, Action action)
    {
        StopAllCoroutines();
        StartCoroutine(StartAlignLookWithWorldAxis(worldAxis, negativeAxis, duration, action));
    }

    /// <summary>
    /// 扩展一个相机旋转完的动作
    /// </summary>
    /// <param name="worldAxis"></param>
    /// <param name="negativeAxis"></param>
    /// <param name="duration"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    private IEnumerator StartAlignLookWithWorldAxis(Axis worldAxis, bool negativeAxis, float duration, Action action)
    {
        Vector3 axisVector = Vector3.right;
        if (worldAxis == Axis.Y) axisVector = Vector3.up;
        else if (worldAxis == Axis.Z) axisVector = Vector3.forward;
        if (negativeAxis) axisVector *= -1.0f;

        float elapsedTime = 0.0f;
        Quaternion desiredRotation = Quaternion.LookRotation(axisVector);
        Quaternion initialRotation = Camera.transform.rotation;
        while (elapsedTime <= duration)
        {
            Camera.transform.rotation = Quaternion.Slerp(initialRotation, desiredRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;

            _background.OnCameraUpdate(Camera, _isDoingPerspectiveSwitch);

            yield return null;
        }

        Camera.transform.rotation = desiredRotation;

        //相机旋转完后执行动作
        if(action != null)
        {
            action();
        }
    }
}
