using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ProceduralTextureViewer))]
[CanEditMultipleObjects]
public class ProceduralTextureViewerEditor : Editor
{
    private ProceduralTextureViewer _myScript;
    private Editor _editor;
    private bool _showCurrSettingsWorley = false;
    private bool _showCurrSettingsPerlin = false;
    private int _baseShapeResPower = 8;
    private int _detailResPower = 5;


    void OnEnable()
    {
        _myScript = (ProceduralTextureViewer)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        _baseShapeResPower = (int)Mathf.Log(_myScript.baseShapeResolution, 2.0f);
        _detailResPower = (int)Mathf.Log(_myScript.detailResolution, 2.0f);

        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.BeginVertical("GroupBox");
            EditorGUILayout.LabelField("Base Shape Noise Resolution",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pixels: " + _myScript.baseShapeResolution.ToString());
            _baseShapeResPower = EditorGUILayout.IntSlider("Power", _baseShapeResPower, 3, 9);
            _myScript.baseShapeResolution = (int)Mathf.Pow(2.0f, _baseShapeResPower);
            
            if (check.changed && _myScript.updateTextureAuto) //If we changed any parameters of the resolution property, update its noise
            {
                _myScript.GenerateBaseShapeNoise();
            }

            EditorGUILayout.EndVertical();
        }
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            EditorGUILayout.BeginVertical("GroupBox");
            EditorGUILayout.LabelField("Detail Noise Resolution", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Pixels: " + _myScript.detailResolution.ToString());
            _detailResPower = EditorGUILayout.IntSlider("Power", _detailResPower, 3, 9);
            _myScript.detailResolution = (int)Mathf.Pow(2.0f, _detailResPower);
            
            if (check.changed && _myScript.updateTextureAuto) //If we changed any parameters of the resolution property, update its noise
            {
                _myScript.GenerateDetailNoise();
            }

            EditorGUILayout.EndVertical();
        }


        if (!_myScript.updateTextureAuto)
        {
            if (GUILayout.Button("GenerateTexture"))
            {
                _myScript.UpdateNoiseManually();
                Debug.Log("Manual Update!");
            }
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
                    _myScript.NoiseSettingsChanged();
                }
            }
        }

        //If base texture & R channel show perlin
        if (_myScript.displayType == ProceduralTextureViewer.TEXTURE_TYPE.BASE_SHAPE && _myScript.displayChannel == ProceduralTextureViewer.TEXTURE_CHANNEL.R)
        {
            currSettings = _myScript.perlinShapeSettings;
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
                        _myScript.NoiseSettingsChanged();
                    }
                }
            }
        }
    }
}

