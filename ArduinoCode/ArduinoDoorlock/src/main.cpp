#include <Arduino.h>
#include <Wire.h>
#include <math.h>
#include <time.h>
#include <ArduinoUniqueID.h>
#include <MQTT.h>
#include <MQTTClient.h>
#include <WiFiNINA.h>
#include <ArduinoJson.h>
#include <rgb_lcd.h>

// Hardware pin definitions
#define RED_LED_PIN 3
#define GREEN_LED_PIN 2
#define BLUE_LED_PIN 8
#define DOOR_BUTTON_PIN 4

// WiFi Configuration
const char WIFI_SSID[] = "Hansenberg";
const char WIFI_PASSWORD[] = "NogetJegKanHuske";

// MQTT Configuration
const char MQTT_BROKER_IP[] = "192.168.10.50";
const int MQTT_PORT = 1883;
String mqttClientId = ""; // Will be set to unique ID in setup
const char MQTT_USERNAME[] = ""; // Change if required
const char MQTT_PASSWORD[] = ""; // Change if required

// MQTT Topics
const char DOOR_UNLOCK_TOPIC[] = "hansenberg/door/unlock";
const char TIME_RESPONSE_TOPIC[] = "hansenberg/system/time";
const char TIME_REQUEST_TOPIC[] = "hansenberg/system/time/get";

WiFiClient network;
MQTTClient mqtt(256);

rgb_lcd lcd;

// Timing constants (in milliseconds)
const unsigned long TIME_REQUEST_INTERVAL = 3600000; // Request time every hour
const unsigned long DOOR_OPEN_ALARM_TIME = 30000; // 30 seconds before door alarm
const unsigned long AUTO_LOCK_DURATION = 15000; // 15 seconds unlock duration
const unsigned long MESSAGE_INTERVAL = 30000; // 30 seconds between status messages
const unsigned long TRIPLE_CLICK_WINDOW = 2000; // 2 seconds for triple-click detection
const unsigned long ALARM_FLASH_INTERVAL = 500; // Flash every 500ms

// Working hours configuration
const int WORK_START_HOUR = 8;  // 8:00
const int WORK_END_HOUR = 18;   // 18:00

// Time synchronization state
struct tm timeInfo;
bool isTimeValid = false;
unsigned long lastTimeRequest = 0;

// Working hours and alarm state
bool isWorkingHours = false;
bool outsideHoursAlarm = false;

// Door state management
bool isDoorLocked = true;
bool isMqttUnlockAuthorized = false; // Button only works after MQTT unlock
unsigned long doorUnlockTime = 0; // When door was unlocked
unsigned long lastStatusMessageTime = 0;

// Button state tracking
bool isButtonPressed = false;
bool lastButtonState = HIGH; // The button is not pressed
bool isDoorHeldOpen = false;
unsigned long buttonPressTime = 0;
unsigned long doorHeldOpenStartTime = 0;

// Triple-click detection for special case
int buttonClickCount = 0;
unsigned long firstClickTime = 0;

// Alarm state management
bool isAlarmActive = false;
bool isSpecialCaseActive = false; // Bypass time restrictions
unsigned long lastAlarmFlashTime = 0;
bool alarmFlashState = false;
String currentAlarmReason = "";

// Function declarations
void connectWiFi();
void connectMQTT();
void onMqttMessage(String &topic, String &payload);
void handleDoorUnlockCommand();
void setDoorState(bool locked);
void requestTime();
void parseTimeFromMqtt(String &payload);
void parseTime(const String &payload);
void applyTimezoneConversion(const String &payload);
void checkWorkingHours();
void checkTimeBasedAlarms();
String getCurrentTimeString();
void toggleSpecialCase();
void resetToNormalOperation();

// Helper functions for main loop
void handleConnections();
void handleButtonInput();
void handleTripleClickDetection();
void handleDoorAutoLock();
void handleAlarmDisplay();
void processButtonPress(bool currentButtonState, unsigned long currentTime);
void processButtonRelease(unsigned long currentTime);

