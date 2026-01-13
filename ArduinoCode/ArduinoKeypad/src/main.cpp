#include <Arduino.h>
#include <Wire.h>
#include <ArduinoUniqueID.h>
#include <MQTT.h>
#include <MQTTClient.h>
#include <WiFiNINA.h>
#include <ArduinoJson.h>
#include <Keypad.h>
#include <rgb_lcd.h>

// WiFi configuration
const char WIFI_SSID[] = "Hansenberg";
const char WIFI_PASSWORD[] = "NogetJegKanHuske";

// MQTT configuration
const char MQTT_BROKER_IP[] = "192.168.10.50";
const int MQTT_PORT = 1883;
String mqttClientId = ""; // Will be set to unique ID in setup
const char MQTT_USERNAME[] = ""; // Change if required, empty if not required
const char MQTT_PASSWORD[] = ""; // Change if required, empty if not required

const char PUBLISH_TOPIC[] = "hansenberg/pin"; // Can be changed
const char SUBSCRIBE_TOPIC[] = ""; // Can be changed

// Network clients
WiFiClient network;
MQTTClient mqtt(256);

// LCD display
rgb_lcd lcd;

// PIN entry state
String currentPin = ""; // Store current PIN being entered
const uint8_t maxPinLength = 10; // Maximum PIN length
unsigned long lastMessageTime = 0;

// Keypad configuration
char keys[4][4] = {
  {'1','2','3','A'},
  {'4','5','6','B'},
  {'7','8','9','C'},
  {'0','F','E','D'}
};

byte rowPins[4] = {2, 3, 4, 5}; 
byte colPins[4] = {6, 7, 8, 9};

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, 4, 4);

// Function declarations
void connectToWiFi();
void connectToMQTT();
void sendPinToMQTT(const String& pin);
void messageHandler(String &topic, String &payload);
void updateDisplay(const String& message, bool showOnLine2 = false);

void setup() {
  Serial.begin(9600);
  
  // Initialize LCD
  lcd.begin(16, 2);
  lcd.setRGB(0, 255, 0); // Green backlight
  updateDisplay("Initializing...");
  delay(1000);
  
  // Generate unique MQTT client ID
  for (size_t i = 0; i < UniqueIDsize; i++) {
    if (UniqueID[i] < 0x10) {
      mqttClientId += "0";
    }
    mqttClientId += String(UniqueID[i], HEX);
  }
  mqttClientId.toUpperCase();
  
  Serial.println("Device Unique ID: " + mqttClientId);
  
  // Connect to WiFi
  connectToWiFi();
  
  // Connect to MQTT
  connectToMQTT();
  
  // Ready for PIN entry
  updateDisplay("PIN: ");
}

void loop() {
  mqtt.loop();

  // Check MQTT connection and reconnect if needed
  if (!mqtt.connected()) {
    Serial.println("MQTT connection lost. Reconnecting...");
    updateDisplay("MQTT reconnect", true);
    connectToMQTT();
    if (mqtt.connected()) {
      updateDisplay("PIN: ");
    }
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
        updateDisplay("PIN: ");
        
        // Show confirmation briefly
        updateDisplay("SENT!", true);
        delay(1000);
        
        // Return to PIN entry
        updateDisplay("PIN: ");
      }
    } else if (key == 'C') {
      // Clear entire PIN
      currentPin = "";
      updateDisplay("PIN: ");
      Serial.println("PIN cleared");
    } else if (key == 'D') {
      // Delete last character (backspace)
      if (currentPin.length() > 0) {
        currentPin = currentPin.substring(0, currentPin.length() - 1);
        updateDisplay("PIN: " + currentPin);
        Serial.println("Delete - PIN now: " + currentPin);
      }
    } else if (key >= '0' && key <= '9') {
      // Add only numbers to PIN (0-9)
      if (currentPin.length() < maxPinLength) {
        currentPin += key;
        updateDisplay("PIN: " + currentPin);
        Serial.println("Added: " + String(key) + " - PIN: " + currentPin);
      } else {
        Serial.println("PIN too long! Press C to clear.");
        updateDisplay("PIN too long!", true);
        delay(1500);
        updateDisplay("PIN: " + currentPin);
      }
    }
  }
}

