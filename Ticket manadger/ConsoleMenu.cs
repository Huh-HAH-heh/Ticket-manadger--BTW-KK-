using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text;

namespace Ticket_manadger
{
    internal sealed class ConsoleMenu
    {
        private readonly RequestRepo _requests = new();
        private readonly UserRepository _users = new();
        private readonly AuthService _auth = new();

        public void Run()
        {
            if (!CheckDatabase()) return;

            while (true)
            {
                var user = AuthenticationMenu();
                if (user is null)
                {
                    Console.WriteLine("Работа завершена.");
                    return;
                }
                RunUserSession(user);
            }
        }

        public void BootstrapAdministrator()
        {
            if (!CheckDatabase()) return;
            if (_users.HasActiveAdministrator())
            {
                Console.WriteLine("Активный администратор уже существует. Первичная настройка запрещена.");
                Pause();
                return;
            }

            Console.WriteLine("Создание первой учётной записи администратора.");
            try
            {
                var fullName = ReadRequired("Имя и фамилия: ");
                var login = ReadRequired("Логин: ");
                var email = ReadRequired("Email: ");
                var password = ReadSecret("Пароль (минимум 8 символов): ");
                if (password != ReadSecret("Повторите пароль: "))
                    throw new ArgumentException("Пароли не совпадают.");

                var admin = _auth.BootstrapFirstAdministrator(login, fullName, email, password);
                Console.WriteLine($"Создан администратор {admin.Login}. Теперь запустите приложение обычной командой.");
            }
            catch (Exception ex) { Console.WriteLine($"Не удалось создать администратора: {ex.Message}"); }
            finally { Pause(); }
        }

