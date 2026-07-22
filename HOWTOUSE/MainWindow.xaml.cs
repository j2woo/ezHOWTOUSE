using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HOWTOUSE
{
    public partial class MainWindow : Window
    {
        private DutyProgramView dutyProgramView;
        private PopupManualView popupManualView;
        private SuggestionView suggestionView;
        private SurveyView surveyView;
        private bool isLoggedIn;

        public MainWindow()
        {
            InitializeComponent();
            UpdateLoginArea();
            CreateDutyProgram("Manual");
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            string target = (sender as Button)?.Tag?.ToString() ?? "Duty";

            if (target == "DutyManual") CreateDutyProgram("Manual");
            if (target == "DutyToday") CreateDutyProgram("Today");
            if (target == "DutySchedule") CreateDutyProgram("Schedule");
            if (target == "DutySwap") CreateDutyProgram("Swap");
            if (target == "ManualPopup") CreatePopupManual("Popup");
            if (target == "ManualInquiry") CreatePopupManual("Inquiry");
            if (target == "Suggestion") CreateSuggestion();
            if (target == "Survey") CreateSurvey();
        }

        private void CreateDutyProgram(string selectedTab)
        {
            if (dutyProgramView == null)
            {
                dutyProgramView = new DutyProgramView();
            }

            MainContentControl.Content = dutyProgramView;
            dutyProgramView.SelectDutyTab(selectedTab);
            UpdateSelectedMenu("Duty" + selectedTab);
        }

        private void CreatePopupManual(string selectedTab)
        {
            if (popupManualView == null)
            {
                popupManualView = new PopupManualView();
            }

            MainContentControl.Content = popupManualView;
            popupManualView.SelectManualTab(selectedTab);
            UpdateSelectedMenu("Manual" + selectedTab);
        }

        private void CreateSuggestion()
        {
            if (suggestionView == null)
            {
                suggestionView = new SuggestionView();
            }

            MainContentControl.Content = suggestionView;
            UpdateSelectedMenu("Suggestion");
        }

        private void CreateSurvey()
        {
            if (surveyView == null)
            {
                surveyView = new SurveyView();
            }

            MainContentControl.Content = surveyView;
            UpdateSelectedMenu("Survey");
        }

        private void UpdateSelectedMenu(string selectedMenu)
        {
            ApplyMenuState(SuggestionNavButton, selectedMenu == "Suggestion");
            ApplyMenuState(SurveyNavButton, selectedMenu == "Survey");

            ApplySubMenuState(DutyManualNavButton, selectedMenu == "DutyManual");
            ApplySubMenuState(DutyTodayNavButton, selectedMenu == "DutyToday");
            ApplySubMenuState(DutyScheduleNavButton, selectedMenu == "DutySchedule");
            ApplySubMenuState(DutySwapNavButton, selectedMenu == "DutySwap");
            ApplySubMenuState(PopupManualNavButton, selectedMenu == "ManualPopup");
            ApplySubMenuState(InquiryNavButton, selectedMenu == "ManualInquiry");
        }

        private static void ApplyMenuState(Button button, bool isSelected)
        {
            button.Background = Brushes.Transparent;
            button.Foreground = isSelected ? new SolidColorBrush(Color.FromRgb(14, 145, 245)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }

        private static void ApplySubMenuState(Button button, bool isSelected)
        {
            button.Background = Brushes.Transparent;
            button.Foreground = isSelected ? new SolidColorBrush(Color.FromRgb(14, 145, 245)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }

        public void SetLoginUser(string employeeNo, string userName)
        {
            string displayName = string.IsNullOrWhiteSpace(userName) ? employeeNo : userName;

            LoginSession.SetUser(employeeNo, userName);

            isLoggedIn = true;
            LoginUserNameTextBlock.Text = displayName;
            LoginUserRoleTextBlock.Text = employeeNo;
            UserInitialTextBlock.Text = GetUserInitial(displayName);
            UpdateLoginArea();
        }

        private void UpdateLoginArea()
        {
            UserInfoPanel.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string GetUserInitial(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return "EZ";
            }

            return userName.Substring(0, 1).ToUpper();
        }
    }
}
