using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    [CustomEditor(typeof(AnimationText))]
    public class AnimationTextEditor : Editor
    {
        private float time = 0;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            AnimationText at = (AnimationText)target;

            Keyframe[] offsetsX = at.offsetCurveX.keys;
            Keyframe[] offsetsY = at.offsetCurveY.keys;
            Keyframe[] alphas = at.alphaCurve.keys;
            Keyframe[] scales = at.scaleCurve.keys;

            float offsetXEnd = offsetsX[offsetsX.Length - 1].time;
            float offsetYEnd = offsetsY[offsetsY.Length - 1].time;
            float alphaEnd = alphas[alphas.Length - 1].time;
            float scalesEnd = scales[scales.Length - 1].time;
            at.maxTime = Mathf.Max(scalesEnd, Mathf.Max(offsetXEnd, Mathf.Max(offsetYEnd, alphaEnd)));
            time = EditorGUILayout.Slider(time, 0, at.maxTime);
            at.UpdateByTime(time);
            if (GUILayout.Button("预览"))
            {
                at.Dump();
            }
        }
    }

}