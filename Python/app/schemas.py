from pydantic import BaseModel
from datetime import datetime
from typing import Dict, Optional

class IngestPayload(BaseModel):
    crew_id: int                                   # id entier, ex. 1 (= Crew01)
    timestamp: datetime              # temps mission (simule)
    metrics: Dict[str, float]
    event: Optional[dict] = None

class EventPayload(BaseModel):
    crew_id: int
    type: str                                  # injury, fatigue, illness, equipment_failure, workload
    timestamp: datetime                        # temps mission (simule)
    payload: Optional[dict] = None             # details libres : {"location": "genou", ...}

class ChatPayload(BaseModel):
    message: str
    history: Optional[list] = None          # [{"role": "user"|"assistant", "content": "..."}]
