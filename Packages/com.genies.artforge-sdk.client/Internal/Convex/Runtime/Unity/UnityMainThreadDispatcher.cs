using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Genies.Convex.Logging;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Genies.Convex.Unity
{
    /// <summary>
    /// Provides a mechanism to execute actions on Unity's main thread from background threads.
    /// This is essential for Unity operations that must run on the main thread (UI updates, GameObject manipulation, etc.).
    /// Works in both Edit mode (via EditorApplication.update) and Play mode (via MonoBehaviour.Update).
    /// </summary>
    /// <remarks>
    /// Unity requires certain operations to run on the main thread. This dispatcher provides a thread-safe
    /// queue to marshal actions from worker threads back to Unity's main thread.
    ///
    /// The dispatcher uses a singleton pattern and persists across scene loads via DontDestroyOnLoad.
    /// It processes all queued actions during the Update() method on each frame (Play mode) or
    /// during EditorApplication.update callbacks (Edit mode).
    ///
    /// Usage example:
    /// <code>
    /// // From a background thread
    /// UnityMainThreadDispatcher.Enqueue(() =>
    /// {
    ///     Debug.Log("This runs on Unity's main thread");
    ///     UpdateUI();
    /// });
    /// </code>
    /// </remarks>
    internal class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        private static int _mainThreadId;
        private static bool _editorUpdateRegistered = false;
        private static IConvexLogger _logger = new UnityConvexLogger();

        /// <summary>
        /// Gets the singleton instance of the UnityMainThreadDispatcher.
        /// If no instance exists, one is automatically created and persisted across scenes.
        /// </summary>
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
#if UNITY_EDITOR
                // Ensure EditorApplication.update is registered for Edit mode (even before GameObject exists)
                RegisterEditorUpdate();
#endif

                if (_instance == null)
                {
                    // Try to find existing instance
                    _instance = FindAnyObjectByType<UnityMainThreadDispatcher>();

                    if (_instance == null)
                    {
                        // Create new instance
                        var go = new GameObject("ConvexMainThreadDispatcher");
                        _instance = go.AddComponent<UnityMainThreadDispatcher>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        public static void SetLogger(IConvexLogger logger)
        {
            _logger = logger ?? new UnityConvexLogger();
        }

        /// <summary>
        /// Gets whether the current thread is Unity's main thread.
        /// </summary>
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>
        /// Gets the current number of actions queued for execution on the main thread.
        /// Useful for monitoring queue depth and detecting potential bottlenecks.
        /// </summary>
        public static int QueuedActionCount
        {
            get
            {
                lock (_executionQueue)
                {
                    return _executionQueue.Count;
                }
            }
        }

        private void Awake()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            _logger.Info($"[ConvexMainThreadDispatcher] Awake() called on thread {threadId}");

            if (_instance == null)
            {
                _instance = this;
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                DontDestroyOnLoad(gameObject);
                _logger.Info($"[ConvexMainThreadDispatcher] Instance initialized. Main thread ID set to: {_mainThreadId}");

#if UNITY_EDITOR
                // Register EditorApplication.update for Edit mode support
                RegisterEditorUpdate();
#endif
            }
            else if (_instance != this)
            {
                _logger.Info("[ConvexMainThreadDispatcher] Duplicate instance detected, destroying");
                // Duplicate instance, destroy it
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Process all queued actions on the main thread
            // We process all actions in the queue each frame to minimize latency
            ProcessQueuedActions();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
#if UNITY_EDITOR
                UnregisterEditorUpdate();
#endif
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Registers EditorApplication.update to process queue in Edit mode
        /// </summary>
        private static void RegisterEditorUpdate()
        {
            if (!_editorUpdateRegistered)
            {
                EditorApplication.update += ProcessQueuedActions;
                _editorUpdateRegistered = true;
                _logger.Info("[ConvexMainThreadDispatcher] EditorApplication.update registered - works in Edit mode now");
            }
        }

        /// <summary>
        /// Unregisters EditorApplication.update
        /// </summary>
        private static void UnregisterEditorUpdate()
        {
            if (_editorUpdateRegistered)
            {
                EditorApplication.update -= ProcessQueuedActions;
                _editorUpdateRegistered = false;
                _logger.Info("[ConvexMainThreadDispatcher] EditorApplication.update unregistered");
            }
        }
#endif

        /// <summary>
        /// Processes all queued actions. Called from both Update() (Play mode) and EditorApplication.update (Edit mode)
        /// </summary>
        private static void ProcessQueuedActions()
        {
            lock (_executionQueue)
            {
                int queueCount = _executionQueue.Count;
                if (queueCount > 0)
                {
                    _logger.Info($"[ConvexMainThreadDispatcher] Processing {queueCount} queued actions");
                }

                while (_executionQueue.Count > 0)
                {
                    var action = _executionQueue.Dequeue();

                    // Execute with exception isolation to prevent one bad action from poisoning the queue
                    try
                    {
                        _logger.Info("[ConvexMainThreadDispatcher] Invoking action...");
                        action?.Invoke();
                        _logger.Info("[ConvexMainThreadDispatcher] Action completed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[ConvexMainThreadDispatcher] Exception executing queued action: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Enqueues an action to be executed on Unity's main thread during the next Update cycle.
        /// This method is thread-safe and can be called from any thread.
        /// </summary>
        /// <param name="action">The action to execute on the main thread. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        /// <example>
        /// <code>
        /// // From a background thread
        /// UnityMainThreadDispatcher.Enqueue(() =>
        /// {
        ///     Debug.Log("This runs on the main thread");
        ///     someGameObject.transform.position = newPosition;
        /// });
        /// </code>
        /// </example>
        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

#if UNITY_EDITOR
            // Ensure EditorApplication.update is registered for Edit mode (safety net if Enqueue called before Instance)
            RegisterEditorUpdate();
#endif

            int currentThreadId = Thread.CurrentThread.ManagedThreadId;
            _logger.Info($"[ConvexMainThreadDispatcher] Enqueue called from thread {currentThreadId}, main thread: {_mainThreadId}, IsMainThread: {IsMainThread}");

            // If we're already on the main thread, execute immediately
            if (IsMainThread)
            {
                _logger.Info("[ConvexMainThreadDispatcher] Already on main thread, executing immediately");
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ConvexMainThreadDispatcher] Exception executing action on main thread: {ex}");
                }
                return;
            }

            // Otherwise, queue for main thread execution
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
                _logger.Info($"[ConvexMainThreadDispatcher] Action queued. Queue size now: {_executionQueue.Count}");
            }
        }

        /// <summary>
        /// Enqueues an asynchronous action to be executed on Unity's main thread.
        /// The async action will start executing during the next Update cycle.
        /// </summary>
        /// <param name="asyncAction">The asynchronous action to execute. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when asyncAction is null.</exception>
        /// <remarks>
        /// Note: The Task returned by asyncAction will execute on the main thread, but any
        /// awaited operations within it may resume on different threads depending on their
        /// synchronization context.
        /// </remarks>
        /// <example>
        /// <code>
        /// UnityMainThreadDispatcher.EnqueueAsync(async () =>
        /// {
        ///     await SomeAsyncOperation();
        ///     UpdateUI(); // This part runs on main thread
        /// });
        /// </code>
        /// </example>
        public static void EnqueueAsync(Func<Task> asyncAction)
        {
            if (asyncAction == null)
            {
                throw new ArgumentNullException(nameof(asyncAction));
            }

            Enqueue(async () =>
            {
                try
                {
                    await asyncAction();
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ConvexMainThreadDispatcher] Exception executing async action: {ex}");
                }
            });
        }

        /// <summary>
        /// Enqueues an action to be executed on Unity's main thread after a specified delay.
        /// Uses Unity's coroutine system for the delay mechanism.
        /// </summary>
        /// <param name="action">The action to execute. Must not be null.</param>
        /// <param name="delaySeconds">The delay in seconds before executing the action.</param>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when delaySeconds is negative.</exception>
        /// <example>
        /// <code>
        /// // Execute after 2 seconds
        /// UnityMainThreadDispatcher.EnqueueDelayed(() =>
        /// {
        ///     Debug.Log("This runs after 2 seconds");
        /// }, 2.0f);
        /// </code>
        /// </example>
        public static void EnqueueDelayed(Action action, float delaySeconds)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (delaySeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Delay cannot be negative");
            }

            Instance.StartCoroutine(DelayedActionCoroutine(action, delaySeconds));
        }

        /// <summary>
        /// Internal coroutine for delayed action execution.
        /// </summary>
        private static IEnumerator DelayedActionCoroutine(Action action, float delay)
        {
            yield return new WaitForSeconds(delay);

            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error($"[ConvexMainThreadDispatcher] Exception executing delayed action: {ex}");
            }
        }

        /// <summary>
        /// Clears all queued actions without executing them.
        /// Useful for cleanup or when resetting the application state.
        /// </summary>
        /// <remarks>
        /// This method should be used with caution as it will discard all pending actions.
        /// Use it only when you're certain that pending actions are no longer needed.
        /// </remarks>
        public static void ClearQueue()
        {
            lock (_executionQueue)
            {
                _executionQueue.Clear();
            }
        }

    }
}
