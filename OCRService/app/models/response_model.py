"""
=============================================================================
RESPONSE MODELS - PYDANTIC DATA MODELS
=============================================================================

WWH EXPLANATION FOR response_model.py:

WHAT:
    Pydantic models define the structure of data our API sends and receives.
    They are like "blueprints" for our JSON responses.

WHY:
    - Automatic data validation (catches errors before they cause problems)
    - Automatic JSON serialization (converts Python objects to JSON)
    - Auto-generated API documentation (FastAPI uses these for /docs)
    - Type safety (IDE can catch errors while coding)

HOW:
    1. Inherit from BaseModel
    2. Define fields with type hints
    3. Optionally add Field() for extra validation
    4. FastAPI automatically uses these for request/response handling

=============================================================================
"""

from pydantic import BaseModel, Field
from typing import Optional, List
from enum import Enum


# =============================================================================
# RESPONSE STATUS ENUM
# =============================================================================
# WWH:
# WHAT: Enum defines a fixed set of possible values
# WHY: Prevents typos and invalid status values
# HOW: Inherit from str and Enum to make it JSON-serializable

class ResponseStatus(str, Enum):
    SUCCESS = "success"
    ERROR = "error"
    PARTIAL = "partial"  # Some text extracted but with warnings


# =============================================================================
# OCR TEXT RESPONSE MODEL
# =============================================================================
# WWH:
# WHAT: The main response model for successful OCR operations
# WHY: Provides a consistent structure for the .NET backend to parse
# HOW: Define fields that FastAPI will automatically convert to JSON

class OCRTextResponse(BaseModel):
    """
    Standard response for successful text extraction.
    
    Example:
    {
        "success": true,
        "extracted_text": "Patient Name: John Doe...",
        "confidence": 0.95,
        "word_count": 25
    }
    """
    
    success: bool = Field(
        default=True,
        description="Whether the OCR operation was successful"
    )
    
    extracted_text: str = Field(
        ...,  # '...' means this field is required
        description="The text extracted from the image"
    )
    
    confidence: Optional[float] = Field(
        default=None,
        ge=0.0,  # Greater than or equal to 0
        le=1.0,  # Less than or equal to 1
        description="Average confidence score of the OCR (0-1)"
    )
    
    word_count: Optional[int] = Field(
        default=None,
        ge=0,
        description="Number of words extracted"
    )
    
    # Configuration for the model
    class Config:
        json_schema_extra = {
            "example": {
                "success": True,
                "extracted_text": "Patient Name: John Doe\nAge: 45\nMedicine: Paracetamol 500mg",
                "confidence": 0.92,
                "word_count": 8
            }
        }


# =============================================================================
# DETAILED OCR RESPONSE MODEL
# =============================================================================
# WWH:
# WHAT: A more detailed response including line-by-line OCR results
# WHY: Some applications need per-line confidence scores
# HOW: Include a list of text lines with their individual scores

class TextLine(BaseModel):
    """Represents a single line of extracted text with its confidence."""
    
    text: str = Field(..., description="The extracted text line")
    confidence: float = Field(..., ge=0, le=1, description="Confidence for this line")
    bounding_box: Optional[List[List[float]]] = Field(
        default=None,
        description="Coordinates of text location [[x1,y1], [x2,y2], [x3,y3], [x4,y4]]"
    )


class DetailedOCRResponse(BaseModel):
    """
    Detailed response with line-by-line OCR results.
    Use this when you need position information or per-line confidence.
    """
    
    success: bool = Field(default=True)
    extracted_text: str = Field(..., description="Full combined text")
    lines: List[TextLine] = Field(default=[], description="Individual text lines")
    average_confidence: float = Field(..., ge=0, le=1)
    total_lines: int = Field(..., ge=0)


# =============================================================================
# ERROR RESPONSE MODEL
# =============================================================================
# WWH:
# WHAT: Standardized error response structure
# WHY: Makes error handling consistent and predictable for clients
# HOW: Include error type, message, and optional details

class ErrorResponse(BaseModel):
    """
    Standard error response.
    
    Example:
    {
        "success": false,
        "error": "INVALID_FILE_TYPE",
        "message": "Only image files are supported",
        "details": "Received file type: application/pdf"
    }
    """
    
    success: bool = Field(default=False)
    
    error: str = Field(
        ...,
        description="Error code (e.g., INVALID_FILE_TYPE, NO_FILE, OCR_FAILED)"
    )
    
    message: str = Field(
        ...,
        description="Human-readable error message"
    )
    
    details: Optional[str] = Field(
        default=None,
        description="Additional error details for debugging"
    )
    
    class Config:
        json_schema_extra = {
            "example": {
                "success": False,
                "error": "INVALID_FILE_TYPE",
                "message": "Unsupported file type. Please upload an image.",
                "details": "Supported formats: jpg, jpeg, png, bmp, tiff"
            }
        }


# =============================================================================
# HEALTH CHECK RESPONSE MODEL
# =============================================================================

class HealthResponse(BaseModel):
    """Health check response structure."""
    
    status: str = Field(..., description="Service status: healthy, degraded, unhealthy")
    ocr_model_loaded: bool = Field(..., description="Whether OCR model is ready")
    message: str = Field(..., description="Status message")
