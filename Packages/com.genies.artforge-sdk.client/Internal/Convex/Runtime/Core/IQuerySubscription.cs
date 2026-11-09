using System;
using System.Collections;
using Genies.Convex.Values;

namespace Genies.Convex.Core
{
    /// <summary>
    /// Represents a subscription to a Convex query with real-time updates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A query subscription maintains a live connection to a Convex query function,
    /// receiving updates whenever the query result changes. The subscription remains
    /// active until explicitly disposed or the client disconnects.
    /// </para>
    /// <para>
    /// Thread Safety: All methods are thread-safe. Callbacks are automatically marshalled
    /// to Unity's main thread via UnityMainThreadDispatcher.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create subscription
    /// var subscription = client.Subscribe("messages:list", new { limit = 10 });
    ///
    /// // Receive updates via callback
    /// subscription.OnNext(
    ///     result => Debug.Log($"Updated: {result}"),
    ///     error => Debug.LogError($"Error: {error.Message}")
    /// );
    ///
    /// // Or use as Unity coroutine
    /// yield return subscription.Subscribe(
    ///     result => UpdateUI(result),
    ///     error => ShowError(error)
    /// );
    ///
    /// // Clean up when done
    /// subscription.Dispose();
    /// </code>
    /// </example>
    internal interface IQuerySubscription : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this subscription.
        /// </summary>
        /// <remarks>
        /// The ID is unique within the client instance and is used internally
        /// to correlate subscription updates from the server.
        /// </remarks>
        int Id { get; }

        /// <summary>
        /// Registers callbacks to receive subscription updates and errors.
        /// </summary>
        /// <param name="onResult">
        /// Callback invoked on the Unity main thread when a new result is available.
        /// Receives the query result as a ConvexValue.
        /// </param>
        /// <param name="onError">
        /// Callback invoked on the Unity main thread when an error occurs.
        /// Receives the exception that caused the error.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method can be called multiple times to register different callbacks,
        /// but only the most recent registration will receive updates.
        /// </para>
        /// <para>
        /// All callbacks are automatically marshalled to Unity's main thread,
        /// making it safe to update UI or GameObject state from within the callbacks.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when onResult or onError is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the subscription has been disposed.
        /// </exception>
        /// <example>
        /// <code>
        /// subscription.OnNext(
        ///     result =>
        ///     {
        ///         var messages = result.As&lt;ConvexValue.Array&gt;();
        ///         messageList.UpdateMessages(messages);
        ///     },
        ///     error =>
        ///     {
        ///         if (error is ConvexError convexError)
        ///             Debug.LogWarning($"Query error: {convexError.Message}");
        ///         else
        ///             Debug.LogError($"Unexpected error: {error}");
        ///     }
        /// );
        /// </code>
        /// </example>
        void OnNext(Action<ConvexValue> onResult, Action<Exception> onError);

        /// <summary>
        /// Returns a Unity coroutine that yields on each subscription update.
        /// </summary>
        /// <param name="onUpdate">
        /// Callback invoked on each update with the new query result.
        /// Called on Unity's main thread.
        /// </param>
        /// <param name="onError">
        /// Callback invoked when an error occurs.
        /// Called on Unity's main thread.
        /// </param>
        /// <returns>
        /// An IEnumerator suitable for use with Unity's StartCoroutine.
        /// The coroutine runs until the subscription is disposed or an unhandled error occurs.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is the recommended approach for Unity developers who prefer
        /// coroutines over callbacks or async/await. The coroutine will automatically
        /// yield null on each frame, processing any queued updates.
        /// </para>
        /// <para>
        /// The coroutine continues running until:
        /// - The subscription is explicitly disposed
        /// - The client disconnects
        /// - An unhandled error occurs (and onError is called)
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// IEnumerator SubscribeToMessages()
        /// {
        ///     var subscription = client.Subscribe("messages:list");
        ///
        ///     yield return subscription.Subscribe(
        ///         result =>
        ///         {
        ///             var messages = result.As&lt;ConvexValue.Array&gt;();
        ///             UpdateChatUI(messages);
        ///         },
        ///         error =>
        ///         {
        ///             Debug.LogError($"Subscription error: {error.Message}");
        ///         }
        ///     );
        ///
        ///     // Cleanup happens automatically when coroutine exits
        ///     subscription.Dispose();
        /// }
        /// </code>
        /// </example>
        IEnumerator Subscribe(Action<ConvexValue> onUpdate, Action<Exception> onError);

        /// <summary>
        /// Unsubscribes from the query and releases all resources.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Calling this method is equivalent to calling Dispose(). After unsubscription,
        /// no more updates will be received and the subscription cannot be reused.
        /// </para>
        /// <para>
        /// It is safe to call this method multiple times. Subsequent calls have no effect.
        /// </para>
        /// </remarks>
        void Unsubscribe();
    }
}