        private User? AuthenticationMenu()
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
                    if (choice == "0") return null;
                    if (choice == "1")
                    {
                        var login = ReadRequired("Логин: ");
                        var password = ReadSecret("Пароль: ");
                        var user = _auth.Authenticate(login, password);
                        if (user is null)
                        {
                            Console.WriteLine("Неверный логин/пароль или учётная запись деактивирована.");
                            Pause();
                            continue;
                        }
                        Console.WriteLine($"Здравствуйте, {user.FullName} ({UserRoles.Display(user.Role)}).");
                        Pause();
                        return user;
                    }
                    if (choice == "2")
                    {
                        Console.WriteLine("Публичная регистрация создаёт только клиента.");
                        var fullName = ReadRequired("Имя и фамилия: ");
                        var login = ReadRequired("Логин: ");
                        var email = ReadRequired("Email: ");
                        var password = ReadSecret("Пароль (минимум 8 символов): ");
                        if (password != ReadSecret("Повторите пароль: "))
                            throw new ArgumentException("Пароли не совпадают.");

                        var user = _auth.RegisterClient(login, fullName, email, password);
                        Console.WriteLine("Регистрация успешна. Вы вошли как клиент.");
                        Pause();
                        return user;
                    }
                    Console.WriteLine("Введите 1, 2 или 0.");
                    Pause();
                }
                catch (ArgumentException ex) { Console.WriteLine($"Проверьте данные: {ex.Message}"); Pause(); }
                catch (DbUpdateException ex) { Console.WriteLine($"Не удалось сохранить данные: {ex.GetBaseException().Message}"); Pause(); }
                catch (NpgsqlException ex) { Console.WriteLine($"Ошибка PostgreSQL: {ex.Message}"); Pause(); }
                catch (Exception ex) { Console.WriteLine($"Операция не выполнена: {ex.Message}"); Pause(); }
            }
        }

        private void RunUserSession(User user)
        {
            while (true)
            {
                PrintUserMenu(user);
                Console.Write("Выберите действие: ");
                var choice = Console.ReadLine()?.Trim();
                if (choice == "0") { Console.WriteLine("Вы вышли из учётной записи."); Pause(); return; }

                if (user.Role == UserRoles.Client)
                {
                    switch (choice)
                    {
                        case "1": RunSafely(() => ShowRequests(_requests.GetAllRequest(user.Id))); break;
                        case "2": RunSafely(() => CreateRequest(user)); break;
                        case "3": RunSafely(() => SearchRequests(user.Id)); break;
                        default: UnknownChoice(); break;
                    }
                }
                else if (user.Role == UserRoles.Operator)
                {
                    switch (choice)
                    {
                        case "1": RunSafely(() => ShowRequests(_requests.GetAllRequest())); break;
                        case "2": RunSafely(() => SearchRequests(null)); break;
                        case "3": RunSafely(ChangeRequestStatus); break;
                        case "4": RunSafely(EditRequest); break;
                        default: UnknownChoice(); break;
                    }
                }
                else if (user.Role == UserRoles.Admin)
                {
                    switch (choice)
                    {
                        case "1": RunSafely(() => ShowRequests(_requests.GetAllRequest())); break;
                        case "2": RunSafely(() => SearchRequests(null)); break;
                        case "3": RunSafely(CreateRequestForChosenClient); break;
                        case "4": RunSafely(EditRequest); break;
                        case "5": RunSafely(ChangeRequestStatus); break;
                        case "6": RunSafely(DeleteRequest); break;
                        case "7": ManageUsers(user); break;
                        case "8": RunSafely(ShowStatistics); break;
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

        private static void PrintUserMenu(User user)
        {
            ClearConsole();
            Console.WriteLine("====================================");
            Console.WriteLine($"TICKET MANAGER — {user.FullName}");
            Console.WriteLine($"Роль: {UserRoles.Display(user.Role)}");
            Console.WriteLine("====================================");
            Console.WriteLine("1. " + (user.Role == UserRoles.Client ? "Мои заявки" : "Все заявки"));
            Console.WriteLine("2. " + (user.Role == UserRoles.Client ? "Создать заявку" : "Поиск, фильтрация и сортировка"));
            if (user.Role == UserRoles.Client) Console.WriteLine("3. Поиск моих заявок");
            if (user.Role == UserRoles.Operator)
            {
                Console.WriteLine("3. Изменить статус заявки");
                Console.WriteLine("4. Редактировать заявку");
            }
            if (user.Role == UserRoles.Admin)
            {
                Console.WriteLine("3. Создать заявку для клиента");
                Console.WriteLine("4. Редактировать заявку");
                Console.WriteLine("5. Изменить статус заявки");
                Console.WriteLine("6. Удалить заявку");
                Console.WriteLine("7. Управление пользователями и ролями");
                Console.WriteLine("8. Статистика и аналитика");
            }
            Console.WriteLine("0. Выход из учётной записи");
        }

        private static bool CheckDatabase()
        {
            try
            {
                using var db = new AppDbContext();
                if (!db.Database.CanConnect())
                {
                    Console.WriteLine("Не удалось подключиться к PostgreSQL.");
                    PrintConnectionHelp(); Pause(); return false;
                }
                var pending = db.Database.GetPendingMigrations().ToArray();
                if (pending.Length > 0)
                {
                    Console.WriteLine("В базе есть неприменённые миграции:");
                    foreach (var migration in pending) Console.WriteLine($"  - {migration}");
                    Console.WriteLine("Примените: dotnet ef database update --project \"Ticket manadger/Ticket manadger.csproj\"");
                    Pause(); return false;
                }
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message); PrintConnectionHelp(); Pause(); return false;
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Ошибка PostgreSQL: {ex.Message}"); PrintConnectionHelp(); Pause(); return false;
            }
        }

        private static void PrintConnectionHelp()
        {
            Console.WriteLine("Установите TICKET_DB_CONNECTION, например:");
            Console.WriteLine("$env:TICKET_DB_CONNECTION = \"Host=localhost;Port=5433;Database=ticket_db;Username=postgres;Password=ВАШ_ПАРОЛЬ\"");
        }

        private void CreateRequest(User user)
        {
            var title = ReadRequired("Название (до 100 символов): ");
            var description = ReadRequired("Описание: ");
            _requests.AddRequest(title, description, user.Id);
            Console.WriteLine("Заявка успешно создана.");
        }

        private void CreateRequestForChosenClient()
        {
            var clients = _requests.GetClients();
            if (clients.Count == 0) { Console.WriteLine("Нет активных клиентов."); return; }
            foreach (var client in clients)
                Console.WriteLine($"  {client.Id}. {client.FullName} ({client.Login ?? "старый аккаунт"})");
            var clientId = ReadPositiveInt("ID клиента: ");
            _requests.AddRequest(ReadRequired("Название: "), ReadRequired("Описание: "), clientId);
            Console.WriteLine("Заявка создана.");
        }

        private void EditRequest()
        {
            var id = ReadPositiveInt("ID заявки: ");
            var request = _requests.GetRequestById(id);
            if (request is null) { Console.WriteLine("Заявка не найдена."); return; }
            var title = ReadOptional($"Название [{request.Title}]: ", request.Title);
            var description = ReadOptional($"Описание [{request.description}]: ", request.description);
            Console.WriteLine(_requests.UpdateRequest(id, title, description) ? "Заявка обновлена." : "Заявка не найдена.");
        }

        private void ChangeRequestStatus()
        {
            var id = ReadPositiveInt("ID заявки: ");
            foreach (var status in _requests.GetStatuses()) Console.WriteLine($"  {status.Id}. {status.Name}");
            var statusId = ReadPositiveInt("Новый статус (ID): ");
            Console.WriteLine(_requests.UpdateStatus(id, statusId) ? "Статус обновлён." : "Заявка не найдена.");
        }

        private void DeleteRequest()
        {
            var id = ReadPositiveInt("ID заявки для удаления: ");
            var request = _requests.GetRequestById(id);
            if (request is null) { Console.WriteLine("Заявка не найдена."); return; }
            Console.Write($"Удалить \"{request.Title}\"? Введите ДА: ");
            if (!string.Equals(Console.ReadLine()?.Trim(), "ДА", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Удаление отменено."); return;
            }
            Console.WriteLine(_requests.DeleteRequest(id) ? "Заявка удалена." : "Заявка уже отсутствует.");
        }

        private void SearchRequests(int? clientScopeId)
        {
            Console.Write("Поиск (Enter — без текста): ");
            var search = Console.ReadLine();
            Console.WriteLine("Статус: 0 — все статусы");
            foreach (var status in _requests.GetStatuses()) Console.WriteLine($"  {status.Id}. {status.Name}");
            var statusChoice = ReadNonNegativeInt("ID статуса: ");
            int? statusId = statusChoice == 0 ? null : statusChoice;

            Console.WriteLine("Сортировка: 1 — новые, 2 — старые, 3 — название А—Я, 4 — название Я—А, 5 — статус");
            var sort = ReadChoice("Вариант: ", "1", "2", "3", "4", "5") switch
            {
                "2" => RequestSortOrder.OldestFirst,
                "3" => RequestSortOrder.TitleAscending,
                "4" => RequestSortOrder.TitleDescending,
                "5" => RequestSortOrder.Status,
                _ => RequestSortOrder.NewestFirst
            };
            ShowRequests(_requests.FindRequests(search, statusId, sort, clientScopeId));
        }

        private void ShowStatistics()
        {
            Console.WriteLine($"Всего заявок: {_requests.GetAllRequest().Count}");
            Console.WriteLine("Количество по статусам:");
            foreach (var item in _requests.GetLog().OrderBy(item => item.Key))
                Console.WriteLine($"  {item.Key}: {item.Value}");
            Console.WriteLine("Количество заявок по клиентам:");
            foreach (var item in _requests.GetClientRequestCounts())
                Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        private void ManageUsers(User admin)
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("УПРАВЛЕНИЕ ПОЛЬЗОВАТЕЛЯМИ");
                Console.WriteLine("1. Список учётных записей");
                Console.WriteLine("2. Создать пользователя с ролью");
                Console.WriteLine("3. Изменить роль");
                Console.WriteLine("4. Деактивировать пользователя");
                Console.WriteLine("0. Назад");
                switch (ReadChoice("Действие: ", "0", "1", "2", "3", "4"))
                {
                    case "0": return;
                    case "1": RunSafely(ShowUsers); break;
                    case "2": RunSafely(CreateUserByAdministrator); break;
                    case "3": RunSafely(() => ChangeUserRole(admin)); break;
                    case "4": RunSafely(() => DeactivateUser(admin)); break;
                }
            }
        }

        private void ShowUsers()
        {
            foreach (var user in _users.GetUsersForAdministrator())
            {
                var state = user.IsDeleted ? "отключён" : "активен";
                Console.WriteLine($"{user.Id}. {user.Login ?? "—"} | {user.FullName} | {user.Email ?? "—"} | {UserRoles.Display(user.Role)} | {state}");
            }
            Console.WriteLine("Хэши паролей не отображаются.");
        }

        private void CreateUserByAdministrator()
        {
            var role = ReadChoice("Роль: 1 — клиент, 2 — оператор, 3 — администратор: ", "1", "2", "3") switch
            {
                "2" => UserRoles.Operator, "3" => UserRoles.Admin, _ => UserRoles.Client
            };
            var fullName = ReadRequired("Имя и фамилия: ");
            var login = ReadRequired("Логин: ");
            var email = ReadRequired("Email: ");
            var password = ReadSecret("Пароль: ");
            if (password != ReadSecret("Повторите пароль: ")) throw new ArgumentException("Пароли не совпадают.");
            var created = _auth.CreateAccountByAdministrator(login, fullName, email, password, role);
            Console.WriteLine($"Создан пользователь {created.Login} — {UserRoles.Display(created.Role)}.");
        }

        private void ChangeUserRole(User admin)
        {
            ShowUsers();
            var id = ReadPositiveInt("ID пользователя: ");
            var role = ReadChoice("Новая роль: 1 — клиент, 2 — оператор, 3 — администратор: ", "1", "2", "3") switch
            {
                "2" => UserRoles.Operator, "3" => UserRoles.Admin, _ => UserRoles.Client
            };
            Console.WriteLine(_users.ChangeRole(id, role, admin.Id) ? "Роль обновлена." : "Пользователь не найден.");
        }

        private void DeactivateUser(User admin)
        {
            ShowUsers();
            var id = ReadPositiveInt("ID пользователя: ");
            Console.Write("Деактивировать учётную запись, сохранив историю заявок (ДА)? ");
            if (!string.Equals(Console.ReadLine()?.Trim(), "ДА", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Операция отменена."); return;
            }
            Console.WriteLine(_users.SoftDelete(id, admin.Id)
                ? "Учётная запись деактивирована."
                : "Активный пользователь не найден.");
        }

        private static void ShowRequests(IReadOnlyCollection<Request> requests)
        {
            if (requests.Count == 0) { Console.WriteLine("Заявки не найдены."); return; }
            Console.WriteLine($"{"ID",4} | {"Название",-28} | {"Статус",-14} | {"Клиент",-22} | Создана (UTC)");
            Console.WriteLine(new string('-', 92));
            foreach (var r in requests)
                Console.WriteLine($"{r.Id,4} | {Shorten(r.Title,28),-28} | {Shorten(r.Status.Name,14),-14} | {Shorten(r.Client.FullName,22),-22} | {r.CraatedAt:yyyy-MM-dd HH:mm}");
        }

        private static string Shorten(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

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
                Console.WriteLine("Введите 0 или положительное целое число.");
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

        private static void RunSafely(Action action)
        {
            try { action(); }
            catch (ArgumentException ex) { Console.WriteLine($"Проверьте данные: {ex.Message}"); }
            catch (InvalidOperationException ex) { Console.WriteLine($"Операция запрещена: {ex.Message}"); }
            catch (DbUpdateException ex) { Console.WriteLine($"Ошибка сохранения: {ex.GetBaseException().Message}"); }
            catch (NpgsqlException ex) { Console.WriteLine($"Ошибка PostgreSQL: {ex.Message}"); }
            catch (Exception ex) { Console.WriteLine($"Операция не выполнена: {ex.Message}"); }
            finally { Pause(); }
        }

        private static void UnknownChoice() { Console.WriteLine("Неизвестный пункт меню."); Pause(); }
        private static void ClearConsole() { try { Console.Clear(); } catch (IOException) { } }
        private static void Pause() { Console.WriteLine(); Console.Write("Нажмите Enter, чтобы продолжить..."); Console.ReadLine(); }
    }
}
