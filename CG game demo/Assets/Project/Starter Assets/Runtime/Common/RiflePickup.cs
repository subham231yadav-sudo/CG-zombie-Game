using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class RiflePickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [Tooltip("Display name of the gun for UI prompt")]
        public string gunName = "Assault Rifle";

        [Tooltip("Distance within which the player can pick up the gun")]
        public float pickupRadius = 2.5f;

        [Tooltip("Key to press to pick up the gun")]
        public KeyCode interactKey = KeyCode.F;

        [Tooltip("If true, any key press while close to the gun will pick it up")]
        public bool anyKeyToPickup = false;

        [Header("Audio Settings")]
        [Tooltip("Audio clip to play when picking up the rifle")]
        public AudioClip pickupSound;

        [Tooltip("Audio clip for firing the weapon")]
        public AudioClip gunshotSound;

        [Tooltip("Audio clip for reloading the weapon")]
        public AudioClip reloadSound;

        [Tooltip("Audio clip when firing with no bullets remaining")]
        public AudioClip emptyGunSound;

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

        [Header("Visual Feedback")]
        [Tooltip("Slowly rotate the gun while lying on the ground")]
        public bool rotateOnGround = true;
        public float rotationSpeed = 35f;

        private ThirdPersonController _nearbyPlayer;
        private bool _isPlayerClose = false;

        private void Start()
        {
            // If tag is not set, set to Gun
            if (!gameObject.CompareTag("Gun"))
            {
                try { gameObject.tag = "Gun"; } catch { }
            }
        }

        private void Update()
        {
            // Optional gentle ground idle rotation
            if (rotateOnGround)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }

            CheckPlayerDistance();

            if (_isPlayerClose && _nearbyPlayer != null && !_nearbyPlayer.hasRifle)
            {
                if (CheckInteractInput())
                {
                    PickupGun();
                }
            }
        }

        private void CheckPlayerDistance()
        {
            if (_nearbyPlayer == null)
            {
                _nearbyPlayer = FindAnyObjectByType<ThirdPersonController>();
            }

            if (_nearbyPlayer != null)
            {
                float dist = Vector3.Distance(transform.position, _nearbyPlayer.transform.position);
                _isPlayerClose = (dist <= pickupRadius);
            }
            else
            {
                _isPlayerClose = false;
            }
        }

        private bool CheckInteractInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            if (Input.GetKeyDown(interactKey))
            {
                return true;
            }

            return false;
        }

        public void PickupGun()
        {
            if (_nearbyPlayer != null)
            {
                if (gunshotSound != null) _nearbyPlayer.gunshotSound = gunshotSound;
                if (reloadSound != null) _nearbyPlayer.reloadSound = reloadSound;
                if (emptyGunSound != null) _nearbyPlayer.emptyGunSound = emptyGunSound;
                _nearbyPlayer.EquipRifleFromPickup(gameObject, pickupSound);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<ThirdPersonController>(out var player))
            {
                _nearbyPlayer = player;
                _isPlayerClose = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<ThirdPersonController>(out var player))
            {
                if (_nearbyPlayer == player)
                {
                    _isPlayerClose = false;
                }
            }
        }

        private void OnGUI()
        {
            // Render on-screen pickup prompt when player is nearby and has no rifle yet
            if (_isPlayerClose && _nearbyPlayer != null && !_nearbyPlayer.hasRifle)
            {
                DrawPickupPrompt();
            }
        }

        private void DrawPickupPrompt()
        {
            float width = 360f;
            float height = 54f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 130f;

            Rect bgRect = new Rect(x, y, width, height);

            // Dark semi-transparent pill background
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.88f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);

            // Gold/Yellow border line at bottom
            GUI.color = new Color(1f, 0.78f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(x, y + height - 3f, width, 3f), Texture2D.whiteTexture);

            // Key Badge and text
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;

            string prompt = $"Press [F] to Pick Up {gunName}";

            GUI.color = Color.white;
            GUI.Label(bgRect, prompt, labelStyle);
        }
    }
}
