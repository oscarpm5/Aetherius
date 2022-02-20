using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ProceduralTextureViewer))]
[CanEditMultipleObjects]
public class ProceduralTextureViewerEditor : Editor
{
    private ProceduralTextureViewer myScript;
    private Editor _editor;
    void OnEnable()
    {
        myScript = (ProceduralTextureViewer)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!myScript.updateTextureAuto)
        {
            if (GUILayout.Button("GenerateTexture"))
            {
                myScript.UpdateNoiseManually();
                Debug.Log("Manual Update!");
            }
        }

        Object currSettings = myScript.activeWorleyShapeSettings;
        if (currSettings != null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                CreateCachedEditor(currSettings, null, ref _editor);//With this we can display a scriptable object in the script editor
                _editor.OnInspectorGUI();

                if (check.changed) //If we changed any parameters of the scriptable object, update its noise
                {
                    myScript.NoiseSettingsChanged();
                }
            }
        }

        //If base texture & R channel show perlin
        if (myScript.displayType==ProceduralTextureViewer.TEXTURE_TYPE.BASE_SHAPE && myScript.displayChannel== ProceduralTextureViewer.TEXTURE_CHANNEL.R)
        {
            currSettings = myScript.perlinShapeSettings;
            if (currSettings != null)
            {
                using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                {
                    CreateCachedEditor(currSettings, null, ref _editor);//With this we can display a scriptable object in the script editor
                    _editor.OnInspectorGUI();

                    if (check.changed) //If we changed any parameters of the scriptable object, update its noise
                    {
                        myScript.NoiseSettingsChanged();
                    }
                }
            }
        }
    }
}

