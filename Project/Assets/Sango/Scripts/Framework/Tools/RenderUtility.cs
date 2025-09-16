
//using FairyGUI;
//using LuaInterface;
//using System.Collections.Generic;
//using UnityEngine;
//using Sango;

//namespace Sango
//{
//    public static class RenderUtility
//    {
//        public static void SetHexShape(FairyGUI.GGraph graph, float[] states, Color32[] colors)
//        {
//            if (states.Length != colors.Length) return;

//            Shape shape = graph.shape;
//            PolygonMesh radial = shape.graphics.GetMeshFactory<PolygonMesh>();
//            radial.fillColor = null;
//            radial.usePercentPositions = true;
//            radial.centerIsZero = true;
//            radial.points.Clear();
//            radial.texcoords.Clear();
//            //float[] states = { 30, 90, 60, 70, 10 };
//            radial.colors = colors;
//            radial.points.Add(shape.pivot);

//            Vector2 begin = new Vector2(0, -1);
//            for (int i = 0; i < states.Length; ++i)
//            {
//                Quaternion rot = Quaternion.AngleAxis((i * 360) / states.Length, new Vector3(0, 0, 1));
//                Vector2 end = rot * begin;
//                end *= (float)states[i] / 200.0f;
//                end += shape.pivot;
//                radial.points.Add(end);
//                //radial.texcoords.Add(new Vector2(0, 0));
//            }
//            shape.graphics.SetMeshDirty();
//        }
//    }
//}