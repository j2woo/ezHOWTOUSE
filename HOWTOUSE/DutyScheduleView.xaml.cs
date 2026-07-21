using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MySql.Data.MySqlClient;

namespace HOWTOUSE
{
    public partial class DutyScheduleView : UserControl
    {
        private readonly ObservableCollection<DutyCalendarDay> calendarDays = new ObservableCollection<DutyCalendarDay>();
        private readonly HashSet<DateTime> holidays = new HashSet<DateTime>();
        // 실제 당직 데이터가 연결되기 전까지 화면에 표시할 샘플 당직자입니다.
        private readonly Dictionary<int, string> dutyOfficersByDay = new Dictionary<int, string>
        {
            { 9, "김민수" },
            { 13, "이서연" },
            { 24, "박지훈" }
        };
        private bool isCalendarInitializing;

        public DutyScheduleView()
        {
            InitializeComponent();
            CalendarItemsControl.ItemsSource = calendarDays;
            InitializeCalendar();
        }

        private void InitializeCalendar()
        {
            isCalendarInitializing = true;
            MonthComboBox.ItemsSource = Enumerable.Range(1, 12).ToList();
            YearComboBox.ItemsSource = Enumerable.Range(DateTime.Today.Year - 2, 6).ToList();
            MonthComboBox.SelectedIndex = DateTime.Today.Month - 1;
            YearComboBox.SelectedItem = DateTime.Today.Year;
            isCalendarInitializing = false;
            RenderCalendar();
        }

        private void CalendarSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isCalendarInitializing)
            {
                RenderCalendar();
            }
        }

        private void PreviousMonthButton_Click(object sender, RoutedEventArgs e)
        {
            MoveCalendarMonth(-1);
        }

        private void NextMonthButton_Click(object sender, RoutedEventArgs e)
        {
            MoveCalendarMonth(1);
        }

        private void MoveCalendarMonth(int offset)
        {
            DateTime current = new DateTime((int)YearComboBox.SelectedItem, MonthComboBox.SelectedIndex + 1, 1).AddMonths(offset);
            if (!YearComboBox.Items.Contains(current.Year))
            {
                YearComboBox.ItemsSource = Enumerable.Range(current.Year - 2, 6).ToList();
            }

            YearComboBox.SelectedItem = current.Year;
            MonthComboBox.SelectedIndex = current.Month - 1;
            RenderCalendar();
        }

        private void RenderCalendar()
        {
            if (MonthComboBox.SelectedIndex < 0 || YearComboBox.SelectedItem == null)
            {
                return;
            }

            calendarDays.Clear();
            int year = (int)YearComboBox.SelectedItem;
            int month = MonthComboBox.SelectedIndex + 1;
            DateTime firstDay = new DateTime(year, month, 1);
            LoadHolidays(firstDay, firstDay.AddMonths(1));
            DateTime start = firstDay.AddDays(-(int)firstDay.DayOfWeek);
            DateTime today = DateTime.Today;

            for (int index = 0; index < 42; index++)
            {
                DateTime date = start.AddDays(index);
                bool isCurrentMonth = date.Month == month;
                bool isHoliday = isCurrentMonth && holidays.Contains(date.Date);
                string dutyOfficerName = isCurrentMonth && dutyOfficersByDay.TryGetValue(date.Day, out string officerName)
                    ? officerName
                    : string.Empty;
                bool isDutyDay = !string.IsNullOrEmpty(dutyOfficerName);
                bool isToday = date.Date == today;
                calendarDays.Add(new DutyCalendarDay(date.Day.ToString(), dutyOfficerName, isCurrentMonth, isDutyDay, isToday, isHoliday, date.DayOfWeek));
            }
        }

        private void LoadHolidays(DateTime firstDay, DateTime nextMonthFirstDay)
        {
            holidays.Clear();
            const string query = @"SELECT HDY_DT
                                   FROM CCCCCHOD
                                   WHERE HDY_DT >= @START_DATE
                                     AND HDY_DT < @END_DATE";

            try
            {
                using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@START_DATE", firstDay.Date);
                    command.Parameters.AddWithValue("@END_DATE", nextMonthFirstDay.Date);
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read() && reader["HDY_DT"] != DBNull.Value)
                        {
                            DateTime holiday;
                            if (reader["HDY_DT"] is DateTime date)
                            {
                                holiday = date.Date;
                            }
                            else if (!TryParseHolidayDate(reader["HDY_DT"].ToString(), out holiday))
                            {
                                continue;
                            }

                            holidays.Add(holiday.Date);
                        }
                    }
                }
            }
            catch (MySqlException)
            {
                // 달력은 공휴일 조회에 실패해도 주말 색상과 당직 일정은 계속 표시합니다.
            }
        }

        private static bool TryParseHolidayDate(string value, out DateTime holiday)
        {
            string[] formats = { "yyyyMMdd", "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMddHHmmss", "yyyy-MM-dd HH:mm:ss" };
            if (DateTime.TryParseExact(value.Trim(), formats, null, System.Globalization.DateTimeStyles.None, out holiday)
                || DateTime.TryParse(value, out holiday))
            {
                return true;
            }

            holiday = default;
            return false;
        }
    }

    public class DutyCalendarDay
    {
        public DutyCalendarDay(string dayText, string dutyOfficerName, bool isCurrentMonth, bool isDutyDay, bool isToday, bool isHoliday, DayOfWeek dayOfWeek)
        {
            DayText = dayText;
            DutyOfficerName = dutyOfficerName;
            Background = isDutyDay ? new SolidColorBrush(Color.FromRgb(49, 49, 49)) : isToday ? new SolidColorBrush(Color.FromRgb(219, 234, 254)) : isCurrentMonth ? new SolidColorBrush(Color.FromRgb(248, 250, 252)) : Brushes.Transparent;
            DayForeground = GetDayForeground(isCurrentMonth, isDutyDay, isHoliday, dayOfWeek);
            DutyOfficerForeground = isDutyDay ? Brushes.White : DayForeground;
        }

        private static Brush GetDayForeground(bool isCurrentMonth, bool isDutyDay, bool isHoliday, DayOfWeek dayOfWeek)
        {
            if (!isCurrentMonth)
            {
                return new SolidColorBrush(Color.FromRgb(180, 185, 191));
            }

            if (isHoliday || dayOfWeek == DayOfWeek.Sunday)
            {
                return isDutyDay ? new SolidColorBrush(Color.FromRgb(252, 165, 165)) : new SolidColorBrush(Color.FromRgb(220, 38, 38));
            }

            if (dayOfWeek == DayOfWeek.Saturday)
            {
                return isDutyDay ? new SolidColorBrush(Color.FromRgb(147, 197, 253)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }

            return isDutyDay ? Brushes.White : new SolidColorBrush(Color.FromRgb(31, 41, 55));
        }

        public string DayText { get; private set; }
        public string DutyOfficerName { get; private set; }
        public Brush Background { get; private set; }
        public Brush DayForeground { get; private set; }
        public Brush DutyOfficerForeground { get; private set; }
    }
}
