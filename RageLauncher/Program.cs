using System.Threading.Tasks;

namespace RageLauncher
{
    public static class Program
    {
        public static async Task Main()
        {
            var launcher = new Launcher();
            await launcher.Launch().ConfigureAwait(false);
        }
    }
}
