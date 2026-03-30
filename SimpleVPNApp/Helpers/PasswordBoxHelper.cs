using System.Windows;
using System.Windows.Controls;

namespace SimpleVPNApp.Helpers;

/// <summary>
/// PasswordBox의 Password를 바인딩하기 위한 헬퍼입니다.
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false, OnBindPasswordChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject dp) =>
        (string)dp.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject dp, string value) =>
        dp.SetValue(BoundPasswordProperty, value);

    public static bool GetBindPassword(DependencyObject dp) =>
        (bool)dp.GetValue(BindPasswordProperty);

    public static void SetBindPassword(DependencyObject dp, bool value) =>
        dp.SetValue(BindPasswordProperty, value);

    private static bool GetIsUpdating(DependencyObject dp) =>
        (bool)dp.GetValue(IsUpdatingProperty);

    private static void SetIsUpdating(DependencyObject dp, bool value) =>
        dp.SetValue(IsUpdatingProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox)
        {
            return;
        }

        passwordBox.PasswordChanged -= HandlePasswordChanged;

        if (!GetIsUpdating(passwordBox))
        {
            passwordBox.Password = e.NewValue as string ?? string.Empty;
        }

        passwordBox.PasswordChanged += HandlePasswordChanged;
    }

    private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox)
        {
            return;
        }

        if ((bool)e.OldValue)
        {
            passwordBox.PasswordChanged -= HandlePasswordChanged;
        }

        if ((bool)e.NewValue)
        {
            passwordBox.PasswordChanged += HandlePasswordChanged;
        }
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }
}
