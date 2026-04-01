# Meta Services Tests

Integration tests requiring Orleans TestCluster + PostgreSQL.
Test grain-level business logic for user management, matchmaking, bots.

## Todo

### UserGrain — `Meta/Users/Common/UserGrain.cs`
- [ ] Create user — state persisted
- [ ] Read user — returns stored data
- [ ] User projection — updated on state change

### UserAuth — `Meta/Users/Auth/UserAuth.cs`
- [ ] Authenticate creates/loads user
- [ ] Duplicate auth — returns same user

### UserDeck — `Meta/Users/Decks/UserDeck.cs`
- [ ] Save deck — persisted to state
- [ ] Load deck — returns saved cards
- [ ] Default deck — when no saved deck

### UserProgression — `Meta/Users/Progression/UserProgression.cs`
- [ ] Record match result — updates progression
- [ ] XP calculation

### UserRating — `Meta/Users/Rating/UserRating.cs`
- [ ] Win increases rating by WinRating (from config)
- [ ] Loss decreases rating by LossRating (from config)
- [ ] Rating floor (minimum 0)

### MatchRecording — `Meta/Matches/Match.cs`
- [ ] Create match — state persisted
- [ ] Record match result — winner/loser stored
- [ ] Match history — linked to user

### BotFactory — `Meta/Bots/BotFactory.cs`
- [ ] Create bot — user + deck initialized
- [ ] Bot collection — add/remove/list

### BotCollection — `Meta/Bots/BotCollection.cs`
- [ ] Add bot to collection
- [ ] Remove bot from collection
- [ ] Random bot selection
