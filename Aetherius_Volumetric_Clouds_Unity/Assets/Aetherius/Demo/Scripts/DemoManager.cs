using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Aetherius.Demo
{

    public class DemoManager : MonoBehaviour
    {
        public struct BenchmarkSectionDisplay
        {
            public Text fps;
            public Text ms;
            public Text highest;
            public Text lowest;
        }

        public float presetTransitionTime = 10.0f;

        public Aetherius.CloudManager managerRef;

        [HideInInspector]
        public Benchmark benchmarkRef;
        public Text fpsCounter;
        public Text msCounter;
        public Dropdown resolution;
        public Dropdown preset;
        public GameObject benchmarkToggleButton;
        public Canvas mainCanvas;
        public Canvas benchmarkCanvas;
        public Canvas benchmarkExcludeCanvas;
        public Light sun;
        public Slider dayNightCycleSlider;
        public Toggle animateSunToggle;
        public bool animateSun;

        Text benchmarkToggleText;
        Image benchmarkToggleImage;

        List<BenchmarkSectionDisplay> displaySections = new List<BenchmarkSectionDisplay>();

        Color originalBenchmarkColor;

        Quaternion initialSunRot;

        [HideInInspector]
        public bool cinematicMode = false;

        // Start is called before the first frame update
        void Start()
        {
            cinematicMode = false;
            mainCanvas.gameObject.SetActive(true);
            benchmarkCanvas.gameObject.SetActive(true);
            benchmarkToggleImage = benchmarkToggleButton.GetComponent<Image>();
            benchmarkToggleText = benchmarkToggleButton.transform.Find("Text").GetComponent<Text>();


            AssignChildren();
            originalBenchmarkColor = benchmarkToggleImage.color;
            SetBenchmarkButtonDisplay(false);
            initialSunRot = sun.transform.rotation;

        }

        // Update is called once per frame
        void Update()
        {
            HandleInput();
            SetSunPos();

            fpsCounter.text = "FPS: " + 1.0 / Time.unscaledDeltaTime;
            msCounter.text = "MS: " + Time.unscaledDeltaTime * 1000.0f;
            SetResolutionText();
            SetPresetText();

        }

        void SetSunPos()
        {
            if (animateSun)
            {
                dayNightCycleSlider.value = (dayNightCycleSlider.value + Time.deltaTime * 2.0f) % dayNightCycleSlider.maxValue;
            }

            sun.transform.rotation = initialSunRot * Quaternion.AngleAxis(dayNightCycleSlider.value, Vector3.right);
            if (!cinematicMode)
            {
                animateSun = animateSunToggle.isOn;
            }
        }

        void SetResolutionText()
        {
            if (resolution.value != (int)managerRef.resolution)
                resolution.value = (int)managerRef.resolution;
        }

        void SetPresetText()
        {
            if (preset.value != (int)managerRef.preset)
                preset.value = (int)managerRef.preset;
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


        public void SetResuloution(int val)
        {
            managerRef.resolution = (Aetherius.CLOUD_RESOLUTION)val;
        }

        public void SetPreset(int val)
        {
            if ((Aetherius.CLOUD_PRESET)val == managerRef.preset)
                return;


            managerRef.preset = (Aetherius.CLOUD_PRESET)val;
            managerRef.StartWMTransition(presetTransitionTime);
        }


        public void ShowBenchmarkResults()
        {
            for (int i = 0; i < benchmarkRef.benchmarkData.Count; ++i)
            {
                BenchmarkSectionData currentData = benchmarkRef.benchmarkData[i];

                double averageMS = (currentData.averageSeconds * 1000.0);

                displaySections[i].fps.text = "AVG FPS: " + ((float)(1.0 / currentData.averageSeconds)).ToString();
                displaySections[i].ms.text = "AVG MS: " + ((float)averageMS).ToString();
                displaySections[i].lowest.text = "LOWEST FPS: " + ((float)(1.0 / currentData.highestSeconds)).ToString();
                displaySections[i].highest.text = "HIGHEST FPS: " + ((float)(1.0 / currentData.lowestSeconds)).ToString();

            }

            benchmarkCanvas.gameObject.SetActive(true);

        }

        public void SetBenchmarkButtonDisplay(bool benchmarkActive)
        {
            if (benchmarkActive)
            {
                benchmarkToggleImage.color = Color.blue;
                benchmarkToggleText.text = "Finish Benchmark (B)";
                benchmarkExcludeCanvas.gameObject.SetActive(false);
            }
            else
            {
                benchmarkToggleImage.color = originalBenchmarkColor;
                benchmarkToggleText.text = "Start Benchmark (B)";
                benchmarkCanvas.gameObject.SetActive(false);
                benchmarkExcludeCanvas.gameObject.SetActive(true);
            }
        }


        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Quit();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                cinematicMode = !cinematicMode;

                mainCanvas.gameObject.SetActive(!cinematicMode);
                benchmarkRef.StopBenchmark();

                if (!cinematicMode)
                    animateSunToggle.isOn = animateSun;



            }

            if( benchmarkRef.evaluatingPreset ==Benchmark.EVALUATION_STAGE.INACTIVE)
            {
                if(Input.GetKeyDown(KeyCode.Alpha4) && managerRef.preset != CLOUD_PRESET.SPARSE)
                {
                    managerRef.preset = CLOUD_PRESET.SPARSE;
                    managerRef.StartWMTransition(presetTransitionTime);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha5) && managerRef.preset != CLOUD_PRESET.CLOUDY)
                {
                    managerRef.preset = CLOUD_PRESET.CLOUDY;
                    managerRef.StartWMTransition(presetTransitionTime);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha6) && managerRef.preset != CLOUD_PRESET.STORMY)
                {
                    managerRef.preset = CLOUD_PRESET.STORMY;
                    managerRef.StartWMTransition(presetTransitionTime);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha7) && managerRef.preset != CLOUD_PRESET.OVERCAST)
                {
                    managerRef.preset = CLOUD_PRESET.OVERCAST;
                    managerRef.StartWMTransition(presetTransitionTime);
                }
            }

            if (!cinematicMode && Input.GetKeyDown(KeyCode.B))
            {
                benchmarkRef.ToggleBenchmark();
            }


            if (Input.GetKeyDown(KeyCode.Alpha1))
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

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }


}