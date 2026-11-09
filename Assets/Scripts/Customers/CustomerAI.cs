using System;
using UnityEngine;
using UnityEngine.AI;
using PVZ.Items;
using PVZ.Economy;
using PVZ.Utils;

namespace PVZ.Customers
{
    /// <summary>
    /// Lifecycle states for customer navigation and counter queue interaction.
    /// </summary>
    public enum CustomerState
    {
        Spawning,
        MovingToQueue,
        WaitingInQueue,
        SatisfiedExit,
        AngryExit
    }

    /// <summary>
    /// Controls NPC customer behavior: queuing up in front of the counter,
    /// waiting for their order barcode to be scanned, tracking elapsed service time,
    /// evaluating rating delta (+1, 0, -1, -3, -5), and departing after parcel handover.
    /// Customers remain waiting until served.
    /// </summary>
    public class CustomerAI : MonoBehaviour
    {
        [Header("Customer Properties")]
        [Tooltip("The unique order number this customer is picking up (front/first item).")]
        [SerializeField] private int _targetOrderId;

        [Tooltip("The complete list of product order IDs this customer is requesting (1 to 4 items).")]
        [SerializeField] private System.Collections.Generic.List<int> _requestedOrderIds = new System.Collections.Generic.List<int>();

        [Tooltip("Product order IDs that still need to be delivered to this customer.")]
        [SerializeField] private System.Collections.Generic.List<int> _remainingOrderIds = new System.Collections.Generic.List<int>();

        [Tooltip("Product order IDs that have already been handed over.")]
        [SerializeField] private System.Collections.Generic.List<int> _receivedOrderIds = new System.Collections.Generic.List<int>();

        [Tooltip("Walking speed towards queue or exit.")]
        [SerializeField] private float _walkSpeed = 2.4f;

        [Header("Runtime State")]
        [SerializeField] private CustomerState _state = CustomerState.Spawning;
        [SerializeField] private int _queueIndex = -1;
        [SerializeField] private bool _isScanned;
        [SerializeField] private float _elapsedTimeAfterScan;

        [Header("Speech / Dialog")]
        [SerializeField] private string _speechMessage = string.Empty;
        [SerializeField] private float _speechTimer;
        [SerializeField] private bool _isAngrySpeech;

        private NavMeshAgent _navAgent;
        private Vector3 _targetPosition;
        private Vector3 _exitPoint;

        /// <summary>
        /// Gets the current or front order ID this customer is requesting.
        /// </summary>
        public int TargetOrderId => (_remainingOrderIds.Count > 0) ? _remainingOrderIds[0] : _targetOrderId;

        /// <summary>
        /// Read-only list of all order IDs originally requested by this customer.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<int> RequestedOrderIds => _requestedOrderIds;

        /// <summary>
        /// Read-only list of order IDs still awaiting delivery.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<int> RemainingOrderIds => _remainingOrderIds;

        /// <summary>
        /// Total number of product items requested in this customer order (1 to 4).
        /// </summary>
        public int TotalRequestedCount => (_requestedOrderIds.Count > 0) ? _requestedOrderIds.Count : 1;

        /// <summary>
        /// Number of products successfully handed over so far.
        /// </summary>
        public int ServedCount => _receivedOrderIds.Count;

        /// <summary>
        /// True once the customer has received all requested products.
        /// </summary>
        public bool IsFullyServed => _remainingOrderIds.Count == 0 && _requestedOrderIds.Count > 0;

        /// <summary>
        /// True once the customer has been scanned with the barcode scanner.
        /// </summary>
        public bool IsScanned => _isScanned;

        /// <summary>
        /// Current position index in the counter queue (0 = front customer).
        /// </summary>
        public int QueueIndex => _queueIndex;

        /// <summary>
        /// Fires when the customer is served or leaves the queue line.
        /// Param 1: customer instance, Param 2: true if satisfied.
        /// </summary>
        public event Action<CustomerAI, bool> OnCustomerDeparted;

        /// <summary>
        /// Elapsed time since scan in seconds.
        /// </summary>
        public float ElapsedTimeAfterScan => _elapsedTimeAfterScan;

        /// <summary>
        /// Current behavioral state.
        /// </summary>
        public CustomerState State => _state;

        private Transform _armPivot;
        private Renderer _phoneScreenRenderer;
        private float _screenFlashTimer;

        /// <summary>
        /// True if customer is standing in front of the counter ready to be served.
        /// </summary>
        public bool IsAtCounter => _queueIndex == 0 && _state == CustomerState.WaitingInQueue;

        private void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _isScanned = false;
            _elapsedTimeAfterScan = 0f;
            BuildArmAndPhone();
        }

