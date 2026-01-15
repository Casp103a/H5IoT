# H5IoT - Smart Building Management System

A comprehensive IoT solution for smart building automation featuring RFID access control, environmental monitoring, automated climate control, and security management. Built with Arduino devices, an MQTT broker backend, and a WPF user registration application.

## 🏗️ Project Architecture

### Components Overview

```
H5IoT/
├── ArduinoCode/           # IoT Device Firmware
│   ├── ArduinoDoorlock/   # Smart door lock with RFID access
│   ├── ArduinoFan/        # Automated fan control system
│   ├── ArduinoKeypad/     # PIN entry system with LCD
│   ├── MqttRfid/          # RFID card reader
│   └── MqttTemp/          # Temperature & humidity sensor
├── MqttBroker/            # .NET Core MQTT broker & API
└── UserRegistaionApp/     # WPF user management application
```

## 🚀 Features

### 🔐 Access Control System
- **RFID Card Authentication**: Contactless access using RFID cards
- **PIN-based Entry**: Backup access via keypad entry
- **Smart Door Lock**: Automated door control with LED status indicators
- **User Management**: Complete user registration and card assignment
- **Time-based Access**: Working hours enforcement (8:00-18:00)
- **Security Alerts**: Real-time notifications for security events

### 🌡️ Environmental Monitoring
- **Temperature Monitoring**: Real-time temperature tracking with DHT22 sensor
- **Humidity Control**: Automated humidity management
- **Smart Ventilation**: Automatic fan control based on environmental conditions
- **Configurable Thresholds**: Customizable temperature and humidity limits
- **Environmental Alerts**: Notifications when conditions exceed safe ranges

### 📱 Management & Monitoring
- **MQTT Communication**: Reliable device-to-server communication
- **Real-time Alerts**: Instant notifications for all system events
- **User Registration**: Easy user onboarding via WPF application
- **Configuration Management**: Remote device configuration
- **Docker Deployment**: Containerized broker for easy deployment

## 🛠️ Technology Stack

### Hardware
- **Arduino** boards with WiFi capability
- **MFRC522** RFID reader modules
- **DHT22** temperature and humidity sensors
- **RGB LCD** displays for user feedback
- **LED indicators** and **relays** for door control
- **4x4 Keypad** for PIN entry

### Software
- **C++/Arduino** - Device firmware
- **C# .NET 8** - MQTT broker and services
- **WPF** - User registration application
- **MQTT Protocol** - Device communication
- **JSON** - Data serialization
- **SQLite** - Local data storage
- **Docker** - Containerized deployment

## 📡 MQTT Topics

| Topic | Purpose | Publisher | Subscriber |
|-------|---------|-----------|------------|
| `hansenberg/card` | RFID card readings | RFID Reader | MQTT Broker |
| `hansenberg/card/deactivate` | Card deactivation | RFID Reader | MQTT Broker |
| `hansenberg/pin` | PIN entry attempts | Keypad | MQTT Broker |
| `hansenberg/door/unlock` | Door unlock commands | MQTT Broker | Door Lock |
| `hansenberg/dht` | Temperature/humidity data | Temp Sensor | MQTT Broker, Fan Controller |
| `hansenberg/alert` | System alerts | All Devices | MQTT Broker |
| `hansenberg/configuration/*` | Device configurations | MQTT Broker | All Devices |
| `hansenberg/system/time/*` | Time synchronization | MQTT Broker | All Devices |

## 🔧 Device Specifications

### Door Lock Controller
- **Features**: RFID authentication, manual unlock button, LED status indicators, LCD display
- **Security**: Working hours enforcement, automatic re-lock (15s), door-open alarm (30s)
- **Communication**: WiFi + MQTT, time synchronization
- **Hardware**: Arduino, RGB LCD, LEDs (Red/Green/Blue), Push button

### RFID Card Reader
- **Features**: RFID card scanning, card deactivation mode, visual feedback
- **Hardware**: Arduino, MFRC522 reader, LEDs (Red/Blue), Push button
- **Communication**: MQTT publishing, unique device identification

### Environmental Sensor
- **Features**: Temperature & humidity monitoring, configurable thresholds
- **Hardware**: Arduino, DHT22 sensor, LED indicator, Push button
- **Data**: Automatic publishing every 10 seconds, alert generation

### Smart Fan Controller
- **Features**: Automatic fan control, temperature-based activation, safety timeout
- **Hardware**: Arduino, Relay for fan control, Push button
- **Logic**: Fan activation based on temperature/humidity thresholds, maximum runtime protection

### PIN Entry System
- **Features**: 4x4 keypad input, LCD display, PIN verification
- **Hardware**: Arduino, 4x4 Keypad, RGB LCD display
- **Security**: PIN-based access control integration

## 🏃‍♂️ Getting Started

### Prerequisites
- Arduino IDE or PlatformIO
- .NET 8 SDK
- Visual Studio (for WPF app)
- Docker (for broker deployment)

### Hardware Setup
1. Connect RFID readers to designated Arduino pins
2. Wire DHT22 sensors for environmental monitoring
3. Set up door lock mechanism with relay control
4. Configure keypad and LCD display connections
5. Ensure all devices connect to the same WiFi network (`Hansenberg` in our case)

### Software Setup

#### 1. Arduino Devices
# Install PlatformIO dependencies
Using platformIO build it.
Using platformIO Upload to the desired device.

#### 2. MQTT Broker
Get the code onto your linux server (raspberry pi 4)
Create a folder "/home/{username}/myapp/data"
Then open cmd and goto the codes folder using cd to the folder "MqttBroker".
Then write "docker compose up --build".
Done.

#### 3. User Registration App
Open this on a pc connected to the same internet as your broker.
Fill out the text boxs.

## 📊 Data Models

### User Model
```csharp
public class UserModel
{
    public string CardID { get; set; }
    public string Pin { get; set; }
    public string UserName { get; set; }
    public DateTime? ExpireDate { get; set; }
    public bool IsActive { get; set; }
}
```

### Alert Model
```csharp
public class AlertModel
{
    public int ID { get; set; }
    public DateTime Timestamp { get; set; }
    public string Message { get; set; }
    public string DeviceID { get; set; }
    public string AlertType { get; set; }
}
```

## 🔒 Security Features
- **Encrypted User Data**: Secure storage of user credentials
- **Working Hours Enforcement**: Time-based access control (8:00-18:00)
- **Automatic Door Lock**: 15-second automatic re-lock
- **Intrusion Detection**: Door-open alarm after 30 seconds
- **Card Deactivation**: Remote card disabling capability
- **Alert System**: Real-time security event notifications

## 🌡️ Environmental Controls
- **Configurable Thresholds**: Customizable temperature and humidity limits
- **Automatic Fan Control**: Climate-responsive ventilation
- **Safety Timeouts**: Maximum fan runtime protection (10 seconds)
- **Real-time Monitoring**: Continuous environmental data collection
- **Alert Generation**: Notifications for out-of-range conditions

## 🔧 Configuration Management
- **Remote Configuration**: MQTT-based device configuration updates
- **Threshold Management**: Dynamic environmental limit adjustments
- **Device Identification**: Unique ID-based device management
- **Service Discovery**: Automatic device registration and discovery

## 📈 Monitoring & Alerts
- **Real-time Dashboard**: Live system status monitoring
- **Alert Categories**: Security, Environmental, System alerts
- **Device Status**: Online/offline device monitoring
- **Historical Data**: Event logging and historical analysis