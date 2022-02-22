using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [CreateAssetMenu(fileName = "Improved_Perlin_Settings", menuName = "ScriptableObjects/Aetherius/ImprovedPerlinNoiseSettings", order = 2)]
    public class ImprovedPerlinSettings : ScriptableObject
    {
        public int seed;

        [Range(1, 12)]
        public int numOctavesPerlin = 8;
        [Range(0.0f, 1.0f)]
        public float persistencePerlin = 0.5f;
        [Range(1.0f, 10.0f)]
        public float lacunarityPerlin = 2.5f;
        [Range(1, 32)]
        public int gridSizePerlin = 5;


        ImprovedPerlinSettings()
        {
            seed = 0;
            numOctavesPerlin = 8;
            persistencePerlin = 0.5f;
            lacunarityPerlin = 2.5f;
            gridSizePerlin = 5;
        }
    }
}
