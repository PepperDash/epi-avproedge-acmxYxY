![PepperDash Essentials Pluign Logo](/images/essentials-plugin-blue.png)

# Essentials Plugin for AVProEdge ACMX Series Matrix Switchers (c) 2025

## License

Provided under MIT license

## Overview

Provides routing control for the AVProEdge ACMX Series Matrix Switchers via RS232 or TCP.

## Communication Methods

***RS-232***

```json
"control": {
    "method": "com",
    "controlPortDevKey": "processor",
    "controlPortNumber": 1,
    "comParams": {
        "protocol": "RS232",
        "baudRate": 9600,
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