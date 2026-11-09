using System;
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

        [Header("State")]
        [SerializeField] private BoxItem _currentBox;
        [SerializeField] private bool _isHighlighted;
        private float _highlightTimer;

        /// <summary>
        /// Fires when a box is placed into or removed from this slot.
        /// </summary>
        public event Action<WarehouseSlot, BoxItem> OnSlotOccupancyChanged;

        /// <summary>
        /// Gets the unique slot code.
        /// </summary>
        public string SlotId => _slotId;

        /// <summary>
        /// Gets the box currently stored in this slot, or null if empty.
        /// </summary>
        public BoxItem CurrentBox => _currentBox;

        /// <summary>
        /// True if no box is currently placed in this slot.
        /// </summary>
        public bool IsEmpty => _currentBox == null;

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
        }

        private void Start()
        {
            if (WarehouseManager.Instance != null)
            {
                WarehouseManager.Instance.RegisterSlot(this);
            }

            // Check if a box was already placed in this slot in the scene
            if (_currentBox != null)
            {
                _currentBox.SetSlot(_slotId);
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
        public void Highlight(float duration = 8.0f)
        {
            _isHighlighted = true;
            _highlightTimer = duration;
        }

        /// <summary>
        /// Attempts to place a box into this slot.
        /// </summary>
        /// <param name="box">The box item to place.</param>
        /// <returns>True if placed successfully, false if slot is already occupied.</returns>
        public bool TryPlaceBox(BoxItem box)
        {
            if (box == null || !IsEmpty)
            {
                return false;
            }

            if (!box.IsIntakeScanned)
            {
                Debug.LogWarning($"<color=yellow>[WarehouseSlot]</color> Нельзя положить заказ #{box.OrderId} в ячейку [{_slotId}]: посылка не принята сканером!");
                return false;
            }

            _currentBox = box;
            _currentBox.SetSlot(_slotId);
            _currentBox.PlaceAt(SnapPoint.position, SnapPoint.rotation);

            OnSlotOccupancyChanged?.Invoke(this, _currentBox);
            return true;
        }

        /// <summary>
        /// Clears the slot occupancy (e.g. when the player takes the box).
        /// </summary>
        /// <returns>The removed box item.</returns>
        public BoxItem RemoveBox()
        {
            if (IsEmpty)
            {
                return null;
            }

            BoxItem removed = _currentBox;
            _currentBox = null;
            removed.ClearSlot();

            OnSlotOccupancyChanged?.Invoke(this, null);
            return removed;
        }

        /// <summary>
        /// Forcefully clears and destroys any box inside this slot (used to ensure clean game start).
        /// </summary>
        public void ClearSlot()
        {
            if (_currentBox != null)
            {
                Destroy(_currentBox.gameObject);
                _currentBox = null;
                OnSlotOccupancyChanged?.Invoke(this, null);
            }
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

                string text = $"🎯 ЯЧЕЙКА [{_slotId}] (Заказ #{(_currentBox != null ? _currentBox.OrderId : 0)})";
                GUI.Box(new Rect(x - 120, y - 20, 240, 32), text, beaconStyle);
            }
        }
    }
}
