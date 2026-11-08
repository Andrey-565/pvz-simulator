using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using PVZ.Items;
using PVZ.Warehouse;
using PVZ.Economy;

namespace PVZ.Customers
{
    /// <summary>
    /// Manages customer arrivals and queue progression in front of the counter.
    /// Customers only arrive if unserved parcels actually exist in the facility.
    /// When warehouse is empty, customer arrivals pause until new shipments arrive.
    /// </summary>
    public class CustomerQueueManager : MonoBehaviour
    {
        private static CustomerQueueManager _instance;

        [Header("Store State")]
        [Tooltip("True if the pickup point is open for visitors.")]
        [SerializeField] private bool _isOpen;

        [Header("Queue Settings")]
        [Tooltip("Maximum number of customers standing in line.")]
        [SerializeField] private int _maxQueueSize = 3;

        [Tooltip("Minimum delay between customer arrivals in seconds.")]
        [SerializeField] private float _minSpawnInterval = 12.0f;

        [Tooltip("Maximum delay between customer arrivals in seconds.")]
        [SerializeField] private float _maxSpawnInterval = 22.0f;

        [Header("Waypoints")]
        [Tooltip("Outdoor street sidewalk where customers spawn.")]
        [SerializeField] private Vector3 _spawnPosition = new Vector3(0f, 0f, -16f);

        [Tooltip("Outdoor street exit point where served customers leave.")]
        [SerializeField] private Vector3 _exitPosition = new Vector3(2.0f, 0f, -16f);

        [Tooltip("Queue standing positions in front of the counter (facing North).")]
        [SerializeField] private List<Vector3> _queuePositions = new List<Vector3>
        {
            new Vector3(-1.0f, 0f, -1.0f), // Slot 0: Counter service front
            new Vector3(-1.0f, 0f, -2.2f), // Slot 1: 2nd in line
            new Vector3(-1.0f, 0f, -3.4f)  // Slot 2: 3rd in line
        };

        [Header("Active Customers")]
        [SerializeField] private List<CustomerAI> _activeQueue = new List<CustomerAI>();
        [SerializeField] private float _spawnCountdown;
        private bool _outOfStockNotified;

        private string _statusBanner = string.Empty;
        private float _statusTimer;

        /// <summary>
        /// Global singleton accessor.
        /// </summary>
        public static CustomerQueueManager Instance => _instance;

        /// <summary>
        /// True if the store is open and accepting visitors.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Returns the customer currently at the front of the counter, or null if line is empty.
        /// </summary>
        public CustomerAI FirstInQueue => (_activeQueue.Count > 0) ? _activeQueue[0] : null;

        /// <summary>
        /// Number of customers currently waiting in line.
        /// </summary>
        public int QueueCount => _activeQueue.Count;