void setup() {
  Serial.begin(9600);
  while (!Serial) delay(10);
  
  // Initialize LEDs
  pinMode(RED_LED_PIN, OUTPUT);
  pinMode(GREEN_LED_PIN, OUTPUT);
  pinMode(BLUE_LED_PIN, OUTPUT);
  
  // Turn on all LEDs to show system is initializing
  digitalWrite(RED_LED_PIN, HIGH);
  digitalWrite(GREEN_LED_PIN, HIGH);
  digitalWrite(BLUE_LED_PIN, HIGH);
  
  // Initialize button
  pinMode(DOOR_BUTTON_PIN, INPUT_PULLUP);
  
  // Initialize LCD
  lcd.begin(16, 2);
  lcd.setRGB(255, 255, 255); // White backlight
  lcd.clear();
  lcd.print("Door Lock System");
  lcd.setCursor(0, 1);
  lcd.print("Initializing...");
  
  // Generate unique client ID
  for (size_t i = 0; i < UniqueIDsize; i++) {
    mqttClientId += String(UniqueID[i], HEX);
  }
  
  // Serial.println("Starting Door Lock System...");
  // Serial.print("Client ID: ");
  // Serial.println(mqttClientId);
  
  // Connect to WiFi
  connectWiFi();
  
  // Connect to MQTT
  connectMQTT();
  
  // Subscribe to topics
  mqtt.subscribe(DOOR_UNLOCK_TOPIC);
  mqtt.subscribe(TIME_RESPONSE_TOPIC);
  // Serial.print("Subscribed to: ");
  // Serial.println(DOOR_UNLOCK_TOPIC);
  // Serial.print("Subscribed to: ");
  // Serial.println(TIME_RESPONSE_TOPIC);
  
  // Request initial time
  delay(1000); // Give MQTT time to settle
  requestTime();
  
  // Set MQTT message callback
  mqtt.onMessage(onMqttMessage);
    // System is ready - set normal door state (locked = red LED only)
  setDoorState(true);
    // Serial.println("Door Lock System ready!");
}

void loop() {
  unsigned long currentTime = millis();
  
  // Maintain connections and request time periodically
  handleConnections();
  
  // Request time periodically
  if (currentTime - lastTimeRequest > TIME_REQUEST_INTERVAL) {
    requestTime();
  }
  
  // Check working hours and time-based alarms
  checkWorkingHours();
  checkTimeBasedAlarms();
  
  // Handle button input and triple-click detection
  handleButtonInput();
  
  // Handle auto-lock functionality
  handleDoorAutoLock();
  
  // Handle alarm display and LED flashing
  handleAlarmDisplay();
  
  // Send periodic status updates
  if (currentTime - lastStatusMessageTime > MESSAGE_INTERVAL) {
    lastStatusMessageTime = currentTime;
    // Could add status message publishing here if needed
  }
  
  delay(100);
}

void connectWiFi() {
  // Serial.print("Connecting to WiFi: ");
  // Serial.println(WIFI_SSID);
  
  lcd.clear();
  lcd.print("Connecting WiFi");
  
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    // Serial.print(".");
  }
  
  // Serial.println();
  // Serial.println("WiFi connected!");
  // Serial.print("IP address: ");
  // Serial.println(WiFi.localIP());
  
  lcd.clear();
  lcd.print("WiFi Connected");
  lcd.setCursor(0, 1);
  lcd.print(WiFi.localIP());
  delay(2000);
}

void connectMQTT() {
  // Serial.print("Connecting to MQTT broker: ");
  // Serial.println(MQTT_BROKER_IP);
  
  lcd.clear();
  lcd.print("Connecting MQTT");
  
  mqtt.begin(MQTT_BROKER_IP, MQTT_PORT, network);
  
  while (!mqtt.connect(mqttClientId.c_str(), MQTT_USERNAME, MQTT_PASSWORD)) {
    // Serial.print(".");
    delay(1000);
  }
  
  // Serial.println();
  // Serial.println("MQTT connected!");
  
  lcd.clear();
  lcd.print("MQTT Connected");
  delay(1000);
}

void onMqttMessage(String &topic, String &payload) {
  // Serial.print("Received message on topic: ");
  // Serial.println(topic);
  // Serial.print("Payload: ");
  // Serial.println(payload);
  
  // Handle time updates
  if (topic == TIME_RESPONSE_TOPIC) {
    parseTimeFromMqtt(payload);
    return;
  }
  
  // Handle door unlock command
  if (topic == DOOR_UNLOCK_TOPIC) {
    handleDoorUnlockCommand();
  }
}

