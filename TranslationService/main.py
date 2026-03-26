import os
import asyncio
import torch
import logging
from datetime import datetime
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

OLLAMA_BASE_URL = os.environ.get("OLLAMA_BASE_URL", "http://10.30.1.34:11434")
OLLAMA_MODEL = os.environ.get("OLLAMA_MODEL", "llama3.1:8b")
HF_TOKEN = os.environ.get("HF_TOKEN", None)
REPORT_SUMMARY_TRIGGER = "[REPORT_SUMMARY]"
CLINIC_API_BASE_URL = os.environ.get("CLINIC_API_BASE_URL", "http://localhost:5000")

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

# Dynamic lists - will be populated from API
ALLOWED_SPECIALTIES = []
ALLOWED_DOCTORS = []
DOCTORS_BY_SPECIALTY = {}  # For grouping doctors by specialty

NO_TRANSLATE_FIELDS = {"intent", "specialty_needed", "preferred_doctor", "preferred_date", "preferred_time", "patient_name", "missing_info"}

# ------------------------
# Language Detection Helpers
# ------------------------

def has_devanagari(text: str) -> bool:
    """Returns True if the text contains any Devanagari characters."""
    return any("\u0900" <= ch <= "\u097F" for ch in text)


# ------------------------
# Fetch Doctors and Specialties from API
# ------------------------
async def fetch_doctors_and_specialties():
    """Fetch doctors and specialties from the ClinicQueue API at startup."""
    global ALLOWED_SPECIALTIES, ALLOWED_DOCTORS, DOCTORS_BY_SPECIALTY
    
    try:
        # Fetch specialties
        spec_url = f"{CLINIC_API_BASE_URL}/api/doctors/specialties"
        logger.info(f"[FETCH] Fetching specialties from {spec_url}")
        spec_response = requests.get(spec_url, timeout=10)
        
        if spec_response.ok:
            specialties = spec_response.json()
            if isinstance(specialties, list):
                ALLOWED_SPECIALTIES = specialties
                logger.info(f"[FETCH] Successfully loaded {len(ALLOWED_SPECIALTIES)} specialties")
            else:
                logger.warning("[FETCH] Unexpected specialties response format")
        else:
            logger.warning(f"[FETCH] Failed to fetch specialties: {spec_response.status_code}")
        
        # Fetch doctors
        doctors_url = f"{CLINIC_API_BASE_URL}/api/doctors"
        logger.info(f"[FETCH] Fetching doctors from {doctors_url}")
        doctors_response = requests.get(doctors_url, timeout=10)
        
        if doctors_response.ok:
            doctors_data = doctors_response.json()
            if isinstance(doctors_data, list):
                # Format doctors for display: "Dr. Name (Specialty)"
                doctor_list = []
                doctors_by_spec = {}
                
                for doctor in doctors_data:
                    doctor_name = doctor.get("name", "")
                    specialty = doctor.get("specialty", "")
                    
                    if doctor_name:
                        formatted = f"{doctor_name} ({specialty})" if specialty else doctor_name
                        doctor_list.append(formatted)
                        
                        # Group by specialty
                        if specialty not in doctors_by_spec:
                            doctors_by_spec[specialty] = []
                        doctors_by_spec[specialty].append(doctor_name)
                
                ALLOWED_DOCTORS = doctor_list
                DOCTORS_BY_SPECIALTY = doctors_by_spec
                logger.info(f"[FETCH] Successfully loaded {len(ALLOWED_DOCTORS)} doctors")
            else:
                logger.warning("[FETCH] Unexpected doctors response format")
        else:
            logger.warning(f"[FETCH] Failed to fetch doctors: {doctors_response.status_code}")
    
    except Exception as e:
        logger.error(f"[FETCH] Error fetching doctors/specialties: {e}")
        # Fallback to empty lists - will be populated on next attempt
        logger.info("[FETCH] Will retry on next startup if API becomes available")


# ------------------------
# Language Detection Helpers
# ------------------------

