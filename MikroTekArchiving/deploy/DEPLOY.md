# Deploying MikroTek Archiving on the school network

One Windows PC is the **server**. It runs the portal and the database.
Every other PC just opens the portal in **Microsoft Edge or Google Chrome**. They don't need to install anything.

```
 [ Bookkeeper PC ]  [ Admin PC ]  [ Viewer PC ]      Edge / Chrome
          \              |              /
           \------ same Wi-Fi / LAN ---/
                         |
            [ SERVER PC  (Windows 10/11) ]
              MikroTek.Archiving.exe  : port 5000
              SQL Server Express      : database
```

---

## Part A: Set up the server PC (once)

### 1. Install SQL Server Express
1. Download **SQL Server Express** (free) from Microsoft and choose **Basic** install.
2. At the end the installer shows the instance name. It is normally `SQLEXPRESS`.
   If yours is different, change `Server=.\SQLEXPRESS` in `appsettings.Production.json` to match.
3. Optional but handy: install **SQL Server Management Studio (SSMS)** so you can see the data and make backups.

You don't create the database yourself. The portal creates `MikroTekArchivingDb` the first time it starts.

> LocalDB (what Visual Studio uses) only works for the Windows user who is signed in to Visual Studio. That's why the server uses SQL Server Express.

### 2. Publish from Visual Studio (on your development PC)
1. In Solution Explorer, right-click **MikroTek.Archiving** and choose **Publish...**
2. Pick the profile **LanServer** and click **Publish**.
3. The output goes to `MikroTekArchiving\bin\Publish\LanServer\`.
   The output is *self-contained*, so the server PC does **not** need .NET installed.

### 3. Copy the published folder to the server
Copy the whole `LanServer` folder to the server PC as **`C:\MikroTek`** (a USB drive is fine).
- Don't put it in `C:\Program Files`. The app writes to its `App_Data` folder, and Windows blocks that in Program Files.
- Check that `C:\MikroTek` contains `MikroTek.Archiving.exe`, `start-server.bat` and the other `.bat` files.

### 4. Open the firewall (once)
Right-click **`open-firewall.bat`** and choose **Run as administrator**.
This lets PCs on the **same local network** reach port 5000. The internet is still blocked.

### 5. Keep the server awake and on the same address
- **Settings > System > Power**: set *Sleep* to **Never** while plugged in.
- Give the server a fixed IP address, so the address the other PCs use doesn't change. Either:
  - reserve its address in the router (DHCP reservation), or
  - use the PC's name instead of the IP, for example `http://LNHS-SERVER:5000/Portal`. This usually works on a Windows network.

### 6. Start the server
Double-click **`start-server.bat`**. The window lists the addresses to use, for example:

```
Other PCs can open: http://192.168.1.20:5000/Portal
On this PC open:    http://localhost:5000/Portal
```

Keep that window open (you can minimize it). Closing it stops the portal.

### 7. (Optional) Start it automatically at boot as a Windows Service
Test with `start-server.bat` first. Once everything works:
1. Close the `start-server.bat` window.
2. Right-click **`install-service.bat`** and choose **Run as administrator**.

The service runs even when nobody is signed in, and restarts itself if it crashes.
It runs as *Local System*, so it needs its own permission in SQL Server:
- `install-service.bat` grants it automatically if `sqlcmd` is installed.
- If the script says `sqlcmd was not found`, open **SSMS**, connect to `.\SQLEXPRESS`, open `sql\grant-service-access.sql`, and click **Execute**.

To remove the service, run `uninstall-service.bat` as administrator.

---

## Part B: On every other PC

1. Connect to the **same network** as the server.
2. Open **Edge or Chrome** and go to `http://SERVER-IP:5000/Portal`, for example `http://192.168.1.20:5000/Portal`.
3. Make it easy to find again: press **Ctrl+D** to bookmark it, or in Edge go to *... > Apps > Install this site as an app*.

If a yellow bar says the browser is out of date, update Edge or Chrome. The portal needs a browser from 2023 or newer.

---

## Checking that everything works

From any PC, open `http://SERVER-IP:5000/health`:

