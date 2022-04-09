using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace Aetherius
{

    [CustomEditor(typeof(RaymarchCamera))]
    [CanEditMultipleObjects]
    public class RaymarchCameraEditor : Editor
    {
        private RaymarchCamera _myScript;
        private Editor _editor;
       

        void OnEnable()
        {
            _myScript = (RaymarchCamera)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

           

            switch (_myScript.mode)
            {
                case RaymarchCamera.CLOUD_CONTROL.SIMPLE:
                    {
                        
                    }
                    break;
                case RaymarchCamera.CLOUD_CONTROL.ADVANCED:
                    {
                        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                        {
                            AnimationCurve newCurve = EditorGUILayout.CurveField(_myScript.densityCurve);

                            if (check.changed) //If we changed any parameters of the resolution property, update its noise
                            {
                                //_myScript.GenerateBaseShapeNoise();
                            }

                        }
                    }
                    break;

            }
            
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                

                if (check.changed) //If we changed any parameters of the resolution property, update its noise
                {
                    //_myScript.GenerateBaseShapeNoise();
                }

            }
        }
    }
}

