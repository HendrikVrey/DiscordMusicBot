using DSharpPlus;
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
            try
            {
                var helpEmbed = new DiscordEmbedBuilder
                {
                    Title = "Music Bot Commands",
                    Color = DiscordColor.CornflowerBlue,
                    Description = "List of available commands for the bot"
                }
                .AddField("`!play <query>`", "Searches for the specified song and plays it. If other tracks are already queued, it adds the song to the queue.")
                .AddField("`!skip`", "Skips the currently playing track and moves to the next one in the queue.")
                .AddField("`!pause`", "Pauses the currently playing track.")
                .AddField("`!resume`", "Resumes the playback of a paused track.")
                .AddField("`!leave`", "Disconnects the bot from the voice channel.")
                .AddField("`!poll`", "Creates a poll. Use `|` to separate options. Example: !poll Game | Dota2 | CS2");

                await ctx.Channel.SendMessageAsync(embed: helpEmbed).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Help command: {ex.Message}");
                await ctx.RespondAsync("An error occurred while displaying help information.").ConfigureAwait(false);
            }
        }

        [Command("join")]
        public async Task JoinCommand(CommandContext ctx)
        {
            try
            {
                if (!await ValidateVoiceChannel(ctx).ConfigureAwait(false))
                    return;

                var userVC = ctx.Member.VoiceState.Channel;
                var lavalinkInstance = ctx.Client.GetLavalink();

                if (lavalinkInstance == null || !lavalinkInstance.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync("Lavalink connection is not established.").ConfigureAwait(false);
                    return;
                }

                var node = lavalinkInstance.ConnectedNodes.Values.FirstOrDefault();
                if (node == null)
                {
                    await ctx.RespondAsync("No Lavalink node found.").ConfigureAwait(false);
                    return;
                }

                await node.ConnectAsync(userVC).ConfigureAwait(false);
                await ctx.RespondAsync($"Connected to `{userVC.Name}`!").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in JoinCommand: {ex.Message}");
                await ctx.RespondAsync("An error occurred while trying to join the voice channel.").ConfigureAwait(false);
            }
        }

        [Command("poll")]
        public async Task PollCommand(CommandContext ctx, [RemainingText] string questionWithOptions)
        {
            try
            {
                var parts = questionWithOptions.Split('|');
                if (parts.Length < 2)
                {
                    await ctx.RespondAsync("Please provide a question and at least two options separated by `|`.").ConfigureAwait(false);
                    return;
                }

                var question = parts[0].Trim();
                var options = parts.Skip(1).Select(option => option.Trim()).Where(o => !string.IsNullOrEmpty(o)).ToArray();

                if (options.Length < 2)
                {
                    await ctx.RespondAsync("Please provide at least two valid options.").ConfigureAwait(false);
                    return;
                }

                if (options.Length > 10)
                {
                    await ctx.RespondAsync("Please provide no more than 10 options.").ConfigureAwait(false);
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
                    .AddComponents(buttons)).ConfigureAwait(false);

                var interactivity = ctx.Client.GetInteractivity();
                if (interactivity == null)
                {
                    await ctx.RespondAsync("Interactivity is not enabled. Please ensure it is configured correctly.").ConfigureAwait(false);
                    return;
                }

                while (true)
                {
                    var result = await interactivity.WaitForButtonAsync(pollMessage, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                    if (result.TimedOut)
                    {
                        await pollMessage.ModifyAsync(new DiscordMessageBuilder()
                            .WithEmbed(embed.WithDescription("Poll timed out."))
                            .AddComponents()).ConfigureAwait(false);
                        break;
                    }
                    else
                    {
                        try
                        {
                            var userId = result.Result.User.Id;
                            var idParts = result.Result.Id.Split('_');
                            if (idParts.Length == 0 || !int.TryParse(idParts.Last(), out int optionIndex))
                            {
                                await result.Result.Interaction.CreateResponseAsync(
                                    InteractionResponseType.ChannelMessageWithSource,
                                    new DiscordInteractionResponseBuilder().WithContent("Invalid vote option.").AsEphemeral(true)).ConfigureAwait(false);
                                continue;
                            }

                            if (userVotes.ContainsKey(userId))
                            {
                                await result.Result.Interaction.CreateResponseAsync(
                                    InteractionResponseType.ChannelMessageWithSource,
                                    new DiscordInteractionResponseBuilder().WithContent("You have already voted!").AsEphemeral(true)).ConfigureAwait(false);
                            }
                            else
                            {
                                userVotes[userId] = optionIndex;
                                votes[optionIndex]++;
                                embed.Description = GetPollDescription(question, options, votes);

                                await pollMessage.ModifyAsync(new DiscordMessageBuilder()
                                    .WithEmbed(embed)
                                    .AddComponents(buttons)).ConfigureAwait(false);

                                await result.Result.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage).ConfigureAwait(false);
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Console.WriteLine($"Error during poll voting: {innerEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PollCommand: {ex.Message}");
                await ctx.RespondAsync("An error occurred while creating the poll.").ConfigureAwait(false);
            }
        }

        private string GetPollDescription(string question, string[] options, int[] votes)
        {
            var description = $"**{question}**\n\n";
            for (int i = 0; i < options.Length; i++)
            {
                description += $"{options[i]}: {votes[i]} vote{(votes[i] == 1 ? "" : "s")}\n";
            }
            return description;
        }

        private async Task<bool> ValidateVoiceChannel(CommandContext ctx)
        {
            try
            {
                var userVC = ctx.Member?.VoiceState?.Channel;
                if (userVC == null)
                {
                    await ctx.RespondAsync("Please join a voice channel first.").ConfigureAwait(false);
                    return false;
                }

                var lavalink = ctx.Client.GetLavalink();
                if (lavalink == null || !lavalink.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync("Lavalink connection is not established.").ConfigureAwait(false);
                    return false;
                }

                if (userVC.Type != ChannelType.Voice)
                {
                    await ctx.RespondAsync("Please enter a valid Voice Channel.").ConfigureAwait(false);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ValidateVoiceChannel: {ex.Message}");
                await ctx.RespondAsync("An error occurred while validating the voice channel.").ConfigureAwait(false);
                return false;
            }
        }
    }
}
