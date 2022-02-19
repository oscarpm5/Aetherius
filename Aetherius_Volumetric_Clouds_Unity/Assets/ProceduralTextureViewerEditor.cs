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
                myScript.UpdateNoise();
                Debug.Log("Manual Update!");
            }
        }

        Object currSettings = myScript.activeWorleyShapeSettings;
        if (currSettings != null)
        {
            CreateCachedEditor(currSettings, null, ref _editor);
            _editor.OnInspectorGUI();

            serializedObject.ApplyModifiedProperties();
        }
    }

}
