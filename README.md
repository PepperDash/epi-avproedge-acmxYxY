![PepperDash Essentials Pluign Logo](/images/essentials-plugin-blue.png)

# Essentials Plugin for AVProEdge ACMX Series Matrix Switchers (c) 2025

## License

Provided under MIT license

## Overview

Provides routing control for the AVProEdge ACMX Series Matrix Switchers via RS232 or TCP.

[AVProEdge ACMX-88 User Manual](/docs/AC-MX-88%20Manual.pdf)

[AVProEdge ACMX-1616 User Manual](/docs/AC-MX-1616%20Manual.pdf)

## Communication Methods

***RS-232***

Default Baud: `57600`

```json
"control": {
    "method": "com",
    "controlPortDevKey": "processor",
    "controlPortNumber": 1,
    "comParams": {
        "protocol": "RS232",
        "baudRate": 57600,
        "dataBits": 8,
        "stopBits": 1,
        "parity": "None",
        "softwareHandshake": "None",
        "hardwareHandshake": "None",
        "pacing": 500
    }
}
```

***Telnet***

```json
"control": {
    "method": "tcpIp",
    "tcpSshProperties": {
        "address": "0.0.0.0",
        "port": 23,
        "username": "",
        "password": "",
        "autoReconnect": true,
        "autoReconnectIntervalMs": 5000
    }
}
```

## Device Configuration

```json
{
    "key": "switcher-1",
    "name": "AVPro Edge AC-MX-88",
    "type": "acmx8x8",
    "group": "switcher",
    "properties": {
        "control": {
            "method": "tcpIp",            
            "tcpSshProperties": {
                "address": "0.0.0.0",
                "port": 23,
                "username": "",
                "password": "",
                "autoReconnect": true,
                "autoReconnectIntervalMs": 5000
            }
        },
        "pollTime": 60000,
        "noRouteText": "None",
        "inputNames": {
            "1": "Input 1",
            "2": "Input 2",
            "3": "Input 3",
            "4": "Input 4",
            "5": "Input 5",
            "6": "Input 6",
            "7": "Input 7",
            "8": "Input 8"
        },
        "outputNames": {
            "1": "Output 1",
            "2": "Output 2",
            "3": "Output 3",
            "4": "Output 4",
            "5": "Output 5",
            "6": "Output 6",
            "7": "Output 7",
            "8": "Output 8"
        }
    }
}
```
<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 2.12.4
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "GeneratedKey",
    "uid": 1,
    "name": "GeneratedName",
    "type": "acmx16x16",
    "group": "Group",
    "properties": {
        "control": "SampleValue",
        "PollTimeMs": 0,
        "inputNames": {
            "SampleValue": "SampleString"
        },
        "outputNames": {
            "SampleValue": "SampleString"
        },
        "noRouteText": "SampleString"
    }
}
```
<!-- END Config Example -->
<!-- START Supported Types -->
### Supported Types

- acmx16x16
- acmx8x8
<!-- END Supported Types -->
<!-- START Join Maps -->

<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- IMatrixRouting
- IRoutingWithFeedback
- ICommunicationMonitor
- IDeviceInfoProvider
- IRoutingOutputSlot
- IRoutingInputSlot
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- DmChassisControllerJoinMap
- EssentialsBridgeableDevice
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void SendText(string text)
- public void Route(string inputSlotKey, string outputSlotKey, eRoutingSignalType type)
- public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
- public void UpdateDeviceInfo()
- public void SetInputRoute(eRoutingSignalType type, IRoutingInputSlot input)
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- ConnectFeedback
- OnlineFeedback
- IsOnline
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->
### Int Feedbacks

- StatusFeedback
<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->

<!-- END String Feedbacks -->
