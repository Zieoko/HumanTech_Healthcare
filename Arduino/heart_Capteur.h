#ifndef HEART_CAPTEUR_H
#define HEART_CAPTEUR_H
#include "MAX30105.h"

extern MAX30105 capteur;
void initHeartSensor();
int readHeartBPM();

#endif
