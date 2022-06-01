using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoManager : MonoBehaviour
{

    public Aetherius.CloudManager managerRef;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            Quit();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            GetComponent<Benchmark>().ToggleBenchmark();
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
