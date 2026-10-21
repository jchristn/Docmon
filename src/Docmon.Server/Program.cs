namespace Docmon.Server
{
    using System.Threading.Tasks;
    internal static class Program
    {
        private static Task Main(string[] args) => DocmonServer.RunAsync();
    }
}
