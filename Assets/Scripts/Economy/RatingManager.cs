using System;
using UnityEngine;

namespace PVZ.Economy
{
    /// <summary>
    /// Tracks customer review ratings (1 to 5 stars per order based on delivery speed),
    /// requires a threshold of 50 fulfilled orders before the store rating is unlocked,
    /// and calculates the average rating across all fulfilled orders.
    /// </summary>
    public class RatingManager : MonoBehaviour
    {
        private static RatingManager _instance;

        [Header("Rating Threshold")]
        [Tooltip("Minimum fulfilled orders required before store rating is officially published.")]
        [SerializeField] private int _minOrdersForRating = 50;

        [Header("Runtime Metrics")]
        [SerializeField] private int _totalOrdersHandled = 0;
        [SerializeField] private int _totalStarsSum = 0;
        [SerializeField] private int _ratingPoints = 0;
        [SerializeField] private int _fastDeliveriesCount = 0; // <= 15s (5★)
        [SerializeField] private int _delayedDeliveriesCount = 0; // > 45s (<= 3★)

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static RatingManager Instance => _instance;

        /// <summary>
        /// Number of fulfilled orders needed to establish the first rating.
        /// </summary>
        public int MinOrdersForRating => _minOrdersForRating;

        /// <summary>
        /// Total orders fulfilled so far.
        /// </summary>
        public int TotalOrdersHandled => _totalOrdersHandled;

        /// <summary>
        /// Cumulative sum of star ratings given by customers.
        /// </summary>
        public int TotalStarsSum => _totalStarsSum;

        /// <summary>
        /// True if the store has completed at least 50 orders and now has an active star rating.
        /// </summary>
        public bool HasRating => _totalOrdersHandled >= _minOrdersForRating;

        /// <summary>
        /// Gets current average store star rating (clamped 1.0 to 5.0).
        /// Returns 0 if under 50 orders have been fulfilled.
        /// </summary>
        public float CurrentRating
        {
            get
            {
                if (!HasRating || _totalOrdersHandled == 0)
                {
                    return 0f;
                }
                float avg = (float)_totalStarsSum / _totalOrdersHandled;
                return Mathf.Clamp((float)Math.Round(avg, 1), 1.0f, 5.0f);
            }
        }

        /// <summary>
        /// Returns the revenue multiplier based on the store's current star rating:
        /// While under 50 orders: 1.0x (neutral until rating is established).
        /// When rating is active: 5★ = 1.2x, 4★ = 1.0x, 3★ = 1.0x, 2★ = 0.9x, 1★ = 0.8x.
        /// </summary>
        public float CurrentMoneyMultiplier
        {
            get
            {
                if (!HasRating)
                {
                    return 1.0f;
                }

                int stars = Mathf.RoundToInt(CurrentRating);
                return stars switch
                {
                    >= 5 => 1.2f,
                    4 => 1.0f,
                    3 => 1.0f,
                    2 => 0.9f,
                    _ => 0.8f // 1 star or below
                };
            }
        }

        /// <summary>
        /// Gets cumulative rating points.
        /// </summary>
        public int RatingPoints => _ratingPoints;

        /// <summary>
        /// Fires when the store star rating changes.
        /// </summary>
        public event Action<float> OnRatingChanged;

        /// <summary>
        /// Fires when rating points update.
        /// </summary>
        public event Action<int, int> OnPointsChanged; // (int currentPoints, int delta)

        /// <summary>
        /// Fires when the store reaches 50 orders and unlocks its first official rating.
        /// </summary>
        public event Action<float> OnRatingUnlocked;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Records an order review with individual order stars (1-5★) and reputation point delta.
        /// </summary>
        /// <param name="orderStars">Customer satisfaction stars for this specific delivery (1-5★).</param>
        /// <param name="delta">Score change.</param>
        /// <param name="feedback">Log or dialog explanation.</param>
        public void RecordOrder(int orderStars, int delta, string feedback = "")
        {
            bool hadRatingBefore = HasRating;

            _totalOrdersHandled++;
            _totalStarsSum += Mathf.Clamp(orderStars, 1, 5);
            _ratingPoints += delta;

            if (delta > 0)
            {
                _fastDeliveriesCount++;
            }
            else if (delta < 0)
            {
                _delayedDeliveriesCount++;
            }

            OnPointsChanged?.Invoke(_ratingPoints, delta);

            if (!hadRatingBefore && HasRating)
            {
                Debug.Log($"<color=green>[RatingManager]</color> 🎉 ПВЗ достиг {_minOrdersForRating} выданных заказов! Официальный рейтинг открыт: {CurrentRating:F1}★!");
                OnRatingUnlocked?.Invoke(CurrentRating);
            }

            if (HasRating)
            {
                OnRatingChanged?.Invoke(CurrentRating);
            }

            string deltaStr = delta >= 0 ? $"+{delta}" : $"{delta}";
            string ratingDisplay = HasRating ? $"{CurrentRating:F1}★" : $"Новый ПВЗ ({_totalOrdersHandled}/{_minOrdersForRating})";
            Debug.Log($"<color=yellow>[RatingManager]</color> Заказ #{_totalOrdersHandled}: {orderStars}★ | Очки: {deltaStr} ({feedback}) | Рейтинг ПВЗ: {ratingDisplay}");
        }

        /// <summary>
        /// Backward-compatible overload for recording rating deltas.
        /// Infers customer review stars from delivery speed delta:
        /// +1 (<=15s) -> 5★
        /// 0 (15-45s) -> 4★
        /// -1 (45-60s) -> 3★
        /// -3 (60-120s) -> 2★
        /// -5 (>120s) -> 1★
        /// </summary>
        public void ApplyRatingDelta(int delta, string feedback = "")
        {
            int orderStars = delta switch
            {
                > 0 => 5,   // Fast delivery (<= 15s)
                0 => 4,     // Standard delivery (15-45s)
                -1 => 3,    // Slight delay (45-60s)
                -3 => 2,    // Long delay (60-120s)
                _ => 1      // Critical delay (> 120s)
            };
            RecordOrder(orderStars, delta, feedback);
        }

        /// <summary>
        /// Legacy fallback for stars adding.
        /// </summary>
        public void AddRating(int stars, string feedback = "")
        {
            int delta = stars switch
            {
                5 => 1,
                4 => 0,
                3 => -1,
                2 => -3,
                _ => -5
            };
            RecordOrder(stars, delta, feedback);
        }

        /// <summary>
        /// Formats current star rating and points for HUD.
        /// </summary>
        public string GetFormattedRating()
        {
            if (!HasRating)
            {
                return $"Новый ПВЗ ({_totalOrdersHandled}/{_minOrdersForRating})";
            }

            float stars = CurrentRating;
            int starCount = Mathf.RoundToInt(stars);
            string starStr = string.Empty;

            for (int i = 1; i <= 5; i++)
            {
                starStr += (i <= starCount) ? "★" : "☆";
            }

            string pointSign = _ratingPoints >= 0 ? $"+{_ratingPoints}" : $"{_ratingPoints}";
            string multStr = $" | x{CurrentMoneyMultiplier:0.#} ₽";
            return $"{starStr} {stars:F1} ({_totalOrdersHandled} отз.{multStr})";
        }
    }
}
