using System;
using System.Collections.Generic;
using UnityEngine;
using PVZ.Items;

namespace PVZ.Warehouse
{
    /// <summary>
    /// Represents a discrete storage slot/cell on a warehouse shelf.
    /// Manages snapping, slot identification (e.g. A1, B2), box occupancy, and visual highlighting.
    /// </summary>
    public class WarehouseSlot : MonoBehaviour
    {
        [Header("Slot Configuration")]
        [Tooltip("Unique warehouse slot code (e.g. A1, A2, B1).")]
        [SerializeField] private string _slotId = "A1";

        [Tooltip("Transform position where placed boxes snap to.")]
        [SerializeField] private Transform _snapPoint;

        [Header("Capacity & Storage")]
        [Tooltip("Maximum number of products that can be placed in this single warehouse slot.")]
        public const int MaxCapacity = 5;

        [SerializeField] private List<BoxItem> _storedProducts = new List<BoxItem>();
        [SerializeField] private BoxItem _currentBox;
        [SerializeField] private bool _isHighlighted;
        private float _highlightTimer;
        private int _highlightTargetId = -1;

        /// <summary>
        /// Fires when a box is placed into or removed from this slot.
        /// </summary>
        public event Action<WarehouseSlot, BoxItem> OnSlotOccupancyChanged;

        /// <summary>
        /// Gets the unique slot code.
        /// </summary>
        public string SlotId => _slotId;

        /// <summary>
        /// Gets the front product currently stored in this slot, or null if empty.
        /// </summary>
        public BoxItem CurrentBox => (_storedProducts.Count > 0) ? _storedProducts[0] : _currentBox;

        /// <summary>
        /// Gets the full read-only list of products stored inside this cell.
        /// </summary>
        public IReadOnlyList<BoxItem> StoredProducts => _storedProducts;

        /// <summary>
        /// Number of products currently held inside this warehouse cell (0 to 5).
        /// </summary>
        public int StoredCount => _storedProducts.Count;

        /// <summary>
        /// True if no product is currently placed in this slot.
        /// </summary>
        public bool IsEmpty => _storedProducts.Count == 0 && _currentBox == null;

        /// <summary>
        /// True if this slot reached its maximum capacity of 5 products.
        /// </summary>
        public bool IsFull => _storedProducts.Count >= MaxCapacity;

        /// <summary>
        /// True if slot is currently highlighted by the counter scanner.
        /// </summary>
        public bool IsHighlighted => _isHighlighted;

        /// <summary>
        /// Gets the snap point transform for placing objects.
        /// </summary>
        public Transform SnapPoint => _snapPoint != null ? _snapPoint : transform;

        private void Awake()
        {
            if (_snapPoint == null)
            {
                _snapPoint = transform;
            }

            if (_currentBox != null && !_storedProducts.Contains(_currentBox))
            {
                _storedProducts.Add(_currentBox);
            }
        }

        private void Start()
        {
            if (WarehouseManager.Instance != null)
            {
                WarehouseManager.Instance.RegisterSlot(this);
            }

            for (int i = 0; i < _storedProducts.Count; i++)
            {
                if (_storedProducts[i] != null)
                {
                    _storedProducts[i].SetSlot(_slotId);
                }
            }
        }

        private void Update()
        {
            if (_isHighlighted)
            {
                _highlightTimer -= Time.deltaTime;
                if (_highlightTimer <= 0f)
                {
                    _isHighlighted = false;
                    _highlightTargetId = -1;
                }
            }
        }

        private void OnDestroy()
        {
            if (WarehouseManager.Instance != null)
            {
                WarehouseManager.Instance.UnregisterSlot(this);
            }
        }

        /// <summary>
        /// Initializes the slot identifier programmatically.
        /// </summary>
        /// <param name="slotId">Code like A1, B3.</param>
        public void Initialize(string slotId)
        {
            _slotId = slotId;
        }

        /// <summary>
        /// Highlights this warehouse slot with a visual on-screen marker for the specified duration.
        /// </summary>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="targetOrderId">Optional specific order ID being sought.</param>
        public void Highlight(float duration = 8.0f, int targetOrderId = -1)
        {
            _isHighlighted = true;
            _highlightTimer = duration;
            _highlightTargetId = targetOrderId;
        }

