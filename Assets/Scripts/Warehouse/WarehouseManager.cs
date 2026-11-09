using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PVZ.Items;

namespace PVZ.Warehouse
{
    /// <summary>
    /// Central warehouse registry tracking all shelves, slots, and parcel placements.
    /// Provides search by order ID, slot code, and occupancy metrics.
    /// </summary>
    public class WarehouseManager : MonoBehaviour
    {
        private static WarehouseManager _instance;

        [Header("Runtime Registry")]
        [SerializeField] private List<WarehouseSlot> _registeredSlots = new List<WarehouseSlot>();

        [Header("Initialization")]
        [Tooltip("Guarantees the game starts with 0 boxes on shelves by clearing any scene-saved starter boxes.")]
        [SerializeField] private bool _startWithZeroBoxes = true;

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static WarehouseManager Instance => _instance;

        /// <summary>
        /// Fires whenever any slot's contents change (box placed or removed).
        /// </summary>
        public event Action OnWarehouseInventoryChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Auto-gather existing scene slots
            var sceneSlots = FindObjectsByType<WarehouseSlot>(FindObjectsSortMode.None);
            foreach (var slot in sceneSlots)
            {
                RegisterSlot(slot);
            }

            if (_startWithZeroBoxes)
            {
                ClearAllStarterBoxes();
            }
        }

        private void ClearAllStarterBoxes()
        {
            foreach (var slot in _registeredSlots)
            {
                slot.ClearSlot();
            }

            var looseBoxes = FindObjectsByType<BoxItem>(FindObjectsSortMode.None);
            foreach (var box in looseBoxes)
            {
                if (box != null && (box.transform.parent == null || !box.transform.parent.name.Contains("Truck")))
                {
                    Destroy(box.gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Registers a warehouse slot with the inventory tracking system.
        /// </summary>
        /// <param name="slot">The slot to track.</param>
        public void RegisterSlot(WarehouseSlot slot)
        {
            if (slot == null || _registeredSlots.Contains(slot))
            {
                return;
            }

            _registeredSlots.Add(slot);
            slot.OnSlotOccupancyChanged += HandleSlotOccupancyChanged;
        }

        /// <summary>
        /// Unregisters a slot from inventory tracking.
        /// </summary>
        /// <param name="slot">The slot to remove.</param>
        public void UnregisterSlot(WarehouseSlot slot)
        {
            if (slot == null || !_registeredSlots.Contains(slot))
            {
                return;
            }

            slot.OnSlotOccupancyChanged -= HandleSlotOccupancyChanged;
            _registeredSlots.Remove(slot);
        }

        private void HandleSlotOccupancyChanged(WarehouseSlot slot, BoxItem box)
        {
            OnWarehouseInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Finds a specific warehouse slot by its code identifier.
        /// </summary>
        /// <param name="slotId">Code such as A1, B2.</param>
        /// <returns>Matching slot or null if not found.</returns>
        public WarehouseSlot GetSlot(string slotId)
        {
            return _registeredSlots.FirstOrDefault(s => string.Equals(s.SlotId, slotId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds the warehouse slot containing a specific customer order.
        /// </summary>
        /// <param name="orderId">Order tracking number.</param>
        /// <returns>Warehouse slot holding the order, or null if not slotted.</returns>
        public WarehouseSlot FindSlotByOrderId(int orderId)
        {
            return _registeredSlots.FirstOrDefault(s => !s.IsEmpty && s.CurrentBox.OrderId == orderId);
        }

        /// <summary>
        /// Returns all slots that currently do not hold a box.
        /// </summary>
        public List<WarehouseSlot> GetEmptySlots()
        {
            return _registeredSlots.Where(s => s.IsEmpty).ToList();
        }

        /// <summary>
        /// Returns all slots currently holding parcels.
        /// </summary>
        public List<WarehouseSlot> GetOccupiedSlots()
        {
            return _registeredSlots.Where(s => !s.IsEmpty).ToList();
        }

        /// <summary>
        /// Returns all registered warehouse slots.
        /// </summary>
        public IReadOnlyList<WarehouseSlot> GetAllSlots()
        {
            return _registeredSlots;
        }
    }
}
