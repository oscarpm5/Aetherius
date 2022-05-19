using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius
{
    public enum CLOUD_CONTROL
    {
        SIMPLE,
        ADVANCED
    }

    public enum CLOUD_PRESET
    {
        SPARSE,
        CLOUDY,
        STORMY,
        OVERCAST
    }

    public enum TEXTURE_CHANNEL
    {
        R,
        G,
        B,
        A
    }

    public enum TEXTURE_TYPE
    {
        BASE_SHAPE,
        DETAIL
    }

    public enum TEXTURE_DIMENSIONS
    {
        TEX_2D,
        TEX_3D
    }

    [System.Serializable]
    public class CloudShape //TODO convert to serializable object?
    {
        public float baseShapeSize = 10000.0f; //Advanced
        public float detailSize = 1500.0f; //Advanced
        public float globalCoverage = 0.5f; //Advanced
        public float globalDensity = 0.2f; //Advanced
        public float weatherMapSize = 36000.0f; //Advanced
    }

}
