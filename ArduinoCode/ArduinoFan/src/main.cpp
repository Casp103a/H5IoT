#include <Arduino.h>
#include <ArduinoUniqueID.h>
#include <ArduinoJson.h>
#include <MQTT.h>
#include <MQTTClient.h>
#include <WiFiNINA.h>

const char *WIFI_SSID = "Hansenberg";
const char *WIFI_PASSWORD = "NogetJegKanHuske";
const char *SERVER_ADDRESS = "192.168.10.50";
const int SERVER_PORT = 1883;

String MqttClientId = "";       // Will be set to unique ID in setup
const char MqttUsername[] = ""; // Change if it's required, empty if it's not required
const char MqttPassword[] = ""; // Change if it's required, empty if it's not required
const char PublishAlertTopic[] = "hansenberg/alert";
const char GetFanConfigTopic[] = "hansenberg/configuration/fan/get";
const char SubscribeFanConfigTopic[] = "hansenberg/configuration/fan";
const char SubscribeTopic[] = "hansenberg/dht";

WiFiClient client;
MQTTClient mqtt = MQTTClient(512);

const int BUTTON_PIN = 4;

const int FAN_PIN = 8;

// Config
long LastAlertPublish;
int TimeBetweenEachPublish = 10000;
int MaxAllowedTemperatur;
int MaxAllowedHumidity;
int MaxFanRunTime = 10000;

bool GotFirstConfig = false;
bool FanRunning = false;
long FanStartTime = 0;

bool connectToWiFi();
bool ConnectToMqtt();
void CheckTemp(float temp);
void CheckHumidity(float humidity);
void SendAlert(String alertMessage, String alertType);
void GetConfiguration();
void OnMessagedRecived(String &topic, String &payload);

void setup()
{
  // put your setup code here, to run once:
  Serial.begin(9600);

  pinMode(BUTTON_PIN, INPUT);
  pinMode(FAN_PIN, OUTPUT);

  for (size_t i = 0; i < UniqueIDsize; i++)
  {
    if (UniqueID[i] < 0x10)
    {
      MqttClientId += "0";
    }
    MqttClientId += String(UniqueID[i], HEX);
  }
  MqttClientId.toUpperCase();

  bool connected = connectToWiFi();
  bool mqttConneted = ConnectToMqtt();

  if (!connected || !mqttConneted)
  {
    // Serial.println("Failed To Connect");
    return;
  }
}

void loop()
{
  mqtt.loop();

  if (!mqtt.connected())
  {
    ConnectToMqtt();
  }

  if (digitalRead(BUTTON_PIN) == HIGH || !GotFirstConfig)
  {
    GetConfiguration();
    GotFirstConfig = true;
  }

  // Turn off fan after MaxFanRunTime
  if (FanRunning && (millis() - FanStartTime > MaxFanRunTime))
  {
    digitalWrite(FAN_PIN, LOW);
    FanRunning = false;
    // Serial.println("Fan stopped due to timeout");
  }
}

bool connectToWiFi()
{
  // Serial.print("Connecting to WiFi");
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  unsigned long startAttempt = millis();
  while (WiFi.status() != WL_CONNECTED)
  {
    // Serial.print(".");
    delay(500);

    if (millis() - startAttempt > 10000)
    {
      // Serial.println("\nFailed to connect. Resetting...");
      delay(2000);
      return false;
    }
  }
  // Serial.println("\nWiFi Connected");
  return true;
}

bool ConnectToMqtt()
{
  mqtt.begin(SERVER_ADDRESS, SERVER_PORT, client);

  mqtt.onMessage(OnMessagedRecived);

  int attempts = 0;
  while (!mqtt.connect(MqttClientId.c_str(), MqttUsername, MqttPassword) && attempts < 10)
  {
    // Serial.print(".");
    attempts++;
    delay(1000);
  }
  // Serial.println();

  if (!mqtt.connected())
  {
    // Serial.println("Arduino - MQTT broker connection failed!");
    // Serial.print("Connection attempts: ");
    // Serial.println(attempts);
    return false;
  }

  mqtt.subscribe(SubscribeTopic);

  return true;
}

void CheckTemp(float temp)
{
  if (temp >= MaxAllowedTemperatur)
  {
    SendAlert("Temperature exceeded: " + String(temp), "TemperatureThreshold");
    if (!FanRunning)
    {
      digitalWrite(FAN_PIN, HIGH);
      FanRunning = true;
      FanStartTime = millis();
      SendAlert("Fan activated due to high temperature: " + String(temp), "FanActivation");
      // Serial.println("Fan started due to high temperature");
    }
  }
}

void CheckHumidity(float humidity)
{
  if (humidity >= MaxAllowedHumidity)
  {
    SendAlert("Humidity exceeded: " + String(humidity), "HumidityThreshold");
    if (!FanRunning)
    {
      digitalWrite(FAN_PIN, HIGH);
      FanRunning = true;
      FanStartTime = millis();
      SendAlert("Fan activated due to high humidity: " + String(humidity), "FanActivation");
      // Serial.println("Fan started due to high humidity");
    }
  }
}

void SendAlert(String alertMessage, String alertType)
{
  DynamicJsonDocument message(200);
  message["Message"] = alertMessage;
  message["DeviceID"] = MqttClientId;
  message["AlertType"] = alertType;

  char messageBuffer[512];
  serializeJson(message, messageBuffer);

  // Serial.println("Publishing messages");
  bool success = mqtt.publish(PublishAlertTopic, messageBuffer);

  if (!success)
  {
    // Serial.println("MQTT publish failed!");
    return;
  }

  // Serial.println("Published messages");
  LastAlertPublish = millis();
}

void GetConfiguration()
{
  mqtt.subscribe(SubscribeFanConfigTopic);

  bool success = mqtt.publish(GetFanConfigTopic, "Get config");

  // if (success)
  // {
  //   Serial.println("successed asking for config");
  // }
  // else
  // {
  //   Serial.println("failed asking for config");
  // }
}

void OnMessagedRecived(String &topic, String &payload)
{
  // Serial.println("got here");

  if (topic == SubscribeFanConfigTopic)
  {
    // Serial.println("got config");
    StaticJsonDocument<256> doc;
    DeserializationError err = deserializeJson(doc, payload);

    MaxFanRunTime = doc["TimeFanIsTurnedOn"];
    MaxAllowedTemperatur = doc["MaxAllowedTemperatur"];
    MaxAllowedHumidity = doc["MaxAllowedHumidity"];

    // Serial.println(MaxFanRunTime);
    // Serial.println(MaxAllowedHumidity);
    // Serial.println(MaxAllowedTemperatur);

    // Serial.println("Config is set");
    mqtt.unsubscribe(SubscribeFanConfigTopic);
  }
  else if (topic == SubscribeTopic)
  {
    StaticJsonDocument<256> doc;
    DeserializationError err = deserializeJson(doc, payload);

    if (!err)
    {
      float temperature = doc["Temp"];
      float humidity = doc["Humidity"];

      CheckTemp(temperature);
      CheckHumidity(humidity);
    }
  }
}