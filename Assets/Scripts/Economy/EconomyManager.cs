using System;
using UnityEngine;

namespace PVZ.Economy
{
    /// <summary>
    /// Tracks player cash balance, revenue from completed orders, and supply purchase expenses.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        private static EconomyManager _instance;

        [Header("Starting Economy")]
        [Tooltip("Initial cash funds available to the player.")]
        [SerializeField] private int _startingBalance = 500;

        [Header("Runtime Balance")]
        [SerializeField] private int _currentBalance;

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static EconomyManager Instance => _instance;

        /// <summary>
        /// Gets the current cash balance.
        /// </summary>
        public int CurrentBalance => _currentBalance;

        /// <summary>
        /// Fires when the account balance changes, passing the new amount.
        /// </summary>
        public event Action<int> OnBalanceChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _currentBalance = _startingBalance;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Checks if player has at least the required funds.
        /// </summary>
        public bool CanAfford(int amount)
        {
            return _currentBalance >= amount;
        }

        /// <summary>
        /// Deducts money from player balance if sufficient funds exist.
        /// </summary>
        /// <param name="amount">Amount to spend.</param>
        /// <returns>True if deduction succeeded, false if not enough cash.</returns>
        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (!CanAfford(amount))
            {
                return false;
            }

            _currentBalance -= amount;
            OnBalanceChanged?.Invoke(_currentBalance);
            return true;
        }

        /// <summary>
        /// Adds earned money to the account balance.
        /// </summary>
        /// <param name="amount">Earnings to credit.</param>
        public void AddMoney(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _currentBalance += amount;
            OnBalanceChanged?.Invoke(_currentBalance);
        }
    }
}
