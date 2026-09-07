# Tool Framework

## Available Tools
1. **`file_system`**: Lists directory files, reads file content, safely writes files, and deletes files (requires approval).
2. **`terminal`**: Executes safe command line instructions while blocking catastrophic blacklist patterns (`rm -rf /`, `mkfs`, fork bombs).
3. **`browser`**: Navigates URLs, extracts clean text content, respects robots/paywalls, and gracefully handles bot blocking.
4. **`document_reader`**: Summarizes and inspects `.txt`, `.md`, `.json`, `.csv`, `.xml`, `.cs`, `.html`, `.log` files.
5. **`notification`**: Delivers system and UI notifications.
