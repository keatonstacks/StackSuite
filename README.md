# StackSuite

**StackSuite** is a high-performance, portable network scanning and diagnostic tool built with .NET 8 LTSC. It supports rapid host discovery, customizable scan inputs, real-time feedback, and export capabilities—all packaged into a single executable for maximum portability.

---

## 🔧 Features

- **Adapter-Based Scanning**  
  Select any active network adapter and scan its associated subnet for live hosts.

- **Manual Input Support**  
  Scan:
  - Individual IP addresses or hostnames
  - IP/hostname ranges (e.g., `192.168.1.1-192.168.1.254`)
  - Imported lists from `.txt` or `.csv` files
  
- **Interactive Right-Click Tools**
  - Launch RDP, SSH, web UI, or file share from any scanned host
  - Context-aware: actions only enabled when ports or device types match

- **Connectivity Testing**  
  Includes ICMP ping with fallback to common TCP port probing (FTP, SSH, Telnet, HTTP, HTTPS).

- **Offline Device Handling**  
  Option to show or hide unreachable (offline) devices in the results grid.

- **Export Results**  
  Save scan results to CSV format for further reporting or auditing.

- **Scan Management**  
  - Cancel in-progress scans at any time  
  - Limit concurrent scans for system efficiency  
  - Real-time status updates per target

- **Smart Labeling** *(if enabled)*  
  Automatically classifies device types based on MAC vendor (e.g., Crestron → Control System, Dell → Workstation).

- **Tabbed Remote Tools**  
StackSuite now includes:
- 🔐 **SSH Console** for shell access to reachable devices  
- 📁 **SFTP Client** for secure file upload/download via SSH  
Each supports multiple dynamic sessions in dedicated tabs, with custom headers and close buttons.

- **Material Design UI**  
  Clean, modern interface using `MaterialDesignInXAML` for dark mode and visual consistency.

---

## 🚀 Getting Started

### Requirements

- **.NET 8 Runtime**  
  Not required if using the single-file executable publish target.

### Download

Clone the repo or download the latest release:

```bash
git clone https://github.com/yourusername/StackSuite.git
```

### Running

Use the prebuilt `.exe`:

```bash
StackSuite.exe
```

> No installation required. Fully self-contained.

---

## 🖥️ Usage Overview

1. **Launch the app** — Adapter list will auto-populate.
2. **Choose scan mode** — Subnet, range, or file input.
3. **Click Scan** — Watch live updates as hosts respond.
4. **Toggle offline visibility** — Focus only on online results, if needed.
5. **Export** — Save results with one click.

---

## 📊 Understanding the Results

Each row in the StackSuite scan results represents a discovered device, along with key details:

| Column        | Description |
|---------------|-------------|
| **IP Address** | The target host address scanned. |
| **MAC Address** | Resolved via ARP (local subnet only). |
| **Vendor**     | Derived from the MAC OUI using local lookup (e.g., Apple, Dell, Cisco). |
| **Device Type** | Optional smart label based on vendor (e.g., Workstation, Control System). |
| **Ping Status** | Indicates whether the device responded to ICMP ping or TCP fallback. |
| **Latency (ms)** | Round-trip time for ICMP ping (lower = faster). |
| **TTL**        | Time-to-Live value from the reply — can hint at OS type (see below). |

### 🧠 Interpreting TTL Values

TTL (Time-To-Live) can hint at the operating system of the responding device:

| TTL Value | Likely OS / Device Type |
|-----------|--------------------------|
| 128       | Windows (default) |
| 64        | Linux, Unix, Android |
| 32        | macOS, iOS, Apple devices (sometimes) |
| 255       | Cisco, network infrastructure |

> Note: TTL can vary due to firewalls, proxying, or hop count. Treat it as a heuristic, not a guarantee.

### 🔌 Offline Devices

Devices that fail both ping and TCP fallback checks are considered **offline**. You can toggle visibility of these rows using the **"Show Offline Devices"** switch.

### 📤 Exporting

Use the **Export to CSV** feature to save current scan results, including both online and (optionally) offline devices, for auditing or reporting.

---

## 📦 Single Executable Notes

StackSuite is packaged using `.NET 8` single-file publish options with trimming disabled for compatibility:

```xml
<PublishSingleFile>true</PublishSingleFile>
<IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
<PublishTrimmed>false</PublishTrimmed> <!-- For WPF compatibility -->
```

---

## 🧠 Smart Features (Advanced)

- **Vendor Lookup**  
  Built-in MAC vendor resolution (via local OUI database).

- **Device Type Detection**  
  Optional XML-based mappings classify devices into categories.

- **Contextual UI Actions**
  Dynamically enables or disables right-click actions like RDP and UNC browsing based on MAC vendor and open ports (e.g., skips Apple for RDP).

---

## 📁 Project Structure

```plaintext
/StackSuite
 ├── StackSuite.exe              # Published executable
 ├── VendorMappings.xml          # Optional smart labeling source
 ├── Products.xml                # Used for labeling/importable tools
 ├── Resources/                  # Embedded UI assets
 └── README.md                   # This file
```

---

## 🛠️ Development Notes

- Built with **WPF** and **MaterialDesignInXAML**
- Network discovery relies on:
  - `Ping`
  - `TcpClient`
  - `SendARP` via `iphlpapi.dll` (for MAC resolution)

- Supports threading and concurrency limits to avoid UI freezing.


---

## 🗒️ Revision History

### Version 1.0.0.0 – Initial Public Release
- First stable release of StackSuite.
- Added full support for:
  - Subnet, range, and list-based host scanning
  - Network adapter selection
  - Host reachability testing via ICMP ping and TCP fallback (ports: 21, 22, 23, 80, 443)
  - CSV export of scan results
  - Toggle for showing/hiding offline devices
  - MAC address vendor lookup and smart device-type labeling
- Fully themed UI using MaterialDesignInXAML (Dark Mode)
- Packaged as a self-contained `.NET 8` single-file executable

### 🔧 Usability & Interaction Enhancements
- **Right-click context menu** on each scanned host with:
  - 🖥️ Open in RDP
  - 🔐 Open in SSH Console
  - 🌐 Open Web UI (HTTP/HTTPS)
  - 📂 Browse via UNC Path
- Context menu dynamically enables actions based on:
  - Open ports
  - Device type (e.g., Workstation, Server)
  - MAC vendor (e.g., Apple = no RDP/UNC)
- **SSH Console & SFTP Client Tabs**  
  - Launch from main navigation or right-click  
  - Spawn multiple dynamic tabs for each session  
  - Tab headers show `username@host`, closable with `×` icon
- **Ctrl+C** support to copy selected row(s) as tab-separated values (Excel-friendly)
- **Double-click** on a row opens a detailed host information popup *(coming soon)*

> Future versions will expand support for deeper service detection, diagnostics tools (e.g., `traceroute`, `nmap` integration), scheduled scanning, and headless/CLI operation.

---

## 📜 License

MIT License — free for personal and commercial use.  
See `LICENSE.md` for full terms.

---

## 🤝 Credits

Developed and maintained by **Keaton Stacks**