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

        private SerializedProperty mode;
        private SerializedProperty preset;

        private SerializedProperty textureGenerator;
        private SerializedProperty perlinSetting;
        private SerializedProperty worleyShapeSettings;
        private SerializedProperty worleyDetailSettings;

        bool showScriptableObjectSettings = false;

        void OnEnable()
        {
            _myScript = (CloudManager)target;
            mode = serializedObject.FindProperty("mode");
            preset = serializedObject.FindProperty("preset");

            textureGenerator = serializedObject.FindProperty("textureGenerator");

            perlinSetting = textureGenerator.FindPropertyRelative("perlinShapeSettings");
            worleyShapeSettings = textureGenerator.FindPropertyRelative("worleyShapeSettings");
            worleyDetailSettings = textureGenerator.FindPropertyRelative("worleyDetailSettings");


        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(mode);

                if (check.changed)
                {
                    if (_myScript.mode != (CLOUD_CONTROL)mode.intValue)
                    {
                        _myScript.mode = (CLOUD_CONTROL)mode.intValue;
                    }
                }

            }
            switch (_myScript.mode)
            {
                case CLOUD_CONTROL.SIMPLE:
                    {
                        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                        {
                            EditorGUILayout.PropertyField(preset);

                            if (check.changed)
                            {
                                if (_myScript.preset != (CLOUD_PRESET)preset.intValue)
                                {
                                    _myScript.preset = (CLOUD_PRESET)preset.intValue;
                                    _myScript.StartWMTransition();
                                }
                            }

                        }


                        //TODO won't be able to edit these in the near future?
                        /*
                        _myScript.simple.baseShapeSize = EditorGUILayout.IntField("Base Shape Size",(int)_myScript.simple.baseShapeSize);
                        _myScript.simple.detailSize = EditorGUILayout.IntField("Detail Shape Size", (int)_myScript.simple.detailSize);
                        _myScript.simple.globalCoverage = EditorGUILayout.FloatField("Global Coverage", _myScript.simple.globalCoverage);
                        _myScript.simple.globalDensity = EditorGUILayout.FloatField("Global Density", _myScript.simple.globalDensity);
                        _myScript.simple.weatherMapSize = EditorGUILayout.IntField("Weather Map Size", (int)_myScript.simple.weatherMapSize);
                        */


                        



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

                        _myScript.advanced.baseShapeSize = EditorGUILayout.IntField("Base Shape Size", (int)_myScript.advanced.baseShapeSize);
                        _myScript.advanced.detailSize = EditorGUILayout.IntField("Detail Shape Size", (int)_myScript.advanced.detailSize);
                        _myScript.advanced.globalCoverage = EditorGUILayout.FloatField("Global Coverage", _myScript.advanced.globalCoverage);
                        _myScript.advanced.globalDensity = EditorGUILayout.FloatField("Global Density", _myScript.advanced.globalDensity);
                        _myScript.advanced.weatherMapSize = EditorGUILayout.IntField("Weather Map Size", (int)_myScript.advanced.weatherMapSize);



                        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                        {
                            AnimationCurve newCurve = EditorGUILayout.CurveField(_myScript.densityCurve1);

                            if (check.changed) //If we changed any parameters of the resolution property, update its noise
                            {
                                //_myScript.GenerateBaseShapeNoise();
                                _myScript.textureGenerator.GenerateWeatherMap(256, ref _myScript.textureGenerator.originalWM, _myScript.wmSeed, _myScript.preset);//TODO test line
                            }

                        }
                    }
                    break;

            }

            if (GUILayout.Button("GenerateWM"))
            {
                _myScript.textureGenerator.GenerateWeatherMap(256, ref _myScript.textureGenerator.originalWM, _myScript.wmSeed, _myScript.preset);
                Debug.Log("Manual WM Update!");
            }


            //Texture Generator related

            showScriptableObjectSettings = EditorGUILayout.Foldout(showScriptableObjectSettings, "Scriptable Objects Noise Settings",true);
            if (showScriptableObjectSettings)
            {



                //Base Shape Noise
                using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                {

                    EditorGUILayout.BeginVertical("Base Shape Noise Settings");
                    EditorGUILayout.LabelField("Base Shape Noise Settings", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.BeginVertical("Channel");
                    EditorGUILayout.PropertyField(perlinSetting, GUIContent.none);
                    EditorGUILayout.PropertyField(worleyShapeSettings.GetArrayElementAtIndex(0), GUIContent.none);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.LabelField("R Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(worleyShapeSettings.GetArrayElementAtIndex(1), GUIContent.none);
                    EditorGUILayout.LabelField("G Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(worleyShapeSettings.GetArrayElementAtIndex(2), GUIContent.none);
                    EditorGUILayout.LabelField("B Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(worleyShapeSettings.GetArrayElementAtIndex(3), GUIContent.none);
                    EditorGUILayout.LabelField("A Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    if (check.changed && _myScript.textureGenerator.updateTextureAuto) //If we changed any parameters of the resolution property, update its noise
                    {
                        _myScript.textureGenerator.GenerateBaseShapeNoise();
                    }

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Separator();

                //Detail Shape Noise
                using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                {

                    EditorGUILayout.BeginVertical("Detail Shape Noise Settings");
                    EditorGUILayout.LabelField("Detail Shape Noise Settings", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(worleyDetailSettings.GetArrayElementAtIndex(0), GUIContent.none);
                    EditorGUILayout.LabelField("R Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(worleyDetailSettings.GetArrayElementAtIndex(1), GUIContent.none);
                    EditorGUILayout.LabelField("G Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(worleyDetailSettings.GetArrayElementAtIndex(2), GUIContent.none);
                    EditorGUILayout.LabelField("B Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    if (check.changed && _myScript.textureGenerator.updateTextureAuto) //If we changed any parameters of the resolution property, update its noise
                    {
                        _myScript.textureGenerator.GenerateDetailNoise();

                    }

                    EditorGUILayout.EndVertical();
                }

            }

        }


    }

}