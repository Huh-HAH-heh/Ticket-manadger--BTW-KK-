namespace Ticket_manadger
{
    internal static class UserRoles
    {
        public const string Client = "Client";
        public const string Operator = "Operator";
        public const string Admin = "Admin";

        public static bool IsKnown(string? role) =>
            role is Client or Operator or Admin;

        public static string Display(string role) => role switch
        {
            Client => "Клиент",
            Operator => "Оператор",
            Admin => "Администратор",
            _ => "Неизвестная роль"
        };
    }
}
