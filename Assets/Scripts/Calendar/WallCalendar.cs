using UnityEngine;
using PVZ.Player;
using PVZ.Calendar.UI;

namespace PVZ.Calendar
{
    /// <summary>
    /// Interactive physical calendar object mounted on the warehouse wall.
    /// Handles player raycast interaction, hover prompting, and opening the calendar window.
    /// </summary>
    public class WallCalendar : MonoBehaviour
    {
        [Tooltip("Reference to the CalendarUI component.")]
        [SerializeField] private CalendarUI _calendarUI;

        /// <summary>
        /// True if the calendar UI is currently opened by the player.
        /// </summary>
        public bool IsOpen => _calendarUI != null && _calendarUI.IsOpen;

        /// <summary>
        /// True if the calendar UI was just closed during the current frame.
        /// </summary>
        public bool JustClosedThisFrame => _calendarUI != null && _calendarUI.JustClosedThisFrame;

        private void Awake()
        {
            if (_calendarUI == null)
            {
                _calendarUI = GetComponent<CalendarUI>();
                if (_calendarUI == null)
                {
                    _calendarUI = gameObject.AddComponent<CalendarUI>();
                }
            }
        }

        /// <summary>
        /// Opens the full interactive calendar interface.
        /// </summary>
        public void OpenCalendar(PlayerController player)
        {
            if (_calendarUI != null)
            {
                _calendarUI.OpenCalendar(player);
            }
        }

        /// <summary>
        /// Closes the calendar interface.
        /// </summary>
        public void CloseCalendar()
        {
            if (_calendarUI != null)
            {
                _calendarUI.CloseCalendar();
            }
        }
    }
}
