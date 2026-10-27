using UnityEngine;

namespace StarterAssets
{
    public class BulletProjectile : MonoBehaviour
    {
        [Tooltip("Speed of the bullet projectile in m/s")]
        public float speed = 130f;

        [Tooltip("Maximum lifetime before being destroyed")]
        public float lifeTime = 3.0f;

        [Tooltip("Damage dealt to enemies")]
        public float damage = 25f;

        [Tooltip("Physical impact force applied to rigidbodies")]
        public float impactForce = 12f;

        [Tooltip("Layer mask for collision checks")]
        public LayerMask hitLayers = ~0;

        private Vector3 _lastPosition;
        private float _spawnTime;

        private void Start()
        {
            _lastPosition = transform.position;
            _spawnTime = Time.time;
            Destroy(gameObject, lifeTime);

            // Ensure a trail renderer exists for visible tracer line
            TrailRenderer trail = GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.05f;
                trail.startWidth = 0.035f;
                trail.endWidth = 0.005f;
                
                // Yellow/Orange glowing bullet tracer gradient
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
                );
                trail.colorGradient = gradient;
                trail.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 moveDelta = transform.forward * speed * Time.deltaTime;
            Vector3 nextPos = currentPos + moveDelta;
            float stepDistance = moveDelta.magnitude;

            // Continuous raycast sweep to ensure bullet never tunnels through colliders
            if (Physics.Raycast(currentPos, transform.forward, out RaycastHit hit, stepDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                OnHit(hit);
                return;
            }

            transform.position = nextPos;
            _lastPosition = currentPos;
        }

        private void OnHit(RaycastHit hit)
        {
            transform.position = hit.point;

            // Push Rigidbodies realistically (clamped so ragdoll bones don't fly into space)
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                float clampedImpulse = Mathf.Min(impactForce, Mathf.Max(3f, hit.rigidbody.mass * 2.5f));
                hit.rigidbody.AddForceAtPosition(transform.forward * clampedImpulse, hit.point, ForceMode.Impulse);
            }

            // Deal damage to enemies/zombies (single damage call, bubbles up to root ZombieHealth)
            hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            // Spawn simple impact spark/dust
            CreateHitSpark(hit.point, hit.normal);

            Destroy(gameObject);
        }

        private void CreateHitSpark(Vector3 point, Vector3 normal)
        {
            GameObject spark = new GameObject("BulletImpact_Spark");
            spark.transform.position = point;
            spark.transform.rotation = Quaternion.LookRotation(normal);

            Light flash = spark.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.8f, 0.4f);
            flash.range = 2.0f;
            flash.intensity = 3.0f;

            Destroy(spark, 0.06f);
        }
    }
}
