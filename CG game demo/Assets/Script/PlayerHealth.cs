using System;
using UnityEngine;
using StarterAssets;
using UnityEngine.SceneManagement;

namespace ZombieGame
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth;
        [SerializeField] private bool _isDead = false;

        [Header("Death Animation")]
        [Tooltip("Parameter name in the Animator used to trigger the death transition (defaults to 'Death')")]
        [SerializeField] private string _deathParameterName = "Death";
        [Tooltip("Fallback state name to crossfade if parameter is not found")]
        [SerializeField] private string _deathStateName = "Death";

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip _hurtSound;
        [SerializeField] private AudioClip _deathSound;
        [SerializeField] private AudioSource _audioSource;

        [Header("UI & HUD")]
        [SerializeField] private bool _showOnScreenHUD = true;

        // Player components to disable on death
        private Animator _animator;
        private ThirdPersonController _thirdPersonController;
        private CharacterController _characterController;
        private StarterAssetsInputs _starterInputs;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _isDead;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDeath;

        private void Awake()
        {
            _currentHealth = _maxHealth;

            // Find AudioSource
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

            // Find Animator (on this GameObject or in children/hierarchy)
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            // Find ThirdPersonController & inputs
            _thirdPersonController = GetComponent<ThirdPersonController>();
            if (_thirdPersonController == null) _thirdPersonController = GetComponentInChildren<ThirdPersonController>();

            _characterController = GetComponent<CharacterController>();
            if (_characterController == null) _characterController = GetComponentInChildren<CharacterController>();

            _starterInputs = GetComponent<StarterAssetsInputs>();
            if (_starterInputs == null) _starterInputs = GetComponentInChildren<StarterAssetsInputs>();
        }

        /// <summary>
        /// Called when the player receives damage from zombie attacks or projectiles
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isDead) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_audioSource != null && _hurtSound != null)
            {
                _audioSource.PlayOneShot(_hurtSound);
            }

            Debug.Log($"[PlayerHealth] Player took {damage} damage! Current HP: {_currentHealth}/{_maxHealth}");

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        public void ApplyDamage(float damage)
        {
            TakeDamage(damage);
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("<color=red><b>[PlayerHealth] Player has DIED!</b></color>");

            // 1. Play death sound
            if (_audioSource != null && _deathSound != null)
            {
                _audioSource.PlayOneShot(_deathSound);
            }

            // 2. Play Death animation in Animator
            TriggerDeathAnimation();

            // 3. Disable Player movement and input so character stays down
            DisablePlayerControls();

            OnDeath?.Invoke();
        }

        private void TriggerDeathAnimation()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
                if (_animator == null)
                {
                    Debug.LogWarning("[PlayerHealth] No Animator found on Player to play Death animation!");
                    return;
                }
            }

            bool triggered = false;

            // Try candidate parameters (custom name, Death, Die, IsDead, Dead)
            string[] candidates = new string[] { _deathParameterName, "Death", "Die", "IsDead", "Dead" };

            foreach (string paramName in candidates)
            {
                if (string.IsNullOrEmpty(paramName)) continue;

                foreach (var p in _animator.parameters)
                {
                    if (string.Equals(p.name, paramName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (p.type == AnimatorControllerParameterType.Trigger)
                        {
                            _animator.SetTrigger(p.name);
                            triggered = true;
                            Debug.Log($"[PlayerHealth] Triggered Animator Trigger: {p.name}");
                        }
                        else if (p.type == AnimatorControllerParameterType.Bool)
                        {
                            _animator.SetBool(p.name, true);
                            triggered = true;
                            Debug.Log($"[PlayerHealth] Set Animator Bool: {p.name} = true");
                        }
                        break;
                    }
                }
                if (triggered) break;
            }

            // Zero out motion parameters so locomotion blend trees don't override the death state
            SetAnimatorParamIfExists("Speed", 0f);
            SetAnimatorParamIfExists("MotionSpeed", 0f);
            SetAnimatorParamIfExists("Jump", false);
            SetAnimatorParamIfExists("FreeFall", false);
            SetAnimatorParamIfExists("Shoot", false);

            // If no matching parameter condition was found, directly crossfade to state
            if (!triggered && !string.IsNullOrEmpty(_deathStateName))
            {
                _animator.CrossFadeInFixedTime(_deathStateName, 0.15f);
                Debug.Log($"[PlayerHealth] CrossFaded to Animator State: {_deathStateName}");
            }
        }

        private void SetAnimatorParamIfExists(string paramName, float value)
        {
            if (_animator == null) return;
            foreach (var p in _animator.parameters)
            {
                if (p.name == paramName && p.type == AnimatorControllerParameterType.Float)
                {
                    _animator.SetFloat(paramName, value);
                    return;
                }
            }
        }

        private void SetAnimatorParamIfExists(string paramName, bool value)
        {
            if (_animator == null) return;
            foreach (var p in _animator.parameters)
            {
                if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool)
                {
                    _animator.SetBool(paramName, value);
                    return;
                }
            }
        }

        private void DisablePlayerControls()
        {
            // Disable ThirdPersonController script (stops walk/run/jump/aim updates)
            if (_thirdPersonController != null)
            {
                _thirdPersonController.enabled = false;
            }

            // Zero inputs and unlock cursor
            if (_starterInputs != null)
            {
                _starterInputs.move = Vector2.zero;
                _starterInputs.look = Vector2.zero;
                _starterInputs.jump = false;
                _starterInputs.sprint = false;
                _starterInputs.shoot = false;
                _starterInputs.cursorLocked = false;
                _starterInputs.cursorInputForLook = false;
                _starterInputs.enabled = false;
            }

            // Disable CharacterController
            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            // Force mouse arrow to be unlocked and visible
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!_showOnScreenHUD) return;

            // 1. Health Bar at top-left
            float barWidth = 220f;
            float barHeight = 22f;
            float margin = 20f;

            GUI.Box(new Rect(margin - 4, margin - 4, barWidth + 8, barHeight + 8), "");

            float healthPercent = Mathf.Clamp01(_currentHealth / _maxHealth);
            Color barColor = Color.Lerp(Color.red, Color.green, healthPercent);

            Texture2D texture = Texture2D.whiteTexture;
            Color prevColor = GUI.color;
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(margin, margin, barWidth * healthPercent, barHeight), texture);
            GUI.color = prevColor;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(margin, margin, barWidth, barHeight), $"HP: {Mathf.CeilToInt(_currentHealth)} / {Mathf.CeilToInt(_maxHealth)}", labelStyle);

            // 2. Game Over Overlay on Death
            if (_isDead)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                float panelW = 340f;
                float panelH = 160f;
                float panelX = (Screen.width - panelW) * 0.5f;
                float panelY = (Screen.height - panelH) * 0.5f;

                GUI.Box(new Rect(panelX, panelY, panelW, panelH), "");

                GUIStyle deadTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 32,
                    fontStyle = FontStyle.Bold
                };
                deadTitleStyle.normal.textColor = new Color(0.9f, 0.15f, 0.15f);
                GUI.Label(new Rect(panelX, panelY + 15, panelW, 45), "YOU DIED", deadTitleStyle);

                GUIStyle subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14
                };
                subStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(panelX, panelY + 60, panelW, 25), "The zombies overwhelmed you!", subStyle);

                if (GUI.Button(new Rect(panelX + 70, panelY + 95, 200, 42), "Respawn / Restart"))
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }
        }
    }
}
