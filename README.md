# YouTube Live Stream Scanner

A self-hosted web app that scans YouTube for **LIVE** streams matching your keywords and sorts by **current viewers**.

## Requirements
- Node.js 18+ (Node 20+ recommended)
- A YouTube Data API v3 key

## Setup

### 1) Clone
```bash
git clone <your-repo-url>
cd yt-stream-scanner
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

## Notes
- Viewer counts may be missing for some streams; those are shown as "—".
- The app uses a small in-memory cache (10s) to reduce API calls.

## Google API Key Setup

In Google Cloud Console:
1. Create project
2. Enable **YouTube Data API v3**
3. Create **API key**
4. (Optional but recommended) restrict key to YouTube Data API + localhost usage
