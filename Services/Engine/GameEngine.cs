using GeoPolitics_Game.Models;

namespace GeoPolitics_Game.Services.Engine
{
    public class GameEngine : IGameEngine
    {
        public const int BoardSize = 10;
        private readonly IRandomSource _random;

        public GameEngine(IRandomSource random) => _random = random;

        // ===================== SETUP =====================

        public Game CreateDraft(string ownerUserId, string ownerName, Country homeland,
                                IReadOnlyList<Country> rivals, Difficulty difficulty, GameSettings settings)
        {
            var game = new Game
            {
                OwnerUserId = ownerUserId,
                OwnerName = ownerName,
                StartCountryCode = homeland.Code,
                Difficulty = difficulty,
                Settings = settings,
                Status = GameStatus.Setup
            };

            var human = new PlayerState { Name = ownerName, HomeCountryCode = homeland.Code, IsHuman = true };
            game.Players.Add(human);
            game.HumanPlayerId = human.Id;
            game.Territories.Add(NewTerritory(homeland, human.Id));   // starts with full soldier value

            AddRivals(game, rivals);
            return game;
        }

        public void ReplaceRivals(Game game, IReadOnlyList<Country> rivals)
        {
            if (game.Status != GameStatus.Setup)
                throw new GameRuleException("Rivals can only be redrawn before the campaign launches.");

            game.Players.RemoveAll(p => !p.IsHuman);
            game.Territories.RemoveAll(t => t.OwnerPlayerId != game.HumanPlayerId);
            AddRivals(game, rivals);
        }

        public List<GameEvent> Launch(Game game)
        {
            if (game.Status != GameStatus.Setup)
                throw new GameRuleException("This campaign has already started.");

            var events = new List<GameEvent>();

            // Rule: the game randomly decides who goes first
            game.TurnOrder = Shuffle(game.Players.Select(p => p.Id).ToList());

            // Give each country a random position on the board
            var slots = Shuffle(Enumerable.Range(0, BoardSize).ToList());
            for (int i = 0; i < game.Territories.Count; i++) game.Territories[i].Slot = slots[i];

            game.Status = GameStatus.InProgress;
            game.Round = 1;
            game.CurrentTurnIndex = 0;

            var first = game.GetPlayer(game.CurrentPlayerId);
            Record(game, events, GameEventType.GameStarted, first.Id,
                   $"The server rolled for turn order: {first.Name} strikes first.", isMove: false);

            BeginTurn(game, events);
            return events;
        }

        // ===================== REINFORCE =====================

        public List<GameEvent> PlaceReinforcements(Game game, string playerId, IDictionary<string, int> placements)
        {
            game.EnsureTurnOf(playerId);
            if (game.Phase != TurnPhase.Reinforce)
                throw new GameRuleException("There are no reinforcements to place right now.");

            var player = game.GetPlayer(playerId);
            if (placements.Values.Any(v => v < 0))
                throw new GameRuleException("Soldier counts cannot be negative.");
            if (placements.Values.Sum() != player.ReinforcementsToPlace)
                throw new GameRuleException($"Place exactly {player.ReinforcementsToPlace} soldiers.");

            // Check everything BEFORE changing anything
            foreach (var (code, _) in placements.Where(p => p.Value > 0))
                if (game.GetTerritory(code).OwnerPlayerId != playerId)
                    throw new GameRuleException("You can only reinforce countries you control.");

            var parts = new List<string>();
            foreach (var (code, amount) in placements.Where(p => p.Value > 0))
            {
                var territory = game.GetTerritory(code);
                territory.AddSoldiers(amount);
                game.PlacedThisTurn[territory.CountryCode] =
                    game.PlacedThisTurn.GetValueOrDefault(territory.CountryCode) + amount;
                parts.Add($"+{amount} to {territory.CountryName}");
            }

            int total = player.ReinforcementsToPlace;
            player.ReinforcementsToPlace = 0;
            game.Phase = TurnPhase.Main;

            var events = new List<GameEvent>();
            var e = Record(game, events, GameEventType.Reinforced, playerId, $"Placed {string.Join(", ", parts)}.");
            e.Amount = total;
            e.Placements = new Dictionary<string, int>(game.PlacedThisTurn);
            return events;
        }

        // ===================== ATTACK =====================