void handleDoorUnlockCommand() {
  // Only process MQTT unlock if door is not being held open manually
  if (!isDoorHeldOpen) {
    isMqttUnlockAuthorized = true; // Authorize button usage
    setDoorState(false);
    doorUnlockTime = millis(); // Record when door was unlocked
    // Serial.println("Door unlocked via MQTT - Button now authorized");
    // Serial.print("Will auto-lock in ");
    // Serial.print(AUTO_LOCK_DURATION / 1000);
    // Serial.println(" seconds");
    
    // Check if this unlock is outside working hours
    if (!isWorkingHours && !isSpecialCaseActive) {
      outsideHoursAlarm = true;
      isAlarmActive = true;
      currentAlarmReason = "Access off hours";
      // Serial.println("Outside hours access detected!");
    }
  } else {
    // Serial.println("MQTT unlock ignored - door is being held open manually");
  }
}

void setDoorState(bool locked) {
  bool stateChanged = (isDoorLocked != locked);
  isDoorLocked = locked;
  
  // Always set the LED state for door status (red=locked, green=unlocked)
  if (locked) {
    // Door locked: Red LED on, Green LED off
    digitalWrite(RED_LED_PIN, HIGH);
    digitalWrite(GREEN_LED_PIN, LOW);
    // if (stateChanged) {
    //   Serial.println("Door state: LOCKED (Red LED ON)");
    // }
  } else {
    // Door unlocked: Green LED on, Red LED off  
    digitalWrite(GREEN_LED_PIN, HIGH);
    digitalWrite(RED_LED_PIN, LOW);
    // if (stateChanged) {
    //   Serial.println("Door state: UNLOCKED (Green LED ON)");
    // }
  }
}

void requestTime() {
  // Serial.println("Requesting time via MQTT...");
  lastTimeRequest = millis();
  
  // Publish a request to the time/get topic to trigger a time response
  // The server should respond on hansenberg/system/time with current time
  mqtt.publish(TIME_REQUEST_TOPIC, "request");
}

void parseTimeFromMqtt(String &payload) {
  // Parse time from MQTT payload - supports ISO 8601, Unix timestamp, and time-only formats
  
  if (payload.length() == 0) {
    // Serial.println("Empty time payload received");
    return;
  }
  
  // Serial.print("Parsing time payload: ");
  // Serial.println(payload);
  
  // Try to parse as ISO 8601 format first (from C# DateTime.ToString("o"))
  if (payload.indexOf("T") >= 0) {
    parseTime(payload);
    return;
  }
  
  // Serial.print("Failed to parse time format: ");
  // Serial.println(payload);
}

void parseTime(const String &payload) {
  int year, month, day, hour, minute, second;
  if (sscanf(payload.c_str(), "%d-%d-%dT%d:%d:%d", &year, &month, &day, &hour, &minute, &second) == 6) {
    timeInfo.tm_year = year - 1900; // tm_year is years since 1900
    timeInfo.tm_mon = month - 1;    // tm_mon is 0-11
    timeInfo.tm_mday = day;
    timeInfo.tm_hour = hour;
    timeInfo.tm_min = minute;
    timeInfo.tm_sec = second;
    timeInfo.tm_wday = 0;
    timeInfo.tm_yday = 0;
    timeInfo.tm_isdst = 0;
    
    // Apply timezone conversion if needed
    applyTimezoneConversion(payload);
    
    isTimeValid = true;
    // Serial.print("Time updated via MQTT (ISO 8601): ");
    // Serial.print(getCurrentTimeString());
    // Serial.print(" on ");
    // Serial.print(year);
    // Serial.print("-");
    // Serial.print(month);
    // Serial.print("-");
    // Serial.println(day);
  }
}

void applyTimezoneConversion(const String &payload) {
  // Check if we need to apply timezone offset
  if (payload.indexOf("Z") >= 0 || payload.indexOf("+00:00") >= 0) {
    // UTC time - add 1 hour for CET
    timeInfo.tm_hour += 1;
    if (timeInfo.tm_hour >= 24) {
      timeInfo.tm_hour -= 24;
      timeInfo.tm_mday += 1;
    }
    // Serial.print(" (converted from UTC to CET)");
  } else {
    // No timezone info - assume UTC and convert to CET
    timeInfo.tm_hour += 1;
    if (timeInfo.tm_hour >= 24) {
      timeInfo.tm_hour -= 24;
      timeInfo.tm_mday += 1;
    }
    // Serial.print(" (assumed UTC, converted to CET)");
  }
}

