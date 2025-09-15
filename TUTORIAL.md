## Smooth left panel animation (Skia/macOS and Windows)

This app includes a collapsible left panel with a smooth animation that works across Skia/macOS and Windows Desktop.

### What happened during development

- We initially used XAML `Storyboard` + `DoubleAnimation` on `Width`. On our Skia/macOS target the animation didn’t visibly play in this setup.
- Multiple event hooks (Button `Click`, Button `Tapped`, Sidebar `Tapped`, and a runtime hookup) caused multiple toggles per click. We removed all but the XAML `Click`.
- After removing Storyboards, XAML still referenced `Completed` handlers, causing build errors. We removed those references.

### Final solution

- We animate the element directly: the border `LeftPanel` in `Presentation/MainPage.xaml`.
- In code-behind (`Presentation/MainPage.xaml.cs`) we use a small helper `AnimateLeftPanelTo(double targetWidth, TimeSpan? duration)` that:
  - Uses a `DispatcherTimer` (~60 FPS) with quadratic ease-in/out.
  - Interpolates `LeftPanel.Width` from the current width to the target (0 for collapse, last saved or 360 for expand) over ~250 ms.
  - Persists the final width and collapsed state using `ApplicationData.Current.LocalSettings`.
- `ToggleLeftPanelButton_Click` decides collapse/expand based on `LeftPanel.Width` to keep behavior idempotent.

### Where to look in the code

- `UnoApp4/Presentation/MainPage.xaml` — UI layout. Look for `x:Name="LeftPanel"` and the 4-column grid.
- `UnoApp4/Presentation/MainPage.xaml.cs` — `AnimateLeftPanelTo(...)` and `ToggleLeftPanelButton_Click`.

### Commands executed while troubleshooting

```bash
# Restore and build to validate XAML/code-behind generation and fixes
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build UnoApp4.sln -c Debug
```

### Why this approach

- Timer + easing ensures consistent visuals on Skia/macOS and Windows without depending on Storyboard behavior.
- If needed in the future, we can animate the grid column instead by updating `LeftPanelColumn.Width = new GridLength(value)` on each tick.

# UnoApp4 – UI Layout Tutorial

This document explains how to reproduce the new master-detail layout implemented in `UnoApp4`, featuring:

- A fixed far-left sidebar (icons only)
- A collapsible left panel ("Espace de travail")
- A draggable splitter between left and right panels
- A right panel with a Chat card, empty state, and input area

The implementation follows MVVM-friendly patterns and uses standard WinUI/Uno controls only.

## Files changed

- `UnoApp4/Presentation/MainPage.xaml`
  - Replaced the sample content with a 3-column grid layout: `Sidebar | Left Panel | Splitter | Right Panel`.
  - Named the left column definition `LeftPanelColumn` so we can toggle its width in code-behind.
  - Added a `GridSplitter` between left and right panels for resizing.
  - Styled panels as cards with rounded borders using theme resources.
- `UnoApp4/Presentation/MainPage.xaml.cs`
  - Added a single event handler `ToggleLeftPanelButton_Click` to collapse/expand the left panel by changing `LeftPanelColumn.Width`.

## How the layout is structured

- Root `Grid` has two rows: a top `NavigationBar` and the main content.
- The main content `Grid` has four columns:
  1. Fixed-width sidebar (`56px`) with a toggle button (icons-only) on the very far left.
  2. Left content panel (`LeftPanelColumn`) that can be collapsed/expanded and resized.
  3. Auto column hosting a `GridSplitter` (`Width=4`) for drag-resize.
  4. Right content panel that fills the remaining space.

### Collapse/Expand behavior
- Clicking the top icon button in the sidebar toggles the left panel width between `0` and `360`.
- This keeps the sidebar independent from the left panel, as requested.

### Resizing behavior
- Users can drag the vertical splitter between panels to resize.
- `LeftPanelColumn` has `MinWidth=280` when expanded; right column has `MinWidth=320`.

## Animation Issue and Solution

### Problem

