﻿using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using UnityNamedPipe;

namespace VirtualMotionCaptureControlPanel
{
    /// <summary>
    /// SettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : Window
    {
        public class ResolutionItem
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int RefreshRate { get; set; }
        }
        public ObservableCollection<ResolutionItem> ResolutionItems;

        public ObservableCollection<TrackerConfigWindow.TrackerInfo> TrackersList { get; set; } = new ObservableCollection<TrackerConfigWindow.TrackerInfo>();

        private ObservableCollection<float> RotationItems = new ObservableCollection<float> { -180.0f, -135.0f, -90.0f, -45.0f, 0.0f, 45.0f, 90.0f, 135.0f, 180.0f };
        public SettingWindow()
        {
            var language = Globals.CurrentLanguage;
            InitializeComponent();
            this.DataContext = this;
            LanguageComboBox.SelectedItem = language;
            LeftHandRotateComboBox.ItemsSource = RotationItems;
            RightHandRotateComboBox.ItemsSource = RotationItems;
            if (RotationItems.Contains(Globals.LeftHandRotation)) LeftHandRotateComboBox.SelectedItem = Globals.LeftHandRotation;
            if (RotationItems.Contains(Globals.RightHandRotation)) RightHandRotateComboBox.SelectedItem = Globals.RightHandRotation;
        }

        private Brush WhiteBrush = new SolidColorBrush(Colors.White);
        private Brush ActiveBrush = new SolidColorBrush(Colors.Green);

        private ConcurrentDictionary<string, DateTime> endTime = new ConcurrentDictionary<string, DateTime>();
        private void Client_Received(object sender, DataReceivedEventArgs e)
        {
            if (e.CommandType == typeof(PipeCommands.TrackerMoved))
            {
                var d = (PipeCommands.TrackerMoved)e.Data;
                var time = DateTime.Now.AddSeconds(3);
                var item = TrackersList.Where(t => t.SerialNumber == d.SerialNumber).FirstOrDefault();
                if (item == null) return;
                Dispatcher.Invoke(() => item.Background = ActiveBrush);
                if (endTime.ContainsKey(d.SerialNumber))
                {
                    endTime[d.SerialNumber] = time;
                }
                else
                {
                    endTime.TryAdd(d.SerialNumber, time);
                }
                var task = Task.Run(async () =>
                {
                    while (time > DateTime.Now)
                    {
                        await Task.Delay(200);
                    }
                    DateTime tmpTime;
                    endTime.TryGetValue(d.SerialNumber, out tmpTime);
                    if (tmpTime == time)
                    {
                        endTime.TryRemove(d.SerialNumber, out tmpTime);
                        Dispatcher.Invoke(() => item.Background = WhiteBrush);
                    }
                });
            }
        }

        private void SetTrackersList(List<Tuple<string, string>> list, PipeCommands.SetTrackerSerialNumbers setting)
        {
            TrackersList.Clear();
            foreach (var d in list.OrderBy(d => d.Item1).ThenBy(d => d.Item2))
            {
                var trackerinfo = new TrackerConfigWindow.TrackerInfo { TypeName = d.Item1, SerialNumber = d.Item2, Background = WhiteBrush };
                TrackersList.Add(trackerinfo);
            }
        }

