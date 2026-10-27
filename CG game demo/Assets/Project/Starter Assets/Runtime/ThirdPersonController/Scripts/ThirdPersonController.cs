using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.25f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("Rotation speed of the camera when looking with mouse")]
        public float RotationSpeed = 1.0f;

        [Tooltip("Invert vertical (Y) camera pitch rotation")]
        public bool InvertPitch = false;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        [Header("Vehicle Interaction")]
        [Tooltip("Whether the player is currently riding a motorcycle or vehicle")]
        public bool isRidingVehicle = false;

        [Header("Weapon / Combat")]
        [Tooltip("Whether the player is currently holding the rifle. Only triggers rifle animations when true!")]
        public bool hasRifle = false;

        [Tooltip("Optional: Weapon GameObject attached to character's hand to activate when holding rifle")]
        public GameObject rifleInHand;

        [Header("Weapon Audio & Ammo")]
        [Tooltip("Audio clip for firing the weapon")]
        public AudioClip gunshotSound;

        [Tooltip("Audio clip for reloading the weapon")]
        public AudioClip reloadSound;

        [Tooltip("Audio clip when firing with no bullets remaining (empty chamber click)")]
        public AudioClip emptyGunSound;

        [Tooltip("Audio clip for picking up the weapon")]
        public AudioClip pickupSound;

        [Header("Audio Volumes")]
        [Tooltip("Volume multiplier for gunshot sound")]
        [Range(0f, 3f)]
        public float gunshotVolume = 1.0f;

        [Tooltip("Volume multiplier for reload sound (increase to make reload louder)")]
        [Range(0f, 3f)]
        public float reloadVolume = 2.0f;

        [Tooltip("Volume multiplier for dry fire click sound")]
        [Range(0f, 3f)]
        public float emptyGunVolume = 1.2f;

        [Header("Ammo Settings")]
        [Tooltip("Key to manually reload the weapon")]
        public KeyCode reloadKey = KeyCode.R;

        [Tooltip("Magazine bullet capacity")]
        public int maxAmmo = 30;

        [Tooltip("Current bullets in the active magazine")]
        public int currentAmmo = 30;

        [Tooltip("Total reserve ammo (-1 for unlimited)")]
        public int reserveAmmo = 90;

        [Tooltip("Duration in seconds to reload")]
        public float reloadDuration = 2.0f;

        [Tooltip("Is the weapon currently reloading")]
        public bool isReloading = false;

        [Tooltip("Delay in seconds before firing the first bullet from relaxed idle pose so character raises the rifle first")]
        [Range(0f, 0.25f)]
        public float idleToFireDelay = 0.08f;

        [Tooltip("Show Ammo counter on screen")]
        public bool showAmmoUI = true;

        [Tooltip("Optional bullet projectile prefab. If null, a tracer bullet is spawned automatically.")]
        public GameObject bulletPrefab;

        [Tooltip("Transform from where bullets originate. If null, automatically uses rifle tip or hand.")]
        public Transform bulletSpawnPoint;

        [Tooltip("Fire rate in seconds between shots (e.g. 0.15 = ~400 RPM)")]
        public float fireRate = 0.15f;

        [Tooltip("Speed of bullets fired in m/s")]
        public float bulletSpeed = 95f;

        [Tooltip("Bullet damage")]
        public float bulletDamage = 25f;

        [Tooltip("Hand local position offset for attached gun")]
        public Vector3 handOffsetPosition = new Vector3(0.06f, 0.10f, 0.04f);

        [Tooltip("Hand local rotation offset (Euler angles) for attached gun")]
        public Vector3 handOffsetRotation = new Vector3(-88f, 92f, 2f);

        [Tooltip("Radius around player to scan for ground guns tagged 'Gun'")]
        public float gunDetectRadius = 2.5f;

        [Tooltip("Key required to pick up the gun")]
        public KeyCode pickupKey = KeyCode.F;

        [Tooltip("Show aiming dot or plus sign crosshair on screen when holding gun")]
        public bool showAimingReticle = true;

        public enum AimReticleType { DotAndPlus, DotOnly, PlusOnly }
        [Tooltip("Style of the aiming crosshair")]
        public AimReticleType reticleType = AimReticleType.DotAndPlus;

        [Tooltip("Color of the aiming crosshair")]
        public Color reticleColor = new Color(1f, 1f, 1f, 0.95f);

        [Tooltip("Size of the aiming crosshair in pixels")]
        public float reticleSize = 9f;

        [Tooltip("Camera recoil kick force per shot")]
        public float recoilKick = 0.4f;

        [Tooltip("When enabled, changing handOffsetPosition or handOffsetRotation in Inspector live-updates the gun in player's hand")]
        public bool liveUpdateHandOffsets = true;

        private float _nextFireTime = 0f;
        private float _nextEmptyClickTime = 0f;
        private AudioSource _audioSource;
        private GameObject _detectedGun;
        private bool _hasIsFiringParam = false;
        private Vector3 _lastHandOffsetPos;
        private Vector3 _lastHandOffsetRot;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animIDHasRifle;
        private int _animIDShoot;
        private int _animIDIsFiring;
        private int _animIDMovingBackward;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (_playerInput == null) return true;
                if (string.IsNullOrEmpty(_playerInput.currentControlScheme)) return true;
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }


        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                if (_mainCamera == null && Camera.main != null)
                {
                    _mainCamera = Camera.main.gameObject;
                }
            }

            if (CinemachineCameraTarget == null)
            {
                Transform targetRoot = transform.Find("PlayerCameraRoot");
                if (targetRoot != null)
                {
                    CinemachineCameraTarget = targetRoot.gameObject;
                }
                else
                {
                    CinemachineCameraTarget = gameObject;
                }
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0.5f;
            }

