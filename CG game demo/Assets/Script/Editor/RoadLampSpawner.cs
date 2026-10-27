using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieGame.Editor
{
    public class RoadLampSpawner : EditorWindow
    {
        private const string DefaultLampPrefabPath = "Assets/Project/Bake lamp.prefab";
        private const string GroupParentName = "[Street_Lamps]";

        [Header("Lamp Prefab")]
        [SerializeField] private GameObject _lampPrefab;

        [Header("Multi-Point Road Path (Waypoints)")]
        [SerializeField] private List<Vector3> _waypoints = new List<Vector3>();
        private bool _isAddingWaypoints = false;
        private bool _showMoveHandles = true;

        [Header("Generation Settings")]
        private float _lampSpacing = 25f;
        private float _roadSideOffset = 4.0f;
        private RoadSideOption _sideOption = RoadSideOption.BothSides;
        private bool _faceRoadCenter = true;
        private float _heightOffset = 0f;

        [Header("Single Click-to-Place Mode")]
        private bool _clickToPlaceActive = false;
        private float _customRotationY = 0f;
        private bool _alignWithNormal = true;

        private Vector2 _scrollPos;

        public enum RoadSideOption
        {
            BothSides,
            RightSideOnly,
            LeftSideOnly,
            AlternatingZigZag
        }

        [MenuItem("Tools/City/Road Lamp Spawner", false, 10)]
        [MenuItem("Tools/Zombie/Road Lamp Spawner", false, 10)]
        public static void OpenWindow()
        {
            var win = GetWindow<RoadLampSpawner>("Lamp Spawner");
            win.minSize = new Vector2(400, 560);
            win.Show();
        }

        private void OnEnable()
        {
            if (_lampPrefab == null)
            {
                _lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLampPrefabPath);
            }
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Space(8);
            GUILayout.Label("Road & Street Lamp Spawner (Multi-Point)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Place 'Bake lamp' along curving or straight roads across the map!\n" +
                "• Click 'Add Road Points' to click multiple points (curves/turns) along any road.\n" +
                "• Lamps are automatically generated along both sides of your custom path.",
                MessageType.Info
            );

            GUILayout.Space(6);
            _lampPrefab = (GameObject)EditorGUILayout.ObjectField("Lamp Prefab", _lampPrefab, typeof(GameObject), false);
            if (_lampPrefab == null)
            {
                EditorGUILayout.HelpBox("Please assign 'Bake lamp' prefab above!", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            GUILayout.Space(10);
            DrawMultiPointSection();

            GUILayout.Space(14);
            DrawClickToPlaceSection();

            GUILayout.Space(14);
            DrawHierarchyUtilitySection();

            GUILayout.Space(14);
            DrawBakeLightingSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawMultiPointSection()
        {
            EditorGUILayout.LabelField("Mode 1: Multi-Point Road Path (Recommended)", EditorStyles.boldLabel);

            // Active toggle for adding waypoints
            GUI.backgroundColor = _isAddingWaypoints ? new Color(0.3f, 0.9f, 0.4f) : Color.white;
            string addBtnText = _isAddingWaypoints
                ? "✔ Click Mode ACTIVE: Click on Road in Scene (Press ESC to finish)"
                : "➕ Add Road Points (Click Road in Scene View)";
            if (GUILayout.Button(addBtnText, GUILayout.Height(36)))
            {
                _isAddingWaypoints = !_isAddingWaypoints;
                if (_isAddingWaypoints)
                {
                    _clickToPlaceActive = false;
                }
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (_isAddingWaypoints)
            {
                EditorGUILayout.HelpBox(
                    "👉 Click anywhere along the road in the Scene View to add path points.\n" +
                    "• Add points around corners/bends to follow the road curve accurately.\n" +
                    "• Press [Escape] or click the green button above when done.",
                    MessageType.Info
                );
            }

            // Path information summary
            float totalDist = CalculateTotalPathDistance();
            int estimatedLamps = CalculateEstimatedLampCount(totalDist);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Points Set: {_waypoints.Count}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Road Length: {totalDist:F1} meters");
            if (_waypoints.Count >= 2)
            {
                EditorGUILayout.LabelField($"Estimated Lamps to Spawn: ~{estimatedLamps} lamps", EditorStyles.miniBoldLabel);
            }
            EditorGUILayout.EndVertical();

            // Waypoint control buttons
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _waypoints.Count > 0;
            if (GUILayout.Button("Undo Last Point", GUILayout.Height(26)))
            {
                Undo.RecordObject(this, "Remove Last Waypoint");
                if (_waypoints.Count > 0)
                {
                    _waypoints.RemoveAt(_waypoints.Count - 1);
                    SceneView.RepaintAll();
                }
            }
            if (GUILayout.Button("Reverse Path", GUILayout.Height(26)))
            {
                Undo.RecordObject(this, "Reverse Waypoints");
                _waypoints.Reverse();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Clear All Points", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Clear Road Points?", "Remove all placed waypoints?", "Clear", "Cancel"))
                {
                    Undo.RecordObject(this, "Clear Waypoints");
                    _waypoints.Clear();
                    SceneView.RepaintAll();
                }
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            _showMoveHandles = EditorGUILayout.Toggle("Show Move Handles in Scene", _showMoveHandles);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Lamp Placement Options", EditorStyles.boldLabel);
            _lampSpacing = EditorGUILayout.Slider("Spacing Between Lamps (m)", _lampSpacing, 5f, 100f);
            _roadSideOffset = EditorGUILayout.Slider("Offset from Center (Curb)", _roadSideOffset, 0f, 30f);
            _sideOption = (RoadSideOption)EditorGUILayout.EnumPopup("Road Sides", _sideOption);
            _faceRoadCenter = EditorGUILayout.Toggle("Rotate Facing Road Center", _faceRoadCenter);
            _heightOffset = EditorGUILayout.FloatField("Ground Height Offset", _heightOffset);

            GUILayout.Space(8);
            GUI.enabled = _waypoints.Count >= 2;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
            if (GUILayout.Button("⚡ Generate Lamps Along Road Path", GUILayout.Height(40)))
            {
                GenerateLampsAlongPath();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            if (_waypoints.Count < 2)
            {
                EditorGUILayout.HelpBox("Add at least 2 points to generate lamps along a road.", MessageType.None);
            }
        }

        private void DrawClickToPlaceSection()
        {
            EditorGUILayout.LabelField("Mode 2: Single Click-to-Place (Quick Stamp)", EditorStyles.boldLabel);

            GUI.backgroundColor = _clickToPlaceActive ? new Color(0.3f, 0.9f, 0.4f) : Color.white;
            if (GUILayout.Button(_clickToPlaceActive ? "✔ Click-to-Place ACTIVE (Shift + Click in Scene)" : "Activate Single Click-to-Place Mode", GUILayout.Height(30)))
            {
                _clickToPlaceActive = !_clickToPlaceActive;
                if (_clickToPlaceActive)
                {
                    _isAddingWaypoints = false;
                }
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (_clickToPlaceActive)
            {
                EditorGUILayout.HelpBox("Hold [SHIFT] + Left Click on any road/ground surface in Scene View to place a single lamp.", MessageType.None);
                _customRotationY = EditorGUILayout.Slider("Rotation Angle (Y)", _customRotationY, 0f, 360f);
                _alignWithNormal = EditorGUILayout.Toggle("Align with Terrain Slope", _alignWithNormal);
            }
        }

        private void DrawHierarchyUtilitySection()
        {
            EditorGUILayout.LabelField("Scene Organization", EditorStyles.boldLabel);
            GameObject parent = GameObject.Find(GroupParentName);
            int count = parent != null ? parent.transform.childCount : 0;
            EditorGUILayout.LabelField($"Total Lamps in Scene: {count}");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All Lamps"))
            {
                if (parent != null) Selection.activeGameObject = parent;
            }
            if (GUILayout.Button("Delete All Lamps"))
            {
                if (parent != null && EditorUtility.DisplayDialog("Delete All Lamps?", $"Delete all {count} placed street lamps in the scene?", "Delete", "Cancel"))
                {
                    Undo.DestroyObjectImmediate(parent);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBakeLightingSection()
        {
            EditorGUILayout.LabelField("Lamp Lighting & Baking Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set Lamps to Realtime (Lights Up Now!)", GUILayout.Height(30)))
            {
                SetAllLampsLightMode(LightmapBakeType.Realtime);
            }
            if (GUILayout.Button("Set Lamps to Baked", GUILayout.Height(30)))
            {
                SetAllLampsLightMode(LightmapBakeType.Baked);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("Mark Terrain & Roads as 'Contribute GI' (Static)", GUILayout.Height(28)))
            {
                MarkSurfacesAsStatic();
            }

            if (Lightmapping.lightingSettings == null)
            {
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("⚠ Fix 'Missing (Lighting Settings)' Asset", GUILayout.Height(32)))
                {
                    CreateAndAssignLightingSettings();
                }
                GUI.backgroundColor = Color.white;
            }

            GUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.6f, 0.1f);
            if (GUILayout.Button("🔥 Bake Lighting Now (Progressive CPU)", GUILayout.Height(34)))
            {
                if (Lightmapping.lightingSettings == null)
                {
                    CreateAndAssignLightingSettings();
                }
                Lightmapping.BakeAsync();
            }
            GUI.backgroundColor = Color.white;
        }

        [MenuItem("Tools/City/Fix Missing Lighting Settings & Enable Baking", false, 20)]
        public static void CreateAndAssignLightingSettings()
        {
            string path = "Assets/Settings/SceneLightingSettings.lighting";
            LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings();
                settings.name = "SceneLightingSettings";
                settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
                settings.lightmapResolution = 10f;
                settings.directSampleCount = 32;
                settings.indirectSampleCount = 256;
                settings.environmentSampleCount = 128;
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
            }
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
            Debug.Log("[Lighting] Successfully configured Progressive CPU Lighting Settings Asset!");
        }

        private void SetAllLampsLightMode(LightmapBakeType mode)
        {
            GameObject parent = GameObject.Find(GroupParentName);
            Light[] lights = parent != null ? parent.GetComponentsInChildren<Light>(true) : Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var l in lights)
            {
                if (l.type == LightType.Spot || (l.transform.parent != null && l.transform.parent.name.Contains("lamp")))
                {
                    l.lightmapBakeType = mode;
                    EditorUtility.SetDirty(l);
                    count++;
                }
            }

            if (_lampPrefab != null)
            {
                string path = AssetDatabase.GetAssetPath(_lampPrefab);
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(path);
                foreach (var l in prefabContents.GetComponentsInChildren<Light>(true))
                {
                    l.lightmapBakeType = mode;
                }
                PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            Debug.Log($"[RoadLampSpawner] Set {count} lamps to {mode} mode!");
        }

        private void MarkSurfacesAsStatic()
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (var t in terrains)
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI);
                EditorUtility.SetDirty(t.gameObject);
            }

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var r in renderers)
            {
                string n = r.gameObject.name.ToLower();
                if (n.Contains("road") || n.Contains("street") || n.Contains("ground") || n.Contains("floor") || n.Contains("city"))
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI);
                    EditorUtility.SetDirty(r.gameObject);
                    count++;
                }
            }
            Debug.Log($"[RoadLampSpawner] Marked terrains and {count} road/ground meshes as Contribute GI (Static)!");
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            // Multi-point drawing and handles
            DrawPathGizmosAndHandles();

            // Handling Add Waypoints Click Mode
            if (_isAddingWaypoints)
            {
                int controlID = GUIUtility.GetControlID(FocusType.Passive);
                HandleUtility.AddDefaultControl(controlID);

                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    _isAddingWaypoints = false;
                    e.Use();
                    Repaint();
                    return;
                }

                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Handles.color = Color.yellow;
                    Handles.DrawWireDisc(hit.point, hit.normal, 1.2f);
                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.yellow } };
                    Handles.Label(hit.point + Vector3.up * 1.2f, $"[Click to Add Point #{_waypoints.Count + 1}]", style);

                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        Undo.RecordObject(this, "Add Road Waypoint");
                        _waypoints.Add(hit.point);
                        e.Use();
                        Repaint();
                    }
                }

                sceneView.Repaint();
                return;
            }

            // Single Click-to-Place Mode
            if (_clickToPlaceActive)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Handles.color = new Color(1f, 0.8f, 0.2f, 0.8f);
                    Handles.DrawWireDisc(hit.point, hit.normal, 0.8f);
                    Handles.DrawLine(hit.point, hit.point + Vector3.up * 4.5f);

                    if (e.type == EventType.MouseDown && e.button == 0 && (e.shift || e.control))
                    {
                        Quaternion rot = Quaternion.Euler(0f, _customRotationY, 0f);
                        if (_alignWithNormal)
                        {
                            rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * rot;
                        }

                        Vector3 spawnPos = hit.point + Vector3.up * _heightOffset;
                        SpawnSingleLamp(spawnPos, rot);
                        e.Use();
                    }
                }
                sceneView.Repaint();
            }
        }

        private void DrawPathGizmosAndHandles()
        {
            if (_waypoints == null || _waypoints.Count == 0) return;

            // Draw line connecting all points
            if (_waypoints.Count >= 2)
            {
                Handles.color = Color.cyan;
                for (int i = 0; i < _waypoints.Count - 1; i++)
                {
                    Handles.DrawLine(_waypoints[i] + Vector3.up * 0.1f, _waypoints[i + 1] + Vector3.up * 0.1f);
                    // Draw direction arrow
                    Vector3 mid = (_waypoints[i] + _waypoints[i + 1]) * 0.5f;
                    Vector3 dir = (_waypoints[i + 1] - _waypoints[i]).normalized;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        Handles.ArrowHandleCap(0, mid, Quaternion.LookRotation(dir), 2.5f, EventType.Repaint);
                    }
                }
            }

            // Draw discs & numbers
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            for (int i = 0; i < _waypoints.Count; i++)
            {
                Color c = (i == 0) ? Color.green : (i == _waypoints.Count - 1 ? Color.red : Color.cyan);
                Handles.color = c;
                Handles.DrawSolidDisc(_waypoints[i] + Vector3.up * 0.05f, Vector3.up, 0.8f);
                Handles.Label(_waypoints[i] + Vector3.up * 1.5f, $"#{i + 1}", labelStyle);

                // Move Handles if enabled and not currently clicking to add
                if (_showMoveHandles && !_isAddingWaypoints)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.PositionHandle(_waypoints[i], Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Move Waypoint");
                        _waypoints[i] = newPos;
                    }
                }
            }
        }

        private void SpawnSingleLamp(Vector3 position, Quaternion rotation)
        {
            if (_lampPrefab == null) return;

            GameObject parent = GetOrCreateParent();
            GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(_lampPrefab, parent.transform);
            lamp.transform.position = position;
            lamp.transform.rotation = rotation;

            Undo.RegisterCreatedObjectUndo(lamp, "Spawn Lamp");
            Debug.Log($"[RoadLampSpawner] Placed lamp at {position}");
        }

        private void GenerateLampsAlongPath()
        {
            if (_lampPrefab == null || _waypoints == null || _waypoints.Count < 2) return;

            float totalDist = CalculateTotalPathDistance();
            if (totalDist <= 0.5f)
            {
                Debug.LogWarning("[RoadLampSpawner] Path length is too short to place lamps!");
                return;
            }

            int sampleCount = Mathf.Max(2, Mathf.RoundToInt(totalDist / _lampSpacing) + 1);
            float step = totalDist / (sampleCount - 1);

            GameObject parent = GetOrCreateParent();
            Undo.RegisterCompleteObjectUndo(parent, "Generate Road Lamps");

            int spawnedCount = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                float targetDist = i * step;
                if (!SamplePathAtDistance(targetDist, out Vector3 centerPos, out Vector3 forwardDir))
                {
                    continue;
                }

                Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
                if (rightDir.sqrMagnitude < 0.001f)
                {
                    rightDir = Vector3.right;
                }

                bool placeRight = false;
                bool placeLeft = false;

                switch (_sideOption)
                {
                    case RoadSideOption.BothSides:
                        placeRight = true;
                        placeLeft = true;
                        break;
                    case RoadSideOption.RightSideOnly:
                        placeRight = true;
                        break;
                    case RoadSideOption.LeftSideOnly:
                        placeLeft = true;
                        break;
                    case RoadSideOption.AlternatingZigZag:
                        if (i % 2 == 0) placeRight = true;
                        else placeLeft = true;
                        break;
                }

                // Right Side Lamp
                if (placeRight)
                {
                    Vector3 samplePos = centerPos + rightDir * _roadSideOffset;
                    if (RaycastToGround(samplePos, out Vector3 groundPos, out Vector3 groundNormal))
                    {
                        Quaternion rot = _faceRoadCenter
                            ? Quaternion.LookRotation(-rightDir, groundNormal)
                            : Quaternion.LookRotation(forwardDir, groundNormal);

                        GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(_lampPrefab, parent.transform);
                        lamp.transform.position = groundPos + Vector3.up * _heightOffset;
                        lamp.transform.rotation = rot;
                        Undo.RegisterCreatedObjectUndo(lamp, "Spawn Road Lamp");
                        spawnedCount++;
                    }
                }

                // Left Side Lamp
                if (placeLeft)
                {
                    Vector3 samplePos = centerPos - rightDir * _roadSideOffset;
                    if (RaycastToGround(samplePos, out Vector3 groundPos, out Vector3 groundNormal))
                    {
                        Quaternion rot = _faceRoadCenter
                            ? Quaternion.LookRotation(rightDir, groundNormal)
                            : Quaternion.LookRotation(forwardDir, groundNormal);

                        GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(_lampPrefab, parent.transform);
                        lamp.transform.position = groundPos + Vector3.up * _heightOffset;
                        lamp.transform.rotation = rot;
                        Undo.RegisterCreatedObjectUndo(lamp, "Spawn Road Lamp");
                        spawnedCount++;
                    }
                }
            }

            Debug.Log($"[RoadLampSpawner] Generated {spawnedCount} lamps along {totalDist:F1}m road path!");
        }

        private bool SamplePathAtDistance(float targetDist, out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;

            if (_waypoints.Count < 2) return false;

            float accumulated = 0f;
            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                float segmentLen = Vector3.Distance(_waypoints[i], _waypoints[i + 1]);
                if (segmentLen <= 0.0001f) continue;

                if (accumulated + segmentLen >= targetDist || i == _waypoints.Count - 2)
                {
                    float t = Mathf.Clamp01((targetDist - accumulated) / segmentLen);
                    position = Vector3.Lerp(_waypoints[i], _waypoints[i + 1], t);
                    forward = (_waypoints[i + 1] - _waypoints[i]).normalized;
                    return true;
                }

                accumulated += segmentLen;
            }

            position = _waypoints[_waypoints.Count - 1];
            forward = (_waypoints[_waypoints.Count - 1] - _waypoints[_waypoints.Count - 2]).normalized;
            return true;
        }

        private float CalculateTotalPathDistance()
        {
            if (_waypoints == null || _waypoints.Count < 2) return 0f;
            float total = 0f;
            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                total += Vector3.Distance(_waypoints[i], _waypoints[i + 1]);
            }
            return total;
        }

        private int CalculateEstimatedLampCount(float totalDist)
        {
            if (totalDist <= 0.5f) return 0;
            int samples = Mathf.Max(2, Mathf.RoundToInt(totalDist / _lampSpacing) + 1);
            switch (_sideOption)
            {
                case RoadSideOption.BothSides:
                    return samples * 2;
                case RoadSideOption.RightSideOnly:
                case RoadSideOption.LeftSideOnly:
                case RoadSideOption.AlternatingZigZag:
                    return samples;
                default:
                    return samples;
            }
        }

        private bool RaycastToGround(Vector3 samplePoint, out Vector3 groundPoint, out Vector3 normal)
        {
            Vector3 origin = samplePoint + Vector3.up * 50f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 120f))
            {
                groundPoint = hit.point;
                normal = hit.normal;
                return true;
            }
            groundPoint = samplePoint;
            normal = Vector3.up;
            return true;
        }

        private GameObject GetOrCreateParent()
        {
            GameObject parent = GameObject.Find(GroupParentName);
            if (parent == null)
            {
                parent = new GameObject(GroupParentName);
                Undo.RegisterCreatedObjectUndo(parent, "Create Lamps Parent");
            }
            return parent;
        }
    }
}
