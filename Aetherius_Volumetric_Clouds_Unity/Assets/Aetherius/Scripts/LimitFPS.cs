using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Helper Script, limits FPS on the editor window as to not overload the GPU while developing the game
[ExecuteInEditMode]
public class LimitFPS : MonoBehaviour
{
#if UNITY_EDITOR
    [Range(1, 120)]
    public int targetFPS = 30;
    int _targetFPS;


    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            QualitySettings.vSyncCount = 0;  // VSync must be disabled
            Application.targetFrameRate = targetFPS;
            _targetFPS = targetFPS;
        }
        else
        {
            this.enabled = false;
        }
    }

    void Update()
    {
        // Update is called once per frame
        if (_targetFPS != targetFPS)
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
