# app.py - Cognitive Memory Support Assistant with Persistent Storage
import os
import json
from pathlib import Path
from datetime import datetime
import streamlit as st

try:
    from groq import Groq
except ImportError:
    Groq = None

try:
    from dotenv import load_dotenv
except ImportError:
    load_dotenv = None

if load_dotenv:
    load_dotenv()

GROQ_API_KEY = os.getenv("GROQ_API_KEY", "").strip()

DATA_DIR = Path("data")
MEMORY_FILE = DATA_DIR / "memory_history.json"

st.set_page_config(
    page_title="Cognitive Memory Assistant",
    page_icon="🧠",
    layout="centered"
)

# Persistence Helpers
def ensure_data_dir():
    DATA_DIR.mkdir(parents=True, exist_ok=True)

def load_persistent_memory():
    ensure_data_dir()
    if MEMORY_FILE.exists():
        try:
            with open(MEMORY_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, list) and len(data) > 0:
                    return data
        except Exception as e:
            st.warning(f"Could not load previous memory: {e}")
    return [
        {
            "role": "system",
            "content": (
                "You are a compassionate cognitive memory assistant for elderly individuals and patients. "
                "Help them remember appointments, where daily items are kept, medicine timings, and past conversations. "
                "Keep answers empathetic, concise, and clear. Respond in the same language or mix (Hinglish/Hindi/English) that the user speaks."
            )
        }
    ]

def save_persistent_memory(messages):
    ensure_data_dir()
    try:
        with open(MEMORY_FILE, "w", encoding="utf-8") as f:
            json.dump(messages, f, ensure_ascii=False, indent=2)
    except Exception as e:
        st.error(f"Failed to save persistent memory: {e}")

def clear_all_memory():
    ensure_data_dir()
    if MEMORY_FILE.exists():
        try:
            MEMORY_FILE.unlink()
        except Exception:
            pass
    st.session_state.messages = [
        {
            "role": "system",
            "content": (
                "You are a compassionate cognitive memory assistant for elderly individuals and patients. "
                "Help them remember appointments, where daily items are kept, medicine timings, and past conversations. "
                "Keep answers empathetic, concise, and clear. Respond in the same language or mix (Hinglish/Hindi/English) that the user speaks."
            )
        }
    ]

# Initialize Session State
if "messages" not in st.session_state:
    st.session_state.messages = load_persistent_memory()

st.title("🧠 Cognitive Memory Assistant")
st.caption("AI-powered daily memory retention and recall support")

# Check Groq configuration
if not GROQ_API_KEY or Groq is None:
    st.error("⚠️ GROQ_API_KEY is missing or the Groq package is not installed.")
    st.info("Please set the `GROQ_API_KEY` in your `.env` file or environment variables to continue.")
    st.stop()

try:
    client = Groq(api_key=GROQ_API_KEY)
except Exception as e:
    st.error(f"Error initializing Groq client: {e}")
    st.stop()

# Sidebar
with st.sidebar:
    st.header("⚙️ Assistant Settings")
    st.success("✅ Connected to Groq AI")
    
    saved_count = len([m for m in st.session_state.messages if m["role"] != "system"])
    st.metric("Remembered Notes / Items", saved_count)
    
    st.markdown("### 💡 Quick Examples:")
    st.markdown("- *'Maine apne chashme kahan rakhe the?'*")
    st.markdown("- *'Dr. Sharma ki appointment kab hai?'*")
    st.markdown("- *'Mujhe shaam 7 baje blood pressure ki dawai yaad dilana.'*")
    
    st.divider()
    if st.button("🗑️ Clear All Memory", use_container_width=True):
        clear_all_memory()
        st.success("Memory cleared!")
        st.rerun()

# Display chat history
for msg in st.session_state.messages:
    if msg["role"] != "system":
        with st.chat_message(msg["role"]):
            st.write(msg["content"])

# Chat input
user_input = st.chat_input("Kuch yaad rakhna hai ya poochna hai? Ask me...")

if user_input:
    # Append & display user message
    st.session_state.messages.append({"role": "user", "content": user_input})
    with st.chat_message("user"):
        st.write(user_input)

    # Generate assistant response
    with st.chat_message("assistant"):
        with st.spinner("🧠 Yaad kar raha hoon..."):
            try:
                response = client.chat.completions.create(
                    model="llama-3.3-70b-versatile",
                    messages=st.session_state.messages,
                    temperature=0.6,
                    max_tokens=600
                )
                reply = response.choices[0].message.content
                st.write(reply)
                st.session_state.messages.append({"role": "assistant", "content": reply})
                # Persist to disk
                save_persistent_memory(st.session_state.messages)
            except Exception as e:
                st.error(f"AI Error: {e}")
                st.info("Please verify your internet connection or Groq API quota.")
