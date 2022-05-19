using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Aetherius
{
    [CustomEditor(typeof(CloudManager))]
    [CanEditMultipleObjects]
    public class CloudManagerEditor : Editor
    {
        private CloudManager _myScript;
        private Editor _editor;


        void OnEnable()
        {
            _myScript = (CloudManager)target;
        }
    }

}