================================================================
  AIRA — LLMUnity Setup Guide
  https://github.com/undreamai/LLMUnity
================================================================

LLMUnity Architecture
─────────────────────
  LLM component       = model engine (loads the .gguf, manages threads/GPU)
  LLMCharacter component = chat interface (sends prompts, receives replies)

Both components must be in the scene.
LLMManager only needs a reference to LLMCharacter.


STEP 1 — Install LLMUnity via Package Manager
──────────────────────────────────────────────
1. Open Unity.
2. Window → Package Manager
3. Click the [+] button (top-left) → "Add package from git URL…"
4. Paste:
     https://github.com/undreamai/LLMUnity.git
5. Click [Add] and wait for import to finish.


STEP 2 — Create the LLM Engine GameObject
──────────────────────────────────────────
1. Hierarchy → right-click → Create Empty → rename: "LLM_Engine"
   (keep it separate from _Managers so it is easy to find)
2. Inspector → [Add Component] → search "LLM" → select "LLM".
3. Configure the LLM component:

     Option A — Download model from Inspector
       Click [Download Model] inside the LLM component panel,
       then pick a GGUF (e.g. Qwen2.5-3B-Instruct-Q4_K_M).

     Option B — Use existing local file
       Model field: drag  Assets/Models/qwen2.5-3b-q4.gguf

     Recommended settings:
       Context Size : 2048
       Batch Size   : 512
       GPU Layers   : 0   (CPU only — raise if you have VRAM)
       Threads      : -1  (auto-detect)


STEP 3 — Add LLMCharacter to LLM_Engine
────────────────────────────────────────
1. Select the "LLM_Engine" GameObject.
2. Inspector → [Add Component] → search "LLMCharacter" → select it.
3. Configure the LLMCharacter component:

     LLM          : drag the LLM component (from Step 2)
     Stream        : TRUE
     Player Name   : User
     AI Name       : Aira
     System Prompt : (leave blank — MemoryManager injects it at runtime)
     Save History  : FALSE  ← IMPORTANT: we manage history in MemoryManager


STEP 4 — Assign LLMCharacter to LLMManager
────────────────────────────────────────────
1. Select the "_Managers" GameObject.
2. Find the LLMManager component in the Inspector.
3. Locate the field:
     LLMUnity — _llmCharacter
4. Drag the LLMCharacter component (from LLM_Engine) into that field.


STEP 5 — Enable LLMUnity in Code
──────────────────────────────────
1. Edit → Project Settings → Player
2. Other Settings → Scripting Define Symbols
3. Add:
     LLMUNITY_AVAILABLE
4. Press Enter / click Apply. Unity will recompile.


STEP 6 — Verify
────────────────
Press Play. Watch the Console for:
  [LLMManager] LLMUnity is ready.

Type a message and press Enter (or Send).
AIRA should reply with a real model response instead of the
stub "[HAPPY] Hei! Ini jawaban stub…" messages.


TROUBLESHOOTING
───────────────
"LLMCharacter component is not assigned in the Inspector"
  → Redo Step 4. Drag the LLMCharacter component, not the
    LLMManager or LLM component.

"LLMCharacter failed to initialise: …"
  → Check the model path in the LLM component is correct.
  → Make sure the LLMCharacter's LLM field points to LLM_Engine.

"CS1061 'LLM' does not contain 'Chat'"
  → You may have dragged the LLM component instead of
    LLMCharacter into _llmCharacter. Fix: assign LLMCharacter.

Timeout / state goes to ERROR on first message
  → First response takes longer due to model warmup.
    Increase  llm_timeout_seconds  in Assets/Data/settings.json
    from 15 to 60 for the initial session.

Responses look like repeated context instead of a clean reply
  → In LLMCharacter Inspector: set Save History = FALSE.
    MemoryManager owns the conversation history; LLMCharacter
    should treat every call as a fresh stateless completion.

Stub responses still appearing after Step 5
  → Check for typos in LLMUNITY_AVAILABLE.
  → Wait for the spinning recompile indicator to stop.

================================================================
