using System.Text;

namespace Ticket_manadger;

internal sealed class ConsoleMenu
{
    private readonly TicketApiClient _api;

    public ConsoleMenu(TicketApiClient api) => _api = api;

    public async Task RunAsync(string[] args)
    {
        try
        {
            var health = await _api.CheckHealthAsync();
            Console.WriteLine($"Ticket Manager API: {health.Status}; PostgreSQL: {health.Database}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удаётся подключиться к API: {ex.Message}");
            Console.WriteLine("Сначала запустите отдельный проект Ticket Manager API.");
            Pause();
            return;
        }

        if (args.Any(arg => arg.Equals("--bootstrap-admin", StringComparison.OrdinalIgnoreCase)))
        {
            await BootstrapAdministratorAsync();
            return;
        }

        while (true)
        {
            var session = await AuthenticationMenuAsync();
            if (session is null)
            {
                Console.WriteLine("Работа завершена.");
                return;
            }

            _api.SetToken(session.Token);
            await RunUserSessionAsync(session.User);
            _api.SetToken(null);
        }
    }

    private async Task BootstrapAdministratorAsync()
    {
        var key = Environment.GetEnvironmentVariable("TICKET_BOOTSTRAP_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("Задайте TICKET_BOOTSTRAP_KEY в окружении консольного клиента. Значение должно совпадать с настройкой API.");
            Pause();
            return;
        }

        try
        {
            var fullName = ReadRequired("Имя и фамилия первого администратора: ");
            var login = ReadRequired("Логин: ");
            var email = ReadRequired("Email: ");
            var password = ReadSecret("Пароль (8–128 символов): ");
            if (password != ReadSecret("Повторите пароль: "))
                throw new ArgumentException("Пароли не совпадают.");

            var session = await _api.BootstrapAdminAsync(key, login, fullName, email, password);
            Console.WriteLine($"Создан первый администратор {session.User.Login}.");
        }
        catch (Exception ex) { Console.WriteLine($"Не удалось создать администратора: {ex.Message}"); }
        finally { Pause(); }
    }

