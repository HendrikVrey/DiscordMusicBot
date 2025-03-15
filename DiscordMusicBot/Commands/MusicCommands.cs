using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using System.Collections.Concurrent;

namespace DiscordBot.Commands
{
    public class MusicCommands : BaseCommandModule
    {
        private static readonly ConcurrentQueue<LavalinkTrack> MusicQueue = new ConcurrentQueue<LavalinkTrack>();
        private static bool isPlaying = false;
        private LavalinkGuildConnection connection;
        private DiscordChannel musicTextChannel;

        [Command("play")]
        public async Task PlayMusic(CommandContext ctx, [RemainingText] string query)
        {
            if (!await ValidateVoiceChannel(ctx).ConfigureAwait(false))
                return;

            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();

            if (connection == null)
            {
                connection = await ConnectToVoiceChannel(ctx, node).ConfigureAwait(false);
                if (connection == null)
                    return;

                musicTextChannel = ctx.Channel;

                connection.PlaybackFinished += async (s, e) => await OnPlaybackFinished().ConfigureAwait(false);
            }

            var searchQuery = await node.Rest.GetTracksAsync(query).ConfigureAwait(false);
            if (searchQuery.LoadResultType == LavalinkLoadResultType.NoMatches ||
                searchQuery.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {
                await ctx.Channel.SendMessageAsync($"Failed to find music with query: {query}").ConfigureAwait(false);
                return;
            }

            var musicTrack = searchQuery.Tracks.First();
            MusicQueue.Enqueue(musicTrack);

            if (!isPlaying)
            {
                isPlaying = true;
                await PlayFromQueue().ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendMessageAsync($"Added to queue: {musicTrack.Title}").ConfigureAwait(false);
            }
        }

        [Command("skip")]
        public async Task Skip(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx).ConfigureAwait(false))
                return;

            if (connection == null || connection.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No track is currently playing.").ConfigureAwait(false);
                return;
            }

            await connection.StopAsync().ConfigureAwait(false);

            var skipEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Orange,
                Title = "Track Skipped!",
                Description = "Skipped to the next track."
            };

            await ctx.Channel.SendMessageAsync(embed: skipEmbed).ConfigureAwait(false);
        }

        [Command("pause")]
        public async Task PauseMusic(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx).ConfigureAwait(false))
                return;

            if (connection == null || connection.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No tracks are playing.").ConfigureAwait(false);
                return;
            }

            await connection.PauseAsync().ConfigureAwait(false);

            var pausedEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Yellow,
                Title = "Track Paused."
            };

            await ctx.Channel.SendMessageAsync(embed: pausedEmbed).ConfigureAwait(false);
        }

        [Command("resume")]
        public async Task ResumeMusic(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx).ConfigureAwait(false))
                return;

            if (connection == null || connection.CurrentState.CurrentTrack == null)
            {
                await ctx.Channel.SendMessageAsync("No tracks are playing.").ConfigureAwait(false);
                return;
            }

            await connection.ResumeAsync().ConfigureAwait(false);

            var resumedEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Green,
                Title = "Resumed"
            };

            await ctx.Channel.SendMessageAsync(embed: resumedEmbed).ConfigureAwait(false);
        }

        [Command("leave")]
        public async Task Leave(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx).ConfigureAwait(false))
                return;

            if (connection == null)
            {
                await ctx.Channel.SendMessageAsync("I am not connected to a voice channel.").ConfigureAwait(false);
                return;
            }

            await connection.StopAsync().ConfigureAwait(false);
            ClearQueue();
            isPlaying = false;

            await connection.DisconnectAsync().ConfigureAwait(false);
            connection = null;
            musicTextChannel = null;

            var leaveEmbed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Gray,
                Title = "Disconnected",
                Description = "The bot has disconnected from the voice channel."
            };

            await ctx.Channel.SendMessageAsync(embed: leaveEmbed).ConfigureAwait(false);
        }

        private async Task<LavalinkGuildConnection> ConnectToVoiceChannel(CommandContext ctx, LavalinkNodeConnection node)
        {
            var userVC = ctx.Member.VoiceState.Channel;
            await node.ConnectAsync(userVC).ConfigureAwait(false);
            var conn = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            if (conn == null)
            {
                await ctx.Channel.SendMessageAsync("Lavalink failed to connect.").ConfigureAwait(false);
                return null;
            }
            return conn;
        }

        private async Task PlayFromQueue()
        {
            if (MusicQueue.TryDequeue(out var musicTrack))
            {
                await connection.PlayAsync(musicTrack).ConfigureAwait(false);
                string musicDescription = $"Now Playing: {musicTrack.Title} \n" +
                                          $"Author: {musicTrack.Author} \n" +
                                          $"URL: {musicTrack.Uri}";

                var nowPlayingEmbed = new DiscordEmbedBuilder
                {
                    Color = DiscordColor.Purple,
                    Title = "Now playing",
                    Description = musicDescription
                };

                await musicTextChannel.SendMessageAsync(embed: nowPlayingEmbed).ConfigureAwait(false);
            }
            else
            {
                isPlaying = false;
            }
        }

        private async Task OnPlaybackFinished()
        {
            if (connection == null) return;

            if (!MusicQueue.IsEmpty)
            {
                await PlayFromQueue().ConfigureAwait(false);
            }
            else
            {
                isPlaying = false;
            }
        }

        private void ClearQueue()
        {
            while (MusicQueue.TryDequeue(out _)) { }
        }

        private async Task<bool> ValidateVoiceChannel(CommandContext ctx)
        {
            var userVC = ctx.Member?.VoiceState?.Channel;
            if (userVC == null)
            {
                await ctx.Channel.SendMessageAsync("Please join a voice channel first.").ConfigureAwait(false);
                return false;
            }

            if (!ctx.Client.GetLavalink().ConnectedNodes.Any())
            {
                await ctx.Channel.SendMessageAsync("Connection to Lavalink is not established.").ConfigureAwait(false);
                return false;
            }

            if (userVC.Type != ChannelType.Voice)
            {
                await ctx.Channel.SendMessageAsync("Please enter a valid Voice Channel.").ConfigureAwait(false);
                return false;
            }

            return true;
        }
    }
}
