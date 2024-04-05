using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Encodings.Web;


Client client = new Client();
await client.CreateClient();

public class ClientData
{
    public string? Operation { get; set; } = "";
    public string? FileName { get; set; } = "";
    public string? FileNameServer { get; set; } = "";
    public string? FileContents { get; set; } = ""; 
    public bool isByte { get; set; }

    public override string ToString()
    {
        string result = "";
        try
        {
            result =
                $"|{Operation.Length}|" + Operation +
                $"|{FileName.Length}|" + FileName +
                $"|{FileNameServer.Length}|" + FileNameServer +
                $"|{FileContents.Length}|" + FileContents +
                $"|{(isByte ? 1 : 0)}|" + $"{(isByte ? 1 : 0)}";
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        return result;
    }
}


class Client
{
    TcpClient client;
    StreamReader? Reader;
    StreamWriter? Writer;

    string[] textFileFormats = { ".txt", ".bin", ".kamil", ".py", ".json" };
    string[] byteFileFormats = { ".jpg", ".jpeg", ".gif", ".png", ".zip", ".rar", ".7z" };
    string path = "C:/Универ/Программная Инженерия/SE_lab03_09251_Sviridov/client/data/";

    public async Task CreateClient()
    {
        client = new TcpClient();
        try
        {
            client.Connect("127.0.0.1", 8888);
            Console.WriteLine("Подключен к серверу");

            Reader = new StreamReader(client.GetStream());
            Writer = new StreamWriter(client.GetStream());

            while (true)
            {
                await SendMessageAsync(Writer);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    async Task SendMessageAsync(StreamWriter writer)
    {
        try
        {
            Console.Write("Введите операцию (1 - получить, 2 - сохранить, 3 - удалить): > ");
            string? operation = Console.ReadLine();

            if (operation == null || operation == "" || (operation != "1" && operation != "2" && operation != "3" && operation != "exit")) 
            { Console.WriteLine("Неправильный ввод"); return; }

            ClientData data = new ClientData();

            string? fileName = "";
            string? fileContents = "";
            string? fileNameServer = "";
            string? getType = "";

            switch (operation)
            {
                case "1": // GET
                    Console.Write("Хотите получить по имени или ID (1 - имени, 2 - ID): > ");
                    getType = Console.ReadLine();
                    if (getType == null || getType == "" || (getType != "1" && getType != "2")) { Console.WriteLine("Неправильный ввод"); return; }

                    Console.Write($"Введите {(getType == "1" ? "имя" : "ID")} файла: > ");
                    fileName = Console.ReadLine();
                    if (fileName == null || fileName == "") { Console.WriteLine("Неправильный ввод"); return; }

                    data.Operation = "GET" + " " + (getType == "1" ? "BY_NAME" : "BY_ID");
                    data.FileName = fileName;

                    Console.WriteLine("Запрос отправлен");

                    await writer.WriteLineAsync(data.ToString());
                    await writer.FlushAsync();

                    break;

                case "2": // PUT
                    Console.Write("Введите имя файла клиента: > ");
                    fileName = Console.ReadLine();
                    if (fileName == null || fileName == "") { Console.WriteLine("Неправильный ввод"); return; }

                    Console.Write("Введите имя файла на сервере: > ");
                    fileNameServer = Console.ReadLine();
                    if (fileNameServer == null) { fileNameServer = ""; }

                    if (textFileFormats.Any(fileName.Contains)) // Текстовые Файлы
                    {
                        Console.Write("Введите контент файла: > ");
                        fileContents = Console.ReadLine();
                        if (fileName == null) fileContents = "";

                        data.Operation = "PUT";
                        data.FileName = fileName;
                        data.FileNameServer = fileNameServer;
                        data.FileContents = fileContents;
                        data.isByte = false;

                        try
                        {
                            await writer.WriteLineAsync(data.ToString());
                            await writer.FlushAsync();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Неправильный ввод {e}");
                            return;
                        }
                    } 
                    else if (byteFileFormats.Any(fileName.Contains)) // Прочие файлы
                    {
                        data.Operation = "PUT";
                        data.FileName = fileName;
                        data.FileNameServer = fileNameServer;
                        data.FileContents = Convert.ToBase64String(File.ReadAllBytes(path + fileName));
                        data.isByte = true;

                        try
                        {
                            await writer.WriteLineAsync(data.ToString());
                            await writer.FlushAsync();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Неправильный ввод {e}");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Неизвестный формат"); return;
                    }

                    break;

                case "3": // DELETE
                    Console.Write("Хотите удалить по имени или ID (1 - имени, 2 - ID): > ");
                    getType = Console.ReadLine();
                    if (getType == null || getType == "" || (getType != "1" && getType != "2")) { Console.WriteLine("Неправильный ввод"); return; }

                    Console.Write($"Введите {(getType == "1" ? "имя" : "ID")} файла: > ");
                    fileName = Console.ReadLine();
                    if (fileName == null || fileName == "") { Console.WriteLine("Неправильный ввод"); return; }

                    data.Operation = "DELETE" + " " + (getType == "1" ? "BY_NAME" : "BY_ID"); ;
                    data.FileName = fileName;

                    await writer.WriteLineAsync(data.ToString());
                    await writer.FlushAsync();

                    break;

                case "exit": // exit
                    Console.WriteLine("Запрос отправлен");

                    data.Operation = "exit";
                    await writer.WriteLineAsync(data.ToString());
                    await writer.FlushAsync();

                    client.Close();

                    Environment.Exit(0);

                    break;
            }

            await ReceiveMessageAsync(operation, Reader);
        }
        catch
        {
            Console.WriteLine("Неправильный ввод");
            return;
        }
    }

    async Task ReceiveMessageAsync(string operation, StreamReader reader)
    {
        try
        {
            string? message = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(message)) return;
            string code = message.Substring(0, 3);

            switch (operation)
            {
                case "1": // GET
                    if (code == "200")
                    {
                        string fileName = "";

                        Console.Write($"Сервер нашептывает внутренности, введите имя файла на клиенте: > ");
                        fileName = Console.ReadLine();
                        while (fileName == null || fileName == "") Console.WriteLine("Неправильный ввод");

                        File.WriteAllBytes(path + fileName, Convert.FromBase64String(message.Substring(4, message.Length - 4)));
                        Console.WriteLine("Файл сохранен на диске!");
                    }
                    if (code == "404") Console.WriteLine("Сервер нашептывает, что файл не существует");

                    break;

                case "2": // PUT
                    if (code == "200") Console.WriteLine($"Сервер нашептывает, что файл успешно создан и ID = {message.Substring(4, message.Length - 4)}");
                    if (code == "403") Console.WriteLine("Сервер нашептывает, что файл уже был создан");

                    break;

                case "3": // DELETE
                    if (code == "200") Console.WriteLine("Сервер нашептывает, что файл успешно удален");
                    if (code == "404") Console.WriteLine("Сервер нашептывает, что файл не существует");

                    break;
            }
        }
        catch
        {
            Console.WriteLine("Соединение разорвано");
        }
    }
}