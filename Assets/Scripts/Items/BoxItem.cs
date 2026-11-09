using UnityEngine;

namespace PVZ.Items
{
    /// <summary>
    /// Represents an individual physical parcel/box item in the pickup point.
    /// Can be picked up, carried, placed onto warehouse slots, or thrown.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class BoxItem : MonoBehaviour
    {
        [Header("Item Properties")]
        [Tooltip("Unique order tracking identifier assigned to this parcel.")]
        [SerializeField] private int _orderId = 101;

        [Tooltip("The warehouse slot currently holding this box (empty if not slotted).")]
        [SerializeField] private string _currentSlotId = string.Empty;

        [Tooltip("True once the box has been scanned in during warehouse supply intake.")]
        [SerializeField] private bool _isIntakeScanned = false;

        private Rigidbody _rigidbody;
        private Collider _collider;
        private bool _isCarried;
        private GameObject _intakeStickerObj;
        private Renderer _intakeStickerRenderer;

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

        /// <summary>
        /// True if the package has been scanned in with the handheld scanner upon delivery.
        /// Must be scanned before it can be placed onto warehouse shelves.
        /// </summary>
        public bool IsIntakeScanned => _isIntakeScanned;

        private void Awake()
        {
            EnsureComponents();
            BuildStickerVisuals();
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
                if (_collider == null)
                {
                    _collider = gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        private void BuildStickerVisuals()
        {
            if (_intakeStickerObj != null)
            {
                return;
            }

            _intakeStickerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _intakeStickerObj.name = "BarcodeSticker";
            _intakeStickerObj.transform.SetParent(transform);
            _intakeStickerObj.transform.localPosition = new Vector3(0f, 0.255f, 0f);
            _intakeStickerObj.transform.localRotation = Quaternion.identity;
            _intakeStickerObj.transform.localScale = new Vector3(0.28f, 0.015f, 0.20f);

            Collider c = _intakeStickerObj.GetComponent<Collider>();
            if (c != null)
            {
                DestroyImmediate(c);
            }

            _intakeStickerRenderer = _intakeStickerObj.GetComponent<Renderer>();
            UpdateStickerVisuals();
        }

        private void UpdateStickerVisuals()
        {
            if (_intakeStickerRenderer == null)
            {
                BuildStickerVisuals();
            }

            if (_intakeStickerRenderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                // Orange/Yellow if awaiting intake; Bright green once accepted/scanned!
                Color col = _isIntakeScanned
                    ? new Color(0.12f, 0.85f, 0.25f)
                    : new Color(0.95f, 0.55f, 0.1f);

                Material mat = new Material(shader) { color = col };
                _intakeStickerRenderer.material = mat;
            }
        }

        /// <summary>
        /// Sets the intake status of the parcel, updating its visual barcode label.
        /// </summary>
        public void SetIntakeScanned(bool scanned)
        {
            _isIntakeScanned = scanned;
            UpdateStickerVisuals();
        }

        /// <summary>
        /// Initializes the box with a specific order ID.
        /// </summary>
        /// <param name="orderId">Unique order number.</param>
        /// <param name="isIntakeScanned">True if already intake-scanned.</param>
        public void Initialize(int orderId, bool isIntakeScanned = false)
        {
            _orderId = orderId;
            _isIntakeScanned = isIntakeScanned;
            UpdateStickerVisuals();
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
