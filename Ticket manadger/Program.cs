using Ticket_manadger;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

using var api = new TicketApiClient();
try
{
    await new ConsoleMenu(api).RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Приложение завершилось из-за ошибки: {ex.Message}");
    Environment.ExitCode = 1;
}
