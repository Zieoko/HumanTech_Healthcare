from sqlalchemy import Column, Integer, String, Float, DateTime, ForeignKey, func
from sqlalchemy.dialects.postgresql import JSONB
from .database import Base

class CrewMember(Base):
    __tablename__ = "crew_members"
    id = Column(Integer, primary_key=True, autoincrement=True)
    name = Column(String, nullable=False)
    role = Column(String)
    mission_start = Column(DateTime(timezone=True))

class Baseline(Base):
    __tablename__ = "baselines"
    id = Column(Integer, primary_key=True)
    crew_id = Column(Integer, ForeignKey("crew_members.id"))
    indicator = Column(String)                     # heart_rate_rest, spo2, ...
    value = Column(Float)
    recorded_at = Column(DateTime(timezone=True))

class Measurement(Base):
    __tablename__ = "measurements"
    id = Column(Integer, primary_key=True)
    crew_id = Column(Integer, ForeignKey("crew_members.id"))
    indicator = Column(String)
    value = Column(Float)
    mission_time = Column(DateTime(timezone=True))          # temps SIMULE (mission)
    received_at = Column(DateTime(timezone=True), server_default=func.now())  # temps reel

class Threshold(Base):
    __tablename__ = "thresholds"
    id = Column(Integer, primary_key=True)
    crew_id = Column(Integer, ForeignKey("crew_members.id"))
    indicator = Column(String)
    alert_delta = Column(Float)                    # ecart max tolere avant alerte
    critical_delta = Column(Float)                 # ecart max avant critique
    max_slope = Column(Float)                      # pente max tolerable (%/mois)

class Event(Base):
    __tablename__ = "events"
    id = Column(Integer, primary_key=True)
    crew_id = Column(Integer, ForeignKey("crew_members.id"))
    type = Column(String)                          # injury, fatigue, equipment_failure...
    payload = Column(JSONB)
    mission_time = Column(DateTime(timezone=True))

class Alert(Base):
    __tablename__ = "alerts"
    id = Column(Integer, primary_key=True)
    crew_id = Column(Integer, ForeignKey("crew_members.id"))
    indicator = Column(String)
    level = Column(String)                         # alert | critical | tendance
    message = Column(String)
    created_at = Column(DateTime(timezone=True), server_default=func.now())