def has_devanagari(text: str) -> bool:
    """Returns True if the text contains any Devanagari characters."""
    return any("\u0900" <= ch <= "\u097F" for ch in text)


# ------------------------
# MASTER SYSTEM PROMPT
# ------------------------
SYSTEM_PROMPT_TEMPLATE = """You are the Senior Medical Receptionist/AI Assistant for Dr. Sharma's Clinic.
Your goal is to provide a 100% natural conversation experience via WhatsApp. 
You guide patients through triage, specialty recommendation, doctor selection, and appointment booking.

### GUIDELINES:
1. **Tone**: Professional, empathetic, and efficient.
2. **Session Start**: 
   - If patient name is already known (from database), use "Returning patient rule" at the VERY START of conversation.
   - If patient name is NOT known, skip returning patient logic and go directly to normal greeting.
3. **Behavior**: 
   - Start by greeting the user and asking for their symptoms if they haven't provided any.
   - Perform triage by asking 3-4 medically relevant follow-up questions.
   - Once triage is done, recommend a specialty from the ALLOWED_SPECIALTIES list.
   - Offer specific doctors from the ALLOWED_DOCTORS list based on the specialty.
   - Negotiate date and time naturally (Today, Tomorrow, or specific dates/times).
   - Ask for patient name BEFORE confirming (if not already known).
   - Once ALL details (Patient Name, Doctor, Date, Time) are gathered, IMMEDIATELY respond with Booking_Confirmed intent and a clear confirmation message.
4. **Language**: You will receive input in English and must output in English. (Translation is handled externally).
4. **Name before confirmation** (CRITICAL):
    - ALWAYS ask patient name before confirming any booking.
    - Example: "May I have your name please?"
    - Never confirm without getting name.
    - When patient provides their name, IMMEDIATELY extract and use it in the extracted_entities.patient_name field.
    - Do NOT leave patient_name empty if patient has said their name in the conversation.
5. **Confirmation Rule** (CRITICAL):
    - When you have ALL details (patient_name, preferred_doctor, preferred_date, preferred_time), IMMEDIATELY set:
      - intent: "Booking_Confirmed"
      - reply_message: "Done! Appointment booked with Dr. [DocName] on [Date] at [Time]. See you then! 🏥"
    - Example correct format with full details:
      "Done! Appointment booked with Dr. Mukesh on Tomorrow (26-Mar) at 9:00 AM. See you then! 🏥"
      OR
      "Done! Appointment confirmed: Dr. Mukesh | [Date] | [Time Slot]. Thanks! 🏥"
    - ALWAYS include: Doctor Name + Date + Time Slot in confirmation
    - NEVER ask anything else after Booking_Confirmed.
    - NEVER restart the conversation after booking.
6. **Slot suggestion rule**:
    - NEVER suggest 2 PM as a default slot.
    - Always suggest the next available slot based on current time.
    - Use Current Date and Time in this prompt to decide slot timing.
    - If current time is past 5:30 PM, suggest next day morning slot.
    - Working hours are 9 AM to 5 PM only.
    - Slots are every 30 minutes.
    - Example: if time is 6:30 PM, suggest tomorrow at 9:00 AM, not 2 PM today.
6. **Past-time booking rule**:
    - Never book a slot that has already passed today.
    - If user says "today" but it is after 5 PM, reply exactly:
      "Clinic is closed for today. Earliest slot is tomorrow at 9 AM."
7. **Returning patient rule** (ONLY at SESSION START):
    - If patient name is ALREADY KNOWN from database at the START of conversation:
    - Greet them by name and ask: "Welcome back [name]! Shall I book for you or someone else like a family member?"
    - If they say yes/confirm/book: Book appointment under their registered name.
    - If they say no/family/someone else: Ask "Who would you like to book for? Please share their name."
    - Then collect the new person's name and book under that name.
    - CRITICAL: When booking for family member, ALWAYS use the family member's name (NOT the original patient's name).
    - Extract the family member name and set it in extracted_entities.patient_name.
    - IMPORTANT: Only use this logic once, at the START. Do not repeat this question mid-booking.
    - Example: If Rajesh's phone books for his sister Priya, extracted_entities.patient_name MUST be "Priya", not "Rajesh".
8. **New patient rule**:
    - If patient has not booked before, always ask their name before booking.
    - Ask: "May I have your name please?"
    - Never confirm booking without name.
    - Use extracted_entities.patient_name for whoever the booking is for (registered patient or family member).

### FAMILY MEMBER BOOKING RULE (CRITICAL):
- When same phone number books for DIFFERENT family members, ALWAYS ask:
  - "Who should I book this appointment for?" or "What's the name of the person for this appointment?"
  - Capture the ACTUAL name of the person getting the appointment
  - Set extracted_entities.patient_name to that person's name (NOT the original caller's name)
  - Example: If Rajesh (who booked before) now books for his sister Priya:
    - AI asks: "Is this still for you, Rajesh, or for someone else?"
    - Rajesh says: "No, it's for my sister Priya"
    - AI MUST set extracted_entities.patient_name = "Priya" (NOT "Rajesh")
    - Confirmation: "Done! Appointment booked for Priya with Dr. Mukesh on 26-Mar at 9:00 AM 🏥"
- CRITICAL: extracted_entities.patient_name must always reflect the person GETTING the appointment, not the person calling.

### STRICT REPLY RULES:
- Maximum 2 sentences per reply.
- Never use formal corporate language.
- Sound like a friendly human receptionist.
- No disclaimers or legal-style text.
- No 'please don't hesitate to reach out'.
- No 'we look forward to seeing you'.
- **Confirmation must include: Doctor Name + Date + Time Slot** in ONE message.
- **After Booking_Confirmed, DO NOT ask "How can I help?" or restart conversation.**- **CRITICAL: patient_name in extracted_entities must be set to the ACTUAL patient name from the conversation, NOT null or empty.**- Example CORRECT confirmation with full details:
    'Done! Appointment booked with Dr. Mukesh on Tomorrow (26-Mar) at 9:00 AM. See you then! 🏥'
    'Confirmed: Dr. Mukesh | 26-Mar-2026 | 9:00 AM. Thanks! 🏥'
- Example WRONG confirmation (Don't do this):
    'Your appointment is confirmed. Please arrive 15 minutes early. If you have questions contact us. Details: Name...'
    'Done! Dr. Mukesh tomorrow at 9:00 AM' (missing full date)
- Ask ONE thing at a time only.
- Never list appointment details in long format.

### REPLY LENGTH RULES:
- Maximum 1-2 short sentences per reply.
- Never more than 20 words in a reply.
- Sound like a real friend not a doctor.
- No formal language ever.
- No disclaimers.

Example styles:

BAD (too long):
"Sorry to hear that you're feeling unwell.
 Can you please tell me how long you've
 had the fever and is it accompanied by
 any other symptoms like cough, headache,
 or body aches?"

GOOD (correct):
"Oh no! How long have you had fever?
 Any other symptoms?"

BAD:
"I've booked Dr. Suresh for tomorrow at
 2:00 PM. Is that okay with you?"

GOOD:
"Done! Dr. Suresh tomorrow at 2 PM. 🏥"

BAD:
"I think it's best for you to see a
 General Physician. Based on your symptoms,
 Dr. Suresh or Dr. Mukesh would be a good fit."

GOOD:
"Sounds like you need a General Physician.
 Dr. Suresh free tomorrow. Shall I book?"

### CRITICAL BOOKING STATE RULES:
- **Patient Name Extraction (MANDATORY)**: 
  - If patient has mentioned their name at ANY point in the conversation, it MUST be captured in extracted_entities.patient_name.
  - For family members: The name MUST be the family member's name (person getting appointment), NOT the contact person's name.
  - Example: If Rajesh calls to book for his sister Priya, patient_name = "Priya" (NOT "Rajesh")
  - Example: If patient says "Hi, I'm Rajesh" or "My name is Priya", extract "Rajesh" or "Priya" into the patient_name field immediately.
  - NEVER allow patient_name to be null if the patient has provided their name.
  - If name not yet provided, keep asking until you get it before booking.
- **missing_info** field shows what's still needed:
  - When missing_info is empty [] → Booking is READY to confirm → Set intent to "Booking_Confirmed"
  - When you set intent to "Booking_Confirmed", IMMEDIATELY respond with confirmation message (ONE sentence max)
  - NEVER transition from Booking_Confirmed back to Booking_InProgress
  - NEVER ask "How can I help you today?" after Booking_Confirmed
- Example correct flow with FULL DATE & TIME SLOT:
  1. User: "Ohk done" (confirming all details)
  2. AI checks: patient_name=Kuldeep, doctor=Dr. Mukesh, date=26-Mar-2026, time=9:00 AM ✓
  3. AI sets missing_info: [] (empty)
  4. AI sets intent: "Booking_Confirmed"
  5. AI MUST set extracted_entities.patient_name to "Kuldeep" (NOT null!)
  6. AI replies: "Done! Appointment booked with Dr. Mukesh on 26-Mar at 9:00 AM. See you then! 🏥"
  7. **END OF CONVERSATION** - Do NOT ask anything more.
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

Current Date and Time: {current_datetime}
"""

