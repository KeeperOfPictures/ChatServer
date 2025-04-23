using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    // Класс, представляющий клиента, подключенного к серверу
    public class ClientObject
    {
        public string Id { get; } = Guid.NewGuid().ToString(); // Уникальный идентификатор клиента
        public string Username { get; set; } // Имя пользователя
        public TcpClient Client { get; } // TCP-клиент
        public NetworkStream Stream { get; private set; } // Сетевой поток для обмена данными
        private readonly ServerObject _server; // Ссылка на сервер

        // Конструктор
        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            Client = tcpClient;
            _server = serverObject;
        }

        // Основной метод обработки клиента
        public void Process()
        {
            try
            {
                Stream = Client.GetStream(); // Получаем сетевой поток

                // Получаем имя пользователя
                string username = GetMessage();
                Username = username;

                Console.WriteLine($"{Username} вошел в чат");

                // Рассылаем обновленный список пользователей
                _server.BroadcastUserList();

                // Основной цикл обработки сообщений
                while (true)
                {
                    try
                    {
                        // Получаем сообщение от клиента
                        string message = GetMessage();
                        if (string.IsNullOrEmpty(message)) continue;

                        // Логируем сообщение с временной меткой
                        Console.WriteLine($"[{DateTime.Now}]{Username}: {message}");

                        // Рассылаем сообщение всем клиентам, кроме отправителя
                        _server.BroadcastMessage($"{Username}: {message}", Id);
                    }
                    catch
                    {
                        // Обработка отключения клиента
                        Console.WriteLine($"{Username} покинул чат");
                        _server.RemoveConnection(Id);
                        _server.BroadcastUserList();
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                // Гарантированное освобождение ресурсов
                _server.RemoveConnection(Id);
                Stream?.Close();
                Client?.Close();
            }
        }

        // Метод для получения сообщения из потока
        private string GetMessage()
        {
            byte[] data = new byte[256]; // Буфер для данных
            StringBuilder builder = new StringBuilder();
            int bytes;
            do
            {
                // Чтение данных из потока
                bytes = Stream.Read(data, 0, data.Length);
                builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
            } while (Stream.DataAvailable); // Пока есть данные

            return builder.ToString();
        }
    }

    // Класс, представляющий сервер чата
    public class ServerObject
    {
        private TcpListener _tcpListener; // Слушатель подключений
        private readonly Dictionary<string, ClientObject> _clients = new Dictionary<string, ClientObject>(); // Список клиентов
        private int _port; // Порт сервера

        // Конструктор
        public ServerObject(int port)
        {
            _port = port;
            _tcpListener = new TcpListener(IPAddress.Any, _port); // Слушаем все IP-адреса
        }

        // Добавление нового клиента
        public void AddConnection(ClientObject clientObject)
        {
            _clients.Add(clientObject.Id, clientObject);
        }

        // Удаление клиента
        public void RemoveConnection(string id)
        {
            if (_clients.TryGetValue(id, out ClientObject client))
            {
                _clients.Remove(id);
                Console.WriteLine($"Клиент {client.Username} отключен");
            }
        }

        // Получение списка имен пользователей
        public List<string> GetUserList()
        {
            var userList = new List<string>();
            foreach (var client in _clients.Values)
            {
                userList.Add(client.Username);
            }
            return userList;
        }

        // Рассылка сообщения всем клиентам, кроме указанного
        public void BroadcastMessage(string message, string id)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var client in _clients.Values)
            {
                if (client.Id != id) // Не отправляем отправителю
                {
                    client.Stream.Write(data, 0, data.Length);
                }
            }
        }

        // Рассылка обновленного списка пользователей
        public void BroadcastUserList()
        {
            var userList = GetUserList();
            string message = "USERLIST:" + string.Join(",", userList);
            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var client in _clients.Values)
            {
                client.Stream.Write(data, 0, data.Length);
            }
        }

        // Основной метод прослушивания подключений
        public void Listen()
        {
            try
            {
                _tcpListener.Start();
                Console.WriteLine($"Сервер запущен на порту {_port}. Ожидание подключений...");

                // Бесконечный цикл принятия подключений
                while (true)
                {
                    TcpClient tcpClient = _tcpListener.AcceptTcpClient(); // Принимаем клиента

                    // Создаем объект клиента
                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    AddConnection(clientObject);

                    // Запускаем обработку клиента в отдельном потоке
                    Thread clientThread = new Thread(clientObject.Process);
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Disconnect();
            }
        }

        // Отключение всех клиентов и остановка сервера
        public void Disconnect()
        {
            foreach (var client in _clients.Values)
            {
                client.Stream?.Close();
                client.Client?.Close();
            }
            _tcpListener.Stop();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Введите порт для сервера (по умолчанию 8888):");
            string portInput = Console.ReadLine();
            int port = string.IsNullOrEmpty(portInput) ? 8888 : int.Parse(portInput);

            // Создаем и запускаем сервер
            ServerObject server = new ServerObject(port);
            try
            {
                server.Listen();
            }
            catch (Exception ex)
            {
                server.Disconnect();
                Console.WriteLine(ex.Message);
            }
        }
    }
}