The initial implementation of the left panel animation did not work as expected on Skia/macOS. The animation was not visible, and multiple event hooks caused multiple toggles per click.

### Solution

We replaced the XAML `Storyboard` + `DoubleAnimation` with a custom animation solution using a `DispatcherTimer` and quadratic ease-in/out. This approach ensures consistent visuals on Skia/macOS and Windows without depending on Storyboard behavior.

### Code Location

The animation code can be found in `UnoApp4/Presentation/MainPage.xaml.cs` in the `AnimateLeftPanelTo` method.

### Commands

To validate the XAML/code-behind generation and fixes, run the following command:

```bash
# Restore and build to validate XAML/code-behind generation and fixes
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build UnoApp4.sln -c Debug
```

## Build and run

Run these commands from the project root. They are shown here so you can reproduce the setup on any machine.

```bash
# Restore and build the solution
# Why: Ensures all NuGet dependencies are restored and the code compiles
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build UnoApp4.sln -c Debug

# Run for a platform (example: Mac Catalyst/Win/WasM depending on targets)
# Uno has multiple targets; use your preferred one from Rider/VS run configs.
# For WebAssembly, if configured, you can publish and serve as needed.
```

Note: Building may restore NuGet packages (network access). If you prefer, use your IDE run configurations (`.run/UnoApp4.run.xml`) to start the app.

## Customization notes

- Icons: The sidebar button currently shows a stacked `Add` and `Page` symbol to mimic the plus/file icon from the screenshot. Replace with a custom glyph or `PathIcon` for exact visuals if desired.
- The cards use theme brushes such as `CardBackgroundFillColorDefaultBrush` and `CardStrokeColorDefaultBrush`. If your theme does not define them, swap to `SurfaceBrush`/`ControlOnImageFillColorDefaultBrush` or custom brushes in `Styles/ColorPaletteOverride.xaml`.
- Persisting UI state (collapsed/expanded flag and splitter position) can be added via `ApplicationData.Current.LocalSettings` or your MVVM state container.

## Why these choices

- Using a `Grid` with a named `ColumnDefinition` makes collapse/expand trivial by setting `Width` to `0` or a specific pixel value.
- `GridSplitter` is the most lightweight and native way to provide drag resizing between two columns across WinUI/Uno targets.
- Keeping the sidebar as a separate fixed column ensures it remains independent from the left panel, matching the requested behavior.

## Implementation Summary (Latest Update)

### What was implemented
- **Main Layout**: 4-column grid layout with fixed sidebar (56px), collapsible left panel, splitter, and chat panel
- **Left Panel**: "Espace de travail" card with three buttons:
  - "Ajouter un fichier" (with click handler placeholder)
  - "Rechercher un fichier" (placeholder)
  - "Créer un document" (placeholder)
- **Right Panel**: "Chat" card with:
  - Header with download and options buttons
  - Empty state message ("No messages here yet")
  - Input field with send button
  - Enter key support for sending messages
- **Animation System**: Smooth collapse/expand animation using DispatcherTimer with quadratic easing
- **State Persistence**: Left panel width and collapsed state saved to ApplicationData.Current.LocalSettings

### Key Changes Made
- Replaced GridSplitter with Border for cross-platform compatibility
- Fixed nullability warnings in code-behind
- Implemented proper MVVM-friendly event handlers
- Added keyboard support (Enter key) for chat input

### Files Modified
- `CAI_design_1_chat/Presentation/MainPage.xaml` - Complete UI layout implementation
- `CAI_design_1_chat/Presentation/MainPage.xaml.cs` - Animation logic and event handlers

### Build Command Used
```bash
# Build the solution
dotnet build CAI_design_1_chat.sln -c Debug

# Run the application (macOS desktop)
dotnet run --project CAI_design_1_chat/CAI_design_1_chat.csproj --framework net9.0-desktop
```

