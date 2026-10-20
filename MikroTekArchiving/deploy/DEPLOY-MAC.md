# Running MikroTek Archiving on a MacBook (Apple silicon M1/M2/M3)

The MacBook is the **server**: it runs the portal and the database.
Other computers on the same Wi-Fi only open the portal in a browser (Safari, Chrome or Edge). They don't install anything.

```
 [ Bookkeeper laptop ]  [ Admin PC ]  [ Viewer phone ]     any browser
            \               |               /
             \------ same Wi-Fi / LAN -----/
                            |
              [ MacBook M1  (the server) ]
                MikroTek.Archiving      : port 5080
                Docker Desktop
                  └ SQL Server 2022     : database (only this Mac can reach it)
                Tesseract (Homebrew)    : scan reader
```

On a Mac the portal uses port **5080**, not 5000. macOS keeps port 5000 for AirPlay Receiver.

---

## Part A: Prepare the Mac (once)

You need an internet connection and the Mac's admin password.

### 1. Install Homebrew
Open **Terminal** (Applications > Utilities). Go to <https://brew.sh>, copy the one line shown there, paste it into Terminal and press Return. Follow what it asks. At the end it may show two `echo ... >> ~/.zprofile` lines to run; run them.

### 2. Install Docker Desktop
1. Download **Docker Desktop for Mac with Apple silicon** from <https://www.docker.com/products/docker-desktop/>.
2. Open it once and accept the terms. You can skip signing in.
3. In Docker Desktop open **Settings > General** and make sure these are **on**:
   - **Use Rosetta for x86_64/amd64 emulation on Apple Silicon** (SQL Server needs it)
   - **Start Docker Desktop when you sign in to your computer**

Docker Desktop is free for schools and other educational use.

---

## Part B: Publish the portal (on the development PC)

1. In Visual Studio, right-click **MikroTek.Archiving** and choose **Publish...**
2. Pick the profile **MacServer** and click **Publish**. (Command line: `dotnet publish MikroTekArchiving -p:PublishProfile=MacServer`)
3. The output is in `MikroTekArchiving\bin\Publish\MacServer\`. It is self-contained, so the Mac does **not** need .NET.
4. Copy the whole `MacServer` folder to the Mac (USB drive, Google Drive or AirDrop) and rename it to **`MikroTek`** in your **home folder**: `/Users/<your name>/MikroTek`.
   - Don't put it in Desktop, Documents or Downloads. macOS stops programs in those folders from starting by themselves.

---

## Part C: Set up and start (on the Mac)

### 1. Run the setup (once)
The first time, macOS may not open the file by double-click, because it came from another computer. So:
1. Open **Terminal**.
2. Type `bash ` (with a space after it), drag **`setup-mac.command`** from the `MikroTek` folder into the Terminal window, and press Return.

The setup:
- unblocks the folder,
- installs the scan reader with Homebrew (`brew install tesseract`),
- starts Docker and creates the database container **mikrotek-sql** with a random password. The first download is about 1.5 GB and takes several minutes.
- saves this Mac's settings in `~/Library/Application Support/MikroTek Archiving/appsettings.macOS.json`.

From now on, you can double-click the `.command` files in Finder.

### 2. Start the portal
Double-click **`start-server-mac.command`**. The window shows the addresses to use, for example:
```
 On this Mac open:     http://localhost:5080/Portal
 Other PCs/Macs open:  http://192.168.1.25:5080/Portal
