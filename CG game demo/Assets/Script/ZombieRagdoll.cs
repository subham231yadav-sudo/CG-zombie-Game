using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame
{
    public class ZombieRagdoll : MonoBehaviour
    {
        [Header("Ragdoll Settings")]
        [Tooltip("Extra force applied to the hips upon death")]
        [SerializeField] private float _deathImpulse = 6f;

        private Animator _animator;
        private NavMeshAgent _navAgent;
        private Collider _mainCollider;
        private Rigidbody _mainRigidbody;

        private readonly List<Rigidbody> _ragdollBodies = new List<Rigidbody>();
        private readonly List<Collider> _ragdollColliders = new List<Collider>();
        private Rigidbody _pelvisBody;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _navAgent = GetComponent<NavMeshAgent>();
            _mainCollider = GetComponent<Collider>();
            _mainRigidbody = GetComponent<Rigidbody>();

            CollectRagdollParts();
            SetRagdollState(false);
        }

        private void CollectRagdollParts()
        {
            _ragdollBodies.Clear();
            _ragdollColliders.Clear();

            // Collect all rigidbodies in child transforms (bones)
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in bodies)
            {
                if (rb == _mainRigidbody) continue; // Exclude root rigidbody if any

                _ragdollBodies.Add(rb);

                // Add realistic damping so bones have real physical weight and settle naturally
                rb.linearDamping = 0.8f;
                rb.angularDamping = 1.0f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // Ensure realistic minimum mass for ragdoll stability
                if (rb.mass < 3f)
                {
                    string bName = rb.name.ToLower();
                    if (bName.Contains("hip") || bName.Contains("pelvis")) rb.mass = 18f;
                    else if (bName.Contains("spine") || bName.Contains("chest")) rb.mass = 14f;
                    else if (bName.Contains("thigh") || bName.Contains("leg")) rb.mass = 8f;
                    else if (bName.Contains("head")) rb.mass = 5f;
                    else rb.mass = 3.5f;
                }

                // Identify hips/pelvis (usually first bone or name contains Hips/Pelvis/Spine)
                if (_pelvisBody == null && (rb.name.ToLower().Contains("hip") || rb.name.ToLower().Contains("pelvis") || rb.name.ToLower().Contains("spine")))
                {
                    _pelvisBody = rb;
                }
            }

            if (_pelvisBody == null && _ragdollBodies.Count > 0)
            {
                _pelvisBody = _ragdollBodies[0];
            }

            // Collect all child colliders
            Collider[] cols = GetComponentsInChildren<Collider>();
            foreach (Collider col in cols)
            {
                if (col == _mainCollider) continue; // Exclude root main collider
                _ragdollColliders.Add(col);
            }
        }

        /// <summary>
        /// Enable or disable ragdoll physics.
        /// When active == false: Bones follow Animator (isKinematic = true)
        /// When active == true: Bones simulate realistic ragdoll physics (isKinematic = false)
        /// </summary>
        public void SetRagdollState(bool active)
        {
            // Toggle Animator
            if (_animator != null)
            {
                _animator.enabled = !active;
            }

            // Toggle NavMeshAgent
            if (_navAgent != null)
            {
                _navAgent.enabled = !active;
            }

            // Toggle Main Collider on Root
            if (_mainCollider != null)
            {
                _mainCollider.enabled = !active;
            }

            // Toggle Ragdoll Rigidbodies
            foreach (Rigidbody rb in _ragdollBodies)
            {
                if (rb != null)
                {
                    rb.isKinematic = !active;
                    rb.detectCollisions = true;
                }
            }
        }

        public void EnableRagdoll(Vector3 impactForce = default)
        {
            SetRagdollState(true);

            // Apply push force to make death feel impactful yet heavy and realistic
            if (_pelvisBody != null)
            {
                if (impactForce != Vector3.zero)
                {
                    // Clamp force so bullet impulse feels heavy, not explosive
                    Vector3 clampedForce = Vector3.ClampMagnitude(impactForce, 16f);
                    _pelvisBody.AddForce(clampedForce + Vector3.up * 1f, ForceMode.Impulse);
                }
                else
                {
                    // Gentle backward slump
                    Vector3 backwardForce = -transform.forward * _deathImpulse + Vector3.up * 1.5f;
                    _pelvisBody.AddForce(backwardForce, ForceMode.Impulse);
                }
            }
        }
    }
}
