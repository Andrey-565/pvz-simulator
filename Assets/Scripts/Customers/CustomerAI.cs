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
        [Tooltip("The unique order number this customer is picking up.")]
        [SerializeField] private int _targetOrderId;

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
        /// Gets the order ID this customer is requesting.
        /// </summary>
        public int TargetOrderId => _targetOrderId;

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

        /// <summary>
        /// True if customer is standing in front of the counter ready to be served.
        /// </summary>
        public bool IsAtCounter => _queueIndex == 0 && _state == CustomerState.WaitingInQueue;

        private void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _isScanned = false;
            _elapsedTimeAfterScan = 0f;
        }

        /// <summary>
        /// Initializes the customer with their target order ID and exit destination.
        /// </summary>
        public void Initialize(int orderId, Vector3 exitPoint)
        {
            _targetOrderId = orderId;
            _exitPoint = exitPoint;
            _isScanned = false;
            _elapsedTimeAfterScan = 0f;
            _state = CustomerState.Spawning;
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
        /// Scans the customer's barcode at the counter. Reveals the order ID and starts the timer.
        /// </summary>
        public void ScanOrder()
        {
            if (_isScanned || !IsAtCounter)
            {
                return;
            }

            _isScanned = true;
            _elapsedTimeAfterScan = 0f;
            ShowSpeech($"Код принят! Заказ #{_targetOrderId}", isAngry: false, duration: 3.0f);
        }

        private void Update()
        {
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
        /// Attempts to hand over a parcel box to this customer.
        /// </summary>
        public bool TryReceiveOrder(BoxItem box, out int ratingDelta, out float paymentAwarded, out string feedbackMessage)
        {
            ratingDelta = 0;
            paymentAwarded = 0f;

            if (box == null)
            {
                feedbackMessage = "Вы не держите посылку в руках!";
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

            if (box.OrderId != _targetOrderId)
            {
                feedbackMessage = $"❌ Клиент отказывается: «Это не мой заказ! Я жду #{_targetOrderId}, а у вас #{box.OrderId}!»";
                ShowSpeech($"Это не моя посылка! Мне нужен заказ #{_targetOrderId}!", isAngry: true, duration: 3.5f);
                return false;
            }

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
            // Base reward: 1 ruble per parcel.
            // Speed bonus (+1 rep): x1.1 multiplier.
            // PVZ star rating multiplier (active only after 50 orders): 5★ = 1.2x, 4★ = 1.0x, 3★ = 1.0x, 2★ = 0.9x, 1★ = 0.8x.
            float baseReward = 1.0f;
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
                RatingManager.Instance.RecordOrder(orderStars, ratingDelta, $"Клиент #{_targetOrderId} обслужен за {TimeFormatter.FormatMinutesSeconds(_elapsedTimeAfterScan)}");
            }

            // Customer reaction speech
            string speech = ratingDelta > 0
                ? "Спасибо огромное за быструю выдачу!"
                : (ratingDelta == 0 ? "Спасибо, заказ получен." : "Наконец-то дождался свой заказ...");

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

            feedbackMessage = $"✔ Заказ #{_targetOrderId} успешно выдан! Оплата: +{EconomyManager.FormatMoney(paymentAwarded)}{bonusDetails}.{ratingProgress} {speedLabel}";

            // Destroy the box item GameObject as it is handed over
            Destroy(box.gameObject);

            // Depart towards entrance
            _state = CustomerState.SatisfiedExit;
            OnCustomerDeparted?.Invoke(this, true);

            return true;
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
                // Scanned: show order ID and live timer in minutes & seconds
                tagStyle.normal.textColor = Color.yellow;
                string timerText = TimeFormatter.FormatMinutesSeconds(_elapsedTimeAfterScan);
                string tagText = $"Заказ #{_targetOrderId} ({timerText})";
                GUI.Box(new Rect(screenPos.x - 90, y - 40, 180, 24), tagText, tagStyle);

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
