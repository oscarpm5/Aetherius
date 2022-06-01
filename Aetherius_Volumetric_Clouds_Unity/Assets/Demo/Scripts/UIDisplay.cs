using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIDisplay : MonoBehaviour
{
    public Aetherius.CloudManager managerRef;
    [HideInInspector]
    public Benchmark benchmarkRef;
    public Text fpsCounter;
    public Text msCounter;
    public Text resolution;
    public GameObject benchmarkToggleButton;

    Text benchmarkToggleText;
    Image benchmarkToggleImage;


    // Start is called before the first frame update
    void Start()
    {
        managerRef.resolution = Aetherius.CLOUD_RESOLUTION.ORIGINAL;
        benchmarkToggleImage = benchmarkToggleButton.GetComponent<Image>();
        benchmarkToggleText = benchmarkToggleButton.transform.Find("Text").GetComponent<Text>();
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput();

        fpsCounter.text = "FPS: " + 1.0/Time.unscaledDeltaTime;
        msCounter.text = "MS: " + Time.unscaledDeltaTime*1000.0f;
        SetResolutionText();
    }

    void HandleInput()
    {
        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            managerRef.resolution = Aetherius.CLOUD_RESOLUTION.ORIGINAL;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            managerRef.resolution = Aetherius.CLOUD_RESOLUTION.HALF;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            managerRef.resolution = Aetherius.CLOUD_RESOLUTION.QUARTER;
        }
    }

    void SetResolutionText()
    {
        switch (managerRef.resolution)
        {
            case Aetherius.CLOUD_RESOLUTION.ORIGINAL:
                {
                    resolution.text = "ORIGINAL RES";
                    resolution.color = Color.red;
                }
                break;
            case Aetherius.CLOUD_RESOLUTION.HALF:
                {
                    resolution.text = "HALF RES";
                    resolution.color = Color.yellow;
                }
                break;
            case Aetherius.CLOUD_RESOLUTION.QUARTER:
                {
                    resolution.text = "QUARTER RES";
                    resolution.color = Color.green;
                }
                break;
        }
    }

    public void ShowBenchmarkResults()
    {

    }

    public void SetBenchmarkButtonDisplay(bool benchmarkActive)
    {
        if(benchmarkActive)
        {
            benchmarkToggleImage.color = Color.blue;
            benchmarkToggleText.text = "Finish Benchmark";
        }
        else
        {
            benchmarkToggleImage.color = Color.green;
            benchmarkToggleText.text = "Start Benchmark";
        }
    }
    

}
