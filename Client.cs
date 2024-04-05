using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Encodings.Web;


Client client = new Client();
await client.CreateClient();

public class ClientData
{
    public string? Operation { get; set; }
    public string? FileName { get; set; }
    public string? FileContents { get; set; }
}


class Client
{
    TcpClient client;
    StreamReader? Reader;
    StreamWriter? Writer;

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

            if (operation == null || operation == "" || (operation != "1" && operation != "2" && operation != "3")) 
            { Console.WriteLine("Неправильный ввод"); return; }

            ClientData data = new ClientData();

            string? fileName = "";
            string? fileContents = "";

            switch (operation)
            {
                case "1": // GET
                    Console.Write("Введите имя файла: > ");
                    fileName = Console.ReadLine();
                    if (fileName == null || fileName == "") { Console.WriteLine("Неправильный ввод"); return; }

                    data.Operation = "GET";
                    data.FileName = fileName;

                    await writer.WriteLineAsync(JsonSerializer.Serialize(data));
                    await writer.FlushAsync();

                    break;

                case "2": // PUT
                    Console.Write("Введите имя файла: > ");
                    fileName = Console.ReadLine();
                    if (fileName == null || fileName == "") { Console.WriteLine("Неправильный ввод"); return; }

                    Console.Write("Введите контент файла: > ");
                    fileContents = Console.ReadLine();
                    if (fileName == null) fileContents = "";

                    data.Operation = "PUT";
                    data.FileName = fileName;
                    data.FileContents = fileContents;

                    await writer.WriteLineAsync(JsonSerializer.Serialize(data));
                    await writer.FlushAsync();

                    break;

                case "3": // DELETE
                    Console.Write("Введите имя файла: > ");
                    fileName = Console.ReadLine();
                    if (fileName == null || fileName == "") { Console.WriteLine("Неправильный ввод"); return; }

                    data.Operation = "DELETE";
                    data.FileName = fileName;

                    await writer.WriteLineAsync(JsonSerializer.Serialize(data));
                    await writer.FlushAsync();

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
                    if (code == "200") Console.WriteLine($"Сервер нашептывает внутренности: {message.Substring(4, message.Length - 4)}" + " " + (message.Length - 4 == 0 ? "(Их нет)" : ""));
                    if (code == "404") Console.WriteLine("Сервер нашептывает, что файл не существует");

                    break;

                case "2": // PUT
                    if (code == "200") Console.WriteLine("Сервер нашептывает, что файл успешно создан");
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