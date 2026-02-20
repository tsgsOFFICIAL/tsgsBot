using tsgsBot_C_.StateServices;
using Discord.Interactions;
using tsgsBot_C_.Services;
using tsgsBot_C_.Models;
using Discord.WebSocket;
using Discord.Rest;
using Discord;
using tsgsBot_C_.Utils;

namespace tsgsBot_C_.Bot.Commands.Restricted
{
    public sealed class PollCommand(
        PollFormStateService stateService,
        PollService pollService,
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<PollCommand> logger) : LoggedCommandModule
    {
        [SlashCommand("poll", "Start a poll where users can participate by reacting")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.SendPolls)]
        public async Task PollAsync()
        {
            stateService.Clear(Context.User.Id); // Clear any previous state for this user

            // STEP 1: Duration select menu
            SelectMenuBuilder durationMenu = new SelectMenuBuilder()
                .WithCustomId("poll_duration")
                .WithPlaceholder("How long should the poll last?")
                .AddOption("5 minutes", "5")
                .AddOption("15 minutes", "15")
                .AddOption("30 minutes", "30")
                .AddOption("1 hour", "60")
                .AddOption("3 hours", "180")
                .AddOption("6 hours", "360")
                .AddOption("12 hours", "720")
                .AddOption("24 hours", "1440")
                .AddOption("3 days", "4320")
                .AddOption("1 week", "10080")
                .AddOption("2 weeks", "20160")
                .AddOption("3 weeks", "30240")
                .AddOption("1 month", "43200");

            ComponentBuilder durationRow = new ComponentBuilder().WithSelectMenu(durationMenu);

            await RespondAsync("First, select the duration of your poll:",
                components: durationRow.Build(),
                ephemeral: true);

            await LogCommandAsync();
        }

