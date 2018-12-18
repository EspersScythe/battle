using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Lithium.Handlers;
using Lithium.Models;
using Lithium.Services;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Serilog;
using EventHandler = Lithium.Handlers.EventHandler;

namespace Lithium
{
    public class Program
    {
        private CommandHandler _chandler;
        private EventHandler _ehandler;
        public DiscordSocketClient Client;

        public static void Main(string[] args)
        {
            new Program().Start().GetAwaiter().GetResult();
        }

        public async Task Start()
        {
            Console.Title = "Lithium Discord Bot by Passive";

            if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "setup/")))
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "setup/"));
            if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "setup/backups")))
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "setup/backups"));
            Config.CheckExistence();

            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 50
            });


            try
            {
                await Client.LoginAsync(TokenType.Bot, Config.Load().BotToken);
                await Client.StartAsync();
            }
            catch (Exception e)
            {
                Log.Information("------------------------------------\n" +
                                $"{e}\n" +
                                "------------------------------------\n" +
                                "Token was rejected by Discord (Invalid Token or Connection Error)\n" +
                                "------------------------------------");
            }


            var serviceProvider = ConfigureServices();
            _chandler = new CommandHandler(serviceProvider);
            _ehandler = new EventHandler(serviceProvider);
            await _chandler.ConfigureAsync();
            Client.Log += Client_Log;
            await Task.Delay(-1);
        }

        private static Task Client_Log(LogMessage arg)
        {
            Logger.LogMessage(arg.Message, arg.Severity);
            return Task.CompletedTask;
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection()
                .AddSingleton(Client)
                .AddSingleton(new DocumentStore
                {
                    Database = DatabaseHandler.DBName,
                    Urls = new[] {DatabaseHandler.ServerURL}
                }.Initialize())
                .AddSingleton(new DatabaseHandler(new DocumentStore {Urls = new[] {Config.Load().ServerURL}}.Initialize()))
                .AddSingleton(new TimerService(Client))
                .AddSingleton(new InteractiveService(Client))
                .AddSingleton(new CommandService(
                    new CommandServiceConfig
                    {
                        CaseSensitiveCommands = false,
                        ThrowOnError = false,
                        DefaultRunMode = RunMode.Async
                    }));

            return services.BuildServiceProvider();
        }
    }
}