### Current Status
The basic screen layout is complete and matches the provided design image. The application builds successfully and includes:
- ✅ Fixed far-left sidebar with toggle button
- ✅ Collapsible "Espace de travail" panel with file management buttons
- ✅ Visual splitter between panels
- ✅ Chat panel with empty state and input functionality
- ✅ Smooth animation for panel collapse/expand
- ✅ State persistence across app sessions

### ✅ Issues Resolved
- ✅ Button visibility: Enhanced contrast with proper theme resources and accent colors
- ✅ Collapse behavior: Panel now collapses completely to show only far-left sidebar
- ✅ Color scheme: Improved button styling with visible backgrounds and proper foregrounds

## Complete Collapse Implementation Guide

### Problem Statement
The original implementation had two critical issues:
1. **Toggle button invisibility**: Transparent background made the far-left toggle button nearly invisible
2. **Incomplete collapse**: Panel would hide content but maintain width, leaving empty space

### Solution Architecture

#### 1. Enhanced Toggle Button Visibility
```xml
<Button x:Name="ToggleLeftPanelButton"
        Background="{ThemeResource SubtleFillColorSecondaryBrush}"
        BorderBrush="{ThemeResource AccentFillColorDefaultBrush}"
        BorderThickness="1"
        CornerRadius="4">
  <StackPanel>
    <FontIcon Glyph="&#xE109;" Foreground="{ThemeResource AccentFillColorDefaultBrush}" />
    <FontIcon Glyph="&#xE8A5;" Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
  </StackPanel>
</Button>
```

#### 2. Complete Collapse Logic
The key insight is to control **both width AND visibility** of multiple elements:

**XAML Structure:**
```xml
<Grid.ColumnDefinitions>
  <ColumnDefinition Width="56" />                    <!-- Fixed sidebar -->
  <ColumnDefinition x:Name="LeftPanelColumn" />      <!-- Collapsible panel -->
  <ColumnDefinition x:Name="SplitterColumn" />       <!-- Collapsible splitter -->
  <ColumnDefinition Width="*" />                     <!-- Chat panel -->
</Grid.ColumnDefinitions>
```

**C# Animation Logic:**
```csharp
private void AnimateLeftPanelTo(double targetWidth)
{
    // Set target widths for both panel and splitter
    LeftPanelColumn.Width = new GridLength(targetWidth);
    SplitterColumn.Width = new GridLength(targetWidth <= 0.5 ? 0 : 4);
    
    // Control visibility
    LeftPanel.Visibility = targetWidth <= 0.5 ? Visibility.Collapsed : Visibility.Visible;
    Splitter.Visibility = targetWidth <= 0.5 ? Visibility.Collapsed : Visibility.Visible;
}
```

#### 3. Progressive Animation Hiding
During animation, hide elements early to prevent visual glitches:
```csharp
// In AnimationTimer_Tick
if (currentWidth <= 10)
{
    LeftPanel.Visibility = Visibility.Collapsed;
    Splitter.Visibility = Visibility.Collapsed;
    SplitterColumn.Width = new GridLength(0);
}
```

### Engineering Best Practices

#### Why This Approach Works
1. **Dual Control**: Managing both `Width` and `Visibility` ensures complete hiding
2. **Column Width = 0**: Removes space allocation entirely
3. **Early Hiding**: Prevents flicker during animation
4. **State Persistence**: Maintains user preferences across sessions

#### Cross-Platform Considerations
- Uses `Border` instead of `GridSplitter` for Uno Platform compatibility
- Theme resources ensure proper contrast across light/dark themes
- Nullability handling prevents runtime errors

#### Performance Optimization
- 60 FPS animation with `DispatcherTimer` (16ms intervals)
- Quadratic easing for smooth visual transitions
- Early termination when delta < 0.5px

### Memory Content Integration
This implementation successfully created a master-detail workspace with:
- 4-column grid layout: Fixed sidebar (61px) | Left panel ("Espace de travail") | Splitter | Right panel ("Chat")
- Navigation bar showing "Chat" title
- Left panel with three functional buttons and smooth collapse animation
- Right panel with chat interface including empty state and message input
- Cross-platform compatibility using Border instead of GridSplitter
- Proper nullability handling and MVVM-friendly event handlers
- State persistence using ApplicationData.Current.LocalSettings
- **Complete collapse functionality** that hides panel entirely when toggled

