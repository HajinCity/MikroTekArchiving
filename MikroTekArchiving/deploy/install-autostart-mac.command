#!/bin/bash
# ---------------------------------------------------------------------------------------------
#  OPTIONAL: start the portal by itself whenever this Mac's user logs in, and restart it if it
#  ever stops. Test with start-server-mac.command first, then close that window and run this.
#  Also turn on Docker Desktop > Settings > General > "Start Docker Desktop when you sign in".
#  To undo: uninstall-autostart-mac.command
# ---------------------------------------------------------------------------------------------
cd "$(dirname "$0")" || exit 1
APP_DIR="$(pwd)"
LABEL="ph.lnhs.mikrotek-archiving"
PLIST="$HOME/Library/LaunchAgents/$LABEL.plist"
LOG="$HOME/Library/Logs/MikroTek Archiving.log"

case "$APP_DIR" in
  "$HOME/Desktop"*|"$HOME/Documents"*|"$HOME/Downloads"*)
    echo "!! The portal is inside Desktop, Documents or Downloads, where macOS stops programs that start at log-in."
    echo "   Move the folder to your home folder (for example ~/MikroTek), then run this again."
    printf '\nPress Return to close this window.'; read -r _; exit 1 ;;
esac
[ -f "$HOME/Library/Application Support/MikroTek Archiving/appsettings.macOS.json" ] || {
  echo "!! Run setup-mac.command first."; printf '\nPress Return to close this window.'; read -r _; exit 1; }

mkdir -p "$HOME/Library/LaunchAgents" "$HOME/Library/Logs"
cat > "$PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>$LABEL</string>
  <key>ProgramArguments</key>
  <array><string>/bin/bash</string><string>$APP_DIR/start-server-mac.command</string></array>
  <key>WorkingDirectory</key><string>$APP_DIR</string>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>ThrottleInterval</key><integer>30</integer>
  <key>StandardOutPath</key><string>$LOG</string>
  <key>StandardErrorPath</key><string>$LOG</string>
</dict>
</plist>
EOF
launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null
launchctl bootstrap "gui/$(id -u)" "$PLIST" && echo "Done. The portal now starts at log-in. Its log: $LOG" \
  || echo "!! Could not register the start-up item. Try again, or start the portal with start-server-mac.command."
echo "Check it in a minute at http://localhost:5080/health"
printf '\nPress Return to close this window.'
read -r _
