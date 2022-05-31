using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIDisplay : MonoBehaviour
{
    public Text fpsCounter;
    public Text msCounter;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        fpsCounter.text = "FPS: " + 1.0/Time.unscaledDeltaTime;
        msCounter.text = "MS: " + Time.unscaledDeltaTime*1000.0f;
    }
}
