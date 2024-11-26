using System.Diagnostics;
using System.Runtime.InteropServices;

public static class MemoUtilities
{
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);

    // 使用静态的 SemaphoreSlim，避免重复创建
    private static readonly SemaphoreSlim FlushLock = new(1);

    public static void FlushMemory()
    {
        // 使用 async/await 模式
        FlushLock.Wait();
        try
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
            }
        }
        finally
        {
            FlushLock.Release();
        }
    }

    // 优化定时GC
    public static IDisposable StartPeriodicGC(bool virtualMemo = false, int sleepSpan = 30)
    {
        var cts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(sleepSpan), cts.Token);
                    if (virtualMemo)
                    {
                        FlushMemory();
                    }
                    else
                    {
                        GC.Collect(2, GCCollectionMode.Optimized);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要处理
            }
        }, cts.Token);

        return new DisposableAction(() => cts.Cancel());
    }

    private class DisposableAction : IDisposable
    {
        private readonly Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action?.Invoke();
        }
    }
}