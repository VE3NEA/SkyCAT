---
title: SkyCAT
nav_order: 1
---

# SkyCAT

## Overview

SkyCAT is a new CAT control engine, open source and multi-platform. It is available as a .NET library, **SkyCAT.dll**, that developers can include in their software, and also as a command-line program, **skycatd.exe**, that accepts TCP commands compatible with
[rigctld.exe](https://hamlib.sourceforge.net/html/rigctld.1.html) from the HamLib package.

SkyCAT has much in common with the existing CAT  engines, such as
[OmniRig](https://dxatlas.com/OmniRig/), [HamLib](https://hamlib.github.io/) and [FLRig](https://github.com/w1hkj/flrig):

- like HamLib, it supports remote operation via TCP;
- like OmniRig, it has open architecture, support of new radios is added by creating a file with the radio commands in a text editor;
- like FLRig, it does not poll the radio. The client application makes an explicit call to read the radio frequency or mode when it needs it, which makes the system more responsive.

It also has a few features that are not available in the existing CAT engines, as you will see below.

## Operating Modes

Many radios have different CAT commands for the same action, depending on the operating mode of the radio. An example is the Set Frequency command that is usually different in the SAT and VFO modes. Existing CAT software often uses wrong commands because it does not know the current operating mode. SatCAT sets the mode explicitly and thus knows which commands to use. The following operating modes are supported:

- Duplex
- Split
- Simplex

See the [SkyCAT Commandset File Format](commandset-format.md) for a description and usage of these modes.

## Supported Radios


## See Also

- [SkyCAT Library](library.md)
- [skycatd.exe](skycatd.md)
- [SkyCAT Commandset File Format](commandset-format.md)
- [Source Code](https://ve3nea.github.io/SkyCAT/index.html)
- [Download](download.md)
