````markdown
# WeatherBot Webhook (.NET 8 + Dialogflow + OpenWeather 5→8 days)

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
````

You should see `Version: 8.x` and `TFM: net8.0`.

---

## 🚀 Quick Start

1. **Clone & Restore**

```bash
git clone https://github.com/Muzzamilkorai/WeatherBotWebhook.git
cd WeatherBotWebhook
dotnet restore
```

2. **Configure your API key**

Create `appsettings.Development.json` (or use env vars):

```json
{
  "OpenWeather": {
    "ApiKey": "YOUR_OPENWEATHER_API_KEY"
  }
}
```

3. **Run**

```bash
dotnet run
```
---

## 🌐 Endpoint

### `POST /api/webhook`

**Request body (Dialogflow-style minimal):**

```json
{
  "queryResult": {
    "parameters": {
      "city": "Karachi",
      "date": "2025-10-08"
    }
  }
}
```

* If **`date` is missing** → replies with **current weather** for `city`.
* If **`date` is provided** → replies with **8-day forecast** for `[date .. date+7]`.

**Example cURL (Karachi):**

```bash
curl -k -X POST https://localhost:5xxx/api/webhook \
  -H "Content-Type: application/json" \
  -d '{"queryResult":{"parameters":{"city":"Karachi","date":"2025-10-08"}}}'
```

**Example response (illustrative):**

```json
{
  "fulfillmentText": "8-day forecast for Karachi from 2025-10-08 to 2025-10-15:
2025-10-08: clear sky, high 34°C, low 26°C, avg humidity 72%.
2025-10-09: clear sky, high 34°C, low 26°C, avg humidity 71%.
..."
}
```

---

## 🏗️ Project Structure

```
.
├─ Controllers/
│  └─ WeatherWebhookController.cs   # POST /api/webhook
├─ Services/
│  └─ OpenWeatherService.cs         # 5-day fetch + simple extender up to requested end date
├─ Program.cs                       # minimal hosting, MapControllers()
├─ appsettings.json / appsettings.Development.json
├─ WeatherBotWebhook.csproj
└─ README.md
```

---

## 🧠 How It Works

* Webhook receives `{ city, date }` from Dialogflow.
* **No date** → calls current weather and returns a simple summary for Karachi (or any city provided).
* **With date**:

  1. Computes `endDate = startDate + 7`.
  2. Calls an internal method that:

     * Fetches the **real** 5-day / 3-hour forecast.
     * If the data ends before `endDate`, it **appends days** by **repeating the last real day’s** min/max/humidity/wind/description and generating **8× 3-hour slices** using a **simple diurnal (day/night) temperature curve**.
  3. Converts all timestamps to **local city dates** using the payload timezone.
  4. Groups by day → prints **high, low, avg humidity, representative description**.

