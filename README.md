# tsgsBot

**tsgsBot** is a versatile, multi-purpose Discord bot built for the tsgsOFFICIAL community, and made open-source as inspiration for others.

It offers fun utilities, moderation tools, giveaways, polls, CS2 stats, custom software support, games, and more - all while running reliably on a Raspberry Pi for maximum uptime, and minimal cost.

[![GitHub stars](https://img.shields.io/github/stars/tsgsOFFICIAL/tsgsBot-CSharp?style=social)](https://github.com/tsgsOFFICIAL/tsgsBot-CSharp)
[![Discord](https://img.shields.io/discord/227048721710317569?color=5865F2&label=Discord&logo=discord&logoColor=white)](https://discord.gg/Cddu5aJ)
[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Discord.Net](https://img.shields.io/badge/Discord.Net-3.x-blue?logo=discord)](https://github.com/discord-net/Discord.Net)
[![Support on Ko-fi](https://img.shields.io/badge/Support%20me%20on%20Ko--fi-F16061?logo=ko-fi&logoColor=white)](https://ko-fi.com/tsgsOFFICIAL)

## Features

- **Slash commands** for clean, modern interaction
- **Persistent giveaways & polls** - stored in a database (survive restarts)
- Most non-user-specific commands work in **DMs** and **servers**
- Moderation tools (mute, purge, role management, nick changes…)
- Fun & utility commands (memes, random number, Tic-Tac-Toe, reminders…)
- CS2 stats lookup, random memes from Reddit or #liams-memes
- Support & verification for tsgsOFFICIAL software/products
- Easy status & presence management (admin only)
- Runs as a **systemd service** on Raspberry Pi for high uptime

## Tech Stack

- Language: **C#**
- Framework: **.NET 10**
- Discord library: **[Discord.Net](https://github.com/discord-net/Discord.Net)**
- Database: Used for persistent giveaways & polls (implementation not exposed in commands)
- Hosting: Raspberry Pi (running as systemd service named `tsgsbot`)

## Commands

All commands are **slash commands** (`/`) unless noted. Scope shows where the command appears; `UseApplicationCommands` means everyone can run it unless server permissions override. Guild-only commands do not show in DMs.

### Public slash commands

| Command                               | Description                               | Scope      | Default permission     |
| ------------------------------------- | ----------------------------------------- | ---------- | ---------------------- |
| `/ping`                               | Check bot latency                         | Guild & DM | UseApplicationCommands |
| `/uptime`                             | Show bot runtime                          | Guild & DM | UseApplicationCommands |
| `/help`                               | List commands you can use                 | Guild      | UseApplicationCommands |
| `/invite`                             | Get bot invite link                       | Guild & DM | UseApplicationCommands |
| `/serverinfo`                         | Show server information                   | Guild      | UseApplicationCommands |
| `/userinfo [user]`                    | Display user details                      | Guild      | UseApplicationCommands |
| `/randomnumber [min] [max]`           | Generate a random number                  | Guild & DM | UseApplicationCommands |
| `/remind [task] [duration]`           | Create a reminder                         | Guild & DM | UseApplicationCommands |
| `/myreminders`                        | List your reminders                       | Guild & DM | UseApplicationCommands |
| `/report [user] [reason]`             | Report a user to moderators               | Guild      | UseApplicationCommands |
| `/mynick [nickname]`                  | Change your own nickname                  | Guild      | ChangeNickname         |
| `/tictactoe [opponent]`               | Challenge someone to Tic-Tac-Toe          | Guild      | UseApplicationCommands |
| `/meme [subreddit]`                   | Random meme from Reddit                   | Guild & DM | UseApplicationCommands |
| `/liam`                               | Random meme from #liams-memes             | Guild      | UseApplicationCommands |
| `/csstats [identifier]`               | CS2 player stats lookup                   | Guild & DM | UseApplicationCommands |
| `/autoaccept`                         | Directions for AutoAccept (CS2)           | Guild & DM | UseApplicationCommands |
| `/streamdropcollector` (alias `/sdc`) | Install guide for StreamDropCollector     | Guild      | UseApplicationCommands |
| `/software`                           | Links to tsgsOFFICIAL software downloads  | Guild & DM | UseApplicationCommands |
| `/support`                            | Submit a support request for applications | Guild      | UseApplicationCommands |
| `/verify [code]`                      | Verify a donation to get supporter role   | Guild      | UseApplicationCommands |

### Moderation & management slash commands

| Command                                 | Description                                   | Scope | Default permission |
| --------------------------------------- | --------------------------------------------- | ----- | ------------------ |
| `/mute [target] [duration] [reason]`    | Mute a member via modal flow                  | Guild | MuteMembers        |
| `/unmute [target]`                      | Remove the muted role                         | Guild | MuteMembers        |
| `/purge [amount] [user] [all-channels]` | Delete messages or nuke channel               | Guild | ManageChannels     |
| `/nick [user] [nickname]`               | Change a user's nickname                      | Guild | ManageNicknames    |
| `/role-add [user] [role]`               | Add a role to a user                          | Guild | ManageRoles        |
| `/role-remove [user] [role]`            | Remove a role from a user                     | Guild | ManageRoles        |
| `/role-panel`                           | Create an embed with self-assign role buttons | Guild | ManageRoles        |
| `/say [message] [channel]`              | Make the bot speak in a channel               | Guild | ManageMessages     |
| `/status [type] [message]`              | Temporarily change the bot's status           | Guild | Administrator      |
| `/dm [user] [message]`                  | Send a DM to a user via the bot               | Guild | Administrator      |

### Giveaways & polls slash commands

| Command     | Description                     | Scope | Default permission |
| ----------- | ------------------------------- | ----- | ------------------ |
| `/giveaway` | Start a reaction-based giveaway | Guild | CreateEvents       |
| `/poll`     | Create a reaction-based poll    | Guild | SendPolls          |

### Context menu commands (right-click)

| Command                 | Type    | Scope | Default permission     |
| ----------------------- | ------- | ----- | ---------------------- |
| `Mute User`             | User    | Guild | MuteMembers            |
| `Mute Message Author`   | Message | Guild | MuteMembers            |
| `Unmute User`           | User    | Guild | MuteMembers            |
| `Unmute Message Author` | Message | Guild | MuteMembers            |
| `Report Message`        | Message | Guild | UseApplicationCommands |
| `Edit Role Panel`       | Message | Guild | ManageRoles            |

## Self-Hosting / Running tsgsBot on Raspberry Pi

This section documents the exact steps used to deploy tsgsBot on Raspberry Pi OS as user tsgsofficial — including .NET 10 installation, private repo cloning with deploy key, custom update script, and systemd service.

### 1. Shell Improvements & Aliases

```bash
nano ~/.bashrc
```

> (added the lines below)

```bash
source ~/.bashrc
```

Aliases:

```bash
alias please="sudo"
alias cls="clear"
alias clock="date '+%A %W %Y %X'"

alias updatePi="sudo apt update && sudo apt upgrade -y && sudo apt autoremove -y"

# Bot aliases
alias bot-status='sudo systemctl status tsgsbot'
alias bot-logs='journalctl -u tsgsbot -f'
alias bot-restart='sudo systemctl restart tsgsbot'
alias bot-start='sudo systemctl start tsgsbot'
alias bot-stop='sudo systemctl stop tsgsbot'
alias bot-update='~/update-bot.sh'
alias bot-rebuild='~/update-bot.sh && bot-restart'
```

### 2. System Update & Prerequisites

```bash
updatePi
sudo apt install curl git -y
sudo reboot
```

> (After reboot)

```bash
updatePi
```

### 3. Install .NET 10 SDK

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc
```

Verify:

```bash
dotnet --version
dotnet --list-sdks
```

### 4. Create Directories

```bash
mkdir -p ~/tsgsbot/publish
mkdir -p ~/tsgsbot/gitClone
```

### 5. Set Up SSH Deploy Key for Private Repo

```bash
ssh-keygen -t ed25519 -C "pi-bot-deploy" -f ~/.ssh/id_bot_deploy
```

> (leave passphrase empty)

```bash
cat ~/.ssh/id_bot_deploy.pub
```

> → add this public key as a read-only deploy key in your GitHub repo settings (only applicable if you intend on keeping the bot repo private on GitHub)

Test:

```bash
ssh -T -i ~/.ssh/id_bot_deploy git@github.com
```

> This should say something like: "_Hi tsgsOFFICIAL/tsgsBot-CSharp! You've successfully authenticated, but GitHub does not provide shell access._"

Secure keys:

```bash
chmod 600 ~/.ssh/id_bot_deploy
chmod 600 ~/.ssh/id_bot_deploy.pub
```

### 6. Clone the Private Repository

```bash
cd ~/tsgsbot/gitClone
git clone --config core.sshCommand="ssh -i ~/.ssh/id_bot_deploy" git@github.com:tsgsOFFICIAL/tsgsBot-CSharp.git .
```

### 7. Create the Update Script

```bash
nano ~/update-bot.sh
chmod +x ~/update-bot.sh
```

Contents:

```bash
#!/bin/bash

# Config
REPO_DIR="$HOME/tsgsBot/gitClone"
PUBLISH_DIR="$HOME/tsgsBot/publish"
SERVICE_NAME="tsgsbot.service"
BRANCH="master"

echo "Starting bot update at $(date)"

cd "$REPO_DIR" || { echo "Repo dir not found!"; exit 1; }

# Pull latest
git fetch origin
git reset --hard origin/"$BRANCH"

# Restore & publish
dotnet restore
dotnet publish -c Release \
    -r linux-arm64 \
    --self-contained true \
    -o "$PUBLISH_DIR"

# Make executable
chmod +x "$PUBLISH_DIR/tsgsBot-CSharp"

echo "Publish done. Restarting service..."
sudo systemctl restart "$SERVICE_NAME"

echo "Update complete at $(date)"
```

### 8. Create systemd Service

```bash
sudo nano /etc/systemd/system/tsgsbot.service
```

Contents:

```bash
[Unit]
Description=tsgsBot Discord.Net
After=network-online.target
Wants=network-online.target

[Service]
User=tsgsofficial
WorkingDirectory=/home/tsgsofficial/tsgsBot/publish
ExecStart=/home/tsgsofficial/tsgsBot/publish/tsgsBot-CSharp
Restart=always
RestartSec=10
KillSignal=SIGINT
Environment="DISCORD_TOKEN=xxx"
Environment="DB_CONNECTION_STRING=xxx"
Environment="ENVIRONMENT=Production"
Environment="STEAM_API_KEY=xxx"
Environment="STEAM_WEB_API_KEY=xxx"

[Install]
WantedBy=multi-user.target
```

After saving:

```bash
sudo systemctl daemon-reload
sudo systemctl enable tsgsbot
sudo systemctl start tsgsbot
```

### Quick Commands (using aliases)

`bot-update` → git pull + publish + restart
`bot-logs` → live logs
`bot-restart` → restart service
`bot-status` → check running status
`updatePi` → full system update

Replace the xxx values in the service file with your real secrets before starting.

## Roadmap

This is where I track planned features, improvements, and experiments for tsgsBot. Feel free to open issues/discussions if something here excites you or if you'd like to help!

### High Priority

- [x] Refactor reminder system to use a persistent state, from the database.
- [x] Refactor reminder system to use a proper scheduler instead of in-memory timers.
- [x] Refactor giveaways system to use a proper scheduler instead of in-memory timers.
- [x] Refactor poll system to use a proper scheduler instead of in-memory timers.

### Nice-to-Have

### Ideas / Experiments

Made with ❤️ by tsgsOFFICIAL
