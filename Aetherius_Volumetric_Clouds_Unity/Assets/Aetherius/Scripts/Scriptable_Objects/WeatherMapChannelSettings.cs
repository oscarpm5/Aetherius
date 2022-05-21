using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    [CreateAssetMenu(fileName = "Weather_Map_Channel_Settings", menuName = "ScriptableObjects/Aetherius/WeatherMapChannelSettings", order = 3)]

    public class WeatherMapChannelSettings : ScriptableObject
    {
        //Perlin Related
        public int perlinGridSize;
        public int perlinOctaves;
        public float perlinPersistence;
        public float perlinLacunarity;

        //Worley Related
        public int worleyNumCellsA;
        public int worleyNumCellsB;
        public int worleyNumCellsC;
        public float worleyPersistence;

        //General
        public Vector2 minMaxBounds;
        public bool activeChannel;

        public WeatherMapChannelSettings()
        {
            perlinGridSize = 13;
            perlinOctaves = 4;
            perlinPersistence = 0.5f;
            perlinLacunarity = 2.0f;

            worleyNumCellsA = 3;
            worleyNumCellsB = 4;
            worleyNumCellsC = 5;
            worleyPersistence = 0.3f;
    
            minMaxBounds = new Vector2(-1.5f, 1.0f);
            activeChannel = true;
        }

        public WeatherMapChannelSettings(WeatherMapChannelSettingsData data)
        {
            perlinGridSize = data.perlinGridSize;
            perlinOctaves = data.perlinOctaves;
            perlinPersistence = data.perlinPersistence;
            perlinLacunarity = data.perlinLacunarity;

            worleyNumCellsA = data.worleyNumCellsA;
            worleyNumCellsB = data.worleyNumCellsB;
            worleyNumCellsC = data.worleyNumCellsC;
            worleyPersistence = data.worleyPersistence;

            minMaxBounds = data.minMaxBounds;
            activeChannel = data.activeChannel;
        }

    }

    public class WeatherMapChannelSettingsData
    {
        //Perlin Related
        public int perlinGridSize;
        public int perlinOctaves;
        public float perlinPersistence;
        public float perlinLacunarity;

        //Worley Related
        public int worleyNumCellsA;
        public int worleyNumCellsB;
        public int worleyNumCellsC;
        public float worleyPersistence;

        //General
        public Vector2 minMaxBounds;
        public bool activeChannel;

        public WeatherMapChannelSettingsData()
        {
            perlinGridSize = 13;
            perlinOctaves = 4;
            perlinPersistence = 0.5f;
            perlinLacunarity = 2.0f;

            worleyNumCellsA = 3;
            worleyNumCellsB = 4;
            worleyNumCellsC = 5;
            worleyPersistence = 0.3f;

            minMaxBounds = new Vector2(-1.5f, 1.0f);
            activeChannel = true;
        }

        public WeatherMapChannelSettingsData(WeatherMapChannelSettings settings)
        {
            perlinGridSize = settings.perlinGridSize;
            perlinOctaves = settings.perlinOctaves;
            perlinPersistence = settings.perlinPersistence;
            perlinLacunarity = settings.perlinLacunarity;

            worleyNumCellsA = settings.worleyNumCellsA;
            worleyNumCellsB = settings.worleyNumCellsB;
            worleyNumCellsC = settings.worleyNumCellsC;
            worleyPersistence = settings.worleyPersistence;

            minMaxBounds = settings.minMaxBounds;
            activeChannel = settings.activeChannel;
        }

    }
}
