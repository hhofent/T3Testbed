using System;
using CsharpSpawner;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace csharp_spawnerv0._1
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var workerClock = Stopwatch.StartNew();
            if (e.Args.Any(arg => string.Equals(arg, "--elevation-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await ElevationWorker.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--water-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await WaterSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--transport-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await TransportSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--population-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await PopulationSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--road-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await RoadSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--town-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await TownSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--tree-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await TreeSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--wall-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await WallPlanSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--rural-worker", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var workerExitCode = await RuralSpawner.RunWorkerAsync();
                WriteWorkerTiming(workerClock);
                Shutdown(workerExitCode);
                return;
            }
            if (e.Args.Any(arg => string.Equals(arg, "--headless", StringComparison.OrdinalIgnoreCase)))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var exitCode = await HeadlessRunner.RunAsync();
                Shutdown(exitCode);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }

        private static void WriteWorkerTiming(Stopwatch clock) => Console.Error.WriteLine($"WORKER_PROFILE_MS={clock.Elapsed.TotalMilliseconds:F1}");
    }
}

