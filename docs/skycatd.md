---
title: skycatd.exe
nav_order: 3
---

# skycatd.exe

**skycatd.exe** is a command-line application, based on the SkyCAT library, that listens on a TCP port for commands and controls the radio via a COM port.

## Installation

There is no installer, just [download](download.md) and unzip all files to a folder.

Make sure that [.NET 9.0 Desktop Runtime](https://learn.microsoft.com/en-us/dotnet/core/install/)
is installed on your system:

Windows:

``` bash
winget install Microsoft.DotNet.DesktopRuntime.9
```

MacOS:

``` bash
brew install dotnet
```
  
Linux (Ubuntu):
  
``` bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0
```
  
  More distros are available [here](https://learn.microsoft.com/dotnet/core/install/linux).

## Command Line Parameters

``` bash
skycatd -m IC-9700 -r COM9 -s 115200 -t 4532 -vvv -f
```

- **-m** - required. The radio model, either model name or numeric code:
  - the model name must be one of the [commandset file names](https://github.com/VE3NEA/SkyCAT/tree/master/Rigs), without extension;
  - the numeric code must be one of the codes printed with the **-l** command, see below;

- **-r** - required. The serial port name, e.g. "COM1" on Windows, or " /dev/ttyS0" on Linux;

- **-s** - The Baud rate of the serial port. Optional, the program knows the maximum speed of each supported radio;

- **-t** - TCP listening port, optional, defaults to 4532;

- **-vvv** - optional, enables detailed logging;

- **-f**  - optional, enables writing the log to a file.

<br>

In addition, there are options that print information and exit:

- **--help** - display the Help screen;

- **--version** - display the version information;

- **-l** - list supported radios and their numeric codes;

- **-a** - list capabilities of all supported radios.

## Running skycatd

Windows:

```bash
skycatd.exe <parameters>
```

Linux and MacOS:

```bash
dotnet skycatd.dll <parameters>
```

## Skycatd Commands

skycatd understands the following TCP commands, followed by the NewLine character:

| Action             | Command        |
|--------------------|----------------|
| setup(Duplex)      | U Duplex       |
| setup(Split)       | U Split        |
| setup(Simplex)     | U Simplex      |
| read_rx_frequency  | f              |
| read_tx_frequency  | i              |
| write_rx_frequency | F {frequency}  |
| write_tx_frequency | I {frequency}  |
| write_rx_mode      | M {mode} 0     |
| write_tx_mode      | X {mode} 0     |
| read_ptt           | t              |
| set_ptt_on         | T 1            |
| set_ptt_off        | T 0            |

where **frequency** is the frequency in Hertz, and **mode** is a mode name, e.g., CW.

For compatibility with rigctld.exe, the '**U SATMODE 1**', '**S 1 VFOB**' and '**S 0 VFOB**' commands are recognized as aliases of the Duplex, Split and Simplex setup commands respectively.
