---
title: SkyCAT Commandset File Format
nav_order: 4
---

# SkyCAT Commandset File Format

_Updated: 2025-09-05_.

[SkyCAT](skycat.md) stores the CAT commands of each supported radio in a separate file of type **.json**. The file name is the model name of the radio, for example, _"IC-9700.json"_ contains the commands of ICOM IC-9700. Support of new radio models may be added to SkyCAT by creating new command set files.

## Operating Modes

Many radios have different CAT commands, depending on the operating mode of a radio. SkyCAT explicitly sets the operating mode during the radio initialization and then uses the commands that correspond to that mode. The following operating modes are supported:

- **Duplex** - the radio can transmit and receive at the same time, but only on different bands. It allows setting the RX and TX frequencies at any time;

- **Split** - the radio can receive and transmit, but not at the same time. However, it has two VFO's, so that the receive and transmit frequencies may be set at any time, and the radio automatically switches the VFO when it goes from receive to transmit and back;

- **Simplex** - the radio has commands to set the frequency of only one VFO. The software is responsible for changing radio's frequency and mode every time the radio switches between RX and TX.

## Top Level File Format

The command-set file stores data in the 
[JSON](https://www.digitalocean.com/community/tutorials/an-introduction-to-json)
format. The JSON document has the following structure:

```json
{
  "id": 3081,
  "echo": false,
  "default_baud_rate": 115200,
  "cross_band_split": false,
  "bad_reply": ["FE", "FE", "E0", "A2", "FA", "FD"],
  
  "duplex": {...},
  "split": {...},
  "simplex": {...}
}
```

where

- **id** is the radio model's id in the
[HamLib list of models](https://github.com/Hamlib/Hamlib/wiki/Supported-Radios).
This value allows the users to specify the radio model in a format compatible with HamLib. 3081 in the example above is the ID of the IC-9700 radio;

- **echo** - some radios, such as old ICOM models, echo all commands back to the sender. For such radios set the `echo` field to true. If echo is not sent, or if it may be disabled in radio's settings, set it to false;

- **default_baud_rate** - the highest Baud rate of the RS-232 interface supported by the radio;

- **cross_band_split** - set it to true if the radio can work in the Split mode when the receive and transmit frequencies are on different bands. Surprisingly, many radios cannot do that;

- **bad reply** - if the radio sends a certain sequence of bytes in response to a failed command, enter that sequence here, otherwise set it to `null`. The description of the byte sequence format is provided below;

- **duplex, split, simplex** -  contain the commands used by the radio in the corresponding operating modes. The **simplex** section is required, the other two should be present only if the radio supports those modes.

## Command Set Format

The values of the **duplex, split** and **simplex** fields are objects that have the following structure:

```json
{
    "setup": {... },
    "read_rx_frequency": {... },
    "read_tx_frequency": {... },
    "read_rx_mode": {... },
    "read_tx_mode": {... },
    "read_ptt": {... },
    "write_rx_frequency": {... },
    "write_tx_frequency": {... },
    "write_rx_mode": {... },
    "write_tx_mode": {... },
    "write_ptt_off": {... },
    "write_ptt_on": {... }
}
```

Each field in the object describes the corresponding command. If the command is not supported by the radio, its value is set to `null`:

```json
"write_tx_mode": null,
```

However, some minimum set of commands should be present for the given operating mode to be useful. The software verifies that at least one command is not null.

## Command Format

The commands in the command set are represented with objects of the following structure:

```json
{
    "messages": [ ... ],
    "alt_messages": [ ... ],
    "restriction": "when_receiving"
}
```

- **messages** - an array of messages to send to the radio. A single command may require multiple messages to be sent. For example, the Split mode in FT-991 is enabled by sending two messages, `FR0;` and `FT1;`.

- **alt_messages** - an alternative array of messages that will be used if the **messages** returns an error. For example, IC-9700 in the SAT mode returns an error if the Set Frequency command attempts to set the Main receiver to the same band as the Sub receiver. When such error happens, the solution is to swap the Main and Sub VFO's and set the frequency again;

- **restriction** - optional. Include this field if the radio accepts the given command only in one of the modes:
  - **when_receiving**;
  - **when_transmitting**;
  - **when_setting_up** - set this value if the command interrupts reception or transmission, e.g., swaps the VFO, and thus may be used only during the initial setup.

## Message Format

The messages in the **messages** and **alt_messages** arrays are the objects with the following fields:

```json
{
  "comment": "command description",
  "command": [ ... ], 
  "command_param": { ... },
  "reply": [ ... ], 
  "reply_param": { ... },
  "ignore_error": true
}
```

- **comment** - optional, a short description that will be printed in the log for readability;
- **command** - required. The sequence of bytes that will be sent to the radio;
- **command_param** - only for the Write commands;
- **reply** - the sequence of bytes expected from the radio in reply to the command message. May be null if the radio does not reply to the given message;
- **reply_param** - only for the Read commands;
- **ignore_error** -- set it to `true` if the radio may return an error even if the command succeeds. For example, some radios reply with an error to the Set Split command if the Split mode is already enabled.

## Byte Sequence Format

The sequence of bytes is represented with  an array of strings, were each string is a two-character
[hex code](https://learn.sparkfun.com/tutorials/hexadecimal/all)
of the byte. Some bytes may have `null` values:

- in the Write commands the nulls indicate the bytes that will be replaced with the value of the parameter being written. For example, the Set Mode command of TS-2000,"MDn;", is stored as

  ```json
  "command": ["4D", "44", null, "3B"],
  ```

  where the `null` will be replaced with the mode byte;

- in the replies the nulls are the bytes that may have any value and should be ignored during the reply validation;

- in the replies to the Read commands some (or all) of the nulls are placeholders for the returned value. In the example below the reply to the Read Mode command of IC-9700 contains two nulls. The first null is a placeholder for the returned mode, and the second one is ignored as that byte contains selected filter that may have any value:

  ```json
    "reply": ["FE", "FE", "E0", "A2", "04",  null, null, "FD"]
  ```

## Parameter Format

The **command_param** and **reply_param** objects describe the parameter that is sent to the radio or received from the radio. The objects have the following structure:

```json
{
  "format": "enum", 
  "step": 10,
  "start": 4,
  "length": 1,
  "mask": [ "FF", "00" ],
  "values": { ... }
}
```

- **format** - one of:
  - **BCD_BE** - numerical value as a [Binary-Coded Decimal](https://www.geeksforgeeks.org/dsa/bcd-or-binary-coded-decimal/) (BCD) in the
  [Big Endian](https://en.wikipedia.org/wiki/Endianness) (BE) byte order. Example: frequency in the FT-847 commands;

  - **BCD_LE** - numerical value as BCD in the Little Endian (LE) byte order. Example: frequency in the IC-9700 commands;

  - **text** - numerical value as a sequence of [ASCII](https://www.ascii-code.com/) character codes. Example: frequency in the TS-2000 commands;

  - **enum** - a parameter that can take one of several pre-defined values, such as mode or ON/OFF setting.

- **step** - optional, defaults to 1. The steps in which a numerical value is expressed. Example: if step = 10, the frequency is specified in 10-Hz steps, so that 43000001 represents 430,000,010 Hz;

- **start** and **length** - optional. If the reply field contains more nulls than needed to represent the parameter, these settings are used to specify which bytes contain the parameter value;

- **mask** - only in the reply params, optional. The bitwise AND operation is performed between the mask and the bytes that contain the parameter, to remove the bits that may take any value. Example: the Read PTT command of FT-817 returns one byte. The 7-th bit of the byte contains the TX/RX flag, the rest of the bits contain some other parameters. `"Mask": [ "80" ]` clears irrelevant bits and leaves only the one of interest;

- **values** - is present if, and only if, the parameter format is **enum**. It contains the list of pre-defined values and the byte sequences that represent them. Example:

  ```
  "values": { "LSB": [ "00" ], "USB": [ "01" ], "CW": [ "03" ], "FM": [ "04" ] }
  ```
