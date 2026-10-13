# LocalMediaTranslator

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

LocalMediaTranslator is a .NET CLI that turns media files into subtitles. It pulls the audio out
with ffmpeg, transcribes it with Whisper, translates the cues through an LLM, and writes an `.srt`
file next to the video. It can also talk to a GraphQL media server, so a job can start from a
library entry instead of a file path.

## What it does

The pipeline runs in one pass: ffmpeg pulls a 16 kHz mono track, Whisper transcribes it, the LLM
translates the cues in batches, and the result lands as an `.srt` next to the video. Each stage sits
behind an interface, so the transcriber and the translator can be swapped out.

Transcription runs over HTTP against a remote GPU Whisper service (Speaches or another
faster-whisper deployment), or locally through Whisper.net when you point it at a ggml model file.
Exactly one of the two must be configured: pass `--server-model` for the remote service or
`--local-model` for a local file, and the backend is picked from whichever is set. Translation goes
to any OpenAI-compatible endpoint, usually a local Ollama instance, with a rolling window of the
preceding lines passed along as context so tone and speaker references stay consistent across a
conversation.

The transcription settings are tuned against Whisper's habit of inventing dialogue. VAD filtering
drops non-speech windows before decoding, `condition_on_previous_text=false` stops a bad guess from
cascading into the next frame, and compression-ratio and repetition thresholds catch the rest.
Timing is normalized afterwards, so a cue does not linger over trailing silence and adjacent cues do
not overlap. Output is a plain `.srt`, or a stacked dual-language track with the original line above
the translation.

With a media server configured, `discover` lists entries by tag or search term without touching
anything on disk, and `translate-scene` runs the pipeline on one item by ID.

## Architecture

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
│   └── LocalMediaTranslator.Cli/              # Spectre.Console CLI, commands, and DI composition root
└── tests/
    └── LocalMediaTranslator.Tests/            # Unit and integration test suites
```

## Prerequisites

- .NET 10 SDK
- ffmpeg on the PATH
- A Whisper HTTP endpoint (Speaches or another faster-whisper deployment) with a model loaded
- An OpenAI-compatible completion endpoint for translation, such as Ollama

Endpoints and API keys are read from `appsettings.json`. Per-machine overrides go in
`appsettings.local.json` (gitignored) or environment variables, which is where anything
that should not be committed belongs. The `set` command writes the local file; env vars
take precedence over it.

## Usage

### Translate a file

```bash
# Writes video.srt next to the input
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4

# Stack the original text above the translation
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4 --dual-language

# Write to a specific path
dotnet run --project src/LocalMediaTranslator.Cli -- translate path/to/video.mp4 -o /path/to/output.srt
```

| Option | Shorthand | Description | Default |
| :--- | :--- | :--- | :--- |
| `<INPUT_PATH>` | — | Path to the video or audio file | *Required* |
| `--local-model` | `-m` | Path to a local ggml Whisper model. Selects the local backend | — |
| `--server-model` | — | Model name on the remote Whisper server. Selects the remote backend | From config |
| `--dual-language` | `-d` | Stack the original text above the translation | `false` |
| `--output` | `-o` | Output `.srt` path | Next to the input |

### Discover scenes

`discover` queries the media server and prints the matches. It does not modify files.

```bash
# Scenes without subtitle tracks
dotnet run --project src/LocalMediaTranslator.Cli -- discover --missing-captions-only

# By tag and search term
dotnet run --project src/LocalMediaTranslator.Cli -- discover --tag "NeedSubtitles" --search "sample"
```

| Option | Shorthand | Description | Default |
| :--- | :--- | :--- | :--- |
| `--search` | `-q` | Search term | — |
| `--tag` | `-t` | Filter by tag name. Repeatable | — |
| `--missing-captions-only` | — | Only scenes without subtitle tracks | — |
| `--limit` | `-l` | Maximum scenes to fetch | — |
| `--server-type` | `-s` | Media server type (e.g. Stash, Jellyfin) | From config |
| `--endpoint` | `-e` | Media server URL (e.g. `http://localhost:9999/graphql`) | From config |
| `--api-key` | `-k` | API key or bearer token | From config |

### Translate a scene by ID

Fetches a scene from the media server, runs the pipeline on its file, and can rescan the server
afterwards.

```bash
dotnet run --project src/LocalMediaTranslator.Cli -- translate-scene 12345

dotnet run --project src/LocalMediaTranslator.Cli -- translate-scene 12345 --dual-language --rescan
```

