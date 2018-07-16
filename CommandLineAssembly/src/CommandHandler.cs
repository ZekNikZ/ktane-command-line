using Assets.Scripts.Missions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CommandLineAssembly {
    partial class CommandLineService : MonoBehaviour {
        public void ProcessCommand(string command) {
            string commandTrimmed = command.Trim().ToLowerInvariant();
            List<string> parts = commandTrimmed.Split(new[] { ' ' }).ToList();

            if (commandTrimmed.StartsWith("!")) {
                HandleTwitchPlays(commandTrimmed);
            } else if (parts[0] == "exit") {
                Console.isVisible = false;
                Console.currentEntry = "";
            } else if (parts[0] == "clear") {
                Console.Clear();
            } else if (parts[0] == "checkactive" && _isDebug) {
                Log(BombActive ? $"Bomb(s) are active. Count: {Bombs.Count}." : "No bomb detected.");
                if (BombActive) {
                    BombCommander heldBombCommander = GetHeldBomb();
                    Log($"Currently held bomb: {(heldBombCommander != null ? $"#{heldBombCommander.Id}" : "N/A")}.");
                    Module focusedModule = GetFocusedModule();
                    Log($"Currently focused module: {(focusedModule != null ? $"{focusedModule.ModuleName}" : "N/A")}.");
                }
            } else if (parts[0] == "detonate") {
                if (BombActive) {
                    BombCommander heldBombCommader = GetHeldBomb();
                    string reason = "Detonate Command";
                    if (parts.Count > 1)
                        reason = command.Substring(9);
                    if (heldBombCommader != null) {
                        Log($"Detonated bomb{(parts.Count > 1 ? $" with reason \"{command.Substring(9)}\"" : "")}.");
                        Debug.Log("[Command Line] Detonating bomb.");
                        heldBombCommader.Detonate(reason);
                    } else {
                        LogWarning("Hold the bomb you wish to detonate.");
                    }
                } else {
                    LogError("Can't detonate: no bombs are active.");
                }
            } else if (parts[0] == "causestrike") {
                if (BombActive) {
                    BombCommander heldBombCommander = GetHeldBomb();
                    string reason = "Strike Command";
                    if (parts.Count > 1)
                        reason = command.Substring(12);
                    if (heldBombCommander != null) {
                        Log($"Caused a strike{(parts.Count > 1 ? $" with reason \"{command.Substring(12)}\"" : "")}.");
                        Debug.Log("[Command Line] Causing strike.");
                        heldBombCommander.CauseStrike(reason);
                    } else {
                        LogWarning("Hold the bomb you wish to cause a strike on.");
                    }
                } else {
                    LogError("Can't cause a strike: no bombs are active.");
                }
            } else if (parts[0].EqualsAny("time", "t")) {
                if (parts.Count > 1 && parts[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set")) {
                    if (BombActive) {
                        bool negative = parts[1].EqualsAny("subtract", "decrease", "remove");
                        bool direct = parts[1].EqualsAny("set");
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
                            foreach (string split in parts.Skip(2)) {
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
                    LogError("Improper usage. Use the \"help\" command for help.S");
                }
            } else if (parts[0].EqualsAny("strikes", "strike", "s")) {
                if (parts.Count > 1 && parts[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set")) {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        if (heldBombCommander != null) {
                            bool negative = parts[1].EqualsAny("subtract", "decrease", "remove");
                            bool direct = parts[1].EqualsAny("set");
                            if (int.TryParse(parts[2], out int strikes) && (strikes != 0 || direct)) {
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
                    LogError("Improper usage. Use the \"help\" command for help.S");
                }
            } else if (parts[0].EqualsAny("ms", "maxstrikes", "sl", "strikelimit")) {
                if (parts.Count > 1 && parts[1].EqualsAny("add", "increase", "change", "subtract", "decrease", "remove", "set")) {
                    if (BombActive) {
                        BombCommander heldBombCommander = GetHeldBomb();
                        if (heldBombCommander != null) {
                            bool negative = parts[1].EqualsAny("subtract", "decrease", "remove");
                            bool direct = parts[1].EqualsAny("set");
                            if (int.TryParse(parts[2], out int maxStrikes) && (maxStrikes != 0 || direct)) {
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
                    LogError("Improper usage. Use the \"help\" command for help.S");
                }
            } else if (commandTrimmed == "solve") {
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
            } else if (commandTrimmed == "solvebomb") {
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
            } else if (commandTrimmed == "pause") {
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
            } else if (commandTrimmed == "unpause") {
                if (BombActive) {
                    BombCommander heldBombCommander = GetHeldBomb();
                    if (heldBombCommander != null) {
                        if (!heldBombCommander.TimerComponent.IsUpdating) {
                            heldBombCommander.TimerComponent.StartTimer();
                            Debug.Log("[Command Line] Unpaused the bomb timer.");
                            Log("Paused the bomb timer.");
                        } else {
                            LogError("Can't unpause bomb: held bomb is not paused.");
                        }
                    } else {
                        LogWarning("Hold the bomb that you wish to unpause.");
                    }
                } else {
                    LogError("Can't unpause bomb: no bombs are active.");
                }
            } else if (parts[0].Trim().ToLowerInvariant().EqualsAny("turn", "rotate", "flip") && _isDebug) {
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
            } else if (parts[0] == "help") {
                Log(@"Command reference:
        detonate [reason] - detonate the currently held bomb, with an optional reason
        causestrike [reason] - cause a strike on the currently held bomb, with an optional reason
        time <set|add|subtract> <time><s|m|h> - changes the time on the currently held bomb (NOTE: this will disable leaderboards if you use it to achieve a faster time)
        strikes <set|add|subtract> <number> - changes the strikes on the currently held bomb (NOTE: this will disable leaderboards if you use it to achieve a faster time)
        strikelimit <set|add|subtract> <number> - changes the strike limit on the currently held bomb (NOTE: this will disable leaderboards if you add a higher strike limit)
        solve - solves the currently focused module (NOTE: this will disable leaderboards)
        solvebomb - solves the currently held bomb (NOTE: this will disable leaderboards)
        pause - pauses the currently held bomb (NOTE: this will disable leaderboards)
        unpause - unpauses the currently held bomb (NOTE: this will disable leaderboards)" + (_isDebug ? @"
        turn - turns the bomb to the opposite face (NOTE: debug command)
        checkactive - returns debugging info about the current bomb (NOTE: debug command)" : "") + (TwitchPlaysAvailable ? @"
        Twitch Plays commands can be sent using the '!' prefix." : ""));
            } else {
                LogError($"Command \"{parts[0]}\" is not valid. Use the \"help\" command for help.");
            }
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