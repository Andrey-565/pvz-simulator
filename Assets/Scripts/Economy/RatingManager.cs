using System;
using UnityEngine;

namespace PVZ.Economy
{
    /// <summary>
    /// Tracks customer review points (+1 for speed, 0 neutral, -1, -3, -5 for delays),
    /// maintains store star rating (0.5 to 5.0 stars), and broadcasts updates to UI and game systems.
    /// </summary>
    public class RatingManager : MonoBehaviour
    {
        private static RatingManager _instance;

        [Header("Rating Parameters")]
        [Tooltip("Starting star rating.")]
        [SerializeField] private float _currentStarRating = 2.5f;

        [Tooltip("Starting rating points score.")]
        [SerializeField] private int _ratingPoints = 10;

        [Header("Runtime Metrics")]
        [SerializeField] private int _totalOrdersHandled;
        [SerializeField] private int _fastDeliveriesCount; // <= 15s (+1)
        [SerializeField] private int _delayedDeliveriesCount; // > 45s (-1, -3, -5)

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static RatingManager Instance => _instance;

        /// <summary>
        /// Gets current average store star rating (clamped 0.5 to 5.0).
        /// </summary>
        public float CurrentRating => Mathf.Clamp((float)Math.Round(_currentStarRating, 1), 0.5f, 5.0f);

        /// <summary>
        /// Gets cumulative rating points.
        /// </summary>
        public int RatingPoints => _ratingPoints;

        /// <summary>
        /// Total orders fulfilled.
        /// </summary>
        public int TotalOrdersHandled => _totalOrdersHandled;

        /// <summary>
        /// Fires when the store star rating changes.
        /// </summary>
        public event Action<float> OnRatingChanged;

        /// <summary>
        /// Fires when rating points update.
        /// </summary>
        public event Action<int, int> OnPointsChanged; // (int currentPoints, int delta)

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
        /// Applies rating delta based on delivery speed:
        /// +1 (<=15s), 0 (15-45s), -1 (45-60s), -3 (60-120s), -5 (>120s).
        /// </summary>
        /// <param name="delta">Score change.</param>
        /// <param name="feedback">Log or dialog explanation.</param>
        public void ApplyRatingDelta(int delta, string feedback = "")
        {
            _ratingPoints += delta;
            _totalOrdersHandled++;

            if (delta > 0)
            {
                _fastDeliveriesCount++;
                _currentStarRating = Mathf.Clamp(_currentStarRating + 0.3f, 0.5f, 5.0f);
            }
            else if (delta < 0)
            {
                _delayedDeliveriesCount++;
                // Scale star penalty with point loss
                float starPenalty = Mathf.Abs(delta) * 0.25f;
                _currentStarRating = Mathf.Clamp(_currentStarRating - starPenalty, 0.5f, 5.0f);
            }

            OnPointsChanged?.Invoke(_ratingPoints, delta);
            OnRatingChanged?.Invoke(CurrentRating);

            string deltaStr = delta >= 0 ? $"+{delta}" : $"{delta}";
            Debug.Log($"<color=yellow>[RatingManager]</color> Очки рейтинга: {deltaStr} ({feedback}) | Всего очков: {_ratingPoints} | Звезды: {CurrentRating:F1}★");
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
            ApplyRatingDelta(delta, feedback);
        }

        /// <summary>
        /// Formats current star rating and points for HUD.
        /// </summary>
        public string GetFormattedRating()
        {
            float stars = CurrentRating;
            int starCount = Mathf.RoundToInt(stars);
            string starStr = string.Empty;

            for (int i = 1; i <= 5; i++)
            {
                starStr += (i <= starCount) ? "★" : "☆";
            }

            string pointSign = _ratingPoints >= 0 ? $"+{_ratingPoints}" : $"{_ratingPoints}";
            return $"{starStr} {stars:F1} (Очки: {pointSign})";
        }
    }
}
