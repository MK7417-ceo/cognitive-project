# game_reaction.py - Automatic Reaction Test with URL Bridge & Persistent Scoring
import json
from pathlib import Path
import streamlit as st
import pandas as pd
from streamlit.components.v1 import html

DATA_DIR = Path("data")
SCORES_FILE = DATA_DIR / "reaction_scores.json"

st.set_page_config(page_title="Reaction Test Game", page_icon="⚡", layout="centered")

def ensure_data_dir():
    DATA_DIR.mkdir(parents=True, exist_ok=True)

def load_saved_scores():
    ensure_data_dir()
    if SCORES_FILE.exists():
        try:
            with open(SCORES_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
                if isinstance(data, list):
                    return [float(x) for x in data]
        except Exception:
            pass
    return []

def save_scores(scores):
    ensure_data_dir()
    try:
        with open(SCORES_FILE, "w", encoding="utf-8") as f:
            json.dump(scores, f, indent=2)
    except Exception as e:
        st.error(f"Error saving scores: {e}")

# Initialize session scores
if "scores" not in st.session_state:
    st.session_state.scores = load_saved_scores()

# Automatic bridge: Check if score arrived via URL query parameters
query_params = st.query_params
if "rt" in query_params:
    try:
        new_rt = float(query_params["rt"])
        if 0.05 <= new_rt <= 10.0:
            st.session_state.scores.append(round(new_rt, 3))
            save_scores(st.session_state.scores)
            st.toast(f"⚡ Recorded: {new_rt:.3f}s", icon="✅")
    except (ValueError, TypeError):
        pass
    finally:
        st.query_params.clear()
        st.rerun()

st.title("⚡ Reaction Test Game")
st.write("Click as **FAST** as possible when the box turns **GREEN**!")

# Top metric summary
col1, col2, col3 = st.columns(3)
if st.session_state.scores:
    best_score = min(st.session_state.scores)
    avg_score = sum(st.session_state.scores) / len(st.session_state.scores)
    col1.metric("Best Reaction", f"{best_score:.3f}s")
    col2.metric("Average Reaction", f"{avg_score:.3f}s")
    col3.metric("Total Attempts", len(st.session_state.scores))
else:
    col1.metric("Best Reaction", "-")
    col2.metric("Average Reaction", "-")
    col3.metric("Total Attempts", "0")

# High-precision automated HTML5 / JS Game Component
game_html = """
<div id="game-container" style="text-align: center; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;">
    <div id="color-box" style="
        width: 100%;
        height: 280px;
        background-color: #4A5568;
        border-radius: 16px;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        user-select: none;
        transition: background-color 0.05s ease;
        margin-bottom: 16px;
        box-shadow: 0 8px 24px rgba(0,0,0,0.15);
    ">
        <h2 id="box-text" style="color: white; font-size: 32px; margin: 0;">⚡ CLICK 'START' TO PLAY</h2>
        <p id="box-sub" style="color: #E2E8F0; font-size: 16px; margin: 8px 0 0 0;">Wait for green, then click as fast as possible</p>
    </div>
    <button id="main-btn" style="
        padding: 14px 32px;
        font-size: 18px;
        font-weight: bold;
        background-color: #3182CE;
        color: white;
        border: none;
        border-radius: 10px;
        cursor: pointer;
        box-shadow: 0 4px 12px rgba(49, 130, 206, 0.4);
    ">🎮 START GAME</button>
</div>

<script>
    let state = 'idle'; // idle | waiting | ready | done
    let stimulusTime = 0;
    let timerId = null;

    const box = document.getElementById('color-box');
    const boxText = document.getElementById('box-text');
    const boxSub = document.getElementById('box-sub');
    const btn = document.getElementById('main-btn');

    function startRound() {
        if (timerId) clearTimeout(timerId);
        state = 'waiting';
        box.style.backgroundColor = '#E53E3E'; // Red
        boxText.textContent = '🔴 WAIT FOR GREEN...';
        boxText.style.color = '#FFFFFF';
        boxSub.textContent = 'Do not click yet!';
        btn.style.display = 'none';

        const delay = Math.random() * 2500 + 1500; // 1.5s to 4.0s random delay
        timerId = setTimeout(() => {
            state = 'ready';
            stimulusTime = performance.now();
            box.style.backgroundColor = '#38A169'; // Green
            boxText.textContent = '🟢 CLICK NOW!';
            boxText.style.color = '#000000';
            boxSub.textContent = 'Fast! Click the box!';
        }, delay);
    }

    function handleBoxClick() {
        if (state === 'waiting') {
            // Clicked too early
            if (timerId) clearTimeout(timerId);
            state = 'idle';
            box.style.backgroundColor = '#DD6B20'; // Orange
            boxText.textContent = '❌ TOO EARLY!';
            boxSub.textContent = 'Wait for green before clicking. Try again!';
            btn.style.display = 'inline-block';
            btn.textContent = '🔄 TRY AGAIN';
        } else if (state === 'ready') {
            const reactionMs = performance.now() - stimulusTime;
            const reactionSec = (reactionMs / 1000).toFixed(3);
            state = 'done';

            box.style.backgroundColor = '#2B6CB0'; // Blue result
            boxText.textContent = '⏱️ ' + reactionSec + 's';
            boxText.style.color = '#FFFFFF';
            boxSub.textContent = 'Auto-saving attempt...';

            btn.style.display = 'inline-block';
            btn.textContent = '🎮 PLAY AGAIN';

            // Auto-bridge: send score directly to Streamlit via URL parameter
            setTimeout(() => {
                const targetUrl = new URL(window.parent.location.href);
                targetUrl.searchParams.set('rt', reactionSec);
                window.parent.location.href = targetUrl.toString();
            }, 350);
        }
    }

    btn.onclick = () => { startRound(); };
    box.onclick = () => { handleBoxClick(); };
</script>
"""

html(game_html, height=400)

# Score Table & History
if st.session_state.scores:
    st.subheader("📊 Score History")
    df = pd.DataFrame({
        "Attempt": range(1, len(st.session_state.scores) + 1),
        "Reaction Time (s)": [f"{s:.3f}" for s in st.session_state.scores],
        "Performance Rating": [
            "⚡ Elite" if s < 0.22 else "🟢 Great" if s < 0.32 else "🟡 Average" if s < 0.45 else "🔴 Slow"
            for s in st.session_state.scores
        ]
    })
    st.dataframe(df, use_container_width=True)

    if st.button("🗑️ Clear All Scores"):
        st.session_state.scores = []
        save_scores([])
        st.rerun()

# Optional manual input for offline or restricted environments
with st.expander("✍️ Manual Attempt Entry (Optional)"):
    manual_val = st.number_input("Time in seconds", min_value=0.0, max_value=10.0, step=0.001, format="%.3f")
    if st.button("Save Manual Entry"):
        if manual_val > 0:
            st.session_state.scores.append(round(float(manual_val), 3))
            save_scores(st.session_state.scores)
            st.success(f"Saved: {manual_val:.3f}s")
            st.rerun()
