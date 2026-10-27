using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// Attach this to the same GameObject as the Player Animator (PlayerArmature).
    /// The motorcycle controller calls SetTargets() on mount and ClearTargets() on dismount.
    ///
    /// SETUP REQUIRED:
    /// 1. Add this component to the PlayerArmature (the GameObject with the Animator).
    /// 2. In the Player Animator Controller, click Base Layer > enable "IK Pass" checkbox.
    /// 3. On the bike prefab, create two empty child GameObjects (HandlebarLeft, HandlebarRight)
    ///    and position them where the hands should grip. Assign them in SimpleMotorcycleController.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class RiderHandIK : MonoBehaviour
    {
        [Tooltip("How quickly IK weight blends in on mount and out on dismount")]
        public float blendSpeed = 8f;

        private Transform _leftTarget;
        private Transform _rightTarget;

        private float _leftWeight  = 0f;
        private float _rightWeight = 0f;
        private float _targetWeight = 0f;
        private float _maxWeight    = 1f;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        /// <summary>Called by SimpleMotorcycleController.Mount() to activate handlebar IK.</summary>
        public void SetTargets(Transform leftHandle, Transform rightHandle, float weight = 1f)
        {
            _leftTarget   = leftHandle;
            _rightTarget  = rightHandle;
            _maxWeight    = Mathf.Clamp01(weight);
            _targetWeight = 1f;
        }

        /// <summary>Called by SimpleMotorcycleController during dismount to release IK.</summary>
        public void ClearTargets()
        {
            _targetWeight = 0f;
        }

        private void Update()
        {
            float goal = _targetWeight * _maxWeight;
            _leftWeight  = Mathf.MoveTowards(_leftWeight,  goal, blendSpeed * Time.deltaTime);
            _rightWeight = Mathf.MoveTowards(_rightWeight, goal, blendSpeed * Time.deltaTime);

            if (_targetWeight <= 0f && _leftWeight <= 0.001f && _rightWeight <= 0.001f)
            {
                _leftTarget  = null;
                _rightTarget = null;
            }
        }

        // OnAnimatorIK MUST be on the same GameObject as the Animator - that is why this is a
        // separate script and NOT in the bike controller.
        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null) return;

            if (_leftTarget != null)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,  _leftWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,  _leftWeight);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand,  _leftTarget.position);
                _animator.SetIKRotation(AvatarIKGoal.LeftHand,  _leftTarget.rotation);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,  0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,  0f);
            }

            if (_rightTarget != null)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _rightWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _rightWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, _rightTarget.position);
                _animator.SetIKRotation(AvatarIKGoal.RightHand, _rightTarget.rotation);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            }
        }
    }
}
