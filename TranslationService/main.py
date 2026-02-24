import os
import asyncio
import torch
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from transformers import AutoModelForSeq2SeqLM, AutoTokenizer
import langid
import requests
import json
from typing import Dict, Any

from indic_transliteration import sanscript
from indic_transliteration.sanscript import transliterate

os.environ["CUDA_VISIBLE_DEVICES"] = "-1"
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "llama3.1")

SUPPORTED_LANGS = ["en", "hi"]

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

NO_TRANSLATE_FIELDS = {
    "intent", "specialty_needed", "preferred_doctor",
    "preferred_date", "preferred_time", "patient_name", "missing_info"
}

# ------------------------
# SYSTEM PROMPT
# Simplified for quantized 8B — one .format() call, no nested braces issues.
# The JSON template uses single-quoted keys to avoid escaping nightmares.
# ------------------------
SYSTEM_PROMPT = (
    "You are a clinic receptionist chatting on WhatsApp. Be warm, brief, human.\n"
    "Today's date: 2026-02-23.\n"
    "\n"
    "Rules:\n"
    "- reply_message must be under 18 words, casual, no bullet points.\n"
    "- Ask only ONE question at a time.\n"
    "- Do not say 'Based on your symptoms', 'Kindly', or 'As an AI'.\n"
    "- Gather: specialty/doctor, date, time.\n"
    "- Suggest a specialty once you understand the problem.\n"
    "- The patient's name is already known: do NOT ask for it again.\n"
    "- You have already sent the opening greeting. Do NOT re-greet or re-ask "
    "  'how are you feeling' if the patient has already responded.\n"
    "- Never re-ask for information already provided earlier in this conversation.\n"
    "  Always read the full chat history before replying.\n"
    "- If the patient writes in another language (e.g. Hindi), understand it, "
    "  but always reply in English.\n"
    "- Never contradict yourself (e.g. do not say a doctor is available, "
    "  then say they are booked in the next message).\n"
    "\n"
    "Allowed specialties: {specialties}\n"
    "Allowed doctors: {doctors}\n"
    "\n"
    "Reply ONLY with valid JSON, no extra text, no markdown:\n"
    '{{"reply_message": "...", '
    '"intent": "Greeting|Triage_Ongoing|Triage_Complete|Booking_InProgress|Booking_Confirmed|General_Inquiry", '
    '"extracted_entities": {{'
    '"specialty_needed": null, '
    '"preferred_doctor": null, '
    '"preferred_date": null, '
    '"preferred_time": null, '
    '"patient_name": "Shloka"}}, '
    '"missing_info": []}}'
).format(
    specialties=", ".join(ALLOWED_SPECIALTIES),
    doctors=", ".join(ALLOWED_DOCTORS),
)

# ------------------------
# Model State
# ------------------------
en_indic_model = None
en_indic_tokenizer = None
indic_en_model = None
indic_en_tokenizer = None
device = "cpu"

sessions: Dict[str, list] = {}
session_languages: Dict[str, str] = {}


# ------------------------
# Lifespan
# ------------------------
@asynccontextmanager
async def lifespan(app: FastAPI):
    load_models()
    langid.set_languages(SUPPORTED_LANGS)
    logger.info("Service Ready")
    yield
    logger.info("Service Shutting Down")