        public List<GameEvent> StartAttack(Game game, string playerId, string fromCode, string toCode)
        {
            game.EnsureTurnOf(playerId);
            if (game.Phase == TurnPhase.Reinforce) throw new GameRuleException("Place your reinforcements before attacking.");
            if (game.ActiveBattle != null) throw new GameRuleException("An attack is already in progress.");
            if (game.AttackUsedThisTurn) throw new GameRuleException("You have already attacked this turn.");

            var from = game.GetTerritory(fromCode);
            var to = game.GetTerritory(toCode);
            if (from.OwnerPlayerId != playerId) throw new GameRuleException("You can only attack from a country you control.");
            if (to.OwnerPlayerId == playerId) throw new GameRuleException("You cannot attack your own country.");
            if (!from.CanAttack) throw new GameRuleException($"{from.CountryName} needs at least 2 soldiers to attack.");

            game.ActiveBattle = new BattleState
            {
                AttackerCode = from.CountryCode,
                DefenderCode = to.CountryCode,
                AttackerPlayerId = playerId,
                DefenderPlayerId = to.OwnerPlayerId,
                AttackerStartSoldiers = from.Soldiers,
                DefenderStartSoldiers = to.Soldiers
            };
            game.AttackUsedThisTurn = true;   // Rule: one attack per turn
            game.Phase = TurnPhase.Battle;

            var player = game.GetPlayer(playerId);
            var events = new List<GameEvent>();
            var e = Record(game, events, GameEventType.AttackStarted, playerId,
                $"{player.Name} launches {from.CountryName} ({from.Soldiers}) at {to.CountryName} ({to.Soldiers}).");
            e.FromCode = from.CountryCode;
            e.ToCode = to.CountryCode;
            return events;
        }

        public List<GameEvent> Roll(Game game, string playerId)
        {
            game.EnsureTurnOf(playerId);
            var battle = game.ActiveBattle ?? throw new GameRuleException("There is no attack in progress.");
            var attacker = game.GetTerritory(battle.AttackerCode);
            var defender = game.GetTerritory(battle.DefenderCode);

            // Rule: dice = smaller of (attackers - 1) and defenders; each die is one soldier
            int dice = attacker.DiceAgainst(defender);
            var attackerDice = RollDice(dice);
            var defenderDice = RollDice(dice);

            // Rule: die 1 vs die 1, die 2 vs die 2... higher wins, a tie kills nobody
            int attackerLosses = 0, defenderLosses = 0;
            for (int i = 0; i < dice; i++)
            {
                if (attackerDice[i] > defenderDice[i]) defenderLosses++;
                else if (attackerDice[i] < defenderDice[i]) attackerLosses++;
            }
            attacker.LoseSoldiers(attackerLosses);
            defender.LoseSoldiers(defenderLosses);

            battle.Rolls.Add(new BattleRoll
            {
                AttackerDice = attackerDice,
                DefenderDice = defenderDice,
                AttackerLosses = attackerLosses,
                DefenderLosses = defenderLosses,
                AttackerSoldiersAfter = attacker.Soldiers,
                DefenderSoldiersAfter = defender.Soldiers,
                OddsAfter = BattleOdds.WinProbability(attacker.Soldiers, defender.Soldiers)
            });

            var events = new List<GameEvent>();
            var e = Record(game, events, GameEventType.DiceRolled, playerId,
                $"Roll {battle.Rolls.Count}: {attacker.CountryName} -{attackerLosses}, {defender.CountryName} -{defenderLosses}. " +
                $"Odds now {BattleOdds.AsPercent(attacker.Soldiers, defender.Soldiers)}%.", isMove: false);
            e.FromCode = attacker.CountryCode;
            e.ToCode = defender.CountryCode;
            e.AttackerDice = attackerDice;
            e.DefenderDice = defenderDice;
            e.AttackerLosses = attackerLosses;
            e.DefenderLosses = defenderLosses;
            e.AttackerSoldiersAfter = attacker.Soldiers;
            e.DefenderSoldiersAfter = defender.Soldiers;
            e.TargetPlayerId = battle.DefenderPlayerId;

            if (defender.Soldiers == 0)
            {
                Conquer(game, events, playerId, attacker, defender);   // Rule: defender wiped out
            }
            else if (attacker.Soldiers <= 1)
            {
                // Rule: attacker down to 1 soldier -> attack fails, both keep their countries
                game.ActiveBattle = null;
                game.Phase = TurnPhase.Main;
                var failed = Record(game, events, GameEventType.AttackFailed, playerId,
                    $"The attack on {defender.CountryName} fails: {attacker.CountryName} is down to 1 soldier.", isMove: false);
                failed.FromCode = attacker.CountryCode;
                failed.ToCode = defender.CountryCode;
            }
            return events;
        }

