# WeatherBot Webhook

ASP.NET Core **.NET 8** API that serves a **Dialogflow webhook**.

- Uses OpenWeather’s **5-day / 3-hour** forecast (`/data/2.5/forecast`)
- **Extends** it to an **8-day** window *(start date → start+7)* without One Call or model changes
- Returns **current weather** when no date is provided

---

## ✨ Features

- **POST /api/webhook** endpoint for Dialogflow
- 5-day forecast → **8-day** by **synthesizing** extra days (repeats last real day’s stats, builds 3-hour slices with a simple day/night curve)
- Clean daily summaries (high / low / humidity / description)
- Minimal DTOs mirroring OpenWeather payloads
- Pure **.NET 8** (controllers + DI + `HttpClient`)

---

## 🧰 Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- OpenWeather API key

Verify SDK:
```bash
dotnet --info

---
## 🚀 Quick Start
git clone https://github.com/<your-username>/<your-repo>.git
cd <your-repo>
dotnet restore

---
##🚀 Quick Start
