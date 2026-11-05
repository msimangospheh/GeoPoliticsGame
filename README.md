# Geopolitics: The Imperial Struggle

A single-player, Risk-style strategy game. Pick a homeland, roll the dice against nine NPC rivals that play to win, and conquer all ten nations, while an AI chronicler narrates every battle.

**Live site:** https://geopoliticsgame.onrender.com
**Source:** https://github.com/msimangospheh/GeoPoliticsGame
**Built for:** UCT INF4001N Entrance Exam 2027

> **First visit may take up to a minute.** The free hosting plan puts the server to sleep after 15 minutes without visitors; the first request wakes it up. After that it is fast.

The marker login and database access details are in the Amathuba submission, not in this public file.

---

## Contents
1. [How to play](#1-how-to-play)
2. [Features](#2-features)
3. [Tech stack](#3-tech-stack)
4. [Architecture](#4-architecture)
5. [Database design](#5-database-design)
6. [Security](#6-security)
7. [AI integration](#7-ai-integration)
8. [Running it locally](#8-running-it-locally)
9. [Configuration reference](#9-configuration-reference)
10. [Tests](#10-tests)
11. [Deployment](#11-deployment)
12. [Known limitations](#12-known-limitations)
13. [Credits](#13-credits)

---

## 1. How to play

1. **Sign up** (free) and open **My campaigns → New campaign**.
2. **Choose your homeland** from 234 real countries (searchable). Nine rivals are drawn at random, each with an NPC personality.
3. **Set the rules**: NPC difficulty, the AI chronicler's voice, and whether spectators may watch. Launch: the server rolls for turn order.
4. **On your turn**
   - From round 2, you receive **2 soldiers for every nation you hold** and must place them all.
   - **Attack** once per turn from a nation with at least 2 soldiers, or **transfer** soldiers between your own nations.
5. **Battles**
   - The attacker rolls one die per soldier that can fight (one soldier always stays home); the defender rolls one die per defending soldier.
   - Dice are sorted and matched **1-to-1, highest against highest**. The higher die wins and the loser loses one soldier. **Ties: nobody falls.**
   - Keep rolling or retreat. The attack ends when the defender reaches 0, the attacker is down to 1, or you retreat.
   - Conquering a nation pays **max(2, its soldier value ÷ 2)** extra soldiers.
6. **Win** by holding all ten nations. Lose when your last nation falls.

**Starting soldiers come from population:**

| Population | Soldiers | Population | Soldiers |
|---|---|---|---|
| up to 5 million | 1 | up to 100 million | 6 |
| up to 10 million | 2 | up to 125 million | 7 |
| up to 25 million | 3 | up to 150 million | 8 |
| up to 50 million | 4 | more than 150 million | 9 |
| up to 75 million | 5 | | |

---

## 2. Features

### Core requirements
| Requirement | Where it lives |
|---|---|
| 10 nations, soldiers by population | `Models/SoldierTable.cs`, `Data/Seed/countries.json` (234 countries) |
| Dice battles matched 1-to-1, transfers, reinforcements | `Services/Engine/GameEngine.cs` (pure game rules, unit-tested) |
| NPCs that play to win, with a justification for every move | `Services/Npc/NpcBrain.cs`: scores every possible attack by win odds × spoils, minus risk to its garrison; reinforces its most threatened border; retreats below its line |
| Every NPC decision visible, also during fast-forward | NPC decision log on the board, full **War Council Ledger** (`/Play/{id}/Ledger`), replay banner with 1×/2×/4× fast-forward (`wwwroot/js/replay.js`) |
| AI commentary on battles | `Services/Commentary/LlmCommentaryService.cs` (LLM) with a template fallback |
| Anyone with the link can watch; only the logged-in owner can play | `[AllowAnonymous]` board views + owner checks on every action (`Security/UserExtensions.cs`, `Controllers/PlayController.cs`) |
| NoSQL database, deployed online | MongoDB Atlas; Docker on Render |

### Bonus features
- **Real-time spectating**: shared links update live with SignalR (`Hubs/GameHub.cs`, `wwwroot/js/live.js`), a "watching" counter and cheer buttons.
- **NPC profiles**: Aggressor, Defender, Opportunist, each with different attack and retreat thresholds.
- **Difficulty levels**: Recruit, Commander, Warlord.
- **Replays and game history**: every move is stored as an event; NPC turns play back with animations; key moments on the result screen.
- **Analytics**: player stats in the War Room; **public leaderboard** with platform-wide totals (MongoDB aggregation pipeline).
- **Graphics and animation**: battlefield theme with blurred glass panels, 3D dice that tumble before landing, stick soldiers carrying their country's flag, parallax world map, territory-over-time chart, victory confetti.
- **Sound**: dice, drums, fanfares and optional war-drum music, all synthesised in the browser with the Web Audio API (`wwwroot/js/sound.js`), with volume controls.
- **Accounts**: forgot password with a 6-digit email code, settings page (commander name, game defaults, sound, change password).

---

## 3. Tech stack

| Layer | Choice |
|---|---|
| Framework | ASP.NET Core MVC (.NET 8), Razor views |
| Database | MongoDB Atlas (cloud), MongoDB.Driver 3 |
| Login | ASP.NET Core Identity stored in MongoDB (AspNetCore.Identity.MongoDbCore), cookie authentication |
| Real-time | ASP.NET Core SignalR |
| AI | Groq's OpenAI-compatible chat API (`openai/gpt-oss-20b`) |
| Email | Brevo transactional email API (HTTPS) |
| Front end | Bootstrap 5, custom CSS, vanilla JavaScript, Web Audio API, canvas-confetti |
| Tests | xUnit |
| Hosting | Docker container on Render |

---

## 4. Architecture

The app follows **MVC with a service layer and the repository pattern**. Controllers stay thin; game rules live in a pure engine with no database or web code, so they can be unit-tested.

```
Browser ──► Controllers ──► Services ──────────► Repositories ──► MongoDB Atlas
            (HTTP, auth,    GameService            (Data/)
             validation)    ├─ GameEngine  (pure rules, dice, odds)
                            ├─ NpcBrain    (NPC decisions + reasons)
                            ├─ Commentary  (LLM, with template fallback)
                            ├─ Email       (Brevo)
                            └─ Profile     (settings)
            SignalR hub ◄── PlayController pushes "game updated" to spectators
```

| Folder | Contents |
|---|---|
| `Controllers/` | Account, Password (reset), Games (lobby & setup), Play (board, battle, ledger, result), Home (landing & live), Leaderboard, Settings, Countries |
| `Models/` | Domain classes: `Game`, `Territory`, `PlayerState`, `GameEvent`, `Country`, `LeaderboardEntry` |
| `Services/Engine/` | `IGameEngine`/`GameEngine` rules, `BattleOdds` (exact odds by dynamic programming), `IRandomSource` (cryptographically secure dice) |
| `Services/Npc/` | `INpcPlayer`/`NpcBrain`, `NpcStyle` per difficulty and profile |
| `Services/Commentary/`, `Email/`, `Profile/` | External services behind interfaces |
| `Data/` | `MongoContext` (the one place that talks to MongoDB), conventions, seeder, repositories, data-protection key store |
| `Identity/` | `ApplicationUser` (+ saved preferences) |
| `ViewModels/` | Form and page models with validation attributes |
| `Hubs/` | SignalR `GameHub` |
| `Tests/` | xUnit test project |

**OOP in practice:** interfaces for every service and repository (dependency injection, swappable implementations such as the template commentary fallback); encapsulation in the domain model (e.g. `Territory.LoseSoldiers`, `CaptureBy`, `TransferTo` enforce their own rules); a custom `GameRuleException` for rule violations; records for immutable NPC styles.

---

## 5. Database design

MongoDB Atlas database `geopolitics`. A game is read and written as a whole on every move, so its state is **embedded in one document** (one read per page, atomic updates). The move history can grow long, so it lives in its **own collection** and is referenced by `gameId`.

| Collection | One document is… | Key fields |
|---|---|---|
| `countries` | a real country (234 seeded from `Data/Seed/countries.json`) | `code`, `name`, `population`, `soldierValue` |
| `games` | a whole campaign | `ownerUserId`, `ownerName`, `status`, `difficulty`, `round`, `turnOrder`, embedded `players[]`, `territories[]`, `activeBattle`, `history[]` (territory per round), `settings` |
| `gameEvents` | one move or decision | `gameId`, `seq`, `round`, `type`, `actorPlayerId`, `isNpc`, `message`, `reason` (the "Why:"), `metrics[]`, `commentary`, dice, `isKeyMoment` |
| `users`, `roles` | Identity accounts | hashed password, lockout fields, claims, `DisplayName`, `Preferences` |
| `dataProtectionKeys` | keys that protect login cookies | so logins survive server restarts |

**Indexes** (created on startup in `MongoContext.CreateIndexesAsync`):
- `countries.name`: country search
- `games.ownerUserId + updatedAt`: "My campaigns", newest first
- `games.status + startCountryCode`: stats queries
- `gameEvents.gameId + seq` (**unique**): events in order, never two with the same number

**Conventions:** camelCase field names, enums stored as readable strings (`"Won"`, not `2`), unknown fields ignored for safe schema changes.

**Aggregation:** the leaderboard is computed by MongoDB itself (`Data/Repositories/MongoLeaderboardRepository.cs`): `$match` finished games → `$group` by player (played, wins, fastest win) → `$addFields` win rate → optional name search → `$sort` → `$limit`.

---

## 6. Security

- **Passwords:** hashed by ASP.NET Core Identity; minimum 8 characters with mixed character types.
- **Brute force:** 5 wrong passwords (or reset codes) lock the account for 5 minutes. "Send me a code" is rate-limited to 3 requests per 10 minutes per IP.
- **Password reset:** 6-digit time-limited code from Identity's email token provider, tied to the account's security stamp (invalid once the password changes). The page never reveals whether an email has an account.
- **Authorization:** viewing a game is public when spectators are allowed; every action that changes a game checks that the logged-in user owns it (`User.IsOwnerOf(game)`). Visitors cannot play for a player. Hidden or foreign games return 404 so ids cannot be probed.
- **Never trust the browser:** all moves are re-validated by the engine on the server; dice are rolled on the server with a cryptographically secure random generator.
- **CSRF:** anti-forgery tokens required on every POST (global filter).
- **Validation:** data annotations on every form model, checked in the browser and again on the server; enum and option values checked against allowed lists.
- **Open redirects:** login only returns to local URLs.
- **Secrets:** no secrets in source control. Locally they live in .NET user-secrets; in production in Render environment variables.
- **HTTPS:** enforced in production (HSTS); forwarded headers trusted from the hosting proxy.

---

## 7. AI integration

After each roll, `LlmCommentaryService` sends the battle facts (nations, dice, losses, odds) to an LLM through Groq's OpenAI-compatible API and asks for one vivid line in the voice the player chose: **sports broadcaster**, **war poet** or **dry historian**. The line is shown on the battle screen, in NPC replays and stored with the event.

It is built to never break the game: an 8-second timeout, and if the API is unavailable, disabled or has no key, a **template commentator** writes the line instead. Commentary is saved with the event, so replays reuse it without new API calls.

---

## 8. Running it locally

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A MongoDB Atlas cluster (the free M0 tier is enough) with your IP allowed under **Network Access**
- Optional: a free [Groq](https://console.groq.com) API key (AI commentary) and a free [Brevo](https://www.brevo.com) API key (reset emails)

### Steps
```bash
git clone https://github.com/msimangospheh/GeoPoliticsGame.git
cd GeoPoliticsGame

# Secrets are stored outside the project folder, never in git
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb+srv://USER:PASSWORD@YOUR-CLUSTER.mongodb.net/?retryWrites=true&w=majority"
dotnet user-secrets set "Llm:ApiKey" "your-groq-key"            # optional
dotnet user-secrets set "Email:ApiKey" "your-brevo-key"         # optional
dotnet user-secrets set "Email:SenderEmail" "you@example.com"   # optional, must be verified in Brevo

dotnet run --launch-profile https
```
Open https://localhost:7002. On first start the app creates the indexes and seeds the 234 countries (look for `[OK] Connected to MongoDB Atlas` in the console).

- **Without a Groq key:** the template commentator is used.
- **Without a Brevo key (Development only):** reset codes are printed to the console instead of emailed.

In Visual Studio 2022: open `GeoPolitics Game.sln`, right-click the project → **Manage User Secrets** to add the same values, then run the **https** profile.

---

## 9. Configuration reference

| Setting | Where | Meaning |
|---|---|---|
| `MongoDb:ConnectionString` | secret | Atlas connection string (required) |
| `MongoDb:DatabaseName` | appsettings.json | default `geopolitics` |
| `Llm:ApiKey` | secret | Groq key; without it, template commentary |
| `Llm:BaseUrl`, `Llm:Model`, `Llm:Enabled`, `Llm:TimeoutSeconds` | appsettings.json | LLM endpoint and behaviour |
| `Email:ApiKey`, `Email:SenderEmail` | secret | Brevo key and verified sender |
| `Email:SenderName` | optional | default "Geopolitics War Room" |

On Render (and any environment variable setup), replace `:` with a double underscore: `MongoDb__ConnectionString`, `Llm__ApiKey`, `Email__ApiKey`, `Email__SenderEmail`.

---

## 10. Tests

```bash
cd Tests/GeoPolitics_Game.Tests
dotnet test
```
25 xUnit tests cover the battle rules and odds (`EngineTests`), NPC decision-making (`NpcBrainTests`), commentary fallback (`CommentaryTests`) and ownership/access rules (`SecurityTests`). The engine uses an injectable `IRandomSource`, so dice can be fixed in tests.

---

## 11. Deployment

The app runs as a Docker container on [Render](https://render.com) (free web service), built from the `Dockerfile` in the repo root:

1. Render → **New → Web Service** → connect the GitHub repository; Language **Docker**; root directory empty.
2. Environment variables: `MongoDb__ConnectionString`, `Llm__ApiKey`, `Email__ApiKey`, `Email__SenderEmail`, `PORT=8080`.
3. MongoDB Atlas → **Network Access** → allow `0.0.0.0/0` (Render's free plan has no fixed IP; access is still protected by the database password).
4. Every push to `master` redeploys automatically.

Email is sent through Brevo's HTTPS API because Render's free plan blocks outgoing SMTP ports.

---

## 12. Known limitations

- **Cold starts:** the free server sleeps after 15 minutes of inactivity; the first request takes about a minute.
- **Email deliverability:** reset emails are sent from a personal Gmail address through Brevo, so the first one may land in spam.
- **Sound:** browsers block audio until you click something on the page, so the very first page you open may be silent until your first click.
- **Single server:** SignalR runs in memory, which is fine for one instance; scaling out would need a backplane such as Redis.

---

## 13. Credits

- Flags: [flagcdn.com](https://flagcdn.com)
- Fonts: Black Ops One, Inter and Cormorant Garamond from Google Fonts
- Confetti: [canvas-confetti](https://github.com/catdad/canvas-confetti)
- Country population data seeded into `Data/Seed/countries.json`
- Background and icon artwork supplied by the author; world map, sounds and stick soldiers created for this project
- Author: _[Siphemandla Thembeka Simango, SMNSIP002]_
