using Ticket_manadger;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

try
{
    var menu = new ConsoleMenu();
    if (args.Any(argument => string.Equals(argument, "--bootstrap-admin", StringComparison.OrdinalIgnoreCase)))
    {
        menu.BootstrapAdministrator();
        return;
    }

    menu.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Приложение завершилось из-за ошибки: {ex.Message}");
    Environment.ExitCode = 1;
}
