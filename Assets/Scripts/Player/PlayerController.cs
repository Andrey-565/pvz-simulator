using UnityEngine;

namespace PVZ.Player
{
    /// <summary>
    /// First-person player controller providing responsive WASD movement,
    /// mouse look, gravity handling, and collision resolution via CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Walking speed in units per second.")]
        [SerializeField] private float _moveSpeed = 4.5f;

        [Tooltip("Downward gravitational acceleration.")]
        [SerializeField] private float _gravity = -9.81f;

        [Header("Look Settings")]
        [Tooltip("First-person camera transform.")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Mouse look sensitivity multiplier.")]
        [SerializeField] private float _mouseSensitivity = 2.0f;

        [Tooltip("Minimum vertical angle limit (looking up).")]
        [SerializeField] private float _minPitchAngle = -85f;

        [Tooltip("Maximum vertical angle limit (looking down).")]
        [SerializeField] private float _maxPitchAngle = 85f;

        private CharacterController _characterController;
        private float _verticalVelocity;
        private float _cameraPitch;
        private bool _isMovementLocked;

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static PlayerController Instance { get; private set; }

        /// <summary>
        /// Gets whether player movement and looking are currently locked.
        /// </summary>
        public bool IsMovementLocked => _isMovementLocked;

        private void Awake()
        {
            Instance = this;
            _characterController = GetComponent<CharacterController>();

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        /// <summary>
        /// Teleports the player to a target position and orientation safely disabling CharacterController.
        /// </summary>
        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            if (_characterController != null)
            {
                _characterController.enabled = false;
                transform.position = position;
                transform.rotation = rotation;
                _characterController.enabled = true;
            }
            else
            {
                transform.position = position;
                transform.rotation = rotation;
            }
        }

        private void Start()
        {
            SetCursorLocked(true);
        }

        private void Update()
        {
            if (_isMovementLocked)
            {
                return;
            }

            HandleLook();
            HandleMovement();
        }

        /// <summary>
        /// Handles mouse look rotation for yaw (body) and pitch (camera).
        /// </summary>
        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

            // Rotate player body horizontally (yaw)
            transform.Rotate(Vector3.up * mouseX);

            // Rotate camera vertically (pitch) with clamping
            if (_cameraTransform != null)
            {
                _cameraPitch -= mouseY;
                _cameraPitch = Mathf.Clamp(_cameraPitch, _minPitchAngle, _maxPitchAngle);
                _cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            }
        }

        /// <summary>
        /// Computes WASD movement relative to player facing direction and applies gravity.
        /// </summary>
        private void HandleMovement()
        {
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float verticalInput = Input.GetAxisRaw("Vertical");

            Vector3 moveDirection = (transform.right * horizontalInput + transform.forward * verticalInput).normalized;

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f; // Slight downward force to keep grounded
            }
            else
            {
                _verticalVelocity += _gravity * Time.deltaTime;
            }

            Vector3 velocity = (moveDirection * _moveSpeed) + (Vector3.up * _verticalVelocity);
            _characterController.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// Enables or disables player input (useful when accessing computer or dialogs).
        /// </summary>
        /// <param name="locked">True to freeze player controls, false to restore.</param>
        public void SetMovementLocked(bool locked)
        {
            _isMovementLocked = locked;
            SetCursorLocked(!locked);
        }

        /// <summary>
        /// Toggles system cursor lock state and visibility.
        /// </summary>
        /// <param name="locked">True to lock and hide cursor, false to unlock and show.</param>
        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