# ------------------------
# App (single definition, with lifespan + CORS)
# ------------------------
app = FastAPI(title="Indic Translation Service + Hinglish Support", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


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
# Detect Language Endpoint
# ------------------------
@app.post("/detect", response_model=DetectLanguageResponse)
async def detect_lang(request: DetectLanguageRequest):
    text = request.text.strip()

    if not text:
        return DetectLanguageResponse(
            language="eng_Latn",
            confidence=1.0
        )

    # If devanagari detected, confidence high
    if has_devanagari(text):
        return DetectLanguageResponse(
            language="hin_Deva",
            confidence=0.99
        )

    lang_code, confidence = langid.classify(text)
    indic_lang = LANGID_TO_INDIC.get(lang_code, "eng_Latn")

    return DetectLanguageResponse(
        language=indic_lang,
        confidence=float(confidence)
    )

# ------------------------
# Model Loading
# Helsinki-NLP models are fully public — no HuggingFace login or gating.
# opus-mt-en-hi : English -> Hindi (~300MB)
# opus-mt-hi-en : Hindi -> English (~300MB)
# ------------------------
def load_models():
    global en_indic_model, en_indic_tokenizer
    global indic_en_model, indic_en_tokenizer

    logger.info("Loading translation models (Helsinki-NLP)...")

    en_ckpt = "Helsinki-NLP/opus-mt-en-hi"
    indic_ckpt = "Helsinki-NLP/opus-mt-hi-en"

    en_indic_tokenizer = AutoTokenizer.from_pretrained(en_ckpt)
    en_indic_model = AutoModelForSeq2SeqLM.from_pretrained(en_ckpt).to(device)

    indic_en_tokenizer = AutoTokenizer.from_pretrained(indic_ckpt)
    indic_en_model = AutoModelForSeq2SeqLM.from_pretrained(indic_ckpt).to(device)

    logger.info("Models loaded successfully.")


# ------------------------
# Language Detection
# ------------------------
def has_devanagari(text: str) -> bool:
    return any("\u0900" <= ch <= "\u097F" for ch in text)


def detect_language(text: str) -> str:
    if len(text.strip()) < 3:
        if has_devanagari(text):
            return "hin_Deva"
        return "eng_Latn"

    if has_devanagari(text):
        logger.info(f"[DETECT] Devanagari found in '{text}' -> hin_Deva")
        return "hin_Deva"

    lang_code, _ = langid.classify(text)
    result = LANGID_TO_INDIC.get(lang_code, "eng_Latn")
    logger.info(f"[DETECT] langid '{text}' -> {result}")
    return result


# ------------------------
# Translation
# ------------------------
def run_translation(text: str, src_lang: str, tgt_lang: str) -> str:
    if src_lang == tgt_lang:
        return text

    if src_lang == "eng_Latn":
        model = en_indic_model
        tokenizer = en_indic_tokenizer
    else:
        model = indic_en_model
        tokenizer = indic_en_tokenizer

    # Helsinki-NLP models don't need a language prefix — just tokenize directly
    inputs = tokenizer([text], return_tensors="pt", padding=True, truncation=True, max_length=512).to(device)

    with torch.inference_mode():
        generated_tokens = model.generate(**inputs, max_length=512)

    return tokenizer.batch_decode(generated_tokens, skip_special_tokens=True)[0]


# ------------------------
# Sanitizer
# ------------------------
def sanitize_ai_output(output: dict) -> dict:
    cleaned = {k: v.strip() if isinstance(v, str) else v for k, v in output.items()}

    cleaned.setdefault(
        "reply_message",
        "Sorry, I'm having trouble. Could you rephrase that?"
    )
    cleaned.setdefault("intent", "General_Inquiry")
    cleaned.setdefault("missing_info", [])

    if "extracted_entities" not in cleaned or not isinstance(cleaned["extracted_entities"], dict):
        cleaned["extracted_entities"] = {
            "specialty_needed": None,
            "preferred_doctor": None,
            "preferred_date": None,
            "preferred_time": None,
            "patient_name": None,
        }
    else:
        entities = cleaned["extracted_entities"]
        for key in ["specialty_needed", "preferred_doctor", "preferred_date", "preferred_time", "patient_name"]:
            if key not in entities:
                entities[key] = None
            elif isinstance(entities[key], str) and entities[key].lower() in ("null", "none", ""):
                entities[key] = None

    return cleaned


# ------------------------
# Llama Call
# Fix: history stores {"role", "content"} where content is plain text
# (the assistant's reply_message), NOT the raw JSON blob.
# This keeps context readable for the LLM without confusing it.
# ------------------------
def call_llama(user_message: str, history: list) -> dict:
    try:
        messages = [{"role": "system", "content": SYSTEM_PROMPT}]
        messages.extend(history)
        messages.append({"role": "user", "content": user_message})

        logger.info(f"[OLLAMA] Sending to {OLLAMA_MODEL}: {user_message}")

        response = requests.post(
            "http://localhost:11435/api/chat",
            json={
                "model": OLLAMA_MODEL,
                "messages": messages,
                "stream": False,
                "format": "json",
                # Lower temperature = more consistent JSON from quantized models
                "options": {"temperature": 0.2, "top_p": 0.9},
            },
            timeout=120,
        )

        if not response.ok:
            logger.error(f"[OLLAMA ERROR] {response.status_code}: {response.text}")
            return _fallback()

        resp_json = response.json()

        if "message" not in resp_json:
            logger.error(f"[OLLAMA] Missing 'message' key: {resp_json}")
            return _fallback()

        content = resp_json["message"]["content"]
        logger.info(f"[OLLAMA CONTENT] {content}")

        # Strip markdown fences if the model wraps JSON in ```json ... ```
        content = content.strip()
        if content.startswith("```"):
            content = content.split("```")[-2] if "```" in content[3:] else content[3:]
            content = content.lstrip("json").strip()

        parsed = json.loads(content)
        return sanitize_ai_output(parsed)

    except json.JSONDecodeError as e:
        logger.error(f"[JSON PARSE ERROR] {e} | Raw: {content if 'content' in dir() else 'N/A'}")
        return _fallback()
    except Exception as e:
        logger.error(f"[AI EXCEPTION] {e}", exc_info=True)
        return _fallback()


def _fallback() -> dict:
    return sanitize_ai_output({
        "reply_message": "Sorry, service is temporarily unavailable.",
        "intent": "General_Inquiry",
    })


# ------------------------
# Chat Endpoint
# ------------------------
@app.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest):
    session_id = request.session_id
    if session_id not in sessions:
        sessions[session_id] = []

    history = sessions[session_id]
    user_text = request.text.strip()

    # Language detection with ambiguity handling
    is_ambiguous = user_text.isdigit() or len(user_text) <= 2
    if is_ambiguous and session_id in session_languages:
        detected_lang = session_languages[session_id]
        if has_devanagari(user_text):
            detected_lang = "hin_Deva"
    else:
        detected_lang = detect_language(user_text)

    # Persist session language
    if detected_lang != "eng_Latn":
        session_languages[session_id] = "hin_Deva"
    elif session_id not in session_languages:
        session_languages[session_id] = "eng_Latn"

    # Translate input to English if needed
    english_input = user_text
    if detected_lang != "eng_Latn":
        logger.info(f"[TRANSLATE] {detected_lang} -> eng_Latn")
        english_input = run_translation(user_text, detected_lang, "eng_Latn")
        logger.info(f"[TRANSLATE RESULT] {english_input}")

    # Call LLM
    english_output = await asyncio.to_thread(call_llama, english_input, list(history))

    # Store history as plain text (user message + assistant reply only)
    # This is the key fix: quantized models choke on JSON blobs in history
    history.append({"role": "user", "content": english_input})
    history.append({"role": "assistant", "content": english_output.get("reply_message", "")})
    sessions[session_id] = history[-10:]  # Keep last 5 turns

    # Translate reply back if needed
    final_output = english_output.copy()
    if detected_lang != "eng_Latn" and "reply_message" in final_output:
        logger.info(f"[TRANSLATE REPLY] eng_Latn -> {detected_lang}")
        final_output["reply_message"] = run_translation(
            final_output["reply_message"],
            "eng_Latn",
            detected_lang,
        )

    return ChatResponse(
        original_language=detected_lang,
        english_input=english_input,
        english_output=english_output,
        final_output=final_output,
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