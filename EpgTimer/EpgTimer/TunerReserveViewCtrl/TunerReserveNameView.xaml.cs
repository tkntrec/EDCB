using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EpgTimer.TunerReserveViewCtrl
{
    /// <summary>
    /// TunerReserveNameView.xaml の相互作用ロジック
    /// </summary>
    public partial class TunerReserveNameView : UserControl
    {
        public TunerReserveNameView()
        {
            InitializeComponent();
        }

        public void ClearInfo()
        {
            stackPanel_tuner.Children.Clear();
        }

        public void SetTunerInfo(List<TunerNameViewItem> tunerInfo)
        {
            stackPanel_tuner.Children.Clear();
            foreach (TunerNameViewItem info in tunerInfo)
            {
                var grid = new Grid()
                {
                    Background = (Brush)FindResource("AppTunerReserveHeaderTextBackgroundBrush"),
                    Margin = new Thickness(1, 2, 1, 2),
                    Width = info.Width - 2
                };
                grid.Children.Add(new TextBlock()
                {
                    Style = (Style)FindResource("AppTunerReserveHeaderTextBlockStyle"),
                    Text = info.TunerInfo.tunerName + (info.TunerInfo.tunerID != 0xFFFFFFFF ? "\r\nID: " + info.TunerInfo.tunerID.ToString("X8") : "")
                });
                stackPanel_tuner.Children.Add(grid);
            }
        }
    }

}
