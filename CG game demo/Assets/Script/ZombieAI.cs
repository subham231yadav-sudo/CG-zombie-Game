using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame
{
    [RequireComponent(typeof(Animator))]
    public class ZombieAI : MonoBehaviour
    {
        public enum AIState
        {
            Idle,
            Wandering,
            Screaming,
            Chasing,
            Attacking,
            Dead
        }

        [Header("State")]
        [SerializeField] private AIState _currentState = AIState.Idle;
        public AIState CurrentState => _currentState;

        [Header("Targeting & Senses")]
        [Tooltip("Target transform (defaults to Player if not set)")]
        [SerializeField] private Transform _target;
        [Tooltip("Detection distance for spotting the player")]
        [SerializeField] private float _detectionRadius = 15f;
        [Tooltip("Field of view in degrees (zombie only spots player in front unless heard/shot)")]
        [SerializeField] private float _fieldOfView = 120f;
        [Tooltip("Distance at which the zombie gives up chasing the player")]
        [SerializeField] private float _loseTargetRadius = 25f;

        [Header("Attack Settings")]
        [Tooltip("Distance within which the zombie attacks")]
        [SerializeField] private float _attackRange = 1.8f;
        [Tooltip("Damage dealt to player per attack")]
        [SerializeField] private float _attackDamage = 20f;
        [Tooltip("Total duration of attack cycle in seconds (should match or exceed the animation swing)")]
        [SerializeField] private float _attackRate = 2.2f;
        [Tooltip("Delay in seconds until the fist/claw swing lands (adjust to match your animation's hit moment)")]
        [SerializeField] private float _damageHitDelay = 1.2f;

        [Header("Alert & Scream")]
        [Tooltip("Duration of the scream/roar animation before chasing begins")]
        [SerializeField] private float _screamDuration = 1.8f;
        [Tooltip("Play scream when first spotting the player")]
        [SerializeField] private bool _screamOnSpotting = true;
        private bool _hasScreamed = false;

        [Header("Movement Speeds")]
        [SerializeField] private float _walkSpeed = 1.6f;
        [SerializeField] private float _runSpeed = 4.2f;
        [SerializeField] private float _turnSpeed = 8f;

        [Header("Wander Settings")]
        [SerializeField] private bool _enableWander = true;
        [SerializeField] private float _wanderRadius = 8f;
        [SerializeField] private float _wanderWaitTime = 4f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Primary scream sound (or single fallback)")]
        [SerializeField] private AudioClip _screamSound;
        [Tooltip("Random scream sounds played on alert")]
        [SerializeField] private AudioClip[] _screamSounds;
        [Tooltip("Primary attack sound (or single fallback)")]
        [SerializeField] private AudioClip _attackSound;
        [Tooltip("Random attack sounds played on claw/swing")]
        [SerializeField] private AudioClip[] _attackSounds;
        [Tooltip("Primary ambient groan sound (or single fallback)")]
        [SerializeField] private AudioClip _groanSound;
        [Tooltip("Random ambient groan sounds played periodically")]
        [SerializeField] private AudioClip[] _groanSounds;
        [Range(0f, 1f)] [SerializeField] private float _audioVolume = 1f;
        [Tooltip("Maximum distance in meters where zombie sounds can be heard")]
        [SerializeField] private float _soundHearingRange = 20f;

        // Components
        private Animator _animator;
        private NavMeshAgent _navAgent;
        private ZombieHealth _health;

        // Internal State Tracking
        private float _lastAttackTime;
        private float _wanderTimer;
        private float _nextGroanTime;
        private Vector3 _spawnPosition;
        private Coroutine _screamRoutine;
        private Coroutine _attackRoutine;

        // Animator Parameter Hashes
        private static readonly int HashIsRunning = Animator.StringToHash("IsRunning");
        private static readonly int HashIsAttacking = Animator.StringToHash("IsAttacking");
        private static readonly int HashScream = Animator.StringToHash("Scream");
        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashIsDead = Animator.StringToHash("IsDead");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _navAgent = GetComponent<NavMeshAgent>();
            _health = GetComponent<ZombieHealth>();
            _spawnPosition = transform.position;

            EnsureAudioSource();

            // Configure NavMeshAgent if present
            if (_navAgent != null)
            {
                _navAgent.speed = _runSpeed;
                _navAgent.stoppingDistance = _attackRange * 0.85f;
            }
        }

        private void Start()
        {
            LocatePlayer();
            SetState(AIState.Idle);
            EnsureAudioListener();
            // Stagger initial ambient groans across a wide random window so all zombies never sound at once
            _nextGroanTime = Time.time + Random.Range(6f, 32f);
        }

        private void Update()
        {
            if (_currentState == AIState.Dead) return;

            // Ensure player reference is valid
            if (_target == null)
            {
                LocatePlayer();
            }

            float distanceToTarget = _target != null ? Vector3.Distance(transform.position, _target.position) : float.MaxValue;

            // Periodic ambient groans while wandering or idle (only if within hearing range so map isn't flooded)
            if (Time.time >= _nextGroanTime && _currentState != AIState.Screaming && _currentState != AIState.Attacking)
            {
                _nextGroanTime = Time.time + Random.Range(12f, 28f);

                if (distanceToTarget <= _soundHearingRange)
                {
                    AudioClip groanToPlay = GetRandomClip(_groanSounds, _groanSound);
                    if (groanToPlay != null)
                    {
                        PlayAudio(groanToPlay, 0.8f);
                    }
                }
            }

            switch (_currentState)
            {
                case AIState.Idle:
                case AIState.Wandering:
                    CheckForPlayerSpotting(distanceToTarget);
                    if (_currentState == AIState.Wandering || _currentState == AIState.Idle)
                    {
                        HandleWandering();
                    }
                    break;

                case AIState.Screaming:
                    FaceTarget(_target.position, _turnSpeed * 1.5f);
                    break;

                case AIState.Chasing:
                    HandleChasing(distanceToTarget);
                    break;

                case AIState.Attacking:
                    HandleAttacking(distanceToTarget);
                    break;
            }
        }

        #region Targeting & Senses

        private void LocatePlayer()
        {
            // 1. Try Player Tag
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _target = playerObj.transform;
                return;
            }

            // 2. Try StarterAssets ThirdPersonController
            StarterAssets.ThirdPersonController tpc = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
            if (tpc != null)
            {
                _target = tpc.transform;
                return;
            }

            // 3. Try PlayerHealth component
            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                _target = playerHealth.transform;
            }
        }

        private void CheckForPlayerSpotting(float distance)
        {
            if (distance > _detectionRadius) return;

            Vector3 directionToPlayer = (_target.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToPlayer);

            // Within sight cone or close enough to detect directly
            if (angle <= _fieldOfView * 0.5f || distance <= 3.5f)
            {
                // Raycast check to ensure no wall obstruction
                Vector3 rayOrigin = transform.position + Vector3.up * 1.5f;
                Vector3 targetHead = _target.position + Vector3.up * 1.5f;

                if (Physics.Raycast(rayOrigin, (targetHead - rayOrigin).normalized, out RaycastHit hit, _detectionRadius))
                {
                    if (hit.transform == _target || hit.transform.IsChildOf(_target) || hit.transform.CompareTag("Player"))
                    {
                        AlertTarget();
                    }
                }
                else
                {
                    AlertTarget();
                }
            }
        }

        /// <summary>
        /// Called when the zombie hears or sees the player, or is shot by a bullet.
        /// </summary>
        public void AlertTarget()
        {
            if (_currentState == AIState.Dead || _currentState == AIState.Chasing || _currentState == AIState.Attacking)
                return;

            if (_target == null) LocatePlayer();

            if (_screamOnSpotting && !_hasScreamed)
            {
                _hasScreamed = true;
                _screamRoutine = StartCoroutine(ScreamRoutine());
            }
            else
            {
                SetState(AIState.Chasing);
            }
        }

        private IEnumerator ScreamRoutine()
        {
            SetState(AIState.Screaming);

            // Stop movement
            StopAgent();

            // Trigger animation
            _animator.SetTrigger(HashScream);

            // Play scream sound (randomized)
            AudioClip screamClip = GetRandomClip(_screamSounds, _screamSound);
            PlayAudio(screamClip);

            yield return new WaitForSeconds(_screamDuration);

            if (_currentState != AIState.Dead)
            {
                SetState(AIState.Chasing);
            }
        }

        #endregion

        #region Chasing & Attacking

        private void HandleChasing(float distance)
        {
            // Check if player escaped
            if (distance > _loseTargetRadius)
            {
                _hasScreamed = false;
                SetState(AIState.Idle);
                return;
            }

            // Check if within attack range
            if (distance <= _attackRange)
            {
                SetState(AIState.Attacking);
                return;
            }

            // Move towards player
            MoveTowards(_target.position, _runSpeed);
            FaceTarget(_target.position, _turnSpeed);
        }

        private void HandleAttacking(float distance)
        {
            FaceTarget(_target.position, _turnSpeed * 1.8f);

            // If player backed away out of attack range, return to chasing
            if (distance > _attackRange * 1.25f)
            {
                SetState(AIState.Chasing);
                return;
            }

            // Attack on cooldown
            if (Time.time >= _lastAttackTime + _attackRate)
            {
                _lastAttackTime = Time.time;
                if (_attackRoutine != null) StopCoroutine(_attackRoutine);
                _attackRoutine = StartCoroutine(PerformAttackHit());
            }
        }

        private IEnumerator PerformAttackHit()
        {
            AudioClip attackClip = GetRandomClip(_attackSounds, _attackSound);
            PlayAudio(attackClip);

            yield return new WaitForSeconds(_damageHitDelay);

            DealDamageIfInRange();
        }

        /// <summary>
        /// Can also be called directly via an Animation Event on the Zombie Attack clip at the exact frame of impact!
        /// </summary>
        public void OnAttackHit()
        {
            DealDamageIfInRange();
        }

        private void DealDamageIfInRange()
        {
            if (_currentState == AIState.Attacking && _target != null)
            {
                float currentDistance = Vector3.Distance(transform.position, _target.position);
                // Fair dodge check: player must still be in range when the arm actually lands
                if (currentDistance <= _attackRange * 1.15f)
                {
                    // Deal damage to player
                    PlayerHealth playerHealth = _target.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(_attackDamage);
                    }
                    else
                    {
                        _target.SendMessage("TakeDamage", _attackDamage, SendMessageOptions.DontRequireReceiver);
                        _target.SendMessage("ApplyDamage", _attackDamage, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }

        #endregion

        #region Movement & Navigation

        private void MoveTowards(Vector3 destination, float speed)
        {
            if (_navAgent != null && _navAgent.isOnNavMesh && _navAgent.enabled)
            {
                _navAgent.isStopped = false;
                _navAgent.speed = speed;
                _navAgent.SetDestination(destination);
            }
            else
            {
                // Fallback for scenes without baked NavMesh
                Vector3 moveDir = (destination - transform.position);
                moveDir.y = 0;
                if (moveDir.magnitude > 0.05f)
                {
                    transform.position += moveDir.normalized * (speed * Time.deltaTime);
                }
            }
        }

        private void StopAgent()
        {
            if (_navAgent != null && _navAgent.isOnNavMesh && _navAgent.enabled)
            {
                _navAgent.isStopped = true;
                _navAgent.velocity = Vector3.zero;
            }
        }

        private void FaceTarget(Vector3 worldPoint, float speed)
        {
            Vector3 direction = worldPoint - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, speed * Time.deltaTime);
            }
        }

        private void HandleWandering()
        {
            if (!_enableWander)
            {
                if (_currentState == AIState.Wandering)
                {
                    SetState(AIState.Idle);
                }
                return;
            }

            if (_currentState == AIState.Idle)
            {
                // Ensure completely stopped while playing Idle animation (no gliding/walking in idle!)
                StopAgent();
                _wanderTimer += Time.deltaTime;

                if (_wanderTimer >= _wanderWaitTime)
                {
                    _wanderTimer = 0f;
                    Vector3 randomOffset = Random.insideUnitSphere * _wanderRadius;
                    randomOffset.y = 0;
                    Vector3 samplePos = _spawnPosition + randomOffset;

                    if (NavMesh.SamplePosition(samplePos, out NavMeshHit hit, _wanderRadius, NavMesh.AllAreas))
                    {
                        SetState(AIState.Wandering);
                        MoveTowards(hit.position, _walkSpeed);
                    }
                    else
                    {
                        SetState(AIState.Wandering);
                        MoveTowards(samplePos, _walkSpeed);
                    }
                }
            }
            else if (_currentState == AIState.Wandering)
            {
                // In wandering state, verify if agent has reached destination
                if (_navAgent != null && _navAgent.isOnNavMesh && _navAgent.enabled)
                {
                    if (!_navAgent.pathPending && _navAgent.remainingDistance <= Mathf.Max(_navAgent.stoppingDistance, 0.4f))
                    {
                        // Destination reached: cleanly transition back to Idle
                        SetState(AIState.Idle);
                        _wanderTimer = 0f;
                    }
                }
                else
                {
                    SetState(AIState.Idle);
                    _wanderTimer = 0f;
                }
            }
        }

        #endregion

        #region State Transitions & Animation

        private void SetState(AIState newState)
        {
            _currentState = newState;

            switch (newState)
            {
                case AIState.Idle:
                    StopAgent();
                    _animator.SetBool(HashIsRunning, false);
                    _animator.SetBool(HashIsAttacking, false);
                    _animator.SetFloat(HashSpeed, 0f);
                    break;

                case AIState.Wandering:
                    if (_navAgent != null && _navAgent.isOnNavMesh && _navAgent.enabled)
                    {
                        _navAgent.isStopped = false;
                        _navAgent.speed = _walkSpeed;
                    }
                    _animator.SetBool(HashIsRunning, true);
                    _animator.SetBool(HashIsAttacking, false);
                    _animator.SetFloat(HashSpeed, _walkSpeed);
                    break;

                case AIState.Screaming:
                    StopAgent();
                    _animator.SetBool(HashIsRunning, false);
                    _animator.SetBool(HashIsAttacking, false);
                    _animator.SetFloat(HashSpeed, 0f);
                    break;

                case AIState.Chasing:
                    _animator.SetBool(HashIsRunning, true);
                    _animator.SetBool(HashIsAttacking, false);
                    _animator.SetFloat(HashSpeed, _runSpeed);
                    break;

                case AIState.Attacking:
                    StopAgent();
                    _animator.SetBool(HashIsRunning, false);
                    _animator.SetBool(HashIsAttacking, true);
                    _animator.SetFloat(HashSpeed, 0f);
                    break;

                case AIState.Dead:
                    StopAgent();
                    if (_screamRoutine != null) StopCoroutine(_screamRoutine);
                    if (_attackRoutine != null) StopCoroutine(_attackRoutine);
                    _animator.SetBool(HashIsRunning, false);
                    _animator.SetBool(HashIsAttacking, false);
                    _animator.SetBool(HashIsDead, true);
                    break;
            }
        }

        public void OnDeath()
        {
            SetState(AIState.Dead);
            this.enabled = false;
        }

        public AudioSource EnsureAudioSource()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Always configure 100% 3D spatial sound with crisp falloff (no 2D audio bleed across the map!)
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1.0f; // 100% 3D
            _audioSource.minDistance = 2.0f;
            _audioSource.maxDistance = _soundHearingRange;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.volume = 1f;

            return _audioSource;
        }

        private void EnsureAudioListener()
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                Camera cam = Camera.main;
                if (cam != null && cam.GetComponent<AudioListener>() == null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log("[ZombieAI] Added missing AudioListener to Main Camera.");
                }
            }
        }

        private void PlayAudio(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            // Range culling: Do not play audio if player is outside hearing distance
            if (_target != null)
            {
                float dist = Vector3.Distance(transform.position, _target.position);
                if (dist > _soundHearingRange) return;
            }

            AudioSource src = EnsureAudioSource();
            if (src != null)
            {
                src.pitch = Random.Range(0.85f, 1.15f);
                src.PlayOneShot(clip, volume * _audioVolume);
            }
        }

        private AudioClip GetRandomClip(AudioClip[] array, AudioClip fallback)
        {
            if (array != null && array.Length > 0)
            {
                System.Collections.Generic.List<AudioClip> valid = new System.Collections.Generic.List<AudioClip>();
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i] != null) valid.Add(array[i]);
                }
                if (valid.Count > 0)
                {
                    return valid[Random.Range(0, valid.Count)];
                }
            }
            return fallback;
        }

        #endregion

        #region Debug Gizmos

        private void OnDrawGizmosSelected()
        {
            // Yellow: Detection Range
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            // Red: Attack Range
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _attackRange);

            // Blue: Lose Target Range
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _loseTargetRadius);
        }

        #endregion
    }
}
