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

All commands are **slash commands** (`/`). Type `/` in Discord to see available commands.

Most commands require only `UseApplicationCommands` permission - some need higher perms (shown below).

### General / Utility

- `/ping` → Check bot latency
- `/uptime` → Bot runtime
- `/help` → List commands you can use
- `/invite` → Get bot invite link
- `/serverinfo` → Server information
- `/userinfo [user]` → User details
- `/randomnumber [min] [max]` → Random number generator
- `/remind [task] [duration]` → Set a reminder
- `/report [user] [reason]` → Report user to moderators

### Fun & Games

- `/tictactoe [opponent]` → Challenge to Tic-Tac-Toe
- `/meme [subreddit]` → Random meme from Reddit
- `/liam` → Random meme from sacred #liams-memes channel

### Moderation & Management

- `/mute [target] [duration] [reason]` (MuteMembers)
- `/unmute [target]` (MuteMembers)
- `/nick [user] [nickname]` (ManageNicknames)
- `/mynick [nickname]` → Change your own nickname
- `/role-add [user] [role]` (ManageRoles)
- `/role-remove [user] [role]` (ManageRoles)
- `/purge [amount] [user]` (ManageChannels) → Delete messages / nuke channel
- `/say [message] [channel]` (ManageMessages) → Make bot speak
- `/status [type] [message]` (Administrator) → Change bot status

### CS2 & Software Related

- `/csstats [identifier]` → CS:GO/CS2 player stats
- `/autoaccept` → Directions for AutoAccept (CS2 tool)
- `/streamdropcollector` → StreamDropCollector install guide
- `/sdc` → Short version of StreamDropCollector guide
- `/software` → Links to tsgsOFFICIAL software downloads
- `/support` → Submit support request for applications
- `/verify [code]` → Verify donation → get supporter role

### Giveaways & Polls

- `/giveaway` (CreateEvents) → Start reacting giveaway
- `/poll` (SendPolls) → Create reacting poll

### Admin Only

- `/dm [user] [message]` (Administrator)

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
- [ ] Refactor reminder system to use a proper scheduler instead of in-memory timers.

### Nice-to-Have

### Ideas / Experiments

Made with ❤️ by tsgsOFFICIAL