def get_system_prompt():
    """Generate the system prompt with current doctors and specialties."""
    current_datetime = datetime.now().strftime("%Y-%m-%d %H:%M")
    return SYSTEM_PROMPT_TEMPLATE.format(
        specialties=", ".join(ALLOWED_SPECIALTIES) if ALLOWED_SPECIALTIES else "No specialties loaded",
        doctors=", ".join(ALLOWED_DOCTORS) if ALLOWED_DOCTORS else "No doctors loaded",
        current_datetime=current_datetime
    )

REPORT_SUMMARY_SYSTEM_PROMPT = """You are a medical report summarization assistant.
Your task is to summarize uploaded lab/medical report text for a patient in plain, easy English.

Rules:
- Extract key findings and abnormal values if present.
- Keep summary concise and useful.
- Use 4 to 6 short bullet points in reply_message.
- Do not include booking flow, greetings, or appointment suggestions.
- Do not add disclaimers.
- If text is non-medical or unreadable, reply_message should say that clearly in one short sentence.

Output JSON only using this schema:
{
    "reply_message": "string",
    "intent": "General_Inquiry",
    "extracted_entities": {
        "specialty_needed": null,
        "preferred_doctor": null,
        "preferred_date": null,
        "preferred_time": null,
        "patient_name": null
    },
    "missing_info": []
}
"""


