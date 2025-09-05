---
title: SkyCAT Library
nav_order: 2
---

# SkyCAT Library

SkyCAT is available as a **.NET** assembly. It does not have any platform-specific code and may be used on all platforms where **dotnet** is supported, including Windows, Linux and MacOS. The main class in the assembly is `CatCommandSender`. Here is an example showing how to use this class:

``` C#
var sender = new CatCommandSender();

sender.SelectRadio("IC-9700");
sender.SerialPort.PortName = "COM2";
sender.SerialPort.BaudRate = 9600;
sender.SerialPort.Open();
sender.SetupRadio(OperatingMode.Simplex);

var frequency = sender.SendCommand(CatCommand.read_rx_frequency)

sender.SendCommand(CatCommand.write_rx_mode, "CW")
```

See the [source code](https://github.com/VE3NEA/SkyCAT/tree/master/skycatd) of **skycatd.exe** for a real-world example.