    private async Task<AuthReply?> AuthenticationMenuAsync()
    {
        while (true)
        {
            ClearConsole();
            Console.WriteLine("====================================");
            Console.WriteLine("          TICKET MANAGER");
            Console.WriteLine("====================================");
            Console.WriteLine("1. Войти");
            Console.WriteLine("2. Зарегистрироваться как клиент");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите действие: ");
            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "0": return null;
                    case "1":
                    {
                        var login = ReadRequired("Логин: ");
                        var password = ReadSecret("Пароль: ");
                        var result = await _api.LoginAsync(login, password);
                        Console.WriteLine($"Вход выполнен: {result.User.FullName} ({RoleNames.Display(result.User.Role)}).");
                        Pause();
                        return result;
                    }
                    case "2":
                    {
                        Console.WriteLine("Публичная регистрация создаёт только клиента.");
                        var fullName = ReadRequired("Имя и фамилия: ");
                        var login = ReadRequired("Логин: ");
                        var email = ReadRequired("Email: ");
                        var password = ReadSecret("Пароль (8–128 символов): ");
                        if (password != ReadSecret("Повторите пароль: "))
                            throw new ArgumentException("Пароли не совпадают.");
                        var result = await _api.RegisterAsync(login, fullName, email, password);
                        Console.WriteLine("Регистрация завершена. Выполнен вход как клиент.");
                        Pause();
                        return result;
                    }
                    default:
                        Console.WriteLine("Введите 1, 2 или 0.");
                        Pause();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Операция не выполнена: {ex.Message}");
                Pause();
            }
        }
    }

    private async Task RunUserSessionAsync(ConsoleUser user)
    {
        while (true)
        {
            PrintMenu(user);
            Console.Write("Выберите действие: ");
            var choice = Console.ReadLine()?.Trim();
            if (choice == "0") { Console.WriteLine("Выход из учётной записи."); Pause(); return; }

            if (user.Role == RoleNames.Client)
            {
                switch (choice)
                {
                    case "1": await RunSafelyAsync(() => ShowRequestsAsync()); break;
                    case "2": await RunSafelyAsync(() => CreateOwnRequestAsync()); break;
                    case "3": await RunSafelyAsync(() => SearchRequestsAsync()); break;
                    case "4": await RunSafelyAsync(() => CommentsWorkflowAsync()); break;
                    default: UnknownChoice(); break;
                }
            }
            else if (user.Role == RoleNames.Operator)
            {
                switch (choice)
                {
                    case "1": await RunSafelyAsync(() => ShowRequestsAsync()); break;
                    case "2": await RunSafelyAsync(() => SearchRequestsAsync()); break;
                    case "3": await RunSafelyAsync(() => ClaimRequestAsync()); break;
                    case "4": await RunSafelyAsync(() => ChangeStatusAsync()); break;
                    case "5": await RunSafelyAsync(() => EditRequestAsync()); break;
                    case "6": await RunSafelyAsync(() => CommentsWorkflowAsync()); break;
                    case "7": await RunSafelyAsync(() => ShowHistoryAsync()); break;
                    case "8": await RunSafelyAsync(() => ShowOverdueAsync()); break;
                    default: UnknownChoice(); break;
                }
            }
            else if (user.Role == RoleNames.Admin)
            {
                switch (choice)
                {
                    case "1": await RunSafelyAsync(() => ShowRequestsAsync()); break;
                    case "2": await RunSafelyAsync(() => SearchRequestsAsync()); break;
                    case "3": await RunSafelyAsync(() => CreateRequestForClientAsync()); break;
                    case "4": await RunSafelyAsync(() => EditRequestAsync()); break;
                    case "5": await RunSafelyAsync(() => ChangeStatusAsync()); break;
                    case "6": await RunSafelyAsync(() => DeleteRequestAsync()); break;
                    case "7": await RunSafelyAsync(() => AssignExecutorAsync()); break;
                    case "8": await ManageUsersAsync(user); break;
                    case "9": await RunSafelyAsync(() => ShowReportsAsync()); break;
                    case "10": await RunSafelyAsync(() => ShowOverdueAsync()); break;
                    case "11": await RunSafelyAsync(() => CommentsWorkflowAsync()); break;
                    case "12": await RunSafelyAsync(() => ShowHistoryAsync()); break;
                    default: UnknownChoice(); break;
                }
            }
            else
            {
                Console.WriteLine("Неизвестная роль. Доступ закрыт.");
                Pause();
                return;
            }
        }
    }

    private static void PrintMenu(ConsoleUser user)
    {
        ClearConsole();
        Console.WriteLine("====================================");
        Console.WriteLine($"TICKET MANAGER — {user.FullName}");
        Console.WriteLine($"Роль: {RoleNames.Display(user.Role)}");
        Console.WriteLine("====================================");

        if (user.Role == RoleNames.Client)
        {
            Console.WriteLine("1. Мои заявки");
            Console.WriteLine("2. Создать заявку");
            Console.WriteLine("3. Поиск и фильтрация моих заявок");
            Console.WriteLine("4. Комментарии к заявке");
        }
        else if (user.Role == RoleNames.Operator)
        {
            Console.WriteLine("1. Очередь заявок");
            Console.WriteLine("2. Поиск и фильтрация");
            Console.WriteLine("3. Взять свободную заявку себе");
            Console.WriteLine("4. Изменить статус");
            Console.WriteLine("5. Редактировать заявку");
            Console.WriteLine("6. Комментарии к заявке");
            Console.WriteLine("7. История заявки");
            Console.WriteLine("8. Просроченные заявки");
        }
        else
        {
            Console.WriteLine("1. Все заявки");
            Console.WriteLine("2. Поиск и фильтрация");
            Console.WriteLine("3. Создать заявку для клиента");
            Console.WriteLine("4. Редактировать заявку");
            Console.WriteLine("5. Изменить статус");
            Console.WriteLine("6. Удалить ошибочную заявку");
            Console.WriteLine("7. Назначить / снять исполнителя");
            Console.WriteLine("8. Управление пользователями и ролями");
            Console.WriteLine("9. Аналитические отчёты");
            Console.WriteLine("10. Просроченные заявки");
            Console.WriteLine("11. Комментарии к заявке");
            Console.WriteLine("12. История заявки");
        }

        Console.WriteLine("0. Выход из учётной записи");
        Console.WriteLine("------------------------------------");
    }

    private async Task ShowRequestsAsync(List<ConsoleRequest>? requests = null)
    {
        requests ??= await _api.GetRequestsAsync();
        if (requests.Count == 0) { Console.WriteLine("Заявки не найдены."); return; }

        Console.WriteLine();
        Console.WriteLine($"{"ID",4} | {"Название",-26} | {"Статус",-12} | {"Приор.",6} | {"Клиент",-18} | {"Исполнитель",-18} | Создана (UTC)");
        Console.WriteLine(new string('-', 122));
        foreach (var r in requests)
        {
            Console.WriteLine($"{r.Id,4} | {Shorten(r.Title,26),-26} | {Shorten(r.Status,12),-12} | {PriorityName(r.Priority),6} | {Shorten(r.ClientName,18),-18} | {Shorten(r.ExecutorName ?? "Не назначен",18),-18} | {r.CreatedAt:yyyy-MM-dd HH:mm}");
        }
    }

    private async Task CreateOwnRequestAsync()
    {
        var title = ReadRequired("Название: ");
        var description = ReadRequired("Описание: ");
        var priority = ReadPriority();
        var created = await _api.CreateRequestAsync(title, description, priority);
        Console.WriteLine($"Создана заявка #{created.Id}.");
    }

    private async Task CreateRequestForClientAsync()
    {
        var users = await _api.GetUsersAsync();
        var clients = users.Where(user => !user.IsDeleted && user.Role == RoleNames.Client).ToList();
        if (clients.Count == 0) { Console.WriteLine("Нет активных клиентов."); return; }
        foreach (var client in clients)
            Console.WriteLine($"{client.Id}. {client.FullName} ({client.Login})");

        var clientId = ReadPositiveInt("ID клиента: ");
        var created = await _api.CreateRequestAsync(
            ReadRequired("Название: "),
            ReadRequired("Описание: "),
            ReadPriority(),
            clientId);
        Console.WriteLine($"Создана заявка #{created.Id}.");
    }

    private async Task SearchRequestsAsync()
    {
        Console.Write("Текст поиска (Enter — любой текст): ");
        var search = Console.ReadLine();
        Console.WriteLine("Статус: 0 — любой статус");
        foreach (var status in await _api.GetStatusesAsync())
            Console.WriteLine($"{status.Id}. {status.Name}");
        var statusChoice = ReadNonNegativeInt("ID статуса: ");
        int? statusId = statusChoice == 0 ? null : statusChoice;

        Console.Write("Минимальная дата создания, YYYY-MM-DD (Enter — без ограничения): ");
        var fromInput = Console.ReadLine();
        DateTime? from = ParseDate(fromInput, endOfDay: false);
        Console.Write("Максимальная дата создания, YYYY-MM-DD (Enter — без ограничения): ");
        var toInput = Console.ReadLine();
        DateTime? to = ParseDate(toInput, endOfDay: true);

        var results = await _api.GetRequestsAsync(
            search, statusId, from: from, to: to);
        await ShowRequestsAsync(results);
    }

    private async Task EditRequestAsync()
    {
        var id = ReadPositiveInt("ID заявки: ");
        var request = (await _api.GetRequestsAsync()).FirstOrDefault(item => item.Id == id);
        if (request is null) { Console.WriteLine("Заявка не найдена или нет доступа."); return; }

        var title = ReadOptional($"Название [{request.Title}]: ", request.Title);
        var description = ReadOptional($"Описание [{request.Description}]: ", request.Description);
        Console.WriteLine($"Текущий приоритет: {PriorityName(request.Priority)}");
        var priorityInput = ReadOptional("Новый приоритет 1–4 (Enter — оставить): ", request.Priority.ToString());
        if (!int.TryParse(priorityInput, out var priority) || priority is < 1 or > 4)
            throw new ArgumentException("Приоритет должен быть числом от 1 до 4.");

        await _api.UpdateRequestAsync(id, title, description, priority);
        Console.WriteLine("Заявка обновлена.");
    }

    private async Task ChangeStatusAsync()
    {
        var id = ReadPositiveInt("ID заявки: ");
        foreach (var status in await _api.GetStatusesAsync())
            Console.WriteLine($"{status.Id}. {status.Name}");
        var statusId = ReadPositiveInt("Новый статус (ID): ");
        await _api.ChangeStatusAsync(id, statusId);
        Console.WriteLine("Статус изменён.");
    }

    private async Task ClaimRequestAsync()
    {
        await ShowRequestsAsync();
        var id = ReadPositiveInt("ID свободной заявки: ");
        var request = await _api.ClaimAsync(id);
        Console.WriteLine($"Заявка #{request.Id} назначена вам; статус изменён на «{request.Status}».");
    }

    private async Task AssignExecutorAsync()
    {
        await ShowRequestsAsync();
        var id = ReadPositiveInt("ID заявки: ");
        Console.WriteLine("Исполнители будут выбираться из списка активных операторов.");
        var users = await _api.GetUsersAsync();
        var operators = users.Where(user => !user.IsDeleted && user.Role == RoleNames.Operator).ToList();
        foreach (var op in operators) Console.WriteLine($"{op.Id}. {op.FullName} ({op.Login})");
        Console.WriteLine("0. Снять назначение");
        var executorId = ReadNonNegativeInt("ID исполнителя: ");
        await _api.AssignExecutorAsync(id, executorId == 0 ? null : executorId);
        Console.WriteLine("Назначение обновлено.");
    }

    private async Task DeleteRequestAsync()
    {
        var id = ReadPositiveInt("ID ошибочной заявки: ");
        var request = (await _api.GetRequestsAsync()).FirstOrDefault(item => item.Id == id);
        if (request is null) { Console.WriteLine("Заявка не найдена."); return; }
        Console.Write($"Удалить заявку #{id} «{request.Title}»? Введите ДА: ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "ДА", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Удаление отменено.");
            return;
        }
        await _api.DeleteRequestAsync(id);
        Console.WriteLine("Заявка удалена.");
    }

    private async Task CommentsWorkflowAsync()
    {
        var id = ReadPositiveInt("ID заявки: ");
        var comments = await _api.GetCommentsAsync(id);
        Console.WriteLine("КОММЕНТАРИИ");
        if (comments.Count == 0) Console.WriteLine("Комментариев пока нет.");
        foreach (var item in comments)
            Console.WriteLine($"[{item.CreatedAt:yyyy-MM-dd HH:mm}] {item.Author}: {item.Text}");

        Console.Write("Добавить комментарий? (Д/н): ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "Д", StringComparison.OrdinalIgnoreCase))
            return;
        var added = await _api.AddCommentAsync(id, ReadRequired("Текст комментария (до 5000 символов): "));
        Console.WriteLine($"Комментарий добавлен ({added.CreatedAt:yyyy-MM-dd HH:mm}).");
    }

    private async Task ShowHistoryAsync()
    {
        var id = ReadPositiveInt("ID заявки: ");
        var history = await _api.GetHistoryAsync(id);
        Console.WriteLine("ИСТОРИЯ ОБРАБОТКИ");
        if (history.Count == 0) Console.WriteLine("Записей истории нет.");
        foreach (var item in history)
            Console.WriteLine($"[{item.CreatedAt:yyyy-MM-dd HH:mm}] {item.Actor} / {item.Action}: {item.Details}");
    }

    private async Task ShowOverdueAsync()
    {
        var days = ReadPositiveInt("Заявки старше скольких дней считать просроченными? ");
        var requests = await _api.GetOverdueAsync(days);
        await ShowRequestsAsync(requests);
    }

    private async Task ShowReportsAsync()
    {
        Console.WriteLine("ЗАЯВКИ ПО СТАТУСАМ");
        var statuses = await _api.GetStatusReportAsync();
        foreach (var item in statuses) Console.WriteLine($"  {item.Status}: {item.Count}");

        Console.WriteLine();
        Console.WriteLine("ЗАЯВКИ ПО КЛИЕНТАМ");
        var clients = await _api.GetClientReportAsync();
        foreach (var item in clients) Console.WriteLine($"  {item.Client}: {item.Count}");

        Console.WriteLine();
        Console.WriteLine($"Всего заявок: {(await _api.GetRequestsAsync()).Count}");
    }

    private async Task ManageUsersAsync(ConsoleUser admin)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("УПРАВЛЕНИЕ УЧЁТНЫМИ ЗАПИСЯМИ");
            Console.WriteLine("1. Список пользователей");
            Console.WriteLine("2. Создать пользователя и назначить роль");
            Console.WriteLine("3. Изменить роль");
            Console.WriteLine("4. Деактивировать пользователя");
            Console.WriteLine("0. Назад");
            var choice = ReadChoice("Действие: ", "0", "1", "2", "3", "4");
            if (choice == "0") return;

            await RunSafelyAsync(async () =>
            {
                switch (choice)
                {
                    case "1": await ShowUsersAsync(); break;
                    case "2": await CreateUserAsync(); break;
                    case "3": await ChangeUserRoleAsync(admin); break;
                    case "4": await DeactivateUserAsync(admin); break;
                }
            });
        }
    }

    private async Task ShowUsersAsync()
    {
        var users = await _api.GetUsersAsync();
        Console.WriteLine($"{"ID",4} | {"Логин",-18} | {"Имя",-24} | {"Email",-30} | {"Роль",-14} | Состояние");
        foreach (var user in users)
            Console.WriteLine($"{user.Id,4} | {Shorten(user.Login ?? "—",18),-18} | {Shorten(user.FullName,24),-24} | {Shorten(user.Email ?? "—",30),-30} | {RoleNames.Display(user.Role),-14} | {(user.IsDeleted ? "отключён" : "активен")}");
    }

    private async Task CreateUserAsync()
    {
        var role = ReadRole();
        var fullName = ReadRequired("Имя и фамилия: ");
        var login = ReadRequired("Логин: ");
        var email = ReadRequired("Email: ");
        var password = ReadSecret("Пароль: ");
        if (password != ReadSecret("Повторите пароль: ")) throw new ArgumentException("Пароли не совпадают.");

        var user = await _api.CreateUserAsync(login, fullName, email, password, role);
        Console.WriteLine($"Создан пользователь {user.Login}, роль: {RoleNames.Display(user.Role)}.");
    }

    private async Task ChangeUserRoleAsync(ConsoleUser admin)
    {
        await ShowUsersAsync();
        var id = ReadPositiveInt("ID пользователя: ");
        var role = ReadRole();
        await _api.ChangeUserRoleAsync(id, role);
        Console.WriteLine("Роль обновлена.");
    }

    private async Task DeactivateUserAsync(ConsoleUser admin)
    {
        await ShowUsersAsync();
        var id = ReadPositiveInt("ID пользователя: ");
        Console.Write("Учётная запись будет отключена, история заявок сохранится. Продолжить (ДА)? ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "ДА", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Операция отменена.");
            return;
        }
        await _api.DeactivateUserAsync(id);
        Console.WriteLine("Пользователь деактивирован.");
    }

    private static string ReadRole() => ReadChoice("Роль: 1 — клиент, 2 — оператор, 3 — администратор: ", "1", "2", "3") switch
    {
        "2" => RoleNames.Operator,
        "3" => RoleNames.Admin,
        _ => RoleNames.Client
    };

    private static int ReadPriority()
    {
        Console.WriteLine("Приоритет: 1 — низкий, 2 — обычный, 3 — высокий, 4 — критический.");
        var priority = ReadPositiveInt("Приоритет (1–4): ");
        if (priority is < 1 or > 4) throw new ArgumentException("Приоритет должен быть от 1 до 4.");
        return priority;
    }

    private static string PriorityName(int priority) => priority switch
    {
        1 => "Низкий",
        2 => "Обычный",
        3 => "Высокий",
        4 => "Критич.",
        _ => "?"
    };

    private static DateTime? ParseDate(string? value, bool endOfDay)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTime.TryParse(value, out var date))
            throw new ArgumentException("Дата должна быть в формате YYYY-MM-DD.");
        date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        return endOfDay ? date.AddDays(1).AddTicks(-1) : date;
    }

    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private static string ReadRequired(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            var value = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            Console.WriteLine("Значение не может быть пустым.");
        }
    }

    private static string ReadOptional(string prompt, string current)
    {
        Console.Write(prompt);
        var value = Console.ReadLine();
        return string.IsNullOrWhiteSpace(value) ? current : value.Trim();
    }

    private static int ReadPositiveInt(string prompt)
    {
        while (true)
        {
            var value = ReadRequired(prompt);
            if (int.TryParse(value, out var number) && number > 0) return number;
            Console.WriteLine("Введите целое число больше нуля.");
        }
    }

    private static int ReadNonNegativeInt(string prompt)
    {
        while (true)
        {
            var value = ReadRequired(prompt);
            if (int.TryParse(value, out var number) && number >= 0) return number;
            Console.WriteLine("Введите целое число 0 или больше.");
        }
    }

    private static string ReadChoice(string prompt, params string[] choices)
    {
        while (true)
        {
            var value = ReadRequired(prompt);
            if (choices.Contains(value)) return value;
            Console.WriteLine($"Допустимые варианты: {string.Join(", ", choices)}.");
        }
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected) return Console.ReadLine() ?? string.Empty;
        var result = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return result.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (result.Length > 0) result.Length--; continue; }
            if (!char.IsControl(key.KeyChar)) result.Append(key.KeyChar);
        }
    }

    private static async Task RunSafelyAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Console.WriteLine($"Операция не выполнена: {ex.Message}"); }
        finally { Pause(); }
    }

    private static void UnknownChoice() { Console.WriteLine("Неизвестный пункт меню."); Pause(); }
    private static void ClearConsole() { try { Console.Clear(); } catch (IOException) { } }
    private static void Pause() { Console.WriteLine(); Console.Write("Нажмите Enter, чтобы продолжить..."); Console.ReadLine(); }
}
