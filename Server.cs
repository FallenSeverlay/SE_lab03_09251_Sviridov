using System;
using System.Buffers.Text;
using System.ComponentModel;
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
    public string? FileNameServer { get; set; }
    public string? FileContents { get; set; }
    public bool isByte { get; set; }

    public ClientData(string data)
    {
        try
        {
            for (int i = 0; i < 5; i++)
            {
                int pos = 0;

                string blockLengthString = "";
                bool length = false;

                foreach (char letter in data)
                {
                    pos++;
                    if (length)
                    {
                        blockLengthString += letter;
                    }
                    if (letter == '|')
                    {
                        if (length)
                        {
                            blockLengthString = blockLengthString.Remove(blockLengthString.Length - 1);
                            break;
                        }
                        length = true;
                    }
                }

                int blockLength = 0;
                if (blockLengthString != "") blockLength = Convert.ToInt32(blockLengthString);

                switch (i)
                {
                    case 0: Operation = data.Substring(pos, blockLength); break;
                    case 1: FileName = data.Substring(pos, blockLength); break;
                    case 2:
                        FileNameServer = "";
                        if (blockLength != 0) FileNameServer = data.Substring(pos, blockLength);
                        break;
                    case 3: 
                        FileContents = "";
                        if (blockLength != 0) FileContents = data.Substring(pos, blockLength); 
                        break;
                    case 4: 
                        isByte = false;
                        if (data.Substring(pos, blockLength) == "1") isByte = true;
                        break;
                }

                data = data.Remove(0, pos);
                if (blockLength != 0) data = data.Remove(0, blockLength);
            }
        }
        catch (Exception e) { Console.WriteLine(e.ToString()); }
    }
}

class Server
{
    TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888);
    List<Client> clients = new List<Client>();
    Random random = new Random();
    string path = "C:/Универ/Программная Инженерия/SE_lab03_09251_Sviridov/server/data/";
    string path_indexes = "C:/Универ/Программная Инженерия/SE_lab03_09251_Sviridov/server/FileIndexes.txt";
    public Dictionary<string, int> serverFiles { get; set; } = new Dictionary<string, int>();

    private int GenerateUniqueID()
    {
        int ID = random.Next();
        while (serverFiles.ContainsValue(ID)) ID = random.Next();
        return ID;
    }

    private void RewriteIndexes()
    {
        string output = "";
        foreach (KeyValuePair<string, int> file in serverFiles)
        {
            output += file.Key + "|" + Convert.ToString(file.Value) + "\n";
        }

        File.WriteAllText(path_indexes, output);
    }

    private void PreloadFiles()
    {
        string[] lines = File.ReadAllLines(path_indexes);

        foreach (string line in lines)
        {
            string fileName = line.Split('|')[0];
            int fileIndex = Convert.ToInt32(line.Split('|')[1]);

            if (!serverFiles.ContainsKey(fileName)) serverFiles.Add(fileName, fileIndex);
        }
    }

    public string PutFile(string fileName, string fileNameServer, string fileContents, out string newName, bool isByte = false)
    {
        newName = "";

        if (!serverFiles.ContainsKey(fileNameServer)) 
        {
            if (fileNameServer == "") fileNameServer = Guid.NewGuid().ToString().Substring(0, 8) + "." + fileName.Split(".").Last();

            if (!isByte) File.WriteAllText(path + fileNameServer, fileContents);
            else File.WriteAllBytes(path + fileNameServer, Convert.FromBase64String(fileContents));

            int ID = GenerateUniqueID();
            serverFiles.Add(fileNameServer, ID);
            RewriteIndexes();

            newName = fileNameServer;
            return "200" + " " + Convert.ToString(ID);
        }
        else return "403";
    }

    public string GetFile(string fileName, bool getByName = true)
    {
        if (getByName)
        {
            if (!serverFiles.ContainsKey(fileName)) return "404";
            else
            {
                return "200" + " " + Convert.ToBase64String(File.ReadAllBytes(path + fileName));
            }
        }
        else
        {
            if (!serverFiles.ContainsValue(Convert.ToInt32(fileName))) return "404";
            else
            {
                foreach (KeyValuePair<string, int> file in serverFiles)
                {
                    if (file.Value == Convert.ToInt32(fileName))
                    {
                        fileName = file.Key;
                        break;
                    }
                }

                return "200" + " " + Convert.ToBase64String(File.ReadAllBytes(path + fileName));
            }
        }
    }

    public string DeleteFile(string fileName, bool delByName = true)
    {
        if (delByName)
        {
            if (!serverFiles.ContainsKey(fileName)) return "404";
            else
            {
                serverFiles.Remove(fileName);
                File.Delete(path + fileName);

                RewriteIndexes();
                return "200";
            }
        }
        else
        {
            if (!serverFiles.ContainsValue(Convert.ToInt32(fileName))) return "404";
            else
            {
                foreach (KeyValuePair<string, int> file in serverFiles)
                {
                    if (file.Value == Convert.ToInt32(fileName))
                    {
                        fileName = file.Key;
                        break;
                    }
                }

                serverFiles.Remove(fileName);
                File.Delete(path + fileName);

                RewriteIndexes();
                return "200";
            }
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

    private string PutFile(ClientData clientFile, out string newName)
    {
        string result = server.PutFile(clientFile.FileName, clientFile.FileNameServer, clientFile.FileContents, out newName, clientFile.isByte);

        return result;
    }

    private string GetFile(ClientData clientFile, bool getByName = true)
    {
        return server.GetFile(clientFile.FileName, getByName);
    }

    private string DeleteFile(ClientData clientFile, bool delByName = true)
    {
        return server.DeleteFile(clientFile.FileName, delByName);
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
                    ClientData? clientData = new ClientData(message);

                    switch (clientData.Operation)
                    {
                        case "GET BY_NAME":
                            result = GetFile(clientData, true);

                            if (result.Split(" ")[0] == "200") Console.WriteLine($"[{DateTime.Now}] {uid} читает файл {clientData.FileName}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался читать {clientData.FileName}, но его не существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;

                        case "GET BY_ID":
                            result = GetFile(clientData, false);

                            if (result.Split(" ")[0] == "200") Console.WriteLine($"[{DateTime.Now}] {uid} читает файл с ID = {clientData.FileName}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался читать файлс ID = {clientData.FileName}, но его не существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;

                        case "PUT":
                            string newName = "";
                            result = PutFile(clientData, out newName);

                            if (result.Substring(0, 3) == "200") Console.WriteLine($"[{DateTime.Now}] {uid} создал файл {newName} с ID = {result.Substring(4, result.Length - 4)}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался создать файл {clientData.FileName}, но он уже существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;

                        case "DELETE BY_NAME":
                            result = DeleteFile(clientData, true);

                            if (result == "200") Console.WriteLine($"[{DateTime.Now}] {uid} удалил файл {clientData.FileName}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался удалить {clientData.FileName}, но его не существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;

                        case "DELETE BY_ID":
                            result = DeleteFile(clientData, false);

                            if (result == "200") Console.WriteLine($"[{DateTime.Now}] {uid} удалил файл с ID = {clientData.FileName}");
                            else Console.WriteLine($"[{DateTime.Now}] {uid} пытался удалить файл с ID = {clientData.FileName}, но его не существует");

                            await Writer.WriteLineAsync(result);
                            await Writer.FlushAsync();

                            break;

                        case "exit":
                            Console.WriteLine($"[{DateTime.Now}] {uid} запросил выход.");

                            Environment.Exit(0);

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