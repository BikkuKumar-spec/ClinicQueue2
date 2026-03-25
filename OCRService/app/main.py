"""
=============================================================================
IMAGE OCR MICROSERVICE - MAIN APPLICATION FILE
=============================================================================

WWH EXPLANATION FOR main.py:

WHAT:
    This is the main entry point of our FastAPI application.
    It initializes the web server and connects all the pieces together.

WHY:
    - FastAPI needs a central file to start the application
    - We load the OCR model here ONCE at startup (not on every request)
    - This improves performance significantly
    - It's where we configure CORS, routes, and middleware

HOW:
    1. Create a FastAPI app instance
    2. Use @app.on_event("startup") to load the OCR model when server starts
    3. Include routers from other files to organize our code
    4. Configure CORS to allow the .NET backend to call this service

=============================================================================
"""

import os
# Disable oneDNN/MKLDNN BEFORE importing PaddleOCR to avoid Windows compatibility issues
os.environ["FLAGS_use_mkldnn"] = "0"
os.environ["MKLDNN_CACHE_CAPACITY"] = "0"
# PaddlePaddle 3.x uses PIR (Program IR) executor which has a Windows OneDNN bug.
# Disabling PIR executor forces the old executor that correctly respects FLAGS_use_mkldnn=0.
os.environ["FLAGS_enable_pir_in_executor"] = "0"
os.environ["FLAGS_pir_apply_shape_optimization_pass"] = "0"

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager
import logging
import sys
from pathlib import Path

# Add the app directory to Python path for imports to work
sys.path.insert(0, str(Path(__file__).parent))

# Import our custom router
from routes.ocr_routes import router as ocr_router

# Import the OCR service to initialize model at startup
from services.ocr_service import OCRService

# =============================================================================
# LOGGING CONFIGURATION
# =============================================================================
# WWH:
# WHAT: Logging helps track what's happening in the application
# WHY: Essential for debugging and monitoring in production
# HOW: Configure Python's logging module to show info-level messages

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# =============================================================================
# GLOBAL OCR SERVICE INSTANCE
# =============================================================================
# WWH:
# WHAT: A single instance of our OCR service shared across all requests
# WHY: Loading PaddleOCR model is slow (~2-3 seconds). We do it once and reuse.
# HOW: Create instance at module level, initialize in lifespan event

ocr_service: OCRService = None


# =============================================================================
# APPLICATION LIFESPAN (STARTUP & SHUTDOWN)
# =============================================================================
# WWH:
# WHAT: asynccontextmanager that handles app startup and shutdown events
# WHY: We need to load the OCR model before receiving requests
# HOW: Code before 'yield' runs at startup, code after runs at shutdown

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Handles application startup and shutdown events.
    
    Startup: Load the PaddleOCR model into memory
    Shutdown: Clean up resources if needed
    """
    global ocr_service
    
    # STARTUP
    logger.info("🚀 Starting OCR Microservice...")
    logger.info("📥 Loading PaddleOCR model (this may take a few seconds)...")
    
    try:
        ocr_service = OCRService()
        logger.info("✅ PaddleOCR model loaded successfully!")
    except Exception as e:
        logger.error(f"❌ Failed to load OCR model: {str(e)}")
        raise
    
    # Make OCR service available to routes via app.state
    app.state.ocr_service = ocr_service
    
    yield  # Application runs here
    
    # SHUTDOWN
    logger.info("🛑 Shutting down OCR Microservice...")


# =============================================================================
# CREATE FASTAPI APPLICATION
# =============================================================================
# WWH:
# WHAT: FastAPI() creates our web application instance
# WHY: FastAPI is a modern, fast web framework perfect for microservices
# HOW: Pass configuration like title, description, version, and lifespan

app = FastAPI(
    title="Image OCR Microservice",
    description="""
    ## Medical Image Text Extraction Service
    
    This microservice extracts text from medical images such as:
    - Prescriptions
    - Medical reports
    - Scanned documents
    
    ### How it works:
    1. Upload an image (jpg, png, bmp, tiff)
    2. Image is preprocessed for better OCR accuracy
    3. PaddleOCR extracts text from the image
    4. Extracted text is returned in JSON format
    
    ### Supported formats:
    - JPEG (.jpg, .jpeg)
    - PNG (.png)
    - BMP (.bmp)
    - TIFF (.tiff, .tif)
    """,
    version="1.0.0",
    lifespan=lifespan
)


# =============================================================================
# CORS MIDDLEWARE CONFIGURATION
# =============================================================================
# WWH:
# WHAT: CORS (Cross-Origin Resource Sharing) controls who can call our API
# WHY: Browsers block requests from different origins by default for security
#      Our .NET backend runs on a different port, so we need to allow it
# HOW: Add middleware that adds appropriate headers to responses

# CORS: read allowed origins from env (comma-separated) so this works in dev and prod
# without code changes. Default covers the .NET backend and both common React dev servers.
_raw_origins = os.environ.get("ALLOWED_ORIGINS", "")
ALLOWED_ORIGINS: list[str] = (
    [o.strip() for o in _raw_origins.split(",") if o.strip()]
    if _raw_origins
    else [
        "http://localhost:5000",   # .NET backend
        "http://localhost:5173",   # React frontend (Vite)
        "http://localhost:3000",   # React frontend (CRA)
    ]
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# =============================================================================
# INCLUDE ROUTERS
# =============================================================================
# WWH:
# WHAT: Routers organize our endpoints into logical groups
# WHY: Keeps code organized as the application grows
# HOW: Use app.include_router() to add routes from other files

app.include_router(ocr_router, prefix="/api/v1", tags=["OCR"])


# =============================================================================
# ROOT ENDPOINT - HEALTH CHECK
# =============================================================================
# WWH:
# WHAT: A simple endpoint that confirms the service is running
# WHY: .NET backend and monitoring tools need to check service health
# HOW: Return basic info about the service status

@app.get("/", tags=["Health"])
async def root():
    """
    Root endpoint - returns service information.
    Used for health checks and service discovery.
    """
    return {
        "service": "Image OCR Microservice",
        "status": "running",
        "version": "1.0.0",
        "endpoints": {
            "extract_text": "/api/v1/extract-text",
            "health": "/health",
            "docs": "/docs"
        }
    }


@app.get("/health", tags=["Health"])
async def health_check():
    """
    Health check endpoint for monitoring and load balancers.
    Returns the status of critical components.
    """
    ocr_ready = app.state.ocr_service is not None
    
    return {
        "status": "healthy" if ocr_ready else "degraded",
        "ocr_model_loaded": ocr_ready,
        "message": "OCR service is ready" if ocr_ready else "OCR model not loaded"
    }


# =============================================================================
# RUN WITH UVICORN (FOR DIRECT EXECUTION)
# =============================================================================
# WWH:
# WHAT: This block runs when you execute 'python main.py' directly
# WHY: Convenient for development, but production should use 'uvicorn' command
# HOW: Import uvicorn and call run() with our app

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host="0.0.0.0",      # Listen on all network interfaces
        port=8001,            # Port number (different from .NET backend)
        reload=True           # Auto-reload on code changes (development only)
    )