#if UNITY_EDITOR
            AutoAssignAudio();
#endif
            if (gunshotSound == null) gunshotSound = Resources.Load<AudioClip>("Gunshot");
            if (pickupSound == null) pickupSound = Resources.Load<AudioClip>("GunPickup");
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoAssignAudio();
        }

        private void OnValidate()
        {
            AutoAssignAudio();
        }

        private void AutoAssignAudio()
        {
            if (gunshotSound == null)
            {
                gunshotSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sound/gunshot-bolt-action-rifle-fascinatedsound-3-3-00-03.mp3");
            }
            if (reloadSound == null)
            {
                reloadSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sound/gun-reload-bolt-action-rifle-fascinatedsound-1-1-00-01.mp3");
            }
            if (emptyGunSound == null)
            {
                emptyGunSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sound/gun-trigger-click-without-bullet-fascinatedsound-2-2-00-00.mp3");
            }
            if (pickupSound == null)
            {
                pickupSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Project/Starter Assets/Runtime/ThirdPersonController/Character/Sfx/GunPickup.wav");
            }
        }
#endif

        private void Start()
        {
            if (CinemachineCameraTarget != null)
            {
                _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            }
            
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            // While riding a vehicle, bike controller handles physics and movement
            if (isRidingVehicle) return;

            GroundedCheck();
            JumpAndGravity();
            Move();
            GroundedCheck(); // Refresh ground status immediately after CharacterController.Move has resolved collisions
            CheckNearbyGuns();
            HandleCombat();
        }

        /// <summary>
        /// Called when mounting a motorcycle or vehicle. Disables walking physics and stows weapon.
        /// </summary>
        public void MountVehicle()
        {
            isRidingVehicle = true;
            if (_controller != null) _controller.enabled = false;
            if (rifleInHand != null) rifleInHand.SetActive(false);
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDHasRifle, false);
                _animator.SetFloat(_animIDSpeed, 0f);
                _animator.SetFloat(_animIDMotionSpeed, 0f);
                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, false);
                _animator.SetBool(_animIDGrounded, true);
            }
        }

        /// <summary>
        /// Called when dismounting a motorcycle or vehicle. Re-enables walking physics and restores weapon if owned.
        /// </summary>
        public void DismountVehicle()
        {
            isRidingVehicle = false;
            if (_controller != null) _controller.enabled = true;
            if (hasRifle)
            {
                if (rifleInHand != null) rifleInHand.SetActive(true);
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDHasRifle, true);
                }
            }
        }

        private void LateUpdate()
        {
            CameraRotation();

            // Live sync gun orientation in hand while adjusting in Inspector
            if (hasRifle && rifleInHand != null && liveUpdateHandOffsets)
            {
                if (handOffsetPosition != _lastHandOffsetPos || handOffsetRotation != _lastHandOffsetRot)
                {
                    rifleInHand.transform.localPosition = handOffsetPosition;
                    rifleInHand.transform.localRotation = Quaternion.Euler(handOffsetRotation);
                    _lastHandOffsetPos = handOffsetPosition;
                    _lastHandOffsetRot = handOffsetRotation;
                }
            }
        }

        public void EquipRifle(bool equip)
        {
            hasRifle = equip;
            if (rifleInHand != null) rifleInHand.SetActive(hasRifle);
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDHasRifle, hasRifle);
                if (!hasRifle)
                {
                    _animator.SetBool(_animIDMovingBackward, false);
                    if (_hasIsFiringParam) _animator.SetBool(_animIDIsFiring, false);
                }
            }
        }

        public Transform FindRightHandBone()
        {
            if (_animator != null && _animator.isHuman)
            {
                Transform bone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (bone != null) return bone;
            }

            return FindChildRecursive(transform, "Right_Hand") 
                ?? FindChildRecursive(transform, "RightHand")
                ?? FindChildRecursive(transform, "Hand_R");
        }

        private Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        public void EquipRifleFromPickup(GameObject groundGun, AudioClip sound = null)
        {
            if (sound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(sound);
            }
            else if (pickupSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(pickupSound);
            }

            if (rifleInHand == null)
            {
                Transform hand = FindRightHandBone();
                if (hand != null && groundGun != null)
                {
                    // Clone the gun to attach into player's hand
                    GameObject inHand = Instantiate(groundGun, hand);
                    inHand.name = groundGun.name + "_InHand";

                    // Remove pickup scripts and colliders from in-hand weapon
                    RiflePickup rp = inHand.GetComponent<RiflePickup>();
                    if (rp != null) Destroy(rp);

                    Collider[] cols = inHand.GetComponentsInChildren<Collider>();
                    foreach (var c in cols) Destroy(c);

                    Rigidbody[] rbs = inHand.GetComponentsInChildren<Rigidbody>();
                    foreach (var r in rbs) Destroy(r);

                    inHand.transform.localPosition = handOffsetPosition;
                    inHand.transform.localRotation = Quaternion.Euler(handOffsetRotation);
                    inHand.transform.localScale = groundGun.transform.localScale;

                    rifleInHand = inHand;
                }
            }

            EquipRifle(true);

            if (groundGun != null)
            {
                groundGun.SetActive(false);
            }
        }

        private void CheckNearbyGuns()
        {
            if (hasRifle)
            {
                _detectedGun = null;
                return;
            }

            _detectedGun = null;
            Collider[] colliders = Physics.OverlapSphere(transform.position, gunDetectRadius);
            foreach (var col in colliders)
            {
                if (col.CompareTag("Gun") || col.GetComponent<RiflePickup>() != null)
                {
                    _detectedGun = col.gameObject;
                    break;
                }
            }

            if (_detectedGun != null)
            {
                bool interactPressed = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                {
                    interactPressed = true;
                }
#endif
                if (!interactPressed && Input.GetKeyDown(pickupKey))
                {
                    interactPressed = true;
                }

                if (interactPressed)
                {
                    EquipRifleFromPickup(_detectedGun, pickupSound);
                }
            }
        }

        private void HandleCombat()
        {
            // Press 1 to toggle rifle for testing or equipping
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                EquipRifle(!hasRifle);
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                EquipRifle(!hasRifle);
            }
