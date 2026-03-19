"""
=============================================================================
OCR ROUTES - API ENDPOINTS
=============================================================================

WWH EXPLANATION FOR ocr_routes.py:

WHAT:
    This file defines the API endpoints (routes) for our OCR service.
    It's where HTTP requests come in and get processed.

WHY:
    - Separation of concerns: Routes handle HTTP, services handle logic
    - Organization: All OCR-related endpoints in one place
    - FastAPI Router: Allows grouping endpoints under a common prefix

HOW:
    1. Create an APIRouter instance
    2. Define endpoint functions with decorators (@router.post, etc.)
    3. Use dependency injection to access the OCR service
    4. Handle file uploads, validation, and response formatting

=============================================================================
"""

from fastapi import APIRouter, UploadFile, File, HTTPException, Request, Depends
from fastapi.responses import JSONResponse
from typing import Optional
import logging
import os

from models.response_model import (
    OCRTextResponse, 
    DetailedOCRResponse, 
    ErrorResponse,
    TextLine
)
from services.ocr_service import OCRService

logger = logging.getLogger(__name__)


# =============================================================================
# CREATE API ROUTER
# =============================================================================
# WWH:
# WHAT: APIRouter is like a "mini FastAPI app" for organizing endpoints
# WHY: Keeps related endpoints together, can be mounted with a prefix
# HOW: Create router, add endpoints to it, include in main app

router = APIRouter()


# =============================================================================
# DEPENDENCY: GET OCR SERVICE
# =============================================================================
# WWH:
# WHAT: A function that provides the OCR service to route handlers
# WHY: Dependency injection makes testing easier and code cleaner
# HOW: FastAPI calls this automatically when route needs OCRService

async def get_ocr_service(request: Request) -> OCRService:
    """
    Get the OCR service instance from the application state.
    
    This is called automatically by FastAPI when an endpoint
    has 'ocr_service: OCRService = Depends(get_ocr_service)'.
    
    Args:
        request: The incoming HTTP request (provides access to app.state)
        
    Returns:
        The initialized OCR service
        
    Raises:
        HTTPException: If OCR service is not available
    """
    ocr_service = getattr(request.app.state, 'ocr_service', None)
    
    if ocr_service is None:
        logger.error("OCR service not initialized")
        raise HTTPException(
            status_code=503,  # Service Unavailable
            detail="OCR service is not available. Please try again later."
        )
    
    return ocr_service


# =============================================================================
# HELPER: VALIDATE FILE UPLOAD
# =============================================================================

def validate_file_upload(file: UploadFile) -> Optional[str]:
    """
    Validate the uploaded file.
    
    Args:
        file: The uploaded file
        
    Returns:
        Error message if invalid, None if valid
    """
    # Check if file was provided
    if file is None or file.filename is None or file.filename == "":
        return "No file uploaded. Please provide an image file."
    
    # Check file extension
    ext = os.path.splitext(file.filename)[1].lower()
    supported = OCRService.SUPPORTED_FORMATS
    
    if ext not in supported:
        return f"Unsupported file type '{ext}'. Supported formats: {', '.join(supported)}"
    
    # Check content type (MIME type)
    if file.content_type:
        valid_content_types = {
            'image/jpeg', 'image/png', 'image/bmp', 
            'image/tiff', 'image/jpg'
        }
        if not file.content_type.startswith('image/'):
            return f"Invalid content type '{file.content_type}'. Please upload an image file."
    
    return None  # Valid


# =============================================================================
# ENDPOINT: EXTRACT TEXT (SIMPLE RESPONSE)
# =============================================================================
# WWH:
# WHAT: Main endpoint that accepts image upload and returns extracted text
# WHY: This is what the .NET backend will call to process images
# HOW: 
#   1. Receive file upload
#   2. Validate the file
#   3. Read and process with OCR service
#   4. Return JSON response

