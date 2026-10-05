using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WashGo.Background.Host
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .UseAutofac()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddApplicationAsync<WashGoBackgroundHostModule>();
                })
                .Build();

            await host.InitializeAsync();
            await host.RunAsync();
        }
    }
}
