### Bot Management Cheat Sheet (Raspberry Pi)

All commands assume you're SSH'd in as your user (`tsgsofficial`).

#### 1. Check bot status (most used)

```bash
sudo systemctl status tsgsbot
```

→ Shows if running, uptime, last log lines. Use this first when something feels off.

#### 2. View live bot logs (see what the bot is saying)

```bash
journalctl -u tsgsbot -f
```

→ `-f` = follow/live tail.  
Press `Ctrl+C` to stop watching.

#### 3. View last N lines of logs (good for quick check)

```bash
journalctl -u tsgsbot -n 50
```

→ `-n 50` = last 50 lines. Change number as needed.

#### 4. Restart the bot (after code change or crash)

```bash
sudo systemctl restart tsgsbot
```

→ Stops + starts again. Takes ~5-10 seconds.

#### 5. Stop the bot (turn off completely)

```bash
sudo systemctl stop tsgsbot
```

#### 6. Start the bot (if you stopped it)

```bash
sudo systemctl start tsgsbot
```

#### 7. Manually run the update script (pull code + rebuild + restart)

```bash
~/update-bot.sh
```

→ This fetches latest from GitHub, rebuilds, and restarts the service automatically.

#### 8. Reboot the entire Pi (tests auto-start on boot)

```bash
sudo reboot
```

→ After reboot, wait ~1 min, then check status again.

#### 9. Quick check if bot exe exists & is executable

```bash
ls -l ~/tsgsBot/publish/tsgsBot-CSharp
```

→ Should show `-rwxr-xr-x` (the `x` means executable).

#### 10. Update system packages (weekly-ish)

```bash
sudo apt update && sudo apt upgrade -y && sudo apt autoremove -y
```

That's basically the full set you'll need 95% of the time.

### Bonus: Creating bash aliases (yes, 100% allowed & recommended!)

You can make short, easy-to-remember aliases in your shell so you don't have to type full commands every time.

1. Open your bash profile:

    ```bash
    nano ~/.bashrc
    ```

2. Scroll to the bottom and add these lines:

    ```bash
    # Bot aliases
    alias bot-status='sudo systemctl status tsgsbot'
    alias bot-logs='journalctl -u tsgsbot -f'
    alias bot-restart='sudo systemctl restart tsgsbot'
    alias bot-start='sudo systemctl start tsgsbot'
    alias bot-stop='sudo systemctl stop tsgsbot'
    alias bot-update='~/update-bot.sh'
    alias bot-rebuild='~/update-bot.sh && bot-restart'
    ```

3. Save/exit (Ctrl+O → Enter → Ctrl+X)

4. Apply the changes:
    ```bash
    source ~/.bashrc
    ```
