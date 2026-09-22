from pydantic import BaseModel
from datetime import datetime
from typing import Dict, Optional

class IngestPayload(BaseModel):
    crew_id: int                                   # id entier, ex. 1 (= Crew01)
    timestamp: datetime              # temps mission (simule)
    metrics: Dict[str, float]
    event: Optional[dict] = None
