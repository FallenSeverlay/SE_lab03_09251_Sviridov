using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;

Server server = new Server();
await server.ListenAsync();

public class ClientData
{
    public string? Operation { get; set; }
    public string? FileName { get; set; }
    public string? FileContents { get; set; }
}

class Server
{
    TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888);
    List<Client> clients = new List<Client>();
    string path = "C:/Универ/Программная Инженерия/SE_lab03_09251_Sviridov/server/data/";
    public Dictionary<string, string> serverFiles { get; set; } = new Dictionary<string, string>();

    private void PreloadFiles()
    {
        string[] files = Directory.GetFiles(path);

        foreach (string file in files)
        {
            string fileName = file.Replace(path, "");

            if (!serverFiles.ContainsKey(fileName)) serverFiles.Add(fileName, "");
        }
    }

    public string PutFile(string fileName, string fileContents)
    {
        if (!serverFiles.ContainsKey(fileName)) 
        {
            File.WriteAllText(path + fileName, fileContents);

            serverFiles.Add(fileName, fileContents);
            return "200";
        }
        else return "403";
    }

    public string GetFile(string fileName)
    {
        if (!serverFiles.ContainsKey(fileName)) return "404";
        else
        {
            return "200" + " " + File.ReadAllText(path + fileName); 
        }
    }

    public string DeleteFile(string fileName)
    {
        if (!serverFiles.ContainsKey(fileName)) return "404";
        else
        {
            serverFiles.Remove(fileName);
            File.Delete(path + fileName);

            return "200";
        }
    }

    public async Task ListenAsync()
    {
        PreloadFiles();

        try
        {
            tcpListener.Start();
            Console.WriteLine("Сервер запущен. Ожидание подключений... ");
            while (true)
            {
                TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();

                Client client = new Client(tcpClient, this);
                clients.Add(client);

                Console.WriteLine($"[{DateTime.Now}] Подключился {client.uid}");

                Task.Run(client.ProcessAsync);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

class Client
{
    public string uid = Guid.NewGuid().ToString().Substring(0, 8);
    private TcpClient client;
    private Server server;

    private StreamReader Reader { get; }
    private StreamWriter Writer { get; }

    public Client(TcpClient client, Server server)
    {
        this.client = client;
        this.server = server;

        var stream = client.GetStream();

        Reader = new StreamReader(stream);
        Writer = new StreamWriter(stream);
    }

    private string PutFile(ClientData clientFile)
    {
        return server.PutFile(clientFile.FileName, clientFile.FileContents);
    }

    private string GetFile(ClientData clientFile)
    {
        return server.GetFile(clientFile.FileName);
    }

    private string DeleteFile(ClientData clientFile)
    {
        return server.DeleteFile(clientFile.FileName);
    }

    public async Task ProcessAsync()
    {
        try
        {
            string? message;
            string result;

            while (true)
            {
                message = await Reader.ReadLineAsync();
                if (message == null || message == "") break;

                try
                {
                    ClientData? clientData = JsonSerializer.Deserialize<ClientData>(message);

                    switch (clientData.Operation)
                    {
                        case "GET":
                            result = GetFile(clientData);

                            if (result.Split(" ")[0] == "200") Console.WriteLine($"[{DateTime.Now}] {uid} читает файл {clientData.FileName}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался читать {clientData.FileName}, но его не существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;

                        case "PUT":
                            result = PutFile(clientData);

                            if (result == "200") Console.WriteLine($"[{DateTime.Now}] {uid} создал файл {clientData.FileName}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался создать файл {clientData.FileName}, но он уже существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;

                        case "DELETE":
                            result = DeleteFile(clientData);

                            if (result == "200") Console.WriteLine($"[{DateTime.Now}] {uid} удалил файл {clientData.FileName}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался удалить {clientData.FileName}, но его не существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;
                    }
                }
                catch
                {
                    break;
                }
            }

            Console.WriteLine($"[{DateTime.Now}] {uid} был отключен");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}