#endif

            if (hasRifle)
            {
                // Moving backward check (S key or backward movement vector)
                bool isMovingBackward = _input.move.y < -0.1f;

                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDHasRifle, true);
                    _animator.SetBool(_animIDMovingBackward, isMovingBackward);
                    if (_hasIsFiringParam) _animator.SetBool(_animIDIsFiring, _input.shoot && !isReloading && currentAmmo > 0);
                }

                // Check reload input (R key)
                if (CheckReloadInput())
                {
                    TryReload();
                }

                // Fire weapon when shoot is held or clicked
                if (_input.shoot)
                {
                    if (isReloading)
                    {
                        // Cannot shoot while reloading
                    }
                    else if (currentAmmo > 0)
                    {
                        if (Time.time >= _nextFireTime)
                        {
                            _nextFireTime = Time.time + fireRate;

                            // Ensure character transitions into firing animation cleanly without resetting every single bullet
                            if (_hasAnimator)
                            {
                                var curState = _animator.GetCurrentAnimatorStateInfo(0);
                                var nextState = _animator.GetNextAnimatorStateInfo(0);
                                bool inFiringStance = curState.IsName("Firing Rifle") || nextState.IsName("Firing Rifle");

                                if (!inFiringStance)
                                {
                                    _animator.ResetTrigger(_animIDShoot);
                                    _animator.CrossFadeInFixedTime("Firing Rifle", 0.05f, 0);
                                }
                            }

                            currentAmmo--;
                            FireBullet();
                        }
                    }
                    else
                    {
                        // Dry fire click when gun has no bullets
                        if (Time.time >= _nextEmptyClickTime)
                        {
                            _nextEmptyClickTime = Time.time + 0.28f;
                            PlayEmptyClick();
                        }
                    }
                }
            }
            else
            {
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDHasRifle, false);
                    _animator.SetBool(_animIDMovingBackward, false);
                    if (_hasIsFiringParam) _animator.SetBool(_animIDIsFiring, false);
                }
            }
        }

        private bool CheckReloadInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(reloadKey);
        }

        private void PlayEmptyClick()
        {
            if (emptyGunSound != null && _audioSource != null)
            {
                _audioSource.pitch = Random.Range(0.98f, 1.02f);
                _audioSource.PlayOneShot(emptyGunSound, emptyGunVolume);
            }
        }

        private Coroutine _delayedFireCoroutine;

        private System.Collections.IEnumerator DelayedFireFirstBullet(float delay)
        {
            yield return new WaitForSeconds(delay);
            _delayedFireCoroutine = null;

            if (hasRifle && !isReloading && currentAmmo > 0 && !isRidingVehicle)
            {
                currentAmmo--;
                FireBullet();
            }
        }

        public void TryReload()
        {
            if (isReloading || !hasRifle) return;
            if (currentAmmo >= maxAmmo) return;
            if (reserveAmmo == 0) return;

            if (_delayedFireCoroutine != null)
            {
                StopCoroutine(_delayedFireCoroutine);
                _delayedFireCoroutine = null;
            }

            StartCoroutine(ReloadRoutine());
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            isReloading = true;

            // Brief 0.12s pause so gunfire audio tail doesn't mask the reload sound
            yield return new WaitForSeconds(0.12f);

            if (reloadSound != null && _audioSource != null)
            {
                _audioSource.pitch = 1.0f;
                // Boosted reload volume
                _audioSource.PlayOneShot(reloadSound, reloadVolume);
            }

            if (_hasAnimator)
            {
                for (int i = 0; i < _animator.parameterCount; i++)
                {
                    if (_animator.parameters[i].name == "Reload")
                    {
                        _animator.SetTrigger("Reload");
                        break;
                    }
                }
            }

            float waitTime = reloadDuration;
            if (reloadSound != null && reloadSound.length > 0.4f)
            {
                waitTime = Mathf.Max(reloadDuration, reloadSound.length);
            }

            yield return new WaitForSeconds(waitTime);

            int needed = maxAmmo - currentAmmo;
            if (reserveAmmo == -1)
            {
                currentAmmo = maxAmmo;
            }
            else
            {
                int take = Mathf.Min(needed, reserveAmmo);
                currentAmmo += take;
                reserveAmmo -= take;
            }

            isReloading = false;
        }

        private void FireBullet()
        {
            // Gunshot sound with slight pitch variation and volume multiplier
            if (gunshotSound != null && _audioSource != null)
            {
                _audioSource.pitch = Random.Range(0.95f, 1.05f);
                _audioSource.PlayOneShot(gunshotSound, gunshotVolume);
            }

            // Determine bullet spawn point
            Vector3 spawnPos;
            if (bulletSpawnPoint != null)
            {
                spawnPos = bulletSpawnPoint.position;
            }
            else if (rifleInHand != null)
            {
                // Check if user added a Muzzle or Barrel child transform
                Transform muzzle = rifleInHand.transform.Find("Muzzle") ?? rifleInHand.transform.Find("Barrel");
                if (muzzle != null)
                {
                    spawnPos = muzzle.position;
                }
                else
                {
                    // Compute accurate front barrel tip using rifle mesh bounds and forward direction
                    Renderer rend = rifleInHand.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        spawnPos = rend.bounds.center + rifleInHand.transform.forward * (rend.bounds.extents.magnitude * 0.7f);
                    }
                    else
                    {
                        spawnPos = rifleInHand.transform.position + rifleInHand.transform.forward * 0.65f;
                    }
                }
            }
            else
            {
                spawnPos = transform.position + transform.forward * 0.5f + Vector3.up * 1.3f;
            }

            // Determine target aim point from Camera
            Vector3 targetPoint = spawnPos + transform.forward * 50f;
            if (_mainCamera != null)
            {
                Camera cam = _mainCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    Ray aimRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

                    // Filter out player's own body colliders!
                    RaycastHit[] hits = Physics.RaycastAll(aimRay, 200f, ~0, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                    bool hitFound = false;
                    foreach (var h in hits)
                    {
                        if (h.transform.root == transform.root || h.transform == transform) continue;
                        if (h.collider != null && h.collider.isTrigger) continue;

                        targetPoint = h.point;
                        hitFound = true;
                        break;
                    }

                    if (!hitFound)
                    {
                        targetPoint = aimRay.origin + aimRay.direction * 150f;
                    }
                }
            }

            Vector3 direction = (targetPoint - spawnPos).normalized;

            // Spawn bullet projectile
            if (bulletPrefab != null)
            {
                GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
                BulletProjectile proj = bullet.GetComponent<BulletProjectile>();
                if (proj != null)
                {
                    proj.speed = bulletSpeed;
                    proj.damage = bulletDamage;
                }
            }
            else
            {
                // Create glowing tracer bullet automatically
                GameObject bullet = new GameObject("Bullet_Tracer");
                bullet.transform.position = spawnPos;
                bullet.transform.rotation = Quaternion.LookRotation(direction);

                BulletProjectile proj = bullet.AddComponent<BulletProjectile>();
                proj.speed = bulletSpeed;
                proj.damage = bulletDamage;
            }

            // Camera recoil kick
            if (recoilKick > 0f)
            {
                _cinemachineTargetPitch = Mathf.Clamp(_cinemachineTargetPitch - recoilKick, BottomClamp, TopClamp);
            }

            // Muzzle flash light effect
            CreateMuzzleFlash(spawnPos);
        }

        private void CreateMuzzleFlash(Vector3 position)
        {
            GameObject flashObj = new GameObject("MuzzleFlash_Light");
            flashObj.transform.position = position;
            Light flash = flashObj.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.85f, 0.4f);
            flash.range = 2.5f;
            flash.intensity = 4.0f;
            Destroy(flashObj, 0.05f);
        }

        private void OnGUI()
        {
            if (isRidingVehicle) return;

            if (hasRifle && showAimingReticle)
            {
                DrawAimingReticle();
            }

            if (hasRifle && showAmmoUI)
            {
                DrawAmmoHUD();
            }

            if (!hasRifle && _detectedGun != null)
            {
                // Only draw if _detectedGun does not already have an active RiflePickup script drawing GUI
                RiflePickup rp = _detectedGun.GetComponent<RiflePickup>();
                if (rp == null)
                {
                    DrawDefaultPickupPrompt();
                }
            }
        }

        private void DrawAmmoHUD()
        {
            float width = 165f;
            float height = 56f;
            float margin = 28f;
            float x = Screen.width - width - margin;
            float y = Screen.height - height - margin;

            // Semi-transparent background plate
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            // Subtle colored left accent stripe
            GUI.color = isReloading ? new Color(1f, 0.75f, 0.2f, 0.9f) : (currentAmmo == 0 ? new Color(1f, 0.25f, 0.25f, 0.9f) : new Color(0.2f, 0.8f, 1f, 0.9f));
            GUI.DrawTexture(new Rect(x, y, 4f, height), Texture2D.whiteTexture);

            GUI.color = Color.white;

            // Label
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = isReloading ? new Color(1f, 0.8f, 0.3f, 0.95f) : (currentAmmo == 0 ? new Color(1f, 0.4f, 0.4f, 0.95f) : new Color(0.7f, 0.82f, 0.95f, 0.9f));
            GUI.Label(new Rect(x + 16f, y + 6f, width - 20f, 16f), isReloading ? "RELOADING... [R]" : "RIFLE AMMO", labelStyle);

            // Large ammo numbers
            GUIStyle numStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            numStyle.normal.textColor = currentAmmo == 0 ? new Color(1f, 0.3f, 0.3f, 1f) : Color.white;
            GUI.Label(new Rect(x + 16f, y + 22f, 75f, 30f), $"{currentAmmo}", numStyle);

            // Reserve ammo label
            GUIStyle resStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            resStyle.normal.textColor = new Color(0.55f, 0.6f, 0.65f, 0.9f);
            string reserveStr = reserveAmmo == -1 ? "INF" : reserveAmmo.ToString();
            GUI.Label(new Rect(x + 65f, y + 27f, 90f, 22f), $"/ {reserveStr}", resStyle);
        }

        private void DrawAimingReticle()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            Color col = reticleColor;
            Color shadow = new Color(0f, 0f, 0f, 0.75f);

            // Draw center dot
            if (reticleType == AimReticleType.DotAndPlus || reticleType == AimReticleType.DotOnly)
            {
                float dotSize = 4f;
                GUI.color = shadow;
                GUI.DrawTexture(new Rect(cx - dotSize * 0.5f - 1f, cy - dotSize * 0.5f - 1f, dotSize + 2f, dotSize + 2f), Texture2D.whiteTexture);
                GUI.color = col;
                GUI.DrawTexture(new Rect(cx - dotSize * 0.5f, cy - dotSize * 0.5f, dotSize, dotSize), Texture2D.whiteTexture);
            }

            // Draw plus sign / crosshair lines
            if (reticleType == AimReticleType.DotAndPlus || reticleType == AimReticleType.PlusOnly)
            {
                float thickness = 2f;
                float len = reticleSize;
                float gap = 5f;

                DrawTick(cx - thickness * 0.5f, cy - gap - len, thickness, len, col, shadow); // Top
                DrawTick(cx - thickness * 0.5f, cy + gap, thickness, len, col, shadow);       // Bottom
                DrawTick(cx - gap - len, cy - thickness * 0.5f, len, thickness, col, shadow); // Left
                DrawTick(cx + gap, cy - thickness * 0.5f, len, thickness, col, shadow);       // Right
            }

            GUI.color = Color.white;
        }

        private void DrawTick(float x, float y, float w, float h, Color fg, Color bg)
        {
            GUI.color = bg;
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, w + 2f, h + 2f), Texture2D.whiteTexture);
            GUI.color = fg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        }

        private void DrawDefaultPickupPrompt()
        {
            float width = 360f;
            float height = 54f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 130f;

            Rect bgRect = new Rect(x, y, width, height);

            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.88f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);

            GUI.color = new Color(1f, 0.78f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(x, y + height - 3f, width, 3f), Texture2D.whiteTexture);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;

            GUI.color = Color.white;
            GUI.Label(bgRect, "Press [F] to Pick Up Gun", labelStyle);
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animIDHasRifle = Animator.StringToHash("HasRifle");
            _animIDShoot = Animator.StringToHash("Shoot");
            _animIDIsFiring = Animator.StringToHash("IsFiring");
            _animIDMovingBackward = Animator.StringToHash("IsMovingBackward");

            if (_hasAnimator && _animator != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.name == "IsFiring") _hasIsFiringParam = true;
                }
            }
        }

        private void GroundedCheck()
        {
            // If character is jumping upwards, we are intentionally airborne
            if (_verticalVelocity > 0.1f)
            {
                Grounded = false;
            }
            else
            {
                // 1. Built-in CharacterController ground contact (updated by PhysX during Move)
                bool ccGrounded = _controller != null && _controller.isGrounded;

                // Determine appropriate layer mask: include all walkable surfaces, excluding player/gun/UI/IgnoreRaycast
                int layerMask = GroundLayers.value;
                if (layerMask == 0 || layerMask == 1)
                {
                    layerMask = ~LayerMask.GetMask("Ignore Raycast", "UI", "Gun");
                }

                // 2. Downward Raycast: 100% reliable on non-convex mesh colliders (city streets, sidewalks, terrain)
                Vector3 rayOrigin = transform.position + Vector3.up * 0.25f;
                float rayDistance = 0.55f; // Reaches 30cm below feet, comfortably covering stepOffset (0.25m)
                bool rayGrounded = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, layerMask, QueryTriggerInteraction.Ignore);

                // 3. Downward SphereCast fallback (sweeps down to avoid missing thin edges or curbs)
                bool sphereCastGrounded = false;
                if (!rayGrounded && !ccGrounded)
                {
                    sphereCastGrounded = Physics.SphereCast(rayOrigin, GroundedRadius, Vector3.down, out RaycastHit sphereHit, 0.35f, layerMask, QueryTriggerInteraction.Ignore);
                }

                // 4. Overlap sphere fallback
                Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
                bool sphereGrounded = Physics.CheckSphere(spherePosition, GroundedRadius, layerMask, QueryTriggerInteraction.Ignore);

                Grounded = ccGrounded || rayGrounded || sphereCastGrounded || sphereGrounded;
            }

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                float pitchMultiplier = InvertPitch ? 1.0f : -1.0f;
                _cinemachineTargetYaw += _input.look.x * RotationSpeed * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier * pitchMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            if (CinemachineCameraTarget != null)
            {
                CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                    _cinemachineTargetYaw, 0.0f);
            }
        }

        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (hasRifle)
            {
                // When holding rifle: keep facing camera aim direction (combat strafe)
                float cameraYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, cameraYaw, ref _rotationVelocity, RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

                // Move relative to character's facing direction
                Vector3 moveDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
                if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

                _controller.Move(moveDirection * (_speed * Time.deltaTime) +
                                 new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
            }
            else
            {
                // Normal movement: rotate to face input direction relative to camera position
                if (_input.move != Vector2.zero)
                {
                    float targetYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
                    _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + targetYaw;
                    float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                        RotationSmoothTime);

                    transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
                }

                Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

                // move the player
                _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                                 new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
            }

            // update animator if using character
            if (_hasAnimator)
            {
                float animSpeed = _animationBlend;
                // If moving backward with rifle, don't trigger the forward Walk/Run Blend tree
                if (hasRifle && _input.move.y < -0.1f)
                {
                    animSpeed = 0f;
                }

                _animator.SetFloat(_animIDSpeed, animSpeed);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character: clear jumping & falling
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    Grounded = false;

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                        _animator.SetBool(_animIDGrounded, false);
                    }

                    _input.jump = false;
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character: only enter FreeFall if actually descending downward
                    if (_hasAnimator && _verticalVelocity < -2.5f)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}