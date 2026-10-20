#!/bin/bash
# ---------------------------------------------------------------------------------------------
#  MikroTek Archiving Portal - start the server on this Mac. Keep this window open.
#  Closing the window (or Ctrl+C) stops the portal. Run setup-mac.command once before this.
#  install-autostart-mac.command makes the Mac run this by itself at log-in instead.
# ---------------------------------------------------------------------------------------------
cd "$(dirname "$0")" || exit 1
SETTINGS="$HOME/Library/Application Support/MikroTek Archiving/appsettings.macOS.json"
CONTAINER="mikrotek-sql"
PATH="/opt/homebrew/bin:/usr/local/bin:/Applications/Docker.app/Contents/Resources/bin:$PATH"

fail() {
  echo "!! $1"
  if [ -t 0 ]; then printf '\nPress Return to close this window.'; read -r _; fi
  exit 1
}

[ -f "$SETTINGS" ] || fail "This Mac is not set up yet. Run setup-mac.command first."
PASS="$(sed -n 's/.*Password=\([^;"]*\).*/\1/p' "$SETTINGS" | head -1)"
PORT="$(sed -n 's/.*"Url": *"http:\/\/0\.0\.0\.0:\([0-9]*\)".*/\1/p' "$SETTINGS" | head -1)"
PORT="${PORT:-5080}"

# 1. Docker Desktop and the database
if ! docker info >/dev/null 2>&1; then
  echo "Starting Docker Desktop..."
  open -a Docker
  for _ in $(seq 1 80); do docker info >/dev/null 2>&1 && break; sleep 3; done
  docker info >/dev/null 2>&1 || fail "Docker Desktop did not start. Open Docker from Applications, then try again."
fi
docker start "$CONTAINER" >/dev/null 2>&1 || fail "The database container \"$CONTAINER\" is missing. Run setup-mac.command again."
printf 'Waiting for the database'
for _ in $(seq 1 80); do
  docker exec "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$PASS" -Q "SELECT 1" >/dev/null 2>&1 && break
  printf '.'; sleep 3
done
echo

# 2. The portal
IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null)"
echo "---------------------------------------------------------------"
echo " MikroTek Archiving Portal - SERVER (do not close this window)"
echo " On this Mac open:     http://localhost:$PORT/Portal"
[ -n "$IP" ] && echo " Other PCs/Macs open:  http://$IP:$PORT/Portal"
echo "---------------------------------------------------------------"
export ASPNETCORE_ENVIRONMENT=Production
# caffeinate -i: the Mac does not fall asleep while the portal is running (keep it plugged in).
exec /usr/bin/caffeinate -i ./MikroTek.Archiving
