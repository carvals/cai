# Job Tracker – UnoApp4

Purpose: a clear, ordered plan for a new engineer to reproduce and extend the app from scratch, based on `TUTORIAL.md` and `spec.md`.

Conventions
- Files and paths use backticks.
- Each task defines prerequisites, steps, acceptance criteria, and references.
- Run commands from the repository root unless stated otherwise.

---

## Phase 0 — Environment & Repo

1) Validate toolchain
- Prereqs: .NET 9 SDK, IDE (Rider/VS), Uno Platform workloads.
- Steps:
  - `DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet --info`
  - Verify Uno templates/workloads if needed.
- Acceptance: The command runs; IDE can open solution.

2) Restore and build
- Steps:
  - `DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build UnoApp4.sln -c Debug`
- Acceptance: Build succeeds. Output DLL produced under `UnoApp4/bin/...`.
- References: `TUTORIAL.md` (Build and run), `spec.md` (Build & Run Commands)

---

## Phase 1 — Base Layout (Main Page)

3) Implement 4-column layout
- Prereqs: Solution builds.
- Steps:
  - Edit `UnoApp4/Presentation/MainPage.xaml` to include:
    - Row 0: `NavigationBar`
    - Row 1: main grid with columns: sidebar(56) | `LeftPanelColumn`(Auto) | splitter(4) | right(*)
    - Card-like borders for left and right panels.
  - Name key elements: `LeftPanel` (Border), `LeftPanelColumn` (ColumnDefinition).
- Acceptance:
  - App runs with visible sidebar, left/right cards.
- References: `spec.md` (Layout Structure, ASCII Diagram)

4) Sidebar actions (left card header)
- Steps:
  - Add buttons to left card: “Ajouter un fichier”, “Rechercher un fichier”, “Créer un document”.
- Acceptance: Buttons visible, no runtime errors on click (handlers may be stubbed initially).
- References: `spec.md` (UX and Behavior)

---

## Phase 2 — Splitter & Resizing

5) Resizable left panel
- Steps:
  - Add a thin splitter column and a drag handle (Thumb or GridSplitter depending on target).
  - In `MainPage.xaml.cs`, implement `LeftRightThumb_DragDelta` (or equivalent) to update `LeftPanel.Width` while enforcing min width.
- Acceptance: User can drag to resize left panel; right panel adjusts.
- References: `TUTORIAL.md` (Resizing behavior), `spec.md` (Technical Design)

---

## Phase 3 — Collapse/Expand Animation

6) Toggle button wiring (sidebar icon)
- Steps:
  - Add a button in the sidebar with `Click="ToggleLeftPanelButton_Click"`.
  - Ensure only a single Click handler exists (no Tapped duplication).
- Acceptance: Breakpoint hits once on click.

7) Smooth animation (cross-platform)
- Steps:
  - In `MainPage.xaml.cs`, implement `AnimateLeftPanelTo(double targetWidth, TimeSpan? duration)` using `DispatcherTimer` (~16 ms) + quadratic easing.
  - In `ToggleLeftPanelButton_Click`, determine target based on `LeftPanel.Width` (idempotent) and call `AnimateLeftPanelTo`.
  - Persist state in `ApplicationData.Current.LocalSettings`.
- Acceptance:
  - Collapsing animates to width 0 in ~250 ms.
  - Expanding animates to last saved width or 360 px.
  - State survives app restart.
- References: `TUTORIAL.md` (animation section), `spec.md` (Animation Strategy, Mermaid sequence)

---

## Phase 4 — Upload Dialog (ContentDialog)

8) Add the dialog UI
- Steps:
  - In `MainPage.xaml`, add `ContentDialog x:Name="UploadDialog"` under the root Grid.
  - Include left drop zone, right action buttons (Convertir, Résumé, Reset), `Sauvegarder`, and a Preview section with `PreviewText` and `PreviewToggle`.
- Acceptance: Dialog XAML compiles; no dangling event handler names.
- References: `spec.md` (Upload Dialog diagram)

9) Wire dialog open and handlers
- Steps:
  - `BtnUpload_Click`: set `UploadDialog.XamlRoot = this.XamlRoot;` then `await UploadDialog.ShowAsync()`.
  - `DropZone_DragOver`, `DropZone_Drop`: accept and read files.
  - `DropZone_Tapped`: open `FileOpenPicker` to select a file.
  - `LoadFilePreviewAsync(IStorageFile)`: read text via `FileIO.ReadTextAsync` and update `PreviewText`.
  - `BtnToText_Click`, `BtnReset_Click`, `BtnSave_Click` (use `UploadDialog.Hide()`).
- Acceptance:
  - Clicking “Ajouter un fichier” opens dialog.
  - Drag/drop and select populate preview.
  - Reset clears preview; Save closes dialog.
- References: `TUTORIAL.md` (new feature), `spec.md` (Upload Dialog Implementation, Mermaid sequence)

---

## Phase 5 — Persistence & Navigation

10) Persist UI state
- Steps:
  - Save `LeftPanelCollapsed` and `LeftPanelWidth` on toggle/drag.
  - Restore in `MainPage` constructor.
- Acceptance: Restarting app restores panel state.
- References: `spec.md` (State and Persistence)

11) Verify navigation
- Steps:
  - Confirm `App.xaml.cs` routes: Shell → MainViewModel (default) or Login depending on auth.
  - Ensure MainPage loads before testing the toggle.
- Acceptance: From LoginPage, login then navigate to MainView.
- References: `App.xaml.cs`, `spec.md` (Navigation)

---

## Phase 6 — QA & Polish

12) Multi-platform checks
- Steps:
  - Run on macOS/Skia and Windows Desktop.
  - Verify animation performance, drop zone behavior, and file picker.