        private void BuildArmAndPhone()
        {
            if (transform.Find("ArmPivot") != null)
            {
                return;
            }

            GameObject pivot = new GameObject("ArmPivot");
            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = new Vector3(0.42f, 0.40f, 0.05f);
            pivot.transform.localRotation = Quaternion.identity;
            _armPivot = pivot.transform;

            // Arm limb
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "ArmLimb";
            arm.transform.SetParent(pivot.transform);
            arm.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            arm.transform.localScale = new Vector3(0.12f, 0.40f, 0.12f);
            Renderer bodyRend = GetComponent<Renderer>();
            if (bodyRend != null)
            {
                arm.GetComponent<Renderer>().material = bodyRend.material;
            }
            Collider armCol = arm.GetComponent<Collider>();
            if (armCol != null)
            {
                DestroyImmediate(armCol);
            }

            // Smartphone body in hand
            GameObject phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            phone.name = "Smartphone";
            phone.transform.SetParent(pivot.transform);
            phone.transform.localPosition = new Vector3(0f, -0.42f, 0.08f);
            phone.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            phone.transform.localScale = new Vector3(0.10f, 0.018f, 0.18f);

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            phone.GetComponent<Renderer>().material = new Material(litShader) { color = new Color(0.1f, 0.1f, 0.12f) };

            // Smartphone screen (Barcode screen glowing white/cyan)
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "PhoneScreen";
            screen.transform.SetParent(phone.transform);
            screen.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            screen.transform.localScale = new Vector3(0.88f, 0.2f, 0.88f);

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
            _phoneScreenRenderer = screen.GetComponent<Renderer>();
            _phoneScreenRenderer.material = new Material(unlitShader) { color = new Color(0.9f, 0.95f, 1.0f) };
        }

        private void UpdateArmAndPhonePose()
        {
            if (_armPivot == null)
            {
                return;
            }

            Quaternion targetRot;
            if (IsAtCounter && !_isScanned)
            {
                // Extended forward across the counter holding phone towards operator!
                targetRot = Quaternion.Euler(-80f, -12f, 0f);
            }
            else if (IsAtCounter && _isScanned)
            {
                // Lowered comfortably after code is scanned
                targetRot = Quaternion.Euler(-25f, -5f, 0f);
            }
            else
            {
                // Relaxed at side while moving or in back queue
                targetRot = Quaternion.Euler(0f, 0f, 0f);
            }

            _armPivot.localRotation = Quaternion.Slerp(_armPivot.localRotation, targetRot, Time.deltaTime * 6f);

            // Screen flash effect on scan
            if (_screenFlashTimer > 0f)
            {
                _screenFlashTimer -= Time.deltaTime;
                if (_screenFlashTimer <= 0f && _phoneScreenRenderer != null)
                {
                    _phoneScreenRenderer.material.color = new Color(0.3f, 0.85f, 0.35f); // Green confirmed screen
                }
            }
        }

        /// <summary>
        /// Initializes the customer with a list of requested product IDs (1 to 4 products) and exit destination.
        /// </summary>
        public void Initialize(System.Collections.Generic.List<int> orderIds, Vector3 exitPoint)
        {
            _requestedOrderIds = (orderIds != null && orderIds.Count > 0)
                ? new System.Collections.Generic.List<int>(orderIds)
                : new System.Collections.Generic.List<int> { 101 };
            _remainingOrderIds = new System.Collections.Generic.List<int>(_requestedOrderIds);
            _receivedOrderIds = new System.Collections.Generic.List<int>();
            _targetOrderId = _requestedOrderIds[0];
            _exitPoint = exitPoint;
            _isScanned = false;
            _elapsedTimeAfterScan = 0f;
            _state = CustomerState.Spawning;
            BuildArmAndPhone();
        }

        /// <summary>
        /// Backward-compatible initialization for single-order callers.
        /// </summary>
        public void Initialize(int orderId, Vector3 exitPoint)
        {
            Initialize(new System.Collections.Generic.List<int> { orderId }, exitPoint);
        }

        /// <summary>
        /// Assigns the customer their place in the queue and destination.
        /// </summary>
        public void SetQueueSlot(int index, Vector3 position)
        {
            _queueIndex = index;
            _targetPosition = position;

            if (_state != CustomerState.SatisfiedExit && _state != CustomerState.AngryExit)
            {
                _state = CustomerState.MovingToQueue;
            }
        }

