using HOWTOUSE.DAC;
using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace HOWTOUSE
{
    // 처음 실행하면 로그인 창이 뜨고, 로그인 성공하면 MainWindow.xaml이 뜨도록 설정하는 로직
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DapperConfig.Configure();
            base.OnStartup(e);

            LoginWindow loginWindow = new LoginWindow();

            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            string ipAddress = GetLocalIPAddress();
            SessionContext.SetUser(loginWindow.EmployeeNo, loginWindow.UserName, ipAddress); // 사용자정보 세션 저장

            MainWindow mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.SetLoginUser(loginWindow.EmployeeNo, loginWindow.UserName);

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            return string.Empty;
        }
    }

}
