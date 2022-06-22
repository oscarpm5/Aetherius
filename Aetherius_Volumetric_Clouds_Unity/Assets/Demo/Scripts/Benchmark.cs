using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;


[System.Serializable]
public class BenchmarkSectionData
{

    public double averageSeconds;
    public double highestSeconds;
    public double lowestSeconds;
}



public class Benchmark : MonoBehaviour
{
    enum EVALUATION_STAGE
    {
        SPARSE,
        CLOUDY,
        STORMY,
        OVERCAST,
        SHOW_RESULTS,
        INACTIVE
    }



    public Aetherius.CloudManager cloudManager;
    public DemoManager demo;
    public CameraMove aetheriusCamMove;
    EVALUATION_STAGE evaluatingPreset;
    public float evaluationTime = 10.0f;
    float currentTime;
    List<float> evaluatingFrames;

    [HideInInspector]
    public List<BenchmarkSectionData> benchmarkData;


    string filename = "";
    bool resultsSaved = false;

    bool wasSunAniamted = false;

    // Start is called before the first frame update
    void Start()
    {
        filename = Application.dataPath + "/BenchmarkResults.csv";

        demo.benchmarkRef = this;
        ResetData();
    }

    void ResetData()
    {
        evaluatingFrames = new List<float>();
        evaluatingPreset = EVALUATION_STAGE.INACTIVE;

        benchmarkData = new List<BenchmarkSectionData>();
        benchmarkData.Add(new BenchmarkSectionData { });//sparse
        benchmarkData.Add(new BenchmarkSectionData { });//cloudy
        benchmarkData.Add(new BenchmarkSectionData { });//stormy
        benchmarkData.Add(new BenchmarkSectionData { });//overcast
        benchmarkData.Add(new BenchmarkSectionData { });//general

        currentTime = 0.0f;
        resultsSaved = false;
    }

    public void ToggleBenchmark()
    {
        if (evaluatingPreset != EVALUATION_STAGE.INACTIVE)
        {
            StopBenchmark();
        }
        else
        {
            StartBenchmark();
        }
    }

    public void StartBenchmark()
    {
        ResetData();
        evaluatingPreset = EVALUATION_STAGE.SPARSE;
        cloudManager.preset = Aetherius.CLOUD_PRESET.SPARSE;
        cloudManager.StopWMTransition();
        cloudManager.StartWMTransition(0.0f);
        aetheriusCamMove.SetPitchYawPos(-30.0f, 200.0f, new Vector3(0.0f, 10.0f, 0.0f));
        aetheriusCamMove.enabledControl = false;
        wasSunAniamted = demo.animateSun.isOn;
        demo.animateSun.isOn = false;
        demo.SetBenchmarkButtonDisplay(true);
    }

    public void StopBenchmark()
    {
        evaluatingPreset = EVALUATION_STAGE.INACTIVE;
        aetheriusCamMove.enabledControl = true;
        demo.SetBenchmarkButtonDisplay(false);
       demo.animateSun.isOn= wasSunAniamted;
    }

    // Update is called once per frame
    void Update()
    {
        if (evaluatingPreset == EVALUATION_STAGE.INACTIVE)
            return;

        if (evaluatingPreset != EVALUATION_STAGE.SHOW_RESULTS)
        {
            if (currentTime >= evaluationTime)
            {
                currentTime = 0.0f;

                CompileAverage(evaluatingPreset);
                evaluatingPreset++;

                if (evaluatingPreset == EVALUATION_STAGE.SHOW_RESULTS)
                {
                    CompileResults();
                }
                else
                {
                    cloudManager.preset++;
                    cloudManager.StopWMTransition();
                    cloudManager.StartWMTransition(0.0f);
                }


            }
            else
            {
                currentTime += Time.unscaledDeltaTime;
                evaluatingFrames.Add(Time.unscaledDeltaTime);
            }
        }
        else
        {
            ShowResults();
        }

    }


    private void CompileAverage(EVALUATION_STAGE evaluatingPreset)
    {
        DiscardExtremes();
        benchmarkData[(int)evaluatingPreset].averageSeconds = GetAverage();
        benchmarkData[(int)evaluatingPreset].highestSeconds = evaluatingFrames[evaluatingFrames.Count - 1];
        benchmarkData[(int)evaluatingPreset].lowestSeconds = evaluatingFrames[0];
        evaluatingFrames.Clear();

        Debug.Log("Compiled data for " + evaluatingPreset.ToString());
    }