```
Keep that window open. You can minimise it. Closing it stops the portal.
The first time, sign in as **admin / admin123**. The portal then asks you to choose a new password.

If macOS asks **"Do you want the application MikroTek.Archiving to accept incoming network connections?"**, click **Allow**. Otherwise the other computers can't open it.

### 3. Keep the MacBook awake and on the same address
- Keep it **plugged in**. While the portal runs, the start script keeps the Mac from going to sleep (`caffeinate`).
- **Closing the lid puts a MacBook to sleep** and stops the portal for everyone. Leave the lid open, or use it with an external display, keyboard and power.
- In **System Settings > Battery > Options**, turn on **Prevent automatic sleeping on power adapter when the display is off**.
- Give the Mac a fixed address in the router (DHCP reservation), so the address the others use doesn't change.

### 4. (Optional) Start it by itself at log-in
Test with `start-server-mac.command` first. Once everything works:
1. Close that window.
2. Double-click **`install-autostart-mac.command`**.

From then on, the portal starts whenever this user logs in, and it restarts itself if it stops. Its log is in `~/Library/Logs/MikroTek Archiving.log`.
To undo it, run `uninstall-autostart-mac.command`.
To have it running right after a restart, turn on automatic log-in for this user (System Settings > Users & Groups).

---

## Part D: The other computers
1. Connect to the **same Wi-Fi** as the MacBook.
2. Open a browser and go to `http://MAC-IP:5080/Portal`, for example `http://192.168.1.25:5080/Portal`.
3. Bookmark it.

---

## Checking that everything works
Open `http://localhost:5080/health` on the Mac, or `http://MAC-IP:5080/health` from another computer:
```json
{ "status": "running", "database": "ok" }
```

| You see | Meaning / fix |
|---|---|
| `"database": "not reachable"` | Docker Desktop isn't running. Open it, wait for "Engine running", then restart the portal. |
| Setup says *SQL Server did not start* | Turn on **Use Rosetta** in Docker Desktop > Settings > General, then Apply & restart and run the setup again. |
| Works on the Mac, not on other computers | You clicked *Deny* on the network question. Open System Settings > Network > Firewall > Options and allow MikroTek.Archiving. Also check that the Wi-Fi doesn't have **client isolation** (common on guest Wi-Fi). |
| Import / Upload Scanned DV says the scan reader isn't installed | Run `brew install tesseract` in Terminal, or run `setup-mac.command` again. |
| *"MikroTek.Archiving" cannot be opened* | Run `setup-mac.command` again. It unblocks and signs the portal. |
| The page stops responding after a while | The MacBook went to sleep (lid closed or on battery). See Part C, step 3. |

---

## Backups (at least weekly)
Double-click **`backup-mac.command`**. It saves into `~/MikroTek Backups/<date>`:
- the database (`MikroTekArchivingDb_<date>.bak`),
- the scanned files and sign-in keys (`App_Data_<date>.zip`),
- this Mac's settings with the database password (`appsettings.macOS.json`).

Copy that folder to a USB drive or Google Drive. Keep it private, because it holds the database password.

### Restoring a database backup
Stop the portal first. Then, in Terminal, go to the backup folder and run these (the password is in `appsettings.macOS.json`):
```bash
docker cp MikroTekArchivingDb_<date>.bak mikrotek-sql:/var/opt/mssql/backup/restore.bak
docker exec mikrotek-sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'THE-PASSWORD' \
  -Q "RESTORE DATABASE MikroTekArchivingDb FROM DISK=N'/var/opt/mssql/backup/restore.bak' WITH REPLACE"
```
Then unzip `App_Data_<date>.zip` over `~/MikroTek/App_Data` and start the portal again.

---

## Updating to a new version
1. Stop the portal: close its window, or run `uninstall-autostart-mac.command` if it starts at log-in.
2. Publish again (Part B) and copy the new files over `~/MikroTek`, but **keep the `App_Data` folder** (scanned files and sign-in keys).
   This Mac's settings are outside the folder, so they are kept automatically.
3. Run `setup-mac.command` again. It is quick the second time and unblocks the new files.
4. Start the portal again. Database changes (migrations) are applied automatically on start.

## Starting over (deletes the whole archive)
Only if you really want an empty archive. Make a backup first.
```bash
docker rm -f mikrotek-sql && docker volume rm mikrotek-sql-data
rm "$HOME/Library/Application Support/MikroTek Archiving/appsettings.macOS.json"
```
Then run `setup-mac.command` again.