        /// <summary>
        /// Scans the customer's barcode at the counter. Reveals the requested products and starts the timer.
        /// Highlights warehouse shelf slots containing the requested products.
        /// </summary>
        public void ScanOrder()
        {
            if (_isScanned || !IsAtCounter)
            {
                return;
            }

            _isScanned = true;
            _elapsedTimeAfterScan = 0f;
            _screenFlashTimer = 0.5f;
            if (_phoneScreenRenderer != null)
            {
                _phoneScreenRenderer.material.color = Color.green;
            }

            // Highlight all warehouse shelf slots containing remaining items
            Warehouse.WarehouseManager.Instance?.HighlightSlotsForOrders(_remainingOrderIds, 15.0f);

            string idsStr = string.Join(", #", _requestedOrderIds);
            string countStr = _requestedOrderIds.Count > 1 ? $" ({_requestedOrderIds.Count} тов.)" : "";
            ShowSpeech($"Код принят! Заказ{countStr}: #{idsStr}", isAngry: false, duration: 3.5f);
        }

        private void Update()
        {
            UpdateArmAndPhonePose();

            // Update speech message duration
            if (_speechTimer > 0f)
            {
                _speechTimer -= Time.deltaTime;
                if (_speechTimer <= 0f)
                {
                    _speechMessage = string.Empty;
                }
            }

            switch (_state)
            {
                case CustomerState.Spawning:
                case CustomerState.MovingToQueue:
                    MoveTowardsTarget(_targetPosition);
                    Vector3 flatCurrent = new Vector3(transform.position.x, 0f, transform.position.z);
                    Vector3 flatTarget = new Vector3(_targetPosition.x, 0f, _targetPosition.z);
                    if (Vector3.Distance(flatCurrent, flatTarget) < 0.45f)
                    {
                        _state = CustomerState.WaitingInQueue;
                        // Face north towards the counter table
                        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                        if (_navAgent != null && _navAgent.isOnNavMesh && _navAgent.isActiveAndEnabled)
                        {
                            _navAgent.ResetPath();
                        }
                    }
                    break;

                case CustomerState.WaitingInQueue:
                    // Adjust position if line shifted
                    if (Vector3.Distance(transform.position, _targetPosition) > 0.25f)
                    {
                        MoveTowardsTarget(_targetPosition);
                    }
                    else
                    {
                        // Keep facing north towards the counter
                        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                    }

                    // Timer ONLY increments after the barcode is scanned
                    if (_isScanned && _queueIndex == 0)
                    {
                        _elapsedTimeAfterScan += Time.deltaTime;
                    }
                    break;

                case CustomerState.SatisfiedExit:
                case CustomerState.AngryExit:
                    MoveTowardsTarget(_exitPoint);
                    if (Vector3.Distance(transform.position, _exitPoint) < 0.8f)
                    {
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        private void MoveTowardsTarget(Vector3 destination)
        {
            if (_navAgent != null && _navAgent.isOnNavMesh && _navAgent.isActiveAndEnabled)
            {
                _navAgent.speed = _walkSpeed;
                _navAgent.SetDestination(destination);
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, destination, _walkSpeed * Time.deltaTime);
                Vector3 moveDir = destination - transform.position;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8.0f);
                }
            }
        }

