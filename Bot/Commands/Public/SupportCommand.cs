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
            .AddField("Additional Info", string.IsNullOrEmpty(modal.Additional) ? "None" : modal.Additional);

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
                props.Topic = $"Support ticket for {Context.User.Username} ({Context.User.Id})";
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

                var initialMessage = await createdSocket.SendMessageAsync(
                    $"{supportRole?.Mention}\n\n{Context.User.Mention}, your support ticket has been created.\n\n" +
                    "**Ticket Status:** 🟢 **Open**",
                    embed: embed.Build(),
                    components: new ComponentBuilder()
                        .WithButton("🔒 Close Ticket", "ticket_close", ButtonStyle.Danger)
                        .Build());

                await initialMessage.PinAsync(); // Pin the status message

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

public sealed class TicketCommands : InteractionModuleBase<SocketInteractionContext>
{
    [ComponentInteraction("ticket_close")]
    public async Task CloseTicket()
    {
        Console.WriteLine($"[TICKET DEBUG] Button clicked by {Context.User.Username} ({Context.User.Id}) in #{Context.Channel.Name}");

        if (Context.Channel is not SocketTextChannel channel)
        {
            Console.WriteLine("[TICKET DEBUG] Not a text channel.");
            await RespondAsync("❌ This button only works inside ticket channels.", ephemeral: true);
            return;
        }

        bool isTicketOwner = channel.Topic?.Contains(Context.User.Id.ToString()) == true;

        var supportRole = Context.Guild.Roles.FirstOrDefault(r =>
            r.Name.Equals("support", StringComparison.OrdinalIgnoreCase));

        bool isSupport = supportRole != null &&
                         Context.User is SocketGuildUser guildUser &&
                         guildUser.Roles.Any(role => role.Id == supportRole.Id);

        Console.WriteLine($"[TICKET DEBUG] IsOwner: {isTicketOwner} | IsSupport: {isSupport} | SupportRole: {supportRole?.Name ?? "null"}");

        if (!isTicketOwner && !isSupport)
        {
            await RespondAsync("❌ Only the ticket creator or support staff can close this ticket.", ephemeral: true);
            return;
        }

        Console.WriteLine("[TICKET DEBUG] Permissions OK → Sending modal");

        var modal = new ModalBuilder()
            .WithTitle("Close Ticket")
            .WithCustomId("ticket_close_confirm")
            .AddTextInput("Reason (optional)", "close_reason", TextInputStyle.Paragraph,
                required: false,
                placeholder: "Issue resolved, duplicate, user left, etc.");

        await Context.Interaction.RespondWithModalAsync(modal.Build());
        Console.WriteLine("[TICKET DEBUG] Modal sent successfully.");
    }

    // ─────────────────────────────────────────────────────────────
    // MODAL HANDLER
    // ─────────────────────────────────────────────────────────────
    [ModalInteraction("ticket_close_confirm")]
    public async Task CloseTicketConfirm(string? close_reason)
    {
        Console.WriteLine($"[TICKET DEBUG] === MODAL SUBMITTED === by {Context.User.Username} ({Context.User.Id})");
        Console.WriteLine($"[TICKET DEBUG] Submitted reason: '{close_reason ?? "null"}'");

        if (Context.Channel is not SocketTextChannel channel)
        {
            Console.WriteLine("[TICKET DEBUG] Modal: Channel is not SocketTextChannel!");
            return;
        }

        Console.WriteLine($"[TICKET DEBUG] Channel: {channel.Name} ({channel.Id})");

        string reason = string.IsNullOrWhiteSpace(close_reason) ? "No reason provided" : close_reason.Trim();

        try
        {
            var closedEmbed = new EmbedBuilder()
                .WithTitle("🔒 Ticket Closed")
                .WithColor(Color.Red)
                .WithDescription($"**Reason:** {reason}")
                .WithFooter($"Closed by {Context.User.Username} • {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC}")
                .Build();

            await channel.SendMessageAsync(embed: closedEmbed);
            Console.WriteLine("[TICKET DEBUG] Closed embed sent.");

            // Rename
            string newName = channel.Name.StartsWith("ticket-", StringComparison.OrdinalIgnoreCase)
                ? channel.Name.Replace("ticket-", "closed-")
                : $"closed-{channel.Name}";

            await channel.ModifyAsync(x => x.Name = newName);
            Console.WriteLine($"[TICKET DEBUG] Channel renamed to: {newName}");

            // Permissions
            await channel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole,
                new OverwritePermissions(sendMessages: PermValue.Deny));

            var supportRole = Context.Guild.Roles.FirstOrDefault(r =>
                r.Name.Equals("support", StringComparison.OrdinalIgnoreCase));

            if (supportRole != null)
            {
                await channel.AddPermissionOverwriteAsync(supportRole,
                    new OverwritePermissions(sendMessages: PermValue.Allow));
            }

            Console.WriteLine("[TICKET DEBUG] Permissions updated.");

            // Final success message
            await FollowupAsync("✅ Ticket has been closed and set to read-only.", ephemeral: true);
            Console.WriteLine("[TICKET DEBUG] Followup sent → Ticket close completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TICKET DEBUG] EXCEPTION in modal handler: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            await FollowupAsync("❌ An error occurred while closing the ticket.", ephemeral: true);
        }
    }
}