        [ComponentInteraction("poll_duration")]
        public async Task HandleDurationAsync(string[] values)
        {
            await DeferAsync(ephemeral: true);

            if (values.Length == 0)
                return;

            UserPollFormState state = stateService.GetOrCreate(Context.User.Id);
            state.DurationMinutes = int.Parse(values[0]);

            ComponentBuilder continueBtn = new ComponentBuilder()
                .WithButton("Continue →", "poll_continue", ButtonStyle.Primary);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = $"Duration set to {state.DurationMinutes} minutes.\nClick Continue to set up your poll.";
                msg.Components = continueBtn.Build();
                msg.Flags = MessageFlags.Ephemeral;
            });
        }

        [ComponentInteraction("poll_continue")]
        public async Task HandleContinueAsync()
        {
            ModalBuilder modal = new ModalBuilder()
                .WithCustomId("poll_modal")
                .WithTitle("Create Your Poll")
                .AddTextInput("Poll Question", "question", TextInputStyle.Short, placeholder: "Are we raiding tonight?", required: true)
                .AddTextInput("Answers (one per line, 2–10)", "answers", TextInputStyle.Paragraph, placeholder: "Yes definitely!\nNo way\nOnly if bribed", required: true)
                .AddTextInput("Custom Emojis (one per line, optional)", "emojis", TextInputStyle.Paragraph, placeholder: ":pepe:\n🤨\n:banhammer:", required: false)
                .AddFileUpload("Image (optional)", "image", 0, 1, false);

            await RespondWithModalAsync(modal.Build());
            await DeleteOriginalResponseAsync(); // Clean up the previous message
        }

        [ModalInteraction("poll_modal")]
        public async Task HandleModalSubmittedAsync(PollModalModel modal)
        {
            await DeferAsync(ephemeral: true);

            UserPollFormState state = stateService.GetOrCreate(Context.User.Id);

            // Get uploaded image if present
            string? uploadedImageUrl = null;
            if (Context.Interaction is SocketModal socketModal && socketModal.Data.Attachments != null && socketModal.Data.Attachments.Count() > 0)
            {
                uploadedImageUrl = socketModal.Data.Attachments.First().Url;
                state.ImageUrl = uploadedImageUrl;
            }
            else
            {
                state.ImageUrl = null;
            }

            string question = modal.Question.Trim();
            List<string> answers = modal.Answers.Trim().Split('\n').Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)).ToList();
            if (answers.Count < 2 || answers.Count > 10)
            {
                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "You must provide 2–10 answers.";
                    msg.Components = new ComponentBuilder().Build(); // Clear components
                    msg.Flags = MessageFlags.Ephemeral;
                });

                return;
            }

            List<string> rawEmojis = modal.Emojis?.Trim().Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList() ?? new List<string>();

            // Parse emojis (guild custom or unicode)
            List<string> emojis = [.. rawEmojis.Select(emoji =>
            {
                if (emoji.StartsWith(':') && emoji.EndsWith(':') && Context.Guild != null)
                {
                    string name = emoji.Trim(':');
                    GuildEmote? guildEmoji = Context.Guild.Emotes.FirstOrDefault(em => em.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (guildEmoji != null)
                        return guildEmoji.ToString(); // <:name:id>
                }

                return emoji;
            })];

            // Fill with defaults if missing
            string[] defaultEmojis = ["1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟"];

            while (emojis.Count < answers.Count)
            {
                emojis.Add(defaultEmojis[emojis.Count]);
            }

            emojis = [.. emojis.Distinct()];

            // Get the display name and avatar URL safely
            string displayName = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;
            string avatarUrl = Context.User.GetAvatarUrl(size: 512);

            // Preview embed
            EmbedBuilder previewEmbed = new EmbedBuilder()
                .WithTitle("📊 Poll (Preview)")
                .WithAuthor(displayName, avatarUrl, "https://discord.gg/Cddu5aJ")
                .WithDescription(
                    $"{question}\n\n" +
                    string.Join("\n", answers.Select((a, i) => $"{emojis[i]} {a}")) +
                    $"\n\n⏳ Ends: <t:{DateTimeOffset.UtcNow.AddMinutes(state.DurationMinutes).ToUnixTimeSeconds()}:R>")
                .WithColor(Color.Teal);

            if (!string.IsNullOrEmpty(state.ImageUrl))
            {
                previewEmbed.WithImageUrl(state.ImageUrl);
            }

            ComponentBuilder confirmRow = new ComponentBuilder()
                .WithButton("Create Poll", "poll_confirm", ButtonStyle.Success)
                .WithButton("Edit", "poll_edit", ButtonStyle.Secondary)
                .WithButton("Cancel", "poll_cancel", ButtonStyle.Danger);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Does this look good?";
                msg.Embed = previewEmbed.Build();
                msg.Components = confirmRow.Build();
                msg.Flags = MessageFlags.Ephemeral;
            });

            state.ModalData = modal;
        }

        [ComponentInteraction("poll_confirm")]
        public async Task HandleConfirmAsync()
        {
            await DeferAsync();

            UserPollFormState state = stateService.GetOrCreate(Context.User.Id);

            string[] answers = state.ModalData!.Answers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            List<string> rawEmojis = state.ModalData!.Emojis?.Trim().Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList() ?? new List<string>();

            // Parse emojis (guild custom or unicode)
            List<string> emojis = [.. rawEmojis.Select(emoji =>
            {
                if (emoji.StartsWith(':') && emoji.EndsWith(':') && Context.Guild != null)
                {
                    string name = emoji.Trim(':');
                    GuildEmote? guildEmoji = Context.Guild.Emotes.FirstOrDefault(em => em.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (guildEmoji != null)
                        return guildEmoji.ToString(); // <:name:id>
                }

                return emoji;
            })];

            // Fill with defaults if missing
            string[] defaultEmojis = ["1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟"];

            while (emojis.Count < answers.Length)
            {
                emojis.Add(defaultEmojis[emojis.Count]);
            }

            emojis = [.. emojis.Distinct()];

            DateTime endTime = DateTime.UtcNow.AddMinutes(state.DurationMinutes);
            EmbedBuilder pollEmbed = new EmbedBuilder()
                .WithTitle("📊 Poll")
                .WithAuthor(
                    (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username,
                    Context.User.GetAvatarUrl(size: 512),
                    "https://discord.gg/Cddu5aJ")
                .WithDescription(
                    $"{state.ModalData.Question}\n\n" +
                    string.Join("\n", answers.Select((a, i) => $"{emojis[i]} {a}")) +
                    $"\n\n⏳ Ends: <t:{DateTimeOffset.UtcNow.AddMinutes(state.DurationMinutes).ToUnixTimeSeconds()}:R>")
                .WithColor(Color.Teal);

            if (!string.IsNullOrEmpty(state.ImageUrl))
            {
                pollEmbed.WithImageUrl(state.ImageUrl);
            }

            RestUserMessage pollMessage = await Context.Channel.SendMessageAsync(embed: pollEmbed.Build());
            await DeleteOriginalResponseAsync(); // Clean up the ephemeral message

            foreach (string emoji in emojis)
            {
                IEmote emote = Emote.TryParse(emoji, out Emote? parsed) ? parsed : new Emoji(emoji);
                await pollMessage.AddReactionAsync(emote);
            }

            logger.LogInformation("Poll created by {User} ({UserId}) in Guild {GuildId}, Channel {ChannelId}, Message {MessageId}",
                Context.User.Username,
                Context.User.Id,
                Context.Guild!.Id,
                Context.Channel.Id,
                pollMessage.Id);

            try
            {
                int pollId = await DatabaseService.Instance.CreatePollAsync(
                        pollMessage.Id.ToString(),
                        pollMessage.Channel.Id.ToString(),
                        Context.Guild!.Id.ToString(),
                        state.ModalData!.Question,
                        [.. answers],
                        emojis,
                        endTime,
                        Context.User.Id
                    );

                logger.LogInformation("Poll recorded in database with PollId {PollId}", pollId);

                // Queue finalization as a background task for tracking and unified handling
                BackgroundTask backgroundTask = new BackgroundTask
                {
                    TaskType = "PollFinalization",
                    Description = $"Poll finalization for poll {pollId}",
                    Work = async (ct) =>
                    {
                        try
                        {
                            TimeSpan timeLeft = endTime - DateTime.UtcNow;

                            logger.LogInformation("Poll finalization task started for PollId {PollId}, waiting {TimeLeft} until end time", pollId, timeLeft);

                            if (timeLeft > TimeSpan.Zero)
                                await DelayHelper.DelayAsync(timeLeft, ct);

                            // Fetch fresh message for reactions
                            if (await pollMessage.Channel.GetMessageAsync(pollMessage.Id) is IUserMessage message)
                            {
                                await pollService.FinalizePollAsync(message, state.ModalData!.Question, [.. answers], emojis, pollId, Context.User.Id);
                                logger.LogInformation("Successfully finalized poll {PollId}", pollId);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            logger.LogInformation("Poll {PollId} finalization was cancelled during bot shutdown", pollId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error in poll finalization for poll {PollId}", pollId);
                        }
                    }
                };
                await backgroundTaskQueue.QueueAsync(backgroundTask);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to record poll in database for MessageId {MessageId}", pollMessage.Id);
            }

            // Clear state
            stateService.Clear(Context.User.Id);
        }

        [ComponentInteraction("poll_edit")]
        public async Task HandleEditAsync()
        {
            UserPollFormState state = stateService.GetOrCreate(Context.User.Id);

            // Re-show the modal with previous values
            ModalBuilder modal = new ModalBuilder()
                .WithCustomId("poll_modal")
                .WithTitle("Create Your Poll")
                .AddTextInput("Poll Question", "question", TextInputStyle.Short, placeholder: "Are we raiding tonight?", value: state.ModalData?.Question, required: true)
                .AddTextInput("Answers (one per line, 2–10)", "answers", TextInputStyle.Paragraph, placeholder: "Yes definitely!\nNo way\nOnly if bribed", value: state.ModalData?.Answers, required: true)
                .AddTextInput("Custom Emojis (one per line, optional)", "emojis", TextInputStyle.Paragraph, placeholder: ":pepe:\n🤨\n:banhammer:", value: state.ModalData?.Emojis, required: false)
                .AddFileUpload("Image (optional)", "image", 0, 1, false);

            await RespondWithModalAsync(modal.Build());
            await DeleteOriginalResponseAsync();
        }

        [ComponentInteraction("poll_cancel")]
        public async Task HandleCancelAsync()
        {
            await DeferAsync();
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Poll creation cancelled.";
                msg.Embed = null;
                msg.Components = new ComponentBuilder().Build(); // Clear components
                msg.Flags = MessageFlags.Ephemeral;
            });
        }
    }
}