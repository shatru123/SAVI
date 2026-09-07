# Long-Term Memory System

## Memory Categories
- `Preference`: User style choices (e.g. "prefers concise answers", "prefers dark theme").
- `PersonalContext`: Personal facts (e.g. "lives in Seattle", "works as software engineer").
- `Project`: Active projects (e.g. "building SAVI on .NET 10").
- `Task`: Active goals and tasks.
- `ConversationFact`: Factual notes from prior conversations.
- `Interest`: Topics of user interest.
- `Instruction`: Explicit behavioral instructions.
- `TemporaryContext`: Ephemeral context with expiration timestamps.

## Natural Extraction
SAVI detects implicit user statements:
- "Remember that I prefer concise answers" -> Stored as `Preference`.
- "I am working on the SAVI architecture" -> Stored as `Project`.
- "My name is Shatru" -> Stored as `PersonalContext`.

## User Data Ownership
- The user can view, edit, search, and delete all memories via the `/memory` view in the Web HUD.
- "Clear Category" and "Wipe All Memory" provide instant data sovereignty.
