#include <Wire.h>
#include "MAX30105.h"
#include "heartRate.h"
#include <Arduino.h>

MAX30105 capteur;
bool heartSensorAvailable = false;

long lastBeat = 0;
int dernierBPM = 0;

void initHeartSensor()
{
  Wire.begin();

  if (!capteur.begin(Wire, I2C_SPEED_FAST))
  {
    Serial.println("Erreur : MAX30102 non détecté !");
    return;
  }

  heartSensorAvailable = true;
  Serial.println("MAX30102 détecté.");

  // Configuration du capteur
  capteur.setup();

  capteur.setPulseAmplitudeRed(0x7F);
  capteur.setPulseAmplitudeIR(0x7F);
}

int readHeartBPM()
{
  if (!heartSensorAvailable)
  {
    return -2;
  }

  long irValue = capteur.getIR();

  // Limiter les valeurs IR trop élevés (évite le blocage)
  if (irValue > 300000)
  {
    irValue = 300000;
  }

  // Détection du doigt
  if (irValue < 20000)
  {
    dernierBPM = 0;
    lastBeat = 0;
    return -1;
  }

  // Détection du battement
  if (checkForBeat(irValue))
  {
    unsigned long maintenant = millis();

    if (lastBeat == 0)
    {
      lastBeat = maintenant;
      return 0;
    }

    long delta = maintenant - lastBeat;
    lastBeat = maintenant;

    if (delta > 0)
    {
      float bpm = 60.0 / (delta / 1000.0);

      if (bpm >= 40 && bpm <= 200)
      {
        dernierBPM = (int)bpm;
      }
    }
  }

  return dernierBPM;
}