        private async void LeftHandRotateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LeftHandRotateComboBox.SelectedItem == null) return;
            Globals.LeftHandRotation = (float)LeftHandRotateComboBox.SelectedItem;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetHandRotations { LeftHandRotation = Globals.LeftHandRotation, RightHandRotation = Globals.RightHandRotation });
        }

        private async void RightHandRotateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RightHandRotateComboBox.SelectedItem == null) return;
            Globals.RightHandRotation = (float)RightHandRotateComboBox.SelectedItem;
            await Globals.Client.SendCommandAsync(new PipeCommands.SetHandRotations { LeftHandRotation = Globals.LeftHandRotation, RightHandRotation = Globals.RightHandRotation });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private async void ExternalCameraConigButton_Click(object sender, RoutedEventArgs e)
        {
            if (ControllerComboBox.SelectedItem == null)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SelectedItemError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var tracker = ControllerComboBox.SelectedItem as TrackerConfigWindow.TrackerInfo;
            var ofd = new OpenFileDialog();
            ofd.Filter = "externalcamera.cfg|externalcamera.cfg";
            if (ofd.ShowDialog() == true)
            {
                var configs = new Dictionary<string, string>();
                var lines = File.ReadAllLines(ofd.FileName);
                foreach (var line in lines)
                {
                    if (line.Contains("="))
                    {
                        var items = line.Split(new string[] { "=" }, 2, StringSplitOptions.None);
                        configs.Add(items[0], items[1]);
                    }
                }
                Func<string, float> GetFloat = (string key) =>
                {
                    if (configs.ContainsKey(key) == false) { return 0.0f; }
                    if (float.TryParse(configs[key], out var ret)) { return ret; }
                    return 0.0f;
                };
                var x = GetFloat("x");
                var y = GetFloat("y");
                var z = GetFloat("z");
                var rx = GetFloat("rx");
                var ry = GetFloat("ry");
                var rz = GetFloat("rz");
                var fov = GetFloat("fov");

                await Globals.Client?.SendCommandAsync(new PipeCommands.SetExternalCameraConfig { x = x, y = y, z = z, rx = rx, ry = ry, rz = rz, fov = fov, ControllerName = tracker.SerialNumber });
            }
        }

        private async void ExternalCameraConigExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (ControllerComboBox.SelectedItem == null)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SelectedItemError"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var tracker = ControllerComboBox.SelectedItem as TrackerConfigWindow.TrackerInfo;
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetExternalCameraConfig { ControllerName = tracker.SerialNumber }, r =>
            {
                var d = (PipeCommands.SetExternalCameraConfig)r;
                Dispatcher.Invoke(() =>
                {
                    var sfd = new SaveFileDialog();
                    sfd.Filter = "externalcamera.cfg|externalcamera.cfg";
                    sfd.Title = "Export externalcamera.cfg";
                    sfd.FileName = "externalcamera.cfg";
                    if (sfd.ShowDialog() == true)
                    {
                        var lines = new List<string>();
                        lines.Add($"x={d.x}");
                        lines.Add($"y={d.y}");
                        lines.Add($"z={d.z}");
                        lines.Add($"rx={d.rx}");
                        lines.Add($"ry={d.ry}");
                        lines.Add($"rz={d.rz}");
                        lines.Add($"fov={d.fov}");
                        lines.Add($"near=0.01");
                        lines.Add($"far=1000");
                        lines.Add($"disableStandardAssets=False");
                        lines.Add($"frameSkip=0");
                        File.WriteAllLines(sfd.FileName, lines);
                    }
                });
            });
        }

        private void TrackerConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new TrackerConfigWindow();
            win.Show();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var language = LanguageComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(language)) language = "Japanese";
            LanguageSelector.ChangeLanguage(language);
        }

        private bool isSetting = false;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetVirtualWebCamConfig { }, d =>
            {
                var config = (PipeCommands.SetVirtualWebCamConfig)d;
                Dispatcher.Invoke(() =>
                {
                    isSetting = true;
                    WebCamEnableCheckBox.IsChecked = config.Enabled;
                    WebCamResizeCheckBox.IsChecked = config.Resize;
                    WebCamMirrorCheckBox.IsChecked = config.Mirroring;
                    WebCamBufferingComboBox.SelectedIndex = config.Buffering;
                    isSetting = false;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetResolutions { }, d =>
            {
                var config = (PipeCommands.ReturnResolutions)d;
                Dispatcher.Invoke(() =>
                {
                    ResolutionItems = new ObservableCollection<ResolutionItem>(config.List.Select(r => new ResolutionItem { Width = r.Item1, Height = r.Item2, RefreshRate = r.Item3 }));
                    ResolutionComboBox.ItemsSource = ResolutionItems;
                });
            });
            await Globals.Client?.SendCommandWaitAsync(new PipeCommands.GetTrackerSerialNumbers(), d =>
            {
                var data = (PipeCommands.ReturnTrackerSerialNumbers)d;
                Dispatcher.Invoke(() => SetTrackersList(data.List, data.CurrentSetting));
            });
            Globals.Client.ReceivedEvent += Client_Received;
        }

        private void VirtualWebCamInstallButton_Click(object sender, RoutedEventArgs e)
        {
            var directory = @"C:\VMC_Camera\";
            if (Directory.Exists(directory) == false)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFolderCreate"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            try
            {
                File.Copy(Globals.GetCurrentAppDir() + @"VMC_Camera\VMC_CameraFilter32bit.dll", directory + "VMC_CameraFilter32bit.dll", true);
                File.Copy(Globals.GetCurrentAppDir() + @"VMC_Camera\VMC_CameraFilter64bit.dll", directory + "VMC_CameraFilter64bit.dll", true);
            }
            catch (Exception)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFileCopy"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var process32 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller32.exe", "/i /s " + directory + "VMC_CameraFilter32bit.dll");
            var process64 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller64.exe", "/i /s " + directory + "VMC_CameraFilter64bit.dll");
            process32.WaitForExit();
            process64.WaitForExit();
            if (process32.ExitCode == 0 && process64.ExitCode == 0)
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SuccessDriverInstall"));
            }
            else
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedDriverInstall"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void VirtualWebCamUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var directory = @"C:\VMC_Camera\";
            var process32 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller32.exe", "/u /s " + directory + "VMC_CameraFilter32bit.dll");
            var process64 = System.Diagnostics.Process.Start(Globals.GetCurrentAppDir() + "DLLInstaller64.exe", "/u /s " + directory + "VMC_CameraFilter64bit.dll");
            process32.WaitForExit();
            process64.WaitForExit();
            if (process32.ExitCode == 0 && process64.ExitCode == 0)
            {
                try
                {
                    File.Delete(directory + "VMC_CameraFilter32bit.dll");
                    File.Delete(directory + "VMC_CameraFilter64bit.dll");
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFileDelete"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                try
                {
                    Directory.Delete(directory);
                }
                catch (Exception)
                {
                    MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedFolderDelete"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                MessageBox.Show(LanguageSelector.Get("SettingWindow_SuccessDriverUninstall"));
            }
            else
            {
                MessageBox.Show(LanguageSelector.Get("SettingWindow_FailedDriverUninstall"), LanguageSelector.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WebCamCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateWebCamConfig();
        }

        private void WebCamBufferingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWebCamConfig();
        }

        private async void UpdateWebCamConfig()
        {
            if (isSetting) return;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetVirtualWebCamConfig
            {
                Enabled = WebCamEnableCheckBox.IsChecked == true,
                Resize = WebCamResizeCheckBox.IsChecked == true,
                Mirroring = WebCamMirrorCheckBox.IsChecked == true,
                Buffering = WebCamBufferingComboBox.SelectedIndex,
            });
        }

        private async void ResolutionApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResolutionComboBox.SelectedItem == null) return;
            var item = ResolutionComboBox.SelectedItem as ResolutionItem;
            await Globals.Client?.SendCommandAsync(new PipeCommands.SetResolution
            {
                Width = item.Width,
                Height = item.Height,
                RefreshRate = item.RefreshRate,
            });
        }
    }
}
