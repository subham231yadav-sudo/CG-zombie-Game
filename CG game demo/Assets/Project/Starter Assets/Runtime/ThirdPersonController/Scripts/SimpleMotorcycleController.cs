using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [AddComponentMenu("Simple Motorcycle Controller")]
    public class SimpleMotorcycleController : MonoBehaviour
    {
        [Header("Physics Mode")]
        [Tooltip("If true and BicycleVehicle component exists, uses the pack's WheelCollider physics. Otherwise uses arcade controller.")]
        public bool useWheelPhysicsIfPresent = true;

        [Header("Mount & Interaction")]
        [Tooltip("Key required to mount/dismount the motorcycle")]
        public KeyCode rideKey = KeyCode.E;

        [Tooltip("Interaction distance from bike to mount")]
        public float interactDistance = 3.0f;

        [Tooltip("Transform where the rider sits. If unassigned, automatically created or calculated.")]
        public Transform driverSeat;

        [Tooltip("Local offset for driver seat. Tweak Y to lower/raise rider onto seat cushion.")]
        public Vector3 seatOffsetPosition = new Vector3(0f, 0.05f, -0.15f);

        [Tooltip("Local rotation for driver seat")]
        public Vector3 seatOffsetRotation = Vector3.zero;

        [Tooltip("Transform where player appears upon dismounting. If null, placed to the left.")]
        public Transform exitPoint;

        [Tooltip("Live sync driver seat position while adjusting in Inspector during Play Mode")]
        public bool liveUpdateSeatOffsets = true;

        [Header("Arcade Performance (when WheelPhysics is not used)")]
        [Tooltip("Top forward speed in m/s (24 m/s is ~85 km/h)")]
        public float maxSpeed = 24.0f;

        [Tooltip("Top reverse speed in m/s")]
        public float reverseSpeed = 6.0f;

        [Tooltip("Forward acceleration rate")]
        public float acceleration = 14.0f;

        [Tooltip("Braking deceleration rate")]
        public float brakePower = 24.0f;

        [Tooltip("Natural coasting slowdown rate")]
        public float coastDecel = 6.0f;

        [Tooltip("Turn/steering speed")]
        public float turnSpeed = 65.0f;

        [Header("Motorcycle Lean (Arcade Mode)")]
        [Tooltip("Transform that tilts when turning. If null, automatically detected.")]
        public Transform visualRoot;

        [Tooltip("Maximum lean roll angle in degrees into corners")]
        public float maxLeanAngle = 22.0f;

        [Tooltip("How smoothly the motorcycle leans into and out of turns")]
        public float leanSpeed = 7.0f;

        [Header("Ground & Collision")]
        [Tooltip("Layers recognized as drivable ground/road")]
        public LayerMask groundLayers = ~0;

        [Tooltip("Height of ground raycast detection")]
        public float groundRayDistance = 2.0f;

        [Header("Audio (Optional)")]
        [Tooltip("Optional engine audio source. Pitch scales with speed.")]
        public AudioSource engineAudio;

        [Tooltip("Idle engine pitch")]
        public float minEnginePitch = 0.8f;

        [Tooltip("Top speed engine pitch")]
        public float maxEnginePitch = 1.9f;

        [Header("Status")]
        [Tooltip("Is a player currently driving this motorcycle")]
        public bool isRiding = false;

        [Tooltip("Current speed in m/s")]
        public float currentSpeed = 0f;

        [Header("Handlebar IK")]
        [Tooltip("If true and handlebar targets are set, uses OnAnimatorIK to lock rider hands to bars")]
        public bool useHandlebarIK = true;

        private float _currentLean = 0f;
        private float _verticalVelocity = 0f;
        private bool _showPrompt = false;

        private ThirdPersonController _rider;
        private Animator _riderAnimator;
        private RiderHandIK _riderHandIK;
        private Rigidbody _rb;
        private MonoBehaviour _bicycleVehicle;

        private Vector3 _lastSeatPos;
        private Vector3 _lastSeatRot;

        [Header("IK Handlebar Targets (Optional)")]
        [Tooltip("World-space transform where the left hand rests on the handlebar. Create an empty child GameObject on the bike.")]
        public Transform leftHandlebarTarget;

        [Tooltip("World-space transform where the right hand rests on the handlebar. Create an empty child GameObject on the bike.")]
        public Transform rightHandlebarTarget;

        [Tooltip("How strongly hands lock to handlebar IK targets (0=off, 1=full)")]
        [Range(0f, 1f)]
        public float handlebarIKWeight = 1f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
                _rb.mass = 185f;
            }

            if (_rb != null)
            {
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Check if built-in BicycleVehicle (WheelCollider physics) is present
            _bicycleVehicle = GetComponent("BicycleVehicle") as MonoBehaviour;

            if (_bicycleVehicle != null && useWheelPhysicsIfPresent)
            {
                // Start with bike parked (disabled) so it doesn't move when player walks around
                _bicycleVehicle.SendMessage("SetControlled", false, SendMessageOptions.DontRequireReceiver);
                _bicycleVehicle.enabled = false;

                // Keep parked bike stable and upright on its wheels
                if (_rb != null)
                {
                    _rb.isKinematic = false;
                    _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
            }
            else
            {
                // In arcade mode, only add BoxCollider if no colliders exist on the bike
                Collider[] existingColliders = GetComponentsInChildren<Collider>();
                if (existingColliders == null || existingColliders.Length == 0)
                {
                    BoxCollider bc = gameObject.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, 0.65f, 0f);
                    bc.size = new Vector3(0.8f, 1.3f, 2.2f);
                }

                if (_rb != null)
                {
                    _rb.isKinematic = true;
                }
            }

            // Auto-detect or create visualRoot (for leaning in arcade mode)
            if (visualRoot == null)
            {
                Transform defSys = transform.Find("Bike:DeformationSystem");
                if (defSys != null)
                {
                    visualRoot = defSys;
                }
                else if (transform.childCount > 0)
                {
                    visualRoot = transform.GetChild(0);
                }
            }

            // Auto-setup DriverSeat socket
            if (driverSeat == null)
            {
                Transform existingSeat = FindChildRecursive(transform, "DriverSeat");
                if (existingSeat != null)
                {
                    driverSeat = existingSeat;
                }
                else
                {
                    Transform parentForSeat = visualRoot != null ? visualRoot : transform;
                    GameObject seatSocket = new GameObject("DriverSeat");
                    seatSocket.transform.SetParent(parentForSeat, false);
                    seatSocket.transform.position = transform.TransformPoint(seatOffsetPosition);
                    seatSocket.transform.rotation = transform.rotation * Quaternion.Euler(seatOffsetRotation);
                    driverSeat = seatSocket.transform;
                }
            }

            if (driverSeat != null)
            {
                driverSeat.position = transform.TransformPoint(seatOffsetPosition);
                driverSeat.rotation = transform.rotation * Quaternion.Euler(seatOffsetRotation);
            }

            _lastSeatPos = seatOffsetPosition;
            _lastSeatRot = seatOffsetRotation;

            // Auto-setup ExitPoint if missing
            if (exitPoint == null)
            {
                Transform existingExit = FindChildRecursive(transform, "ExitPoint");
                if (existingExit != null)
                {
                    exitPoint = existingExit;
                }
                else
                {
                    GameObject exitObj = new GameObject("ExitPoint");
                    exitObj.transform.SetParent(transform, false);
                    exitObj.transform.localPosition = new Vector3(-1.3f, 0f, 0f);
                    exitPoint = exitObj.transform;
                }
            }

            // Auto-setup Engine Audio if AudioSource is attached
            if (engineAudio == null)
            {
                engineAudio = GetComponent<AudioSource>();
            }
        }

        private void Update()
        {
            if (!isRiding)
            {
                CheckPlayerProximity();
            }
            else
            {
                // Check for dismount input
                if (CheckActionKey())
                {
                    Dismount();
                    return;
                }

                // Update live offsets if tweaking in Inspector
                if (liveUpdateSeatOffsets && driverSeat != null)
                {
                    if (seatOffsetPosition != _lastSeatPos || seatOffsetRotation != _lastSeatRot)
                    {
                        driverSeat.position = transform.TransformPoint(seatOffsetPosition);
                        driverSeat.rotation = transform.rotation * Quaternion.Euler(seatOffsetRotation);
                        _lastSeatPos = seatOffsetPosition;
                        _lastSeatRot = seatOffsetRotation;
                    }
                }

                // Engine audio pitch scaling
                if (engineAudio != null && engineAudio.isPlaying)
                {
                    float speedRatio = Mathf.Abs(currentSpeed) / maxSpeed;
                    engineAudio.pitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, speedRatio);
                }
            }

            // If Arcade physics mode is used (no BicycleVehicle), update movement smoothly here
            if (_bicycleVehicle == null || !useWheelPhysicsIfPresent)
            {
                if (!isRiding)
                {
                    ApplyParkedPhysics();
                }
                else
                {
                    DriveMotorcycle();
                }
            }
        }

        private void FixedUpdate()
        {
            // If using WheelCollider physics via BicycleVehicle
            if (_bicycleVehicle != null && useWheelPhysicsIfPresent)
            {
                if (isRiding && _rb != null)
                {
                    currentSpeed = _rb.linearVelocity.magnitude;
                }
            }
        }

        private void CheckPlayerProximity()
        {
            if (_rider == null)
            {
#if UNITY_6000_0_OR_NEWER
                _rider = Object.FindFirstObjectByType<ThirdPersonController>();
#else
                _rider = FindObjectOfType<ThirdPersonController>();
#endif
            }

            if (_rider == null) return;

            // If player is already riding a motorcycle/vehicle, do not interact with other bikes
            if (_rider.isRidingVehicle)
            {
                _showPrompt = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, _rider.transform.position);
            if (dist <= interactDistance)
            {
                _showPrompt = true;
                if (CheckActionKey())
                {
                    Mount(_rider);
                }
            }
            else
            {
                _showPrompt = false;
            }
        }

        private bool CheckActionKey()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                Key k = Key.E;
                if (rideKey == KeyCode.F) k = Key.F;
                else if (rideKey == KeyCode.E) k = Key.E;
                else if (rideKey == KeyCode.Space) k = Key.Space;
                else if (rideKey == KeyCode.Return) k = Key.Enter;

                if (Keyboard.current[k].wasPressedThisFrame) return true;
            }
