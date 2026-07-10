using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace EpgTimer.EpgView
{
    /// <summary>
    /// WeekDayView.xaml の相互作用ロジック
    /// </summary>
    public partial class WeekDayView : UserControl
    {
        public WeekDayView()
        {
            InitializeComponent();
        }

        public void ClearInfo()
        {
            stackPanel_day.Children.Clear();
        }

        public void SetDay(List<DateTime> dayList, double serviceWidth, bool gradationHeader)
        {
            stackPanel_day.Children.Clear();
            if (serviceWidth > 2)
            {
                foreach (DateTime time in dayList)
                {
                    var item = new TextBlock()
                    {
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        // やや重い感じになるのでbottomをつける
                        Padding = new Thickness(2, 0, 2, 2),
                        Text = time.ToString("M\\/d\r\n(ddd)"),
                        TextAlignment = TextAlignment.Center
                    };
                    var border = new Border()
                    {
                        BorderBrush = Brushes.DarkRed,
                        BorderThickness = new Thickness(),
                        Child = item,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Color backgroundColor;
                    if (time.DayOfWeek == DayOfWeek.Saturday)
                    {
                        item.Foreground = Brushes.DarkBlue;
                        backgroundColor = Colors.Lavender;
                    }
                    else if (time.DayOfWeek == DayOfWeek.Sunday)
                    {
                        item.Foreground = Brushes.DarkRed;
                        backgroundColor = Colors.MistyRose;
                    }
                    else
                    {
                        item.Foreground = Brushes.Black;
                        backgroundColor = Colors.White;
                    }
                    var gridItem = new UniformGrid();
                    if (gradationHeader == false)
                    {
                        gridItem.Background = new SolidColorBrush(backgroundColor);
                        gridItem.Background.Freeze();
                    }
                    else
                    {
                        gridItem.Background = ColorDef.GradientBrush(backgroundColor, 0.8);
                    }

                    gridItem.Margin = new Thickness(1, 1, 1, 1);
                    gridItem.Width = serviceWidth - 2;
                    gridItem.Tag = time;
                    gridItem.Children.Add(border);
                    stackPanel_day.Children.Add(gridItem);
                }
            }
        }

        public void SetTodayMark(int startHour)
        {
            DateTime today = DateTime.UtcNow.AddHours(9 - startHour).Date;
            UniformGrid todayItem = stackPanel_day.Children.OfType<UniformGrid>().FirstOrDefault(grid => (DateTime)grid.Tag == today);
            UniformGrid markedItem = stackPanel_day.Children.OfType<UniformGrid>().FirstOrDefault(grid => ((Border)grid.Children[0]).BorderThickness.Left != 0);
            if (todayItem != markedItem)
            {
                if (markedItem != null)
                {
                    ((Border)markedItem.Children[0]).BorderThickness = new Thickness();
                }
                if (todayItem != null)
                {
                    ((Border)todayItem.Children[0]).BorderThickness = new Thickness(1);
                }
            }
        }
    }
}
