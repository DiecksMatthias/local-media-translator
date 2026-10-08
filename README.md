# LocalMediaTranslator

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture-informational)](#architecture)

An automated .NET CLI and processing pipeline that transcribes dialogue from media files using local or remote Whisper models, translates subtitles via LLMs with contextual batching, generates standardized `.srt` subtitle tracks (single or dual-language), and integrates with self-hosted GraphQL media servers.

---

## 🚀 Key Features

- **End-to-End Pipeline**: Unified pipeline orchestrating audio extraction, speech-to-text, LLM batch translation, and subtitle generation.
- **Flexible Speech-to-Text**: Supports both offline local Whisper (`ggml` models via `Whisper.net`) and remote GPU Whisper HTTP microservices (Speaches / Faster-Whisper).
- **Anti-Hallucination**: Whisper VAD filtering, `condition_on_previous_text=false`, compression ratio thresholds, and deduplication filter to minimize hallucinated repetitions.
- **Context-Aware LLM Translation**: Batches dialogue cues with sliding-window context to preserve conversational nuance, tone, and speaker cues.
- **Resilient JSON Repair**: LLM output parsing handles markdown fenced blocks, truncated JSON, malformed keys, and trailing commas.
- **Dual-Language Subtitles**: Generates stacked subtitles containing both original and translated text for language learners.
- **GraphQL Media Server Sync**: Built-in client to query media libraries, discover scenes missing subtitles, and trigger background metadata scans.
- **Bidirectional Path Mapping**: Translates container paths to host paths and back for seamless Docker/host integration.
- **Timing Normalization**: Adjusts subtitle display durations based on reading speed, enforces minimum/maximum durations, and resolves overlapping segments.
- **Rich Terminal UX**: Multi-step progress bars and status reporting powered by [Spectre.Console](https://spectreconsole.net/).
- **Modular Clean Architecture**: Loosely coupled domain and infrastructure layers with pluggable extractors, transcribers, translators, and media clients.

---

## 🏗️ Architecture

The solution follows clean architectural separation:

```text
LocalMediaTranslator/
├── src/
│   ├── LocalMediaTranslator.Core/             # Domain models, enums, and interface contracts
│   │   ├── Interfaces/                        # IMediaTranslationPipeline, IAudioExtractor, ITranscriber, ITranslator, IMediaServerClient, etc.
│   │   ├── Models/                            # PipelineExecutionOptions, PipelineProgressReport, SubtitleItem, SubtitleTrack, MediaScene, etc.
│   │   └── Utilities/                         # PathTransformer (bidirectional path mapping), SubtitleTimingNormalizer
│   ├── LocalMediaTranslator.Infrastructure/   # Concrete service implementations
│   │   └── Services/                          # MediaTranslationPipeline, FFmpegAudioExtractor, LocalWhisperTranscriber,
│   │                                          # HttpWhisperTranscriber, LlmTranslator, SrtSubtitleWriter, GraphQlMediaClient
│   └── LocalMediaTranslator.Cli/              # Spectre.Console CLI interface, commands, and DI composition root
└── tests/
    └── LocalMediaTranslator.Tests/            # Unit and integration test suites
```

---

## 📋 Prerequisites

1. **.NET 10.0 SDK** (or later)
2. **FFmpeg** installed and accessible in system `PATH`
3. **Speech-to-Text Backend**:
   - Local Whisper `ggml` model file, or
   - Remote HTTP Whisper microservice (e.g. Speaches / Faster-Whisper on GPU server)
4. **LLM Translation Endpoint**: Local (Ollama) or OpenAI-compatible completion API

---

## 💻 CLI Usage

### 1. Translate a Video File

```bash
# Basic translation (creates video.srt alongside the video)
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4

# Generate dual-language subtitles (source + target text)
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4 --dual-language

# Custom output destination
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4 -o /path/to/output.srt
```

#### `translate` Options

| Option | Shorthand | Description | Default |
| :--- | :--- | :--- | :--- |
| `<INPUT_PATH>` | — | Path to the source video or audio file | *(Required)* |
| `--model` | `-m` | Path to the local `ggml` Whisper model | `ggml-base.bin` |
| `--dual-language` | `-d` | Generates stacked original & translated subtitle cues | `false` |
| `--output` | `-o` | Custom output `.srt` file path | `<input_path>.srt` |

---

### 2. Discover Media on Server

Query connected media servers for items missing subtitles or matching tags:

```bash
# Search for items missing subtitles
dotnet run --project src/LocalMediaTranslator.Cli -- discover --missing-captions

# Filter by tag and search query
dotnet run --project src/LocalMediaTranslator.Cli -- discover --tag "NeedSubtitles" --query "sample"
```

---

### 3. Translate a Scene by ID

Fetch a scene from the media server by its ID, translate it, and optionally trigger a metadata rescan:

```bash
# Translate scene with ID 12345
dotnet run --project src/LocalMediaTranslator.Cli -- translate-scene 12345

# With dual-language output and automatic rescan
dotnet run --project src/LocalMediaTranslator.Cli -- translate-scene 12345 --dual-language --rescan
```

#### `translate-scene` Options

| Option | Shorthand | Description | Default |
| :--- | :--- | :--- | :--- |
| `<SCENE_ID>` | — | ID of the scene to fetch from the media server | *(Required)* |
| `--server-type` | `-s` | Target media server type (e.g. Stash) | From config |
| `--dual-language` | `-d` | Generate dual-language subtitles | `false` |
| `--rescan` | `-r` | Trigger a media server metadata rescan after writing | `false` |
| `--output` | `-o` | Custom output `.srt` path | `<scene_file>.srt` |
| `--local-model` | `-m` | Path to the ggml Whisper model (if using local Whisper) | — |
| `--server-model` | — | Name of the Whisper model on the remote server | — |

---

## 🧪 Testing

The test suite includes unit tests with mocked HTTP handlers and integration tests for audio extraction, transcription, and translation:

```bash
dotnet test
```

Test coverage includes:
- Pipeline deduplication and adjacent cue merging
- HTTP Whisper transcriber (anti-hallucination parameters, VAD, CUDA OOM handling, model unloading)
- LLM translator (resilient JSON repair, batch translation, context windows, failure handling)
- GraphQL media client (scene queries, filter mapping, metadata scan triggers)
- Path transformer (bidirectional mapping, longest-prefix matching)
- Subtitle timing normalization (duration clamping, overlap resolution, reading speed)
- CLI command registration (option validation, help rendering)

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
