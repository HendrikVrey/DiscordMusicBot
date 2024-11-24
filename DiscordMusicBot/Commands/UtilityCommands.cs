using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Interactivity;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DiscordBot.Commands
{
    public class UtilityCommands : BaseCommandModule
    {
        [Command("help")]
        public async Task Help(CommandContext ctx)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

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
            .AddField("`!clear`", "Deletes a specified amount of messages. Not older than 2 weeks. Example: !clear 100 <Deletes previous 100 messages in selected channel>.")
            .AddField("`poll`", "Creates a poll. Use `|` to separate options. Example: !poll Game | Dota2 | CS2 <Creates a poll with the caption Game for Guild Members to select between Dota2 or CS2 >.");

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

            await ctx.RespondAsync("Please join a voice channel first.");

            
        }

        [Command("clear")]
        [RequirePermissions(Permissions.ManageMessages)]
        public async Task ClearCommand(CommandContext ctx, [Description("Number of messages to delete.")] int count)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;

            if (count < 1)
            {
                await ctx.RespondAsync("You need to specify a number greater than 0.");
                return;
            }

            var messages = await ctx.Channel.GetMessagesAsync(count + 1);
            if (messages.Count < count + 1)
            {
                await ctx.RespondAsync("Not enough messages to delete.");
                return;
            }

            await ctx.Channel.DeleteMessagesAsync(messages, "Bulk delete command invoked.");

            var confirmation = await ctx.Channel.SendMessageAsync($"Deleted {count} messages.");
            await Task.Delay(3000);
            await confirmation.DeleteAsync();
        }

        [Command("poll")]
        public async Task PollCommand(CommandContext ctx, [RemainingText] string questionWithOptions)
        {
            if (!await ValidateVoiceChannel(ctx))
                return;
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