#endif
            return Input.GetKeyDown(rideKey);
        }

        public void Mount(ThirdPersonController player)
        {
            if (player == null) return;

            _rider = player;
            isRiding = true;
            _showPrompt = false;

            // Notify player controller to disable character walking & hide gun
            _rider.MountVehicle();

            // Explicitly disable CharacterController so it doesn't fight transform updates
            CharacterController cc = _rider.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Ensure DriverSeat socket exists and has current seat offsets
            if (driverSeat == null)
            {
                driverSeat = transform.Find("DriverSeat");
                if (driverSeat == null)
                {
                    GameObject seatObj = new GameObject("DriverSeat");
                    seatObj.transform.SetParent(transform, false);
                    driverSeat = seatObj.transform;
                }
            }

            driverSeat.position = transform.TransformPoint(seatOffsetPosition);
            driverSeat.rotation = transform.rotation * Quaternion.Euler(seatOffsetRotation);

            // Parent player to driver seat
            _rider.transform.SetParent(driverSeat, false);
            _rider.transform.localPosition = Vector3.zero;
            _rider.transform.localRotation = Quaternion.identity;
            _rider.transform.localScale = Vector3.one;

            // Directly snap to Idle Riding — no mount animation
            _riderAnimator = _rider.GetComponent<Animator>();
            if (_riderAnimator != null)
            {
                SetAnimatorBoolIfExists("IsRiding", true);
                _riderAnimator.CrossFadeInFixedTime("AS_Idle_Riding", 0.15f);
            }

            // Wire up handlebar IK on the player
            _riderHandIK = _rider.GetComponent<RiderHandIK>();
            if (_riderHandIK != null && useHandlebarIK)
            {
                _riderHandIK.SetTargets(leftHandlebarTarget, rightHandlebarTarget, handlebarIKWeight);
            }

            // Enable WheelCollider physics if present
            if (_bicycleVehicle != null && useWheelPhysicsIfPresent)
            {
                if (_rb != null)
                {
                    _rb.constraints = RigidbodyConstraints.None;
                }
                _bicycleVehicle.SendMessage("SetControlled", true, SendMessageOptions.DontRequireReceiver);
                _bicycleVehicle.enabled = true;
            }

            if (engineAudio != null && !engineAudio.isPlaying)
            {
                engineAudio.Play();
            }
        }

        private void SetAnimatorBoolIfExists(string paramName, bool value)
        {
            if (_riderAnimator == null) return;
            for (int i = 0; i < _riderAnimator.parameterCount; i++)
            {
                var p = _riderAnimator.parameters[i];
                if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool)
                {
                    _riderAnimator.SetBool(paramName, value);
                    return;
                }
            }
        }

        public void Dismount()
        {
            if (!isRiding) return;

            isRiding = false;
            currentSpeed = 0f;
            _currentLean = 0f;

            // Stop bike physics
            if (_bicycleVehicle != null && useWheelPhysicsIfPresent)
            {
                _bicycleVehicle.SendMessage("SetControlled", false, SendMessageOptions.DontRequireReceiver);
                _bicycleVehicle.enabled = false;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
            }

            if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;
            if (engineAudio != null && engineAudio.isPlaying) engineAudio.Stop();

            if (_rider != null)
            {
                // Release handlebar IK
                if (_riderHandIK != null) { _riderHandIK.ClearTargets(); _riderHandIK = null; }

                // Reset animator — no dismount animation, just clear riding state
                if (_riderAnimator != null)
                {
                    SetAnimatorBoolIfExists("IsRiding", false);
                    _riderAnimator.CrossFadeInFixedTime("Idle Walk Run Blend", 0.15f);
                }

                // Instantly unparent and place at exit point
                _rider.transform.SetParent(null);
                _rider.transform.localScale = Vector3.one;

                Vector3 exitPos = exitPoint != null
                    ? exitPoint.position
                    : transform.position - transform.right * 1.3f + Vector3.up * 0.1f;

                if (Physics.Raycast(exitPos + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3f, groundLayers))
                    exitPos = hit.point + Vector3.up * 0.05f;

                _rider.transform.position = exitPos;
                _rider.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

                CharacterController cc = _rider.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                _rider.DismountVehicle();
                _rider = null;
            }
        }

        private void DriveMotorcycle()
        {
            float v = GetVerticalInput();
            float h = GetHorizontalInput();

            // Throttle & Braking
            if (v > 0.05f)
            {
                if (currentSpeed < 0f)
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * Time.deltaTime);
                }
                else
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
                }
            }
            else if (v < -0.05f)
            {
                if (currentSpeed > 0.5f)
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * Time.deltaTime);
                }
                else
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, -reverseSpeed, acceleration * 0.6f * Time.deltaTime);
                }
            }
            else
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, coastDecel * Time.deltaTime);
            }

            // Steering Yaw
            if (Mathf.Abs(currentSpeed) > 0.1f)
            {
                float speedTurnFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / (maxSpeed * 0.35f));
                float dirFactor = currentSpeed >= 0f ? 1f : -1f;
                float yaw = h * turnSpeed * speedTurnFactor * dirFactor * Time.deltaTime;
                transform.Rotate(0f, yaw, 0f, Space.World);
            }

            // Realistic Lean Roll
            float targetLean = 0f;
            if (Mathf.Abs(currentSpeed) > 0.4f)
            {
                float speedLeanFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / (maxSpeed * 0.4f));
                targetLean = -h * maxLeanAngle * speedLeanFactor;
            }

            _currentLean = Mathf.Lerp(_currentLean, targetLean, Time.deltaTime * leanSpeed);

            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, 0f, _currentLean);
            }

            // Rider Animator parameter synchronization
            if (_riderAnimator != null)
            {
                for (int i = 0; i < _riderAnimator.parameterCount; i++)
                {
                    var p = _riderAnimator.parameters[i];
                    if (p.name == "Steer") _riderAnimator.SetFloat("Steer", h);
                    if (p.name == "BikeSpeed") _riderAnimator.SetFloat("BikeSpeed", Mathf.Abs(currentSpeed));
                }
            }

            // Ground Snapping & Slope Alignment
            Vector3 rayOrigin = transform.position + Vector3.up * 0.6f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance + 0.6f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                _verticalVelocity = 0f;

                Vector3 targetPosition = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 20f);

                // Stable rotation: preserve exact yaw, tilt to match ground normal
                float currentYaw = transform.eulerAngles.y;
                Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, currentYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
            else
            {
                _verticalVelocity -= 9.81f * Time.deltaTime;
                transform.position += Vector3.up * _verticalVelocity * Time.deltaTime;
            }

            // Forward Translation
            Vector3 forwardMove = transform.forward * currentSpeed * Time.deltaTime;
            transform.position += forwardMove;
        }

        private void ApplyParkedPhysics()
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.6f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundRayDistance + 0.6f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                Vector3 targetPos = new Vector3(transform.position.x, hit.point.y, transform.position.z);
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);

                float currentYaw = transform.eulerAngles.y;
                Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, currentYaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }

        private float GetVerticalInput()
        {
            float val = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) val += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) val -= 1f;
            }