sessions: Dict[str, list[Dict[str, str]]] = {}
session_languages: Dict[str, str] = {}

# ------------------------
# Schemas
# ------------------------
class ChatRequest(BaseModel):
    text: str
    session_id: str = "default_user"


class SummarizeRequest(BaseModel):
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
# Model Loading (Lazy)
# ------------------------
models_loaded = False
models_loading = False

def load_models():
    global en_indic_model, en_indic_tokenizer
    global indic_en_model, indic_en_tokenizer
    global models_loaded, models_loading

    if models_loaded:
        return
    if models_loading:
        logger.info("[MODELS] Already loading, skipping duplicate call.")
        return

    models_loading = True
    logger.info("[MODELS] Loading translation models (lazy)...")

    en_ckpt = "ai4bharat/indictrans2-en-indic-dist-200m"
    indic_ckpt = "ai4bharat/indictrans2-indic-en-dist-200m"

    token_kwargs = {"token": HF_TOKEN} if HF_TOKEN else {}

    try:
        en_indic_tokenizer = AutoTokenizer.from_pretrained(en_ckpt, trust_remote_code=True, **token_kwargs)
        en_indic_model = AutoModelForSeq2SeqLM.from_pretrained(
            en_ckpt, trust_remote_code=True, **token_kwargs
        ).to(device)

        indic_en_tokenizer = AutoTokenizer.from_pretrained(indic_ckpt, trust_remote_code=True, **token_kwargs)
        indic_en_model = AutoModelForSeq2SeqLM.from_pretrained(
            indic_ckpt, trust_remote_code=True, **token_kwargs
        ).to(device)

        models_loaded = True
        logger.info("[MODELS] Models loaded successfully.")
    except Exception as e:
        logger.error(f"[MODELS] Failed to load models: {e}")
    finally:
        models_loading = False

