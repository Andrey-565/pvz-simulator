using System;
using System.Collections.Generic;
using UnityEngine;
using PVZ.Computer.UI;

namespace PVZ.Delivery
{
    /// <summary>
    /// Coordinates parcel delivery operations: tracks 20s countdown after PC orders,
    /// manages truck spawn outside the front entrance, and communicates delivery status to PC, HUD,
    /// and renders Steam achievement notifications.
    /// </summary>
    public class DeliveryManager : MonoBehaviour
    {
        private static DeliveryManager _instance;

        [Header("Components")]
        [Tooltip("The delivery truck instance on the street.")]
        [SerializeField] private DeliveryTruck _truck;

        [Header("Settings")]
        [Tooltip("Delivery delay in seconds before truck arrives outside.")]
        [SerializeField] private float _deliveryDuration = 20.0f;

        [Header("State")]
        [SerializeField] private bool _isDeliveryPending;
        [SerializeField] private float _currentCountdown;
        [SerializeField] private int _nextOrderId = 201;
        [SerializeField] private List<int> _pendingOrderBatch = new List<int>();
        private string _statusNotification = string.Empty;
        private float _notificationTimer = 0f;

        [Header("Steam Achievement State")]
        private bool _showAchievement;
        private float _achievementTimer;
        private string _achTitle = "«Безбилетник»";
        private string _achDesc = "Вы случайно уехали на склад в кузове фуры";

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static DeliveryManager Instance => _instance;

        /// <summary>
        /// Reference to the delivery truck.
        /// </summary>
        public DeliveryTruck Truck => _truck;

        /// <summary>
        /// True if a shipment has been ordered on PC and is on its way.
        /// </summary>
        public bool IsDeliveryPending => _isDeliveryPending;

        /// <summary>
        /// True if a delivery is currently pending or the truck is actively delivering, parked, or departing.
        /// New orders are blocked while this is true.
        /// </summary>
        public bool IsDeliveryActive => _isDeliveryPending || (_truck != null && _truck.IsTruckBusy);

        /// <summary>
        /// Remaining seconds until the delivery truck arrives.
        /// </summary>
        public float RemainingCountdown => _currentCountdown;

        /// <summary>
        /// Total boxes in the current in-transit or arrived shipment.
        /// </summary>
        public int PendingBatchCount => _pendingOrderBatch.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (_truck == null)
            {
                _truck = FindFirstObjectByType<DeliveryTruck>();
            }
        }

        private void Start()
        {
            // The street outside is empty until a delivery is ordered
            if (_truck != null && !_isDeliveryPending)
            {
                _truck.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            ComputerUI.OnSupplyBatchOrdered += HandleSupplyBatchOrdered;

            if (_truck != null)
            {
                _truck.OnTruckArrived += HandleTruckArrived;
                _truck.OnTruckFullyUnloaded += HandleTruckFullyUnloaded;
                _truck.OnTruckDeparted += HandleTruckDeparted;
            }
        }

        private void OnDisable()
        {
            ComputerUI.OnSupplyBatchOrdered -= HandleSupplyBatchOrdered;

            if (_truck != null)
            {
                _truck.OnTruckArrived -= HandleTruckArrived;
                _truck.OnTruckFullyUnloaded -= HandleTruckFullyUnloaded;
                _truck.OnTruckDeparted -= HandleTruckDeparted;
            }
        }

        private void Update()
        {
            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
                if (_notificationTimer <= 0f)
                {
                    _statusNotification = string.Empty;
                }
            }

            if (_achievementTimer > 0f)
            {
                _achievementTimer -= Time.deltaTime;
                if (_achievementTimer <= 0f)
                {
                    _showAchievement = false;
                }
            }

            if (!_isDeliveryPending)
            {
                return;
            }

            _currentCountdown -= Time.deltaTime;

            if (_currentCountdown <= 0f)
            {
                _isDeliveryPending = false;
                _currentCountdown = 0f;

                if (_truck != null)
                {
                    _truck.gameObject.SetActive(true);
                    _truck.ArriveWithCargo(_pendingOrderBatch);
                }
            }
        }

