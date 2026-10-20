#!/bin/bash
# Stops the portal from starting by itself at log-in (undoes install-autostart-mac.command).
# The database and the archived files are not touched.
LABEL="ph.lnhs.mikrotek-archiving"
launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null
rm -f "$HOME/Library/LaunchAgents/$LABEL.plist"
echo "The portal no longer starts at log-in. Use start-server-mac.command to start it by hand."
printf '\nPress Return to close this window.'
read -r _
