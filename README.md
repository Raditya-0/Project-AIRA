# Project AIRA (Artificial Interactive Room Assistant)

Aplikasi *companion* interaktif berbasis Live2D yang ditenagai oleh Local AI (SLM), dirancang untuk merespons percakapan secara *real-time* dan berjalan *fully offline* di PC Anda.

## Tech Stack

- **Engine:** Unity 6.3 LTS (URP)
- **Live2D:** Cubism SDK 5-r.4.1 (Character: Hiyori Momose)
- **AI Foundation:** Qwen 2.5 3B Q4_K_M (via LLMUnity / Llama.cpp)
- **STT:** Whisper (via whisper.unity) — offline speech recognition
- **TTS:** Piper TTS — offline text-to-speech
- **Emotion Classifier:** DistilBERT ONNX (opsional)

---

## Panduan Setup 

Karena batasan ukuran file di GitHub (maksimal 100MB), file model AI dan beberapa *library* mesin utama (LlamaLib) tidak disertakan secara langsung di dalam repository ini.

Ikuti panduan berikut agar AIRA bisa berjalan di komputermu:

### 1. Clone Repository

Buka terminal/CMD dan jalankan perintah ini:

```bash
git clone https://github.com/Raditya-0/Project-AIRA
```

### 2. Download Model AI Lokal

AIRA membutuhkan model **Qwen 2.5 3B (Q4\_K\_M)** sebagai otak utamanya.

- **Link Download:** [Unduh dari Hugging Face](https://huggingface.co/Qwen/Qwen2.5-3B-Instruct-GGUF?show_file_info=qwen2.5-3b-instruct-q4_k_m.gguf) (~2.2 GB)
- **Penempatan:** Setelah selesai diunduh, pastikan nama filenya adalah `qwen2.5-3b-instruct-q4_k_m.gguf` dan pindahkan file tersebut ke dalam folder:
  `Assets/StreamingAssets/qwen2.5-3b-instruct-q4_k_m.gguf`

### 3. Restore LLMUnity Dependencies

File *binary* raksasa (`.dll`) milik LLMUnity sengaja tidak di-*push* ke GitHub. Saat kamu membuka project ini untuk pertama kali:

1. Buka project menggunakan **Unity 6.3 LTS (6000.3.11f1)**.
2. Tunggu beberapa saat biarkan Unity melakukan **Resolving Packages**.
3. Unity/LLMUnity biasanya akan otomatis mengunduh ulang file `.dll` yang hilang di belakang layar.
4. **Troubleshooting:** Jika terjadi *error* Llama.cpp tidak ditemukan saat di-Play, buka **Window -> Package Manager**, cari **LLMUnity**, lalu lakukan *Remove* dan tambahkan ulang via Git URL (`https://github.com/undreamai/LLMUnity.git`) untuk memancing unduhan *library*-nya.

### 4. Download & Setup Piper TTS

AIRA menggunakan Piper TTS untuk suara karakter.

- **Download Piper:** [piper_windows_amd64.zip](https://github.com/rhasspy/piper/releases)
- **Ekstrak** dan ambil file `piper.exe`
- **Penempatan:** `Assets/StreamingAssets/Piper/piper.exe`
- **Download voice model Amy:**
  [en_US-amy-medium.onnx](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/amy/medium)
  (~60MB) — download keduanya: `.onnx` dan `.onnx.json`
- **Penempatan:**
  `Assets/StreamingAssets/Piper/en_US-amy-medium.onnx`
  `Assets/StreamingAssets/Piper/en_US-amy-medium.onnx.json`

### 5. Download & Setup Whisper STT

AIRA menggunakan **Whisper** (via [whisper.unity](https://github.com/Macoron/whisper.unity)) untuk speech recognition offline — menggantikan Vosk.

**Install package whisper.unity:**

1. Buka **Window -> Package Manager**
2. Klik **+** -> **Add package from git URL**
3. Masukkan: https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity

**Download model Whisper:**

- **Rekomendasi:** `ggml-small.en-q5_1.bin` — balance antara speed dan akurasi
- **Link Download:** [Hugging Face — ggerganov/whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp/tree/main)
- **Penempatan:** `Assets/StreamingAssets/ggml-small.en-q5_1.bin`

**Setup di Unity Inspector:**

1. Di GameObject `STTManager`, tambah component `WhisperManager`
2. Tambah component `MicrophoneRecord`
3. Di `WhisperManager` -> `Model Path` -> isi path ke file `.bin` di StreamingAssets
4. Di `MicrophoneRecord` -> aktifkan `Use Vad`, `Vad Stop`, `Drop Vad Part`
5. `Vad Stop Time` -> `1.5`

> **Catatan:** Jika mic tidak terdeteksi dengan benar, gunakan **Microphone Selector dropdown** di UI untuk memilih input device yang sesuai.

### 6. (Opsional) Setup Emotion Classifier

Fitur klasifikasi emosi membutuhkan model ONNX tambahan.
Bisa dilewati — fitur ini bisa dimatikan via toggle
`AIRASettings -> Use Emotion Classifier` di Inspector.

Jika ingin mengaktifkan:

- **Download model:** [DistilBERT Emotion Classifier](https://www.kaggle.com/models/raditya0/distilbert-emotion-classifier)
- **Penempatan:** 
  - `Assets/StreamingAssets/EmotionClassifier/vocab.txt`
  - `Assets/StreamingAssets/EmotionClassifier/emotion_labels.json`
  - `Assets/Model/model.onnx`

### 7. Buka Scene & Jalankan

- Di dalam panel Project Unity, masuk ke folder: `Assets/Scenes/`
- Buka file `SampleScene.unity`
- Tekan tombol **Play** dan mulailah mengobrol dengan AIRA!

---

## Mini-Games

AIRA saat ini memiliki dua mini-game yang bisa dimainkan bersama:

| Game                         | Status      | Deskripsi                                   |
| ---------------------------- | ----------- | ------------------------------------------- |
| **Head's Up!**         | Playable    | Tebak kata — Aira kasih clue, player tebak |
| **Platformer Coop**    | Playable | Puzzle platformer kooperatif bersama Aira   |
| **Space Shooter Coop** | Playable | Top-down shooter, Aira sebagai co-pilot     |

---

## Fitur Utama

- **Free Chat** — ngobrol natural dengan Aira via teks atau suara
- **Mini-Games** — main bareng Aira dengan AI commentary real-time
- **Memory System** — Aira ingat percakapan sebelumnya antar sesi
- **Live2D Expressions** — ekspresi wajah reaktif ([HAPPY], [SAD], [SURPRISED], dll)
- **TTS + Lip-sync** — Aira berbicara dengan sinkronisasi bibir
- **STT Offline** — bicara langsung ke Aira tanpa internet
- **Emotion Classifier** — deteksi emosi user untuk respons yang lebih empatik (opsional)

---

## Kredit

- **Character:** Hiyori Momose © Live2D Inc.
- **SDK:** Live2D Cubism SDK for Unity
- **AI Runtime:** [LLMUnity](https://github.com/undreamai/LLMUnity) by undreamai
- **STT:** [whisper.unity](https://github.com/Macoron/whisper.unity) by Macoron
- **TTS:** [Piper](https://github.com/rhasspy/piper) by rhasspy
