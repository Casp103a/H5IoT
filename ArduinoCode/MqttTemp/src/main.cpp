#include <Arduino.h>
#include <DHT.h>
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
const char PublishTempTopic[] = "hansenberg/temp";
const char PublishHumidityTopic[] = "hansenberg/humidity";
const char GetTempConfigTopic[] = "hansenberg/configuration/dht/get";
const char SubscribeTopic[] = "hansenberg/configuration/dht";

WiFiClient client;
MQTTClient mqtt = MQTTClient(512);

const int Red_Led_Pin = 4;
const int Button_Pin = 5;

const int DHT_PIN = 2;
const int DHT_TYPE = DHT22;

// Config
long LastTempPublish;
long LastHumidityPublish;
int TimeBetweenEachPublish = 10000;
int MaxAllowedTemperatur;
int MaxAllowedHumidity;

bool GotFirstConfig = false;

DHT dht(DHT_PIN, DHT_TYPE);

bool connectToWiFi();
bool ConnectToMqtt();
void HandleTemp();
void CheckTemp(float temp);
void SendTemp(float temp);
void HandleHumidity();
void CheckHumidity(float humidity);
void SendHumidity(float humidity);
void GetConfiguration();
void OnMessagedRecived(String &topic, String &payload);

void setup()
{
  // put your setup code here, to run once:
  Serial.begin(9600);

  pinMode(Red_Led_Pin, OUTPUT);
  pinMode(Button_Pin, INPUT);

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
    Serial.println("Failed To Connect");
    digitalWrite(Red_Led_Pin, HIGH);
    return;
  }

  dht.begin();
}

void loop()
{
  mqtt.loop();

  if (!mqtt.connected())
  {
    ConnectToMqtt();
  }

  if (digitalRead(Button_Pin) == HIGH || !GotFirstConfig)
  {
    GetConfiguration();
    GotFirstConfig = true;
  }

  HandleTemp();
  HandleHumidity();
}

bool connectToWiFi()
{
  Serial.print("Connecting to WiFi");
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  unsigned long startAttempt = millis();
  while (WiFi.status() != WL_CONNECTED)
  {
    Serial.print(".");
    delay(500);

    if (millis() - startAttempt > 10000)
    {
      Serial.println("\nFailed to connect. Resetting...");
      delay(2000);
      return false;
    }
  }
  Serial.println("\nWiFi Connected");
  return true;
}

bool ConnectToMqtt()
{
  mqtt.begin(SERVER_ADDRESS, SERVER_PORT, client);

  mqtt.onMessage(OnMessagedRecived);

  int attempts = 0;
  while (!mqtt.connect(MqttClientId.c_str(), MqttUsername, MqttPassword) && attempts < 10)
  {
    Serial.print(".");
    attempts++;
    delay(1000);
  }
  Serial.println();

  if (!mqtt.connected())
  {
    Serial.println("Arduino - MQTT broker connection failed!");
    Serial.print("Connection attempts: ");
    Serial.println(attempts);
    return false;
  }

  return true;
}

void HandleTemp()
{
  float temp = dht.readTemperature();

  if (isnan(temp))
  {
    return;
  }

  CheckTemp(temp);

  long currentTime = millis();
  if (currentTime - LastTempPublish < TimeBetweenEachPublish)
  {
    return;
  }

  SendTemp(temp);
}

int timeBetweenEachCheck = 2000;
int maxReadingsOverMax = 8;
long timeSinceLastTempCheck = 0;
long timeSinceLastHumidityCheck = 0;
int amountOfReadingOverMaxTemp;
int amountOfReadingOverMaxHumidity;

void CheckTemp(float temp)
{
  if(temp > MaxAllowedTemperatur)
  {
    long currentTime = millis();
    if(currentTime - timeSinceLastTempCheck > timeBetweenEachCheck)
    {
      amountOfReadingOverMaxTemp++;
      timeSinceLastTempCheck = currentTime;
    }

    if(amountOfReadingOverMaxTemp > maxReadingsOverMax)
    {
      SendTemp(temp);
    }
  }
}

void SendTemp(float temp)
{
  DynamicJsonDocument message(200);
  message["Temp"] = temp;
  message["UniqueID"] = MqttClientId;

  char messageBuffer[512];
  serializeJson(message, messageBuffer);

  Serial.println("Publishing messages");
  bool success = mqtt.publish(PublishTempTopic, messageBuffer);

  if (!success)
  {
    Serial.println("MQTT publish failed!");
    digitalWrite(Red_Led_Pin, HIGH);
    return;
  }

  Serial.println("Published messages");
  LastTempPublish = millis();
  amountOfReadingOverMaxTemp = 0;
}

void HandleHumidity()
{
  float humidity = dht.readHumidity();

  if (isnan(humidity))
  {
    return;
  }

  CheckHumidity(humidity);

  long currentTime = millis();
  if (currentTime - LastHumidityPublish < TimeBetweenEachPublish)
  {
    return;
  }

  SendHumidity(humidity);
}

void CheckHumidity(float humidity)
{
  if(humidity > MaxAllowedHumidity)
  {
    long currentTime = millis();
    if(currentTime - timeSinceLastHumidityCheck > timeBetweenEachCheck)
    {
      amountOfReadingOverMaxHumidity++;
      timeSinceLastHumidityCheck = currentTime;
    }

    if(amountOfReadingOverMaxHumidity > maxReadingsOverMax)
    {
      SendHumidity(humidity);
    }
  }
}

void SendHumidity(float humidity)
{
  DynamicJsonDocument message(200);
  message["Humidity"] = humidity;
  message["UniqueID"] = MqttClientId;

  char messageBuffer[512];
  serializeJson(message, messageBuffer);

  Serial.println("Publishing messages");
  bool success = mqtt.publish(PublishHumidityTopic, messageBuffer);

  if (!success)
  {
    Serial.println("MQTT publish failed!");
    digitalWrite(Red_Led_Pin, HIGH);
    return;
  }

  Serial.println("Published messages");
  LastHumidityPublish = millis();
  amountOfReadingOverMaxTemp = 0;
}

void GetConfiguration()
{
  mqtt.subscribe(SubscribeTopic);

  bool success = mqtt.publish(GetTempConfigTopic, "Get config");

  if (success)
  {
    Serial.println("successed asking for config");
  }
  else
  {
    Serial.println("failed asking for config");
  }
}

void OnMessagedRecived(String &topic, String &payload)
{
    Serial.println("got here");

  if (topic == SubscribeTopic)
  {
    Serial.println("got config");

    StaticJsonDocument<256> doc;
    DeserializationError err = deserializeJson(doc, payload);

    TimeBetweenEachPublish = doc["TimeBetweenSample"];
    MaxAllowedTemperatur = doc["MaxAllowedTemperatur"];
    MaxAllowedHumidity = doc["MaxAllowedHumidity"];

    Serial.println("Config is set");
    mqtt.unsubscribe(SubscribeTopic);
  }
}