# **IC-R7000 SkyCAT Driver**

_Development Notes & Community Contribution Guide_

Prepared by N1BAQ

# Background

The Icom IC-R7000 wideband receiver had no SkyCAT command definition file, making it incompatible with SkyRoof's native CAT control. The goal was to get SkyRoof to control the R7000 for Doppler-corrected satellite tracking.

# Why Not rigctld/hamlib?

We started with hamlib's rigctld since it lists the R7000 as model 3040. Basic communication worked - `rigctl -m 3040 -r COM4 -s 1200` successfully returned frequencies. However rigctld failed with SkyRoof because:

- SkyRoof sends a V VFOA (VFO select) command during setup
- The R7000 rejects it with error -9 (command rejected by rig) - it is a receiver with no VFO A/B concept
- SkyRoof disconnects on that error and keeps retrying endlessly

SkyCAT was the correct solution since it uses a custom command definition file that only sends commands the radio actually supports.

# Building the JSON - Where We Started Wrong

We built IC-R7000.json by modifying the IC-706MKIIG.json that ships with SkyCAT, substituting what we believed was the R7000's CI-V address. That address - 52h - came from hamlib's own model database for the R7000 (model 3040).

**This was wrong, and it cost hours of debugging.**

Hamlib uses 52h internally but apparently handles the address mismatch gracefully enough to still get responses during direct rigctl testing. This masked the real problem completely - the R7000 was receiving every command, echoing them back on the CI-V bus, and silently ignoring them because they were addressed to the wrong radio.

The symptom was maddening: SkyCAT connected, setup succeeded, frequency write commands returned OK, but the radio never changed frequency.

**The R7000's actual CI-V address per its own manual is 08h - not 52h.**

If you are starting from scratch, go to the R7000 manual first - not hamlib's model database.

# Key Discoveries

## 1\. CI-V Address is 08h - NOT 52h

The single most important finding. The R7000 manual explicitly states the default CI-V address is 08h. Hamlib lists it as 52h, which is incorrect. Using 52h causes the radio to echo all commands but silently ignore them.

## 2\. reply: null Required Throughout

The R7000 at 1200 baud is slow. SkyCAT's default 1-second timeout expires before the radio's acknowledgement arrives. Setting "reply": null on all commands lets SkyCAT fire-and-forget rather than waiting for confirmations that time out. In practice the radio responds correctly regardless.

## 3\. Setup Command Needs reply: null

SkyCAT requires a setup block in the simplex section or it fails to initialize. A simple read-frequency command works as the setup ping, but with "reply": null to avoid the timeout.

## 4\. echo: true is Correct

The R7000 echoes all CI-V commands back on the bus - standard CI-V behavior for older Icom radios. Setting "echo": true tells SkyCAT to expect and discard the echo before looking for a reply.

## 5\. Remote Switch Must Be ON

The rear panel REMOTE switch on the R7000 must be in the ON position for CI-V commands to be acted upon. With it OFF the radio still echoes commands but ignores them - a symptom identical to the wrong address problem, making it very confusing to diagnose.

## 6\. SkyRoof CAT Delay Setting

Set the Delay in SkyRoof's CAT Control settings to 500-1000ms initially when working with the R7000's slow 1200 baud interface.

# Final Working IC-R7000.json

Place this file in the Rigs folder of your SkyCAT installation:

```json
{
  "id": 3040,
  "echo": true,
  "default_baud_rate": 1200,
  "cross_band_split": false,
  "bad_reply": [
    "FE",
    "FE",
    "E0",
    "08",
    "FA",
    "FD"
  ],
  "comment": "Icom IC-R7000. CI-V address 08h (NOT 52h as in hamlib). reply:null required due to 1200 baud timeout.",
  "simplex": {
    "setup": {
      "messages": [
        {
          "command": [
            "FE",
            "FE",
            "08",
            "E0",
            "03",
            "FD"
          ],
          "reply": null,
          "comment": "Read frequency as setup ping"
        }
      ],
      "restriction": "when_setting_up"
    },
    "read_rx_frequency": {
      "messages": [
        {
          "command": [
            "FE",
            "FE",
            "08",
            "E0",
            "03",
            "FD"
          ],
          "reply": null
        }
      ],
      "restriction": "when_receiving"
    },
    "read_tx_frequency": null,
    "read_rx_mode": {
      "messages": [
        {
          "command": [
            "FE",
            "FE",
            "08",
            "E0",
            "04",
            "FD"
          ],
          "reply": null
        }
      ],
      "restriction": "when_receiving"
    },
    "read_tx_mode": null,
    "read_ptt": null,
    "write_rx_frequency": {
      "messages": [
        {
          "command": [
            "FE",
            "FE",
            "08",
            "E0",
            "05",
            null,
            null,
            null,
            null,
            null,
            "FD"
          ],
          "reply": null,
          "command_param": {
            "format": "BCD_LE"
          }
        }
      ],
      "restriction": "when_receiving"
    },
    "write_tx_frequency": null,
    "write_rx_mode": {
      "messages": [
        {
          "command": [
            "FE",
            "FE",
            "08",
            "E0",
            "06",
            null,
            "FD"
          ],
          "reply": null,
          "command_param": {
            "format": "Enum",
            "values": {
              "LSB": [
                "00"
              ],
              "USB": [
                "01"
              ],
              "AM": [
                "02"
              ],
              "CW": [
                "03"
              ],
              "RTTY": [
                "04"
              ],
              "FM": [
                "05"
              ],
              "WFM": [
                "06"
              ]
            }
          }
        }
      ],
      "restriction": "when_receiving"
    },
    "write_tx_mode": null,
    "write_ptt_off": null,
    "write_ptt_on": null
  }
}
```

# Where to Contribute

## SkyCAT GitHub

Submit a pull request adding IC-R7000.json to the Rigs folder:

- URL: <https://github.com/VE3NEA/SkyCAT>
- Fork the repo, add IC-R7000.json to the Rigs/ folder, open a pull request
- Include this document's notes in the PR description

## SkyRoof Google Group

Alex VE3NEA (the developer) is very active and responsive. Post your findings and the working JSON:

- URL: <https://groups.google.com/g/skyroof>
- He will likely incorporate it into the official SkyCAT release

## hamlib Issue Report

The incorrect 52h address in hamlib's R7000 model definition is worth flagging so future users are not misled:

- URL: <https://github.com/Hamlib/Hamlib/issues>
- Note that hamlib model 3040 (IC-R7000) has CI-V address 08h, not 52h

# Quick Setup Instructions

For anyone setting up IC-R7000 with SkyRoof for the first time:

- **Hardware:** Connect R7000 to PC via CT-17 CI-V interface. Set rear panel REMOTE switch to ON.
- **SkyCAT:** Download SkyCAT 1.6 from <https://github.com/VE3NEA/SkyCAT/releases>. Place IC-R7000.json in the Rigs/ folder.
- **Start daemon:** `skycatd.exe -m IC-R7000 -r COM4 -s 1200 -t 4532` (adjust COM port as needed)
- **SkyRoof settings:** CAT Control > RX CAT: Host 127.0.0.1, Port 4532, Enabled True. Radio Type: Simplex. Delay: 500ms.
- **Verify:** RX CAT dot in SkyRoof status bar should turn green. R7000 frequency display should step with Doppler correction during satellite pass.

_Developed and tested by N1BAQ, FN41QO. May 2026. Verified working with SkyCAT 1.6 and SkyRoof 1.29._