void checkWorkingHours() {
  if (!isTimeValid) return;
  
  int currentHour = timeInfo.tm_hour;
  bool wasWorkingHours = isWorkingHours;
  isWorkingHours = (currentHour >= WORK_START_HOUR && currentHour < WORK_END_HOUR);
  
  // if (wasWorkingHours != isWorkingHours) {
  //   Serial.print("Working hours changed: ");
  //   Serial.print(isWorkingHours ? "Now in working hours" : "Now outside working hours");
  //   Serial.print(" (Current hour: ");
  //   Serial.print(currentHour);
  //   Serial.print(", Work hours: ");
  //   Serial.print(WORK_START_HOUR);
  //   Serial.print("-");
  //   Serial.print(WORK_END_HOUR);
  //   Serial.println(")");
  // }
}

void checkTimeBasedAlarms() {
  if (!isTimeValid || isSpecialCaseActive) return;
  
  // Check for activity outside working hours
  if (!isWorkingHours && (!isDoorLocked || isDoorHeldOpen)) {
    if (!outsideHoursAlarm) {
      outsideHoursAlarm = true;
      isAlarmActive = true;
      currentAlarmReason = "Off hours access";
      // Serial.print("ALARM: Activity outside working hours! Current time: ");
      // Serial.print(getCurrentTimeString());
      // Serial.print(", Working hours: ");
      // Serial.print(WORK_START_HOUR);
      // Serial.print(":00 - ");
      // Serial.print(WORK_END_HOUR);
      // Serial.println(":00");
    }
  }
}

String getCurrentTimeString() {
  if (!isTimeValid) return "Time N/A";
  
  char timeStr[20];
  sprintf(timeStr, "%02d:%02d:%02d", timeInfo.tm_hour, timeInfo.tm_min, timeInfo.tm_sec);
  return String(timeStr);
}

// Helper function implementations

void handleConnections() {
  // Keep MQTT connection alive
  mqtt.loop();
  
  // Check WiFi connection
  if (WiFi.status() != WL_CONNECTED) {
    // Serial.println("WiFi disconnected, reconnecting...");
    connectWiFi();
  }
  
  // Check MQTT connection
  if (!mqtt.connected()) {
    // Serial.println("MQTT disconnected, reconnecting...");
    connectMQTT();
  }
}

void handleButtonInput() {
  unsigned long currentTime = millis();
  bool currentButtonState = digitalRead(DOOR_BUTTON_PIN);
  
  // Update door held open state based on button state
  // HIGH = button pressed, LOW = button released
  if (currentButtonState == HIGH) {
    if (!isDoorHeldOpen && isMqttUnlockAuthorized) {
      isDoorHeldOpen = true;
      doorHeldOpenStartTime = currentTime;
      // Serial.println("Door being held open");
    }
  } else {
    if (isDoorHeldOpen) {
      isDoorHeldOpen = false;
      // Clear alarm when button is released
      if (currentAlarmReason == "Door held open") {
        isAlarmActive = false;
        currentAlarmReason = "";
        // Serial.println("Button released - Alarm cleared");
      }
    }
  }
  
  // Process button press and release events
  processButtonPress(currentButtonState, currentTime);
  processButtonRelease(currentTime);
  
  // Handle triple-click timeout
  handleTripleClickDetection();
  
  // Check for door held open too long (alarm condition)
  if (isDoorHeldOpen && !isSpecialCaseActive && (currentTime - doorHeldOpenStartTime > DOOR_OPEN_ALARM_TIME)) {
    if (!isAlarmActive) {
      isAlarmActive = true;
      currentAlarmReason = "Door open too long";
      // Serial.println("ALARM: Door has been held open too long!");
    }
  }
  
  lastButtonState = currentButtonState;
}

void processButtonPress(bool currentButtonState, unsigned long currentTime) {
  // Detect button press (transition from LOW to HIGH)
  if (lastButtonState == LOW && currentButtonState == HIGH) {
    if (isMqttUnlockAuthorized) {
      isButtonPressed = true;
      buttonPressTime = currentTime;
      setDoorState(false); // Unlock door when button pressed
      // Serial.println("Button pressed - Door held open");
    } //else {
      // Serial.println("Button press ignored - MQTT unlock required first");
    //}
  }
}

