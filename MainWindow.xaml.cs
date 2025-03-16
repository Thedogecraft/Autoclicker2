using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using AutoUpdaterDotNET;
using System.Reflection;
namespace Autoclicker
{
    // Windows APIs SUCK
    public partial class MainWindow
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;
        private uint selectedHotkey = 0x77;  // Default F8
        private const uint MOD_NONE = 0x0000;

        // Mouse button constants
        private uint mouseButtonDown = 0x02;  // Left button down
        private uint mouseButtonUp = 0x04;    // Left button up

        private bool autoclickerEnabled = false;
        private DispatcherTimer autoclickerTimer;
        private Random random = new Random();
        private int clickCount = 0;
        private int clickLimit = 0;

        // Minimum interval in milliseconds (1ms = 1000 clicks per second theoretical maximum)
        private const double MIN_INTERVAL = 10; // 10ms = up to 100 clicks per second

        public MainWindow()
        {
            InitializeComponent();
            AutoUpdater.Start("https://thedogecraft.github.io/Autoclicker/Version.xml");
            AutoUpdater.RunUpdateAsAdmin = true;
           
            InitializeAutoclickerTimer();
            ApplicationThemeManager.Apply(this);
            SetupUIBindings();

            // Make sure the slider's minimum value is set to allow for very fast clicking
            // This should be done in XAML, but im adding here as a backup
            if (ClickSpeedSlider != null)
            {
                ClickSpeedSlider.Minimum = MIN_INTERVAL;
                ClickSpeedSlider.Maximum = 1000; // 1 click per second at slowest
                ClickSpeedSlider.SmallChange = 10;
                ClickSpeedSlider.LargeChange = 50;
            }
        }
     
        private void SetupUIBindings()
        {
          
            StatusText.Text = "Ready - Press F8 to start";
            StatusIndicator.Fill = new SolidColorBrush(Colors.Gray);

            // Setup mouse button radio events
            LeftClickRadio.Checked += (s, e) => {
                mouseButtonDown = 0x02;  // Left button down
                mouseButtonUp = 0x04;    // Left button up
            };

            RightClickRadio.Checked += (s, e) => {
                mouseButtonDown = 0x08;  // Right button down
                mouseButtonUp = 0x10;    // Right button up
            };

            MiddleClickRadio.Checked += (s, e) => {
                mouseButtonDown = 0x20;  // Middle button down
                mouseButtonUp = 0x40;    // Middle button up
            };
        }

        private void InitializeAutoclickerTimer()
        {
            autoclickerTimer = new DispatcherTimer();
            autoclickerTimer.Interval = TimeSpan.FromMilliseconds(100);
            autoclickerTimer.Tick += AutoclickerTimer_Tick;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            RegisterHotkey();
        }

        private void RegisterHotkey()
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            HwndSource source = HwndSource.FromHwnd(hWnd);
            source.AddHook(HwndHook);

            if (!RegisterHotKey(hWnd, HOTKEY_ID, MOD_NONE, selectedHotkey))
            {
                return;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleAutoclicker();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void ToggleAutoclicker()
        {
            autoclickerEnabled = !autoclickerEnabled;

            if (autoclickerEnabled)
            {
                StartStopButton.Content = "Stop Clicking";
                StatusText.Text = "Running - Press hotkey to stop";
                StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                clickCount = 0;
                autoclickerTimer.Start();
            }
            else
            {
                StartStopButton.Content = "Start Clicking";
                StatusText.Text = "Ready - Press hotkey to start";
                StatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
                autoclickerTimer.Stop();
            }
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleAutoclicker();
        }

        private void AutoclickerTimer_Tick(object sender, EventArgs e)
        {
            DoMouseClick();
            clickCount++;

            if (clickLimit > 0 && clickCount >= clickLimit)
            {
                ToggleAutoclicker();
                return;
            }

            if (RandomDelayCheckBox.IsChecked == true)
            {
                double baseInterval = ClickSpeedSlider.Value;
                double variation = baseInterval * 0.25; // 25% variation
                double newInterval = baseInterval + (random.NextDouble() * variation * 2 - variation);
                newInterval = Math.Max(newInterval, MIN_INTERVAL);
                autoclickerTimer.Interval = TimeSpan.FromMilliseconds(newInterval);
            }
        }

        private void DoMouseClick()
        {
            mouse_event(mouseButtonDown | mouseButtonUp, 0, 0, 0, 0);
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            if (hWnd != IntPtr.Zero)
            {
                UnregisterHotKey(hWnd, HOTKEY_ID);
            }
            base.OnClosed(e);
        }

        private void ClickSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (autoclickerTimer != null)
            {
                double value = Math.Max(MIN_INTERVAL, Math.Round(e.NewValue));
                autoclickerTimer.Interval = TimeSpan.FromMilliseconds(value);

              
                double clicksPerSecond = 1000 / value;
                ClickSpeedLabel.Text = $"Clicking every {value} ms ({clicksPerSecond:F1} clicks per second)";
            }
        }

        private void HotkeyComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hWnd, HOTKEY_ID);

            string selectedKey = (HotkeyComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem).Content.ToString();
            switch (selectedKey)
            {
                case "F6":
                    selectedHotkey = 0x75;
                    break;
                case "F7":
                    selectedHotkey = 0x76;
                    break;
                case "F8":
                    selectedHotkey = 0x77;
                    break;
                case "F9":
                    selectedHotkey = 0x78;
                    break;
                case "F10":
                    selectedHotkey = 0x79;
                    break;
                case "F11":
                    selectedHotkey = 0x7A;
                    break;
                case "F12":
                    selectedHotkey = 0x7B;
                    break;
            }

            RegisterHotkey();
            StatusText.Text = $"Ready - Press {selectedKey} to start";
        }

        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string url = "https://github.com/thedogecraft/autoclicker";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}