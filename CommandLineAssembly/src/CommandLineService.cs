using Assets.Scripts.Missions;
using Assets.Scripts.Records;
using Assets.Scripts.Stats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CommandLineAssembly {
    partial class CommandLineService : MonoBehaviour {
        private KMGameCommands GameCommands;
        private KMGameInfo GameInfo;
        private KMBombInfo BombInfo;
        private KMService Service;
        private CommandLineWindow Console;

        private bool _enabled = false;
        private bool BombActive = false;
#if DEBUG
	public static readonly bool _isDebug = true;
#else
        public static readonly bool _isDebug = false;
#endif

        private List<Bomb> Bombs = new List<Bomb> { };
        private List<BombCommander> BombCommanders = new List<BombCommander> { };
        private List<Module> Modules = new List<Module> { };
        private static bool Leaderboardoff = false;

        private bool TwitchPlaysAvailable {
            get {
                if (_tpPresent == null) {
                    if (GameObject.Find("TwitchPlays_Info") != null) {
                        InitTwitchPlays();
                        return true;
                    }
                    return false;
                }
                return (bool) _tpPresent;

            }
        }
        private bool? _tpPresent = null;
        private GameObject TwitchPlays;
        private const string TwitchPlaysHandle = "CommandLine";

        public void InitTwitchPlays() {
            TwitchPlays = GameObject.Find("TwitchPlays_Info");
            var comp_gen = TwitchPlays.GetComponent("TwitchPlaysProperties");
            var tp_asm = comp_gen.GetType().Assembly;
            var useracc_type = tp_asm.GetType("UserAccess");
            var adduser_meth = useracc_type.GetMethod("AddUser");
            adduser_meth.Invoke(null, new object[] { TwitchPlaysHandle, 0x10000 | 0x8000 | 0x4000 | 0x2000 } );
        }

        private void Start() {
            GameCommands = GetComponent<KMGameCommands>();
            GameInfo = GetComponent<KMGameInfo>();
            BombInfo = GetComponent<KMBombInfo>();
            Service = GetComponent<KMService>();
            Console = GetComponent<CommandLineWindow>();
            SetUpCommands();
        }

        private void OnEnable() {
            _enabled = true;
            GameInfo = GetComponent<KMGameInfo>();
            GameInfo.OnStateChange += delegate (KMGameInfo.State state) {
                StateChange(state);
            };
        }

        private void OnDisable() {
            _enabled = false;
            if (Console.isVisible) Console.isVisible = false;
            GameInfo.OnStateChange -= delegate (KMGameInfo.State state) {
                StateChange(state);
            };
            StopAllCoroutines();
        }

        private void Update() {
            if (BombActive) {
                foreach (Module module in Modules) {
                    module.Update();
                }
            }
        }

        private void Log(string text) {
            Console.Log(text, LogType.Log);
        }

        private void LogError(string text) {
            Console.Log(text, LogType.Error);
        }

        private void LogWarning(string text) {
            Console.Log(text, LogType.Warning);
        }

        private BombCommander GetHeldBomb() {
            BombCommander held = null;
            foreach (BombCommander commander in BombCommanders) {
                if (commander.IsHeld())
                    held = commander;
            }
            return held;
        }

        private Module GetFocusedModule() {
            Module focused = null;
            foreach (Module module in Modules) {
                if (module.IsHeld())
                    focused = module;
            }
            return focused;
        }

        private static void ChangeLeaderboard(bool off) {
            if (RecordManager.Instance != null)
                RecordManager.Instance.DisableBestRecords = off;

            if (StatsManager.Instance != null)
                StatsManager.Instance.DisableStatChanges = off;

            Leaderboardoff = off;
        }

        private void SolveModule(Module module) {
            if (!Leaderboardoff) {
                ChangeLeaderboard(true);
                Debug.Log("[Command Line] Disabling leaderboard.");
            }
            try {
                KMBombModule KMmodule = module.BombComponent.GetComponent<KMBombModule>();
                CommonReflectedTypeInfo.HandlePassMethod.Invoke(module.BombComponent, null);
                foreach (MonoBehaviour behavior in module.BombComponent.GetComponentsInChildren<MonoBehaviour>(true)) {
                    behavior.StopAllCoroutines();
                }
            } catch (Exception ex) {
                Log($"Exception while force solving module: {ex}");
            }
        }

        private void StateChange(KMGameInfo.State state) {
            switch (state) {
                case KMGameInfo.State.Gameplay:
                    StartCoroutine(CheckForBomb());
                    break;
                case KMGameInfo.State.Setup:
                case KMGameInfo.State.Quitting:
                case KMGameInfo.State.PostGame:
                    Modules.Clear();
                    BombActive = false;
                    StopCoroutine(CheckForBomb());
                    Bombs.Clear();
                    Modules.Clear();
                    BombCommanders.Clear();
                    ChangeLeaderboard(false);
                    break;
            }
        }

        private IEnumerator CheckForBomb() {
            yield return new WaitUntil(() => (SceneManager.Instance.GameplayState.Bombs != null && SceneManager.Instance.GameplayState.Bombs.Count > 0));
            Bombs.AddRange(SceneManager.Instance.GameplayState.Bombs);
            int i = 0;
            string[] keyModules =
            {
            "SouvenirModule", "MemoryV2", "TurnTheKey", "TurnTheKeyAdvanced", "theSwan", "HexiEvilFMN", "taxReturns"
        };
            foreach (Bomb bomb in Bombs) {
                BombCommanders.Add(new BombCommander(bomb, i));
                foreach (BombComponent bombComponent in bomb.BombComponents) {
                    ComponentTypeEnum componentType = bombComponent.ComponentType;
                    bool keyModule = false;
                    string moduleName = "";

                    switch (componentType) {
                        case ComponentTypeEnum.Empty:
                        case ComponentTypeEnum.Timer:
                            continue;

                        case ComponentTypeEnum.NeedyCapacitor:
                        case ComponentTypeEnum.NeedyKnob:
                        case ComponentTypeEnum.NeedyVentGas:
                        case ComponentTypeEnum.NeedyMod:
                            moduleName = bombComponent.GetModuleDisplayName();
                            keyModule = true;
                            break;

                        case ComponentTypeEnum.Mod:
                            KMBombModule KMModule = bombComponent.GetComponent<KMBombModule>();
                            keyModule = keyModules.Contains(KMModule.ModuleType);
                            goto default;

                        default:
                            moduleName = bombComponent.GetModuleDisplayName();
                            break;
                    }
                    Module module = new Module(bombComponent, i) {
                        ComponentType = componentType,
                        IsKeyModule = keyModule,
                        ModuleName = moduleName
                    };

                    Modules.Add(module);
                }
                i++;
            }
            BombActive = true;
        }
    }
}