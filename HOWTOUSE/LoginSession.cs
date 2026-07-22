namespace HOWTOUSE
{
    public static class LoginSession
    {
        public static string EmployeeNo { get; private set; }
        public static string UserName { get; private set; }

        public static void SetUser(string employeeNo, string userName)
        {
            EmployeeNo = employeeNo;
            UserName = userName;
        }
    }
}