        public List<GameEvent> Retreat(Game game, string playerId)
        {
            game.EnsureTurnOf(playerId);
            var battle = game.ActiveBattle ?? throw new GameRuleException("There is no attack in progress.");
            var attacker = game.GetTerritory(battle.AttackerCode);
            var defender = game.GetTerritory(battle.DefenderCode);
            var player = game.GetPlayer(playerId);

            var events = new List<GameEvent>();
            var e = Record(game, events, GameEventType.Retreated, playerId,
                $"{player.Name} stops the attack on {defender.CountryName}; {attacker.CountryName} keeps {attacker.Soldiers} soldiers.",
                isMove: false);
            e.FromCode = attacker.CountryCode;
            e.ToCode = defender.CountryCode;

            game.ActiveBattle = null;
            game.Phase = TurnPhase.Main;
            AdvanceTurn(game, events);   // Rule: stopping an attack ends your turn
            return events;
        }

        // ===================== TRANSFER =====================

        public List<GameEvent> Transfer(Game game, string playerId, string fromCode, string toCode, int amount)
        {
            game.EnsureTurnOf(playerId);
            if (game.ActiveBattle != null) throw new GameRuleException("You cannot transfer while an attack is in progress.");
            if (game.Phase == TurnPhase.Reinforce) throw new GameRuleException("Place your reinforcements first.");

            var from = game.GetTerritory(fromCode);
            var to = game.GetTerritory(toCode);
            if (from.OwnerPlayerId != playerId) throw new GameRuleException("You can only move soldiers out of your own countries.");

            from.TransferTo(to, amount);   // the Territory enforces "keep at least 1"

            var events = new List<GameEvent>();
            var e = Record(game, events, GameEventType.Transferred, playerId,
                $"Moved {amount} soldier{(amount == 1 ? "" : "s")} {from.CountryName} -> {to.CountryName}.");
            e.FromCode = from.CountryCode;
            e.ToCode = to.CountryCode;
            e.Amount = amount;
            return events;
        }

        // ===================== END TURN =====================

        public List<GameEvent> EndTurn(Game game, string playerId)
        {
            game.EnsureTurnOf(playerId);
            if (game.ActiveBattle != null) throw new GameRuleException("Finish or retreat from your attack first.");
            if (game.Phase == TurnPhase.Reinforce) throw new GameRuleException("Place your reinforcements first.");

            var player = game.GetPlayer(playerId);
            var events = new List<GameEvent>();
            Record(game, events, GameEventType.TurnSkipped, playerId,
                game.AttackUsedThisTurn ? $"{player.Name} ends the turn." : $"{player.Name} skips the attack this turn.");

            AdvanceTurn(game, events);
            return events;
        }

        // ===================== PRIVATE HELPERS =====================

        private void AddRivals(Game game, IReadOnlyList<Country> rivals)
        {
            if (rivals.Count != BoardSize - 1)
                throw new GameRuleException($"A campaign needs exactly {BoardSize - 1} rivals.");
            if (rivals.Any(r => r.Code == game.StartCountryCode) ||
                rivals.Select(r => r.Code).Distinct().Count() != rivals.Count)
                throw new GameRuleException("Rivals must be 9 different countries, not including your homeland.");

            foreach (var country in rivals)
            {
                var profile = (NpcProfile)(_random.Next(3) + 1);   // Aggressor, Defender or Opportunist
                var npc = new PlayerState { Name = $"The {profile} ({country.Name})", HomeCountryCode = country.Code, Profile = profile };
                game.Players.Add(npc);
                game.Territories.Add(NewTerritory(country, npc.Id));
            }
        }

        private static Territory NewTerritory(Country country, string ownerId) => new()
        {
            CountryCode = country.Code,
            CountryName = country.Name,
            SoldierValue = country.SoldierValue,
            OwnerPlayerId = ownerId,
            Soldiers = country.SoldierValue
        };

        private void BeginTurn(Game game, List<GameEvent> events)
        {
            var player = game.GetPlayer(game.CurrentPlayerId);
            game.AttackUsedThisTurn = false;
            game.PlacedThisTurn.Clear();
            game.MoveInTurn = 0;

            if (game.Round >= 2)
            {
                // Rule: from round 2, 2 new soldiers per country you control
                player.ReinforcementsToPlace = 2 * game.TerritoryCount(player.Id);
                game.Phase = TurnPhase.Reinforce;
                Record(game, events, GameEventType.TurnStarted, player.Id,
                       $"Round {game.Round}: {player.Name} receives {player.ReinforcementsToPlace} new soldiers.", isMove: false)
                    .Amount = player.ReinforcementsToPlace;
            }
            else
            {
                player.ReinforcementsToPlace = 0;
                game.Phase = TurnPhase.Main;
                Record(game, events, GameEventType.TurnStarted, player.Id,
                       $"Round 1: {player.Name}'s turn.", isMove: false);
            }
        }

