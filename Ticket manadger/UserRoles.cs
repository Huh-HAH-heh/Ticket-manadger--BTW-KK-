namespace Ticket_manadger
{
    internal static class UserRoles
    {
        public const string Client = "Client";
        public const string Operator = "Operator";
        public const string Admin = "Admin";

        public static bool IsKnown(string? role) =>
            string.Equals(role, Client, StringComparison.Ordinal) ||
            string.Equals(role, Operator, StringComparison.Ordinal) ||
            string.Equals(role, Admin, StringComparison.Ordinal);

        public static string Display(string role) => role switch
        {
            Client => "Клиент",
            Operator => "Оператор",
            Admin => "Администратор",
            _ => "Неизвестная роль"
        };
    }
}
