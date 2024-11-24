using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class MusicCommands : BaseCommandModule
    {
        private static readonly ConcurrentQueue<LavalinkTrack> MusicQueue = new ConcurrentQueue<LavalinkTrack>();
        private static bool isPlaying = false;
        private static readonly object queueLock = new object();

        [Command("play")]
        public async Task PlayMusic(CommandContext ctx, [RemainingText] string query)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = await ConnectToVoiceChannel(ctx, node);

            if (conn == null)
                return;

            var searchQuery = await node.Rest.GetTracksAsync(query);
            if (searchQuery.LoadResultType == LavalinkLoadResultType.NoMatches || searchQuery.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {
                await ctx.Channel.SendMessageAsync($"Failed to find music with query: {query}");
                return;
            }

            var musicTrack = searchQuery.Tracks.First();
            bool shouldStartPlaying = false;

            lock (queueLock)
            {
                MusicQueue.Enqueue(musicTrack);

                if (!isPlaying)
                {
                    isPlaying = true;
                    shouldStartPlaying = true;
                }
            }

            if (shouldStartPlaying)
            {
                await PlayFromQueue(ctx, conn);
            }
            else
            {
                await ctx.Channel.SendMessageAsync($"Added to queue: {musicTrack.Title}");
            }
        }

        [Command("skip")]
        public async Task Skip(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null || conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is currently playing.");
                return;
            }

            await conn.StopAsync();

            var skipEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Orange,
                Title = "Track Skipped!",
                Description = "Skipped to the next track."
            };

            await ctx.Channel.SendMessageAsync(embed: skipEmbed);
        }

        [Command("pause")]
        public async Task PauseMusic(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null || conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No tracks are playing.");
                return;
            }

            await conn.PauseAsync();

            var pausedEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Yellow,
                Title = "Track Paused."
            };

            await ctx.Channel.SendMessageAsync(embed: pausedEmbed);
        }

        [Command("resume")]
        public async Task ResumeMusic(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null || conn.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No tracks are playing.");
                return;
            }

            await conn.ResumeAsync();

            var resumedEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Green,
                Title = "Resumed"
            };

            await ctx.Channel.SendMessageAsync(embed: resumedEmbed);
        }

        [Command("leave")]
        public async Task Leave(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("I am not connected to this voice channel.");
                return;
            }

            await conn.StopAsync();
            ClearQueue();
            isPlaying = false;

            await conn.DisconnectAsync();

            var leaveEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Gray,
                Title = "Disconnected",
                Description = "The bot has disconnected from the voice channel."
            };

            await ctx.Channel.SendMessageAsync(embed: leaveEmbed);
        }

        private async Task<bool> ValidateVoiceChannel(CommandContext ctx)
        {
            var userVC = ctx.Member.VoiceState.Channel;
            if (userVC == null)
            {
                await ctx.Channel.SendMessageAsync("Please enter a Voice Channel.");
                return false;
            }

            if (!ctx.Client.GetLavalink().ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Connection is not Established.");
                return false;
            }

            if (userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("Please enter a valid Voice Channel.");
                return false;
            }

            return true;
        }

        private async Task<LavalinkGuildConnection> ConnectToVoiceChannel(CommandContext ctx, LavalinkNodeConnection node)
        {
            var userVC = ctx.Member.VoiceState.Channel;
            await node.ConnectAsync(userVC);
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);

            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("Lavalink Failed to connect.");
                return null;
            }

            return conn;
        }

        private async Task PlayFromQueue(CommandContext ctx, LavalinkGuildConnection conn)
        {
            LavalinkTrack musicTrack;
            lock (queueLock)
            {
                if (!MusicQueue.TryDequeue(out musicTrack))
                {
                    isPlaying = false;
                    return;
                }
            }

            await conn.PlayAsync(musicTrack);
            string musicDescription = $"Now Playing: {musicTrack.Title} \n" +
                                      $"Author: {musicTrack.Author} \n" +
                                      $"URL: {musicTrack.Uri}";

            var nowPlayingEmbed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Purple,
                Title = "Now playing",
                Description = musicDescription
            };

            await ctx.Channel.SendMessageAsync(embed: nowPlayingEmbed);

            conn.PlaybackFinished += async (s, e) => await OnPlaybackFinished(ctx, conn);
        }

        private async Task OnPlaybackFinished(CommandContext ctx, LavalinkGuildConnection conn)
        {
            lock (queueLock)
            {
                if (!MusicQueue.Any())
                {
                    isPlaying = false;
                    return;
                }
            }

            await PlayFromQueue(ctx, conn);
        }

        private void ClearQueue()
        {
            lock (queueLock)
            {
                while (MusicQueue.TryDequeue(out _)) { }
            }
        }
    }
}
