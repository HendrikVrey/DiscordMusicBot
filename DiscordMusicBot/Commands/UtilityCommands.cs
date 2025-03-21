﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;

namespace DiscordBot.Commands
{
    public class UtilityCommands : BaseCommandModule
    {
        [Command("help")]
        public async Task Help(CommandContext ctx)
        {
            var helpEmbed = new DiscordEmbedBuilder
            {
                Title = "Music Bot Commands",
                Color = DiscordColor.CornflowerBlue,
                Description = "List of available commands for the bot"
            }
            .AddField("`!play <query>`", "Searches for the specified song and plays it (Only Youtube is supported for now). If other tracks are already queued, it adds the song to the queue.")
            .AddField("`!skip`", "Skips the currently playing track and moves to the next one in the queue.")
            .AddField("`!pause`", "Pauses the currently playing track.")
            .AddField("`!resume`", "Resumes the playback of a paused track.")
            .AddField("`!leave`", "Disconnects the bot from the voice channel.")
            //.AddField("`!clear`", "Deletes a specified amount of messages. If you have the permissions to delete messages on the server. Not older than 2 weeks. Example: !clear 100 <Deletes previous 100 messages in selected channel>.")
            .AddField("`!poll`", "Creates a poll. Use `|` to separate options. Example: !poll Game | Dota2 | CS2 <Creates a poll with the caption Game for Guild Members to select between Dota2 or CS2 >.");

            await ctx.Channel.SendMessageAsync(embed: helpEmbed);
        }

        [Command("join")]
        public async Task JoinCommand(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

            var userVC = ctx.Member.VoiceState.Channel;
            var lavalinkInstance = ctx.Client.GetLavalink();
            var node = lavalinkInstance.ConnectedNodes.Values.First();
            await node.ConnectAsync(userVC);

            await ctx.RespondAsync($"Connected to `{userVC.Name}`!");          
        }

        //[Command("clear")]
        //[RequirePermissions(Permissions.ManageMessages)]
        //public async Task ClearCommand(CommandContext ctx, [Description("Number of messages to delete.")] int count)
        //{
        //    try
        //    {
        //        if (count < 1 || count > 1000)
        //        {
        //            await ctx.RespondAsync("Please specify a number between **1** and **1000**");
        //            return;
        //        }

        //        await ctx.Message.DeleteAsync().ConfigureAwait(false);

        //        int totalDeleted = 0;
        //        ulong? lastMessageId = ctx.Message.Id;

        //        while (totalDeleted < count)
        //        {
        //            int remaining = count - totalDeleted;
        //            int chunkSize = Math.Min(remaining, 100);

        //            var messages = await ctx.Channel.GetMessagesBeforeAsync(lastMessageId.Value, chunkSize)
        //                .ConfigureAwait(false);

        //            if (messages.Count == 0) break;

        //            var validMessages = messages
        //                .Where(m => DateTimeOffset.UtcNow - m.CreationTimestamp < TimeSpan.FromDays(14))
        //                .ToList();

        //            if (validMessages.Count == 0) break;

        //            if (validMessages.Count > 1)
        //            {
        //                await ctx.Channel.DeleteMessagesAsync(validMessages).ConfigureAwait(false);
        //            }
        //            else
        //            {
        //                await validMessages[0].DeleteAsync().ConfigureAwait(false);
        //            }

        //            totalDeleted += validMessages.Count;
        //            lastMessageId = validMessages.Last().Id;
        //        }

        //        var confirmation = await ctx.Channel.SendMessageAsync($"Deleted **{totalDeleted}** messages")
        //            .ConfigureAwait(false);
        //        await Task.Delay(3000).ConfigureAwait(false);
        //        await confirmation.DeleteAsync().ConfigureAwait(false);
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Clear command failed: {ex}");
        //        await ctx.RespondAsync($"Error clearing messages: {ex.Message}");
        //    }
        //}

        [Command("poll")]
        public async Task PollCommand(CommandContext ctx, [RemainingText] string questionWithOptions)
        {
            var parts = questionWithOptions.Split('|');
            if (parts.Length < 2)
            {
                await ctx.RespondAsync("Please provide a question and at least two options separated by `|`.");
                return;
            }

            var question = parts[0].Trim();
            var options = parts.Skip(1).Select(option => option.Trim()).ToArray();

            if (options.Length > 10)
            {
                await ctx.RespondAsync("Please provide no more than 10 options.");
                return;
            }

            var votes = new int[options.Length];
            var userVotes = new Dictionary<ulong, int>();

            var embed = new DiscordEmbedBuilder
            {
                Title = "Poll",
                Description = GetPollDescription(question, options, votes),
                Color = DiscordColor.Azure
            };

            var buttons = new List<DiscordButtonComponent>();
            for (int i = 0; i < options.Length; i++)
            {
                buttons.Add(new DiscordButtonComponent(ButtonStyle.Primary, $"poll_option_{i}", options[i]));
            }

            var pollMessage = await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder()
                .WithEmbed(embed)
                .AddComponents(buttons));

            var interactivity = ctx.Client.GetInteractivity();
            if (interactivity == null)
            {
                await ctx.RespondAsync("Interactivity is not enabled. Please ensure it is configured correctly.");
                return;
            }

            while (true)
            {
                var result = await interactivity.WaitForButtonAsync(pollMessage, TimeSpan.FromMinutes(5));
                if (result.TimedOut)
                {
                    await pollMessage.ModifyAsync(new DiscordMessageBuilder()
                        .WithEmbed(embed.WithDescription("Poll timed out."))
                        .AddComponents());
                    break;
                }
                else
                {
                    var userId = result.Result.User.Id;
                    var optionIndex = int.Parse(result.Result.Id.Split('_').Last());

                    if (userVotes.ContainsKey(userId))
                    {
                        await result.Result.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                            .WithContent("You have already voted!")
                            .AsEphemeral(true));
                    }
                    else
                    {
                        userVotes[userId] = optionIndex;
                        votes[optionIndex]++;
                        embed.Description = GetPollDescription(question, options, votes);

                        await pollMessage.ModifyAsync(new DiscordMessageBuilder()
                            .WithEmbed(embed)
                            .AddComponents(buttons));

                        await result.Result.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                    }
                }
            }
        }

        private string GetPollDescription(string question, string[] options, int[] votes)
        {
            var description = $"**{question}**\n\n";
            for (int i = 0; i < options.Length; i++)
            {
                description += $"{options[i]}: {votes[i]} votes\n";
            }
            return description;
        }

        private async Task<bool> ValidateVoiceChannel(CommandContext ctx)
        {
            try
            {
                var userVC = ctx.Member.VoiceState.Channel;

                if (!ctx.Client.GetLavalink().ConnectedNodes.Any())
                {
                    await ctx.RespondAsync("Connection is not established.");
                    return false;
                }

                if (userVC.Type != ChannelType.Voice)
                {
                    await ctx.RespondAsync("Please enter a valid Voice Channel.");
                    return false;
                }

                return true;
            }
            catch
            {
                await ctx.RespondAsync("Please enter a voice channel first.");
                return false;
            }

        }
    }
}
