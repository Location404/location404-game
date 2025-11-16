# Location404 Game - Core Game Engine

Backend service for the Location404 geolocation guessing game, built with ASP.NET Core and SignalR.

## Features

### Round Timer System with Auto-Submit

The game includes a sophisticated round timer system that automatically manages player submissions:

- **Initial Timer**: 90 seconds when a new round starts
- **Adjusted Timer**: Reduces to 15 seconds after the first player submits their guess
- **Auto-Submit**: Players who don't submit within the time limit receive a null guess (0 points)
- **Real-time Synchronization**: Timer state is synchronized across clients via SignalR events

#### Implementation Components

**Backend Services:**
- `IRoundTimerService` - Timer service interface with Start, Cancel, Adjust, and GetRemaining operations
- `RedisRoundTimerService` - Production implementation using Redis keyspace notifications
- `InMemoryRoundTimerService` - Development/testing implementation
- `RoundTimerExpirationListener` - Background service that listens for timer expirations and triggers auto-submit

**SignalR Events:**
- `RoundStarted` - Includes `startedAt` timestamp and `durationSeconds` for client-side countdown
- `OpponentSubmitted` - Notifies when opponent has submitted their guess
- `TimerAdjusted` - Notifies when timer duration changes (90s → 15s)
- `RoundEnded` - Includes nullable coordinates for players who didn't submit

## Configuration

### Redis/Dragonfly Setup

The timer system requires Redis keyspace notifications to be enabled. If using Dragonfly, configure it with:

```yaml
command:
  - dragonfly
  - --logtostderr
  - --notify_keyspace_events=Ex
```

For standard Redis, you can enable keyspace notifications via:
```bash
redis-cli config set notify-keyspace-events Ex
```

Or add to your `redis.conf`:
```
notify-keyspace-events Ex
```

### Environment Variables

Copy `.env.example` to `.env` and configure:

```bash
# Redis Configuration (Required for timer system)
REDIS_ENABLED=true
REDIS_CONNECTION_STRING=localhost:6379

# RabbitMQ Configuration (Optional - for event publishing)
RABBITMQ_ENABLED=true
RABBITMQ_HOSTNAME=localhost
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=admin
RABBITMQ_PASSWORD=your_password_here

# Geo Data Service (Required for game functionality)
GEO_DATA_SERVICE_BASE_URL=http://localhost:5000

# CORS (Configure for your frontend)
CORS_ALLOWED_ORIGINS=http://localhost:5173,http://localhost:4200
```

## Development

### Prerequisites

- .NET 9.0 SDK
- Redis or Dragonfly (for timer functionality)
- RabbitMQ (optional, for event publishing)

### Building

```bash
dotnet restore
dotnet build
```

### Running

```bash
dotnet run --project src/Location404.Game.API
```

The API will be available at `https://localhost:5001` (or the port specified in launchSettings.json).

## Architecture

### Project Structure

- **Location404.Game.API** - ASP.NET Core web API with SignalR hubs and background services
- **Location404.Game.Application** - Application layer with DTOs, interfaces, and business logic
- **Location404.Game.Domain** - Domain entities and core business rules
- **Location404.Game.Infrastructure** - Infrastructure implementations (Redis, RabbitMQ, HTTP clients)

### Timer Flow

1. **Round Start**: Timer starts at 90 seconds via `IRoundTimerService.StartTimerAsync()`
2. **First Guess**: Timer adjusts to 15 seconds via `IRoundTimerService.AdjustTimerAsync()`
3. **Both Guess**: Timer is cancelled via `IRoundTimerService.CancelTimerAsync()`
4. **Expiration**: `RoundTimerExpirationListener` receives Redis keyspace notification
5. **Auto-Submit**: Background service processes remaining players with null guesses
6. **Round End**: Scores are calculated and `RoundEnded` event is broadcast

## SignalR Hub Methods

### Client → Server

- `JoinMatchmaking(JoinMatchmakingRequest)` - Join the matchmaking queue
- `StartRound(StartRoundRequest)` - Start a new round (authorized players only)
- `SubmitGuess(SubmitGuessRequest)` - Submit coordinate guess for current round
- `EndRound(EndRoundRequest)` - End the current round (when both players have guessed)

### Server → Client

- `MatchFound(MatchFoundResponse)` - Match has been found
- `RoundStarted(RoundStartedResponse)` - New round started with location and timer info
- `OpponentSubmitted` - Opponent has submitted their guess
- `TimerAdjusted` - Timer duration has been adjusted
- `RoundEnded(RoundEndedResponse)` - Round completed with scores and guesses
- `MatchEnded(MatchEndedResponse)` - Match completed with final winner
- `Error(string)` - Error message

## License

Copyright © 2025 Location404. All rights reserved.
