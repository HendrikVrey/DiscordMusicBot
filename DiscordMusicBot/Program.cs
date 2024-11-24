using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using DSharpPlus;
using Newtonsoft.Json;
using DiscordBot.Commands;
using DSharpPlus.Interactivity.Extensions;
using DiscordBot.Config;

namespace DiscordMusicBot
{
    internal class Program
    {
        private static DiscordClient? Client { get; set; }
        private static CommandsNextExtension? Commands { get; set; }

        static async Task Main(string[] args)
        {
            var jsonReader = new JSONReader();
            await jsonReader.ReadJSON();

            var discordConfig = new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = jsonReader.token,
                TokenType = TokenType.Bot,
                AutoReconnect = true
            };

            Client = new DiscordClient(discordConfig);

            Client.Ready += Client_Ready;

            var commandsConfig = new CommandsNextConfiguration()
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

            //Commands.RegisterCommands<GameCommands>();


            //Change this to your Lavalink Endpoint
            var endPoint = new ConnectionEndpoint
            {
                Hostname = "lava-v3.ajieblogs.eu.org",
                Port = 443,
                Secured = true,
            };

            //Change the password to your Lavalink Password
            var LavaLinkConfig = new LavalinkConfiguration()
            {
                Password = "https://dsc.gg/ajidevserver",
                RestEndpoint = endPoint,
                SocketEndpoint = endPoint
            };

            var LavaLink = Client.UseLavalink();

            await Client.ConnectAsync();
            await LavaLink.ConnectAsync(LavaLinkConfig);
            await Task.Delay(-1);
        }

        private static Task Client_Ready(DiscordClient sender, DSharpPlus.EventArgs.ReadyEventArgs args)
        {
            return Task.CompletedTask;
        }
    }
}
