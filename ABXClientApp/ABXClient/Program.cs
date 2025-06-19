using ABXClient;

class Program
{
    static void Main(string[] args)
    {
        string host = "localhost";
        int port = 3000;
        string outputFile = "C:\\Users\\DELL\\source\\repos\\ABX_App\\ABXClientApp\\ABXClient\\JsonFile\\output.json";

        var client = new AbxClient(host, port, outputFile);
        client.Run();
    }
}
