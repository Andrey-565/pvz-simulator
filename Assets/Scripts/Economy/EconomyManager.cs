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
        [Tooltip("Initial cash funds available to the player in rubles.")]
        [SerializeField] private float _startingBalance = 0f;

        [Header("Runtime Balance")]
        [SerializeField] private float _currentBalance;

        /// <summary>
        /// Currency symbol for Russian rubles.
        /// </summary>
        public const string CurrencySymbol = "₽";

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static EconomyManager Instance => _instance;

        /// <summary>
        /// Gets the current cash balance in rubles.
        /// </summary>
        public float CurrentBalance => _currentBalance;

        /// <summary>
        /// Formats an amount into rubles format (e.g. "1.32 ₽" or "0.00 ₽").
        /// </summary>
        public static string FormatMoney(float amount)
        {
            return $"{amount:F2} ₽";
        }

        /// <summary>
        /// Fires when the account balance changes, passing the new amount.
        /// </summary>
        public event Action<float> OnBalanceChanged;

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
        public bool CanAfford(float amount)
        {
            return _currentBalance >= amount - 0.001f;
        }

        /// <summary>
        /// Deducts money from player balance if sufficient funds exist.
        /// </summary>
        /// <param name="amount">Amount to spend.</param>
        /// <returns>True if deduction succeeded, false if not enough cash.</returns>
        public bool TrySpend(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (!CanAfford(amount))
            {
                return false;
            }

            _currentBalance = Mathf.Max(0f, _currentBalance - amount);
            _currentBalance = (float)Math.Round(_currentBalance, 2);
            OnBalanceChanged?.Invoke(_currentBalance);
            return true;
        }

        /// <summary>
        /// Adds earned money to the account balance.
        /// </summary>
        /// <param name="amount">Earnings to credit.</param>
        public void AddMoney(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentBalance += amount;
            _currentBalance = (float)Math.Round(_currentBalance, 2);
            OnBalanceChanged?.Invoke(_currentBalance);
        }

        private void OnGUI()
        {
            DrawMoneyHUD();
        }

        /// <summary>
        /// Displays cash funds in the bottom-right corner of the player HUD.
        /// </summary>
        private void DrawMoneyHUD()
        {
            float width = 180f;
            float height = 40f;
            float x = Screen.width - width - 20f;
            float y = Screen.height - height - 20f;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            boxStyle.normal.textColor = Color.white;

            string moneyText = $"<size=16>💰</size> <color=#00FF66>{FormatMoney(_currentBalance)}</color>";
            GUI.Box(new Rect(x, y, width, height), moneyText, boxStyle);
        }
    }
}
