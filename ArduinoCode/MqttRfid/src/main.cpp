#include <Arduino.h>
#include <WiFiNINA.h>
#include <Wire.h>
#include "MFRC522_I2C.h"
#include <ArduinoUniqueID.h>
#include <ArduinoJson.h>
#include <MQTT.h>
#include <MQTTClient.h>

#define RST_PIN 6 // Arduino UNO Pin

const char *WIFI_SSID = "Hansenberg";
const char *WIFI_PASSWORD = "NogetJegKanHuske";
const char *SERVER_ADDRESS = "192.168.10.50";
const int SERVER_PORT = 1883;

String MqttClientId = "";       // Will be set to unique ID in setup
const char MqttUsername[] = ""; // Change if it's required, empty if it's not required
const char MqttPassword[] = ""; // Change if it's required, empty if it's not required
const char PublishTopic[] = "hansenberg/card";

long LastPublish = 0;
int TimeBetweenEachPublish = 2500;
int BlueLedOnTime = 2000;

const int Red_Led_Pin = 4;
const int Blue_Led_Pin = 8;

// 0x28 is i2c address on SDA. Check your address with i2cscanner if not match.
MFRC522_I2C mfrc522(0x28, RST_PIN); // Create MFRC522 instance.
WiFiClient client;
MQTTClient mqtt = MQTTClient(256);

bool connectToWiFi();
bool ConnectToMqtt();
void ShowReaderDetails();
bool SendCardUID(char *cardUID);

void setup()
{
  // put your setup code here, to run once:
  Serial.begin(9600);

  pinMode(Blue_Led_Pin, OUTPUT);
  pinMode(Red_Led_Pin, OUTPUT);

  Wire.begin(); // Initialize I2C
  mfrc522.PCD_Init();

  // Generate unique MQTT client ID
  for (size_t i = 0; i < UniqueIDsize; i++)
  {
    if (UniqueID[i] < 0x10)
    {
      MqttClientId += "0";
    }
    MqttClientId += String(UniqueID[i], HEX);
  }
  MqttClientId.toUpperCase();

  ShowReaderDetails();
  bool connected = connectToWiFi();
  bool mqttConneted = ConnectToMqtt();

  if (!connected || !mqttConneted)
  {
    Serial.println("Failed To Connect");
    digitalWrite(Red_Led_Pin, HIGH);
    return;
  }
}

void loop()
{
  long currentTime = millis();
  if (currentTime - LastPublish < BlueLedOnTime)
  {
    digitalWrite(Blue_Led_Pin, HIGH);
    digitalWrite(Red_Led_Pin, LOW);
  }
  else
  {
    digitalWrite(Blue_Led_Pin, LOW);
  }

  // put your main code here, to run repeatedly:
  if (!mfrc522.PICC_IsNewCardPresent() || !mfrc522.PICC_ReadCardSerial())
  {
    delay(50);
    return;
  }

  char cardUID[21] = "";

  Serial.print(F("Card UID:"));
  for (byte i = 0; i < mfrc522.uid.size; i++)
  {
    Serial.print(mfrc522.uid.uidByte[i] < 0x10 ? " 0" : " ");
    Serial.print(mfrc522.uid.uidByte[i], HEX);

    char hexstring[3];
    sprintf(hexstring, "%02X", mfrc522.uid.uidByte[i]);

    strcat(cardUID, hexstring);
  }
  Serial.println();

  SendCardUID(cardUID);
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

void ShowReaderDetails()
{
  Serial.println("Trying to get reader details.");
  // Get the MFRC522 software version
  byte v = mfrc522.PCD_ReadRegister(mfrc522.VersionReg);
  Serial.print(F("MFRC522 Software Version: 0x"));
  Serial.print(v, HEX);
  if (v == 0x91)
    Serial.print(F(" = v1.0"));
  else if (v == 0x92)
    Serial.print(F(" = v2.0"));
  else
    Serial.print(F(" (unknown)"));
  Serial.println("");
  // When 0x00 or 0xFF is returned, communication probably failed
  if ((v == 0x00) || (v == 0xFF))
  {
    Serial.println(F("WARNING: Communication failure, is the MFRC522 properly connected?"));
  }
}

bool ConnectToMqtt()
{
  mqtt.begin(SERVER_ADDRESS, SERVER_PORT, client);

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

bool SendCardUID(char *cardUID)
{
  long currentTime = millis();
  if (currentTime - LastPublish < TimeBetweenEachPublish)
  {
    return false;
  }

  DynamicJsonDocument message(200);
  message["CardID"] = cardUID;
  message["UniqueID"] = MqttClientId;

  char messageBuffer[512];
  serializeJson(message, messageBuffer);

  Serial.println("Publishing messages");
  bool success = mqtt.publish(PublishTopic, messageBuffer);

  if (!success)
  {
    Serial.println("MQTT publish failed!");
    digitalWrite(Red_Led_Pin, HIGH);
    return false;
  }

  Serial.println("Published messages");
  LastPublish = millis();
}
