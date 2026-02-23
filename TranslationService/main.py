import os
import asyncio
import torch
import logging
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from transformers import AutoModelForSeq2SeqLM, AutoTokenizer
import langid
import requests
import json
from typing import Dict, Any

# 🔥 NEW — Hinglish Support
from indic_transliteration import sanscript
from indic_transliteration.sanscript import transliterate

os.environ["CUDA_VISIBLE_DEVICES"] = "-1"

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "llama3.1")

app = FastAPI(title="Indic Translation Service + Hinglish Support")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ------------------------
# Model State
# ------------------------
en_indic_model = None
en_indic_tokenizer = None
indic_en_model = None
indic_en_tokenizer = None

device = "cpu"

SUPPORTED_LANGS = [
    "en", "hi"
]

LANGID_TO_INDIC = {
    "en": "eng_Latn",
    "hi": "hin_Deva",
}

ALLOWED_SPECIALTIES = [
    "General Physician", "Dermatologist", "Pediatrician", 
    "Orthopedist", "ENT Specialist", "Ophthalmologist", 
    "Cardiologist", "Pulmonologist"
]
ALLOWED_DOCTORS = [
    "Dr. Sharma (General Physician)", "Dr. Verma (General Physician)",
    "Dr. Mehta (Dermatologist)", "Dr. Ajay (Dermatologist)",
    "Dr. Gupta (Pediatrician)", "Dr. Rao (Orthopedist)",
    "Dr. Singh (ENT Specialist)", "Dr. Kapoor (Ophthalmologist)",
    "Dr. Desai (Cardiologist)", "Dr. Iyer (Pulmonologist)"
]
NO_TRANSLATE_FIELDS = {"intent", "specialty_needed", "preferred_doctor", "preferred_date", "preferred_time", "patient_name", "missing_info"}

# ------------------------
# Language Detection Helpers
# ------------------------

def has_devanagari(text: str) -> bool:
    """Returns True if the text contains any Devanagari characters."""
    return any("\u0900" <= ch <= "\u097F" for ch in text)


# ------------------------
# MASTER SYSTEM PROMPT
# ------------------------
SYSTEM_PROMPT = """You are the Senior Medical Receptionist/AI Assistant for Dr. Sharma's Clinic.
Your goal is to provide a 100% natural conversation experience via WhatsApp. 
You guide patients through triage, specialty recommendation, doctor selection, and appointment booking.

### GUIDELINES:
1. **Tone**: Professional, empathetic, and efficient.
2. **Behavior**: 
   - Start by greeting the user and asking for their symptoms if they haven't provided any.
   - Perform triage by asking 3-4 medically relevant follow-up questions.
   - Once triage is done, recommend a specialty from the ALLOWED_SPECIALTIES list.
   - Offer specific doctors from the ALLOWED_DOCTORS list based on the specialty.
   - Negotiate date and time naturally (Today, Tomorrow, or specific dates/times).
   - Once all details (Patient Name, Doctor, Date, Time) are gathered, confirm the booking.
3. **Language**: You will receive input in English and must output in English. (Translation is handled externally).

### KNOWLEDGE BASE:
- **Allowed Specialties**: {specialties}
- **Allowed Doctors**: {doctors}

### OUTPUT FORMAT:
You MUST respond strictly in valid JSON format ONLY. No backticks, no explanatory text.
Schema:
{{
  "reply_message": "The natural text to send to the user.",
  "intent": "Greeting | Triage_Ongoing | Triage_Complete | Booking_InProgress | Booking_Confirmed | General_Inquiry",
  "extracted_entities": {{
    "specialty_needed": "String or null",
    "preferred_doctor": "String or null (Extract name only, e.g., 'Dr. Ajay')",
    "preferred_date": "YYYY-MM-DD or null",
    "preferred_time": "HH:mm or null (24-hour format)",
    "patient_name": "String or null"
  }},
  "missing_info": ["patient_name", "preferred_doctor", etc.] (List fields still needed to confirm a booking)
}}

Current Date: {current_date}
""".format(
    specialties=", ".join(ALLOWED_SPECIALTIES),
    doctors=", ".join(ALLOWED_DOCTORS),
    current_date="2026-02-23" # Hardcoded for now, ideally dynamic
)