        /// <summary>
        /// Called when the player orders a new shipment from the PC terminal.
        /// </summary>
        private void HandleSupplyBatchOrdered(int boxCount)
        {
            if (IsDeliveryActive)
            {
                Debug.LogWarning("[DeliveryManager] Cannot order new supply: delivery is already active!");
                return;
            }

            _pendingOrderBatch.Clear();
            for (int i = 0; i < boxCount; i++)
            {
                _pendingOrderBatch.Add(_nextOrderId++);
            }

            _currentCountdown = _deliveryDuration;
            _isDeliveryPending = true;
            ShowNotification($"🚛 Заказ оформлен! Фура с {boxCount} посылками прибудет к входу через 20 сек.", 5f);
        }

        private void HandleTruckArrived()
        {
            ShowNotification("🚛 Поставка прибыла! Фура стоит на улице у входа, кузов открыт.", 6f);
        }

        private void HandleTruckFullyUnloaded()
        {
            ShowNotification("✔ Партия полностью выгружена!", 3f);
        }

        private void HandleTruckDeparted()
        {
            _statusNotification = string.Empty;
        }

        /// <summary>
        /// Displays on-screen banner notification.
        /// </summary>
        public void ShowNotification(string text, float duration)
        {
            _statusNotification = text;
            _notificationTimer = duration;
        }

        /// <summary>
        /// Triggers Steam achievement unlock popup with sound chime.
        /// </summary>
        public void TriggerSteamAchievement(string title, string description)
        {
            _achTitle = title;
            _achDesc = description;
            _showAchievement = true;
            _achievementTimer = 8.0f;
            PlayAchievementChime();
        }

        private void PlayAchievementChime()
        {
            AudioSource audio = GetComponent<AudioSource>();
            if (audio == null)
            {
                audio = gameObject.AddComponent<AudioSource>();
            }

            int sampleRate = 44100;
            int length = sampleRate / 2; // 0.5s
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.2f ? 587.33f : 880.0f; // D5 -> A5 bright ding
                float env = Mathf.Clamp01(1f - (t / 0.5f));
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create("SteamAchChime", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            audio.PlayOneShot(clip);
        }

        private void OnGUI()
        {
            // 1. Truck status during unloading
            if (_truck != null && _truck.IsArrived)
            {
                GUIStyle truckStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                truckStyle.normal.textColor = Color.yellow;

                string text = $"📦 Разгрузка фуры у входа: осталось забрать {_truck.RemainingBoxCount} коробок";
                GUI.Box(new Rect((Screen.width - 480) / 2, 45, 480, 30), text, truckStyle);
            }
            else if (!string.IsNullOrEmpty(_statusNotification) && _notificationTimer > 0f)
            {
                GUIStyle noteStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                noteStyle.normal.textColor = Color.green;
                GUI.Box(new Rect((Screen.width - 560) / 2, 45, 560, 30), _statusNotification, noteStyle);
            }

            // 2. Steam Achievement Popup in bottom-right corner
            if (_showAchievement && _achievementTimer > 0f)
            {
                float achW = 340f;
                float achH = 80f;
                float achX = Screen.width - achW - 24f;
                float achY = Screen.height - achH - 24f;

                // Dark Steam overlay background
                GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
                GUI.Box(new Rect(achX, achY, achW, achH), GUIContent.none, bgStyle);

                // Trophy Icon
                GUIStyle iconStyle = new GUIStyle
                {
                    fontSize = 34,
                    alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(achX + 8, achY + 12, 54, 54), "🏆", iconStyle);

                // Header
                GUIStyle headerStyle = new GUIStyle
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold
                };
                headerStyle.normal.textColor = new Color(0.40f, 0.75f, 0.98f); // Steam Cyan
                GUI.Label(new Rect(achX + 68, achY + 10, 260, 16), "ДОСТИЖЕНИЕ РАЗБЛОКИРОВАНО", headerStyle);

                // Achievement Name
                GUIStyle nameStyle = new GUIStyle
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                nameStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(achX + 68, achY + 28, 260, 22), _achTitle, nameStyle);

                // Description
                GUIStyle descStyle = new GUIStyle
                {
                    fontSize = 11
                };
                descStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(achX + 68, achY + 50, 260, 20), _achDesc, descStyle);
            }
        }
    }
}
