using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame
{
    public class ZombieCitySpawner : MonoBehaviour
    {
        [Header("Zombie Prefabs")]
        [Tooltip("The zombie prefabs to spawn randomly. If left empty, all prefabs from Project/Zombies Prefab will be loaded.")]
        [SerializeField] private GameObject[] _zombiePrefabs;

        [Header("Spawn Controls")]
        [Tooltip("Total number of zombies to spawn and maintain in the city")]
        [Range(1, 100)]
        [SerializeField] private int _totalZombieCount = 20;

        [Tooltip("Automatically spawn zombies upon game start")]
        [SerializeField] private bool _spawnOnStart = true;

        [Tooltip("When a zombie dies, automatically spawn a new one elsewhere in the city after a delay to keep the city populated")]
        [SerializeField] private bool _maintainPopulation = true;

        [Tooltip("Delay in seconds before respawning a replacement zombie")]
        [SerializeField] private float _respawnDelay = 5f;

        [Header("City Placement & Area")]
        [Tooltip("Reference to the city GameObject (e.g. demo_city_by_versatile_studio). Auto-found if null.")]
        [SerializeField] private GameObject _cityRoot;

        [Tooltip("Whether to use custom manual bounds instead of calculating from city geometry")]
        [SerializeField] private bool _useCustomBounds = false;

        [Tooltip("Custom center for zombie spawning")]
        [SerializeField] private Vector3 _customCenter = new Vector3(40f, 1f, 160f);

        [Tooltip("Custom bounding size for zombie spawning")]
        [SerializeField] private Vector3 _customSize = new Vector3(250f, 30f, 250f);

        [Header("Player Distance Constraints")]
        [Tooltip("Minimum distance from player so zombies never spawn directly in front of the player")]
        [SerializeField] private float _minDistanceFromPlayer = 20f;

        [Tooltip("Optional maximum distance from player (0 = spawn anywhere within entire city)")]
        [SerializeField] private float _maxDistanceFromPlayer = 0f;

        [Header("Ground & NavMesh Detection")]
        [Tooltip("Layer mask for detecting road / pavement / ground")]
        [SerializeField] private LayerMask _groundLayer = ~0;

        [Tooltip("Max radius to snap to a walkable NavMesh surface")]
        [SerializeField] private float _navMeshSampleRadius = 6f;

        [Tooltip("Max random placement attempts per zombie")]
        [SerializeField] private int _maxAttemptsPerZombie = 35;

        // Runtime Tracking
        private readonly List<GameObject> _activeZombies = new List<GameObject>();
        private Transform _zombieContainer;
        private Transform _playerTransform;
        private Bounds _calculatedCityBounds;
        private bool _boundsCalculated = false;

        public int TotalZombieCount
        {
            get => _totalZombieCount;
            set => _totalZombieCount = Mathf.Clamp(value, 1, 100);
        }

        public int ActiveZombieCount
        {
            get
            {
                _activeZombies.RemoveAll(item => item == null);
                return _activeZombies.Count;
            }
        }

        public Bounds CityBounds => _useCustomBounds ? new Bounds(_customCenter, _customSize) : _calculatedCityBounds;

        private void Awake()
        {
            EnsureContainer();
            LocateCityRoot();
            LocatePlayer();
            CalculateCityBounds();
        }

        private void Start()
        {
            if (_spawnOnStart)
            {
                SpawnAllZombies();
            }
        }

        private void EnsureContainer()
        {
            if (_zombieContainer == null)
            {
                GameObject container = GameObject.Find("[Spawned_Zombies]");
                if (container == null)
                {
                    container = new GameObject("[Spawned_Zombies]");
                }
                _zombieContainer = container.transform;
            }
        }

        public void LocateCityRoot()
        {
            if (_cityRoot == null)
            {
                _cityRoot = GameObject.Find("demo_city_by_versatile_studio");
                if (_cityRoot == null)
                {
                    GameObject[] allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var root in allRoots)
                    {
                        if (root.name.ToLower().Contains("city") || root.name.ToLower().Contains("versatile"))
                        {
                            _cityRoot = root;
                            break;
                        }
                    }
                }
            }
        }

        private void LocatePlayer()
        {
            if (_playerTransform != null) return;

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                return;
            }

            var tpc = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
            if (tpc != null)
            {
                _playerTransform = tpc.transform;
            }
        }

        public void CalculateCityBounds()
        {
            if (_useCustomBounds)
            {
                _calculatedCityBounds = new Bounds(_customCenter, _customSize);
                _boundsCalculated = true;
                return;
            }

            LocateCityRoot();

            if (_cityRoot != null)
            {
                Renderer[] renderers = _cityRoot.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        b.Encapsulate(renderers[i].bounds);
                    }
                    _calculatedCityBounds = b;
                    _boundsCalculated = true;
                    return;
                }

                Collider[] colliders = _cityRoot.GetComponentsInChildren<Collider>();
                if (colliders.Length > 0)
                {
                    Bounds b = colliders[0].bounds;
                    for (int i = 1; i < colliders.Length; i++)
                    {
                        b.Encapsulate(colliders[i].bounds);
                    }
                    _calculatedCityBounds = b;
                    _boundsCalculated = true;
                    return;
                }
            }

            // Fallback default city bounds around the known city scene location
            _calculatedCityBounds = new Bounds(new Vector3(40f, 1f, 160f), new Vector3(250f, 30f, 250f));
            _boundsCalculated = true;
        }

        /// <summary>
        /// Clears existing spawned zombies and spawns the configured total amount.
        /// </summary>
        [ContextMenu("Spawn All Zombies")]
        public void SpawnAllZombies()
        {
            EnsureContainer();
            LocatePlayer();
            CalculateCityBounds();

            int toSpawn = _totalZombieCount - ActiveZombieCount;
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnSingleZombie();
            }

            Debug.Log($"[ZombieCitySpawner] Spawned {ActiveZombieCount} / {_totalZombieCount} zombies across city.");
        }

        /// <summary>
        /// Spawns a single zombie at a random valid ground/NavMesh position in the city.
        /// </summary>
        public GameObject SpawnSingleZombie()
        {
            EnsureContainer();
            LocatePlayer();

            if (!_boundsCalculated)
            {
                CalculateCityBounds();
            }

            GameObject prefab = GetRandomZombiePrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[ZombieCitySpawner] No zombie prefabs assigned or found!");
                return null;
            }

            Vector3 spawnPos;
            if (!TryGetRandomSpawnPosition(out spawnPos))
            {
                // Fallback: slight random offset around city center
                spawnPos = CityBounds.center + new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit fallbackHit, 15f, NavMesh.AllAreas))
                {
                    spawnPos = fallbackHit.position;
                }
            }

            Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject zombie = Instantiate(prefab, spawnPos, spawnRot, _zombieContainer);
            zombie.name = $"{prefab.name}_{System.Guid.NewGuid().ToString().Substring(0, 4)}";

            _activeZombies.Add(zombie);

            // Hook into death event for dynamic respawning
            ZombieHealth health = zombie.GetComponent<ZombieHealth>();
            if (health != null)
            {
                health.OnDeath += () => OnZombieDied(zombie);
            }

            return zombie;
        }

        private void OnZombieDied(GameObject deadZombie)
        {
            if (_activeZombies.Contains(deadZombie))
            {
                _activeZombies.Remove(deadZombie);
            }

            if (_maintainPopulation && Application.isPlaying && gameObject.activeInHierarchy)
            {
                StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_respawnDelay);

            if (ActiveZombieCount < _totalZombieCount)
            {
                SpawnSingleZombie();
            }
        }

        private bool TryGetRandomSpawnPosition(out Vector3 spawnPosition)
        {
            Bounds bounds = CityBounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            for (int i = 0; i < _maxAttemptsPerZombie; i++)
            {
                float randomX, randomZ;

                if (_maxDistanceFromPlayer > 0f && _playerTransform != null)
                {
                    // Random point within player's radius
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float dist = Random.Range(_minDistanceFromPlayer, _maxDistanceFromPlayer);
                    randomX = _playerTransform.position.x + Mathf.Cos(angle) * dist;
                    randomZ = _playerTransform.position.z + Mathf.Sin(angle) * dist;
                }
                else
                {
                    // Random point within city bounds
                    randomX = Random.Range(center.x - extents.x * 0.9f, center.x + extents.x * 0.9f);
                    randomZ = Random.Range(center.z - extents.z * 0.9f, center.z + extents.z * 0.9f);
                }

                // Check player minimum distance
                if (_playerTransform != null)
                {
                    float distToPlayerSq = (new Vector2(randomX, randomZ) - new Vector2(_playerTransform.position.x, _playerTransform.position.z)).sqrMagnitude;
                    if (distToPlayerSq < _minDistanceFromPlayer * _minDistanceFromPlayer)
                    {
                        continue;
                    }
                }

                // Raycast downwards from above city to find street/ground
                float highY = bounds.max.y + 15f;
                Vector3 rayStart = new Vector3(randomX, highY, randomZ);

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, highY + 50f, _groundLayer, QueryTriggerInteraction.Ignore))
                {
                    // Avoid spawning on extreme slopes or rooftops (pitch > 35 degrees)
                    if (Vector3.Angle(hit.normal, Vector3.up) > 35f)
                    {
                        continue;
                    }

                    // Check NavMesh snap
                    if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, _navMeshSampleRadius, NavMesh.AllAreas))
                    {
                        spawnPosition = navHit.position;
                        return true;
                    }
                }
            }

            spawnPosition = Vector3.zero;
            return false;
        }

        private GameObject GetRandomZombiePrefab()
        {
            if (_zombiePrefabs != null && _zombiePrefabs.Length > 0)
            {
                List<GameObject> validPrefabs = new List<GameObject>();
                foreach (var p in _zombiePrefabs)
                {
                    if (p != null) validPrefabs.Add(p);
                }

                if (validPrefabs.Count > 0)
                {
                    return validPrefabs[Random.Range(0, validPrefabs.Count)];
                }
            }

#if UNITY_EDITOR
            // Automatically find prefabs in Assets/Project/Zombies Prefab if not assigned
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Zombies Prefab" });
            List<GameObject> found = new List<GameObject>();
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<ZombieAI>() != null)
                {
                    found.Add(prefab);
                }
            }

            if (found.Count > 0)
            {
                _zombiePrefabs = found.ToArray();
                return _zombiePrefabs[Random.Range(0, _zombiePrefabs.Length)];
            }
#endif
            return null;
        }

        /// <summary>
        /// Clears all currently spawned zombies.
        /// </summary>
        [ContextMenu("Clear All Zombies")]
        public void ClearAllZombies()
        {
            EnsureContainer();

            if (_zombieContainer != null)
            {
                int childCount = _zombieContainer.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    Transform child = _zombieContainer.GetChild(i);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            _activeZombies.Clear();
            Debug.Log("[ZombieCitySpawner] Cleared all spawned zombies.");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.35f);
            Bounds b = CityBounds;
            Gizmos.DrawWireCube(b.center, b.size);

            if (_playerTransform != null)
            {
                // Draw minimum player avoidance radius
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.45f);
                Gizmos.DrawWireSphere(_playerTransform.position, _minDistanceFromPlayer);

                if (_maxDistanceFromPlayer > 0f)
                {
                    Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
                    Gizmos.DrawWireSphere(_playerTransform.position, _maxDistanceFromPlayer);
                }
            }
        }
    }
}
