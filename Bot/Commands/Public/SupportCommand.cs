using tsgsBot_C_.StateServices;
using Discord.Interactions;
using Discord.WebSocket;
using tsgsBot_C_.Models;
using Discord.Rest;
using Discord;

namespace tsgsBot_C_.Bot.Commands.Public;

public sealed class SupportCommand(SupportFormStateService stateService) : LoggedCommandModule
{
    [SlashCommand("support", "Submit a support request for one of my applications")]
    [CommandContextType(InteractionContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.UseApplicationCommands)]
    public async Task SupportAsync()
    {
        stateService.Clear(Context.User.Id); // Clear any previous state for this user

        SelectMenuBuilder appMenu = new SelectMenuBuilder()
            .WithCustomId("support_app")
            .WithPlaceholder("Which application is this for?")
            .AddOption("CS2 AutoAccept", "cs2aa")
            .AddOption("Stream Drop Collector", "sdc")
            .AddOption("CrosshairY", "crosshairy")
            .AddOption("Rusty Painter", "rustypainter")
            .AddOption("CodeRaider", "coderaider");

        await RespondAsync("First select the **application**.",
            components: new ComponentBuilder().WithSelectMenu(appMenu).Build(),
            ephemeral: true);

        await LogCommandAsync();
    }

    // App selection - updates message with new components
    [ComponentInteraction("support_app")]
    public async Task AppSelected(string[] values)
    {
        await DeferAsync(ephemeral: true);

        if (values.Length == 0)
            return;

        UserSupportFormState state = stateService.GetOrCreate(Context.User.Id);
        state.SelectedApp = values[0];

        string appName = GetAppDisplayName(state.SelectedApp);

        // Build dynamic menus (same as before)
        SelectMenuBuilder issueMenu = new SelectMenuBuilder().WithCustomId("support_issue_type").WithPlaceholder("Select Issue Type")
            .AddOption("🐞 Bug Report", "Bug Report").AddOption("💡 Feature Request", "Feature Request")
            .AddOption("❓ General Question", "General Question").AddOption("⚙️ Other", "Other");

        SelectMenuBuilder reproMenu = new SelectMenuBuilder().WithCustomId("support_repro").WithPlaceholder("Select Reproducibility")
            .AddOption("Every time", "Every time").AddOption("Occasionally", "Occasionally")
            .AddOption("Rarely", "Rarely").AddOption("Only once", "Only once");

        SelectMenuBuilder urgencyMenu = new SelectMenuBuilder().WithCustomId("support_urgency").WithPlaceholder("Select Urgency")
            .AddOption("Low", "Low").AddOption("Medium", "Medium").AddOption("High", "High").AddOption("Critical", "Critical");

        bool showPlatform = state.SelectedApp is not ("coderaider" or "rustypainter" or "crosshairy");

        SelectMenuBuilder? platformMenu = null;
        if (showPlatform)
        {
            platformMenu = new SelectMenuBuilder()
                .WithCustomId("support_platform")
                .WithPlaceholder("Select Platform");

            switch (state.SelectedApp)
            {
                case "cs2aa":
                    platformMenu
                        .AddOption("Faceit", "Faceit")
                        .AddOption("Matchmaking", "Regular Matchmaking")
                        .AddOption("Other", "Other");
                    break;

                case "sdc":
                    platformMenu
                        .AddOption("Twitch", "Twitch")
                        .AddOption("Kick", "Kick")
                        .AddOption("Both", "Both")
                        .AddOption("Other", "Other");
                    break;
            }
        }

        ButtonBuilder continueBtn = new ButtonBuilder().WithLabel("Continue to Form").WithCustomId("support_continue").WithStyle(ButtonStyle.Primary);

        ComponentBuilder componentBuilder = new ComponentBuilder()
            .WithSelectMenu(issueMenu, row: 0)
            .WithSelectMenu(reproMenu, row: 1)
            .WithSelectMenu(urgencyMenu, row: 2);

        if (showPlatform && platformMenu != null)
            componentBuilder.WithSelectMenu(platformMenu, row: 3);

        componentBuilder.WithButton(continueBtn, row: showPlatform ? 4 : 3);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Content = $"Selected: **{appName}**\nNow fill out the remaining fields.";
            x.Components = componentBuilder.Build();
        });
    }

    // Generic handler for the other selects
    [ComponentInteraction("support_issue_type|support_repro|support_urgency|support_platform", TreatAsRegex = true)]
    public async Task HandleSelect(string[] values)
    {
        await DeferAsync(ephemeral: true);
        if (values.Length == 0)
            return;

        UserSupportFormState state = stateService.GetOrCreate(Context.User.Id);

        // Use the interaction's custom id from the component interaction context
        string? customId = (Context.Interaction as SocketMessageComponent)?.Data.CustomId;
        switch (customId)
        {
            case "support_issue_type": state.IssueType = values[0]; break;
            case "support_repro": state.Reproducibility = values[0]; break;
            case "support_urgency": state.Urgency = values[0]; break;
            case "support_platform": state.Platform = values[0]; break;
        }
    }

    // Continue button → show modal
    [ComponentInteraction("support_continue")]
    public async Task ContinueToForm()
    {
        if (!stateService.TryGet(Context.User.Id, out UserSupportFormState? state) || string.IsNullOrEmpty(state?.SelectedApp))
        {
            await RespondAsync("Please select an application first.", ephemeral: true);
            return;
        }

        string appName = GetAppDisplayName(state.SelectedApp);
        string versionLabel = GetVersionLabel(state.SelectedApp);

        ModalBuilder modal = new ModalBuilder()
            .WithTitle(appName)
            .WithCustomId("support_modal_full")
            .AddTextInput("Describe the Issue", "description", TextInputStyle.Paragraph, required: true)
            .AddTextInput("Operating System", "os", TextInputStyle.Short, required: true)
            .AddTextInput(versionLabel, "version", TextInputStyle.Short, required: true)
            .AddTextInput("Steps to Reproduce", "steps", TextInputStyle.Paragraph, required: false)
            .AddTextInput("Additional Info / Logs", "additional", TextInputStyle.Paragraph, required: false);

        await Context.Interaction.RespondWithModalAsync(modal.Build());
    }

    // Modal handler - final step, sends embed + cleans up
    [ModalInteraction("support_modal_full")]
    public async Task ModalSubmitted(SupportModalModel modal)
    {
        await DeferAsync(ephemeral: true);

        if (!stateService.TryGet(Context.User.Id, out UserSupportFormState? state) || string.IsNullOrEmpty(state?.SelectedApp))
        {
            await FollowupAsync("Session expired or invalid. Please start over with /support", ephemeral: true);
            return;
        }

        string appName = GetAppDisplayName(state.SelectedApp);

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"🧾 {appName} — Support Request")
            .WithColor(new Color(3, 169, 252))
            .WithAuthor(Context.User.GlobalName ?? Context.User.Username, Context.User.GetDisplayAvatarUrl())
            .WithCurrentTimestamp()
            .AddField("Application", appName, true)
            .AddField("Issue Type", state.IssueType ?? "Not specified", true)
            .AddField("Reproducibility", state.Reproducibility ?? "Not specified", true)
            .AddField("Urgency", state.Urgency ?? "Not specified", true);

        // Only add Platform field if it was actually selected
        if (!string.IsNullOrEmpty(state.Platform))
            embed.AddField("Platform", state.Platform, true);

        embed
            .AddField("Operating System", modal.OS, true)
            .AddField("Version", modal.Version, true)
            .AddField("Description", modal.Description)
            .AddField("Steps", string.IsNullOrEmpty(modal.Steps) ? "Not provided" : modal.Steps)
            .AddField("Additional Info", string.IsNullOrEmpty(modal.Additional) ? "None" : modal.Additional)
            .AddField("Status", "🟢 Open", true);

        // Final cleanup - remove all components from original message
        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = "✅ Support request submitted! You can close this.";
            msg.Embed = embed.Build();
            msg.Components = new ComponentBuilder().Build();  // ← removes ALL dropdowns/buttons
            msg.Flags = MessageFlags.Ephemeral;
        });

        // Create a private ticket channel under support category.
        ITextChannel? ticketChannel = null;
        SocketTextChannel? supportChannel = Context.Guild.TextChannels
            .FirstOrDefault(channel => channel.Name.Equals("support", StringComparison.OrdinalIgnoreCase));

        SocketCategoryChannel? targetCategory = Context.Guild.CategoryChannels
            .FirstOrDefault(category => category.Name.Equals("Support🆘", StringComparison.OrdinalIgnoreCase));

        targetCategory ??= supportChannel != null
            ? Context.Guild.GetCategoryChannel(supportChannel.CategoryId.GetValueOrDefault())
            : null;

        if (targetCategory != null)
        {
            string baseName = "ticket-";
            HashSet<string> existingNames = Context.Guild.TextChannels
                .Select(channel => channel.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int nextNumber = Context.Guild.TextChannels
                .Where(channel => channel.Name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                .Select(channel => channel.Name[baseName.Length..])
                .Select(value => int.TryParse(value, out int parsed) ? parsed : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;

            string ticketName = $"{baseName}{nextNumber}";
            while (existingNames.Contains(ticketName))
            {
                nextNumber++;
                ticketName = $"{baseName}{nextNumber}";
            }

            RestTextChannel created = await Context.Guild.CreateTextChannelAsync(ticketName, props =>
            {
                props.CategoryId = targetCategory.Id;
                props.Topic = $"[OPEN] Support ticket for {Context.User.Username} ({Context.User.Id})";
            });

            SocketTextChannel? createdSocket = Context.Guild.GetTextChannel(created.Id);
            if (createdSocket != null)
            {
                // Keep channel private by default.
                await createdSocket.AddPermissionOverwriteAsync(
                    Context.Guild.EveryoneRole,
                    new OverwritePermissions(viewChannel: PermValue.Deny));

                IRole? supportRole = Context.Guild.Roles
                    .FirstOrDefault(role => role.Name.Equals("support", StringComparison.OrdinalIgnoreCase));

                if (supportRole != null)
                {
                    await createdSocket.AddPermissionOverwriteAsync(
                        supportRole,
                        new OverwritePermissions(
                            viewChannel: PermValue.Allow,
                            sendMessages: PermValue.Allow,
                            readMessageHistory: PermValue.Allow,
                            manageChannel: PermValue.Allow,
                            manageMessages: PermValue.Allow,
                            attachFiles: PermValue.Allow,
                            embedLinks: PermValue.Allow,
                            addReactions: PermValue.Allow,
                            createPublicThreads: PermValue.Allow,
                            createPrivateThreads: PermValue.Allow,
                            sendMessagesInThreads: PermValue.Allow,
                            sendTTSMessages: PermValue.Allow,
                            sendVoiceMessages: PermValue.Allow
                            ));
                }

                await createdSocket.AddPermissionOverwriteAsync(
                    (IGuildUser)Context.User,
                    new OverwritePermissions(
                        viewChannel: PermValue.Allow,
                        sendMessages: PermValue.Allow,
                        readMessageHistory: PermValue.Allow,
                        attachFiles: PermValue.Allow,
                        embedLinks: PermValue.Allow,
                        addReactions: PermValue.Allow,
                        sendMessagesInThreads: PermValue.Allow,
                        sendTTSMessages: PermValue.Allow,
                        sendVoiceMessages: PermValue.Allow
                        ));

                var closeButton = new ButtonBuilder()
                    .WithLabel("🔒 Close Ticket")
                    .WithCustomId("ticket_close")
                    .WithStyle(ButtonStyle.Danger);

                var component = new ComponentBuilder()
                    .WithButton(closeButton)
                    .Build();

                var ticketMessage = await createdSocket.SendMessageAsync(
                    $"{supportRole?.Mention}\n\n{Context.User.Mention}, your support ticket has been created.",
                    embed: embed.Build(),
                    components: component
                );

                await ticketMessage.PinAsync(); // Pin the status message

                ticketChannel = createdSocket;
            }
        }

        if (ticketChannel == null)
        {
            await FollowupAsync("⚠️ Support ticket could not be created. Please contact Support.", ephemeral: true);
        }
        else
        {
            await FollowupAsync($"✅ Support ticket created: {ticketChannel.Mention}", ephemeral: true);
        }

        // Clear state after success
        stateService.Clear(Context.User.Id);

        await LogCommandAsync(
            [
                ("Application",      state.SelectedApp == "cs2aa" ? "CS2 AutoAccept" : "Stream Drop Collector"),
                ("Issue Type",       state.IssueType    ?? "Not specified"),
                ("Reproducibility",  state.Reproducibility ?? "Not specified"),
                ("Urgency",          state.Urgency      ?? "Not specified"),
                ("Platform",         state.Platform     ?? "Not specified"),
                ("Operating System", modal.OS                  ?? "Unknown"),
                ("Version",          modal.Version                ?? "Not specified"),
                ("Description",      modal.Description            ?? "No description provided"),
                ("Steps",            string.IsNullOrWhiteSpace(modal.Steps) ? "Not provided" : modal.Steps.Trim()),
                ("Additional Info",  string.IsNullOrWhiteSpace(modal.Additional) ? "None" : modal.Additional.Trim())
            ]
        );
    }

    [ComponentInteraction("ticket_close")]
    public async Task CloseTicket()
    {
        if (Context.Channel is not SocketTextChannel channel)
            return;

        var user = (SocketGuildUser)Context.User;

        bool isSupport = user.Roles.Any(r =>
            r.Name.Equals("support", StringComparison.OrdinalIgnoreCase));

        bool isOwner = channel.Topic?.Contains(user.Id.ToString()) == true;

        if (!isSupport && !isOwner)
        {
            await RespondAsync("❌ You are not allowed to close this ticket.", ephemeral: true);
            return;
        }

        await DeferAsync();

        // Rename channel → closed
        if (!channel.Name.StartsWith("closed-"))
            await channel.ModifyAsync(x => x.Name = $"closed-{channel.Name}");

        // Update topic
        string? topic = channel.Topic;

        if (!string.IsNullOrEmpty(topic))
        {
            topic = topic.Replace("[OPEN]", "[CLOSED]");
            await channel.ModifyAsync(x => x.Topic = topic);
        }

        // Lock channel
        await channel.AddPermissionOverwriteAsync(
            Context.Guild.EveryoneRole,
            new OverwritePermissions(sendMessages: PermValue.Deny)
        );

        // Reopen button
        var reopenButton = new ButtonBuilder()
            .WithLabel("🔓 Reopen Ticket")
            .WithCustomId("ticket_reopen")
            .WithStyle(ButtonStyle.Success);

        await channel.SendMessageAsync(
            $"🔒 Ticket closed by {Context.User.Mention}",
            components: new ComponentBuilder().WithButton(reopenButton).Build()
        );

        // Send closed embed
        var closedEmbed = new EmbedBuilder()
            .WithTitle("🔒 Ticket Closed")
            .WithColor(Color.Red)
            .WithDescription($"Closed by {Context.User.Mention}")
            .WithCurrentTimestamp();

        await channel.SendMessageAsync(embed: closedEmbed.Build());
    }

    [ComponentInteraction("ticket_reopen")]
    public async Task ReopenTicket()
    {
        if (Context.Channel is not SocketTextChannel channel)
            return;

        var user = (SocketGuildUser)Context.User;

        bool isSupport = user.Roles.Any(r =>
            r.Name.Equals("support", StringComparison.OrdinalIgnoreCase));

        if (!isSupport)
        {
            await RespondAsync("❌ Only support can reopen tickets.", ephemeral: true);
            return;
        }

        await DeferAsync();

        // Rename back
        if (channel.Name.StartsWith("closed-"))
            await channel.ModifyAsync(x => x.Name = channel.Name.Replace("closed-", ""));

        // Update topic
        string? topic = channel.Topic;

        if (!string.IsNullOrEmpty(topic))
        {
            topic = topic.Replace("[CLOSED]", "[OPEN]");
            await channel.ModifyAsync(x => x.Topic = topic);
        }

        // Unlock channel
        await channel.AddPermissionOverwriteAsync(
            Context.Guild.EveryoneRole,
            new OverwritePermissions(sendMessages: PermValue.Allow)
        );

        // Add close button again
        var closeButton = new ButtonBuilder()
            .WithLabel("🔒 Close Ticket")
            .WithCustomId("ticket_close")
            .WithStyle(ButtonStyle.Danger);

        await channel.SendMessageAsync(
            "🔓 Ticket reopened.",
            components: new ComponentBuilder().WithButton(closeButton).Build()
        );
    }

    private static string GetAppDisplayName(string key) => key switch
    {
        "cs2aa" => "CS2 AutoAccept",
        "sdc" => "Stream Drop Collector",
        "crosshairy" => "CrosshairY",
        "rustypainter" => "Rusty Painter",
        "coderaider" => "CodeRaider",
        _ => "Unknown Application"
    };

    private static string GetVersionLabel(string key) => key switch
    {
        "cs2aa" => "CS2-AutoAccept Version",
        "sdc" => "Stream Drop Collector Version",
        "crosshairy" => "CrosshairY Version",
        "rustypainter" => "Rusty Painter Version",
        "coderaider" => "CodeRaider Version",
        _ => "Application Version"
    };
}