        private void AdvanceTurn(Game game, List<GameEvent> events)
        {
            int index = game.CurrentTurnIndex;
            for (int step = 0; step < game.TurnOrder.Count; step++)
            {
                index++;
                if (index >= game.TurnOrder.Count)
                {
                    // Rule: a round ends once every player has had a turn
                    game.TakeSnapshot();
                    game.Round++;
                    index = 0;
                }
                // Rule: eliminated players are skipped
                if (!game.GetPlayer(game.TurnOrder[index]).IsEliminated) break;
            }
            game.CurrentTurnIndex = index;
            BeginTurn(game, events);
        }

        private void Conquer(Game game, List<GameEvent> events, string playerId, Territory attacker, Territory defender)
        {
            string loserId = defender.OwnerPlayerId;
            int spoils = defender.ConquestSpoils;

            // Rule: conqueror gains max(2, value / 2) soldiers, placed in the conquered country
            defender.CaptureBy(playerId, spoils);
            game.ActiveBattle = null;
            game.Phase = TurnPhase.Main;   // the player may now transfer more soldiers in

            var conqueror = game.GetPlayer(playerId);
            var e = Record(game, events, GameEventType.Conquered, playerId,
                $"{conqueror.Name} conquers {defender.CountryName}! +{spoils} soldiers.", isMove: false);
            e.FromCode = attacker.CountryCode;
            e.ToCode = defender.CountryCode;
            e.Amount = spoils;
            e.IsKeyMoment = playerId == game.HumanPlayerId || loserId == game.HumanPlayerId;

            // Rule: a player with no countries left is eliminated
            if (game.TerritoryCount(loserId) == 0)
            {
                var loser = game.GetPlayer(loserId);
                loser.IsEliminated = true;
                loser.ReinforcementsToPlace = 0;
                Record(game, events, GameEventType.PlayerEliminated, loserId,
                       $"{loser.Name} ({loser.HomeCountryCode}) has been eliminated.", isMove: false)
                    .IsKeyMoment = true;
            }

            CheckForGameEnd(game, events);
        }

        private static void CheckForGameEnd(Game game, List<GameEvent> events)
        {
            var human = game.GetPlayer(game.HumanPlayerId);
            var owners = game.Territories.Select(t => t.OwnerPlayerId).Distinct().ToList();

            if (owners.Count == 1 && owners[0] == human.Id)
            {
                Finish(game, events, GameStatus.Won, human.Id, GameEventType.GameWon,
                       $"The world is yours! {human.Name} holds all {BoardSize} nations.");
            }
            else if (human.IsEliminated)
            {
                var leader = game.Players.Where(p => !p.IsEliminated)
                                         .OrderByDescending(p => game.TerritoryCount(p.Id))
                                         .First();
                Finish(game, events, GameStatus.Lost, leader.Id, GameEventType.GameLost,
                       $"Your banner falls. {leader.Name} now leads the world.");
            }
        }

        private static void Finish(Game game, List<GameEvent> events, GameStatus status,
                                   string winnerId, GameEventType type, string message)
        {
            game.Status = status;
            game.Phase = TurnPhase.Finished;
            game.WinnerPlayerId = winnerId;
            game.FinishedAt = DateTime.UtcNow;
            game.ActiveBattle = null;
            game.TakeSnapshot();
            Record(game, events, type, winnerId, message, isMove: false).IsKeyMoment = true;
        }

        private List<int> RollDice(int count)
        {
            var dice = new List<int>(count);
            for (int i = 0; i < count; i++) dice.Add(_random.RollDie());
            return dice;
        }

        // Fisher-Yates shuffle
        private List<T> Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
            return items;
        }

        // Creates an event describing what just happened
        private static GameEvent Record(Game game, List<GameEvent> events, GameEventType type,
                                        string actorId, string message, bool isMove = true)
        {
            if (isMove) game.MoveInTurn++;
            var actor = game.Players.FirstOrDefault(p => p.Id == actorId);
            var e = new GameEvent
            {
                GameId = game.Id,
                Round = game.Round,
                Move = game.MoveInTurn,
                Type = type,
                ActorPlayerId = actorId,
                IsNpc = actor != null && !actor.IsHuman,
                Message = message
            };
            events.Add(e);
            return e;
        }
    }
}
