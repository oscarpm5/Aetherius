using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class LimitFPS : MonoBehaviour
{
    [Range(1,120)]
    public int targetFPS =30;
    // Update is called once per frame
    void Update()
    {

#if UNITY_EDITOR
            QualitySettings.vSyncCount = 0;  // VSync must be disabled
            Application.targetFrameRate = targetFPS;
#endif
        
    }
}
