#include <Arduino.h>
#include <Wire.h>
#include <math.h>
#include <ArduinoUniqueID.h>
#include <MQTT.h>
#include <MQTTClient.h>
#include <WiFiNINA.h>
#include <ArduinoJson.h>
#include <Keypad.h>
#include <rgb_lcd.h>

const char WifiName[] = "Hansenberg";
 
const char WifiPassword[] = "NogetJegKanHuske";

const char MqttBrokerIP[] = "192.168.10.50";
const char MqttPort[] = "1883";
String MqttClientId = ""; // Will be set to unique ID in setup
const char MqttUsername[] = ""; // Change if it's required, empty if it's not required
const char MqttPassword[] = ""; // Change if it's required, empty if it's not required

const char PublishTopic[] = "hansenberg/test/1"; // Can be changed
const char SubscribeTopic[] = ""; // Can be changed

const int PublishInterval = 5000; // 5 seconds

WiFiClient network;
MQTTClient mqtt = MQTTClient(256);

rgb_lcd lcd;

unsigned long lastPublishTime = 0;
String currentPin = ""; // Store current PIN being entered
const int maxPinLength = 10; // Maximum PIN length

char keys[4][4] = {
  {'1','2','3','A'},
  {'4','5','6','B'},
  {'7','8','9','C'},
  {'0','F','E','D'}
};

byte rowPins[4] = {2, 3, 4, 5}; 
byte colPins[4] = {6, 7, 8, 9};


Keypad keypad = Keypad( makeKeymap(keys), rowPins, colPins, 4, 4 );

// Function declarations
void connectToMQTT();
void sendPinToMQTT(String pin);
void messageHandler(String &topic, String &payload);

void setup() {
  Serial.begin(9600);
  
  // Generate unique MQTT client ID
  for (size_t i = 0; i < UniqueIDsize; i++) {
    if (UniqueID[i] < 0x10) {
      MqttClientId += "0";
    }
    MqttClientId += String(UniqueID[i], HEX);
  }
  MqttClientId.toUpperCase();
  
  Serial.println("Device Unique ID: " + MqttClientId);
  
  // Initialize LCD
  lcd.begin(16, 2);
  lcd.setRGB(0, 255, 0); // Green backlight
  lcd.clear();
  lcd.print("PIN: ");

  int status = WL_IDLE_STATUS;
  while (status != WL_CONNECTED) {
    Serial.print("Arduino - Attempting to connect to SSID: ");
    Serial.println(WifiName);
    // Connect to WPA/WPA2 network. Change this line if using open or WEP network:
    status = WiFi.begin(WifiName, WifiPassword);

    // wait 10 seconds for connection:
    delay(10000);
  }
  // print your board's IP address:
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  connectToMQTT();
}

void loop() {
  mqtt.loop();

  // Check MQTT connection and reconnect if needed
  if (!mqtt.connected()) {
    Serial.println("MQTT connection lost. Reconnecting...");
    connectToMQTT();
  }

  // Check for keypad presses
  char key = keypad.getKey();
  if (key) {
    if (key == 'E') {
      // Send PIN if there's something to send
      if (currentPin.length() > 0) {
        sendPinToMQTT(currentPin);
        Serial.println("Sent PIN: " + currentPin);
        
        // Clear the PIN after sending
        currentPin = "";
        
        // Update display
        lcd.clear();
        lcd.print("PIN: ");
        
        // Show confirmation briefly
        lcd.setCursor(0, 1);
        lcd.print("SENT!");
        delay(1000);
        
        // Clear confirmation and return to PIN entry
        lcd.setCursor(0, 1);
        lcd.print("     ");
      }
    } else if (key == 'C') {
      // Clear entire PIN
      currentPin = "";
      lcd.clear();
      lcd.print("PIN: ");
      Serial.println("PIN cleared");
    } else if (key == 'D') {
      // Delete last character (backspace)
      if (currentPin.length() > 0) {
        currentPin = currentPin.substring(0, currentPin.length() - 1);
        
        // Update display
        lcd.clear();
        lcd.print("PIN: " + currentPin);
        
        Serial.println("Delete - PIN now: " + currentPin);
      }
    } else if (key >= '0' && key <= '9') {
      // Add only numbers to PIN (0-9)
      if (currentPin.length() < maxPinLength) {
        currentPin += key;
        
        // Update LCD display
        lcd.clear();
        lcd.print("PIN: " + currentPin);
        
        Serial.println("Added: " + String(key) + " - PIN: " + currentPin);
      } else {
        Serial.println("PIN too long! Press C to clear.");
      }
    }
    // A, B, F do nothing (ignored)
  }
}

void connectToMQTT() {
  // Connect to the MQTT broker
  mqtt.begin(MqttBrokerIP, atoi(MqttPort), network);

  // Create a handler for incoming messages
  mqtt.onMessage(messageHandler);

  Serial.print("Arduino - Connecting to MQTT broker at ");
  Serial.print(MqttBrokerIP);
  Serial.print(":");
  Serial.println(MqttPort);

  int attempts = 0;
  while (!mqtt.connect(MqttClientId.c_str(), MqttUsername, MqttPassword) && attempts < 10) {
    Serial.print(".");
    attempts++;
    delay(1000);
  }
  Serial.println();

  if (!mqtt.connected()) {
    Serial.println("Arduino - MQTT broker connection failed!");
    Serial.print("Connection attempts: ");
    Serial.println(attempts);
    return;
  }

  // Only subscribe if SubscribeTopic is not empty
  if (strlen(SubscribeTopic) > 0) {
    if (mqtt.subscribe(SubscribeTopic))
      Serial.print("Arduino - Subscribed to the topic: ");
    else
      Serial.print("Arduino - Failed to subscribe to the topic: ");
    Serial.println(SubscribeTopic);
  }
  
  Serial.println("Arduino - MQTT broker Connected successfully!");
  Serial.print("Client ID: ");
  Serial.println(MqttClientId);
  Serial.print("Publish topic: ");
  Serial.println(PublishTopic);
}

void sendPinToMQTT(String pin) {
  DynamicJsonDocument message(200);
  message["Pin"] = pin;
  message["UniqueID"] = MqttClientId;
  
  char messageBuffer[512];
  serializeJson(message, messageBuffer);

  bool success = mqtt.publish(PublishTopic, messageBuffer);
  
  if (!success) {
    Serial.println("MQTT publish failed!");
  }
}

void messageHandler(String &topic, String &payload) {
  Serial.println("Arduino - received from MQTT:");
  Serial.println("- topic: " + topic);
  Serial.println("- payload:");
  Serial.println(payload);
}