        /// <summary>
        /// Fires when the store Open/Closed status changes.
        /// </summary>
        public event Action<bool> OnStoreOpenStatusChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _spawnCountdown = 2.0f;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f)
                {
                    _statusBanner = string.Empty;
                }
            }

            if (!_isOpen)
            {
                return;
            }

            // Clean up missing references
            for (int i = _activeQueue.Count - 1; i >= 0; i--)
            {
                if (_activeQueue[i] == null)
                {
                    _activeQueue.RemoveAt(i);
                }
            }

            // Only spawn customers if line has room AND there is an unclaimed parcel in the store
            if (_activeQueue.Count < _maxQueueSize)
            {
                if (TryPickAvailableOrderId(out int nextOrderId))
                {
                    _outOfStockNotified = false;
                    _spawnCountdown -= Time.deltaTime;
                    if (_spawnCountdown <= 0f)
                    {
                        SpawnCustomerWithOrder(nextOrderId);
                        _spawnCountdown = UnityEngine.Random.Range(_minSpawnInterval, _maxSpawnInterval);
                    }
                }
                else
                {
                    // No parcels remain to be claimed!
                    if (_activeQueue.Count == 0 && !_outOfStockNotified)
                    {
                        _outOfStockNotified = true;
                        ShowBanner("📢 Все посылки со склада выданы! Оформите новую поставку на ПК.");
                    }
                    _spawnCountdown = 2.0f; // Fast arrival once new boxes arrive
                }
            }
        }

        /// <summary>
        /// Toggles store open/closed status. Opening requires at least one parcel in the facility.
        /// </summary>
        public bool ToggleOpen(out string feedback)
        {
            if (_isOpen)
            {
                _isOpen = false;
                feedback = "🚪 ПВЗ ЗАКРЫТ. Очередь не пополняется.";
                ShowBanner(feedback);
                OnStoreOpenStatusChanged?.Invoke(false);
                return true;
            }

            // Validation: must have at least one parcel in the facility
            bool hasParcels = TryPickAvailableOrderId(out _);

            if (!hasParcels)
            {
                feedback = "❌ Нельзя открыть ПВЗ: на складе нет посылок! Закажите коробки на ПК.";
                ShowBanner(feedback);
                return false;
            }

            _isOpen = true;
            _outOfStockNotified = false;
            _spawnCountdown = 2.0f; // Rapid first arrival
            feedback = "🔔 ПВЗ ОТКРЫТ! Ожидаем посетителей.";
            ShowBanner(feedback);
            OnStoreOpenStatusChanged?.Invoke(true);
            return true;
        }

        /// <summary>
        /// Spawns a customer seeking a specific available parcel.
        /// </summary>
        private void SpawnCustomerWithOrder(int targetOrderId)
        {
            GameObject customerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            customerObj.name = $"Customer_Order_{targetOrderId}";
            customerObj.transform.position = _spawnPosition;
            customerObj.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Facing North towards building entrance
            customerObj.transform.localScale = new Vector3(0.85f, 0.95f, 0.85f);

            // Add Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(customerObj.transform);
            head.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            head.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            // Distinct visitor clothing color
            Renderer rend = customerObj.GetComponent<Renderer>();
            if (rend != null)
            {
                Color[] customerColors = new Color[]
                {
                    new Color(0.2f, 0.5f, 0.9f), // Blue
                    new Color(0.9f, 0.4f, 0.2f), // Orange
                    new Color(0.3f, 0.8f, 0.4f), // Green
                    new Color(0.8f, 0.3f, 0.7f), // Purple
                    new Color(0.9f, 0.8f, 0.2f)  // Yellow
                };
                rend.material.color = customerColors[UnityEngine.Random.Range(0, customerColors.Length)];
            }

            // Navigation (only add agent if scene has baked NavMesh)
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(_spawnPosition, out navHit, 1.0f, NavMesh.AllAreas))
            {
                NavMeshAgent agent = customerObj.AddComponent<NavMeshAgent>();
                agent.height = 1.9f;
                agent.radius = 0.4f;
                agent.speed = 3.2f;
                agent.acceleration = 8.0f;
            }

            // Logic
            CustomerAI customer = customerObj.AddComponent<CustomerAI>();
            customer.Initialize(targetOrderId, _exitPosition);
            customer.OnCustomerDeparted += HandleCustomerDeparted;

            // Place in Queue
            int queueIdx = _activeQueue.Count;
            _activeQueue.Add(customer);

            Vector3 queuePos = (queueIdx < _queuePositions.Count)
                ? _queuePositions[queueIdx]
                : _queuePositions[_queuePositions.Count - 1] - (Vector3.forward * (queueIdx * 1.2f));

            customer.SetQueueSlot(queueIdx, queuePos);
        }

        /// <summary>
        /// Selects an order ID from an unserved parcel present in the warehouse or facility.
        /// Returns false if no parcels remain to be claimed.
        /// </summary>
        public bool TryPickAvailableOrderId(out int targetOrderId)
        {
            targetOrderId = -1;
            var claimedOrderIds = new HashSet<int>(_activeQueue.Select(c => c.TargetOrderId));

            // 1. Prefer parcel already resting on a warehouse shelf
            if (WarehouseManager.Instance != null)
            {
                var occupiedSlots = WarehouseManager.Instance.GetOccupiedSlots();
                foreach (var slot in occupiedSlots)
                {
                    if (slot.CurrentBox != null && !claimedOrderIds.Contains(slot.CurrentBox.OrderId))
                    {
                        targetOrderId = slot.CurrentBox.OrderId;
                        return true;
                    }
                }
            }

            // 2. Otherwise pick from any unslotted physical box in scene
            var allBoxes = FindObjectsByType<BoxItem>(FindObjectsSortMode.None);
            foreach (var box in allBoxes)
            {
                if (box != null && !claimedOrderIds.Contains(box.OrderId))
                {
                    targetOrderId = box.OrderId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Explicitly removes a customer from the queue.
        /// </summary>
        public void RemoveCustomer(CustomerAI customer)
        {
            if (customer != null && _activeQueue.Contains(customer))
            {
                HandleCustomerDeparted(customer, true);
            }
        }

        private void HandleCustomerDeparted(CustomerAI customer, bool satisfied)
        {
            customer.OnCustomerDeparted -= HandleCustomerDeparted;
            _activeQueue.Remove(customer);

            // Advance remaining customers in queue towards the counter
            for (int i = 0; i < _activeQueue.Count; i++)
            {
                Vector3 newSlotPos = (i < _queuePositions.Count)
                    ? _queuePositions[i]
                    : _queuePositions[_queuePositions.Count - 1] - (Vector3.forward * (i * 1.2f));

                _activeQueue[i].SetQueueSlot(i, newSlotPos);
            }
        }

        private void ShowBanner(string text)
        {
            _statusBanner = text;
            _statusTimer = 5.0f;
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_statusBanner) && _statusTimer > 0f)
            {
                GUIStyle bannerStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                bannerStyle.normal.textColor = Color.cyan;
                GUI.Box(new Rect((Screen.width - 550) * 0.5f, 65, 550, 32), _statusBanner, bannerStyle);
            }

            // Top Status Bar: Store status, Stock notice, Queue, and Rating
            GUIStyle statusStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            statusStyle.normal.textColor = Color.white;

            string openText = _isOpen ? "<color=#00FF00>● ОТКРЫТО</color>" : "<color=#FF4444>■ ЗАКРЫТО</color>";
            string stockNotice = string.Empty;

            if (_isOpen && !TryPickAvailableOrderId(out _) && _activeQueue.Count == 0)
            {
                stockNotice = " | <color=yellow>Склад пуст (ждем поставку)</color>";
            }

            string ratingText = (RatingManager.Instance != null) ? RatingManager.Instance.GetFormattedRating() : "2.5★";
            string barContent = $" ПВЗ: {openText}{stockNotice} | Очередь: {_activeQueue.Count}/{_maxQueueSize} | Рейтинг: {ratingText}";

            GUI.Box(new Rect(15, 15, 420, 26), barContent, statusStyle);
        }
    }
}
