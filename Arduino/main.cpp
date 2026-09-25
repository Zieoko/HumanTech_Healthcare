#include <OneWire.h>
#include <DallasTemperature.h>

#define ONE_WIRE_BUS 2 // Pin du capteur (fil jaune sur D2)

// ---- Moyenne glissante ----
const int N = 10;
float buffer[N];
int index = 0;

float moyenne(float nouvelleValeur)
{
  buffer[index] = nouvelleValeur;
  index = (index + 1) % N;

  float somme = 0;
  for (int i = 0; i < N; i++)
  {
    somme += buffer[i];
  }

  return somme / N;
}
// ---------------------------

OneWire oneWire(ONE_WIRE_BUS);
DallasTemperature sensors(&oneWire);

void setup()
{
  Serial.begin(9600);
  sensors.begin();
}

void loop()
{
  sensors.requestTemperatures();
  float temp = sensors.getTempCByIndex(0);

  // Calcul de la moyenne
  float tempMoyenne = moyenne(temp);

  // Création du JSON
  String json = "{";
  json += "\"temperature\": " + String(temp);
  json += ", \"moyenne\": " + String(tempMoyenne);
  json += "}";

  Serial.println(json);

  delay(1000);
}
