"""
=============================================================================
OCR SERVICE - PADDLEOCR WRAPPER
=============================================================================

WWH EXPLANATION FOR ocr_service.py:

WHAT:
    A service class that wraps the PaddleOCR library to provide text extraction.
    It's the "brain" of our microservice that does the actual OCR work.

WHY:
    - Encapsulation: Keeps OCR logic separate from web routing logic
    - Reusability: The same service can be used in different contexts
    - Testability: Easy to unit test without running the web server
    - Single Responsibility: This class only handles OCR, nothing else

HOW:
    1. Initialize PaddleOCR with appropriate settings at startup
    2. Accept images as numpy arrays or file bytes
    3. Call PaddleOCR to detect and recognize text
    4. Parse and format the results

=============================================================================
"""

import os
# Disable oneDNN/MKLDNN to avoid compatibility issues on Windows
os.environ["FLAGS_use_mkldnn"] = "0"
os.environ["MKLDNN_CACHE_CAPACITY"] = "0"
# PaddlePaddle 3.x uses PIR executor which has a Windows OneDNN bug.
os.environ["FLAGS_enable_pir_in_executor"] = "0"
os.environ["FLAGS_pir_apply_shape_optimization_pass"] = "0"

import cv2
import numpy as np
from typing import List, Tuple, Dict, Any
import logging
from paddleocr import PaddleOCR

from utils.image_preprocessing import preprocess_image, validate_image, apply_threshold

logger = logging.getLogger(__name__)


# =============================================================================
# OCR SERVICE CLASS
# =============================================================================

