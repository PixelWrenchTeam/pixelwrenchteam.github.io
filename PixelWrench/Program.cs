using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

namespace PixelWrench
{
    [SupportedOSPlatform("browser")]
    internal partial class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                await BuildAvaloniaApp()
                    .WithInterFont()
                    .StartBrowserAppAsync("out");
                
                await Task.Delay(-1); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOT ERROR] {ex.Message}");
                Console.WriteLine(ex.ToString());
                throw;
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>();
    }
}