sessions: Dict[str, list[Dict[str, str]]] = {}
session_languages: Dict[str, str] = {}

# ------------------------
# Schemas
# ------------------------
class ChatRequest(BaseModel):
    text: str
    session_id: str = "default_user"

class ChatResponse(BaseModel):
    original_language: str
    english_input: str
    english_output: Dict[str, Any]
    final_output: Dict[str, Any]

class DetectLanguageRequest(BaseModel):
    text: str

class DetectLanguageResponse(BaseModel):
    language: str
    confidence: float

# ------------------------
# Model Loading
# ------------------------
def load_models():
    global en_indic_model, en_indic_tokenizer
    global indic_en_model, indic_en_tokenizer

    logger.info("Loading translation models...")

    en_ckpt = "ai4bharat/indictrans2-en-indic-dist-200m"
    indic_ckpt = "ai4bharat/indictrans2-indic-en-dist-200m"

    en_indic_tokenizer = AutoTokenizer.from_pretrained(en_ckpt, trust_remote_code=True)
    en_indic_model = AutoModelForSeq2SeqLM.from_pretrained(
        en_ckpt, trust_remote_code=True
    ).to(device)

    indic_en_tokenizer = AutoTokenizer.from_pretrained(indic_ckpt, trust_remote_code=True)
    indic_en_model = AutoModelForSeq2SeqLM.from_pretrained(
        indic_ckpt, trust_remote_code=True
    ).to(device)

    logger.info("Models loaded successfully.")

@app.on_event("startup")
async def startup_event():
    load_models()
    langid.set_languages(SUPPORTED_LANGS)
    logger.info("Service Ready")

# ------------------------
# Language Detection
# ------------------------
def detect_language(text: str):
    if len(text.strip()) < 3:
        # Check for 2-char Devanagari (like "हा")
        if has_devanagari(text):
            return "hin_Deva"
        return "eng_Latn"

    # 🔥 Aggressive Hindi Detection: If any Devanagari char is present, it's Hindi.
    if has_devanagari(text):
        logger.info(f"[DETECT] Found Devanagari in: '{text}' -> hin_Deva")
        return "hin_Deva"

    # For Roman text, use langid
    lang_code, _ = langid.classify(text)
    result = LANGID_TO_INDIC.get(lang_code, "eng_Latn")
    logger.info(f"[DETECT] langid result for '{text}': {result}")
    return result

# ------------------------
# Translation
# ------------------------
def run_translation(text: str, src_lang: str, tgt_lang: str):
    if src_lang == tgt_lang:
        return text

    if src_lang == "eng_Latn":
        model = en_indic_model
        tokenizer = en_indic_tokenizer
    else:
        model = indic_en_model
        tokenizer = indic_en_tokenizer

    prefixed_text = f"{src_lang} {tgt_lang} {text}"

    inputs = tokenizer([prefixed_text], return_tensors="pt").to(device)

    with torch.inference_mode():
        generated_tokens = model.generate(**inputs, max_length=256)

    return tokenizer.batch_decode(generated_tokens, skip_special_tokens=True)[0]

# ------------------------
# Sanitizer (UNCHANGED)
# ------------------------
def sanitize_ai_output(output: dict) -> dict:
    cleaned = {k: v.strip() if isinstance(v, str) else v for k, v in output.items()}

    # Ensure required top-level keys
    cleaned.setdefault("reply_message", "I apologize, but I'm having trouble processing that. Could you please rephrase?")
    cleaned.setdefault("intent", "General_Inquiry")
    cleaned.setdefault("missing_info", [])

    # Ensure extracted_entities
    if "extracted_entities" not in cleaned or not isinstance(cleaned["extracted_entities"], dict):
        cleaned["extracted_entities"] = {
            "specialty_needed": None,
            "preferred_doctor": None,
            "preferred_date": None,
            "preferred_time": None,
            "patient_name": None
        }
    else:
        # Sanitize entities
        entities = cleaned["extracted_entities"]
        for key in ["specialty_needed", "preferred_doctor", "preferred_date", "preferred_time", "patient_name"]:
            if key not in entities:
                entities[key] = None
            elif isinstance(entities[key], str) and entities[key].lower() in ("null", "none", ""):
                entities[key] = None

    return cleaned

