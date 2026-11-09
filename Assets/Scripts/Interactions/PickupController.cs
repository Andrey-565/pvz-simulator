using System;
using UnityEngine;
using PVZ.Items;
using PVZ.Warehouse;
using PVZ.Player;
using PVZ.Computer;
using PVZ.Customers;
using PVZ.Counter;
using PVZ.Calendar;

namespace PVZ.Interactions
{
    /// <summary>
    /// Handles player raycasting, hovering indicators, picking up boxes,
    /// carrying them, placing onto warehouse slots, operating the office computer terminal,
    /// and serving customers at the pickup desk.
    /// </summary>
    public class PickupController : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("Camera from which interaction rays originate.")]
        [SerializeField] private Camera _playerCamera;

        [Tooltip("Transform socket where the carried box is parented.")]
        [SerializeField] private Transform _holdPoint;

        [Tooltip("Maximum raycast distance for picking up objects.")]
        [SerializeField] private float _interactionDistance = 3.0f;

        [Tooltip("Forward impulse force applied when throwing a box.")]
        [SerializeField] private float _throwForce = 7.0f;

        [Tooltip("Layer mask for interactable items.")]
        [SerializeField] private LayerMask _interactableLayer = ~0;

        [Header("Placement Settings")]
        [Tooltip("Downward raycast distance to find ground/table surface when placing.")]
        [SerializeField] private float _placementRayDistance = 2.0f;

        private BoxItem _currentBox;
        private BoxItem _hoveredBox;
        private SupplyBox _currentSupplyBox;
        private SupplyBox _hoveredSupplyBox;
        private BarcodeScannerItem _currentScanner;
        private BarcodeScannerItem _hoveredScannerItem;
        private WarehouseSlot _hoveredSlot;
        private ComputerTerminal _hoveredTerminal;
        private CustomerAI _hoveredCustomer;
        private CheckoutCounter _hoveredCounter;
        private WallCalendar _hoveredCalendar;
        private bool _isHoveringSign;

        private PlayerController _playerController;
        private string _interactionPrompt = string.Empty;
        private string _bannerMessage = string.Empty;
        private float _bannerTimer;

        /// <summary>
        /// Fires when the interaction target changes, passing an explanatory prompt for the player UI.
        /// </summary>
        public event Action<string> OnInteractionPromptChanged;

        /// <summary>
        /// Gets the product parcel currently held in player hands, or null if hands are empty.
        /// </summary>
        public BoxItem CurrentBox => _currentBox;

        /// <summary>
        /// Gets the delivery supply crate currently held in player hands, or null.
        /// </summary>
        public SupplyBox CurrentSupplyBox => _currentSupplyBox;

        /// <summary>
        /// Indicates whether player hands are currently occupied with either a product parcel or a supply crate.
        /// </summary>
        public bool IsCarryingBox => _currentBox != null || _currentSupplyBox != null;

        /// <summary>
        /// Indicates whether player hands are currently holding an individual product parcel.
        /// </summary>
        public bool IsCarryingProduct => _currentBox != null;

        /// <summary>
        /// Indicates whether player hands are currently holding a large supply crate.
        /// </summary>
        public bool IsCarryingSupplyBox => _currentSupplyBox != null;

        /// <summary>
        /// Indicates whether player hands are currently holding the handheld barcode scanner.
        /// </summary>
        public bool IsCarryingScanner => _currentScanner != null;

        /// <summary>
        /// True if the player's hands are free (neither holding a parcel, supply crate, nor scanner).
        /// </summary>
        public bool HasFreeHands => _currentBox == null && _currentSupplyBox == null && _currentScanner == null;

        /// <summary>
        /// Gets the handheld scanner currently in player hands, or null.
        /// </summary>
        public BarcodeScannerItem CurrentScanner => _currentScanner;

        /// <summary>
        /// Gets the current contextual interaction text (e.g. "[E] Поставить в ячейку A1").
        /// </summary>
        public string InteractionPrompt => _interactionPrompt;

        /// <summary>
        /// Displays an on-screen banner notice to the player.
        /// </summary>
        public void ShowBanner(string message, float duration = 4.0f)
        {
            _bannerMessage = message;
            _bannerTimer = duration;
        }

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();

            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            if (_holdPoint == null && _playerCamera != null)
            {
                GameObject defaultHold = new GameObject("DefaultHoldPoint");
                defaultHold.transform.SetParent(_playerCamera.transform);
                defaultHold.transform.localPosition = new Vector3(0.25f, -0.25f, 0.9f);
                defaultHold.transform.localRotation = Quaternion.identity;
                _holdPoint = defaultHold.transform;
            }
        }

        private void Update()
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.deltaTime;
            }

            if (_playerController != null && _playerController.IsMovementLocked)
            {
                _interactionPrompt = string.Empty;
                return;
            }

            UpdateHoverTarget();
            HandleInput();
        }

        /// <summary>
        /// Performs a forward raycast to detect interactable items, customers, or terminals.
        /// </summary>
        private void UpdateHoverTarget()
        {
            if (_playerCamera == null)
            {
                return;
            }

            Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            BoxItem targetBox = null;
            SupplyBox targetSupplyBox = null;
            BarcodeScannerItem targetScanner = null;
            WarehouseSlot targetSlot = null;
            ComputerTerminal targetTerminal = null;
            CustomerAI targetCustomer = null;
            CheckoutCounter targetCounter = null;
            WallCalendar targetCalendar = null;
            bool targetSign = false;
            string newPrompt = string.Empty;

            if (Physics.Raycast(ray, out RaycastHit hit, _interactionDistance, _interactableLayer))
            {
                targetScanner = hit.collider.GetComponentInParent<BarcodeScannerItem>();
                targetTerminal = hit.collider.GetComponentInParent<ComputerTerminal>();
                targetSlot = hit.collider.GetComponentInParent<WarehouseSlot>();
                targetBox = hit.collider.GetComponentInParent<BoxItem>();
                targetSupplyBox = hit.collider.GetComponentInParent<SupplyBox>();
                targetCustomer = hit.collider.GetComponentInParent<CustomerAI>();
                targetCounter = hit.collider.GetComponentInParent<CheckoutCounter>();
                targetCalendar = hit.collider.GetComponentInParent<WallCalendar>();

                if (targetCounter != null)
                {
                    string colName = hit.collider.gameObject.name;
                    if (colName.Contains("Sign") || (targetCounter.StatusSignTablet != null && hit.collider.transform == targetCounter.StatusSignTablet))
                    {
                        targetSign = true;
                    }
                }

                // --- 1. PROMPTS WHEN HOLDING HANDHELD SCANNER ---
                if (IsCarryingScanner)
                {
                    if (targetCustomer != null)
                    {
                        if (!targetCustomer.IsAtCounter)
                        {
                            newPrompt = "Клиент подходит к стойке...";
                        }
                        else if (!targetCustomer.IsScanned)
                        {
                            newPrompt = $"[E / ЛКМ] Отсканировать телефон клиента ({targetCustomer.TotalRequestedCount} тов.)";
                        }
                        else
                        {
                            string remStr = string.Join(", #", targetCustomer.RemainingOrderIds);
                            newPrompt = $"Клиент отсканирован (ожидает товары: #{remStr})";
                        }
                    }
                    else if (targetSupplyBox != null && !targetSupplyBox.IsCarried)
                    {
                        newPrompt = "[E / ЛКМ] Отсканировать и распаковать ящик (5 товаров)";
                    }
                    else if (targetBox != null && !targetBox.IsCarried)
                    {
                        if (!targetBox.IsIntakeScanned)
                        {
                            newPrompt = $"[E / ЛКМ] Отсканировать товар #{targetBox.OrderId} (Приёмка на склад)";
                        }
                        else
                        {
                            newPrompt = $"Товар #{targetBox.OrderId} уже принят (готов к размещению на стеллаж)";
                        }
                    }
                    else if (targetCounter != null)
                    {
                        if (targetSign)
                        {
                            bool isOpen = CustomerQueueManager.Instance != null && CustomerQueueManager.Instance.IsOpen;
                            newPrompt = isOpen ? "[E] Закрыть ПВЗ" : "[E] Открыть ПВЗ";
                        }
                        else
                        {
                            newPrompt = "[E] Положить сканер на стойку";
                        }
                    }
                    else
                    {
                        newPrompt = "[E] Положить сканер";
                    }
                }
                // --- 2. PROMPTS WHEN HOLDING A SUPPLY CRATE ---
                else if (IsCarryingSupplyBox)
                {
                    newPrompt = "[E] Поставить ящик на пол | [F] Распаковать ящик (5 товаров)";
                }
                // --- 3. PROMPTS WHEN HOLDING AN INDIVIDUAL PRODUCT PARCEL ---
                else if (IsCarryingProduct)
                {
                    if (targetCustomer != null)
                    {
                        if (!targetCustomer.IsScanned)
                        {
                            newPrompt = "Сначала отсканируйте код клиента ручным сканером!";
                        }
                        else if (targetCustomer.IsAtCounter)
                        {
                            newPrompt = $"[E] Выдать товар #{_currentBox.OrderId} клиенту";
                        }
                    }
                    else if (targetCounter != null)
                    {
                        if (targetSign)
                        {
                            bool isOpen = CustomerQueueManager.Instance != null && CustomerQueueManager.Instance.IsOpen;
                            newPrompt = isOpen ? "[E] Закрыть ПВЗ" : "[E] Открыть ПВЗ";
                        }
                        else
                        {
                            newPrompt = $"[E] Выдать товар #{_currentBox.OrderId} клиенту";
                        }
                    }
                    else if (targetSlot != null)
                    {
                        if (!_currentBox.IsIntakeScanned)
                        {
                            newPrompt = $"Товар #{_currentBox.OrderId} не принят (отсканируйте сканером)";
                        }
                        else if (!targetSlot.IsFull)
                        {
                            newPrompt = $"[E] Положить товар #{_currentBox.OrderId} в ячейку {targetSlot.SlotId} ({targetSlot.StoredCount}/5)";
                        }
                        else
                        {
                            newPrompt = $"Ячейка {targetSlot.SlotId} заполнена (5/5 товаров)";
                        }
                    }
                    else
                    {
                        newPrompt = "[E] Положить товар на пол";
                    }
                }
                // --- 4. PROMPTS WHEN HANDS ARE COMPLETELY FREE ---
                else
                {
                    if (targetSupplyBox != null && !targetSupplyBox.IsCarried)
                    {
                        newPrompt = "[E] Взять ящик поставки | [F] Распаковать ящик (5 товаров)";
                    }
                    else if (targetScanner != null && !targetScanner.IsCarried)
                    {
                        newPrompt = "[E] Взять сканер штрихкодов в руки";
                    }
                    else if (targetTerminal != null)
                    {
                        newPrompt = "[E] Сесть за рабочий компьютер";
                    }
                    else if (targetCalendar != null)
                    {
                        newPrompt = "[E] Посмотреть настенный календарь";
                    }
                    else if (targetCustomer != null)
                    {
                        if (!targetCustomer.IsAtCounter)
                        {
                            newPrompt = "Клиент подходит к стойке...";
                        }
                        else if (!targetCustomer.IsScanned)
                        {
                            newPrompt = "Возьмите сканер со стойки и отсканируйте телефон клиента!";
                        }
                        else
                        {
                            string remStr = string.Join(", #", targetCustomer.RemainingOrderIds);
                            newPrompt = $"Клиент ожидает товары: #{remStr}";
                        }
                    }
                    else if (targetCounter != null)
                    {
                        if (targetSign)
                        {
                            bool isOpen = CustomerQueueManager.Instance != null && CustomerQueueManager.Instance.IsOpen;
                            newPrompt = isOpen ? "[E] Закрыть ПВЗ" : "[E] Открыть ПВЗ";
                        }
                        else if (targetCounter.ScannerItem != null && !targetCounter.ScannerItem.IsCarried)
                        {
                            targetScanner = targetCounter.ScannerItem;
                            newPrompt = "[E] Взять сканер штрихкодов в руки";
                        }
                    }
                    else if (targetSlot != null)
                    {
                        if (!targetSlot.IsEmpty)
                        {
                            newPrompt = $"[E] Взять товар #{targetSlot.CurrentBox.OrderId} из ячейки {targetSlot.SlotId} ({targetSlot.StoredCount}/5)";
                        }
                        else
                        {
                            newPrompt = $"Ячейка {targetSlot.SlotId} (Свободна)";
                        }
                    }
                    else if (targetBox != null && !targetBox.IsCarried)
                    {
                        string status = targetBox.IsIntakeScanned ? string.Empty : " [Не принят]";
                        newPrompt = $"[E] Взять товар #{targetBox.OrderId}{status}";
                    }
                }
            }

            _hoveredTerminal = targetTerminal;
            _hoveredCalendar = targetCalendar;
            _hoveredCustomer = targetCustomer;
            _hoveredCounter = targetCounter;
            _isHoveringSign = targetSign;
            _hoveredSlot = targetSlot;
            _hoveredBox = targetBox;
            _hoveredSupplyBox = targetSupplyBox;
            _hoveredScannerItem = targetScanner;

            if (_interactionPrompt != newPrompt)
            {
                _interactionPrompt = newPrompt;
                OnInteractionPromptChanged?.Invoke(_interactionPrompt);
            }
        }

        /// <summary>
        /// Processes pickup, placement, unpacking, counter operations, scanning, and throw inputs.
        /// </summary>
        private void HandleInput()
        {
            bool isEPressed = Input.GetKeyDown(KeyCode.E);
            bool isFPressed = Input.GetKeyDown(KeyCode.F);
            bool isLMBPressed = Input.GetMouseButtonDown(0);
            bool isRMBPressed = Input.GetMouseButtonDown(1);

            // 1. Unpacking supply crate via [F] key
            if (isFPressed)
            {
                if (IsCarryingSupplyBox)
                {
                    SupplyBox boxToUnpack = _currentSupplyBox;
                    _currentSupplyBox = null;
                    boxToUnpack.Unpack(out string fb);
                    ShowBanner(fb, 4.0f);
                    return;
                }
                else if (HasFreeHands && _hoveredSupplyBox != null && !_hoveredSupplyBox.IsCarried)
                {
                    SupplyBox boxToUnpack = _hoveredSupplyBox;
                    _hoveredSupplyBox = null;
                    boxToUnpack.Unpack(out string fb);
                    ShowBanner(fb, 4.0f);
                    return;
                }
            }

            // 2. Terminal interaction (strictly [E] key when hands free)
            if (isEPressed && _hoveredTerminal != null && HasFreeHands)
            {
                _hoveredTerminal.OpenTerminal(_playerController);
                return;
            }

            // 3. Wall Calendar interaction (strictly [E] key when hands free)
            if (isEPressed && _hoveredCalendar != null && !_hoveredCalendar.IsOpen && !_hoveredCalendar.JustClosedThisFrame && HasFreeHands)
            {
                _hoveredCalendar.OpenCalendar(_playerController);
                return;
            }

            // 4. Open/Close store sign tablet on counter
            if ((isEPressed || isLMBPressed) && _hoveredCounter != null && _isHoveringSign)
            {
                _hoveredCounter.ToggleOpenClosed(out string fb);
                ShowBanner(fb);
                return;
            }

            // ==========================================
            // CASE A: PLAYER IS CARRYING HANDHELD SCANNER
            // ==========================================
            if (IsCarryingScanner)
            {
                // Fire scan at customer's phone, supply crate, or individual product parcel
                if (isEPressed || isLMBPressed)
                {
                    if (_hoveredCustomer != null || _hoveredBox != null || _hoveredSupplyBox != null)
                    {
                        Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                        bool success = _currentScanner.FireScan(ray, _interactableLayer, out string msg);
                        ShowBanner(msg);
                        return;
                    }

                    // Put scanner back onto counter cradle or surface on E
                    if (isEPressed)
                    {
                        PlaceScanner();
                        return;
                    }
                }

                // RMB: Put down scanner
                if (isRMBPressed)
                {
                    PlaceScanner();
                    return;
                }
            }

            // ==========================================
            // CASE B: PLAYER IS CARRYING A SUPPLY CRATE
            // ==========================================
            else if (IsCarryingSupplyBox)
            {
                if (isEPressed || isLMBPressed)
                {
                    PlaceSupplyBoxOnFloor();
                    return;
                }
            }

            // ==========================================
            // CASE C: PLAYER IS CARRYING AN INDIVIDUAL PRODUCT PARCEL
            // ==========================================
            else if (IsCarryingProduct)
            {
                // Handover product to customer directly or through counter
                if ((isEPressed || isLMBPressed) && (_hoveredCustomer != null || _hoveredCounter != null))
                {
                    CustomerAI targetCust = _hoveredCustomer;
                    if (targetCust == null && CustomerQueueManager.Instance != null)
                    {
                        targetCust = CustomerQueueManager.Instance.FirstInQueue;
                    }

                    if (targetCust != null && targetCust.IsAtCounter)
                    {
                        if (!targetCust.IsScanned)
                        {
                            ShowBanner("❌ Клиент еще не отсканирован! Возьмите ручной сканер со стойки и отсканируйте телефон клиента.");
                            return;
                        }

                        BoxItem boxToDeliver = _currentBox;
                        if (targetCust.TryReceiveOrder(boxToDeliver, out int ratingDelta, out float payment, out string msg, out bool isOrderComplete))
                        {
                            _currentBox = null;
                            ShowBanner(msg);
                            return;
                        }
                        else
                        {
                            ShowBanner(msg);
                            return;
                        }
                    }
                }

                // Place carried product into slot or onto floor
                if (isEPressed || isLMBPressed)
                {
                    if (_hoveredSlot != null)
                    {
                        // Requirement: Cannot place product onto shelf before intake scanning!
                        if (!_currentBox.IsIntakeScanned)
                        {
                            ShowBanner($"❌ Нельзя положить товар #{_currentBox.OrderId} на стеллаж! Сначала отсканируйте его ручным сканером (приёмка груза).");
                            return;
                        }

                        if (_hoveredSlot.IsFull)
                        {
                            ShowBanner($"❌ Ячейка [{_hoveredSlot.SlotId}] уже заполнена (5 из 5 товаров)!");
                            return;
                        }

                        PlaceIntoSlot(_hoveredSlot);
                        return;
                    }
                    else
                    {
                        PlaceBoxOnFloor();
                        return;
                    }
                }

                // Right Click to Throw product
                if (isRMBPressed)
                {
                    ThrowBox();
                    return;
                }
            }

            // ==========================================
            // CASE D: PLAYER HAS FREE HANDS
            // ==========================================
            else if (HasFreeHands)
            {
                // Pick up handheld scanner
                if ((isEPressed || isLMBPressed) && _hoveredScannerItem != null && !_hoveredScannerItem.IsCarried)
                {
                    PickupScanner(_hoveredScannerItem);
                    return;
                }

                // Reminder if clicking on customer without scanner
                if ((isEPressed || isLMBPressed) && _hoveredCustomer != null && !_hoveredCustomer.IsScanned)
                {
                    ShowBanner("❌ Возьмите ручной сканер со стойки выдачи и наведите его на телефон клиента!");
                    return;
                }

                // Pick up supply crate
                if ((isEPressed || isLMBPressed) && _hoveredSupplyBox != null && !_hoveredSupplyBox.IsCarried)
                {
                    PickupSupplyBox(_hoveredSupplyBox);
                    return;
                }

                // Pick up product parcel from slot or floor
                if (isEPressed || isLMBPressed)
                {
                    if (_hoveredSlot != null && !_hoveredSlot.IsEmpty)
                    {
                        BoxItem boxFromSlot = _hoveredSlot.RemoveBox();
                        PickupBox(boxFromSlot);
                        return;
                    }
                    else if (_hoveredBox != null && !_hoveredBox.IsCarried)
                    {
                        PickupBox(_hoveredBox);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Equips a large delivery supply crate into player hands.
        /// </summary>
        public void PickupSupplyBox(SupplyBox supplyBox)
        {
            if (supplyBox == null || !HasFreeHands)
            {
                return;
            }

            _currentSupplyBox = supplyBox;
            _currentSupplyBox.PickUp(_holdPoint != null ? _holdPoint : _playerCamera.transform);
            _hoveredSupplyBox = null;
            ShowBanner("📦 Ящик поставки взят в руки! Нажмите [F] чтобы распаковать 5 товаров.", 3.5f);
        }

        /// <summary>
        /// Places the carried supply crate firmly onto the floor or table surface.
        /// </summary>
        public void PlaceSupplyBoxOnFloor()
        {
            if (_currentSupplyBox == null)
            {
                return;
            }

            Vector3 placePos;
            Quaternion placeRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            Vector3 rayOrigin = transform.position + (transform.forward * 1.4f) + (Vector3.up * 0.5f);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _placementRayDistance))
            {
                placePos = hit.point + (Vector3.up * 0.28f);
            }
            else
            {
                placePos = rayOrigin + (Vector3.down * 0.5f);
            }

            SupplyBox boxToPlace = _currentSupplyBox;
            _currentSupplyBox = null;
            boxToPlace.PlaceAt(placePos, placeRot);
            ShowBanner("Ящик поставки поставлен.", 2.0f);
        }

        /// <summary>
        /// Equips the handheld barcode scanner into player hands.
        /// </summary>
        public void PickupScanner(BarcodeScannerItem scanner)
        {
            if (scanner == null || !HasFreeHands)
            {
                return;
            }

            _currentScanner = scanner;
            _currentScanner.PickUp(_holdPoint != null ? _holdPoint : _playerCamera.transform);
            _hoveredScannerItem = null;
            ShowBanner("🔫 Сканер штрихкодов взят в руки! Наведите на телефон клиента или коробку и нажмите [E / ЛКМ].", 3.0f);
        }

        /// <summary>
        /// Puts down the handheld scanner onto the counter cradle or surface.
        /// </summary>
        public void PlaceScanner()
        {
            if (_currentScanner == null)
            {
                return;
            }

            if (_hoveredCounter != null)
            {
                _currentScanner.PlaceInCradle();
                _currentScanner = null;
                ShowBanner("Сканер возвращен на стойку выдачи.", 2.5f);
                return;
            }

            Ray ray = _playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, _placementRayDistance, _interactableLayer))
            {
                _currentScanner.PlaceAt(hit.point + Vector3.up * 0.04f, Quaternion.Euler(0f, _playerCamera.transform.eulerAngles.y, 0f));
            }
            else
            {
                _currentScanner.PlaceAt(transform.position + transform.forward * 0.8f + Vector3.up * 0.1f, Quaternion.identity);
            }

            _currentScanner = null;
            ShowBanner("Сканер положен.", 2.0f);
        }

        /// <summary>
        /// Releases the currently held box from the player hands without throwing.
        /// </summary>
        public BoxItem ReleaseCurrentBox()
        {
            if (_currentBox == null)
            {
                return null;
            }

            BoxItem released = _currentBox;
            _currentBox = null;
            released.SetCarried(false);
            return released;
        }

        /// <summary>
        /// Places the carried box into a designated warehouse slot.
        /// </summary>
        public void PlaceIntoSlot(WarehouseSlot slot)
        {
            if (_currentBox == null || slot == null)
            {
                return;
            }

            BoxItem boxToPlace = _currentBox;
            _currentBox = null;
            boxToPlace.SetCarried(false);
            slot.TryPlaceBox(boxToPlace);
        }

        /// <summary>
        /// Attaches target box to the hold point.
        /// </summary>
        public void PickupBox(BoxItem box)
        {
            if (box == null || _currentBox != null)
            {
                return;
            }

            _currentBox = box;
            _currentBox.SetCarried(true, _holdPoint);
            _hoveredBox = null;
            _hoveredSlot = null;
        }

        /// <summary>
        /// Places the carried box down on a floor or table surface.
        /// </summary>
        public void PlaceBoxOnFloor()
        {
            if (_currentBox == null)
            {
                return;
            }

            Vector3 placePos;
            Quaternion placeRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            Vector3 rayOrigin = transform.position + (transform.forward * 1.2f) + (Vector3.up * 0.5f);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, _placementRayDistance))
            {
                placePos = hit.point + (Vector3.up * 0.25f);
            }
            else
            {
                placePos = rayOrigin + (Vector3.down * 0.5f);
            }

            BoxItem boxToRelease = _currentBox;
            _currentBox = null;
            boxToRelease.PlaceAt(placePos, placeRot);
        }

        /// <summary>
        /// Throws the carried box in the camera's forward direction.
        /// </summary>
        public void ThrowBox()
        {
            if (_currentBox == null)
            {
                return;
            }

            Vector3 throwDirection = _playerCamera != null ? _playerCamera.transform.forward : transform.forward;
            BoxItem boxToThrow = _currentBox;
            _currentBox = null;

            boxToThrow.Throw(throwDirection + (Vector3.up * 0.2f), _throwForce);
        }

        private void OnGUI()
        {
            if (_playerController != null && _playerController.IsMovementLocked)
            {
                return;
            }

            // On-screen HUD for contextual interaction prompt
            if (!string.IsNullOrEmpty(_interactionPrompt))
            {
                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = Color.yellow;

                float width = 520f;
                float height = 36f;
                float x = (Screen.width - width) * 0.5f;
                float y = Screen.height * 0.65f;

                GUI.Box(new Rect(x, y, width, height), _interactionPrompt, style);
            }

            // On-screen notification banner
            if (_bannerTimer > 0f && !string.IsNullOrEmpty(_bannerMessage))
            {
                GUIStyle bStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                bStyle.normal.textColor = _bannerMessage.StartsWith("❌") ? new Color(1f, 0.4f, 0.4f) : Color.yellow;

                float bWidth = 620f;
                float bHeight = 40f;
                float bx = (Screen.width - bWidth) * 0.5f;
                float by = (Screen.height * 0.65f) - 50f;

                GUI.Box(new Rect(bx, by, bWidth, bHeight), _bannerMessage, bStyle);
            }

            // Small center crosshair
            GUIStyle crossStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20
            };
            crossStyle.normal.textColor = Color.white;
            GUI.Label(new Rect((Screen.width - 20) * 0.5f, (Screen.height - 20) * 0.5f, 20, 20), "+", crossStyle);
        }
    }
}