| Option | Shorthand | Description | Default |
| :--- | :--- | :--- | :--- |
| `<SCENE_ID>` | — | Scene ID to fetch from the media server | *Required* |
| `--server-type` | `-s` | Media server type (e.g. Stash) | From config |
| `--dual-language` | `-d` | Stack the original text above the translation | `false` |
| `--rescan` | `-r` | Trigger a metadata rescan after the subtitles are written | `false` |
| `--output` | `-o` | Output `.srt` path | Next to the video |
| `--local-model` | `-m` | Path to a local ggml Whisper model. Selects the local backend | — |
| `--server-model` | — | Model name on the remote Whisper server. Selects the remote backend | From config |

### Set a configuration value

Writes one key into `appsettings.local.json`, the gitignored overlay on top of `appsettings.json`. Keys are validated against the options classes first, so a typo is caught instead of writing a key nothing reads. The known sections are `Transcription`, `Translation`, and `MediaServer`; a third segment addresses a dictionary entry, e.g. `MediaServer:PathMappings:/data`.

```bash
dotnet run --project src/LocalMediaTranslator.Cli -- set Transcription:ServerModel large-v3

# Write somewhere else, e.g. a mounted volume in a container
dotnet run --project src/LocalMediaTranslator.Cli -- set Transcription:ServerModel large-v3 --file /config/appsettings.local.json
```

The target file resolves in order: `--file`, then the `LMT_CONFIG_FILE` environment variable, then `appsettings.local.json` next to the binary. A relative `--file` path is resolved by the process, not against the binary directory.

### Run in a container

A `Dockerfile` at the repo root builds an image with the .NET runtime, ffmpeg, and the Whisper
native runtime bundled, so the host only needs Docker plus reachable Whisper and LLM endpoints.
The image is built locally for now; nothing is published to a registry yet.

```bash
docker build -t local-media-translator:dev .
```

Then pass the endpoints in as environment variables, using a double underscore wherever the
JSON config nests:

```bash
docker run --rm -it --network host --user $(id -u):$(id -g) \
  -e Transcription__ServerModel=Systran/faster-whisper-large-v3 \
  -e Transcription__Endpoint=http://your-whisper-host:8000 \
  -e Translation__Endpoint=http://your-llm-host:11434 \
  -e MediaServer__Endpoint=http://your-media-server:9999/graphql \
  -e MediaServer__ApiKey=... \
  -v /path/to/your/media:/media \
  local-media-translator:dev translate /media/video.mp4
```

Two things about that command are easy to get wrong. The media library mounts at `/media`
inside the container, so file arguments are container paths (`/media/video.mp4`), not the host
path you would type outside Docker. And `--user $(id -u):$(id -g)` matters: without it the
container runs as root and the `.srt` it writes next to the video lands root-owned. The image
seeds its config volume with a `/data` → `/media` path mapping so media-server paths resolve
with no first-run setup; a different prefix is a one-time `set` away.

Instead of a long `docker run` every time, `compose.yaml` wraps the same image into a per-command
container. Copy `.env.example` to `.env`, fill in the endpoints, the uid/gid the container runs
as, the host directory to mount at `/media`, and the name of an existing Docker network (the one
your media server lives on, so container-name endpoints resolve — compose will not create it):

```bash
cp .env.example .env    # edit the endpoints, LMT_UID/LMT_GID, LMT_MEDIA_DIR, LMT_NETWORK

docker compose run --rm cli discover --missing-captions-only
docker compose run --rm --build cli translate /media/video.mp4
```

Each invocation starts a fresh container from the current image and removes it on exit; `--build`
rebuilds after source changes. Config lives in a named volume rather than a bind mount, so values
written by `set` persist between runs — Docker initializes the volume with the image's ownership,
which is what keeps `set` working as an arbitrary uid.

In both forms, `LMT_CONFIG_FILE` points at the config file the CLI reads and writes
(`/config/appsettings.local.json` inside the container). Values set inside a container persist
through it; environment variables take precedence over the file, so `.env` always wins.

## Testing

Unit tests mock the HTTP handlers; integration tests shell out to real ffmpeg and 
download a Whisper ggml model.

The integration tests carry `Category=Integration`, so the fast offline run skips them:

```bash
dotnet test --filter "Category!=Integration"
```

To run everything, including the model download:

```bash
dotnet test
```

The integration suite needs ffmpeg on the PATH (with libx264 for the audio-extraction test) and
network access to fetch the model.

Coverage includes pipeline deduplication and cue merging, the HTTP Whisper transcriber
(anti-hallucination parameters, VAD, CUDA OOM handling, model unloading), the LLM translator
(resilient parsing, batching, context windows, failure handling), the GraphQL media client, path
transformer, subtitle timing normalization, config-target resolution, and CLI command
registration.

## License

MIT. See [LICENSE](LICENSE).