@router.post(
    "/extract-text",
    response_model=OCRTextResponse,
    responses={
        200: {
            "description": "Text extracted successfully",
            "model": OCRTextResponse
        },
        400: {
            "description": "Invalid request (bad file, unsupported format)",
            "model": ErrorResponse
        },
        500: {
            "description": "OCR processing failed",
            "model": ErrorResponse
        }
    },
    summary="Extract text from an image",
    description="""
    Upload an image file to extract text using OCR.
    
    **Supported formats:** jpg, jpeg, png, bmp, tiff
    
    **How it works:**
    1. The image is preprocessed (grayscale, noise reduction, contrast enhancement)
    2. PaddleOCR detects text regions in the image
    3. Text is recognized and combined into a single string
    4. Results are returned with confidence score
    """
)
async def extract_text(
    file: UploadFile = File(..., description="Image file to process"),
    preprocess: bool = True,
    ocr_service: OCRService = Depends(get_ocr_service)
):
    """
    Extract text from an uploaded image file.
    
    Args:
        file: The image file to process
        preprocess: Whether to apply image preprocessing (default: True)
        ocr_service: The OCR service (injected by FastAPI)
        
    Returns:
        OCRTextResponse with extracted text and metadata
    """
    logger.info(f"Received file: {file.filename}, content_type: {file.content_type}")
    
    # =============================================================================
    # STEP 1: VALIDATE FILE
    # =============================================================================
    error_message = validate_file_upload(file)
    if error_message:
        logger.warning(f"File validation failed: {error_message}")
        return JSONResponse(
            status_code=400,
            content={
                "success": False,
                "error": "INVALID_FILE",
                "message": error_message,
                "details": f"Received: {file.filename}"
            }
        )
    
    # =============================================================================
    # STEP 2: READ FILE CONTENT
    # =============================================================================
    try:
        contents = await file.read()
        logger.debug(f"Read {len(contents)} bytes from file")
        
        if len(contents) == 0:
            return JSONResponse(
                status_code=400,
                content={
                    "success": False,
                    "error": "EMPTY_FILE",
                    "message": "The uploaded file is empty."
                }
            )
            
    except Exception as e:
        logger.exception(f"Failed to read file: {str(e)}")
        return JSONResponse(
            status_code=400,
            content={
                "success": False,
                "error": "READ_ERROR",
                "message": f"Failed to read file: {str(e)}"
            }
        )
    
    # =============================================================================
    # STEP 3: EXTRACT TEXT USING OCR SERVICE
    # =============================================================================
    try:
        result = ocr_service.extract_text_from_bytes(contents, preprocess=preprocess)
        
        if not result["success"]:
            return JSONResponse(
                status_code=500,
                content={
                    "success": False,
                    "error": result.get("error", "OCR_FAILED"),
                    "message": result.get("message", "OCR processing failed")
                }
            )
        
        # Return successful response
        return OCRTextResponse(
            success=True,
            extracted_text=result["extracted_text"],
            confidence=result.get("confidence"),
            word_count=result.get("word_count")
        )
        
    except Exception as e:
        logger.exception(f"OCR processing failed: {str(e)}")
        return JSONResponse(
            status_code=500,
            content={
                "success": False,
                "error": "OCR_FAILED",
                "message": f"OCR processing failed: {str(e)}"
            }
        )


# =============================================================================
# ENDPOINT: EXTRACT TEXT (DETAILED RESPONSE)
# =============================================================================
# WWH:
# WHAT: Alternative endpoint that returns detailed per-line results
# WHY: Some applications need position data or per-line confidence
# HOW: Same as above, but returns more detailed structure

@router.post(
    "/extract-text/detailed",
    response_model=DetailedOCRResponse,
    summary="Extract text with detailed results",
    description="""
    Extract text from an image with detailed line-by-line results.
    
    Returns:
    - Full combined text
    - Each detected line with its confidence score
    - Bounding box coordinates for each line
    - Average confidence across all lines
    """
)
async def extract_text_detailed(
    file: UploadFile = File(..., description="Image file to process"),
    preprocess: bool = True,
    ocr_service: OCRService = Depends(get_ocr_service)
):
    """
    Extract text with detailed per-line information.
    
    This endpoint is useful when you need:
    - Per-line confidence scores
    - Text location coordinates
    - Individual line analysis
    """
    logger.info(f"Detailed extraction for: {file.filename}")
    
    # Validate
    error_message = validate_file_upload(file)
    if error_message:
        return JSONResponse(
            status_code=400,
            content={
                "success": False,
                "error": "INVALID_FILE",
                "message": error_message
            }
        )
    
    # Read and process
    try:
        contents = await file.read()
        result = ocr_service.extract_text_from_bytes(contents, preprocess=preprocess)
        
        if not result["success"]:
            return JSONResponse(
                status_code=500,
                content={
                    "success": False,
                    "error": result.get("error", "OCR_FAILED"),
                    "message": result.get("message", "OCR processing failed")
                }
            )
        
        # Build detailed response
        lines = [
            TextLine(
                text=line["text"],
                confidence=line["confidence"],
                bounding_box=line.get("bounding_box")
            )
            for line in result.get("lines", [])
        ]
        
        return DetailedOCRResponse(
            success=True,
            extracted_text=result["extracted_text"],
            lines=lines,
            average_confidence=result.get("confidence", 0.0),
            total_lines=len(lines)
        )
        
    except Exception as e:
        logger.exception(f"Detailed OCR failed: {str(e)}")
        return JSONResponse(
            status_code=500,
            content={
                "success": False,
                "error": "OCR_FAILED",
                "message": f"OCR processing failed: {str(e)}"
            }
        )


# =============================================================================
# ENDPOINT: GET SUPPORTED FORMATS
# =============================================================================
# WWH:
# WHAT: Informational endpoint that lists supported image formats
# WHY: Helps clients know what files they can send
# HOW: Return the static list from OCRService

@router.get(
    "/supported-formats",
    summary="Get supported image formats",
    description="Returns a list of image formats that the OCR service can process."
)
async def get_supported_formats():
    """
    Get the list of supported image formats.
    
    Returns:
        Dictionary with list of supported extensions
    """
    return {
        "supported_formats": OCRService.get_supported_formats(),
        "description": "Image files with these extensions can be processed for text extraction."
    }
