# Flame Boss Local Controller

A self-hosted replacement for the Flame Boss phone app, for the
[Flame Boss 400 WiFi Smoker Controller](https://www.flameboss.com/product/flame-boss-400-wifi-smoker-controller-kit-universal/).

It talks directly to the controller's built-in web page on your LAN (no Flame Boss cloud account or app needed) and gives you:

- **Web dashboard** (Blazor + MudBlazor): live pit, meat, set-point, and blower readings, time-series charts for the current cook, and a **Set Temperature** button that opens a dialog accepting 150–400 °F and sends it to the controller.
- **REST API**: read status, set the pit target temperature, start/stop a cook, health checks.
- **Home Assistant via MQTT**: publishes sensor state and auto-discovery config so the controller shows up as a device in Home Assistant.

## How it works

```mermaid
flowchart LR
    FB["Flame Boss 400<br/>(http://&lt;ip&gt;/ and /set)"]
    subgraph App["Flameboss (ASP.NET Core, .NET 10)"]
        Poll["FlameBossPollingService<br/>every POLLSECONDS"]
        Svc["FlameBossService<br/>HTML scrape / form POST"]
        Data["FlamebossData<br/>(in-memory cook history)"]
        API["REST API /api/*"]
        UI["Dashboard /"]
        Mqtt["MqttService"]
    end
    HA["Home Assistant"]
    Broker["MQTT broker"]

    Poll --> Svc
    Svc -- "GET /, GET /set" --> FB
    Svc -- "POST /set (s=temp)" --> FB
    Svc --> Data
    Data --> API
    Data --> UI
    Poll --> Mqtt --> Broker --> HA
```

The Flame Boss 400 serves a plain HTML page on its local IP:

| Device URL | Used for |
|---|---|
| `GET /` | Pit temperature, blower %, Meat 1 temperature (parsed from the HTML tables) |
| `GET /set` | Current set temperature (value of the `s` form field) |
| `POST /set` with form field `s=<temp>` | Change the set temperature |

Sample captures of both pages are in `Flameboss/output.html` and `Flameboss/set.html`.

### Cook sessions

Polling only happens while a cook is active. Start/stop a cook with the dashboard's **Toggle Cook** button or `GET /api/toggle-cook`.

- Starting a cook clears history, takes an immediate reading, then polls every `POLLSECONDS`.
- Each reading is appended to the in-memory history that feeds the charts, and is published to MQTT.
- Stopping a cook clears the history.
- Cook state and history live only in memory and are lost when the app restarts.

## Configuration

Settings are read through ASP.NET Core configuration: environment variables, or `dotnet user-secrets` when running locally in Development. Nothing is hard-coded in the repo. Names are case-insensitive.

| Variable | Default | Notes |
|---|---|---|
| `FLAMEBOSSURL` | (required) | Controller base URL, e.g. `http://192.168.1.178/`. A trailing `/` is added if missing. |
| `MQTTBROKER` | `localhost` | MQTT broker host (port 1883). |
| `MQTTUSERNAME` | (none) | MQTT username. Treat as a secret. |
| `MQTTPASSWORD` | (none) | MQTT password. **Secret**: never put it in a committed file. |
| `DEVICENAME` | `flameboss` | Home Assistant device identifier. |
| `FRIENDLYNAME` | `Flame Boss` | Home Assistant device name and sensor name prefix. |
| `POLLSECONDS` | `5` | Poll interval, at least 1. |

The app exits at startup if `FLAMEBOSSURL` is missing, if `POLLSECONDS` is below 1, or if it can't connect to the MQTT broker.

## Running

### Locally

Requires the .NET 10 SDK. Store the settings as user secrets; they live in your user profile, outside the repo:

```sh
cd Flameboss
dotnet user-secrets set FLAMEBOSSURL http://192.168.1.178/
dotnet user-secrets set MQTTBROKER 192.168.1.3
dotnet user-secrets set MQTTUSERNAME mqtt
dotnet user-secrets set MQTTPASSWORD '<password>'
dotnet user-secrets set DEVICENAME flameboss_bbq
dotnet user-secrets set FRIENDLYNAME "Flame Boss BBQ"
dotnet run --launch-profile http
```

The dashboard is at <http://localhost:5163>. Exported environment variables override user secrets.

### Docker Compose

`compose.yaml` passes the variables through from your shell, or from a `.env` file next to it (git-ignored by `*.env`):

```sh
cat > .env <<'EOF'
FLAMEBOSSURL=http://192.168.1.178/
MQTTBROKER=192.168.1.3
MQTTUSERNAME=mqtt
MQTTPASSWORD=<password>
DEVICENAME=flameboss_bbq
FRIENDLYNAME=Flame Boss BBQ
POLLSECONDS=5
EOF
docker compose up --build
```

The dashboard is at <http://localhost:5163> (container port 8080).

### Kubernetes (k3s)

`.github/workflows/image.yaml` builds `ghcr.io/factotuminok/flameboss:{latest,sha-<commit>}` (linux/amd64) on every push to `main`.

The homelab k3s cluster deploys it with Argo CD from the private `factotuminok/homelab` repo (`apps/flameboss`), at `https://flameboss.lewisfamily4.com`:

- Non-secret settings (`FLAMEBOSSURL`, `MQTTBROKER`, `DEVICENAME`, `FRIENDLYNAME`, `POLLSECONDS`) are plain `env` values in the Deployment.
- `MQTTUSERNAME` and `MQTTPASSWORD` come from the existing Kubernetes Secret `mosquitto` (keys `MQTT_USER`, `MQTT_PASSWORD`). The manifests only reference that Secret.
- The broker is the in-cluster `mosquitto` Service.
- Run one replica only: cook state is kept in memory.

To deploy a new build, set the image tag in `apps/flameboss/kustomization.yaml` in the homelab repo and Sync `flameboss` in Argo CD. `apps/flameboss/README.md` there has the details.

## REST API

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/toggle-cook` | Starts a cook if none is active, otherwise stops it. Returns `Cook started` / `Cook stopped`. |
| `GET` | `/api/status` | Latest reading as JSON. Returns `400` if no cook is active. |
| `POST` | `/api/set-temperature?temperature=225` | Sets the pit target. Range is 100–400 °F. Returns `400` if no cook is active or the value is out of range, `503` if the controller can't be reached. |
| `GET` | `/api/alive` | Liveness check. Returns `200` if the poll loop has run in the last `POLLSECONDS × 5` seconds and, during a cook, the last successful device read is also within that window. Otherwise returns `400`. |
| `GET` | `/api/ready` | Always `200`. |
| `GET` | `/heartbeat` | Always `200 Alive`. |
| `GET` | `/api` | Banner text. |
| `GET` | `/openapi/v1.json` | OpenAPI document (Development environment only). |

Example `/api/status` response:

```json
{
  "pit": 225,
  "meat1": 160,
  "setTemperature": 225,
  "blowerPercentage": 35,
  "lastUpdate": "2026-11-08T14:05:00"
}
```

Values the controller shows as `---` (for example, an unplugged probe) are reported as `0`.

## Home Assistant (MQTT)

At startup the app publishes discovery config to `homeassistant/sensor/flameboss_<id>/config`. On every poll during a cook, it publishes state to `homeassistant/sensor/flameboss_<id>/state`:

| `<id>` | Sensor | Unit |
|---|---|---|
| `pit` | `<FRIENDLYNAME> Pit Temperature` | °F |
| `meat1` | `<FRIENDLYNAME> Meat 1 Probe` | °F |
| `blower` | `<FRIENDLYNAME> Blower` | % |
| `set_temp` | `<FRIENDLYNAME> Set Temp` | °F |

Discovery messages aren't retained. If Home Assistant or the broker restarts, restart this app so the sensors get discovered again.

## Limitations

- Only **Meat 1** is read. The device page also shows Meat 2 and Meat 3, but they're ignored.
- Fahrenheit only.
- Cook history is kept in memory only. There's no persistence across restarts and no multi-instance support.
- Setting the temperature through the REST API requires an active cook (the dashboard button doesn't).
- Data comes from scraping the controller's HTML, so a firmware change to that page would break parsing.

## Project layout

```
Flameboss/
  Program.cs                  DI setup, settings binding/validation, endpoint mapping
  Configuration.cs            Settings record (bound from env vars / user secrets)
  FlameBossService.cs         Device scraping, set-temperature POST, start/stop/toggle cook
  FlameBossPollingService.cs  Background poll loop (active only during a cook)
  FlamebossData.cs            Static in-memory state and cook history
  FlameBossController.cs      REST API
  MqttService.cs              MQTT connection, HA discovery, state publishing
  Components/Pages/Home.razor Dashboard and charts
  Components/SetTemperatureDialog.razor  Set-temperature dialog (150–400 °F)
  output.html, set.html       Sample controller pages (parsing reference)
  Dockerfile
compose.yaml                  Local container run; settings from shell or .env
.github/workflows/image.yaml  Builds and pushes ghcr.io/factotuminok/flameboss
```
