using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class LimitFPS : MonoBehaviour
{
#if UNITY_EDITOR
    [Range(1, 120)]
    public int targetFPS = 30;
    int _targetFPS;


    private void OnEnable()
    {
        QualitySettings.vSyncCount = 0;  // VSync must be disabled
        Application.targetFrameRate = targetFPS;
        _targetFPS = targetFPS;
    }

    void Update()
    {
        // Update is called once per frame
        if(_targetFPS!=targetFPS)
        {
            _targetFPS = targetFPS;

            QualitySettings.vSyncCount = 0;  // VSync must be disabled
            Application.targetFrameRate = _targetFPS;

        }

    }

    private void OnDisable()
    {
        Application.targetFrameRate = -1;

    }

#endif
}
