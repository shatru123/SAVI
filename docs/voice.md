# Voice Architecture

## Zero-Cost Web Speech API
SAVI integrates the standard W3C Web Speech API directly in the browser:
- **Speech-to-Text (STT)**: Uses browser `SpeechRecognition` / `webkitSpeechRecognition`.
- **Text-to-Speech (TTS)**: Uses `window.speechSynthesis`.
- Zero latency overhead, zero cloud audio streaming costs, zero third-party API dependencies.

## Voice State Machine
- `Idle`: Central core breathes slowly.
- `Listening`: Microphone active, visualizer reactive to voice.
- `Processing`: Rotating technical rings during intent reasoning.
- `Searching`: Expanding data rings while querying providers.
- `Executing`: Sequential technical pulse during tool actions.
- `Speaking`: Concentric glow matching speech synthesis output.
- `Error`: Amber/red indicator for diagnostics.
