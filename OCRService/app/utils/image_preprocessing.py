"""
=============================================================================
IMAGE PREPROCESSING UTILITIES
=============================================================================

WWH EXPLANATION FOR image_preprocessing.py:

WHAT:
    A collection of image processing functions that prepare images for OCR.
    Uses OpenCV (cv2) to manipulate image pixels.

WHY:
    - Raw images often have noise, poor contrast, or skewed orientation
    - Preprocessing significantly improves OCR accuracy (can increase by 20-40%)
    - Medical documents may be photos of papers (not clean scans)
    - These operations help PaddleOCR "see" the text more clearly

HOW:
    1. Convert to grayscale (removes color, keeps text structure)
    2. Apply noise reduction (removes speckles and artifacts)
    3. Enhance contrast (makes text stand out from background)
    4. Apply thresholding (converts to pure black/white if needed)

=============================================================================
"""

import cv2
import numpy as np
from typing import Tuple, Optional
import logging

logger = logging.getLogger(__name__)


# =============================================================================
# GRAYSCALE CONVERSION
# =============================================================================
# WWH:
# WHAT: Converts a color image to shades of gray (0-255)
# WHY: OCR works on text contrast, not color. Grayscale reduces data and focuses on brightness.
# HOW: OpenCV's cvtColor transforms BGR (Blue-Green-Red) to single-channel grayscale

def convert_to_grayscale(image: np.ndarray) -> np.ndarray:
    """
    Convert an image to grayscale.
    
    Args:
        image: Input image as numpy array (BGR format from OpenCV)
        
    Returns:
        Grayscale image as numpy array
    """
    # Check if already grayscale (single channel)
    if len(image.shape) == 2:
        logger.debug("Image is already grayscale")
        return image
    
    # Check if image has 3 channels (color)
    if len(image.shape) == 3:
        grayscale = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        logger.debug("Converted image to grayscale")
        return grayscale
    
    raise ValueError(f"Unexpected image shape: {image.shape}")


# =============================================================================
# NOISE REDUCTION
# =============================================================================
# WWH:
# WHAT: Removes random pixel variations (noise) that can confuse OCR
# WHY: Photos often have noise from camera sensors; scans may have dust specks
# HOW: Gaussian blur averages neighboring pixels; Non-local means is smarter but slower

def reduce_noise(
    image: np.ndarray, 
    method: str = "gaussian"
) -> np.ndarray:
    """
    Reduce noise in the image.
    
    Args:
        image: Input image (grayscale or color)
        method: "gaussian" (fast) or "nlm" (better quality, slower)
        
    Returns:
        Denoised image
    """
    if method == "gaussian":
        # Gaussian blur - fast and good for most cases
        # (5, 5) is the kernel size - larger = more blur
        denoised = cv2.GaussianBlur(image, (5, 5), 0)
        logger.debug("Applied Gaussian blur for noise reduction")
        
    elif method == "nlm":
        # Non-Local Means Denoising - better quality but slower
        # Parameters: h (filter strength), templateWindowSize, searchWindowSize
        if len(image.shape) == 2:  # Grayscale
            denoised = cv2.fastNlMeansDenoising(image, None, 10, 7, 21)
        else:  # Color
            denoised = cv2.fastNlMeansDenoisingColored(image, None, 10, 10, 7, 21)
        logger.debug("Applied Non-Local Means denoising")
        
    else:
        raise ValueError(f"Unknown denoising method: {method}")
    
    return denoised


# =============================================================================
# CONTRAST ENHANCEMENT
# =============================================================================
# WWH:
# WHAT: Increases the difference between light and dark areas
# WHY: Faded documents or poor lighting can make text hard to distinguish
# HOW: CLAHE (Contrast Limited Adaptive Histogram Equalization) enhances locally

def enhance_contrast(
    image: np.ndarray, 
    clip_limit: float = 2.0, 
    tile_grid_size: Tuple[int, int] = (8, 8)
) -> np.ndarray:
    """
    Enhance contrast using CLAHE (Contrast Limited Adaptive Histogram Equalization).
    
    CLAHE is better than simple histogram equalization because it:
    - Works on small regions (tiles) instead of the whole image
    - Limits contrast enhancement to avoid noise amplification
    
    Args:
        image: Input grayscale image
        clip_limit: Threshold for contrast limiting (higher = more contrast)
        tile_grid_size: Size of grid for CLAHE algorithm
        
    Returns:
        Contrast-enhanced image
    """
    # Ensure grayscale
    if len(image.shape) == 3:
        image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    
    # Create CLAHE object
    clahe = cv2.createCLAHE(clipLimit=clip_limit, tileGridSize=tile_grid_size)
    
    # Apply CLAHE
    enhanced = clahe.apply(image)
    logger.debug("Applied CLAHE contrast enhancement")
    
    return enhanced


# =============================================================================
# THRESHOLDING (BINARIZATION)
# =============================================================================
# WWH:
# WHAT: Converts grayscale to pure black and white (binary)
# WHY: Some OCR engines work better with binary images; removes gray backgrounds
# HOW: Otsu's method automatically finds the best threshold value