- Acceptance: Parity on both platforms.

13) Code quality
- Steps:
  - Remove unused fields (e.g., `_isAnimating` if not used).
  - Ensure only one event path is wired for toggling.
- Acceptance: Build is warning-free (or documented warnings).

14) Documentation updates
- Steps:
  - Ensure `TUTORIAL.md` contains reproduction steps and rationale.
  - Ensure `spec.md` diagrams and flows reflect current code.
- Acceptance: Docs aligned with implementation.

---

## Phase 7 — Chat Streaming Implementation (chat_stream branch)

### Step 1: Sidebar Enhancements ⏳
- [ ] **7.1** Add login avatar placeholder (round icon) at bottom left of 61px sidebar
- [ ] **7.2** Add settings icon above avatar with expandable menu section
- [ ] **7.3** Create 3 example menu items + "AI Settings" option
- [ ] **7.4** Style expandable menu with proper animations

### Step 2: AI Settings Dialog 📋
- [ ] **7.5** Create AI Settings ContentDialog with provider selection
- [ ] **7.6** Add Ollama configuration (URL, port 11434 default, model selection)
- [ ] **7.7** Add OpenAI configuration (API key, model selection)
- [ ] **7.8** Add Anthropic configuration (API key, model selection)
- [ ] **7.9** Add Google Gemini configuration (API key, model selection)
- [ ] **7.10** Add Mistral configuration (API key, model selection)
- [ ] **7.11** Implement connection testing for each provider
- [ ] **7.12** Add provider icons (download from web)

### Step 3: Chat Interface Enhancement 💬
- [ ] **7.13** Replace empty state with conversation-style message display
- [ ] **7.14** Add message bubbles for user and AI responses
- [ ] **7.15** Implement copy button for each AI response
- [ ] **7.16** Add proper scrolling and auto-scroll to latest message
- [ ] **7.17** Style messages with proper spacing and colors

### Step 4: Data Models & Services 🏗️
- [ ] **7.18** Create AI provider interface and implementations
- [ ] **7.19** Create message models (User, Assistant, System)
- [ ] **7.20** Create chat session model
- [ ] **7.21** Implement local JSON persistence service
- [ ] **7.22** Create settings service for AI provider configuration

### Step 5: Ollama Integration 🤖
- [ ] **7.23** Implement Ollama API client (HttpClient)
- [ ] **7.24** Add model discovery via `/api/tags` endpoint
- [ ] **7.25** Implement streaming chat via `/api/chat` endpoint
- [ ] **7.26** Handle word-by-word streaming display
- [ ] **7.27** Add error handling for connection issues

### Step 6: Cloud Provider Integration ☁️
- [ ] **7.28** Implement OpenAI API client with streaming
- [ ] **7.29** Implement Anthropic Claude API client
- [ ] **7.30** Implement Google Gemini API client
- [ ] **7.31** Implement Mistral API client
- [ ] **7.32** Add rate limiting and error handling for all providers

### Step 7: Message Management 📝
- [ ] **7.33** Implement chat history loading from JSON
- [ ] **7.34** Add conversation management (new/delete/rename)
- [ ] **7.35** Add message search functionality
- [ ] **7.36** Implement message export features
- [ ] **7.37** Add conversation context management

### Step 8: Polish & Testing 🎨
- [ ] **7.38** Add loading states and progress indicators
- [ ] **7.39** Implement proper error messages and user feedback
- [ ] **7.40** Add keyboard shortcuts (Enter to send, etc.)
- [ ] **7.41** Test all AI providers thoroughly
- [ ] **7.42** Add comprehensive error handling and recovery

## Technical Architecture

### File Structure
```
CAI_design_1_chat/
├── Models/
│   ├── ChatMessage.cs
│   ├── ChatSession.cs
│   ├── AIProvider.cs
│   └── AISettings.cs
├── Services/
│   ├── IAIService.cs
│   ├── OllamaService.cs
│   ├── OpenAIService.cs
│   ├── AnthropicService.cs
│   ├── GeminiService.cs
│   ├── MistralService.cs
│   ├── ChatPersistenceService.cs
│   └── SettingsService.cs
├── Presentation/
│   ├── Dialogs/
│   │   └── AISettingsDialog.xaml
│   ├── Controls/
│   │   ├── MessageBubble.xaml
│   │   └── ProviderIcon.xaml
│   └── MainPage.xaml (enhanced)
└── Assets/
    └── ProviderIcons/
```

### Dependencies to Add
- `System.Text.Json` - JSON serialization
- `Microsoft.Extensions.Http` - HTTP client factory
- `Microsoft.Extensions.Logging` - Logging support

---

## Optional Enhancements (Backlog)
- Real converters (PDF/DOCX → text) for "Convertir en text brut".
- Summarization pipeline for "Faire un résumé".
- Persist uploaded files and list them in the left panel.
- Animate column width (`LeftPanelColumn.Width`) instead of element width, if desired.

---

## Work Breakdown (Suggested Order & Ownership)

- **Phase 1-6**: Core UI and file handling (completed)
- **Phase 7**: Chat streaming implementation (current - chat_stream branch)
  - Week 1: Steps 7.1-7.12 (UI enhancements + settings)
  - Week 2: Steps 7.13-7.22 (chat interface + data models)
  - Week 3: Steps 7.23-7.32 (AI integrations)
  - Week 4: Steps 7.33-7.42 (advanced features + testing)

---

## Quick Commands
```bash
# Build
dotnet build CAI_design_1_chat.sln -c Debug

# Run
dotnet run --project CAI_design_1_chat/CAI_design_1_chat.csproj --framework net9.0-desktop

# Switch to chat streaming branch
git checkout chat_stream
