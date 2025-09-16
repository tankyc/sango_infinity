using RTEditor;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneGizmoExtend : SceneGizmo
{
    private new void Update()
    {
        UpdateGizmoCamera();

        _gizmoTransform.rotation = CalculateGizmoRotation();
        _gizmoTransform.gameObject.SetAbsoluteScale(Vector3.one);
        _gizmoTransform.position = CalculateCubePosition();

        _hoveredComponent = DetectHoveredComponent();
        EstablishComponentColors();

        if (InputDevice.Instance.WasPressedInCurrentFrame(0)) OnFirstInputDeviceBtnDown();
    }
    private new void OnFirstInputDeviceBtnDown()
    {
        if (_hoveredComponent == (int)SceneGizmoComponent.Cube)
        {
            Camera editorCamera = EditorCamera.Instance.Camera;
            EditorCamera.Instance.SetOrtho(!editorCamera.orthographic);
            _gizmoCamera.orthographic = editorCamera.orthographic;
        }

        if (_hoveredComponent == (int)SceneGizmoComponent.PositiveX)
        {
            EditorCameraExtend.Instance.AlignLookWithWorldAxis(Axis.X, true, _cameraAlignDuration, delegate { EditorCameraExtend.Instance.FocusOnSelection(); });
        }
        else if (_hoveredComponent == (int)SceneGizmoComponent.NegativeX)
        {
            EditorCameraExtend.Instance.AlignLookWithWorldAxis(Axis.X, false, _cameraAlignDuration, delegate { EditorCameraExtend.Instance.FocusOnSelection(); });
        }   
        else if (_hoveredComponent == (int)SceneGizmoComponent.PositiveY)
        {
            EditorCameraExtend.Instance.AlignLookWithWorldAxis(Axis.Y, true, _cameraAlignDuration, delegate { EditorCameraExtend.Instance.FocusOnSelection(); });
        }   
        else if (_hoveredComponent == (int)SceneGizmoComponent.NegativeY)
        {
            EditorCameraExtend.Instance.AlignLookWithWorldAxis(Axis.Y, false, _cameraAlignDuration, delegate { EditorCameraExtend.Instance.FocusOnSelection(); });
        } 
        else if (_hoveredComponent == (int)SceneGizmoComponent.PositiveZ)
        {
            EditorCameraExtend.Instance.AlignLookWithWorldAxis(Axis.Z, true, _cameraAlignDuration, delegate { EditorCameraExtend.Instance.FocusOnSelection(); });
        } 
        else if (_hoveredComponent == (int)SceneGizmoComponent.NegativeZ)
        {
            EditorCameraExtend.Instance.AlignLookWithWorldAxis(Axis.Z, false, _cameraAlignDuration, delegate { EditorCameraExtend.Instance.FocusOnSelection(); });
        }
    }
}
