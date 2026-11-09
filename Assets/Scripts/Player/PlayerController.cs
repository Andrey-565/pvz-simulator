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

        [Header("Crouch Settings")]
        [Tooltip("Height of the player CharacterController when crouched.")]
        [SerializeField] private float _crouchHeight = 1.0f;

        [Tooltip("Movement speed while crouching.")]
        [SerializeField] private float _crouchSpeed = 2.4f;

        [Tooltip("Speed of transition between standing and crouching.")]
        [SerializeField] private float _crouchTransitionSpeed = 10.0f;

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

        private float _standingHeight;
        private Vector3 _standingCenter;
        private float _standingCameraY;
        private float _crouchCameraY;
        private bool _isCrouching;

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static PlayerController Instance { get; private set; }

        /// <summary>
        /// Gets whether player movement and looking are currently locked.
        /// </summary>
        public bool IsMovementLocked => _isMovementLocked;

        /// <summary>
        /// True if the player is currently crouching.
        /// </summary>
        public bool IsCrouching => _isCrouching;

        private void Awake()
        {
            Instance = this;
            _characterController = GetComponent<CharacterController>();

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }

            if (_characterController != null)
            {
                _standingHeight = _characterController.height;
                _standingCenter = _characterController.center;
            }
            else
            {
                _standingHeight = 1.9f;
                _standingCenter = Vector3.zero;
            }

            if (_cameraTransform != null)
            {
                _standingCameraY = _cameraTransform.localPosition.y;
                float heightDiff = _standingHeight - _crouchHeight;
                _crouchCameraY = _standingCameraY - (heightDiff * 0.85f);
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
            HandleCrouch();
            HandleMovement();
        }

        /// <summary>
        /// Handles crouching on Ctrl (holding LeftControl/RightControl/C), smoothly lowering CharacterController height and camera Y.
        /// </summary>
        private void HandleCrouch()
        {
            if (_characterController == null)
            {
                return;
            }

            bool wantsToCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C);

            // If player releases Ctrl, verify there is enough overhead clearance to stand up
            if (!wantsToCrouch && _isCrouching)
            {
                float clearanceNeeded = _standingHeight - _characterController.height;
                if (clearanceNeeded > 0.05f)
                {
                    Vector3 origin = transform.position + _characterController.center + (Vector3.up * (_characterController.height * 0.35f));
                    if (Physics.SphereCast(origin, _characterController.radius * 0.8f, Vector3.up, out _, clearanceNeeded, ~0, QueryTriggerInteraction.Ignore))
                    {
                        // Something is blocking overhead (shelf, counter, etc.) - keep crouching!
                        wantsToCrouch = true;
                    }
                }
            }

            _isCrouching = wantsToCrouch;

            // Interpolate character controller height
            float targetHeight = _isCrouching ? _crouchHeight : _standingHeight;
            float newHeight = Mathf.MoveTowards(_characterController.height, targetHeight, _crouchTransitionSpeed * Time.deltaTime);
            _characterController.height = newHeight;

            // Adjust center so player's feet stay pinned to the floor
            float bottom = _standingCenter.y - (_standingHeight * 0.5f);
            Vector3 center = _characterController.center;
            center.y = bottom + (newHeight * 0.5f);
            _characterController.center = center;

            // Smoothly interpolate Camera Y position
            if (_cameraTransform != null)
            {
                float targetCamY = _isCrouching ? _crouchCameraY : _standingCameraY;
                Vector3 camPos = _cameraTransform.localPosition;
                camPos.y = Mathf.MoveTowards(camPos.y, targetCamY, _crouchTransitionSpeed * Time.deltaTime);
                _cameraTransform.localPosition = camPos;
            }
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

            float currentSpeed = _isCrouching ? _crouchSpeed : _moveSpeed;
            Vector3 velocity = (moveDirection * currentSpeed) + (Vector3.up * _verticalVelocity);
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
