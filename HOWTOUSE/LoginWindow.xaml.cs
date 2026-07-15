using MySql.Data.MySqlClient;
using System;
using System.Windows;
using System.Windows.Input;

namespace HOWTOUSE
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            EmployeeNoTextBox.Focus();
        }

        public string EmployeeNo { get; private set; }

        public string UserName { get; private set; }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string employeeNo = EmployeeNoTextBox.Text.Trim();
            string password = PasswordInput.Password;

            if (string.IsNullOrWhiteSpace(employeeNo) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("사번과 비밀번호를 입력하세요.", "EZHOWTOUSE", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                LoginUser loginUser = FindLoginUser(employeeNo, password);

                if (loginUser == null)
                {
                    MessageBox.Show("사번 또는 비밀번호를 확인해주세요.", "EZHOWTOUSE", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PasswordInput.Clear();
                    PasswordInput.Focus();
                    return;
                }

                UpdateLastLoginTime(employeeNo);

                EmployeeNo = loginUser.EmployeeNo;
                UserName = loginUser.UserName;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"로그인 처리 중 오류가 발생했습니다.\n\n{ex.Message}", "EZHOWTOUSE", MessageBoxButton.OK, MessageBoxImage.Error);
                PasswordInput.Clear();
                PasswordInput.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static LoginUser FindLoginUser(string employeeNo, string password)
        {
            const string query = @"
            SELECT EMP_NO, USER_NM
                FROM CNLRRUSD
                WHERE STF_NO = @EMP_NO
                AND LGIN_PWD = @PASSWORD
                LIMIT 1";

            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@EMP_NO", employeeNo);
                command.Parameters.AddWithValue("@PASSWORD", password);

                connection.Open();

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new LoginUser
                    {
                        EmployeeNo = reader["EMP_NO"].ToString(),
                        UserName = reader["USER_NM"].ToString()
                    };
                }
            }
        }

        private static void UpdateLastLoginTime(string employeeNo)
        {
            const string query = @"
            UPDATE CNLRRUSD
               SET LAST_LOGIN_DTM = NOW()
             WHERE EMP_NO = @EMP_NO";

            using (MySqlConnection connection = new MySqlConnection(AppSettings.Current.Database.ConnectionString))
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@EMP_NO", employeeNo);

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private sealed class LoginUser
        {
            public string EmployeeNo { get; set; }

            public string UserName { get; set; }
        }
    }
}