        /// <summary>
        /// Attempts to hand over a product parcel to this customer.
        /// Handles multi-item orders (1 to 4 items): partial deliveries keep the customer at the counter
        /// until all items are delivered. Once completed, calculates economy payment, rating, and departs.
        /// </summary>
        public bool TryReceiveOrder(BoxItem box, out int ratingDelta, out float paymentAwarded, out string feedbackMessage, out bool isOrderComplete)
        {
            ratingDelta = 0;
            paymentAwarded = 0f;
            isOrderComplete = false;

            if (box == null)
            {
                feedbackMessage = "Вы не держите товар в руках!";
                return false;
            }

            if (!IsAtCounter)
            {
                feedbackMessage = "Клиент еще подходит к стойке!";
                return false;
            }

            if (!_isScanned)
            {
                feedbackMessage = "⚠ Сначала отсканируйте штрихкод клиента сканером на стойке!";
                ShowSpeech("Отсканируйте сначала мой штрихкод!", isAngry: false, duration: 3.0f);
                return false;
            }

            // Check if the presented box matches any remaining requested order ID
            if (!_remainingOrderIds.Contains(box.OrderId))
            {
                string neededIds = string.Join(", #", _remainingOrderIds);
                feedbackMessage = $"❌ Клиент отказывается: «Это не мой товар! Мне нужны: #{neededIds}, а у вас #{box.OrderId}!»";
                ShowSpeech($"Это не мой товар! Мне нужны: #{neededIds}!", isAngry: true, duration: 3.5f);
                return false;
            }

            // Hand over accepted item
            int deliveredId = box.OrderId;
            _remainingOrderIds.Remove(deliveredId);
            _receivedOrderIds.Add(deliveredId);
            Destroy(box.gameObject);

            // Check if more items remain for this customer
            if (_remainingOrderIds.Count > 0)
            {
                isOrderComplete = false;
                string remainingStr = string.Join(", #", _remainingOrderIds);
                feedbackMessage = $"✔ Товар #{deliveredId} принят! ({_receivedOrderIds.Count}/{TotalRequestedCount} выдано). Осталось выдать: #{remainingStr}.";
                ShowSpeech($"Товар #{deliveredId} получен! Жду еще {_remainingOrderIds.Count} тов. (#{remainingStr})", isAngry: false, duration: 3.0f);

                // Highlight remaining items in warehouse
                Warehouse.WarehouseManager.Instance?.HighlightSlotsForOrders(_remainingOrderIds, 15.0f);
                return true;
            }

            // --- FULL ORDER COMPLETE ---
            isOrderComplete = true;

            // Calculate rating delta and customer review stars (1 - 5★):
            // 0 - 15s: 5★ (+1 очко рейтинга)
            // 15 - 45s: 4★ (нейтрально: 0)
            // 45 - 60s: 3★ (-1 очко рейтинга)
            // 60 - 120s: 2★ (-3 очка рейтинга)
            // > 120s: 1★ (-5 очков рейтинга)
            int orderStars;
            string speedLabel;
            if (_elapsedTimeAfterScan <= 15.0f)
            {
                orderStars = 5;
                ratingDelta = 1;
                speedLabel = "Быстрое обслуживание! (+1 очко рейтинга)";
            }
            else if (_elapsedTimeAfterScan <= 45.0f)
            {
                orderStars = 4;
                ratingDelta = 0;
                speedLabel = "Стандартная выдача (нейтрально: 0)";
            }
            else if (_elapsedTimeAfterScan <= 60.0f)
            {
                orderStars = 3;
                ratingDelta = -1;
                speedLabel = "Задержка заказа (-1 очко рейтинга)";
            }
            else if (_elapsedTimeAfterScan <= 120.0f)
            {
                orderStars = 2;
                ratingDelta = -3;
                speedLabel = "Долгое ожидание (-3 очка рейтинга)";
            }
            else
            {
                orderStars = 1;
                ratingDelta = -5;
                speedLabel = "Критическое опоздание (-5 очков рейтинга)";
            }

            // Economy calculation per user specification:
            // Base reward: 1 ruble per parcel/product.
            // Speed bonus (+1 rep): x1.1 multiplier.
            // PVZ star rating multiplier (active only after 50 orders): 5★ = 1.2x, 4★ = 1.0x, 3★ = 1.0x, 2★ = 0.9x, 1★ = 0.8x.
            float baseReward = 1.0f * TotalRequestedCount;
            float speedMultiplier = (ratingDelta > 0) ? 1.1f : 1.0f;
            float ratingMultiplier = (RatingManager.Instance != null) ? RatingManager.Instance.CurrentMoneyMultiplier : 1.0f;

            paymentAwarded = (float)Math.Round(baseReward * speedMultiplier * ratingMultiplier, 2);

            // Apply economy & rating updates
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddMoney(paymentAwarded);
            }

            if (RatingManager.Instance != null)
            {
                string orderDesc = $"Клиент ({TotalRequestedCount} тов.) обслужен за {TimeFormatter.FormatMinutesSeconds(_elapsedTimeAfterScan)}";
                RatingManager.Instance.RecordOrder(orderStars, ratingDelta, orderDesc);
            }

            // Customer reaction speech
            string speech = ratingDelta > 0
                ? "Спасибо огромное за быструю выдачу всех товаров!"
                : (ratingDelta == 0 ? "Спасибо, все товары получены." : "Наконец-то получил все позиции...");

            ShowSpeech(speech, isAngry: ratingDelta < 0, duration: 3.5f);

            string ratingProgress = string.Empty;
            if (RatingManager.Instance != null)
            {
                if (!RatingManager.Instance.HasRating)
                {
                    ratingProgress = $" [Оценка: {orderStars}★ | До рейтинга: {RatingManager.Instance.TotalOrdersHandled}/{RatingManager.Instance.MinOrdersForRating}]";
                }
                else
                {
                    ratingProgress = $" [Оценка: {orderStars}★ | Рейтинг ПВЗ: {RatingManager.Instance.CurrentRating:F1}★]";
                }
            }

