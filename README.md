# YouTube Live Stream Scanner

A self-hosted web app that scans YouTube for **live** and **upcoming** streams matching your keywords, with advanced filtering, trending indicators, and search presets.

## Features

- **Live & Upcoming tabs** - Switch between currently live streams and scheduled upcoming streams
- **Advanced search filters** - Region (15 countries), Language (13 languages), Category (Gaming, Music, Sports, etc.), Sort order, Safe search
- **Viewer filters** - Set minimum and maximum viewer count thresholds
- **Channel blocklist** - Block channels from appearing in results, persisted in localStorage
- **Search presets** - Save, load, and delete search configurations
- **Quota dashboard** - Monitor YouTube API quota usage with color-coded progress bar
- **Grid/list view toggle** - Switch between card grid and list layouts
- **Trending indicators** - See viewer count changes between refreshes (up/down/stable arrows)
- **Copy link** - One-click copy stream URL to clipboard
- **Auto-refresh** - Configurable interval (10s, 20s, 30s, 60s)
- **In-memory cache** - 10-second cache to reduce redundant API calls

## Requirements

- Node.js 18+ (Node 20+ recommended)
- A YouTube Data API v3 key

## Setup

### 1) Clone

```bash
git clone <your-repo-url>
cd LivestreamerScanner
```

### 2) Install deps

```bash
npm install
```

### 3) Add API key

- Copy `.env.example` to `.env`
- Put your key in `YOUTUBE_API_KEY=...`

### 4) Run

```bash
npm start
```

Open: http://localhost:3000

## API Endpoints

### `GET /api/live`

Search for live or upcoming streams.

| Parameter | Default | Description |
|-----------|---------|-------------|
| `q` | required | Comma-separated keywords |
| `max` | 25 | Max results (up to 50) |
| `minViewers` | 0 | Minimum viewer count filter |
| `maxViewers` | 0 | Maximum viewer count filter (0 = no limit) |
| `blocked` | | Comma-separated channel names to exclude |
| `region` | | ISO country code (US, GB, JP, etc.) |
| `lang` | | Language code (en, es, ja, etc.) |
| `topic` | | YouTube topic ID (/m/0bzvm2 for Gaming, etc.) |
| `order` | viewCount | Sort: viewCount, date, relevance, rating |
| `safeSearch` | none | Safe search: none, moderate, strict |
| `eventType` | live | Event type: live, upcoming, completed |

### `GET /api/quota`

Returns current YouTube API quota usage.

```json
{ "used": 201, "limit": 10000 }
```

## Google API Key Setup

In Google Cloud Console:
1. Create project
2. Enable **YouTube Data API v3**
3. Create **API key**
4. (Optional but recommended) restrict key to YouTube Data API + localhost usage

## Quota Usage

YouTube Data API v3 has a daily quota of 10,000 units:
- **Search** = 100 units per call
- **Video details** = 1 unit per call
- Each scan uses ~101 units (1 search + 1 video details call)

The quota dashboard in the header tracks your session usage in real time.
