using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Worley_Settings", menuName = "ScriptableObjects/Aetherius/WorleyNoiseSettings",order = 1)]
public class WorleySettings : ScriptableObject
{
    public int seed=0;
    [Range(1, 100)]
    public int numberOfCellsAxisA=10;
    [Range(1, 100)]
    public int numberOfCellsAxisB=15;
    [Range(1, 100)]
    public int numberOfCellsAxisC=35;

    WorleySettings()
    {
        seed = 0;
        numberOfCellsAxisA = 10;
        numberOfCellsAxisB = 15;
        numberOfCellsAxisC = 35;
    }
}