void connectToWiFi() {
  updateDisplay("Connecting WiFi");
  
  int status = WL_IDLE_STATUS;
  int attempts = 0;
  const int maxAttempts = 5;
  
  while (status != WL_CONNECTED && attempts < maxAttempts) {
    Serial.print("Arduino - Attempting to connect to SSID: ");
    Serial.println(WIFI_SSID);
    
    updateDisplay("WiFi attempt " + String(attempts + 1), true);
    
    // Connect to WPA/WPA2 network
    status = WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
    
    // Wait for connection
    for (int i = 0; i < 10 && status != WL_CONNECTED; i++) {
      delay(1000);
      status = WiFi.status();
    }
    
    attempts++;
  }
  
  if (status == WL_CONNECTED) {
    Serial.print("IP Address: ");
    Serial.println(WiFi.localIP());
    updateDisplay("WiFi Connected!");
    delay(1500);
  } else {
    Serial.println("WiFi connection failed!");
    updateDisplay("WiFi Failed!", true);
    delay(2000);
  }
}

void connectToMQTT() {
  updateDisplay("Connecting MQTT");
  
  // Connect to the MQTT broker
  mqtt.begin(MQTT_BROKER_IP, MQTT_PORT, network);

  // Create a handler for incoming messages
  mqtt.onMessage(messageHandler);

  Serial.print("Arduino - Connecting to MQTT broker at ");
  Serial.print(MQTT_BROKER_IP);
  Serial.print(":");
  Serial.println(MQTT_PORT);

  int attempts = 0;
  const int maxAttempts = 10;
  
  while (!mqtt.connected() && attempts < maxAttempts) {
    updateDisplay("MQTT attempt " + String(attempts + 1), true);
    
    if (mqtt.connect(mqttClientId.c_str(), MQTT_USERNAME, MQTT_PASSWORD)) {
      break;
    }
    
    Serial.print(".");
    attempts++;
    delay(1000);
  }
  Serial.println();

  if (!mqtt.connected()) {
    Serial.println("Arduino - MQTT broker connection failed!");
    Serial.print("Connection attempts: ");
    Serial.println(attempts);
    updateDisplay("MQTT Failed!", true);
    delay(2000);
    return;
  }

  // Only subscribe if SUBSCRIBE_TOPIC is not empty
  if (strlen(SUBSCRIBE_TOPIC) > 0) {
    if (mqtt.subscribe(SUBSCRIBE_TOPIC)) {
      Serial.print("Arduino - Subscribed to the topic: ");
    } else {
      Serial.print("Arduino - Failed to subscribe to the topic: ");
    }
    Serial.println(SUBSCRIBE_TOPIC);
  }
  
  Serial.println("Arduino - MQTT broker Connected successfully!");
  Serial.print("Client ID: ");
  Serial.println(mqttClientId);
  Serial.print("Publish topic: ");
  Serial.println(PUBLISH_TOPIC);
  
  updateDisplay("MQTT Connected!");
  delay(1500);
}

void sendPinToMQTT(const String& pin) {
  DynamicJsonDocument message(200);
  message["Pin"] = pin;
  message["UniqueID"] = mqttClientId;
  
  char messageBuffer[256]; // Reduced buffer size
  serializeJson(message, messageBuffer);

  bool success = mqtt.publish(PUBLISH_TOPIC, messageBuffer);
  
  if (!success) {
    Serial.println("MQTT publish failed!");
    updateDisplay("Send failed!", true);
    delay(1500);
  }
}

void messageHandler(String &topic, String &payload) {
  Serial.println("Arduino - received from MQTT:");
  Serial.println("- topic: " + topic);
  Serial.println("- payload:");
  Serial.println(payload);
}

void updateDisplay(const String& message, bool showOnLine2) {
  if (!showOnLine2) {
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print(message);
  } else {
    lcd.setCursor(0, 1);
    // Clear line 2 first
    lcd.print("                "); // 16 spaces
    lcd.setCursor(0, 1);
    lcd.print(message);
  }
}