﻿using System.Diagnostics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using ExecutesPlugin.Managers;
using ExecutesPlugin.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace ExecutesPlugin
{
    [MinimumApiVersion(147)]
    public class ExecutesPlugin : BasePlugin
    {
        private const string Version = "0.0.1";
        
        #region Plugin Info
        public override string ModuleName => "Executes Plugin";
        public override string ModuleAuthor => "zwolof, B3none";
        public override string ModuleDescription => "Community executes for CS2.";
        public override string ModuleVersion => Version;
        #endregion

        private readonly GameManager _gameManager;
        private readonly QueueManager _queueManager;
        private readonly SpawnManager _spawnManager;

        public ExecutesPlugin(GameManager gameManager, QueueManager queueManager, SpawnManager spawnManager)
        {
            _gameManager = gameManager;
            _queueManager = queueManager;
            _spawnManager = spawnManager;
        }

        public override void Load(bool hotReload)
        {
            RegisterListener<Listeners.OnMapStart>(OnMapStart);

            Console.WriteLine("[Executes] ----------- CS2 Executes loaded -----------");

            if (hotReload)
            {
                OnMapStart(Server.MapName);
            }
        }

        private void OnMapStart(string mapName)
        {
            var loaded = _gameManager.LoadSpawns(mapName);

            if (!loaded)
            {
                Console.WriteLine("[Executes] Failed to load spawns.");
            }
        }

		[ConsoleCommand("css_addspawn", "Adds a spawn point to the map")]
		[CommandHelper(
			minArgs: 2,
			usage: "[T/CT] [A/B]",
			whoCanExecute: CommandUsage.CLIENT_ONLY
		)]
		[RequiresPermissions("@css/root")]
		public void OnCommandAddSpawn(CCSPlayerController? player, CommandInfo commandInfo)
		{

			if(!player.IsValidPlayer())
			{
				commandInfo.ReplyToCommand("[Executes] You must be a player to execute this command.");
				return;
			}
			
			var team = commandInfo.GetArg(1).ToUpper();

			if (team != "T" && team != "CT")
			{
				commandInfo.ReplyToCommand($"[Executes] You must specify a team [T / CT] - [Value: {team}].");
				return;
			}

			var bombsite = commandInfo.GetArg(2).ToUpper();

			if (bombsite != "A" && bombsite != "B")
			{
				commandInfo.ReplyToCommand($"[Executes] You must specify a bombsite [A / B] - [Value: {bombsite}].");
				return;
			}

			Debug.Assert(player != null, "player != null");
			Debug.Assert(player.PlayerPawn != null, "player.PlayerPawn != null");
			Debug.Assert(player.PlayerPawn.Value != null, "player.PlayerPawn.Value != null");
			
			var spawn = new Spawn
			{
				Id = 0,
				Name = "Spawn",
				Position = player.PlayerPawn.Value.AbsOrigin,
				Angle = player.PlayerPawn.Value.EyeAngles,
				Team = team == "T" ? CsTeam.Terrorist : CsTeam.CounterTerrorist,
				SpawnType = Enums.ESpawnType.SPAWNTYPE_LURKER
			};

			player.PrintToConsole("Latest spawn:");
			player.PrintToConsole("---------------------------");
			player.PrintToConsole(JsonConvert.SerializeObject(spawn));
			player.PrintToConsole("---------------------------");
		}

        [GameEventHandler]
        public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnPlayerTeam");
            @event.Silent = true;

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnPlayerConnectFull");

            var player = @event.Userid;

            if (player == null)
            {
                Console.WriteLine("[Executes] Failed to get player.");
                return HookResult.Continue;
            }

            player.ForceTeamTime = 3600.0f;

            // Add the player to the queue
            _queueManager._queue.Enqueue(player);            

            return HookResult.Continue;
        }
      
        [GameEventHandler]
        public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnPlayerDisconnect");

            var player = @event.Userid;

            if (player == null)
            {
                Console.WriteLine("[Executes] Failed to get player.");
                return HookResult.Continue;
            }

            // Remove the player from the queue if they disconnect
            _queueManager._queue.Drop(player);

            return HookResult.Continue;
        }
    
        [GameEventHandler]
        public HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnRoundFreezeEnd");

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnRoundPreStart(EventRoundPrestart @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnRoundPreStart");
            
            // TODO: Handle team swapping here
            
            // Attempt to get a random scenario from the game manager
            var scenario = _gameManager.GetRandomScenario();

            if (scenario == null)
            {
                Console.WriteLine("[Executes] Failed to get executes.");
                return HookResult.Continue;
            }
            
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnRoundStart");
            
            // If we have a scenario then setup the players
            _spawnManager.SetupSpawns(
                _gameManager.GetCurrentScenario()
            );
            
            return HookResult.Continue;
        }
      
        [GameEventHandler]
        public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            Console.WriteLine("[Executes] EventHandler::OnRoundEnd");
            
            return HookResult.Continue;
        }
    }

    public class WithDependencyInjectionPluginServiceCollection : IPluginServiceCollection<ExecutesPlugin>
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<QueueManager>();
            services.AddSingleton<GameManager>();
            services.AddSingleton<GrenadeManager>();
            services.AddSingleton<SpawnManager>();

            Console.WriteLine("[Executes] ----------- CS2 Executes services loaded -----------");
        }
    }
}
