using System;
using UnityEngine;

namespace ZombieGame
{
    public class ZombieHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth;
        [SerializeField] private bool _isDead = false;

        [Header("Feedback Effects")]
        [SerializeField] private AudioClip _hitSound;
        [SerializeField] private AudioClip[] _hitSounds;
        [SerializeField] private AudioClip _deathSound;
        [SerializeField] private AudioClip[] _deathSounds;
        [SerializeField] private GameObject _hitVfxPrefab;
        [SerializeField] private float _corpseCleanupDelay = 15f;
        [SerializeField] private float _soundHearingRange = 22f;

        private AudioSource _audioSource;
        private ZombieAI _zombieAI;
        private ZombieRagdoll _ragdoll;
        private Vector3 _lastHitForce = Vector3.zero;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _isDead;

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action OnDeath;

        private void Awake()
        {
            _currentHealth = _maxHealth;
            EnsureAudioSource();
            _zombieAI = GetComponent<ZombieAI>();
            _ragdoll = GetComponent<ZombieRagdoll>();
        }

        private AudioSource EnsureAudioSource()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Always configure 100% 3D spatial sound with proper falloff
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1.0f; // 100% 3D (no 2D audio bleed across the map!)
            _audioSource.minDistance = 2.0f;
            _audioSource.maxDistance = _soundHearingRange;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.volume = 1f;

            return _audioSource;
        }

        /// <summary>
        /// Compatible with BulletProjectile.cs SendMessage("TakeDamage", damage)
        /// </summary>
        public void TakeDamage(float damage)
        {
            TakeDamage(damage, Vector3.zero);
        }

        public void TakeDamage(float damage, Vector3 hitForce)
        {
            if (_isDead) return;

            if (hitForce != Vector3.zero)
            {
                _lastHitForce = hitForce;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            // Play hit audio
            AudioClip clipToPlay = GetRandomClip(_hitSounds, _hitSound);
            if (clipToPlay != null)
            {
                AudioSource src = EnsureAudioSource();
                if (src != null)
                {
                    src.pitch = UnityEngine.Random.Range(0.88f, 1.15f);
                    src.PlayOneShot(clipToPlay);
                }
            }

            // Alert the AI to target the player upon getting shot
            if (_zombieAI != null && !_isDead)
            {
                _zombieAI.AlertTarget();
            }

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Backward compatibility for damage messages
        /// </summary>
        public void ApplyDamage(float damage)
        {
            TakeDamage(damage, Vector3.zero);
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            OnDeath?.Invoke();

            // Play death sound
            AudioClip deathClip = GetRandomClip(_deathSounds, _deathSound);
            if (deathClip != null)
            {
                AudioSource src = EnsureAudioSource();
                if (src != null)
                {
                    src.pitch = UnityEngine.Random.Range(0.82f, 1.08f);
                    src.PlayOneShot(deathClip);
                }
            }

            // Disable AI
            if (_zombieAI != null)
            {
                _zombieAI.OnDeath();
            }

            // Enable physics ragdoll if present
            if (_ragdoll != null)
            {
                _ragdoll.EnableRagdoll(_lastHitForce);
            }
            else
            {
                // Fallback: disable animator and collider if no ragdoll
                Animator anim = GetComponent<Animator>();
                if (anim != null) anim.enabled = false;
                
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }

            // Optional corpse destruction after timer
            if (_corpseCleanupDelay > 0f)
            {
                Destroy(gameObject, _corpseCleanupDelay);
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
                    return valid[UnityEngine.Random.Range(0, valid.Count)];
                }
            }
            return fallback;
        }
    }
}