        /// <summary>
        /// Checks if this slot contains a product with the specified tracking order ID.
        /// </summary>
        public bool ContainsOrderId(int orderId)
        {
            return _storedProducts.Exists(p => p != null && p.OrderId == orderId);
        }

        /// <summary>
        /// Attempts to place a product parcel into this slot (up to 5 items per slot).
        /// </summary>
        /// <param name="box">The product item to place.</param>
        /// <returns>True if placed successfully, false if slot is full or item unscanned.</returns>
        public bool TryPlaceBox(BoxItem box)
        {
            if (box == null || IsFull)
            {
                return false;
            }

            if (!box.IsIntakeScanned)
            {
                Debug.LogWarning($"<color=yellow>[WarehouseSlot]</color> Нельзя положить товар #{box.OrderId} в ячейку [{_slotId}]: товар не принят сканером!");
                return false;
            }

            _storedProducts.Add(box);
            box.SetSlot(_slotId);

            int index = _storedProducts.Count - 1;
            Vector3 localOffset = GetLocalOffsetForIndex(index);
            box.PlaceAt(SnapPoint.TransformPoint(localOffset), SnapPoint.rotation);

            _currentBox = CurrentBox;
            OnSlotOccupancyChanged?.Invoke(this, box);
            return true;
        }

        /// <summary>
        /// Calculates neat shelf placement offset for up to 5 items inside a 1.1m x 0.8m slot.
        /// </summary>
        public static Vector3 GetLocalOffsetForIndex(int index)
        {
            return index switch
            {
                0 => new Vector3(-0.30f, 0.12f, -0.15f),
                1 => new Vector3( 0.00f, 0.12f, -0.15f),
                2 => new Vector3( 0.30f, 0.12f, -0.15f),
                3 => new Vector3(-0.16f, 0.12f,  0.18f),
                4 => new Vector3( 0.16f, 0.12f,  0.18f),
                _ => new Vector3( 0.00f, 0.12f,  0.00f)
            };
        }

        /// <summary>
        /// Removes a product from this slot (optionally targeting a specific order ID).
        /// </summary>
        /// <param name="targetOrderId">If specified >= 0, seeks that specific product.</param>
        /// <returns>The removed product item or null if empty.</returns>
        public BoxItem RemoveBox(int targetOrderId = -1)
        {
            if (IsEmpty)
            {
                return null;
            }

            BoxItem removed = null;
            if (targetOrderId >= 0)
            {
                removed = _storedProducts.Find(p => p != null && p.OrderId == targetOrderId);
            }

            if (removed == null)
            {
                // Default to the last added product
                removed = _storedProducts[_storedProducts.Count - 1];
            }

            _storedProducts.Remove(removed);
            removed.ClearSlot();

            _currentBox = CurrentBox;
            OnSlotOccupancyChanged?.Invoke(this, CurrentBox);
            return removed;
        }

        /// <summary>
        /// Forcefully clears and destroys all products inside this slot (used for clean restart).
        /// </summary>
        public void ClearSlot()
        {
            for (int i = _storedProducts.Count - 1; i >= 0; i--)
            {
                if (_storedProducts[i] != null)
                {
                    Destroy(_storedProducts[i].gameObject);
                }
            }
            _storedProducts.Clear();
            _currentBox = null;
            OnSlotOccupancyChanged?.Invoke(this, null);
        }

        private void OnGUI()
        {
            if (!_isHighlighted)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 worldPos = transform.position + Vector3.up * 0.4f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // Only show if in front of camera
            if (screenPos.z > 0.5f)
            {
                float x = screenPos.x;
                float y = Screen.height - screenPos.y;

                GUIStyle beaconStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                beaconStyle.normal.textColor = Color.yellow;

                string targetStr = (_highlightTargetId > 0) ? $"Товар #{_highlightTargetId} | " : "";
                string text = $"🎯 ЯЧЕЙКА [{_slotId}] ({targetStr}{StoredCount}/5 тов.)";
                GUI.Box(new Rect(x - 120, y - 20, 240, 32), text, beaconStyle);
            }
        }
    }
}