## Chat Streaming UI Implementation (Phase 7)

### Step 7.1: Login Avatar Implementation ✅

**Challenge**: Adding a login avatar at the bottom of the sidebar while maintaining proper layout.

**Initial Approach Issues**:
- Using `StackPanel` with `<Border Height="*" />` caused XAML compilation errors
- The `Height="*"` syntax is invalid for Border elements in StackPanel

**Solution**: Grid-based Layout
```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />    <!-- Toggle button -->
    <RowDefinition Height="*" />       <!-- Flexible spacer -->
    <RowDefinition Height="Auto" />    <!-- Login avatar -->
  </Grid.RowDefinitions>
  
  <Button Grid.Row="0" ... />          <!-- Toggle at top -->
  <Button Grid.Row="2" ... />          <!-- Avatar at bottom -->
</Grid>
```

**Key Lessons**:
1. **Grid vs StackPanel**: Use Grid when you need flexible spacing with `Height="*"`
2. **PersonPicture Control**: Perfect for user avatar placeholders in Uno Platform
3. **Circular Buttons**: `CornerRadius="24"` with 48px width/height creates perfect circles
4. **Proper Margins**: `Margin="6,0,6,8"` provides consistent spacing within 61px sidebar

**Implementation Details**:
- **Avatar Size**: 48x48 button with 40x40 PersonPicture inside
- **Styling**: Subtle background with border for visibility
- **Positioning**: Bottom-anchored with proper margins
- **Accessibility**: Tooltip "User profile" for screen readers

### Steps 7.2-7.4: Settings Menu Implementation ✅

**Challenge**: Creating an expandable settings menu with proper icons and animations.

**XAML Parsing Issues Encountered**:
- `SymbolIcon` with values like `Symbol="Robot"`, `Symbol="Setting"`, `Symbol="Notification"` caused XAML parsing errors
- Error: `UXAML0001: An error was found in Page` prevented code generation
- **Root Cause**: Uno Platform has limited SymbolIcon enum support compared to WinUI/UWP
- **Time Cost**: ~45 minutes debugging build failures and XAML generation issues

**Solution**: FontIcon with Unicode Glyphs
```xml
<!-- WRONG: Causes XAML parsing errors in Uno Platform -->
<SymbolIcon Symbol="Robot" />
<SymbolIcon Symbol="Setting" />
<SymbolIcon Symbol="Notification" />

<!-- CORRECT: Use FontIcon with Segoe MDL2 Assets glyphs -->
<FontIcon Glyph="&#xE8B8;" />  <!-- Robot/AI -->
<FontIcon Glyph="&#xE713;" />  <!-- Settings gear -->
<FontIcon Glyph="&#xE7E7;" />  <!-- Notification bell -->
```

**Design Right First Time - Prevention Strategy**:

1. **Icon Selection Best Practice**:
   ```xml
   <!-- ALWAYS prefer FontIcon for cross-platform compatibility -->
   <FontIcon Glyph="&#xE713;" FontSize="20" />
   
   <!-- Only use SymbolIcon for basic, well-supported symbols -->
   <SymbolIcon Symbol="Add" />
   <SymbolIcon Symbol="Delete" />
   <SymbolIcon Symbol="Save" />
   ```

2. **Uno Platform Icon Compatibility Rules**:
   - ✅ **Safe SymbolIcon values**: Add, Delete, Save, Edit, Search, Home, Back, Forward
   - ❌ **Avoid SymbolIcon for**: Robot, Setting, Notification, Brightness, Help, Custom icons
   - ✅ **Always use FontIcon for**: Settings UI, notifications, themes, AI/robot icons

3. **Icon Reference Strategy**:
   ```csharp
   // Keep a reference file for commonly used glyphs
   public static class IconGlyphs
   {
       public const string Settings = "&#xE713;";
       public const string Notifications = "&#xE7E7;";
       public const string Theme = "&#xE706;";
       public const string Help = "&#xE897;";
       public const string AI = "&#xE8B8;";
   }
   ```

