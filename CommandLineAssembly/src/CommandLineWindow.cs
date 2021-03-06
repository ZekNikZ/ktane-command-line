﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CommandLineAssembly {
    class CommandLineWindow : MonoBehaviour {

        #region Inspector Settings
        /// <summary>
        /// The hotkey to show and hide the command line window.
        /// </summary>
        public KeyCode toggleKey = KeyCode.BackQuote;

        /// <summary>
        /// Whether to open as soon as the game starts.
        /// </summary>
        public bool openOnStart = false;

        /// <summary>
        /// Whether to open the window by shaking the device (mobile-only).
        /// </summary>
        public bool shakeToOpen = true;

        /// <summary>
        /// The (squared) acceleration above which the window should open.
        /// </summary>
        public float shakeAcceleration = 3f;

        /// <summary>
        /// Whether to only keep a certain number of logs, useful if memory usage is a concern.
        /// </summary>
        public bool restrictLogCount = false;

        /// <summary>
        /// Number of logs to keep before removing old ones.
        /// </summary>
        public int maxLogCount = 1000;

        /// <summary>
        /// Style to be used for console messages.
        /// </summary>
        public GUIStyle logMessageStyle;
        #endregion

        private CommandLineService serviceProvider;
        static readonly GUIContent clearLabel = new GUIContent("Clear", "Clear the contents of the command line.");
        static readonly GUIContent collapseLabel = new GUIContent("Collapse", "Hide repeated messages.");
        static readonly GUIContent sendLabel = new GUIContent("Send", "Sends a command to the command line.");
        static GUIStyle boxStyle;
        const int margin = 20;
        const string windowTitle = "Command Line";

        static readonly Dictionary<LogType, Color> logTypeColors = new Dictionary<LogType, Color>
        {
            { LogType.Log, Color.white },
            { LogType.Error, Color.red },
            { LogType.Warning, Color.yellow },
            { LogType.Exception, Color.red },
            { LogType.Assert, Color.white }
        };

        //bool isCollapsed;
        bool gainFocus;
        bool justClosed;
        public bool isVisible;
        bool doScroll;
        readonly List<Log> logs = new List<Log>();
        readonly ConcurrentQueue<Log> queuedLogs = new ConcurrentQueue<Log>();

        Vector2 scrollPosition;
        readonly Rect titleBarRect = new Rect(0, 0, 10000, 20);
        Rect windowRect = new Rect(margin, margin, Screen.width - (margin * 2), Screen.height - (margin * 2));

        public string currentEntry = "";
        public string savedEntry = "";
        private List<string> previousEntries = new List<string>();
        private int previousEntryIndex = -1;

        readonly Dictionary<LogType, bool> logTypeFilters = new Dictionary<LogType, bool>
        {
            { LogType.Log, true },
            { LogType.Error, true },
            { LogType.Warning, true },
            { LogType.Exception, false },
            { LogType.Assert, false }
        };

        #region MonoBehaviour Messages
        /* TODO: Enable if needed
        void OnDisable() {
            Application.logMessageReceivedThreaded -= HandleLogThreaded;
        }

        void OnEnable() {
            Application.logMessageReceivedThreaded += HandleLogThreaded;
        }
        */

        void OnGUI() {
            if (boxStyle == null) {
                boxStyle = new GUIStyle(GUI.skin.box) {
                    font = logMessageStyle.font,
                    fontSize = logMessageStyle.fontSize,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == toggleKey) {
                if (justClosed) {
                    justClosed = false;
                } else {
                    isVisible = !isVisible;
                }
                Event.current.Use();
                if (isVisible) {
                    ScrollToBottom();
                    gainFocus = true;
                }
            }/*else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) {
            SendCommand();
        }*/

            if (!isVisible) {
                return;
            }

            //windowRect = 
            GUILayout.Window(123456, windowRect, DrawWindow, windowTitle);
        }

        void Start() {
            logMessageStyle = new GUIStyle {
                font = GetComponentInChildren<Text>().font,
                fontSize = 14,
                normal = new GUIStyleState {
                    textColor = Color.white
                }
            };
            serviceProvider = GetComponent<CommandLineService>();
            if (openOnStart) {
                isVisible = true;
            }
        }

        void Update() {
            UpdateQueuedLogs();

            if (shakeToOpen && Input.acceleration.sqrMagnitude > shakeAcceleration) {
                isVisible = true;
            }
        }
        #endregion

        void DrawCollapsedLog(Log log) {
            GUILayout.BeginHorizontal();

            GUILayout.Label(log.GetTruncatedMessage(), logMessageStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(log.count.ToString(), boxStyle);

            GUILayout.EndHorizontal();
        }

        void DrawExpandedLog(Log log) {
            for (var i = 0; i < log.count; i += 1) {
                GUILayout.Label(log.GetTruncatedMessage(), logMessageStyle);
            }
        }

        void DrawLog(Log log) {
            GUI.contentColor = logTypeColors[log.type];

            //if (isCollapsed) { TODO: Deal with this
            if (false) {
                DrawCollapsedLog(log);
            } else {
                DrawExpandedLog(log);
            }
        }

        void DrawLogList() {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            // Used to determine height of accumulated log labels.
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            var visibleLogs = logs.Where(IsLogVisible);

            foreach (Log log in visibleLogs) {
                DrawLog(log);
            }

            GUILayout.EndVertical();
            var innerScrollRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndScrollView();
            var outerScrollRect = GUILayoutUtility.GetLastRect();

            // If we're scrolled to bottom now, guarantee that it continues to be in next cycle.
            if (Event.current.type == EventType.Repaint && IsScrolledToBottom(innerScrollRect, outerScrollRect)) {
                ScrollToBottom();
            }

            if (doScroll) {
                ScrollToBottom();
                doScroll = false;
            }

            // Ensure GUI colour is reset before drawing other components.
            GUI.contentColor = Color.white;
        }

        void DrawToolbar() {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(clearLabel)) {
                logs.Clear();
            }

            foreach (LogType logType in Enum.GetValues(typeof(LogType))) {
                var currentState = logTypeFilters[logType];
                var label = logType.ToString();
                logTypeFilters[logType] = GUILayout.Toggle(currentState, label, GUILayout.ExpandWidth(false));
                GUILayout.Space(20);
            }

            //isCollapsed = GUILayout.Toggle(isCollapsed, collapseLabel, GUILayout.ExpandWidth(false)); // TODO: deal with this

            GUILayout.EndHorizontal();
        }

        void DrawEntryField() {
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("InputTextField");
            currentEntry = GUILayout.TextField(currentEntry, boxStyle);
            if (GUILayout.Button(sendLabel, GUILayout.MaxWidth(50))) {
                SendCommand(); // TODO: Finish
            }
            GUILayout.EndHorizontal();
        }

        void DrawWindow(int windowID) {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == toggleKey) {
                justClosed = true;
                isVisible = !isVisible;
                Event.current.Use();
                if (isVisible) {
                    ScrollToBottom();
                    gainFocus = true;
                }
            } else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return) {
                SendCommand();
                gainFocus = true;
            } else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.UpArrow) {
                if (previousEntryIndex == -1) savedEntry = currentEntry;
                if (previousEntryIndex < previousEntries.Count - 1) previousEntryIndex++;
                currentEntry = previousEntries[previousEntryIndex];
            } else if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.DownArrow) {
                if (previousEntryIndex > -1) previousEntryIndex--;
                if (previousEntryIndex == -1) {
                    currentEntry = savedEntry;
                } else {
                    currentEntry = previousEntries[previousEntryIndex];
                }
            }

            DrawToolbar();
            DrawLogList();
            DrawEntryField();

            if (gainFocus) {
                GUI.FocusControl("InputTextField");
                gainFocus = false;
            }

            // Allow the window to be dragged by its title bar.
            GUI.DragWindow(titleBarRect);
        }

        Log? GetLastLog() {
            if (logs.Count == 0) {
                return null;
            }

            return logs.Last();
        }

        void UpdateQueuedLogs() {
            Log log;
            while (queuedLogs.TryDequeue(out log)) {
                ProcessLogItem(log);
                ScrollToBottom();
            }
        }

        void HandleLogThreaded(string message, string stackTrace, LogType type) {
            var log = new Log {
                count = 1,
                message = message,
                stackTrace = stackTrace,
                timeStamp = DateTime.Now,
                type = type,
            };

            // Queue the log into a ConcurrentQueue to be processed later in the Unity main thread,
            // so that we don't get GUI-related errors for logs coming from other threads
            queuedLogs.Enqueue(log);
        }

        void ProcessLogItem(Log log) {
            var lastLog = GetLastLog();
            var isDuplicateOfLastLog = lastLog.HasValue && log.Equals(lastLog.Value);

            if (isDuplicateOfLastLog) {
                // Replace previous log with incremented count instead of adding a new one.
                log.count = lastLog.Value.count + 1;
                logs[logs.Count - 1] = log;
            } else {
                logs.Add(log);
                TrimExcessLogs();
            }
        }

        bool IsLogVisible(Log log) {
            return logTypeFilters[log.type];
        }

        bool IsScrolledToBottom(Rect innerScrollRect, Rect outerScrollRect) {
            var innerScrollHeight = innerScrollRect.height;

            // Take into account extra padding added to the scroll container.
            var outerScrollHeight = outerScrollRect.height - GUI.skin.box.padding.vertical;

            // If contents of scroll view haven't exceeded outer container, treat it as scrolled to bottom.
            if (outerScrollHeight > innerScrollHeight) {
                return true;
            }

            // Scrolled to bottom (with error margin for float math)
            return Mathf.Approximately(innerScrollHeight, scrollPosition.y + outerScrollHeight);
        }

        void ScrollToBottom() {
            scrollPosition = new Vector2(0, Int32.MaxValue);
        }

        void TrimExcessLogs() {
            if (!restrictLogCount) {
                return;
            }

            var amountToRemove = logs.Count - maxLogCount;

            if (amountToRemove <= 0) {
                return;
            }

            logs.RemoveRange(0, amountToRemove);
        }

        void SendCommand() {
            if (currentEntry.Trim() == "") return;
            doScroll = true;
            serviceProvider.ProcessCommand(currentEntry);
            previousEntries.Insert(0, currentEntry);
            previousEntryIndex = -1;
            currentEntry = "";
        }

        public void Log(string message, LogType type = LogType.Log, string stackTrace = null, int count = 1) {
            var log = new Log {
                count = count,
                message = message,
                stackTrace = stackTrace,
                timeStamp = DateTime.Now,
                type = type,
            };

            queuedLogs.Enqueue(log);
        }

        public void Clear() {
            logs.Clear();
        }
    }


    /// <summary>
    /// A basic container for log details.
    /// </summary>
    struct Log {
        public int count;
        public string message;
        public string stackTrace;
        public DateTime timeStamp;
        public LogType type;

        /// <summary>
        /// The max string length supported by UnityEngine.GUILayout.Label without triggering this error:
        /// "String too long for TextMeshGenerator. Cutting off characters."
        /// </summary>
        private const int MaxMessageLength = 16382;

        public bool Equals(Log log) {
            return message == log.message && stackTrace == log.stackTrace && timeStamp == log.timeStamp && type == log.type;
        }

        /// <summary>
        /// Return a truncated message if it exceeds the max message length
        /// </summary>
        public string GetTruncatedMessage() {
            if (string.IsNullOrEmpty(message)) return message;
            string str = $"[{timeStamp.ToString("HH:mm:ss")}] {message}";
            return str.Length <= MaxMessageLength ? str : str.Substring(0, MaxMessageLength);
        }
    }

    /// <summary>
    /// Alternative to System.Collections.Concurrent.ConcurrentQueue
    /// (It's only available in .NET 4.0 and greater)
    /// </summary>
    /// <remarks>
    /// It's a bit slow (as it uses locks), and only provides a small subset of the interface
    /// Overall, the implementation is intended to be simple & robust
    /// </remarks>
    public class ConcurrentQueue<T> {
        private readonly System.Object queueLock = new System.Object();
        private readonly Queue<T> queue = new Queue<T>();

        public void Enqueue(T item) {
            lock (queueLock) {
                queue.Enqueue(item);
            }
        }

        public bool TryDequeue(out T result) {
            lock (queueLock) {
                if (queue.Count == 0) {
                    result = default(T);
                    return false;
                }

                result = queue.Dequeue();
                return true;
            }
        }
    }
}