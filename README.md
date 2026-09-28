# LocalMediaTranslator

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture-informational)](#architecture)

An automated .NET CLI and processing pipeline that transcribes dialogue from local video files using local Whisper models, translates subtitles via LLMs with contextual prompt tuning, generates standardized `.srt` subtitle tracks (single or dual-language), and integrates with self-hosted GraphQL media servers.

---

## 🚀 Key Features

- **End-to-End Pipeline**: Single-command execution covering audio extraction, speech-to-text, LLM batch translation, and subtitle formatting.
- **Local Speech-to-Text**: Offline, privacy-preserving transcription via local Whisper (`ggml` models).
- **Context-Aware LLM Translation**: Batches dialogue cues to preserve conversational nuance, honor tone/gender cues, and produce natural subtitles.
- **Dual-Language Subtitles**: Option to generate side-by-side / stacked subtitles containing both original and translated text for language learners.
- **GraphQL Media Server Sync**: Built-in client to query media libraries, search scenes by filter, and trigger background metadata scans.
- **Rich Terminal UX**: Live progress bars and multi-step pipeline tracking powered by [Spectre.Console](https://spectreconsole.net/).
- **Modular Clean Architecture**: Loosely coupled interfaces for audio extractors, transcribers, translators, and media server clients.

---

## 🏗️ Architecture

The solution follows clean architectural separation:

```text
LocalMediaTranslator/
├── src/
│   ├── LocalMediaTranslator.Core/             # Domain models, enums, and interfaces
│   │   ├── Interfaces/                        # IAudioExtractor, ITranscriber, ITranslator, IMediaServerClient, etc.
│   │   └── Models/                            # SubtitleItem, SubtitleTrack, MediaScene, TranslationOptions, etc.
│   ├── LocalMediaTranslator.Infrastructure/   # Concrete implementations
│   │   ├── Services/                          # FFmpegAudioExtractor, LocalWhisperTranscriber, LlmTranslator, SrtSubtitleWriter
│   │   └── Clients/                           # GraphQlMediaClient (GraphQL API client)
│   └── LocalMediaTranslator.Cli/              # Spectre.Console CLI interface and DI wiring
└── tests/
    └── LocalMediaTranslator.Tests/            # Unit and integration test suites
```

---

## 📋 Prerequisites

1. **.NET 10.0 SDK** (or later)
2. **FFmpeg** installed and accessible in your system `PATH`
3. **Whisper ggml Model** (e.g., `ggml-base.bin`, `ggml-small.bin`, `ggml-medium.bin` or `ggml-large-v3.bin`)
4. **OpenAI / OpenAI-compatible API Key** (for LLM translation)

---

## 🛠️ Installation & Setup

```bash
# Clone the repository
git clone https://github.com/<your-username>/local-media-translator.git
cd local-media-translator

# Build solution and run tests
dotnet build
dotnet test
```

### Configuration (User Secrets)
Configure your LLM API credentials securely via .NET User Secrets:

```bash
cd src/LocalMediaTranslator.Cli
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key-here"
```

---

## 💻 CLI Usage

### Translate a Single Video File

```bash
# Basic translation (creates video.srt alongside the video)
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4 -m models/ggml-base.bin

# Generate dual-language subtitles (source + target text)
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4 -m models/ggml-base.bin --dual-language

# Custom output destination
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4 -m models/ggml-base.bin -o /path/to/output.srt
```

### Command Options

| Option | Shorthand | Description | Default |
| :--- | :--- | :--- | :--- |
| `<INPUT_PATH>` | — | Path to the source video or audio file | *(Required)* |
| `--model` | `-m` | Path to the local `ggml` Whisper model | `ggml-base.bin` |
| `--dual-language` | `-d` | Generates stacked original & translated subtitle cues | `false` |
| `--output` | `-o` | Custom output `.srt` file path | `<input_path>.srt` |

---

## 🧪 Testing

The test suite includes unit tests with mocked HTTP handlers and integration tests for audio extraction, transcription, and translation:

```bash
dotnet test
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