4. **Early Validation Approach**:
   - Test build after adding each new icon
   - Use Segoe MDL2 Assets font reference for glyph codes
   - Validate on target platform (desktop/mobile) early in development

**Time Savings**: Following this approach prevents 30-60 minutes of debugging per icon-related build failure.

**Icon Mappings Used**:
- **Settings**: `&#xE713;` (gear icon)
- **Notifications**: `&#xE7E7;` (bell icon)  
- **Theme**: `&#xE706;` (brightness icon)
- **Help**: `&#xE897;` (help icon)
- **AI Settings**: `&#xE8B8;` (robot/AI icon)

**Menu Structure**:
```xml
<StackPanel x:Name="SettingsSection" Grid.Row="2">
  <Button x:Name="SettingsButton" />
  <StackPanel x:Name="SettingsMenu" Visibility="Collapsed">
    <!-- 4 menu items: Notifications, Theme, Help, AI Settings -->
  </StackPanel>
</StackPanel>
```

**Key Lessons**:
1. **FontIcon vs SymbolIcon**: Use FontIcon with Unicode glyphs for better Uno Platform compatibility
2. **XAML Code Generation**: Invalid SymbolIcon values prevent entire XAML from compiling
3. **Menu Animation**: Simple Visibility toggle works for basic expand/collapse
4. **Icon Consistency**: All menu items use 48x48 size with 18px font icons
5. **Visual Hierarchy**: AI Settings uses accent colors to highlight importance

## Engineering Best Practices - Avoiding Common Pitfalls

### Issue #1: StackPanel Height="*" Syntax Error
**Problem**: `<Border Height="*" />` inside StackPanel caused build failure
**Root Cause**: Star sizing (`*`) is only valid for Grid rows/columns, not element Height/Width
**Solution**: Use Grid with `<RowDefinition Height="*" />` for flexible spacing

### Issue #2: SymbolIcon Compatibility in Uno Platform  
**Problem**: 45 minutes lost debugging XAML parsing errors from unsupported SymbolIcon values
**Root Cause**: Uno Platform has subset of WinUI SymbolIcon enum values
**Prevention Strategy**:
```xml
<!-- DESIGN PATTERN: Always validate icon compatibility first -->
<!-- Step 1: Check if basic SymbolIcon works -->
<SymbolIcon Symbol="Add" />  <!-- Test build -->

<!-- Step 2: If custom icons needed, use FontIcon immediately -->
<FontIcon Glyph="&#xE713;" />  <!-- Settings -->
<FontIcon Glyph="&#xE8B8;" />  <!-- AI/Robot -->
```

### Issue #3: XAML Code Generation Dependencies
**Problem**: Single invalid XAML element prevents ALL named elements from generating
**Impact**: All `x:Name` references in code-behind show as "does not exist" errors
**Prevention**: 
- Build after each major XAML change
- Use incremental development approach
- Test one new control at a time

### Time-Saving Development Workflow
```bash
# 1. Add single new UI element
# 2. Build immediately
dotnet build CAI_design_1_chat.sln -c Debug

# 3. If build fails, fix before adding more
# 4. Only proceed when build succeeds
# 5. Repeat for next element
```

**ROI**: This workflow prevents cascading failures that can cost 1-2 hours of debugging time.

**Functionality Implemented**:
- ✅ Settings button with gear icon
- ✅ Expandable menu with 4 items (Notifications, Theme, Help, AI Settings)
- ✅ Toggle visibility on settings button click
- ✅ Proper styling and spacing within 61px sidebar
- ✅ Event handlers for settings toggle and AI settings dialog

## Next steps (optional)

- Implement file upload dialog functionality for "Ajouter un fichier"
- Add actual chat message handling and display
- Implement search functionality for "Rechercher un fichier"
- Add document creation functionality for "Créer un document"
- Replace placeholder actions in the left panel with real commands.
