using System;
using UnityEngine;
using PVZ.Items;
using PVZ.Warehouse;
using PVZ.Player;
using PVZ.Computer;
using PVZ.Customers;
using PVZ.Counter;

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
        private WarehouseSlot _hoveredSlot;
        private ComputerTerminal _hoveredTerminal;
        private CustomerAI _hoveredCustomer;
        private CheckoutCounter _hoveredCounter;
        private bool _isHoveringSign;
        private bool _isHoveringScanner;

        private PlayerController _playerController;
        private string _interactionPrompt = string.Empty;

        /// <summary>
        /// Fires when the interaction target changes, passing an explanatory prompt for the player UI.
        /// </summary>
        public event Action<string> OnInteractionPromptChanged;

        /// <summary>
        /// Gets the box currently held in player hands, or null if hands are empty.
        /// </summary>
        public BoxItem CurrentBox => _currentBox;

        /// <summary>
        /// Indicates whether player hands are currently occupied with a parcel.
        /// </summary>
        public bool IsCarryingBox => _currentBox != null;

        /// <summary>
        /// Gets the current contextual interaction text (e.g. "[E] Поставить в ячейку A1").
        /// </summary>
        public string InteractionPrompt => _interactionPrompt;

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
            WarehouseSlot targetSlot = null;
            ComputerTerminal targetTerminal = null;
            CustomerAI targetCustomer = null;
            CheckoutCounter targetCounter = null;
            bool targetSign = false;
            bool targetScanner = false;
            string newPrompt = string.Empty;

            if (Physics.Raycast(ray, out RaycastHit hit, _interactionDistance, _interactableLayer))
            {
                targetTerminal = hit.collider.GetComponentInParent<ComputerTerminal>();
                targetSlot = hit.collider.GetComponentInParent<WarehouseSlot>();
                targetBox = hit.collider.GetComponentInParent<BoxItem>();
                targetCustomer = hit.collider.GetComponentInParent<CustomerAI>();
                targetCounter = hit.collider.GetComponentInParent<CheckoutCounter>();

                if (targetTerminal != null && !IsCarryingBox)
                {
                    newPrompt = "[E] Сесть за рабочий компьютер";
                }
                else if (targetCustomer != null)
                {
                    if (!targetCustomer.IsScanned)
                    {
                        if (IsCarryingBox)
                        {
                            newPrompt = "Сначала отсканируйте код клиента на стойке!";
                        }
                        else
                        {
                            newPrompt = "[E] Отсканировать штрихкод клиента";
                        }
                    }
                    else
                    {
                        if (IsCarryingBox)
                        {
                            newPrompt = $"[E] Выдать заказ #{_currentBox.OrderId} клиенту";
                        }
                        else
                        {
                            newPrompt = $"Клиент ожидает заказ #{targetCustomer.TargetOrderId}";
                        }
                    }
                }
                else if (targetCounter != null)
                {
                    string colName = hit.collider.gameObject.name;
                    if (colName.Contains("Sign") || (targetCounter.StatusSignTablet != null && hit.collider.transform == targetCounter.StatusSignTablet))
                    {
                        targetSign = true;
                        bool isOpen = CustomerQueueManager.Instance != null && CustomerQueueManager.Instance.IsOpen;
                        newPrompt = isOpen ? "[E] Закрыть ПВЗ" : "[E] Открыть ПВЗ";
                    }
                    else if (colName.Contains("Scanner") || (targetCounter.BarcodeScanner != null && hit.collider.transform == targetCounter.BarcodeScanner))
                    {
                        targetScanner = true;
                        newPrompt = "[E] Использовать сканер штрихкодов";
                    }
                    else
                    {
                        if (IsCarryingBox)
                        {
                            newPrompt = $"[E] Выдать заказ #{_currentBox.OrderId} клиенту";
                        }
                    }
                }
                else if (targetSlot != null)
                {
                    if (IsCarryingBox)
                    {
                        if (targetSlot.IsEmpty)
                        {
                            newPrompt = $"[E] Разместить заказ #{_currentBox.OrderId} в ячейку {targetSlot.SlotId}";
                        }
                        else
                        {
                            newPrompt = $"Ячейка {targetSlot.SlotId} занята (Заказ #{targetSlot.CurrentBox.OrderId})";
                        }
                    }
                    else
                    {
                        if (!targetSlot.IsEmpty)
                        {
                            newPrompt = $"[E] Взять заказ #{targetSlot.CurrentBox.OrderId} из ячейки {targetSlot.SlotId}";
                        }
                        else
                        {
                            newPrompt = $"Ячейка {targetSlot.SlotId} (Свободна)";
                        }
                    }
                }
                else if (targetBox != null && !targetBox.IsCarried)
                {
                    if (!IsCarryingBox)
                    {
                        newPrompt = $"[E] Взять посылку #{targetBox.OrderId}";
                    }
                }
            }

            _hoveredTerminal = targetTerminal;
            _hoveredCustomer = targetCustomer;
            _hoveredCounter = targetCounter;
            _isHoveringSign = targetSign;
            _isHoveringScanner = targetScanner;
            _hoveredSlot = targetSlot;
            _hoveredBox = targetBox;

            if (_interactionPrompt != newPrompt)
            {
                _interactionPrompt = newPrompt;
                OnInteractionPromptChanged?.Invoke(_interactionPrompt);
            }
        }

        /// <summary>
        /// Processes pickup, placement, counter operations, scanning, and throw inputs.
        /// </summary>
        private void HandleInput()
        {
            // [E] or Left Click to Interact
            if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
            {
                // 1. Terminal interaction
                if (_hoveredTerminal != null && !IsCarryingBox)
                {
                    _hoveredTerminal.OpenTerminal(_playerController);
                    return;
                }

                // 2. Open/Close tablet on counter
                if (_hoveredCounter != null && _isHoveringSign)
                {
                    _hoveredCounter.ToggleOpenClosed(out string fb);
                    return;
                }

                // 3. Barcode scanner on counter
                if (_hoveredCounter != null && _isHoveringScanner)
                {
                    _hoveredCounter.ScanCustomerOrder();
                    return;
                }

                // 4. Scanning customer directly if player aims at customer with empty hands
                if (_hoveredCustomer != null && !_hoveredCustomer.IsScanned && !IsCarryingBox)
                {
                    CheckoutCounter counter = FindFirstObjectByType<CheckoutCounter>();
                    if (counter != null)
                    {
                        counter.ScanCustomerOrder();
                    }
                    else
                    {
                        _hoveredCustomer.ScanOrder();
                    }
                    return;
                }

                // 5. Handover box to customer directly or through counter
                if (IsCarryingBox && (_hoveredCustomer != null || _hoveredCounter != null))
                {
                    CustomerAI targetCust = _hoveredCustomer;
                    if (targetCust == null && CustomerQueueManager.Instance != null)
                    {
                        targetCust = CustomerQueueManager.Instance.FirstInQueue;
                    }

                    if (targetCust != null)
                    {
                        BoxItem boxToDeliver = _currentBox;
                        if (targetCust.TryReceiveOrder(boxToDeliver, out int ratingDelta, out int payment, out string msg))
                        {
                            _currentBox = null;
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                // 6. Carrying box placement
                if (IsCarryingBox)
                {
                    if (_hoveredSlot != null && _hoveredSlot.IsEmpty)
                    {
                        PlaceIntoSlot(_hoveredSlot);
                    }
                    else if (_hoveredSlot == null)
                    {
                        PlaceBoxOnFloor();
                    }
                }
                else
                {
                    // 7. Free hands pickup
                    if (_hoveredSlot != null && !_hoveredSlot.IsEmpty)
                    {
                        BoxItem boxFromSlot = _hoveredSlot.RemoveBox();
                        PickupBox(boxFromSlot);
                    }
                    else if (_hoveredBox != null && !_hoveredBox.IsCarried)
                    {
                        PickupBox(_hoveredBox);
                    }
                }
            }

            // Right Click to Throw
            if (Input.GetMouseButtonDown(1) && IsCarryingBox)
            {
                ThrowBox();
            }
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

                float width = 480f;
                float height = 36f;
                float x = (Screen.width - width) * 0.5f;
                float y = Screen.height * 0.65f;

                GUI.Box(new Rect(x, y, width, height), _interactionPrompt, style);
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
