using tsgsBot_C_.StateServices;
using Discord.Interactions;
using tsgsBot_C_.Services;
using tsgsBot_C_.Models;
using Discord.WebSocket;
using Discord.Rest;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Restricted
{
    public sealed class GiveawayCommand(
        GiveawayFormStateService stateService,
        GiveawayService giveawayService,
        IBackgroundTaskQueue backgroundTaskQueue,
        ILogger<GiveawayCommand> logger) : LoggedCommandModule
    {
        [SlashCommand("giveaway", "Start a giveaway where users can participate by reacting")]
        [CommandContextType(InteractionContextType.Guild)]
        [DefaultMemberPermissions(GuildPermission.CreateEvents)]
        public async Task GiveawayAsync()
        {
            stateService.Clear(Context.User.Id); // Clear any existing state for the user

            // STEP 1: Duration select menu
            SelectMenuBuilder durationMenu = new SelectMenuBuilder()
                .WithCustomId("giveaway_duration")
                .WithPlaceholder("How long should the giveaway last?")
                .AddOption("5 minutes", "5")
                .AddOption("15 minutes", "15")
                .AddOption("30 minutes", "30")
                .AddOption("1 hour", "60")
                .AddOption("3 hours", "180")
                .AddOption("6 hours", "360")
                .AddOption("12 hours", "720")
                .AddOption("24 hours", "1440")
                .AddOption("3 days", "4320")
                .AddOption("1 week", "10080");

            ComponentBuilder durationRow = new ComponentBuilder().WithSelectMenu(durationMenu);

            await RespondAsync("First, select the duration of your giveaway:",
                components: durationRow.Build(),
                ephemeral: true);

            await LogCommandAsync();
        }

        [ComponentInteraction("giveaway_duration")]
        public async Task HandleDurationAsync(string[] values)
        {
            await DeferAsync(ephemeral: true);

            if (values.Length == 0)
                return;

            UserGiveawayFormState state = stateService.GetOrCreate(Context.User.Id);
            state.DurationMinutes = int.Parse(values[0]);

            ComponentBuilder continueBtn = new ComponentBuilder()
                .WithButton("Continue →", "giveaway_continue", ButtonStyle.Primary);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = $"Duration set to {state.DurationMinutes} minutes.\nClick Continue to set up your giveaway.";
                msg.Components = continueBtn.Build();
                msg.Flags = MessageFlags.Ephemeral;
            });
        }

        [ComponentInteraction("giveaway_continue")]
        public async Task HandleContinueAsync()
        {
            ModalBuilder modal = new ModalBuilder()
                .WithCustomId("giveaway_modal")
                .WithTitle("Create Your Giveaway")
                .AddTextInput("What's the prize", "prize", TextInputStyle.Short, placeholder: "The key to my heart", required: true)
                .AddTextInput("How many can win", "winners", TextInputStyle.Short, value: "1", required: true)
                .AddTextInput("Reaction ReactionEmoji", "reaction_emoji", TextInputStyle.Short, value: "🎉", required: true);

            await RespondWithModalAsync(modal.Build());
            await DeleteOriginalResponseAsync(); // Clean up the previous message
        }

        [ModalInteraction("giveaway_modal")]
        public async Task HandleModalSubmittedAsync(GiveawayModalModel modal)
        {
            await DeferAsync(ephemeral: true);

            UserGiveawayFormState state = stateService.GetOrCreate(Context.User.Id);

            string rawEmoji = modal.ReactionEmoji?.Trim() ?? string.Empty;

            string emoji = rawEmoji;
            if (rawEmoji.StartsWith(':') && rawEmoji.EndsWith(':') && Context.Guild != null)
            {
                string name = rawEmoji.Trim(':');
                GuildEmote? guildEmoji = Context.Guild.Emotes.FirstOrDefault(em => em.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (guildEmoji != null)
                    emoji = guildEmoji.ToString(); // <:name:id>
            }

            // Get the display name and avatar URL safely
            string displayName = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;
            string avatarUrl = Context.User.GetAvatarUrl(size: 512);

            // Preview embed
            EmbedBuilder previewEmbed = new EmbedBuilder()
                .WithTitle("📊 Giveaway (Preview)")
                .WithAuthor(displayName, avatarUrl, "https://discord.gg/Cddu5aJ")
                .WithDescription(
                        $"**Prize:** {modal.Prize}\n\n" +
                        $"React with {emoji} to enter!\n\n" +
                        $"🏆 **Winners:** {modal.Winners}\n" +
                        $"⏳ **Ends:** <t:{DateTimeOffset.UtcNow.AddMinutes(state.DurationMinutes).ToUnixTimeSeconds()}:R>")
                .WithColor(Color.Teal);

            ComponentBuilder confirmRow = new ComponentBuilder()
                .WithButton("Create Giveaway", "giveaway_confirm", ButtonStyle.Success)
                .WithButton("Cancel", "giveaway_cancel", ButtonStyle.Danger);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Does this look good?";
                msg.Embed = previewEmbed.Build();
                msg.Components = confirmRow.Build();
                msg.Flags = MessageFlags.Ephemeral;
            });

            state.ModalData = modal;
        }

        [ComponentInteraction("giveaway_confirm")]
        public async Task HandleConfirmAsync()
        {
            await DeferAsync();

            UserGiveawayFormState state = stateService.GetOrCreate(Context.User.Id);

            string rawEmoji = state.ModalData!.ReactionEmoji?.Trim() ?? string.Empty;

            string emoji = rawEmoji;
            if (rawEmoji.StartsWith(':') && rawEmoji.EndsWith(':') && Context.Guild != null)
            {
                string name = rawEmoji.Trim(':');
                GuildEmote? guildEmoji = Context.Guild.Emotes.FirstOrDefault(em => em.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (guildEmoji != null)
                    emoji = guildEmoji.ToString(); // <:name:id>
            }

            // Get the display name and avatar URL safely
            string displayName = (Context.User as SocketGuildUser)?.Nickname ?? Context.User.Username;
            string avatarUrl = Context.User.GetAvatarUrl(size: 512);

            // Preview embed
            DateTime endTime = DateTime.UtcNow.AddMinutes(state.DurationMinutes);
            EmbedBuilder giveawayEmbed = new EmbedBuilder()
                .WithTitle("📊 Giveaway")
                .WithAuthor(displayName, avatarUrl, "https://discord.gg/Cddu5aJ")
                .WithDescription(
                        $"**Prize:** {state.ModalData.Prize}\n\n" +
                        $"React with {emoji} to enter!\n\n" +
                        $"🏆 **Winners:** {state.ModalData.Winners}\n" +
                        $"⏳ **Ends:** <t:{DateTimeOffset.UtcNow.AddMinutes(state.DurationMinutes).ToUnixTimeSeconds()}:R>")
                .WithColor(Color.Teal);

            RestUserMessage giveawayMessage = await Context.Channel.SendMessageAsync(embed: giveawayEmbed.Build());
            await DeleteOriginalResponseAsync(); // Clean up the ephemeral message

            IEmote emote = Emote.TryParse(emoji, out Emote? parsed) ? parsed : new Emoji(emoji);
            await giveawayMessage.AddReactionAsync(emote);

            logger.LogInformation("Giveaway created by {User} ({UserId}) in Guild {GuildId}, Channel {ChannelId}, Message {MessageId}",
                Context.User.Username,
                Context.User.Id,
                Context.Guild!.Id,
                Context.Channel.Id,
                giveawayMessage.Id);

            try
            {
                int giveawayId = await DatabaseService.Instance.CreateGiveawayAsync(
                        giveawayMessage.Id.ToString(),
                        giveawayMessage.Channel.Id.ToString(),
                        Context.Guild!.Id.ToString(),
                        state.ModalData.Prize,
                        state.ModalData.ReactionEmoji!,
                        endTime,
                        Context.User.Id
                    );

                logger.LogInformation("Giveaway recorded in database with GiveawayId {GiveawayId}", giveawayId);

                // Queue finalization as a background task for tracking and unified handling
                var backgroundTask = new BackgroundTask
                {
                    TaskType = "GiveawayFinalization",
                    Description = $"Giveaway finalization for giveaway {giveawayId}",
                    Work = async (ct) =>
                    {
                        try
                        {
                            TimeSpan timeLeft = endTime - DateTime.UtcNow;

                            logger.LogInformation("Giveaway finalization task started for GiveawayId {GiveawayId}, waiting {TimeLeft} until end time", giveawayId, timeLeft);

                            if (timeLeft > TimeSpan.Zero)
                                await Task.Delay(timeLeft, ct);

                            // Fetch fresh message for reactions
                            if (await giveawayMessage.Channel.GetMessageAsync(giveawayMessage.Id) is IUserMessage message)
                            {
                                await giveawayService.FinalizeGiveawayAsync(message, state.ModalData.Prize, state.ModalData.ReactionEmoji!, state.ModalData.Winners, giveawayId, Context.User.Id);
                                logger.LogInformation("Successfully finalized giveaway {GiveawayId}", giveawayId);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            logger.LogInformation("Giveaway {GiveawayId} finalization was cancelled during bot shutdown", giveawayId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error in giveaway finalization for giveaway {GiveawayId}", giveawayId);
                        }
                    }
                };
                await backgroundTaskQueue.QueueAsync(backgroundTask);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to record giveaway in database for MessageId {MessageId}", giveawayMessage.Id);
            }

            // Clear state
            stateService.Clear(Context.User.Id);
        }

        [ComponentInteraction("giveaway_cancel")]
        public async Task HandleCancelAsync()
        {
            await DeferAsync();
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "Giveaway creation cancelled.";
                msg.Embed = null;
                msg.Components = new ComponentBuilder().Build(); // Clear components
                msg.Flags = MessageFlags.Ephemeral;
            });
        }
    }
}