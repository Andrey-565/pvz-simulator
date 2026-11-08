using UnityEngine;

namespace PVZ.Items
{
    /// <summary>
    /// Represents an individual physical parcel/box item in the pickup point.
    /// Can be picked up, carried, placed onto warehouse slots, or thrown.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class BoxItem : MonoBehaviour
    {
        [Header("Item Properties")]
        [Tooltip("Unique order tracking identifier assigned to this parcel.")]
        [SerializeField] private int _orderId = 101;

        [Tooltip("The warehouse slot currently holding this box (empty if not slotted).")]
        [SerializeField] private string _currentSlotId = string.Empty;

        private Rigidbody _rigidbody;
        private Collider _collider;
        private bool _isCarried;

        /// <summary>
        /// Gets the order ID associated with this package.
        /// </summary>
        public int OrderId => _orderId;

        /// <summary>
        /// Gets the slot identifier if stored on a warehouse shelf.
        /// </summary>
        public string CurrentSlotId => _currentSlotId;

        /// <summary>
        /// Indicates whether this box is currently held in the player's hands.
        /// </summary>
        public bool IsCarried => _isCarried;

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }
        }

        /// <summary>
        /// Initializes the box with a specific order ID.
        /// </summary>
        /// <param name="orderId">Unique order number.</param>
        public void Initialize(int orderId)
        {
            _orderId = orderId;
        }

        /// <summary>
        /// Assigns the warehouse slot location for this package.
        /// </summary>
        /// <param name="slotId">Slot identifier (e.g. A1, B2).</param>
        public void SetSlot(string slotId)
        {
            _currentSlotId = slotId;
        }

        /// <summary>
        /// Clears the warehouse slot registration when picked up from a shelf.
        /// </summary>
        public void ClearSlot()
        {
            _currentSlotId = string.Empty;
        }

        /// <summary>
        /// Sets the carried state of the box, updating physics and hierarchy.
        /// </summary>
        /// <param name="carried">True when picked up, false when released.</param>
        /// <param name="parent">Target transform to attach to when carried.</param>
        public void SetCarried(bool carried, Transform parent = null)
        {
            EnsureComponents();
            _isCarried = carried;

            if (_isCarried)
            {
                ClearSlot();
                if (_rigidbody != null)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                    _rigidbody.isKinematic = true;
                }
                if (_collider != null)
                {
                    _collider.enabled = false;
                }

                transform.SetParent(parent);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.SetParent(null);
                if (_rigidbody != null)
                {
                    _rigidbody.isKinematic = false;
                }
                if (_collider != null)
                {
                    _collider.enabled = true;
                }
            }
        }

        /// <summary>
        /// Releases and throws the box in a given direction with an impulse force.
        /// </summary>
        /// <param name="direction">Normalized throw direction vector.</param>
        /// <param name="force">Force magnitude of the throw.</param>
        public void Throw(Vector3 direction, float force)
        {
            EnsureComponents();
            SetCarried(false);
            if (_rigidbody != null)
            {
                _rigidbody.AddForce(direction.normalized * force, ForceMode.Impulse);
                _rigidbody.AddTorque(Random.insideUnitSphere * (force * 0.5f), ForceMode.Impulse);
            }
        }

        /// <summary>
        /// Places the box firmly at a designated world position and orientation.
        /// </summary>
        /// <param name="position">World position coordinates.</param>
        /// <param name="rotation">Orientation quaternion.</param>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            EnsureComponents();
            SetCarried(false);
            transform.position = position;
            transform.rotation = rotation;
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }
}