            string bonusDetails = (ratingDelta > 0 || (RatingManager.Instance != null && RatingManager.Instance.HasRating && ratingMultiplier != 1.0f))
                ? $" [x{speedMultiplier:0.#} скорость{(RatingManager.Instance != null && RatingManager.Instance.HasRating ? $", x{ratingMultiplier:0.#} рейтинг" : string.Empty)}]"
                : string.Empty;

            feedbackMessage = $"✔ Заказ успешно выдан ({TotalRequestedCount} тов.)! Оплата: +{EconomyManager.FormatMoney(paymentAwarded)}{bonusDetails}.{ratingProgress} {speedLabel}";

            // Depart towards entrance
            _state = CustomerState.SatisfiedExit;
            OnCustomerDeparted?.Invoke(this, true);

            return true;
        }

        /// <summary>
        /// Backward-compatible overload for 4-parameter callers.
        /// </summary>
        public bool TryReceiveOrder(BoxItem box, out int ratingDelta, out float paymentAwarded, out string feedbackMessage)
        {
            return TryReceiveOrder(box, out ratingDelta, out paymentAwarded, out feedbackMessage, out _);
        }

        /// <summary>
        /// Displays speech bubble above the customer head.
        /// </summary>
        public void ShowSpeech(string text, bool isAngry, float duration = 3.0f)
        {
            _speechMessage = text;
            _isAngrySpeech = isAngry;
            _speechTimer = duration;
        }

        private void OnGUI()
        {
            if (Camera.main == null)
            {
                return;
            }

            Vector3 headWorldPos = transform.position + Vector3.up * 2.1f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(headWorldPos);

            // Only draw if in front of camera
            if (screenPos.z <= 0.2f || screenPos.z > 14f)
            {
                return;
            }

            float y = Screen.height - screenPos.y;

            // 1. Order Status Tag (Hidden until scanned!)
            GUIStyle tagStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            if (_isScanned)
            {
                // Scanned: show order progress and live timer in minutes & seconds
                tagStyle.normal.textColor = Color.yellow;
                string timerText = TimeFormatter.FormatMinutesSeconds(_elapsedTimeAfterScan);
                string tagText = (TotalRequestedCount > 1)
                    ? $"Заказ: {_receivedOrderIds.Count}/{TotalRequestedCount} тов. ({timerText})"
                    : $"Заказ #{_targetOrderId} ({timerText})";
                GUI.Box(new Rect(screenPos.x - 110, y - 40, 220, 24), tagText, tagStyle);

                // Speed rating indicator below tag
                GUIStyle speedStyle = new GUIStyle
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                if (_elapsedTimeAfterScan <= 15.0f)
                {
                    speedStyle.normal.textColor = Color.green;
                    GUI.Label(new Rect(screenPos.x - 75, y - 18, 150, 18), "⚡ Бонус (+1)", speedStyle);
                }
                else if (_elapsedTimeAfterScan <= 45.0f)
                {
                    speedStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(screenPos.x - 75, y - 18, 150, 18), "⏱ Нейтрально (0)", speedStyle);
                }
                else if (_elapsedTimeAfterScan <= 60.0f)
                {
                    speedStyle.normal.textColor = new Color(1f, 0.6f, 0.2f);
                    GUI.Label(new Rect(screenPos.x - 75, y - 18, 150, 18), "⚠ Штраф (-1)", speedStyle);
                }
                else if (_elapsedTimeAfterScan <= 120.0f)
                {
                    speedStyle.normal.textColor = new Color(1f, 0.3f, 0.2f);
                    GUI.Label(new Rect(screenPos.x - 75, y - 18, 150, 18), "⚠ Задержка (-3)", speedStyle);
                }
                else
                {
                    speedStyle.normal.textColor = Color.red;
                    GUI.Label(new Rect(screenPos.x - 75, y - 18, 150, 18), "⛔ Опоздание (-5)", speedStyle);
                }
            }
            else
            {
                // Unscanned: hide order ID!
                tagStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                string tagText;
                if (_queueIndex == 0)
                {
                    tagText = IsAtCounter ? "Посетитель [Не отсканирован]" : "Посетитель (подходит...)";
                }
                else
                {
                    tagText = $"Очередь #{_queueIndex + 1}";
                }
                GUI.Box(new Rect(screenPos.x - 90, y - 36, 180, 24), tagText, tagStyle);
            }

            // 2. Speech Bubble if active
            if (!string.IsNullOrEmpty(_speechMessage) && _speechTimer > 0f)
            {
                GUIStyle bubbleStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                bubbleStyle.normal.textColor = _isAngrySpeech ? Color.red : Color.cyan;

                GUI.Box(new Rect(screenPos.x - 110, y - 82, 220, 38), _speechMessage, bubbleStyle);
            }
        }
    }
}
