using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Aetherius
{
    [CustomEditor(typeof(CloudManager))]
    public class CloudManagerEditor : Editor
    {
        CloudManager _myScript;

        SerializedProperty mode;
        SerializedProperty preset;
        SerializedProperty resolution;



        bool showScriptableObjectSettings = false;

        void OnEnable()
        {
            _myScript = (CloudManager)target;
            mode = serializedObject.FindProperty("mode");
            preset = serializedObject.FindProperty("preset");
            resolution = serializedObject.FindProperty("resolution");
        }

        void StartSection(string sectionLabel)
        {
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField(sectionLabel, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
        }

        void EndSection()
        {
            EditorGUI.indentLevel--;
        }

        void AtmosphereSection()
        {
            StartSection("Atmosphere");
            _myScript.planetRadiusKm = Mathf.Max(EditorGUILayout.IntField("Planet Radius", _myScript.planetRadiusKm), 0);
            _myScript.minCloudHeightMeters = EditorGUILayout.FloatField("Lowest Cloud Altitude", _myScript.minCloudHeightMeters);
            _myScript.maxCloudHeightMeters = EditorGUILayout.FloatField("Highest Cloud Altitude", _myScript.maxCloudHeightMeters);
            _myScript.hazeVisibilityAtmos = EditorGUILayout.Vector2IntField(new GUIContent("Atmospheric Haze Distance",
                "(X) distance at wich haze starts; (Y) distance at wich haze ends (If 0, default horizon distance is used. Use negative values to get rid of the haze)."), _myScript.hazeVisibilityAtmos);

            _myScript.cumulusHorizon = EditorGUILayout.Toggle(new GUIContent("Cumulus Clouds At The Horizon", "Option to make more epic cloudscapes, making the clouds toward the horizon appear more imposing"), _myScript.cumulusHorizon);
            if (_myScript.cumulusHorizon == true)
            {
                _myScript.cumulusHorizonGradient = EditorGUILayout.Vector2Field(new GUIContent("Start / End cumulus",
                    "Distance in meters of the start and end of the cumulus gradient from the camera towards the horizon"),
                    _myScript.cumulusHorizonGradient);
            }

            EndSection();
        }
        void WindSection()
        {
            StartSection("Wind");
            _myScript.windDirection = EditorGUILayout.Vector3Field("WindDirection", _myScript.windDirection);
            _myScript.baseShapeWindMult = EditorGUILayout.FloatField("Base Shape Wind Multiplier", _myScript.baseShapeWindMult);
            _myScript.detailShapeWindMult = EditorGUILayout.FloatField("Detail Shape Wind Multiplier", _myScript.detailShapeWindMult);
            _myScript.windDisplacesWeatherMap = EditorGUILayout.Toggle("Wind Displaces Clouds", _myScript.windDisplacesWeatherMap);
            _myScript.skewAmmount = EditorGUILayout.FloatField("Skew Ammount", _myScript.skewAmmount);

            EndSection();
        }

        void LightSection()
        {
            StartSection("Light");

            _myScript.sunLight = (Light)EditorGUILayout.ObjectField("Sun Light", _myScript.sunLight, typeof(Light), true);
            _myScript.lightIntensityMult = EditorGUILayout.FloatField("Light Intensity Multiplier", _myScript.lightIntensityMult);
            _myScript.ambientLightIntensity = Mathf.Max(EditorGUILayout.FloatField("Ambient Light Intensity Multiplier", _myScript.ambientLightIntensity), 0.0f);
            _myScript.scatterC = Mathf.Max(EditorGUILayout.FloatField("Scatter Coefficient", _myScript.scatterC), 0.0f);
            _myScript.absorptionC = Mathf.Max(EditorGUILayout.FloatField("Absorption Coefficient", _myScript.absorptionC), 0.0f);


            _myScript.shadowSize = Mathf.Max(EditorGUILayout.FloatField("Shadow Step Distance", _myScript.shadowSize), 0.0f);
            _myScript.softerShadows = EditorGUILayout.Toggle("Softer Shadows", _myScript.softerShadows);

            _myScript.lightIterations = EditorGUILayout.IntField("Light Iterations", _myScript.lightIterations);

            EndSection();
        }

        void QualitySection()
        {
            StartSection("Quality");

            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(resolution);
                if (check.changed)
                {
                    if (_myScript.resolution != (CLOUD_RESOLUTION)resolution.intValue)
                    {
                        _myScript.resolution = (CLOUD_RESOLUTION)resolution.intValue;
                    }
                }
            }


            _myScript.maxRayVisibilityDist = Mathf.Max(EditorGUILayout.IntField("Max Ray Distance", _myScript.maxRayVisibilityDist), 0);
            _myScript.blueNoise = (Texture2D)EditorGUILayout.ObjectField("Blue Noise", _myScript.blueNoise, typeof(Texture2D), false);


            EndSection();
        }

        void TextureGenerationSection()
        {
            StartSection("Texture Generation");

            _myScript.textureGenerator.computeShader = (ComputeShader)EditorGUILayout.ObjectField("Compute Shader", _myScript.textureGenerator.computeShader, typeof(ComputeShader), false);
            int newSeed = EditorGUILayout.IntField("Cloudscape Seed", _myScript.wmSeed);
            if (_myScript.wmSeed != newSeed)
            {
                _myScript.wmSeed = newSeed;
                _myScript.textureGenerator.GenerateWeatherMap(256, ref _myScript.textureGenerator.originalWM, _myScript.wmSeed, _myScript.preset);
            }

            showScriptableObjectSettings = EditorGUILayout.Foldout(showScriptableObjectSettings, "Scriptable Objects Noise Settings", true);
            if (showScriptableObjectSettings)
            {
                //Base Shape Noise
                using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                {

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Base Shape Noise Settings", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.BeginVertical();
                    _myScript.textureGenerator.perlinShapeSettings = (ImprovedPerlinSettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.perlinShapeSettings, typeof(ImprovedPerlinSettings), false);
                    _myScript.textureGenerator.worleyShapeSettings[0] = (WorleySettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.worleyShapeSettings[0], typeof(WorleySettings), false);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.LabelField("R Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    _myScript.textureGenerator.worleyShapeSettings[1] = (WorleySettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.worleyShapeSettings[1], typeof(WorleySettings), false);
                    EditorGUILayout.LabelField("G Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    _myScript.textureGenerator.worleyShapeSettings[2] = (WorleySettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.worleyShapeSettings[2], typeof(WorleySettings), false);
                    EditorGUILayout.LabelField("B Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    _myScript.textureGenerator.worleyShapeSettings[3] = (WorleySettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.worleyShapeSettings[3], typeof(WorleySettings), false);

                    EditorGUILayout.LabelField("A Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    if (check.changed) //If we changed any parameters of the resolution property, update its noise
                    {
                        _myScript.textureGenerator.GenerateBaseShapeNoise();
                    }

                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.Separator();

                //Detail Shape Noise
                using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                {

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Detail Shape Noise Settings", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    _myScript.textureGenerator.worleyDetailSettings[0] = (WorleySettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.worleyDetailSettings[0], typeof(WorleySettings), false);
                    EditorGUILayout.LabelField("R Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    _myScript.textureGenerator.worleyDetailSettings[1] = (WorleySettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.worleyDetailSettings[1], typeof(WorleySettings), false);
                    EditorGUILayout.LabelField("G Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    _myScript.textureGenerator.worleyDetailSettings[2] = (WorleySettings)EditorGUILayout.ObjectField(GUIContent.none, _myScript.textureGenerator.worleyDetailSettings[2], typeof(WorleySettings), false);
                    EditorGUILayout.LabelField("B Channel", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();

                    if (check.changed) //If we changed any parameters of the resolution property, update its noise
                    {
                        _myScript.textureGenerator.GenerateDetailNoise();

                    }

                    EditorGUILayout.EndVertical();
                }

            }


            EndSection();
        }



        public override void OnInspectorGUI()
        {

            //serializedObject.Update();
            
            
            //Debug code TODO delete
            {
                //EditorGUILayout.BeginVertical("GroupBox");
                //DrawDefaultInspector();
                //EditorGUILayout.EndVertical();
            }



            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(mode);
                if (check.changed)
                {
                    if (_myScript.mode != (CLOUD_CONTROL)mode.intValue)
                    {
                        _myScript.mode = (CLOUD_CONTROL)mode.intValue;
                    }
                }
            }


            EditorGUI.indentLevel++;
            switch (_myScript.mode)
            {
                case CLOUD_CONTROL.SIMPLE:
                    {
                        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
                        {
                            EditorGUILayout.PropertyField(preset);

                            if (check.changed)
                            {
                                if (_myScript.preset != (CLOUD_PRESET)preset.intValue)
                                {
                                    _myScript.preset = (CLOUD_PRESET)preset.intValue;
                                    _myScript.StartWMTransition();
                                }
                            }

                        }


                        //TODO won't be able to edit these in the near future
                        /*
                        _myScript.simple.baseShapeSize = EditorGUILayout.IntField("Base Shape Size",(int)_myScript.simple.baseShapeSize);
                        _myScript.simple.detailSize = EditorGUILayout.IntField("Detail Shape Size", (int)_myScript.simple.detailSize);
                        _myScript.simple.globalCoverage = EditorGUILayout.FloatField("Global Coverage", _myScript.simple.globalCoverage);
                        _myScript.simple.globalDensity = EditorGUILayout.FloatField("Global Density", _myScript.simple.globalDensity);
                        _myScript.simple.weatherMapSize = EditorGUILayout.IntField("Weather Map Size", (int)_myScript.simple.weatherMapSize);
                        */

                    }
                    break;
                case CLOUD_CONTROL.ADVANCED:
                    {

                        _myScript.advanced.baseShapeSize = EditorGUILayout.IntField("Base Shape Size", (int)_myScript.advanced.baseShapeSize);
                        _myScript.advanced.detailSize = EditorGUILayout.IntField("Detail Shape Size", (int)_myScript.advanced.detailSize);
                        _myScript.advanced.globalCoverage = EditorGUILayout.FloatField("Global Coverage", _myScript.advanced.globalCoverage);
                        _myScript.advanced.globalDensity = EditorGUILayout.FloatField("Global Density", _myScript.advanced.globalDensity);
                        _myScript.advanced.weatherMapSize = EditorGUILayout.IntField("Weather Map Size", (int)_myScript.advanced.weatherMapSize);

                        _myScript.densityCurveMultiplier1 = EditorGUILayout.FloatField("Layer1 Density Multiplier", _myScript.densityCurveMultiplier1);
                        _myScript.densityCurve1 = EditorGUILayout.CurveField("Layer1 Density Profile", _myScript.densityCurve1);
                        _myScript.densityCurveMultiplier2 = EditorGUILayout.FloatField("Layer2 Density Multiplier", _myScript.densityCurveMultiplier2);
                        _myScript.densityCurve2 = EditorGUILayout.CurveField("Layer2 Density Profile", _myScript.densityCurve2);
                        _myScript.densityCurveMultiplier3 = EditorGUILayout.FloatField("Layer3 Density Multiplier", _myScript.densityCurveMultiplier3);
                        _myScript.densityCurve3 = EditorGUILayout.CurveField("Layer3 Density Profile", _myScript.densityCurve3);



                    }
                    break;

            }
            EditorGUI.indentLevel--;


            AtmosphereSection();

            WindSection();

            LightSection();

            QualitySection();

            TextureGenerationSection();

            //serializedObject.ApplyModifiedProperties();
        }


    }

}