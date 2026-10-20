#!/bin/bash
# ---------------------------------------------------------------------------------------------
#  Backs up the archive: the database AND the scanned files (App_Data), into
#  ~/MikroTek Backups/<date>. Copy that folder to a USB drive or Google Drive afterwards.
#  The portal can keep running while this runs.
# ---------------------------------------------------------------------------------------------
cd "$(dirname "$0")" || exit 1
SETTINGS="$HOME/Library/Application Support/MikroTek Archiving/appsettings.macOS.json"
CONTAINER="mikrotek-sql"
PATH="/opt/homebrew/bin:/usr/local/bin:/Applications/Docker.app/Contents/Resources/bin:$PATH"
fail() { echo "!! $1"; printf '\nPress Return to close this window.'; read -r _; exit 1; }

[ -f "$SETTINGS" ] || fail "This Mac is not set up yet (run setup-mac.command)."
PASS="$(sed -n 's/.*Password=\([^;"]*\).*/\1/p' "$SETTINGS" | head -1)"
docker info >/dev/null 2>&1 || fail "Docker Desktop is not running. Open it, then run this again."

STAMP="$(date +%Y-%m-%d_%H%M)"
DEST="$HOME/MikroTek Backups/$STAMP"
mkdir -p "$DEST"

echo "1/3 Backing up the database..."
docker exec "$CONTAINER" mkdir -p /var/opt/mssql/backup
docker exec "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$PASS" -b \
  -Q "BACKUP DATABASE MikroTekArchivingDb TO DISK = N'/var/opt/mssql/backup/MikroTekArchivingDb.bak' WITH INIT, COPY_ONLY" >/dev/null \
  || fail "The database backup failed. Is the database container running?"
docker cp "$CONTAINER:/var/opt/mssql/backup/MikroTekArchivingDb.bak" "$DEST/MikroTekArchivingDb_$STAMP.bak" || fail "Could not copy the backup out of Docker."

echo "2/3 Backing up the scanned files and sign-in keys (App_Data)..."
[ -d App_Data ] && ditto -c -k --keepParent App_Data "$DEST/App_Data_$STAMP.zip"

echo "3/3 Saving this Mac's settings (database password)..."
cp "$SETTINGS" "$DEST/appsettings.macOS.json"

echo
echo "Backup saved in: $DEST"
echo "Keep a copy somewhere else too (USB drive, Google Drive). It contains the database password, so keep it safe."
open "$DEST"
printf '\nPress Return to close this window.'
read -r _
