using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BenchmarkSectionData
{
    public BenchmarkSectionData(String name)
    {
        this.name = name;
    }

    public String name;
    public float averageFPS;
    public float highestFPS;
    public float lowestFPS;
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



    Aetherius.CloudManager cloudManager;
    EVALUATION_STAGE evaluatingPreset;
    public float evaluationTime = 10.0f;
    float currentTime;
    List<float> evaluatingFrames;

    float averageFPS = -1.0f;
    float lowestFPS = -1.0f;
    float highestFPS = -1.0f;


    List<BenchmarkSectionData> benchmarkData;


    // Start is called before the first frame update
    void Start()
    {
        ResetData();
    }

    void ResetData()
    {
        evaluatingFrames = new List<float>();
        evaluatingPreset = EVALUATION_STAGE.INACTIVE;

        benchmarkData = new List<BenchmarkSectionData>();
        benchmarkData.Add(new BenchmarkSectionData("Sparse"));
        benchmarkData.Add(new BenchmarkSectionData("Cloudy"));
        benchmarkData.Add(new BenchmarkSectionData("Stormy"));
        benchmarkData.Add(new BenchmarkSectionData("Overcast"));

        averageFPS = -1.0f;
        lowestFPS = -1.0f;
        highestFPS = -1.0f;
        currentTime = 0.0f;
    }

    void StartBenchmark()
    {
        ResetData();
        evaluatingPreset = EVALUATION_STAGE.SPARSE;
        Camera.main.transform.rotation = Quaternion.Euler(-30,200,0);
    }

    void StopBenchmark()
    {
        evaluatingPreset = EVALUATION_STAGE.INACTIVE;
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
                cloudManager.preset++;
                evaluatingPreset++;
                cloudManager.StartWMTransition(0);

                if (evaluatingPreset == EVALUATION_STAGE.SHOW_RESULTS)
                {
                    CompileResults();
                }


            }
            else
            {
                currentTime += Time.unscaledDeltaTime;
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
        benchmarkData[(int)evaluatingPreset].averageFPS = GetAverage();
        benchmarkData[(int)evaluatingPreset].highestFPS = evaluatingFrames[evaluatingFrames.Count - 1];
        benchmarkData[(int)evaluatingPreset].lowestFPS = evaluatingFrames[0];
        evaluatingFrames.Clear();
    }

    private void CompileResults()
    {
        averageFPS = -1.0f;
        highestFPS = float.MinValue;
        lowestFPS = float.MaxValue;
        foreach (BenchmarkSectionData item in benchmarkData)
        {
            averageFPS += item.averageFPS;
            highestFPS = Mathf.Max(item.highestFPS, highestFPS);
            lowestFPS = Mathf.Min(item.lowestFPS, lowestFPS);
        }
        averageFPS /= benchmarkData.Count;
    }

    private void ShowResults()
    {
        //TODO
        throw new NotImplementedException();
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
}
