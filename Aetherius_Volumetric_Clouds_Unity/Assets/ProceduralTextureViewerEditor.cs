using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ProceduralTextureViewer))]
[CanEditMultipleObjects]
public class ProceduralTextureViewerEditor : Editor
{
    ProceduralTextureViewer myScript;
    void OnEnable()
    {
        myScript = (ProceduralTextureViewer)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if(!myScript.updateTextureAuto)
        {
            if (GUILayout.Button("GenerateTexture"))
            {
                myScript.GenerateTexture(256);//TODO doesnt work directly, call it OnRenderImage
            }
        }
    }

}
