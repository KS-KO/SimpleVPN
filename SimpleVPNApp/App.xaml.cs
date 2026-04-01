using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using SimpleVPNApp.ViewModels;

namespace SimpleVPNApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = @"Global\SimpleVPNApp.SingleInstance";
    private const int SW_RESTORE = 9;
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        var createdNew = false;
        _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
        _ownsMutex = createdNew;

        if (!createdNew)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _mainViewModel = new MainViewModel();
        MainWindow = new MainWindow { DataContext = _mainViewModel };
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Rule: 프로그램 종료 시 모든 리소스 완전 해제
        try
        {
            _mainViewModel?.Dispose();
        }
        catch { }

        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }

    private static void ActivateExistingInstance()
    {
        var currentProcess = Process.GetCurrentProcess();

        foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            if (process.Id == currentProcess.Id)
            {
                continue;
            }

            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                continue;
            }

            ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
            break;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
