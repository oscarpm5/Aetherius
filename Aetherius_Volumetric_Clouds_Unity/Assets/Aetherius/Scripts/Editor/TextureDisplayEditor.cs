using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace Aetherius
{
    [CustomEditor(typeof(TextureDisplay))]
    [CanEditMultipleObjects]
    public class TextureDisplayEditor : Editor
    {
        private TextureDisplay _myScript;
        private Editor _editor;


        private bool _showCurrSettingsWorley = false;
        private bool _showCurrSettingsPerlin = false;
        private int _baseShapeResPower = 7;
        private int _detailResPower = 5;

        void OnEnable()
        {
            _myScript = (TextureDisplay)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            _baseShapeResPower = (int)Mathf.Log(_myScript.textureGenerator.baseShapeResolution, 2.0f);
            _detailResPower = (int)Mathf.Log(_myScript.textureGenerator.detailResolution, 2.0f);

            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.BeginVertical("GroupBox");
                EditorGUILayout.LabelField("Base Shape Noise Resolution", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Pixels: " + _myScript.textureGenerator.baseShapeResolution.ToString());
                _baseShapeResPower = EditorGUILayout.IntSlider("Power", _baseShapeResPower, 3, 9);
                _myScript.textureGenerator.baseShapeResolution = (int)Mathf.Pow(2.0f, _baseShapeResPower);

                if (check.changed) //If we changed any parameters of the resolution property, update its noise
                {
                    _myScript.textureGenerator.GenerateBaseShapeNoise();
                }

                EditorGUILayout.EndVertical();
            }
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.BeginVertical("GroupBox");
                EditorGUILayout.LabelField("Detail Noise Resolution", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Pixels: " + _myScript.textureGenerator.detailResolution.ToString());
                _detailResPower = EditorGUILayout.IntSlider("Power", _detailResPower, 3, 9);
                _myScript.textureGenerator.detailResolution = (int)Mathf.Pow(2.0f, _detailResPower);

                if (check.changed) //If we changed any parameters of the resolution property, update its noise
                {
                    _myScript.textureGenerator.GenerateDetailNoise();
                }

                EditorGUILayout.EndVertical();
            }

            //Show scriptable object editor
            Object currSettings = _myScript.activeWorleySettings;
            if (currSettings != null)
            {
                _showCurrSettingsWorley = EditorGUILayout.InspectorTitlebar(_showCurrSettingsWorley, currSettings);
                using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                {
                    if (_showCurrSettingsWorley)
                    {
                        CreateCachedEditor(currSettings, null, ref _editor);//With this we can display a scriptable object in the script editor
                        _editor.OnInspectorGUI();
                    }

                    if (check.changed) //If we changed any parameters of the scriptable object, update its noise
                    {
                        if (_myScript.displayType == TEXTURE_TYPE.BASE_SHAPE)
                        {
                            _myScript.textureGenerator.GenerateBaseShapeNoise();
                        }
                        else
                        {
                            _myScript.textureGenerator.GenerateDetailNoise();
                        }
                    }
                }
            }

            //If base texture & R channel show perlin
            if (_myScript.displayType == TEXTURE_TYPE.BASE_SHAPE && _myScript.displayChannel == TEXTURE_CHANNEL.R)
            {
                currSettings = _myScript.textureGenerator.perlinShapeSettings;
                if (currSettings != null)
                {
                    _showCurrSettingsPerlin = EditorGUILayout.InspectorTitlebar(_showCurrSettingsPerlin, currSettings);
                    using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                    {
                        if (_showCurrSettingsPerlin)
                        {
                            CreateCachedEditor(currSettings, null, ref _editor);//With this we can display a scriptable object in the script editor
                            _editor.OnInspectorGUI();
                        }

                        if (check.changed) //If we changed any parameters of the scriptable object, update its noise
                        {
                            _myScript.textureGenerator.GenerateBaseShapeNoise();
                        }
                    }
                }
            }

        }
    }
}
