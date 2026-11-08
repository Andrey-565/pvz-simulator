using System.Collections.Generic;
using UnityEngine;

namespace PVZ.Warehouse
{
    /// <summary>
    /// Represents a multi-tier warehouse shelving rack holding multiple slots.
    /// </summary>
    public class WarehouseShelf : MonoBehaviour
    {
        [Header("Shelf Settings")]
        [Tooltip("Letter prefix for this rack (e.g. 'A', 'B').")]
        [SerializeField] private string _shelfLetter = "A";

        [Tooltip("List of storage slots belonging to this shelf.")]
        [SerializeField] private List<WarehouseSlot> _slots = new List<WarehouseSlot>();

        /// <summary>
        /// Gets the shelf code letter.
        /// </summary>
        public string ShelfLetter => _shelfLetter;

        /// <summary>
        /// Gets the read-only collection of slots on this shelf.
        /// </summary>
        public IReadOnlyList<WarehouseSlot> Slots => _slots;

        private void Awake()
        {
            if (_slots.Count == 0)
            {
                _slots.AddRange(GetComponentsInChildren<WarehouseSlot>());
            }
        }

        /// <summary>
        /// Automatically indexes and names all child slots with the shelf prefix.
        /// </summary>
        /// <param name="letter">Shelf identifier letter.</param>
        public void ConfigureSlots(string letter)
        {
            _shelfLetter = letter;
            _slots.Clear();
            _slots.AddRange(GetComponentsInChildren<WarehouseSlot>());

            for (int i = 0; i < _slots.Count; i++)
            {
                string slotCode = $"{_shelfLetter}{i + 1}";
                _slots[i].Initialize(slotCode);
                _slots[i].gameObject.name = $"Slot_{slotCode}";
            }
        }
    }
}
