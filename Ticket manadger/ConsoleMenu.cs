using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ticket_manadger
{
    internal sealed class ConsoleMenu
    {
        private readonly RequestRepo _requests = new();

        public void Run()
        {
            if (!CheckDatabase())
                return;

            while (true)
            {
                PrintMenu();
                Console.Write("Выберите действие: ");
                var choice = Console.ReadLine()?.Trim();

                if (choice == "0")
                {
                    Console.WriteLine("Работа завершена.");
                    return;
                }

                switch (choice)
                {
                    case "1": RunSafely(ListRequests); break;
                    case "2": RunSafely(CreateRequest); break;
                    case "3": RunSafely(EditRequest); break;
                    case "4": RunSafely(ChangeRequestStatus); break;
                    case "5": RunSafely(DeleteRequest); break;
                    case "6": RunSafely(SearchRequests); break;
                    case "7": RunSafely(ShowStatistics); break;
                    default:
                        Console.WriteLine("Неизвестный пункт меню. Введите число от 0 до 7.");
                        Pause();
                        break;
                }
            }
        }

        private static void PrintMenu()
        {
            Console.Clear();
            Console.WriteLine("====================================");
            Console.WriteLine("          TICKET MANAGER");
            Console.WriteLine("====================================");
            Console.WriteLine("1. Просмотреть все заявки");
            Console.WriteLine("2. Добавить заявку");
            Console.WriteLine("3. Изменить заявку");
            Console.WriteLine("4. Изменить статус заявки");
            Console.WriteLine("5. Удалить заявку");
            Console.WriteLine("6. Поиск, фильтрация и сортировка");
            Console.WriteLine("7. Статистика и аналитика");
            Console.WriteLine("0. Выход");
            Console.WriteLine("------------------------------------");
        }

        private static bool CheckDatabase()
        {
            try
            {
                using var db = new AppDbContext();

                if (!db.Database.CanConnect())
                {
                    Console.WriteLine("Не удалось подключиться к PostgreSQL.");
                    PrintConnectionHelp();
                    Pause();
                    return false;
                }

                var pendingMigrations = db.Database.GetPendingMigrations().ToArray();
                if (pendingMigrations.Length > 0)
                {
                    Console.WriteLine("В базе есть неприменённые миграции:");
                    foreach (var migration in pendingMigrations)
                        Console.WriteLine($"  - {migration}");

                    Console.WriteLine();
                    Console.WriteLine("Примените миграции командой:");
                    Console.WriteLine("dotnet ef database update --project \"Ticket manadger/Ticket manadger.csproj\"");
                    Pause();
                    return false;
                }

                return true;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                PrintConnectionHelp();
                Pause();
                return false;
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Ошибка PostgreSQL: {ex.Message}");
                PrintConnectionHelp();
                Pause();
                return false;
            }
        }

        private static void PrintConnectionHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Установите переменную окружения TICKET_DB_CONNECTION, например:");
            Console.WriteLine("$env:TICKET_DB_CONNECTION = \"Host=localhost;Port=5433;Database=ticket_db;Username=postgres;Password=ВАШ_ПАРОЛЬ\"");
        }

        private void ListRequests()
        {
            ShowRequests(_requests.GetAllRequest());
        }

        private void CreateRequest()
        {
            var clients = _requests.GetClients();
            if (clients.Count == 0)
            {
                Console.WriteLine("В базе нет клиентов. Добавьте пользователя с ролью Client перед созданием заявки.");
                return;
            }

            Console.WriteLine("Клиенты:");
            foreach (var client in clients)
                Console.WriteLine($"  {client.Id}. {client.FullName}");

            var clientId = ReadPositiveInt("ID клиента: ");
            var title = ReadRequired("Название (до 100 символов): ");
            var description = ReadRequired("Описание: ");

            _requests.AddRequest(title, description, clientId);
            Console.WriteLine("Заявка успешно создана.");
        }

        private void EditRequest()
        {
            var id = ReadPositiveInt("ID заявки для изменения: ");
            var request = _requests.GetRequestById(id);

            if (request is null)
            {
                Console.WriteLine("Заявка с таким ID не найдена.");
                return;
            }

            Console.WriteLine($"Текущее название: {request.Title}");
            var title = ReadOptional("Новое название (Enter — оставить прежнее): ", request.Title);
            Console.WriteLine($"Текущее описание: {request.description}");
            var description = ReadOptional("Новое описание (Enter — оставить прежнее): ", request.description);

            if (_requests.UpdateRequest(id, title, description))
                Console.WriteLine("Заявка обновлена.");
            else
                Console.WriteLine("Заявка не найдена; изменения не сохранены.");
        }

        private void ChangeRequestStatus()
        {
            var id = ReadPositiveInt("ID заявки: ");
            var statuses = _requests.GetStatuses();

            Console.WriteLine("Доступные статусы:");
            foreach (var status in statuses)
                Console.WriteLine($"  {status.Id}. {status.Name}");

            var statusId = ReadPositiveInt("Новый статус (ID): ");
            if (_requests.UpdateStatus(id, statusId))
                Console.WriteLine("Статус заявки обновлён.");
            else
                Console.WriteLine("Заявка с таким ID не найдена.");
        }

        private void DeleteRequest()
        {
            var id = ReadPositiveInt("ID заявки для удаления: ");
            var request = _requests.GetRequestById(id);

            if (request is null)
            {
                Console.WriteLine("Заявка с таким ID не найдена.");
                return;
            }

            Console.Write($"Удалить заявку \"{request.Title}\"? Введите ДА для подтверждения: ");
            var confirmation = Console.ReadLine()?.Trim();
            if (!string.Equals(confirmation, "ДА", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Удаление отменено.");
                return;
            }

            Console.WriteLine(_requests.DeleteRequest(id)
                ? "Заявка удалена."
                : "Заявка уже отсутствует; удаление не выполнено.");
        }

        private void SearchRequests()
        {
            Console.Write("Текст для поиска (Enter — без поиска): ");
            var search = Console.ReadLine();

            var statuses = _requests.GetStatuses();
            Console.WriteLine("Фильтр по статусу:");
            Console.WriteLine("  0. Все статусы");
            foreach (var status in statuses)
                Console.WriteLine($"  {status.Id}. {status.Name}");

            var statusChoice = ReadNonNegativeInt("ID статуса: ");
            int? statusId = statusChoice == 0 ? null : statusChoice;

            Console.WriteLine("Сортировка:");
            Console.WriteLine("  1. Сначала новые");
            Console.WriteLine("  2. Сначала старые");
            Console.WriteLine("  3. Название А—Я");
            Console.WriteLine("  4. Название Я—А");
            Console.WriteLine("  5. По статусу");

            var sortChoice = ReadChoice("Вариант сортировки: ", "1", "2", "3", "4", "5");
            var sortOrder = sortChoice switch
            {
                "2" => RequestSortOrder.OldestFirst,
                "3" => RequestSortOrder.TitleAscending,
                "4" => RequestSortOrder.TitleDescending,
                "5" => RequestSortOrder.Status,
                _ => RequestSortOrder.NewestFirst
            };

            ShowRequests(_requests.FindRequests(search, statusId, sortOrder));
        }

        private void ShowStatistics()
        {
            var requests = _requests.GetAllRequest();
            Console.WriteLine($"Всего заявок: {requests.Count}");
            Console.WriteLine();
            Console.WriteLine("Количество по статусам:");

            var byStatus = _requests.GetLog();
            if (byStatus.Count == 0)
                Console.WriteLine("  Заявок пока нет.");
            else
                foreach (var item in byStatus.OrderBy(item => item.Key))
                    Console.WriteLine($"  {item.Key}: {item.Value}");

            Console.WriteLine();
            Console.WriteLine("Количество заявок по клиентам:");
            var byClient = _requests.GetClientRequestCounts();
            if (byClient.Count == 0)
                Console.WriteLine("  Данных пока нет.");
            else
                foreach (var item in byClient)
                    Console.WriteLine($"  {item.Key}: {item.Value}");
        }

        private static void ShowRequests(IReadOnlyCollection<Request> requests)
        {
            if (requests.Count == 0)
            {
                Console.WriteLine("Заявки не найдены.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"{"ID",4} | {"Название",-30} | {"Статус",-14} | {"Клиент",-22} | Создана (UTC)");
            Console.WriteLine(new string('-', 96));

            foreach (var request in requests)
            {
                var title = Shorten(request.Title, 30);
                var status = Shorten(request.Status.Name, 14);
                var client = Shorten(request.Client.FullName, 22);
                Console.WriteLine($"{request.Id,4} | {title,-30} | {status,-14} | {client,-22} | {request.CraatedAt:yyyy-MM-dd HH:mm}");
            }
        }

        private static string Shorten(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

        private static string ReadRequired(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                var value = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();

                Console.WriteLine("Значение не может быть пустым.");
            }
        }

        private static string ReadOptional(string prompt, string currentValue)
        {
            Console.Write(prompt);
            var value = Console.ReadLine();
            return string.IsNullOrWhiteSpace(value) ? currentValue : value.Trim();
        }

        private static int ReadPositiveInt(string prompt)
        {
            while (true)
            {
                var value = ReadRequired(prompt);
                if (int.TryParse(value, out var number) && number > 0)
                    return number;

                Console.WriteLine("Введите целое число больше нуля.");
            }
        }

        private static int ReadNonNegativeInt(string prompt)
        {
            while (true)
            {
                var value = ReadRequired(prompt);
                if (int.TryParse(value, out var number) && number >= 0)
                    return number;

                Console.WriteLine("Введите целое число 0 или больше.");
            }
        }

        private static string ReadChoice(string prompt, params string[] allowedValues)
        {
            while (true)
            {
                var value = ReadRequired(prompt);
                if (allowedValues.Contains(value))
                    return value;

                Console.WriteLine($"Допустимые варианты: {string.Join(", ", allowedValues)}.");
            }
        }

        private static void RunSafely(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Проверьте введённые данные: {ex.Message}");
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"База отклонила изменение: {ex.GetBaseException().Message}");
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Ошибка PostgreSQL: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Операция не выполнена: {ex.Message}");
            }
            finally
            {
                Pause();
            }
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.Write("Нажмите Enter, чтобы продолжить...");
            Console.ReadLine();
        }
    }
}
