using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



public class UIDisplay : MonoBehaviour
{
    public struct BenchmarkSectionDisplay
    {
        public Text fps;
        public Text ms;
        public Text highest;
        public Text lowest;
    }

    public Aetherius.CloudManager managerRef;
    [HideInInspector]
    public Benchmark benchmarkRef;
    public Text fpsCounter;
    public Text msCounter;
    public Dropdown resolution;
    public GameObject benchmarkToggleButton;
    public Canvas benchmarkCanvas;

    Text benchmarkToggleText;
    Image benchmarkToggleImage;

    List<BenchmarkSectionDisplay> displaySections = new List<BenchmarkSectionDisplay>();

    Color originalBenchmarkColor;


    // Start is called before the first frame update
    void Start()
    {
        benchmarkCanvas.gameObject.SetActive(true);
        benchmarkToggleImage = benchmarkToggleButton.GetComponent<Image>();
        benchmarkToggleText = benchmarkToggleButton.transform.Find("Text").GetComponent<Text>();



        AssignChildren();
        originalBenchmarkColor = benchmarkToggleImage.color;
        SetBenchmarkButtonDisplay(false);

    }

    private void AssignChildren()
    {
        AssignBenchmarkSection(benchmarkCanvas.gameObject.transform.Find("SparseStats"));
        AssignBenchmarkSection(benchmarkCanvas.gameObject.transform.Find("CloudyStats"));
        AssignBenchmarkSection(benchmarkCanvas.gameObject.transform.Find("StormyStats"));
        AssignBenchmarkSection(benchmarkCanvas.gameObject.transform.Find("OvercastStats"));

        AssignBenchmarkSection(benchmarkCanvas.gameObject.transform.Find("GeneralStats"));
    }

    private void AssignBenchmarkSection(Transform obj)
    {
        BenchmarkSectionDisplay newDisplay; 
        newDisplay.fps = obj.Find("FPS").GetComponent<Text>();
        newDisplay.ms = obj.Find("MS").GetComponent<Text>();
        newDisplay.lowest = obj.Find("LOWEST FPS").GetComponent<Text>();
        newDisplay.highest = obj.Find("HIGHEST FPS").GetComponent<Text>();

        displaySections.Add(newDisplay);
    }

    // Update is called once per frame
    void Update()
    {

        fpsCounter.text = "FPS: " + 1.0/Time.unscaledDeltaTime;
        msCounter.text = "MS: " + Time.unscaledDeltaTime*1000.0f;
        SetResolutionText();
    }   

    void SetResolutionText()
    {
        resolution.value = (int)managerRef.resolution;
    }

    public void ShowBenchmarkResults()
    {
        for (int i = 0; i < benchmarkRef.benchmarkData.Count; ++i)
        {
            BenchmarkSectionData currentData = benchmarkRef.benchmarkData[i];

            double averageMS = (currentData.averageSeconds * 1000.0);

            displaySections[i].fps.text = "AVG FPS: " + ((float)(1.0 / currentData.averageSeconds)).ToString();
            displaySections[i].ms.text = "AVG MS: " + ((float)averageMS).ToString();
            displaySections[i].lowest.text = "LOWEST FPS: " + ((float)(1.0/ currentData.highestSeconds)).ToString();
            displaySections[i].highest.text = "HIGHEST FPS: " + ((float)(1.0 / currentData.lowestSeconds)).ToString();

        }

        benchmarkCanvas.gameObject.SetActive(true);

    }

    public void SetBenchmarkButtonDisplay(bool benchmarkActive)
    {
        if(benchmarkActive)
        {
            benchmarkToggleImage.color = Color.blue;
            benchmarkToggleText.text = "Finish Benchmark (B)";
        }
        else
        {
            benchmarkToggleImage.color = originalBenchmarkColor;
            benchmarkToggleText.text = "Start Benchmark (B)";
            benchmarkCanvas.gameObject.SetActive(false);
        }
    }
    

}
