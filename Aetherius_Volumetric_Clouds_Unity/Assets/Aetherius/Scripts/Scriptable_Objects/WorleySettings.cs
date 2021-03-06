using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [CreateAssetMenu(fileName = "Worley_Settings", menuName = "ScriptableObjects/Aetherius/WorleyNoiseSettings", order = 1)]
    public class WorleySettings : ScriptableObject
    {
        public int seed = 0;
        [Range(1, 100)]
        public int numberOfCellsAxisA = 10;
        [Range(1, 100)]
        public int numberOfCellsAxisB = 15;
        [Range(1, 100)]
        public int numberOfCellsAxisC = 35;
        [Range(0.0f, 1.0f)]
        public float persistence = 0.5f;

        WorleySettings()
        {
            seed = 0;
            numberOfCellsAxisA = 10;
            numberOfCellsAxisB = 15;
            numberOfCellsAxisC = 35;
            persistence = 0.5f;
        }
    }
}