    private void CompileResults()
    {
        benchmarkData[benchmarkData.Count - 1].averageSeconds = 0.0f;
        benchmarkData[benchmarkData.Count - 1].highestSeconds = double.MinValue;
        benchmarkData[benchmarkData.Count - 1].lowestSeconds = double.MaxValue;
        for (int i = 0; i < benchmarkData.Count - 1; i++)
        {
            benchmarkData[benchmarkData.Count - 1].averageSeconds += benchmarkData[i].averageSeconds;
            benchmarkData[benchmarkData.Count - 1].highestSeconds = Math.Max(benchmarkData[i].highestSeconds, benchmarkData[benchmarkData.Count - 1].highestSeconds);
            benchmarkData[benchmarkData.Count - 1].lowestSeconds = Math.Min(benchmarkData[i].lowestSeconds, benchmarkData[benchmarkData.Count - 1].lowestSeconds);
        }
        benchmarkData[benchmarkData.Count - 1].averageSeconds /= benchmarkData.Count - 1;
        Debug.Log("Results Compiled!");
    }

    private void ShowResults()
    {
        demo.ShowBenchmarkResults();
    }

    void DiscardExtremes()
    {
        evaluatingFrames.Sort();
        evaluatingFrames.RemoveAt(evaluatingFrames.Count - 1);
        evaluatingFrames.RemoveAt(0);
    }

    float GetAverage()
    {
        float res = 0.0f;
        foreach (float item in evaluatingFrames)
        {
            res += item;
        }
        res /= evaluatingFrames.Count;
        return res;
    }


    //This .csv file is intended to be opened by excel with Europe separator settings (semicolon) instead of comma
    public void SaveResultsToDisk()
    {
        if (evaluatingPreset != EVALUATION_STAGE.SHOW_RESULTS || resultsSaved)
            return;

        resultsSaved = true;

        TextWriter writter;
        if (!System.IO.File.Exists(filename))
        {
            writter = new StreamWriter(filename, false);
            writter.WriteLine("Abbreviations:");
            writter.WriteLine();
            writter.WriteLine("Sparse: ; Sp");
            writter.WriteLine("Cloudy: ; Cl");
            writter.WriteLine("Stormy: ; St");
            writter.WriteLine("Overcast: ; Ov");
            writter.WriteLine();
            writter.WriteLine("Average: ; Avg");
            writter.WriteLine("Highest: ; Hi");
            writter.WriteLine("Lowest: ; Lo");
            writter.WriteLine();
            writter.WriteLine();

            writter.WriteLine("Date; Time; Resolution; Section Analysis Duration; " +
                "Sp Avg MS; Cl Avg MS; St Avg MS; Ov Avg MS; Total Avg MS; " +
                "Sp Avg FPS; Cl Avg FPS; St Avg FPS; Ov Avg FPS; Total Avg FPS; " +
                "Sp Hi FPS; Cl Hi FPS; St Hi FPS; Ov Hi FPS; Total Hi FPS; " +
                "Sp Lo FPS; Cl Lo FPS; St Lo FPS; Ov Lo FPS; Total Lo FPS");
            writter.Close();
        }


        string avgMS = "";
        string avgFPS = "";
        string highestFPS = "";
        string lowestFPS = "";


        foreach (BenchmarkSectionData item in benchmarkData)
        {
            double averageMS = (item.averageSeconds * 1000.0);

            avgMS += ((float)averageMS).ToString() + "; ";
            avgFPS += ((float)(1.0 / item.averageSeconds)).ToString() + "; ";
            highestFPS += ((float)(1.0 / item.lowestSeconds)).ToString() + "; ";
            lowestFPS += ((float)(1.0 / item.highestSeconds)).ToString() + "; ";
        }


        string resolution = "";

        switch (cloudManager.resolution)
        {
            case Aetherius.CLOUD_RESOLUTION.ORIGINAL:
                resolution = "Original";
                break;
            case Aetherius.CLOUD_RESOLUTION.HALF:
                resolution = "Half";
                break;
            case Aetherius.CLOUD_RESOLUTION.QUARTER:
                resolution = "Quarter";
                break;
        }


        writter = new StreamWriter(filename, true);
        writter.WriteLine(System.DateTime.Now.ToString("dd/MM/yyyy") + ";" + System.DateTime.Now.ToString("HH: mm: ss") + ";" +
          resolution + ";"+ evaluationTime.ToString() + ";" + avgMS + avgFPS + highestFPS + lowestFPS);
        writter.Close();

    }



}