#endif
            if (Mathf.Approximately(val, 0f))
            {
                val = Input.GetAxisRaw("Vertical");
            }
            return val;
        }

        private float GetHorizontalInput()
        {
            float val = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) val += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) val -= 1f;
            }
#endif
            if (Mathf.Approximately(val, 0f))
            {
                val = Input.GetAxisRaw("Horizontal");
            }
            return val;
        }

        private void OnGUI()
        {
            if (!isRiding && _showPrompt && (_rider == null || !_rider.isRidingVehicle))
            {
                DrawMountPrompt();
            }

            if (isRiding)
            {
                DrawRidingHUD();
            }
        }

        private void DrawMountPrompt()
        {
            float width = 340f;
            float height = 50f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 130f;

            Color bg = new Color(0.08f, 0.10f, 0.14f, 0.90f);
            GUI.color = bg;
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            Color border = new Color(0.2f, 0.8f, 1f, 0.9f);
            GUI.color = border;
            GUI.DrawTexture(new Rect(x, y, width, 2f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;

            string keyName = rideKey.ToString().ToUpper();
            GUI.Label(new Rect(x, y, width, height), $"Press [{keyName}] to Ride Motorcycle", labelStyle);
        }

        private void DrawRidingHUD()
        {
            // Dismount hint
            float topW = 280f;
            float topH = 34f;
            float topX = (Screen.width - topW) * 0.5f;
            float topY = 24f;

            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.75f);
            GUI.DrawTexture(new Rect(topX, topY, topW, topH), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            hintStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);
            string keyName = rideKey.ToString().ToUpper();
            GUI.Label(new Rect(topX, topY, topW, topH), $"Press [{keyName}] to Dismount", hintStyle);

            // Speedometer
            float speedKmH = Mathf.Max(0f, Mathf.Abs(currentSpeed) * 3.6f);
            float speedW = 180f;
            float speedH = 65f;
            float speedX = Screen.width - speedW - 24f;
            float speedY = Screen.height - speedH - 24f;

            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(speedX, speedY, speedW, speedH), Texture2D.whiteTexture);

            float fillRatio = Mathf.Clamp01(speedKmH / 100f);
            Color barCol = Color.Lerp(new Color(0.2f, 0.8f, 1f), new Color(1f, 0.3f, 0.2f), fillRatio);
            GUI.color = barCol;
            GUI.DrawTexture(new Rect(speedX, speedY + speedH - 4f, speedW * fillRatio, 4f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUIStyle numStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
            numStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(speedX + 16f, speedY + 8f, 80f, 36f), $"{Mathf.RoundToInt(speedKmH)}", numStyle);

            GUIStyle unitStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            unitStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
            GUI.Label(new Rect(speedX + 80f, speedY + 16f, 80f, 24f), "KM/H", unitStyle);
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform found = parent.Find(name);
            if (found != null) return found;

            for (int i = 0; i < parent.childCount; i++)
            {
                found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactDistance);

            Vector3 seatPos = driverSeat != null ? driverSeat.position : transform.TransformPoint(seatOffsetPosition);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(seatPos, 0.25f);

            Vector3 exitPos = exitPoint != null ? exitPoint.position : transform.TransformPoint(new Vector3(-1.3f, 0f, 0f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(exitPos, 0.3f);
        }
    }
}