# ------------------------
# Llama Call (UNCHANGED)
# ------------------------
def call_llama(user_message: str, history):
    try:
        messages = [{"role": "system", "content": SYSTEM_PROMPT}]
        messages.extend(history)
        messages.append({"role": "user", "content": user_message})

        logger.info(f"[OLLAMA] Calling {OLLAMA_MODEL} with message: {user_message}")

        response = requests.post(
            "http://localhost:11434/api/chat",
            json={
                "model": OLLAMA_MODEL,
                "messages": messages,
                "stream": False,
                "format": "json"
            },
            timeout=120
        )

        if not response.ok:
            logger.error(f"[OLLAMA ERROR] Status: {response.status_code} | Body: {response.text}")
            return {"stage": "questioning", "question": "Service temporarily unavailable."}

        resp_json = response.json()
        logger.info(f"[OLLAMA RESPONSE] Success")

        if "message" not in resp_json:
            logger.error(f"[OLLAMA ERROR] Response missing 'message' key: {resp_json}")
            return {"stage": "questioning", "question": "Please describe your symptoms clearly."}

        content = resp_json["message"]["content"]
        logger.info(f"[OLLAMA CONTENT] {content}")
        
        parsed = json.loads(content)
        return sanitize_ai_output(parsed)

    except Exception as e:
        logger.error(f"[AI EXCEPTION] {str(e)}", exc_info=True)
        return {"stage": "questioning", "question": "Service temporarily unavailable."}

# ------------------------
# CHAT ENDPOINT (UPDATED FOR HINGLISH)
# ------------------------
@app.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest):
    session_id = request.session_id

    if session_id not in sessions:
        sessions[session_id] = []

    history = sessions[session_id]

    user_text = request.text.strip()

    # Normal detection logic
    is_ambiguous = user_text.isdigit() or len(user_text) <= 2

    if is_ambiguous and session_id in session_languages:
        detected_lang = session_languages[session_id]
        # BUT if those 2 digits/chars are Devanagari, it's Hindi!
        if has_devanagari(user_text):
            detected_lang = "hin_Deva"
    else:
        detected_lang = detect_language(user_text)
        
    # Update session language
    if detected_lang != "eng_Latn":
        session_languages[session_id] = "hin_Deva"
    elif session_id not in session_languages:
        session_languages[session_id] = "eng_Latn"

    english_input = user_text
    if detected_lang != "eng_Latn":
        logger.info(f"[TRANSLATE] {detected_lang} -> eng_Latn")
        english_input = run_translation(user_text, detected_lang, "eng_Latn")
        logger.info(f"[TRANSLATE RESULT] {english_input}")

    english_output = await asyncio.to_thread(call_llama, english_input, list(history))

    history.append({"role": "user", "content": english_input})
    history.append({"role": "assistant", "content": json.dumps(english_output)})

    sessions[session_id] = history[-10:]

    final_output = english_output.copy()

    if detected_lang != "eng_Latn":
        # We only translate the user-facing text
        if "reply_message" in final_output:
            logger.info(f"[TRANSLATE REPLY] eng_Latn -> {detected_lang}")
            final_output["reply_message"] = run_translation(
                final_output["reply_message"],
                "eng_Latn",
                detected_lang
            )

    return ChatResponse(
        original_language=detected_lang,
        english_input=english_input,
        english_output=english_output,
        final_output=final_output
    )

# ------------------------
# Reset
# ------------------------
@app.post("/reset")
async def reset_session(session_id: str = "default_user"):
    sessions.pop(session_id, None)
    session_languages.pop(session_id, None)
    return {"message": "Session reset"}

# ------------------------
# Health
# ------------------------
@app.get("/health")
async def health_check():
    return {"status": "ok", "allowed_specialties": ALLOWED_SPECIALTIES}