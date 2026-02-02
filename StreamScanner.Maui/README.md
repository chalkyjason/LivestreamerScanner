# Stream Scanner - .NET MAUI

A cross-platform native app that scans YouTube for **live** and **upcoming** streams matching your keywords, with advanced filtering, presets, and quota tracking.

Built with .NET MAUI for Windows, macOS, iOS, and Android.

## Features

- **Live & Upcoming tabs** - Switch between currently live streams and scheduled upcoming streams
- **Advanced search filters** - Region, Language, Category (Gaming, Music, Sports, etc.), Sort order, Safe search
- **Viewer filters** - Set minimum and maximum viewer count thresholds
- **Channel blocklist** - Block channels from appearing in results
- **Search presets** - Save, load, and delete search configurations (persisted via Preferences)
- **Quota dashboard** - Monitor YouTube API quota usage with progress bar
- **Favorites** - Mark favorite channels for quick access
- **Copy link** - Copy stream URL to clipboard
- **Auto-refresh** - Configurable interval (10s, 20s, 30s, 60s)
- **In-memory cache** with rate limiting to reduce API calls

## Requirements

- .NET 8 SDK
- Visual Studio 2022 (Windows) or Visual Studio for Mac
- Platform-specific workloads:
  - Windows: `.NET MAUI` workload
  - macOS: Xcode + `.NET MAUI` workload
  - Android: Android SDK
  - iOS: Xcode + iOS SDK

## Quick Start

### 1. Install .NET MAUI Workload

```bash
dotnet workload install maui
```

### 2. Build and Run

**Windows:**
```bash
cd StreamScanner.Maui
dotnet build -f net8.0-windows10.0.19041.0
dotnet run -f net8.0-windows10.0.19041.0
```

**macOS:**
```bash
cd StreamScanner.Maui
dotnet build -f net8.0-maccatalyst
dotnet run -f net8.0-maccatalyst
```

**Or use Visual Studio:**
1. Open `StreamScanner.Maui.csproj`
2. Select target platform (Windows/Mac/Android/iOS)
3. Press F5 to run

### 3. Configure API Key

1. Click the **Settings** button (gear icon) in the app header
2. Paste your YouTube Data API v3 key
3. Click **Save API Key**
4. Restart the app

## Getting a YouTube API Key

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project (or select existing)
3. Navigate to **APIs & Services** > **Library**
4. Search for and enable **YouTube Data API v3**
5. Go to **APIs & Services** > **Credentials**
6. Click **Create Credentials** > **API Key**
7. Copy the key and paste it in the app settings

## Project Structure

```
StreamScanner.Maui/
├── Models/
│   ├── LiveStream.cs              # Stream data model (live + upcoming)
│   └── YouTubeApiModels.cs        # API response models
├── Services/
│   ├── IYouTubeService.cs         # Service interface + SearchFilters
│   ├── YouTubeService.cs          # YouTube API implementation
│   ├── IFavoritesService.cs       # Favorites interface
│   ├── FavoritesService.cs        # Favorites persistence
│   ├── IBlockedChannelsService.cs # Blocked channels interface
│   ├── BlockedChannelsService.cs  # Blocked channels persistence
│   ├── ICacheService.cs           # Cache interface
│   └── CacheService.cs            # In-memory cache + rate limiting
├── ViewModels/
│   └── MainViewModel.cs           # MVVM ViewModel with all commands
├── Views/
│   ├── MainPage.xaml              # Main scanner UI (tabs, filters, results)
│   ├── SettingsPage.xaml          # API key settings
│   └── FavoritesPage.xaml         # Favorites management
├── Converters/
│   ├── RefreshOptionConverter.cs  # Refresh interval binding
│   └── TabColorConverter.cs       # Tab active state + quota progress
├── Resources/
│   ├── Styles/
│   │   ├── Colors.xaml            # Color palette
│   │   └── Styles.xaml            # UI styles
│   └── Images/
│       └── appicon.svg            # App icon
├── App.xaml                       # Application resources + converters
├── AppShell.xaml                  # Navigation shell
├── MauiProgram.cs                 # DI and app configuration
└── StreamScanner.Maui.csproj      # Project file
```

## Architecture

- **MVVM Pattern**: Using CommunityToolkit.Mvvm for clean separation
- **Dependency Injection**: All services registered in MauiProgram.cs
- **Async/Await**: All API calls are asynchronous with cancellation support
- **Data Binding**: Two-way binding for all UI interactions
- **SearchFilters**: Region, Language, TopicId, SortOrder, SafeSearch, EventType, MinViewers, MaxViewers, BlockedChannels

## Search Filter Options

| Filter | Options |
|--------|---------|
| Region | US, UK, Canada, Australia, Germany, France, Japan, S. Korea, Brazil, India, Mexico, Spain, Italy, Russia, Philippines |
| Language | English, Spanish, Portuguese, French, German, Japanese, Korean, Hindi, Russian, Italian, Chinese, Arabic, Filipino |
| Category | Gaming, Music, Sports, Entertainment, Lifestyle, Knowledge, Society |
| Sort | Most Viewers, Newest, Relevance, Rating |
| Safe Search | Off, Moderate, Strict |
| Event Type | Live, Upcoming |

## Comparison with Web Version

| Feature | Web App | MAUI App |
|---------|---------|----------|
| Runtime | Node.js + Browser | Native |
| Distribution | npm install | Single executable |
| Platforms | Any with browser | Windows, Mac, iOS, Android |
| API Key Storage | .env file | Secure preferences |
| View Toggle | Grid/List CSS toggle | Native list |
| Presets | localStorage | Preferences API |
| Trending | Viewer delta arrows | Not yet implemented |

## Troubleshooting

**"API key not configured"**
- Open Settings and enter your YouTube API key
- Make sure the key has YouTube Data API v3 enabled

**Build errors on Windows**
- Ensure Windows App SDK is installed
- Run: `dotnet workload restore`

**Build errors on Mac**
- Ensure Xcode is installed and up to date
- Run: `sudo xcode-select --reset`