```json
{ "status": "running", "server": "LNHS-SERVER", "database": "ok" }
```

| You see | Meaning / fix |
|---|---|
| Page doesn't load at all | Server not started, or the firewall is closed. Re-run `open-firewall.bat` as admin, and make sure both PCs are on the same network. |
| Works on the server, not on other PCs | Firewall, or the Wi-Fi has **client isolation** turned on (common on *guest* / school Wi-Fi). Use the staff network or a LAN cable. From the other PC, run `ping SERVER-IP` in Command Prompt to test. |
| `"database": "not reachable"` | SQL Server isn't running. Press Windows, type *Services*, find **SQL Server (SQLEXPRESS)**, and start it. Also check the connection string in `appsettings.Production.json`. |
| Server window closes immediately | Port 5000 is used by another program. Change `5000` in `appsettings.Production.json` (and in the firewall rule). |
| Service won't start | Open *Event Viewer > Windows Logs > Application*. Usually the fix is the SQL permission in step 7. |

---

## Updating to a new version

1. Stop the portal: close the `start-server.bat` window, or run `sc stop MikroTekArchiving` as admin.
2. Publish again from Visual Studio (step 2).
3. Copy the new files over `C:\MikroTek`, but **keep**:
   - the `App_Data` folder (sign-in keys and, later, the uploaded scans)
   - `appsettings.Production.json`, if you changed it on the server
4. Start the portal again. Database changes (migrations) are applied automatically on start.

## Moving the database from your PC to the server (optional)
Usually you **don't need to**: the server creates a fresh `MikroTekArchivingDb` the first time it starts.
Move it only when your PC has real records you want to keep.

**Option A: Backup and restore (best)**
1. On your PC, open SSMS and connect to `(localdb)\MSSQLLocalDB` (tick *Trust Server Certificate*).
2. Right-click **MikroTekArchivingDb** and choose *Tasks > Back Up...*
   - Backup type: **Full**.
   - Destination: Disk. Remove the old path, click **Add...**, and save to `C:\Backup\MikroTekArchivingDb.bak` (make the folder first).
3. Copy the `.bak` to the server with a USB drive. Put it in SQL Server's own backup folder, for example
   `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\`
   (the number can differ). SQL Server is allowed to read that folder.
4. On the server, open SSMS and connect to `.\SQLEXPRESS` (tick *Trust Server Certificate*).
5. Right-click **Databases** and choose *Restore Database...*
   1. Pick **Device**, click **...**, then **Add**, and choose the `.bak`.
   2. Database name: `MikroTekArchivingDb`.
   3. On the **Files** page, tick **Relocate all files to folder** (your PC's paths don't exist on the server).
   4. On the **Options** page, tick *Overwrite the existing database* if the server already created one.
   5. Click **OK**.
6. Restart the portal. If you use the Windows Service, run `sql\grant-service-access.sql` again.

> If the restore says the backup is from a *newer version*, the server's SQL Server Express is older than your PC's LocalDB.
> Run `SELECT @@VERSION` on both to compare. Then either install the same or a newer SQL Server Express on the server, or use Option B.

**Option B: Script it (works across versions)**
1. On your PC in SSMS, right-click **MikroTekArchivingDb** and choose *Tasks > Generate Scripts...*
2. Keep *Script entire database*. Click **Next**, then **Advanced**, and set *Types of data to script* to **Schema and data**. Save to a `.sql` file.
3. On the server, create an empty database named `MikroTekArchivingDb` in SSMS.
4. Open the `.sql` file, pick that database, and click **Execute**.

## Backups
- **Database:** in SSMS, right-click `MikroTekArchivingDb` and choose *Tasks > Back Up...*
- **Files:** copy `C:\MikroTek\App_Data` to a USB drive or another PC.

Back up both regularly (at least weekly), because this is the school's disbursement archive.

## Testing on the LAN straight from Visual Studio
Choose the **lan** launch profile (next to the green Run button) and press F5.
Other PCs can then open `http://YOUR-PC-IP:5088/Portal` while you debug.
Windows may ask to allow the app through the firewall. Allow it on **Private** networks.