def apply_threshold(
    image: np.ndarray, 
    method: str = "otsu"
) -> np.ndarray:
    """
    Apply thresholding to convert image to binary (black and white).
    
    Args:
        image: Input grayscale image
        method: "otsu" (automatic) or "adaptive" (for uneven lighting)
        
    Returns:
        Binary image (only black and white pixels)
    """
    # Ensure grayscale
    if len(image.shape) == 3:
        image = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    
    if method == "otsu":
        # Otsu's binarization - automatically finds optimal threshold
        _, binary = cv2.threshold(
            image, 0, 255, 
            cv2.THRESH_BINARY + cv2.THRESH_OTSU
        )
        logger.debug("Applied Otsu's thresholding")
        
    elif method == "adaptive":
        # Adaptive thresholding - handles uneven lighting (like photos of documents)
        binary = cv2.adaptiveThreshold(
            image, 255,
            cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
            cv2.THRESH_BINARY,
            11,  # Block size (must be odd)
            2    # Constant subtracted from mean
        )
        logger.debug("Applied adaptive thresholding")
        
    else:
        raise ValueError(f"Unknown thresholding method: {method}")
    
    return binary


# =============================================================================
# DESKEWING (ROTATION CORRECTION)
# =============================================================================
# WWH:
# WHAT: Detects and corrects image rotation caused by tilted scanning/photography
# WHY: Rotated text is harder for OCR to recognize accurately
# HOW: Find text lines, calculate their average angle, rotate image to straighten

def deskew_image(image: np.ndarray) -> np.ndarray:
    """
    Correct slight rotation in the image.
    
    This is useful when documents were scanned or photographed at an angle.
    
    Args:
        image: Input image (grayscale recommended)
        
    Returns:
        Deskewed (straightened) image
    """
    # Ensure grayscale
    gray = image if len(image.shape) == 2 else cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    
    # Detect edges
    edges = cv2.Canny(gray, 50, 150, apertureSize=3)
    
    # Detect lines using Hough Transform
    lines = cv2.HoughLines(edges, 1, np.pi / 180, 200)
    
    if lines is None:
        logger.debug("No lines detected for deskewing, returning original")
        return image
    
    # Calculate the median angle
    angles = []
    for line in lines:
        rho, theta = line[0]
        angle = (theta * 180 / np.pi) - 90
        if -45 < angle < 45:  # Only consider reasonable angles
            angles.append(angle)
    
    if not angles:
        return image
    
    median_angle = np.median(angles)
    
    # Only deskew if angle is significant (> 0.5 degrees)
    if abs(median_angle) < 0.5:
        logger.debug(f"Skew angle too small ({median_angle:.2f}°), skipping deskew")
        return image
    
    # Rotate the image
    (h, w) = image.shape[:2]
    center = (w // 2, h // 2)
    rotation_matrix = cv2.getRotationMatrix2D(center, median_angle, 1.0)
    rotated = cv2.warpAffine(
        image, rotation_matrix, (w, h),
        flags=cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_REPLICATE
    )
    
    logger.debug(f"Deskewed image by {median_angle:.2f}°")
    return rotated


# =============================================================================
# MAIN PREPROCESSING PIPELINE
# =============================================================================
# WWH:
# WHAT: Combines all preprocessing steps into a single function
# WHY: Provides a one-call solution for image preparation
# HOW: Applies steps in optimal order; steps can be enabled/disabled

def preprocess_image(
    image: np.ndarray,
    grayscale: bool = True,
    denoise: bool = True,
    enhance_contrast_flag: bool = True,
    deskew: bool = False,
    binarize: bool = False
) -> np.ndarray:
    """
    Complete image preprocessing pipeline for OCR.
    
    Applies preprocessing steps in optimal order:
    1. Convert to grayscale (optional)
    2. Reduce noise
    3. Enhance contrast
    4. Deskew (optional, can be slow)
    5. Binarize (optional, for some OCR engines)
    
    Args:
        image: Input image as numpy array
        grayscale: Convert to grayscale
        denoise: Apply noise reduction
        enhance_contrast_flag: Apply contrast enhancement
        deskew: Correct image rotation
        binarize: Convert to black and white
        
    Returns:
        Preprocessed image ready for OCR
    """
    logger.info("Starting image preprocessing pipeline")
    processed = image.copy()
    
    # Step 1: Convert to grayscale
    if grayscale:
        processed = convert_to_grayscale(processed)
    
    # Step 2: Reduce noise
    if denoise:
        processed = reduce_noise(processed, method="gaussian")
    
    # Step 3: Enhance contrast
    if enhance_contrast_flag:
        # CLAHE works on grayscale, ensure we have it
        if len(processed.shape) == 3:
            processed = convert_to_grayscale(processed)
        processed = enhance_contrast(processed)
    
    # Step 4: Deskew (rotation correction)
    if deskew:
        processed = deskew_image(processed)
    
    # Step 5: Binarize (optional)
    if binarize:
        processed = apply_threshold(processed, method="otsu")
    
    logger.info("Image preprocessing completed")
    return processed


# =============================================================================
# UTILITY: VALIDATE IMAGE
# =============================================================================

def validate_image(image: np.ndarray) -> Tuple[bool, Optional[str]]:
    """
    Validate that the image is suitable for OCR processing.
    
    Args:
        image: Input image as numpy array
        
    Returns:
        Tuple of (is_valid, error_message)
    """
    if image is None:
        return False, "Image is None"
    
    if not isinstance(image, np.ndarray):
        return False, f"Expected numpy array, got {type(image)}"
    
    if image.size == 0:
        return False, "Image is empty"
    
    if len(image.shape) < 2:
        return False, f"Invalid image dimensions: {image.shape}"
    
    # Check minimum size (too small images won't have readable text)
    min_dimension = min(image.shape[:2])
    if min_dimension < 10:
        return False, f"Image too small: {image.shape}"
    
    return True, None
