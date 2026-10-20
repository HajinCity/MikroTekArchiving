#!/bin/bash
# ---------------------------------------------------------------------------------------------
#  MikroTek Archiving Portal - first-time setup on a Mac (MacBook M1/M2/M3, also works on Intel)
#
#  First time: open Terminal, type   bash   and a space, drag this file into the window, press Return.
#  After that you can simply double-click it. Safe to run again: it keeps the database and password.
#
#  What it does:
#    1. unblocks this folder (files copied from another computer are blocked by macOS)
#    2. installs the scan reader (Tesseract OCR) with Homebrew
#    3. starts Docker Desktop and creates the SQL Server database container "mikrotek-sql"
#    4. writes this Mac's settings to ~/Library/Application Support/MikroTek Archiving/appsettings.macOS.json
# ---------------------------------------------------------------------------------------------
cd "$(dirname "$0")" || exit 1
APP_DIR="$(pwd)"
SETTINGS_DIR="$HOME/Library/Application Support/MikroTek Archiving"
SETTINGS="$SETTINGS_DIR/appsettings.macOS.json"
CONTAINER="mikrotek-sql"
PORT=5080            # 5000 is used by AirPlay Receiver on a Mac

step() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
fail() { printf '\n\033[1;31m!! %s\033[0m\n\nPress Return to close this window.' "$1"; read -r _; exit 1; }

step "1/5  Checking this Mac"
echo "Portal folder: $APP_DIR"
[ -f "$APP_DIR/MikroTek.Archiving" ] || fail "MikroTek.Archiving was not found in this folder. Put setup-mac.command in the published MacServer folder (see DEPLOY-MAC.md)."
case "$APP_DIR" in
  "$HOME/Desktop"*|"$HOME/Documents"*|"$HOME/Downloads"*)
    echo "Note: macOS protects Desktop, Documents and Downloads. Keep the portal in your home folder (for example ~/MikroTek) so it can start by itself at log-in." ;;
esac
# Files copied from a download, AirDrop or another computer are marked as "from the internet"; remove that mark.
xattr -dr com.apple.quarantine "$APP_DIR" 2>/dev/null
chmod +x "$APP_DIR/MikroTek.Archiving" "$APP_DIR"/*.command 2>/dev/null
# Apple silicon only runs signed programs: give the portal a local (ad-hoc) signature.
codesign --force --sign - "$APP_DIR/MikroTek.Archiving" >/dev/null 2>&1
if [ "$(uname -m)" = "arm64" ]; then
  echo "Apple silicon Mac (M1/M2/M3)."
  # SQL Server runs through Rosetta 2 on Apple silicon.
  if ! /usr/bin/arch -x86_64 /usr/bin/true 2>/dev/null; then
    echo "Installing Rosetta 2 (Apple's translator, needed by SQL Server)..."
    /usr/sbin/softwareupdate --install-rosetta --agree-to-license || fail "Rosetta 2 could not be installed. Run: softwareupdate --install-rosetta"
  fi
else
  echo "Intel Mac."
fi

step "2/5  Scan reader (Tesseract OCR)"
BREW=""
for b in /opt/homebrew/bin/brew /usr/local/bin/brew; do [ -x "$b" ] && BREW="$b" && break; done
[ -n "$BREW" ] || fail "Homebrew is not installed. Go to https://brew.sh, copy the one line shown there into Terminal and press Return. When it finishes, run this setup again."
if "$BREW" list --formula tesseract >/dev/null 2>&1; then
  echo "Tesseract is already installed."
else
  echo "Installing Tesseract (a few minutes)..."
  "$BREW" install tesseract || fail "Tesseract could not be installed. Run: brew install tesseract"
fi
"$("$BREW" --prefix)/bin/tesseract" --version 2>/dev/null | head -1

step "3/5  Docker Desktop (it runs the SQL Server database)"
DOCKER="$(command -v docker 2>/dev/null)"
[ -z "$DOCKER" ] && [ -x /usr/local/bin/docker ] && DOCKER=/usr/local/bin/docker
[ -z "$DOCKER" ] && [ -x /Applications/Docker.app/Contents/Resources/bin/docker ] && DOCKER=/Applications/Docker.app/Contents/Resources/bin/docker
[ -n "$DOCKER" ] || fail "Docker Desktop is not installed. Download \"Docker Desktop for Mac - Apple Silicon\" from docker.com, open it once and accept its terms, then run this setup again."
if ! "$DOCKER" info >/dev/null 2>&1; then
  echo "Starting Docker Desktop..."
  open -a Docker
  for _ in $(seq 1 60); do "$DOCKER" info >/dev/null 2>&1 && break; sleep 3; done
  "$DOCKER" info >/dev/null 2>&1 || fail "Docker Desktop did not start. Open Docker from Applications, wait until it says \"Engine running\", then run this setup again."
fi
echo "Docker is running."

step "4/5  SQL Server database"
mkdir -p "$SETTINGS_DIR"
PASS=""
[ -f "$SETTINGS" ] && PASS="$(sed -n 's/.*Password=\([^;"]*\).*/\1/p' "$SETTINGS" | head -1)"
if "$DOCKER" container inspect "$CONTAINER" >/dev/null 2>&1; then
  [ -n "$PASS" ] || fail "The database container \"$CONTAINER\" exists, but its password file is missing ($SETTINGS). Restore that file from a backup, or see \"Starting over\" in DEPLOY-MAC.md."
  echo "The database container already exists; starting it."
  "$DOCKER" start "$CONTAINER" >/dev/null
else
  [ -n "$PASS" ] || PASS="Lnhs-$(openssl rand -hex 12)-Db1"
  echo "Downloading and creating SQL Server 2022 Express (the first time takes several minutes)..."
  "$DOCKER" run -d --name "$CONTAINER" --platform linux/amd64 --restart unless-stopped \
    -e ACCEPT_EULA=Y -e MSSQL_PID=Express -e "MSSQL_SA_PASSWORD=$PASS" \
    -p 127.0.0.1:1433:1433 -v mikrotek-sql-data:/var/opt/mssql \
    mcr.microsoft.com/mssql/server:2022-latest >/dev/null || fail "The SQL Server container could not be created. Check your internet connection and run this setup again."
fi
printf 'Waiting for SQL Server to be ready'
READY=""
for _ in $(seq 1 80); do
  if "$DOCKER" exec "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$PASS" -Q "SELECT 1" >/dev/null 2>&1; then READY=1; break; fi
  printf '.'; sleep 3
done
echo
[ -n "$READY" ] || fail "SQL Server did not start. In Docker Desktop open Settings > General, turn ON \"Use Rosetta for x86_64/amd64 emulation on Apple Silicon\", click Apply & restart, then run this setup again."
echo "SQL Server is ready (only this Mac can reach it; it is not open to the network)."

step "5/5  Saving this Mac's settings"
umask 077
cat > "$SETTINGS" <<EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1,1433;Database=MikroTekArchivingDb;User Id=sa;Password=$PASS;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Kestrel": {
    "Endpoints": {
      "Lan": { "Url": "http://0.0.0.0:$PORT" }
    }
  },
  "Ocr": {
    "Mode": "Program"
  }
}
EOF
chmod 600 "$SETTINGS"
echo "Saved: $SETTINGS"

printf '\n\033[1;32mSetup finished.\033[0m\n'
echo "Next: double-click start-server-mac.command (keep its window open)."
echo "Then open http://localhost:$PORT/Portal and sign in as admin / admin123 (you will be asked to change it)."
printf '\nPress Return to close this window.'
read -r _
