using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EpgTimer.EpgView
{
    /// <summary>
    /// ServiceView.xaml の相互作用ロジック
    /// </summary>
    public partial class ServiceView : UserControl, IEpgSettingAccess, IEpgViewDataSet
    {
        public event Action LeftClick;

        public ServiceView()
        {
            InitializeComponent();
        }

        public void ClearInfo()
        {
            stackPanel_service.Children.Clear();
        }

        public int EpgSettingIndex { get; private set; }
        public void SetViewData(EpgViewData data)
        {
            EpgSettingIndex = data.EpgSettingIndex;
            Background = this.EpgBrushCache().ServiceBorderColor;
        }

        public void SetService(List<EpgServiceInfo> serviceList)
        {
            stackPanel_service.Children.Clear();
            uint tickCountToPreventAccidentalClick = (uint)Environment.TickCount;

            foreach (EpgServiceInfo info in serviceList)
            {
                var service1 = new StackPanel();
                service1.Width = this.EpgStyle().ServiceWidth - 1;
                service1.VerticalAlignment = VerticalAlignment.Center;
                DispatcherTimer clickTimer = null;
                service1.MouseLeftButtonDown += (sender, e) =>
                {
                    if (e.ClickCount == 1 && clickTimer == null && LeftClick != null && (uint)e.Timestamp - tickCountToPreventAccidentalClick > 500)
                    {
                        // ダブルクリックと区別するため
                        clickTimer = new DispatcherTimer();
                        clickTimer.Interval = CommonUtil.GetDoubleClickTime() + TimeSpan.FromMilliseconds(100);
                        clickTimer.Tick += (sender2, e2) =>
                        {
                            clickTimer.Stop();
                            clickTimer = null;
                            if (LeftClick != null) LeftClick();
                        };
                        clickTimer.Start();
                    }
                    else if (clickTimer != null)
                    {
                        clickTimer.Stop();
                        clickTimer = null;
                    }
                    if (e.ClickCount == 2)
                    {
                        var serviceInfo = ((FrameworkElement)sender).DataContext as EpgServiceInfo;
                        if (Settings.Instance.UseWatchCmd == false)
                        {
                            CommonManager.Instance.TVTestCtrl.SetLiveCh(info.ONID, info.TSID, info.SID);
                        }
                        else if (Settings.Instance.WatchCmd.Length > 0)
                        {
                            var cmdLine = new string[] { Settings.Instance.WatchCmd, Settings.Instance.WatchCmdOpt };
                            for (int i = 0; i < 2; i++)
                            {
                                cmdLine[i] = cmdLine[i]
                                    .Replace("$ONID$", info.ONID.ToString())
                                    .Replace("$ONID10$", info.ONID.ToString())
                                    .Replace("$ONID16$", info.ONID.ToString("X4"))
                                    .Replace("$TSID$", info.TSID.ToString())
                                    .Replace("$TSID10$", info.TSID.ToString())
                                    .Replace("$TSID16$", info.TSID.ToString("X4"))
                                    .Replace("$SID$", info.SID.ToString())
                                    .Replace("$SID10$", info.SID.ToString())
                                    .Replace("$SID16$", info.SID.ToString("X4"));
                            }
                            try
                            {
                                using (Process.Start(new ProcessStartInfo(cmdLine[0], cmdLine[1]) { UseShellExecute = true })) { }
                            }
                            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                        }
                    }
                };
                //service1.DataContext = info;

                var text = ViewUtil.GetPanelTextBlock(CommonManager.ReplaceUrl(info.service_name));
                text.Margin = new Thickness(1, 0, 1, 0);
                text.Foreground = this.EpgBrushCache().ServiceFontColor;
                service1.Children.Add(text);

                int chnum = ChSet5.ChNumber(info.Key);
                text = ViewUtil.GetPanelTextBlock((info.IsDttv ? (chnum != 0 ? "地デジ " : "ServiceID:") : CommonManager.ReplaceUrl(info.network_name) + " ") + (chnum != 0 ? chnum : info.SID).ToString());
                text.Margin = new Thickness(1, 0, 1, 2);
                text.Foreground = this.EpgBrushCache().ServiceFontColor;
                service1.Children.Add(text);

                service1.ToolTip = this.EpgStyle().EpgServiceNameTooltip != true ? null : ViewUtil.ServiceHeaderToToolTip(service1);

                var stack = new StackPanel();
                stack.Orientation = Orientation.Horizontal;
                stack.Background = this.EpgBrushCache().ServiceBackColor;
                stack.Margin = new Thickness(0, 1, 1, 1);
                stack.Tag = service1.Width;
                stack.DataContext = info;
                stack.Children.Add(service1);
                stackPanel_service.Children.Add(stack);
            }

            RefreshLogo();
        }

        public void RefreshLogo()
        {
            foreach (StackPanel stack in stackPanel_service.Children)
            {
                Image logoItem = stack.Children.OfType<Image>().FirstOrDefault();
                if (logoItem != null)
                {
                    stack.Children.Remove(logoItem);
                }

                StackPanel item = stack.Children.OfType<StackPanel>().First();
                double serviceWidth = (double)stack.Tag;

                var info = (EpgServiceInfo)stack.DataContext;
                if (Settings.Instance.ShowLogo && info.Logo != null && serviceWidth >= 30 + 1 + 2)
                {
                    logoItem = new Image();
                    logoItem.Source = info.Logo;
                    logoItem.Width = 30;
                    logoItem.VerticalAlignment = VerticalAlignment.Top;
                    logoItem.Margin = new Thickness(1, 2, 0, 0);
                    stack.Children.Insert(0, logoItem);
                    item.Width = serviceWidth - logoItem.Width - logoItem.Margin.Left;
                }
                else
                {
                    item.Width = serviceWidth;
                }
            }
        }
    }
}
