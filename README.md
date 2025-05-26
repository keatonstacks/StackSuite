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

- **Connectivity Testing**  
  Includes ICMP ping and fallback TCP port probing for deeper reachability analysis (e.g., FTP, HTTP, SSH).

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

## 📜 License

MIT License — free for personal and commercial use.  
See `LICENSE.md` for full terms.

---

## 🤝 Credits

Developed and maintained by **Keaton Stacks**