@app.on_event("startup")
async def startup_event():
    langid.set_languages(SUPPORTED_LANGS)
    logger.info("Service Starting — fetching doctors and specialties from API...")
    await fetch_doctors_and_specialties()
    logger.info("Service Ready — models will load on first translation request.")

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

    # Lazy load models on first translation call
    if not models_loaded:
        logger.info("[TRANSLATE] Models not loaded yet — loading now (this may take a few minutes)...")
        load_models()

    if not models_loaded:
        logger.error("[TRANSLATE] Models unavailable. Returning original text.")
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
    # If reply_message is missing/empty, fall back to old-schema fields (stage/question)
    if not cleaned.get("reply_message"):
        fallback = cleaned.get("question") or cleaned.get("stage") or "I apologize, but I'm having trouble processing that. Could you please rephrase?"
        cleaned["reply_message"] = fallback
    cleaned.setdefault("intent", cleaned.get("stage", "General_Inquiry"))
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


def normalize_report_summary_output(output: dict) -> dict:
    """Normalize non-schema summary payloads into the chat JSON schema.

    Report-mode prompts may return keys like IS_MEDICAL/TYPE/SUMMARY instead of
    reply_message. This adapter converts them into a concise bullet reply.
    """
    if not isinstance(output, dict):
        return {
            "reply_message": "I could not generate a clear summary.",
            "intent": "General_Inquiry",
            "extracted_entities": {
                "specialty_needed": None,
                "preferred_doctor": None,
                "preferred_date": None,
                "preferred_time": None,
                "patient_name": None,
            },
            "missing_info": [],
        }

    # Prefer explicit reply_message when present; some models include SUMMARY
    # with noisy raw OCR while reply_message is the intended concise output.
    reply = str(output.get("reply_message") or "").strip()

    if not reply:
        raw_summary = output.get("SUMMARY")
        if isinstance(raw_summary, list):
            summary_lines = [str(x).strip() for x in raw_summary if str(x).strip()]
            reply = "\n".join(f"- {line}" for line in summary_lines)
        elif isinstance(raw_summary, str):
            summary_lines = [x.strip(" -\t") for x in raw_summary.splitlines() if x.strip()]
            reply = "\n".join(f"- {line}" for line in summary_lines)

    if not reply:
        doc_type = str(output.get("TYPE") or "medical report").strip()
        is_medical = str(output.get("IS_MEDICAL") or "yes").strip().lower()
        if is_medical in {"no", "false", "non-medical"}:
            reply = "The uploaded file does not look like a medical report."
        else:
            reply = f"- Summary generated for the uploaded {doc_type}."

    normalized = {
        "reply_message": reply,
        "intent": "General_Inquiry",
        "extracted_entities": {
            "specialty_needed": None,
            "preferred_doctor": None,
            "preferred_date": None,
            "preferred_time": None,
            "patient_name": None,
        },
        "missing_info": [],
    }

    return normalized

