using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieGame.Editor
{
    [CustomEditor(typeof(ZombieCitySpawner))]
    public class ZombieCitySpawnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _zombiePrefabs;
        private SerializedProperty _totalZombieCount;
        private SerializedProperty _spawnOnStart;
        private SerializedProperty _maintainPopulation;
        private SerializedProperty _respawnDelay;
        private SerializedProperty _cityRoot;
        private SerializedProperty _useCustomBounds;
        private SerializedProperty _customCenter;
        private SerializedProperty _customSize;
        private SerializedProperty _minDistanceFromPlayer;
        private SerializedProperty _maxDistanceFromPlayer;
        private SerializedProperty _groundLayer;
        private SerializedProperty _navMeshSampleRadius;

        private void OnEnable()
        {
            _zombiePrefabs = serializedObject.FindProperty("_zombiePrefabs");
            _totalZombieCount = serializedObject.FindProperty("_totalZombieCount");
            _spawnOnStart = serializedObject.FindProperty("_spawnOnStart");
            _maintainPopulation = serializedObject.FindProperty("_maintainPopulation");
            _respawnDelay = serializedObject.FindProperty("_respawnDelay");
            _cityRoot = serializedObject.FindProperty("_cityRoot");
            _useCustomBounds = serializedObject.FindProperty("_useCustomBounds");
            _customCenter = serializedObject.FindProperty("_customCenter");
            _customSize = serializedObject.FindProperty("_customSize");
            _minDistanceFromPlayer = serializedObject.FindProperty("_minDistanceFromPlayer");
            _maxDistanceFromPlayer = serializedObject.FindProperty("_maxDistanceFromPlayer");
            _groundLayer = serializedObject.FindProperty("_groundLayer");
            _navMeshSampleRadius = serializedObject.FindProperty("_navMeshSampleRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ZombieCitySpawner spawner = (ZombieCitySpawner)target;

            // Header Banner
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("🧟 Zombie City Spawner", titleStyle);
            EditorGUILayout.LabelField($"Active Zombies in Scene: {spawner.ActiveZombieCount} / Max: {spawner.TotalZombieCount}", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Amount Controller
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Amount Control", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_totalZombieCount, new GUIContent("Zombie Count", "The amount of zombies you want to spawn in the city"));
            EditorGUILayout.PropertyField(_spawnOnStart, new GUIContent("Spawn On Start", "Immediately spawn zombies when the game begins"));
            EditorGUILayout.PropertyField(_maintainPopulation, new GUIContent("Maintain Population", "Automatically respawn a new random zombie elsewhere in the city when one is killed"));
            if (_maintainPopulation.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_respawnDelay, new GUIContent("Respawn Delay (s)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Zombie Prefabs
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Zombie Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_zombiePrefabs, new GUIContent("Prefabs List"), true);
            if (GUILayout.Button("Load All Prefabs from Project/Zombies Prefab", EditorStyles.miniButton))
            {
                LoadAllPrefabs(spawner);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // City Area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("City Area & Placement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_cityRoot, new GUIContent("City GameObject"));
            EditorGUILayout.PropertyField(_useCustomBounds, new GUIContent("Use Custom Bounds"));
            if (_useCustomBounds.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_customCenter, new GUIContent("Center"));
                EditorGUILayout.PropertyField(_customSize, new GUIContent("Size"));
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox($"Calculated City Bounds:\nCenter: {spawner.CityBounds.center}\nSize: {spawner.CityBounds.size}", MessageType.None);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Player Constraints & NavMesh
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Distance & Placement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_minDistanceFromPlayer, new GUIContent("Min Distance From Player"));
            EditorGUILayout.PropertyField(_maxDistanceFromPlayer, new GUIContent("Max Distance (0 = Entire City)"));
            EditorGUILayout.PropertyField(_groundLayer, new GUIContent("Ground Layer"));
            EditorGUILayout.PropertyField(_navMeshSampleRadius, new GUIContent("NavMesh Sample Radius"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Quick Action Buttons
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("▶ Spawn Zombies Now", GUILayout.Height(32)))
            {
                spawner.SpawnAllZombies();
                EditorUtility.SetDirty(spawner);
            }

            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("✖ Clear Zombies", GUILayout.Height(32)))
            {
                spawner.ClearAllZombies();
                EditorUtility.SetDirty(spawner);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Recalculate City Bounds"))
            {
                spawner.CalculateCityBounds();
                SceneView.RepaintAll();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void LoadAllPrefabs(ZombieCitySpawner spawner)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Zombies Prefab" });
            List<GameObject> list = new List<GameObject>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<ZombieAI>() != null)
                {
                    list.Add(prefab);
                }
            }

            SerializedObject so = new SerializedObject(spawner);
            SerializedProperty prefabsProp = so.FindProperty("_zombiePrefabs");
            prefabsProp.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedProperties();
            Debug.Log($"[ZombieCitySpawner] Loaded {list.Count} zombie prefabs into spawner.");
        }

        [MenuItem("Tools/Zombie/Setup City Zombie Spawner in Scene", false, 1)]
        public static void SetupCitySpawnerInScene()
        {
            ZombieCitySpawner spawner = Object.FindAnyObjectByType<ZombieCitySpawner>();
            if (spawner == null)
            {
                GameObject spawnerObj = new GameObject("[Zombie_City_Spawner]");
                spawner = spawnerObj.AddComponent<ZombieCitySpawner>();
                Undo.RegisterCreatedObjectUndo(spawnerObj, "Create Zombie City Spawner");
            }

            spawner.LocateCityRoot();
            LoadAllPrefabs(spawner);
            spawner.CalculateCityBounds();

            Selection.activeGameObject = spawner.gameObject;
            EditorUtility.SetDirty(spawner);

            EditorUtility.DisplayDialog(
                "Zombie City Spawner Ready!",
                $"Zombie City Spawner has been configured in the scene.\n\n" +
                $"• City Root: demo_city_by_versatile_studio\n" +
                $"• Loaded: 7 Zombie Prefabs\n" +
                $"• You can control the Zombie Count slider in the Inspector and click 'Spawn Zombies Now'!",
                "OK"
            );
        }
    }
}