void processButtonRelease(unsigned long currentTime) {
  bool currentButtonState = digitalRead(DOOR_BUTTON_PIN);
  
  // Detect button release (transition from HIGH to LOW)
  if (lastButtonState == HIGH && currentButtonState == LOW && isButtonPressed) {
    isButtonPressed = false;
    
    // Handle triple-click for special case
    if (buttonClickCount == 0) {
      buttonClickCount = 1;
      firstClickTime = currentTime;
      // Serial.println("Click 1/3 for special case");
    } else if (buttonClickCount == 1 && (currentTime - firstClickTime < TRIPLE_CLICK_WINDOW)) {
      buttonClickCount = 2;
      // Serial.println("Click 2/3 for special case");
    } else if (buttonClickCount == 2 && (currentTime - firstClickTime < TRIPLE_CLICK_WINDOW)) {
      // Third click - toggle special case!
      buttonClickCount = 0;
      toggleSpecialCase();
    } else {
      // Normal button release - lock door
      buttonClickCount = 0;
      resetToNormalOperation();
    }
  }
}

void toggleSpecialCase() {
  if (!isSpecialCaseActive) {
    // Activate special case
    isSpecialCaseActive = true;
    isAlarmActive = false;
    outsideHoursAlarm = false;
    currentAlarmReason = "";
    // Serial.println("SPECIAL CASE ACTIVATED - All time restrictions bypassed");
    delay(2000);
  } else {
    // Deactivate special case
    isSpecialCaseActive = false;
    // Serial.println("SPECIAL CASE DEACTIVATED - Normal operation resumed");
    delay(2000);
    // Return to normal locked state
    setDoorState(true);
    isMqttUnlockAuthorized = false;
  }
}

void resetToNormalOperation() {
  isAlarmActive = false;
  outsideHoursAlarm = false;
  isSpecialCaseActive = false;
  currentAlarmReason = "";
  isMqttUnlockAuthorized = false;
  setDoorState(true);
  // Serial.println("Button released - Door locked, MQTT auth reset");
}

void handleTripleClickDetection() {
  unsigned long currentTime = millis();
  // Reset click count if too much time has passed
  if (buttonClickCount > 0 && (currentTime - firstClickTime > TRIPLE_CLICK_WINDOW)) {
    buttonClickCount = 0;
    // Serial.println("Triple-click timeout - count reset");
  }
}

void handleDoorAutoLock() {
  unsigned long currentTime = millis();
  
  // Auto-lock after timeout - happens regardless of button state
  if (!isDoorLocked && !isSpecialCaseActive && (currentTime - doorUnlockTime > AUTO_LOCK_DURATION)) {
    isMqttUnlockAuthorized = false; // Reset MQTT authorization on auto-lock
    setDoorState(true); // Auto-lock
    // Serial.println("Auto-lock activated after timeout, MQTT auth reset");
    
    // If door is still being held open after auto-lock, trigger alarm immediately
    if (isDoorHeldOpen) {
      isAlarmActive = true;
      currentAlarmReason = "Door held open";
      // Serial.println("ALARM: Door held open after auto-lock timeout!");
      // Serial.print("Button state: ");
      // Serial.println(digitalRead(DOOR_BUTTON_PIN) == HIGH ? "PRESSED" : "RELEASED");
    }
  }
}

void handleAlarmDisplay() {
  unsigned long currentTime = millis();
  
  // Handle blue LED for alarm - only flash for door held open after auto-lock
  if (isAlarmActive && !isSpecialCaseActive && currentAlarmReason == "Door held open" && 
      (currentTime - lastAlarmFlashTime > ALARM_FLASH_INTERVAL)) {
    lastAlarmFlashTime = currentTime;
    alarmFlashState = !alarmFlashState;
    digitalWrite(BLUE_LED_PIN, alarmFlashState ? HIGH : LOW);

  } else if (!isAlarmActive || isSpecialCaseActive || currentAlarmReason != "Door held open") {
    // Turn off blue LED when no door held alarm or in special case
    digitalWrite(BLUE_LED_PIN, LOW);
  }
}