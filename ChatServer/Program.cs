using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    public class ClientObject
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Username { get; set; }
        public TcpClient Client { get; }
        public NetworkStream Stream { get; private set; }
        private readonly ServerObject _server;

        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            Client = tcpClient;
            _server = serverObject;
        }

        public void Process()
        {
            try
            {
                Stream = Client.GetStream();

                string username = GetMessage();
                Username = username;

                Console.WriteLine($"{Username} вошел в чат");

                _server.BroadcastUserList();

                while (true)
                {
                    try
                    {
                        string message = GetMessage();
                        if (string.IsNullOrEmpty(message)) continue;

                        Console.WriteLine($"{Username}: {message}");
                        _server.BroadcastMessage($"{Username}: {message}", Id);
                    }
                    catch
                    {
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
                _server.RemoveConnection(Id);
                Stream?.Close();
                Client?.Close();
            }
        }

        private string GetMessage()
        {
            byte[] data = new byte[256];
            StringBuilder builder = new StringBuilder();
            int bytes;
            do
            {
                bytes = Stream.Read(data, 0, data.Length);
                builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
            } while (Stream.DataAvailable);

            return builder.ToString();
        }
    }

    public class ServerObject
    {
        private TcpListener _tcpListener;
        private readonly Dictionary<string, ClientObject> _clients = new Dictionary<string, ClientObject>();
        private int _port;

        public ServerObject(int port)
        {
            _port = port;
            _tcpListener = new TcpListener(IPAddress.Any, _port);
        }

        public void AddConnection(ClientObject clientObject)
        {
            _clients.Add(clientObject.Id, clientObject);
        }

        public void RemoveConnection(string id)
        {
            if (_clients.TryGetValue(id, out ClientObject client))
            {
                _clients.Remove(id);
                Console.WriteLine($"Клиент {client.Username} отключен");
            }
        }

        public List<string> GetUserList()
        {
            var userList = new List<string>();
            foreach (var client in _clients.Values)
            {
                userList.Add(client.Username);
            }
            return userList;
        }

        public void BroadcastMessage(string message, string id)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var client in _clients.Values)
            {
                if (client.Id != id)
                {
                    client.Stream.Write(data, 0, data.Length);
                }
            }
        }

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

        public void Listen()
        {
            try
            {
                _tcpListener.Start();
                Console.WriteLine($"Сервер запущен на порту {_port}. Ожидание подключений...");

                while (true)
                {
                    TcpClient tcpClient = _tcpListener.AcceptTcpClient();

                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    AddConnection(clientObject);

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