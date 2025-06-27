using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class KeyedTaskQueue
    {
        private class TaskQueue
        {
            private readonly Channel<Func<Task>> _channel;
            private readonly int _delayBetweenTasksMs;
            private int _pendingTasks = 0;

            public ChannelWriter<Func<Task>> Writer => _channel.Writer;
            public Task ProcessingTask { get; }

            public TaskQueue(int delayBetweenTasksMs)
            {
                _channel = Channel.CreateUnbounded<Func<Task>>();
                _delayBetweenTasksMs = delayBetweenTasksMs;
                ProcessingTask = Task.Run(ProcessQueueAsync);
            }

            private async Task ProcessQueueAsync()
            {
                await foreach (var task in _channel.Reader.ReadAllAsync())
                {
                    Interlocked.Increment(ref _pendingTasks);
                    try
                    {
                        await task();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error in queued task: {ex}");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingTasks);
                    }

                    await Task.Delay(_delayBetweenTasksMs);
                }
            }

            public async Task<bool> IsDrainedAsync(CancellationToken cancellationToken = default)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(300));

                try
                {
                    while (true)
                    {
                        if (_pendingTasks == 0 && !_channel.Reader.TryPeek(out _))
                        {
                            return true;
                        }

                        await Task.Delay(25, timeoutCts.Token);
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    return false;
                }
            }

            public async Task IsCompletedAsync()
            {
                await ProcessingTask;
            }

            public void Complete()
            {
                _channel.Writer.Complete();
            }
        }

        private readonly ConcurrentDictionary<string, TaskQueue> _queues = new();
        private readonly int _delayBetweenTasksMs;

        public KeyedTaskQueue(IOptions<SemanticPluginOptions> options)
        {
            _delayBetweenTasksMs = options.Value.DelayBetweenQueuedTasksMs;
        }

        public async Task EnqueueAsync(string key, Func<Task> task)
        {
            var queue = _queues.GetOrAdd(key, _ => new TaskQueue(_delayBetweenTasksMs));
            await queue.Writer.WriteAsync(task);
        }

        public async Task WaitUntilQueueIsDrainedAsync(string key, CancellationToken cancellationToken = default)
        {
            if (_queues.TryGetValue(key, out var queue))
            {
                await queue.IsDrainedAsync(cancellationToken);
            }
        }

        public void MarkAllQueuesAsComplete()
        {
            foreach (var queue in _queues.Values)
                queue.Complete();
        }

        public async Task WaitForAllQueuesCompletionAsync()
        {
            var tasks = _queues.Values.Select(q => q.ProcessingTask);
            await Task.WhenAll(tasks);
        }

        public void CleanupDrainedQueues()
        {
            foreach (var kvp in _queues)
            {
                var key = kvp.Key;
                var queue = kvp.Value;

                if (queue.IsDrainedAsync().GetAwaiter().GetResult())
                {
                    _queues.TryRemove(key, out _);
                }
            }
        }
    }
}
