using System.Windows;
using System.Windows.Input;

namespace SimpleVPNApp;

/// <summary>
/// MainWindow.xaml에 대한 상호 작용 논리
/// </summary>
public partial class MainWindow : Window
{
    private bool _isClosingFromTray = false;

    public MainWindow()
    {
        InitializeComponent();
        
        // Window 창 이동 지원
        this.MouseDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        };

        // Rule: 창 닫기 시 트레이로 상주 (실제 종료가 아닌 숨기기)
        this.Closing += (s, e) =>
        {
            if (!_isClosingFromTray)
            {
                e.Cancel = true;
                this.Hide();
            }
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide(); // X 버튼 클릭 시 트레이 상주를 위해 숨김
    }

    // 트레이 메뉴에서 종료 시 호출할 메서드
    public void AppExit()
    {
        _isClosingFromTray = true;

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        this.Close();
        Application.Current.Shutdown();
    }
}