class OCRService:
    """
    Service class for performing OCR using PaddleOCR.
    
    WWH:
    WHAT: A wrapper around PaddleOCR that handles text extraction from images.
    WHY: Provides a clean interface for the rest of the application.
    HOW: Initializes PaddleOCR once, then reuses it for all requests.
    
    Usage:
        service = OCRService()  # Initialize once
        text = service.extract_text(image_bytes)  # Use many times
    """
    
    # Supported image formats
    SUPPORTED_FORMATS = {'.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.tif'}
    
    def __init__(
        self,
        lang: str = 'en'
    ):
        """
        Initialize the OCR service with PaddleOCR.
        
        WWH:
        WHAT: Constructor that sets up PaddleOCR model
        WHY: Loading the model takes time; we do it once at startup
        HOW: Create PaddleOCR instance with optimal settings
        
        Args:
            lang: Language for recognition ('en' for English, 'ch' for Chinese)
        """
        logger.info(f"Initializing PaddleOCR (lang={lang})")
        
        # =============================================================================
        # PADDLEOCR INITIALIZATION
        # =============================================================================
        # WWH:
        # WHAT: PaddleOCR is an open-source OCR library by Baidu/PaddlePaddle
        # WHY: It's accurate, supports 80+ languages, and works offline
        # HOW: Creates detection + recognition + classification pipeline

        # Tuned for medical documents and mobile-captured reports where text can be tiny/faint.
        self.ocr = PaddleOCR(
            lang=lang,
            use_angle_cls=True,
            det_limit_side_len=1536,
            det_db_thresh=0.20,
            det_db_box_thresh=0.45,
            det_db_unclip_ratio=1.8
        )
        
        logger.info("PaddleOCR initialized successfully")
    
    def extract_text_from_bytes(
        self, 
        image_bytes: bytes,
        preprocess: bool = True
    ) -> Dict[str, Any]:
        """
        Extract text from image bytes (from file upload).
        
        WWH:
        WHAT: Main method to process uploaded image files
        WHY: File uploads arrive as raw bytes; we need to decode them
        HOW: Convert bytes to numpy array, then call extract_text
        
        Args:
            image_bytes: Raw image data as bytes
            preprocess: Whether to apply preprocessing
            
        Returns:
            Dictionary with extraction results
        """
        logger.info("Processing image from bytes")
        
        try:
            # Convert bytes to numpy array
            # np.frombuffer creates array from raw bytes
            # cv2.imdecode converts to image format
            nparr = np.frombuffer(image_bytes, np.uint8)
            image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            
            if image is None:
                logger.error("Failed to decode image from bytes")
                return {
                    "success": False,
                    "error": "INVALID_IMAGE",
                    "message": "Could not decode image. File may be corrupted."
                }
            
            logger.debug(f"Decoded image shape: {image.shape}")
            return self.extract_text(image, preprocess=preprocess)
            
        except Exception as e:
            logger.exception(f"Error processing image bytes: {str(e)}")
            return {
                "success": False,
                "error": "PROCESSING_ERROR",
                "message": f"Failed to process image: {str(e)}"
            }
    
    def extract_text(
        self, 
        image: np.ndarray,
        preprocess: bool = True
    ) -> Dict[str, Any]:
        """
        Extract text from an image (as numpy array).
        
        WWH:
        WHAT: Core OCR method that processes an image and extracts text
        WHY: Separates the OCR logic from image loading/decoding
        HOW: 
            1. Validate the image
            2. Optionally preprocess for better results
            3. Run PaddleOCR
            4. Parse and format results
        
        Args:
            image: Image as numpy array (BGR format from OpenCV)
            preprocess: Whether to apply preprocessing
            
        Returns:
            Dictionary containing:
            - success: bool
            - extracted_text: str (combined text)
            - lines: list of (text, confidence) tuples
            - confidence: average confidence score
        """
        logger.info("Starting text extraction")
        
        # =============================================================================
        # STEP 1: VALIDATE IMAGE
        # =============================================================================
        is_valid, error_msg = validate_image(image)
        if not is_valid:
            logger.error(f"Invalid image: {error_msg}")
            return {
                "success": False,
                "error": "INVALID_IMAGE",
                "message": error_msg
            }
        
        # =============================================================================
        # STEP 2: MULTI-PASS OCR FOR HIGHER RECALL
        # =============================================================================
        # Single-pass OCR often misses faint/small text. Run several views and merge.
        pass_results: List[Dict[str, Any]] = []
        candidate_images = self._build_candidate_images(image, preprocess)

        for pass_name, candidate in candidate_images:
            try:
                logger.debug(f"Running OCR pass: {pass_name}")
                raw = self.ocr.ocr(candidate, cls=True)
                parsed = self._parse_ocr_results(raw)
                pass_results.append(parsed)
                logger.debug(
                    "OCR pass %s -> lines=%s words=%s confidence=%.4f",
                    pass_name,
                    len(parsed.get("lines", [])),
                    parsed.get("word_count", 0),
                    parsed.get("confidence", 0.0)
                )
            except Exception as ex:
                logger.warning("OCR pass '%s' failed: %s", pass_name, ex)

        if not pass_results:
            return {
                "success": False,
                "error": "OCR_FAILED",
                "message": "OCR processing failed for all passes."
            }

        return self._merge_pass_results(pass_results)

    def _build_candidate_images(self, image: np.ndarray, preprocess: bool) -> List[Tuple[str, np.ndarray]]:
        candidates: List[Tuple[str, np.ndarray]] = [("original", image)]

        if preprocess:
            try:
                enhanced = preprocess_image(
                    image,
                    grayscale=True,
                    denoise=True,
                    enhance_contrast_flag=True,
                    deskew=False,
                    binarize=False
                )
                if len(enhanced.shape) == 2:
                    enhanced_bgr = cv2.cvtColor(enhanced, cv2.COLOR_GRAY2BGR)
                else:
                    enhanced_bgr = enhanced
                candidates.append(("enhanced", enhanced_bgr))

                adaptive = apply_threshold(enhanced, method="adaptive")
                adaptive_bgr = cv2.cvtColor(adaptive, cv2.COLOR_GRAY2BGR)
                candidates.append(("adaptive", adaptive_bgr))
            except Exception as ex:
                logger.warning("Preprocess variants failed, fallback to original only: %s", ex)

        max_side = max(image.shape[:2])
        if max_side < 2200:
            scale = 1.6 if max_side < 1500 else 1.3
            upscaled = cv2.resize(image, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
            candidates.append((f"upscaled_{scale:.1f}x", upscaled))

        return candidates

    def _merge_pass_results(self, pass_results: List[Dict[str, Any]]) -> Dict[str, Any]:
        merged_lines: List[Dict[str, Any]] = []
        best_by_key: Dict[str, Dict[str, Any]] = {}

        for result in pass_results:
            for line in result.get("lines", []):
                key = self._line_key(line)
                if key not in best_by_key or line.get("confidence", 0.0) > best_by_key[key].get("confidence", 0.0):
                    best_by_key[key] = line

        merged_lines.extend(best_by_key.values())

        def sort_key(line: Dict[str, Any]) -> Tuple[float, float]:
            center = self._bbox_center(line.get("bounding_box"))
            if center is None:
                return (10_000_000.0, 10_000_000.0)
            return (center[1], center[0])

        merged_lines.sort(key=sort_key)

        combined_text = "\n".join([x.get("text", "") for x in merged_lines if x.get("text")])
        confidences = [float(x.get("confidence", 0.0)) for x in merged_lines]
        avg_confidence = (sum(confidences) / len(confidences)) if confidences else 0.0

        logger.info(
            "Merged OCR output: lines=%s words=%s confidence=%.4f",
            len(merged_lines),
            len(combined_text.split()) if combined_text else 0,
            avg_confidence
        )

        return {
            "success": True,
            "extracted_text": combined_text,
            "lines": merged_lines,
            "confidence": round(avg_confidence, 4),
            "word_count": len(combined_text.split()) if combined_text else 0,
            "line_count": len(merged_lines)
        }

    @staticmethod
    def _bbox_center(bbox: Any) -> Tuple[float, float] | None:
        if not bbox:
            return None

        try:
            points = np.array(bbox, dtype=float)
            if points.ndim != 2 or points.shape[1] != 2:
                return None
            center = points.mean(axis=0)
            return float(center[0]), float(center[1])
        except Exception:
            return None

    def _line_key(self, line: Dict[str, Any]) -> str:
        text = " ".join(str(line.get("text", "")).upper().split())
        center = self._bbox_center(line.get("bounding_box"))
        if center is None:
            return text

        x_bin = int(center[0] / 8.0)
        y_bin = int(center[1] / 8.0)
        return f"{text}|{x_bin}|{y_bin}"
    
    def _parse_ocr_results(self, results: List) -> Dict[str, Any]:
        """
        Parse PaddleOCR results into a structured response.
        
        PaddleOCR 3.4+ .predict() returns a list of result objects with:
        - rec_texts: list of recognized texts
        - rec_scores: list of confidence scores
        - dt_polys: list of bounding polygon coordinates
        
        Args:
            results: Raw output from PaddleOCR predict()
            
        Returns:
            Formatted dictionary with extracted text
        """
        # Handle empty results
        if not results:
            logger.info("No text detected in image")
            return {
                "success": True,
                "extracted_text": "",
                "lines": [],
                "confidence": 0.0,
                "word_count": 0,
                "message": "No text detected in the image"
            }
        
        extracted_lines = []
        confidences = []
        
        # PaddleOCR 3.4+ returns list of result objects
        for result in results:
            # Handle new format from .predict()
            if hasattr(result, 'rec_texts') and hasattr(result, 'rec_scores'):
                texts = result.rec_texts if result.rec_texts else []
                scores = result.rec_scores if result.rec_scores else []
                polys = result.dt_polys if hasattr(result, 'dt_polys') and result.dt_polys is not None else []
                
                for i, (text, score) in enumerate(zip(texts, scores)):
                    if text:
                        bbox = polys[i].tolist() if i < len(polys) else None
                        extracted_lines.append({
                            "text": str(text),
                            "confidence": float(score),
                            "bounding_box": bbox
                        })
                        confidences.append(float(score))
            # Handle old format (list of [bbox, (text, confidence)])
            elif isinstance(result, list):
                for line in result:
                    if line is None:
                        continue
                    if isinstance(line, list) and len(line) >= 2:
                        bounding_box = line[0]
                        text_info = line[1]
                        if text_info and len(text_info) >= 2:
                            text = text_info[0]
                            confidence = text_info[1]
                            extracted_lines.append({
                                "text": str(text),
                                "confidence": float(confidence),
                                "bounding_box": bounding_box
                            })
                            confidences.append(float(confidence))
        
        # Combine all text lines with newlines
        combined_text = "\n".join([line["text"] for line in extracted_lines])
        
        # Calculate average confidence
        avg_confidence = sum(confidences) / len(confidences) if confidences else 0.0
        
        # Count words
        word_count = len(combined_text.split()) if combined_text else 0
        
        logger.info(f"Extracted {len(extracted_lines)} lines, {word_count} words, avg confidence: {avg_confidence:.2f}")
        
        return {
            "success": True,
            "extracted_text": combined_text,
            "lines": extracted_lines,
            "confidence": round(avg_confidence, 4),
            "word_count": word_count,
            "line_count": len(extracted_lines)
        }
    
    @staticmethod
    def is_supported_format(filename: str) -> bool:
        """
        Check if a file has a supported image format.
        
        Args:
            filename: Name of the file (with extension)
            
        Returns:
            True if format is supported, False otherwise
        """
        import os
        ext = os.path.splitext(filename)[1].lower()
        return ext in OCRService.SUPPORTED_FORMATS
    
    @staticmethod
    def get_supported_formats() -> List[str]:
        """Get list of supported image formats."""
        return list(OCRService.SUPPORTED_FORMATS)


# =============================================================================
# EXAMPLE USAGE (for testing)
# =============================================================================

if __name__ == "__main__":
    # This runs only when you execute this file directly
    # Useful for quick testing
    
    print("Testing OCRService...")
    
    # Initialize service
    service = OCRService()
    
    # Test with a sample image (if you have one)
    import sys
    if len(sys.argv) > 1:
        image_path = sys.argv[1]
        image = cv2.imread(image_path)
        
        if image is not None:
            result = service.extract_text(image)
            print("\nExtraction Result:")
            print(f"Success: {result['success']}")
            print(f"Text: {result.get('extracted_text', 'N/A')}")
            print(f"Confidence: {result.get('confidence', 'N/A')}")
        else:
            print(f"Could not read image: {image_path}")
    else:
        print("No test image provided. Usage: python ocr_service.py <image_path>")
