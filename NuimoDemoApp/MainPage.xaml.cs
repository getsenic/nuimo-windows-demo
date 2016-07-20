using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using NuimoSDK;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NuimoDemoApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        private readonly PairedNuimoManager _pairedNuimoManager = new PairedNuimoManager();
        private IEnumerable<INuimoController> _nuimoControllers = new List<INuimoController>();
        private INuimoController _nuimoController;

        public MainPage()
        {
            InitializeComponent();
            ListPairedNuimos();
            AddLedCheckBoxes();
            OutputTextBox.TextWrapping = TextWrapping.NoWrap;
            DisplayIntervalTextBox.Text = "5.0";

            Application.Current.DebugSettings.EnableFrameRateCounter = false;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;
        }

        private async void ListPairedNuimos()
        {
            PairedNuimosComboBox.Items?.Clear();
            _nuimoControllers = await _pairedNuimoManager.ListPairedNuimosAsync();
            foreach (var nuimoController in _nuimoControllers)
            {
                PairedNuimosComboBox.Items?.Add("Nuimo: " + nuimoController.Identifier);
            }
            ReloadButton.Content = "Reload";
            if (PairedNuimosComboBox.Items?.Count > 0) PairedNuimosComboBox.SelectedIndex = 0;
        }

        private void AddLedCheckBoxes()
        {
            for (var row = 0; row < 9; row++)
            {
                for (var col = 0; col < 9; col++)
                {
                    var checkBox = new CheckBox
                    {
                        Background = new SolidColorBrush(new Color { A = 255, B = 255, G = 255, R = 255 })
                    };
                    Grid.SetRow(checkBox, row);
                    Grid.SetColumn(checkBox, col);
                    LedGrid.Children.Add(checkBox);
                }
            }
        }

        public async void Close()
        {
            var task = _nuimoController?.DisconnectAsync();
            if (task != null) await task;
        }

        private async void ConnectButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_nuimoControllers == null) return;
            var oldNuimoController = _nuimoController;
            if (oldNuimoController != null) RemoveDelegates(oldNuimoController);
            _nuimoController = _nuimoControllers.ElementAt(PairedNuimosComboBox.SelectedIndex);

            AddDelegates(_nuimoController);

            switch (_nuimoController.ConnectionState)
            {
                case NuimoConnectionState.Disconnected: await _nuimoController.ConnectAsync();    break;
                case NuimoConnectionState.Connected:    await _nuimoController.DisconnectAsync(); break;
            }
        }

        private void AddDelegates(INuimoController nuimoController)
        {
            nuimoController.GestureEventOccurred     += OnNuimoGestureEvent;
            nuimoController.FirmwareVersionRead      += OnFirmwareVersion;
            nuimoController.ConnectionStateChanged   += OnConnectionState;
            nuimoController.BatteryPercentageChanged += OnBatteryPercentage;
            nuimoController.LedMatrixDisplayed       += OnLedMatrixDisplayed;
        }

        private void RemoveDelegates(INuimoController nuimoController)
        {
            nuimoController.GestureEventOccurred     -= OnNuimoGestureEvent;
            nuimoController.FirmwareVersionRead      -= OnFirmwareVersion;
            nuimoController.ConnectionStateChanged   -= OnConnectionState;
            nuimoController.BatteryPercentageChanged -= OnBatteryPercentage;
            nuimoController.LedMatrixDisplayed       -= OnLedMatrixDisplayed;
        }

        private async void ReloadButton_OnClick(object sender, RoutedEventArgs e)
        {
            var task = _nuimoController?.DisconnectAsync();
            if (task != null) await task;
            ListPairedNuimos();
        }

        private void PairedNuimosComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConnectButton.IsEnabled = e.AddedItems != null;
        }

        private async void DisplayMatrix_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var displayInterval = GetLedMatrixDisplayInterval();
                var matrixString = GetLedMatrixString();
                var options = GetLedMatrixOptions();
                _nuimoController?.DisplayLedMatrixAsync(new NuimoLedMatrix(matrixString), displayInterval, options);
            }
            catch (FormatException) { await new MessageDialog("Display interval: Please enter a number between 0.00 and 25.5").ShowAsync(); }
        }

        private double GetLedMatrixDisplayInterval()
        {
            var displayIntervalText = DisplayIntervalTextBox.Text.Replace(',', '.');
            return displayIntervalText.Length > 0 ? double.Parse(displayIntervalText, new CultureInfo("us")) : 2;
        }

        private string GetLedMatrixString()
        {
            return LedGrid.Children
                .Select(element => element as CheckBox)
                .Select(checkBox => (checkBox.IsChecked ?? false) ? "*" : " ")
                .Aggregate((matrix, led) => matrix + led);
        }

        private int GetLedMatrixOptions()
        {
            return
                ((FadeTransitionCheckBox.IsChecked ?? false) ? (int)NuimoLedMatrixWriteOption.WithFadeTransition : 0) +
                ((WithoutWriteResponseCheckBox.IsChecked ?? false) ? (int)NuimoLedMatrixWriteOption.WithoutWriteResponse : 0);
        }

        private void OnNuimoGestureEvent(NuimoGestureEvent nuimoGestureEvent)
        {
            OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                .Append("NuimoGesture: ")
                .Append(nuimoGestureEvent.Gesture)
                .Append(" value: ")
                .Append(nuimoGestureEvent.Value + "\n")
                .ToString();
            OutputTextBox.ScrollToBottom();
        }

        private void OnFirmwareVersion(string firmwareVersion)
        {
            OutputTextBox.Text = "-------------------------------\nFirmware version: " + firmwareVersion + "\n-------------------------------\n";
        }

        private void OnConnectionState(NuimoConnectionState nuimoConnectionState)
        {
            string buttonTitle;
            switch (nuimoConnectionState)
            {
                case     NuimoConnectionState.Disconnected:  buttonTitle = "Connect";          break;
                case     NuimoConnectionState.Connecting:    buttonTitle = "Connecting...";    break;
                case     NuimoConnectionState.Connected:     buttonTitle = "Disconnect";       break;
                case     NuimoConnectionState.Disconnecting: buttonTitle = "Disconnecting..."; break;
                default: buttonTitle = "";                                                 break;
            }
            ConnectButton.Content = buttonTitle;
            ConnectionStateTextBlock.Text = nuimoConnectionState.ToString();
        }

        private void OnBatteryPercentage(int batteryPercentage)
        {
            BatteryPercentageTextBlock.Text = batteryPercentage + "%";
        }

        private void OnLedMatrixDisplayed()
        {
            OutputTextBox.Text = new StringBuilder(OutputTextBox.Text)
                .Append("The matrix you have sent has been displayed." + "\n")
                .ToString();
            OutputTextBox.ScrollToBottom();
        }
    }
}

internal static class DependencyObjectExtension
{
    public static void ScrollToBottom(this DependencyObject dependencyObject)
    {
        var grid = (Grid)VisualTreeHelper.GetChild(dependencyObject, 0);
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
        {
            object obj = VisualTreeHelper.GetChild(grid, i);
            if (!(obj is ScrollViewer)) continue;
            ((ScrollViewer)obj).ScrollToVerticalOffset(((ScrollViewer)obj).ExtentHeight);
            break;
        }
    }
}
