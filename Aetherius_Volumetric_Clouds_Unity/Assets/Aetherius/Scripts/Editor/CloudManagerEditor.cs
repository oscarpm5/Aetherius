using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Aetherius
{
    [CustomEditor(typeof(CloudManager))]
    [CanEditMultipleObjects]
    public class CloudManagerEditor : Editor
    {
        private CloudManager _myScript;
        private Editor _editor;
        private SerializedProperty preset;


        void OnEnable()
        {
            _myScript = (CloudManager)target;
            preset = serializedObject.FindProperty("preset");

        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            switch (_myScript.mode)
            {
                case CLOUD_CONTROL.SIMPLE:
                    {
                        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                        {
                            EditorGUILayout.PropertyField(preset);

                            if (check.changed)
                            {
                                _myScript.preset = (CLOUD_PRESET)preset.intValue;
                                _myScript.StartWMTransition();
                            }

                        }



                        _myScript.cumulusHorizon = EditorGUILayout.ToggleLeft(new GUIContent("Cumulus Horizon", "Option to make more epic cloudscapes, making the clouds toward the horizon appear more imposing"), _myScript.cumulusHorizon);
                        if (_myScript.cumulusHorizon == true)
                        {
                            _myScript.cumulusHorizonGradient = EditorGUILayout.Vector2Field(new GUIContent("Start / End cumulus",
                                "Distance in meters of the start and end of the cumulus gradient from the camera towards the horizon"),
                                _myScript.cumulusHorizonGradient);
                        }
                    }
                    break;
                case CLOUD_CONTROL.ADVANCED:
                    {
                        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                        {
                            AnimationCurve newCurve = EditorGUILayout.CurveField(_myScript.densityCurve1);

                            if (check.changed) //If we changed any parameters of the resolution property, update its noise
                            {
                                //_myScript.GenerateBaseShapeNoise();
                                _myScript.textureGenerator.GenerateWeatherMap(256,ref _myScript.textureGenerator.originalWM, _myScript.wmSeed, _myScript.preset);//TODO test line
                            }

                        }
                    }
                    break;

            }

            if (GUILayout.Button("GenerateWM"))
            {
                _myScript.textureGenerator.GenerateWeatherMap(256,ref _myScript.textureGenerator.originalWM,_myScript.wmSeed,_myScript.preset);
                Debug.Log("Manual WM Update!");
            }

        }


    }

}