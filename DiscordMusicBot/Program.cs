using DiscordBot.Commands;
using DiscordBot.Config;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;

namespace DiscordMusicBot
{
    internal class Program
    {
        private static DiscordClient? Client { get; set; }
        private static CommandsNextExtension? Commands { get; set; }

        static async Task Main(string[] args)
        {
            try
            {
                var jsonReader = new JSONReader();
                await jsonReader.ReadJSON().ConfigureAwait(false);

                var discordConfig = new DiscordConfiguration
                {
                    Intents = DiscordIntents.All,
                    Token = jsonReader.token,
                    TokenType = TokenType.Bot,
                    AutoReconnect = true
                };

                Client = new DiscordClient(discordConfig);
                Client.Ready += Client_Ready;

                var commandsConfig = new CommandsNextConfiguration
                {
                    StringPrefixes = new string[] { jsonReader.prefix },
                    EnableMentionPrefix = true,
                    EnableDms = true,
                    EnableDefaultHelp = false,
                };

                Client.UseInteractivity(new InteractivityConfiguration
                {
                    Timeout = TimeSpan.FromMinutes(2)
                });

                Commands = Client.UseCommandsNext(commandsConfig);

                Commands.RegisterCommands<MusicCommands>();
                Commands.RegisterCommands<UtilityCommands>();

                var endPoint = new ConnectionEndpoint
                {
                    Hostname = "lava-v3.ajieblogs.eu.org",
                    Port = 443,
                    Secured = true,
                };

                var lavalinkConfig = new LavalinkConfiguration
                {
                    Password = "https://dsc.gg/ajidevserver",
                    RestEndpoint = endPoint,
                    SocketEndpoint = endPoint
                };

                var lavaLink = Client.UseLavalink();

                await Client.ConnectAsync().ConfigureAwait(false);
                await lavaLink.ConnectAsync(lavalinkConfig).ConfigureAwait(false);

                Console.WriteLine("Bot is connected and running.");
                await Task.Delay(-1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error during startup: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static Task Client_Ready(DiscordClient sender, DSharpPlus.EventArgs.ReadyEventArgs args)
        {
            Console.WriteLine("Bot is ready!");
            return Task.CompletedTask;
        }
    }
}
