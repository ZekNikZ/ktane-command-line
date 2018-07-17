using Assets.Scripts.Missions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CommandLineAssembly {
    class Command {
        public string Name;
        public string[] Aliases = null;
        public string DisablesLeaderboardReason = null;
        public string Help;
        public string Usage;
        public CommandAction Action;
        public delegate void CommandAction(string command);
    }

    partial class CommandLineService : MonoBehaviour {
        private List<Command> Commands = new List<Command>();
        private List<Command> DebugModeCommands = new List<Command>();

        private void SetUpCommands() {
            Commands.Add(new Command {
                Name = "help",
                Help = "Display a list of commands, or more information of a certain command.",
                Usage = "help [command]",
                Action = command => {
                    if (string.IsNullOrEmpty(command)) {
                        string result = "Command Reference:";
                        foreach (Command comm in Commands) {
                            result += $"\n    {comm.Usage} - {comm.Help}{(comm.DisablesLeaderboardReason != null ? "*" : "")}";
                        }
                        if (_isDebug) {
                            foreach (Command comm in DebugModeCommands) {
                                result += $"\n    {comm.Usage} - {comm.Help}†{(comm.DisablesLeaderboardReason != null ? "*" : "")}";
                            }
                            result += "\n    † A dagger (†) indicates that the command is a debug mode-only command.";
                        }
                        result += "\n    * An asterisk (*) indicates that the command will disable the mission leaderboard.";
                        if (TwitchPlaysAvailable) {
                            result += "\n    ! Twitch Plays commands can be sent using the '!' prefix.";
                        }
                        Log(result);
                    } else {
                        string arg = command.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
                        Command comm = null;
                        try {
                            comm = Commands.First(c => c.Name.ToLowerInvariant() == arg.ToLowerInvariant() || (c.Aliases != null && c.Aliases.Contains(arg.ToLowerInvariant())));
                        } catch (InvalidOperationException) { }
                        if (comm == null) {
                            try {
                                if (_isDebug) comm = DebugModeCommands.First(c => c.Name.ToLowerInvariant() == arg.ToLowerInvariant() || (c.Aliases != null && c.Aliases.Contains(arg.ToLowerInvariant())));
                            } catch (InvalidOperationException) { }
                        }
                        if (comm == null) {
                            LogError($"Command \"{arg}\" does not exist. Use the \"help\" command for a list of commands.");
                            return;
                        }

                        string result = $"Command information for \"{arg}\":";
                        result += $"\n    Name: {comm.Name}";
                        if (comm.Aliases != null) result += $"\n    Aliases: {string.Join(", ", comm.Aliases)}";
                        result += $"\n    Help String: {comm.Help}";
                        result += $"\n    Usage Info: {comm.Usage}";
                        if (comm.DisablesLeaderboardReason != null) {
                            result += $"\n    * This command will disable the leaderboard if used to {comm.DisablesLeaderboardReason}.";
                        }
                        Log(result);
                    }
                }
            });
            Commands.Add(new Command {
                Name = "clear",
                Help = "Clear the console window.",
                Usage = "clear",
                Action = _ => {
                    Console.Clear();
                }
            });
            Commands.Add(new Command {
                Name = "exit",
                Help = "Close the command line window.",
                Usage = "exit",
                Action = _ => {
                    Console.isVisible = false;
                    Console.currentEntry = "";
                }
            });
            Commands.Add(new Command {
                Name = "detonate",
                Help = "Detonate the currently held bomb. Optionally, include a cause of explosion.",
                Usage = "detonate [cause]",
                Action = message => {
                    if (BombActive) {
                        BombCommander heldBombCommader = GetHeldBomb();
                        string cause = "Detonate Command";
                        if (!string.IsNullOrEmpty(message)) cause = message;
                        if (heldBombCommader != null) {
                            Log($"Detonated bomb{(!string.IsNullOrEmpty(message) ? $" with reason \"{cause}\"" : "")}.");
                            Debug.Log("[Command Line] Detonating bomb.");
                            heldBombCommader.Detonate(cause);
                        } else {
                            LogWarning("Hold the bomb you wish to detonate.");
                        }
                    } else {
                        LogError("Can't detonate: no bombs are active.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "causestrike",
                Help = "Cause a strike on the currently held bomb. Optionally, include a reason for the strike.",
                Usage = "causestrike [reason]",
                Action = message => {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        string reason = "Strike Command";
                        if (!string.IsNullOrEmpty(message)) reason = message;
                        if (heldBombCommander != null) {
                            Log($"Caused a strike{(!string.IsNullOrEmpty(message) ? $" with reason \"{reason}\"" : "")}.");
                            Debug.Log("[Command Line] Causing strike.");
                            heldBombCommander.CauseStrike(reason);
                        } else {
                            LogWarning("Hold the bomb you wish to cause a strike on.");
                        }
                    } else {
                        LogError("Can't cause a strike: no bombs are active.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "time",
                Aliases = new string[] { "t" },
                Help = "Change the time of the currently held bomb.",
                Usage = "time <set|add|subtract> <number><h|m|s> [<number2><h|m|s>...]",
                DisablesLeaderboardReason = "achieve a faster time",
                Action = command => {
                    string[] parts = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!string.IsNullOrEmpty(command) && parts[0].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set")) {
                        if (BombActive) {
                            bool negative = parts[0].EqualsAny("subtract", "decrease", "remove");
                            bool direct = parts[0].EqualsAny("set");
                            BombCommander heldBombCommander = GetHeldBomb();
                            if (heldBombCommander != null) {
                                float time = 0;
                                float originalTime = heldBombCommander.TimerComponent.TimeRemaining;
                                Dictionary<string, float> timeLengths = new Dictionary<string, float>() {
                                    { "ms", 0.001f },
                                    { "s", 1 },
                                    { "m", 60 },
                                    { "h", 3600 },
                                    { "d", 86400 },
                                    { "w", 604800 },
                                    { "y", 31536000 },
                                };
                                foreach (string split in parts.Skip(1)) {
                                    bool valid = false;
                                    foreach (string unit in timeLengths.Keys) {
                                        if (!split.EndsWith(unit) || !float.TryParse(split.Substring(0, split.Length - unit.Length), out float length)) continue;
                                        time += length * timeLengths[unit];
                                        valid = true;
                                        break;
                                    }

                                    if (valid) {
                                        time = (float)Math.Round((decimal)time, 2, MidpointRounding.AwayFromZero);
                                        if (!direct && Math.Abs(time) == 0) break;
                                        if (negative) time = -time;

                                        if (direct)
                                            heldBombCommander.TimerComponent.TimeRemaining = time;
                                        else
                                            heldBombCommander.TimerComponent.TimeRemaining = heldBombCommander.CurrentTimer + time;

                                        if (originalTime < heldBombCommander.TimerComponent.TimeRemaining && !Leaderboardoff) {
                                            ChangeLeaderboard(true);
                                            Debug.Log("[Command Line] Leaderboard disabled.");
                                            LogWarning("Leaderboard disabled.");
                                        }

                                        if (direct) {
                                            Log($"Set the timer to {Math.Abs(time < 0 ? 0 : time).FormatTime()}.");
                                            Debug.Log("[Command Line] Set bomb time.");
                                        } else {
                                            Log($"{(time > 0 ? "Added" : "Subtracted")} {Math.Abs(time).FormatTime()} {(time > 0 ? "to" : "from")} the timer.");
                                            Debug.Log("[Command Line] Changed bomb time.");
                                        }
                                        break;
                                    } else {
                                        LogError("Can't change time: entered time is not valid.");
                                        break;
                                    }
                                }
                            } else {
                                LogWarning("Hold the bomb you wish to change the time of.");
                            }
                        } else {
                            LogError("Can't change time: no bombs are active.");
                        }
                    } else {
                        LogError("Improper usage. Use the \"help\" command for help.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "strikes",
                Aliases = new string[] { "strike", "s" },
                Help = "Change the current strike count on the currently held bomb.",
                Usage = "strikes <set|add|subtract> <strikes>",
                DisablesLeaderboardReason = "achieve a faster time",
                Action = command => {
                    string[] parts = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!string.IsNullOrEmpty(command) && parts[0].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set")) {
                        if (BombActive) {
                            BombCommander heldBombCommander = GetHeldBomb();
                            if (heldBombCommander != null) {
                                bool negative = parts[0].EqualsAny("subtract", "decrease", "remove");
                                bool direct = parts[0].EqualsAny("set");
                                if (int.TryParse(parts[1], out int strikes) && (strikes != 0 || direct)) {
                                    int originalStrikes = heldBombCommander.StrikeCount;
                                    if (negative) strikes = -strikes;

                                    if (direct && strikes < 0) {
                                        strikes = 0;
                                    } else if (!direct && (heldBombCommander.StrikeCount + strikes) < 0) {
                                        strikes = -heldBombCommander.StrikeCount;
                                    }

                                    if (direct)
                                        heldBombCommander.StrikeCount = strikes;
                                    else
                                        heldBombCommander.StrikeCount += strikes;

                                    if (heldBombCommander.StrikeCount < originalStrikes && !Leaderboardoff) {
                                        ChangeLeaderboard(true);
                                        Debug.Log("[Command Line] Leaderboard disabled.");
                                        LogWarning("Leaderboard disabled.");
                                    }

                                    if (direct) {
                                        Log($"Set the current strike count to {Math.Abs(strikes)} {(Math.Abs(strikes) != 1 ? "strikes" : "strike")}.");
                                        Debug.Log("[Command Line] Set bomb strike count.");
                                    } else {
                                        Log($"{(strikes > 0 ? "Added" : "Subtracted")} {Math.Abs(strikes)} {(Math.Abs(strikes) != 1 ? "strikes" : "strike")} {(strikes > 0 ? "to" : "from")} the bomb.");
                                        Debug.Log("[Command Line] Changed bomb strike count.");
                                    }
                                }
                            } else {
                                LogWarning("Hold the bomb you wish to change the current strike count on.");
                            }
                        } else {
                            LogError("Can't change current strike count: no bombs are active.");
                        }
                    } else {
                        LogError("Improper usage. Use the \"help\" command for help.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "strikelimit",
                Aliases = new string[] { "maxstrikes", "sl", "ms" },
                Help = "Change the strike limit of the currently held bomb.",
                Usage = "strikelimit <set|add|subtract> <strikes>",
                DisablesLeaderboardReason = "increase the strike limit",
                Action = command => {
                    string[] parts = command.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (!string.IsNullOrEmpty(command) && parts[0].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set")) {
                        if (BombActive) {
                            BombCommander heldBombCommander = GetHeldBomb();
                            if (heldBombCommander != null) {
                                bool negative = parts[0].EqualsAny("subtract", "decrease", "remove");
                                bool direct = parts[0].EqualsAny("set");
                                if (int.TryParse(parts[1], out int maxStrikes) && (maxStrikes != 0 || direct)) {
                                    int originalStrikeLimit = heldBombCommander.StrikeLimit;
                                    if (negative) maxStrikes = -maxStrikes;

                                    if (direct && maxStrikes < 0)
                                        maxStrikes = 0;
                                    else if (!direct && (heldBombCommander.StrikeLimit + maxStrikes) < 0)
                                        maxStrikes = -heldBombCommander.StrikeLimit;

                                    if (direct)
                                        heldBombCommander.StrikeLimit = maxStrikes;
                                    else
                                        heldBombCommander.StrikeLimit += maxStrikes;

                                    if (originalStrikeLimit < heldBombCommander.StrikeLimit && !Leaderboardoff) {
                                        ChangeLeaderboard(true);
                                        Debug.Log("[Command Line] Leaderboard disabled.");
                                        LogWarning("Leaderboard disabled.");
                                    }

                                    if (direct) {
                                        Log($"Set the strike limit to {Math.Abs(maxStrikes)} {(Math.Abs(maxStrikes) != 1 ? "strikes" : "strike")}.");
                                        Debug.Log("[Command Line] Set bomb strike limit.");
                                    } else {
                                        Log($"{(maxStrikes > 0 ? "Added" : "Subtracted")} {Math.Abs(maxStrikes)} {(Math.Abs(maxStrikes) > 1 ? "strikes" : "strike")} {(maxStrikes > 0 ? "to" : "from")} the strike limit.");
                                        Debug.Log("[Command Line] Changed bomb strike limit.");
                                    }
                                }
                            } else {
                                LogWarning("Hold the bomb you wish to change the strike limit on.");
                            }
                        } else {
                            LogError("Can't change strike limit: no bombs are active.");
                        }
                    } else {
                        LogError("Improper usage. Use the \"help\" command for help.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "solve",
                Help = "Solve the currently selected module.",
                Usage = "solve",
                DisablesLeaderboardReason = "solve a module",
                Action = _ => {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        if (heldBombCommander != null) {
                            Module module = GetFocusedModule();
                            if (module != null) {
                                if (!module.IsSolved) {
                                    switch (module.ComponentType) {
                                        case ComponentTypeEnum.NeedyCapacitor:
                                        case ComponentTypeEnum.NeedyKnob:
                                        case ComponentTypeEnum.NeedyMod:
                                        case ComponentTypeEnum.NeedyVentGas:
                                            LogError("Can't solve module: needy modules cannot be solved.");
                                            break;

                                        case ComponentTypeEnum.Empty:
                                            LogError("Can't solve module: empty slots cannot be solved.");
                                            break;
                                        case ComponentTypeEnum.Timer:
                                            LogError("Can't solve module: the timer cannot be solved.");
                                            break;

                                        default:
                                            SolveModule(module);
                                            Log($"Solved module \"{module.ModuleName}\".");
                                            Debug.Log($"[Command Line] Solved module: {module.ModuleName}");
                                            break;
                                    }
                                } else {
                                    LogError("Can't solve module: module already solved.");
                                }
                            } else {
                                LogWarning("Focus on the module that you wish to solve.");
                            }
                        } else {
                            LogWarning("Hold the bomb that contains the module you wish to solve.");
                        }
                    } else {
                        LogError("Can't solve module: no bombs are active.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "solvebomb",
                Help = "Solve the currently held bomb.",
                Usage = "solvebomb",
                DisablesLeaderboardReason = "solve a bomb",
                Action = _ => {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        if (heldBombCommander != null) {
                            if (!Leaderboardoff) {
                                ChangeLeaderboard(true);
                                Debug.Log("[Command Line] Leaderboard disabled.");
                                LogWarning("Leaderboard disabled.");
                            }
                            foreach (Module module in Modules.Where(x => x.BombId == heldBombCommander.Id && x.IsSolvable && x.ComponentType != ComponentTypeEnum.Empty && x.ComponentType != ComponentTypeEnum.Timer)) {
                                if (!module.IsSolved) SolveModule(module);
                            }
                        } else {
                            LogWarning("Hold the bomb that you wish to solve.");
                        }
                    } else {
                        LogError("Can't solve bomb: no bombs are active.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "pause",
                Help = "Pause the timer of the currently held bomb.",
                Usage = "pause",
                DisablesLeaderboardReason = "pause the timer",
                Action = _ => {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        if (heldBombCommander != null) {
                            if (heldBombCommander.TimerComponent.IsUpdating) {
                                if (!Leaderboardoff) {
                                    ChangeLeaderboard(true);
                                    Debug.Log("[Command Line] Leaderboard disabled.");
                                    LogWarning("Leaderboard disabled.");
                                }
                                heldBombCommander.TimerComponent.StopTimer();
                                Debug.Log("[Command Line] Paused the bomb timer.");
                                Log("Paused the bomb timer.");
                            } else {
                                LogError("Can't pause bomb: held bomb is already paused.");
                            }
                        } else {
                            LogWarning("Hold the bomb that you wish to pause.");
                        }
                    } else {
                        LogError("Can't pause bomb: no bombs are active.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "unpause",
                Help = "Unpause the timer of the currently held bomb.",
                Usage = "unpause",
                Action = _ => {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        if (heldBombCommander != null) {
                            if (!heldBombCommander.TimerComponent.IsUpdating) {
                                heldBombCommander.TimerComponent.StartTimer();
                                Debug.Log("[Command Line] Unpaused the bomb timer.");
                                Log("Unpaused the bomb timer.");
                            } else {
                                LogError("Can't unpause bomb: held bomb is not paused.");
                            }
                        } else {
                            LogWarning("Hold the bomb that you wish to unpause.");
                        }
                    } else {
                        LogError("Can't unpause bomb: no bombs are active.");
                    }
                }
            });
            Commands.Add(new Command {
                Name = "turn",
                Aliases = new string[] { "flip", "rotate" },
                Help = "Turn the bomb over to the opposite face.",
                Usage = "turn",
                Action = _ => {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        if (heldBombCommander != null) {
                            StartCoroutine(heldBombCommander.TurnBombCoroutine());
                        } else {
                            LogWarning("Hold the bomb you wish to turn.");
                        }
                    } else {
                        LogError("Can't turn bomb: no bombs active.");
                    }
                }
            });
            DebugModeCommands.Add(new Command {
                Name = "checkactive",
                Help = "Display debug information about the currently held bomb.",
                Usage = "checkactive",
                Action = _ => {
                    string result = BombActive ? $"Bomb(s) are active. Count: {Bombs.Count}." : "No bomb detected.";
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        result += $"\n     Currently held bomb: {(heldBombCommander != null ? $"#{heldBombCommander.Id}" : "N/A")}.";
                        Module focusedModule = GetFocusedModule();
                        result += $"\n     Currently focused module: {(focusedModule != null ? $"{focusedModule.ModuleName}" : "N/A")}.";
                    }
                    Log(result);
                }
            });
        }

        public void ProcessCommand(string command) {
            string commandTrimmed = command.Trim().ToLowerInvariant();

            if (commandTrimmed.StartsWith("!") && _isDebug) {
                Log("Twitch Plays command sent: " + commandTrimmed);
                HandleTwitchPlays(commandTrimmed);
                return;
            }

            string[] parts = commandTrimmed.Split(new[] { ' ' }, 2);

            Command comm = null;
            try {
                comm = Commands.First(c => c.Name.ToLowerInvariant() == parts[0].ToLowerInvariant() || (c.Aliases != null && c.Aliases.Contains(parts[0].ToLowerInvariant())));
            } catch (InvalidOperationException) { }
            if (comm != null) {
                if (commandTrimmed != "clear") Log("Command sent: " + command);
                comm.Action(parts.Length > 1 ? parts[1].Trim() : "");
                return;
            }
            try {
                if (_isDebug) comm = DebugModeCommands.First(c => c.Name.ToLowerInvariant() == parts[0].ToLowerInvariant() || (c.Aliases != null && c.Aliases.Contains(parts[0].ToLowerInvariant())));
            } catch (InvalidOperationException) { }
            if (comm != null) {
                Log("Experimental command sent: " + command);
                comm.Action(parts.Length > 1 ? parts[1].Trim() : "");
                return;
            }
            Log("Command sent: " + command);
            LogError($"Command \"{parts[0]}\" is not valid. Use the \"help\" command for a list of commands.");
        }

        public void HandleTwitchPlays(string message) {
            if (TwitchPlaysAvailable) {
                var comp_gen = TwitchPlays.transform.parent.GetComponent("IRCConnection");
                var comp_type = comp_gen.GetType();
                var instance_obj = comp_type.GetProperty("Instance").GetValue(null, null);
                var messageRec_field = comp_type.GetField("OnMessageReceived");
                var messageRec_obj = messageRec_field.GetValue(instance_obj);
                var messageRec_type = messageRec_field.FieldType;
                var invoke_meth = messageRec_type.GetMethod("Invoke");
                invoke_meth.Invoke(messageRec_obj, new object[] { TwitchPlaysHandle, null, message });
            } else {
                LogError("Twitch Plays is not available");
            }
        }
    }
}