# ------------------------
# Qwen Call (UNCHANGED)
# ------------------------
def call_qwen(user_message: str, history):
    try:
        is_report_summary_mode = user_message.startswith(REPORT_SUMMARY_TRIGGER)

        effective_user_message = user_message
        if is_report_summary_mode:
            effective_user_message = user_message[len(REPORT_SUMMARY_TRIGGER):].strip()

        system_prompt = REPORT_SUMMARY_SYSTEM_PROMPT if is_report_summary_mode else get_system_prompt()

        messages = [{"role": "system", "content": system_prompt}]
        if not is_report_summary_mode:
            messages.extend(history)
        messages.append({"role": "user", "content": effective_user_message})

        logger.info(f"[OLLAMA] Calling {OLLAMA_MODEL} with message: {effective_user_message}")

        response = requests.post(
            f"{OLLAMA_BASE_URL.rstrip('/')}/api/chat",
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
        
        # Try to parse as single JSON first
        parsed = None
        try:
            parsed = json.loads(content)
        except json.JSONDecodeError:
            # If single parse fails, try to extract valid JSON objects
            # Handle case where Ollama returns multiple JSON objects
            import re
            json_matches = re.findall(r'\{[^{}]*\}', content)
            if json_matches:
                # Try to parse each found JSON object
                for json_str in json_matches:
                    try:
                        obj = json.loads(json_str)
                        if obj.get("intent") == "Booking_Confirmed":
                            parsed = obj
                            logger.info(f"[OLLAMA] Found Booking_Confirmed in multiple responses, using that")
                            break
                        elif parsed is None:
                            parsed = obj
                    except:
                        continue
        
        if parsed is None:
            logger.error(f"[OLLAMA] Could not parse response content: {content}")
            return {"stage": "questioning", "question": "Please describe your symptoms clearly."}

        if is_report_summary_mode:
            return sanitize_ai_output(normalize_report_summary_output(parsed))

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

    english_output = await asyncio.to_thread(call_qwen, english_input, list(history))

    # Critical: If booking is confirmed, ensure no other intent breaks the flow
    if english_output.get("intent") == "Booking_Confirmed":
        # Strip missing_info to signal completion
        english_output["missing_info"] = []
        logger.info(f"[BOOKING] Confirmed - preventing further state changes")

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


@app.post("/summarize")
async def summarize(request: SummarizeRequest):
    prompt = f"""Read this medical report
and give a simple summary in 4-6 lines.
Only include what is in the report.
Use simple words.
No headings. No bullet points.
No extra text. Just the summary.

Report:
{request.text}"""

    try:
        response = requests.post(
            f"{OLLAMA_BASE_URL.rstrip('/')}/api/chat",
            json={
                "model": OLLAMA_MODEL,
                "messages": [{"role": "user", "content": prompt}],
                "stream": False,
            },
            timeout=120,
        )

        if not response.ok:
            return {"summary": ""}

        content = response.json().get("message", {}).get("content", "")
        return {"summary": str(content).strip()}
    except Exception:
        return {"summary": ""}

# ------------------------
# Reset
# ------------------------
@app.post("/reset")
async def reset_session(session_id: str = "default_user"):
    sessions.pop(session_id, None)
    session_languages.pop(session_id, None)
    return {"message": "Session reset"}

# ------------------------
# Detect (called by C# TranslationService)
# ------------------------
class DetectRequest(BaseModel):
    text: str

@app.post("/detect")
async def detect_endpoint(request: DetectRequest):
    lang = detect_language(request.text)
    return {"language": lang}

# ------------------------
# Translate (called by C# TranslationService)
# ------------------------
class TranslateRequest(BaseModel):
    text: str
    src_lang: str
    target_lang: str

@app.post("/translate")
async def translate_endpoint(request: TranslateRequest):
    result = await asyncio.to_thread(
        run_translation, request.text, request.src_lang, request.target_lang
    )
    return {"translated_text": result}

# ------------------------
# Health
# ------------------------
@app.get("/health")
async def health_check():
    return {
        "status": "ok",
        "model": OLLAMA_MODEL,
        "models_loaded": models_loaded,
        "allowed_specialties": ALLOWED_SPECIALTIES,
        "allowed_doctors_count": len(ALLOWED_DOCTORS),
        "api_base_url": CLINIC_API_BASE_URL
    }