using System;
// Ported from FireAxe_Alt\FireAxe.GUI\Views\AboutWindow.axaml.cs
using System.Runtime.InteropServices;
using FireAxe.Views.Controls;
using Microsoft.UI.Xaml;

namespace FireAxe.Views;

public sealed partial class AboutWindow : HostedPage
{
    public AboutWindow()
    {
        InitializeComponent();
        _ = new ViewModelLifecycleBinder((FrameworkElement)Content);
    }

    public string FrameworkDescription => RuntimeInformation.FrameworkDescription;
}
