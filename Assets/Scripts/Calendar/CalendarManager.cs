using System;
using UnityEngine;
using PVZ.Delivery;
using PVZ.Calendar.UI;

namespace PVZ.Calendar
{
    /// <summary>
    /// Central manager for the in-game calendar and shift progression.
    /// Tracks current work day, month, shift completion, and coordinates resetting
    /// daily delivery limits (2 per day) and cooldowns (100s) upon shift change.
    /// </summary>
    public class CalendarManager : MonoBehaviour
    {
        private static CalendarManager _instance;

        [Header("Calendar Settings")]
        [Tooltip("Name of the active month in Russian.")]
        [SerializeField] private string _monthName = "Ноябрь";

        [Tooltip("Active year.")]
        [SerializeField] private int _year = 2026;

        [Tooltip("Total days in the active month.")]
        [SerializeField] private int _daysInMonth = 30;

        [Tooltip("Day of week of the 1st of month (0 = Monday, 6 = Sunday). November 2026 started on Sunday = 6.")]
        [SerializeField] private int _firstDayOfWeekOffset = 6;

        [Header("Shift State")]
        [Tooltip("The current active work day number (1-based).")]
        [SerializeField] private int _currentDay = 1;

        [Tooltip("Total completed work shifts.")]
        [SerializeField] private int _totalShiftsCompleted = 0;

        private AudioSource _audioSource;

        /// <summary>
        /// Fires when the work shift is completed and day advances to the next.
        /// Passes new currentDay and totalShiftsCompleted.
        /// </summary>
        public event Action<int, int> OnShiftCompleted;

        /// <summary>
        /// Global singleton accessor with lazy auto-initialization.
        /// </summary>
        public static CalendarManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CalendarManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("CalendarManager");
                        _instance = go.AddComponent<CalendarManager>();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Active month display title.
        /// </summary>
        public string MonthName => _monthName;

        /// <summary>
        /// Active year.
        /// </summary>
        public int Year => _year;

        /// <summary>
        /// Total days in the active month.
        /// </summary>
        public int DaysInMonth => _daysInMonth;

        /// <summary>
        /// Offset for 1st day of month (0 = Mon, 6 = Sun).
        /// </summary>
        public int FirstDayOfWeekOffset => _firstDayOfWeekOffset;

        /// <summary>
        /// Current active work day (1 to DaysInMonth).
        /// </summary>
        public int CurrentDay => _currentDay;

        /// <summary>
        /// Total completed shifts during gameplay.
        /// </summary>
        public int TotalShiftsCompleted => _totalShiftsCompleted;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
        }

        private void Start()
        {
            EnsureWallCalendarInScene();
        }

        /// <summary>
        /// Ensures a physical interactive WallCalendar object is present on the warehouse wall.
        /// </summary>
        public void EnsureWallCalendarInScene()
        {
            if (FindFirstObjectByType<WallCalendar>() != null)
            {
                return;
            }

            Vector3 position = new Vector3(-4.0f, 1.8f, 11.78f);
            Quaternion rotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject calendarObj = new GameObject("WallCalendar");
            calendarObj.transform.position = position;
            calendarObj.transform.rotation = rotation;

            // Cork/wood backing
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Backboard";
            board.transform.SetParent(calendarObj.transform);
            board.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            board.transform.localRotation = Quaternion.identity;
            board.transform.localScale = new Vector3(0.85f, 1.05f, 0.03f);
            Renderer boardRend = board.GetComponent<Renderer>();
            if (boardRend != null && boardRend.sharedMaterial != null)
            {
                Material mat = new Material(boardRend.sharedMaterial) { color = new Color(0.35f, 0.22f, 0.12f) };
                boardRend.sharedMaterial = mat;
            }

            // Paper sheet
            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "PaperPage";
            paper.transform.SetParent(calendarObj.transform);
            paper.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            paper.transform.localRotation = Quaternion.identity;
            paper.transform.localScale = new Vector3(0.78f, 0.88f, 0.02f);
            Renderer paperRend = paper.GetComponent<Renderer>();
            if (paperRend != null && paperRend.sharedMaterial != null)
            {
                Material mat = new Material(paperRend.sharedMaterial) { color = new Color(0.96f, 0.96f, 0.94f) };
                paperRend.sharedMaterial = mat;
            }

            // Top red header
            GameObject topBinding = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topBinding.name = "TopBinding";
            topBinding.transform.SetParent(calendarObj.transform);
            topBinding.transform.localPosition = new Vector3(0f, 0.44f, -0.01f);
            topBinding.transform.localRotation = Quaternion.identity;
            topBinding.transform.localScale = new Vector3(0.80f, 0.12f, 0.03f);
            Renderer bindRend = topBinding.GetComponent<Renderer>();
            if (bindRend != null && bindRend.sharedMaterial != null)
            {
                Material mat = new Material(bindRend.sharedMaterial) { color = new Color(0.85f, 0.15f, 0.15f) };
                bindRend.sharedMaterial = mat;
            }

            BoxCollider col = calendarObj.AddComponent<BoxCollider>();
            col.size = new Vector3(0.85f, 1.05f, 0.1f);
            col.center = Vector3.zero;

            calendarObj.AddComponent<WallCalendar>();
            calendarObj.AddComponent<CalendarUI>();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Completes the current shift:
        /// - Advances to the next day on the calendar (crosses out current, marks new in red).
        /// - Resets delivery allowance (2 per shift) and delivery cooldown.
        /// - Plays a chime sound and displays notification.
        /// </summary>
        public void CompleteShift()
        {
            int previousDay = _currentDay;
            _currentDay++;
            _totalShiftsCompleted++;

            if (_currentDay > _daysInMonth)
            {
                _currentDay = 1; // Loop or roll over
            }

            // Reset deliveries and cooldown in DeliveryManager
            if (DeliveryManager.Instance != null)
            {
                DeliveryManager.Instance.ResetShift();
                DeliveryManager.Instance.ShowNotification(
                    $"🌙 Смена #{_totalShiftsCompleted} завершена! Наступил День {_currentDay} ({_monthName} {_year}). Лимит поставок (2/2) и КД сброшены!",
                    7.0f);
            }

            PlayShiftCompleteSound();
            OnShiftCompleted?.Invoke(_currentDay, _totalShiftsCompleted);
        }

        private void PlayShiftCompleteSound()
        {
            if (_audioSource == null)
            {
                return;
            }

            // Procedural pleasant dual-tone chime
            int sampleRate = 44100;
            int length = sampleRate / 2; // 0.5s
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float freq = t < 0.22f ? 523.25f : 659.25f; // C5 -> E5 bright pleasant bell
                float env = Mathf.Exp(-t * 4.5f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("ShiftChime", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip);
        }
    }
}
