using System.Collections.Concurrent;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

/// <summary>
///     Evaluates watch expressions on a background thread to prevent blocking the game loop.
///     Results are collected on the main thread during the next update cycle.
/// </summary>
public class WatchEvaluator : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private readonly ConcurrentQueue<EvaluationRequest> _requestQueue = new();
    private readonly ConcurrentQueue<EvaluationResult> _resultQueue = new();
    private readonly AutoResetEvent _workAvailable = new(false);
    private readonly Thread _workerThread;

    public WatchEvaluator()
    {
        _workerThread = new Thread(WorkerLoop)
        {
            Name = "WatchEvaluator",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
        };
        _workerThread.Start();
    }

    /// <summary>
    ///     Maximum time allowed for a single evaluation before it's considered timed out.
    /// </summary>
    public TimeSpan EvaluationTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Number of pending evaluations in the queue.
    /// </summary>
    public int PendingCount => _requestQueue.Count;

    /// <summary>
    ///     Number of results ready to be collected.
    /// </summary>
    public int ResultCount => _resultQueue.Count;

    public void Dispose()
    {
        _cts.Cancel();
        _workAvailable.Set(); // Wake up worker thread
        _workerThread.Join(1000); // Wait up to 1 second for graceful shutdown
        _cts.Dispose();
        _workAvailable.Dispose();
    }

    /// <summary>
    ///     Queues a watch for evaluation on the background thread.
    /// </summary>
    public void QueueEvaluation(
        string watchName,
        Func<object?> valueGetter,
        Func<bool>? conditionEvaluator = null
    )
    {
        _requestQueue.Enqueue(
            new EvaluationRequest
            {
                WatchName = watchName,
                ValueGetter = valueGetter,
                ConditionEvaluator = conditionEvaluator,
            }
        );
        _workAvailable.Set();
    }

    /// <summary>
    ///     Tries to get the next available result. Returns false if no results are ready.
    ///     Call this from the main thread during update.
    /// </summary>
    public bool TryGetResult(out EvaluationResult? result)
    {
        return _resultQueue.TryDequeue(out result);
    }

    /// <summary>
    ///     Collects all available results. Call this from the main thread during update.
    /// </summary>
    public IEnumerable<EvaluationResult> CollectResults()
    {
        while (_resultQueue.TryDequeue(out EvaluationResult? result))
        {
            yield return result;
        }
    }

    /// <summary>
    ///     Clears all pending evaluations (e.g., when watches are cleared).
    /// </summary>
    public void ClearPending()
    {
        while (_requestQueue.TryDequeue(out _)) { }
    }

    private void WorkerLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Wait for work or cancellation
                _workAvailable.WaitOne(100); // Check periodically for cancellation

                // Process all queued requests
                while (_requestQueue.TryDequeue(out EvaluationRequest? request))
                {
                    if (_cts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    EvaluationResult result = EvaluateRequest(request);
                    _resultQueue.Enqueue(result);
                }
            }
            catch (Exception)
            {
                // Don't let exceptions crash the worker thread
            }
        }
    }

    private EvaluationResult EvaluateRequest(EvaluationRequest request)
    {
        DateTime startTime = DateTime.Now;

        try
        {
            // Evaluate condition first if present
            bool conditionMet = true;
            if (request.ConditionEvaluator != null)
            {
                try
                {
                    conditionMet = request.ConditionEvaluator();
                }
                catch
                {
                    conditionMet = false;
                }
            }

            // Only evaluate value if condition is met
            object? value = null;
            if (conditionMet)
            {
                // Use a task with timeout for the actual evaluation
                using var evalCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                evalCts.CancelAfter(EvaluationTimeout);

                var evalTask = Task.Run(() => request.ValueGetter(), evalCts.Token);

                if (evalTask.Wait(EvaluationTimeout))
                {
                    value = evalTask.Result;
                }
                else
                {
                    return new EvaluationResult
                    {
                        WatchName = request.WatchName,
                        HasError = true,
                        ErrorMessage =
                            $"Evaluation timed out after {EvaluationTimeout.TotalSeconds:F1}s",
                        EvaluatedAt = DateTime.Now,
                        EvaluationTime = DateTime.Now - startTime,
                    };
                }
            }

            return new EvaluationResult
            {
                WatchName = request.WatchName,
                Value = value,
                ConditionMet = conditionMet,
                HasError = false,
                EvaluatedAt = DateTime.Now,
                EvaluationTime = DateTime.Now - startTime,
            };
        }
        catch (Exception ex)
        {
            return new EvaluationResult
            {
                WatchName = request.WatchName,
                HasError = true,
                ErrorMessage = ex.InnerException?.Message ?? ex.Message,
                EvaluatedAt = DateTime.Now,
                EvaluationTime = DateTime.Now - startTime,
            };
        }
    }

    /// <summary>
    ///     Request to evaluate a watch expression.
    /// </summary>
    public class EvaluationRequest
    {
        public required string WatchName { get; init; }
        public required Func<object?> ValueGetter { get; init; }
        public required Func<bool>? ConditionEvaluator { get; init; }
        public DateTime QueuedAt { get; init; } = DateTime.Now;
    }

    /// <summary>
    ///     Result of a watch evaluation.
    /// </summary>
    public class EvaluationResult
    {
        public required string WatchName { get; init; }
        public object? Value { get; init; }
        public bool ConditionMet { get; init; } = true;
        public bool HasError { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime EvaluatedAt { get; init; } = DateTime.Now;
        public TimeSpan EvaluationTime { get; init; }
    }
}
