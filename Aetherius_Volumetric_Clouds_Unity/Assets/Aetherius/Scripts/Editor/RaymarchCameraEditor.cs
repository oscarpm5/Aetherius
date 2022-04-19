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
                        _myScript.cumulusHorizon = EditorGUILayout.ToggleLeft(new GUIContent("Cumulus Horizon","Option to make more epic cloudscapes, making the clouds toward the horizon appear more imposing"), _myScript.cumulusHorizon);
                        if (_myScript.cumulusHorizon==true)
                        {
                            _myScript.cumulusHorizonGradient = EditorGUILayout.Vector2Field(new GUIContent("Start / End cumulus",
                                "Distance in meters of the start and end of the cumulus gradient from the camera towards the horizon"),
                                _myScript.cumulusHorizonGradient);
                        }
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
                                _myScript.GenerateWM();//TODO test line
                            }

                        }
                    }
                    break;

            }

            if (GUILayout.Button("GenerateWM"))
            {
                _myScript.GenerateWM();
                Debug.Log("Manual WM Update!");
            }
        }
    }
}

