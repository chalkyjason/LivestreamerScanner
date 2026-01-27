# Stream Scanner - .NET MAUI

A cross-platform native app that scans YouTube for **LIVE** streams matching your keywords and sorts by **current viewers**.

Built with .NET MAUI for Windows, macOS, iOS, and Android.

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

## Features

- Search live YouTube streams by comma-separated keywords
- Results sorted by concurrent viewer count (highest first)
- Configurable auto-refresh interval (10s, 20s, 30s, 60s)
- Native UI on all platforms
- Tap any stream to open in browser/YouTube app
- Settings page for API key configuration

## Project Structure

```
StreamScanner.Maui/
├── Models/
│   ├── LiveStream.cs           # Stream data model
│   └── YouTubeApiModels.cs     # API response models
├── Services/
│   ├── IYouTubeService.cs      # Service interface
│   └── YouTubeService.cs       # YouTube API implementation
├── ViewModels/
│   └── MainViewModel.cs        # MVVM ViewModel
├── Views/
│   ├── MainPage.xaml           # Main scanner UI
│   └── SettingsPage.xaml       # API key settings
├── Converters/
│   └── RefreshOptionConverter.cs
├── Resources/
│   ├── Styles/
│   │   ├── Colors.xaml         # Color palette
│   │   └── Styles.xaml         # UI styles
│   └── Images/
│       └── appicon.svg         # App icon
├── App.xaml                    # Application resources
├── AppShell.xaml               # Navigation shell
├── MauiProgram.cs              # DI and app configuration
└── StreamScanner.Maui.csproj   # Project file
```

## Architecture

- **MVVM Pattern**: Using CommunityToolkit.Mvvm for clean separation
- **Dependency Injection**: Services registered in MauiProgram.cs
- **Async/Await**: All API calls are asynchronous
- **Data Binding**: Two-way binding for all UI interactions

## Comparison with Web Version

| Feature | Web App | MAUI App |
|---------|---------|----------|
| Runtime | Node.js + Browser | Native |
| Distribution | npm install | Single executable |
| Platforms | Any with browser | Windows, Mac, iOS, Android |
| API Key Storage | .env file | Secure preferences |
| Offline | Requires server | Better offline handling |

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
