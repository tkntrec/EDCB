using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EpgTimer
{
    using BoxExchangeEdit;
 
    public enum JumpItemStyle : ulong { None = 0, JumpTo = 1, MoveTo = 2, PanelNoScroll = 0x10000 }

    public static class ViewUtil
    {
        private static Matrix deviceMatrix = new Matrix();
        public static Matrix DeviceMatrix
        {
            get
            {
                var mw = CommonManager.MainWindow;//主にデザイン画面のエラー対策
                if (mw != null)
                {
                    var ps = PresentationSource.FromVisual(mw);
                    if (ps != null)
                    {
                        deviceMatrix = ps.CompositionTarget.TransformToDevice;
                    }
                }
                return deviceMatrix;
            }
        }
        public static double SnapsToDevicePixelsX(double x, int mode = 0)
        {
            return SnapsToDevicePixels(x, DeviceMatrix.M11, mode);
        }
        public static double SnapsToDevicePixelsY(double y, int mode = 0)
        {
            return SnapsToDevicePixels(y, DeviceMatrix.M22, mode);
        }
        public static double SnapsToDevicePixels(double v, double m, int mode = 0)
        {
            return (mode == 0 ? Math.Floor : mode == 1 ? Math.Round : (Func<double, double>)Math.Ceiling)(v * m) / m;
        }

        public static Brush EpgDataContentBrush(EpgEventInfo EventInfo, int EpgSettingIndex = 0, bool filtered = false)
        {
            if (EventInfo == null) return null;

            var nibbleList = (EventInfo.ContentInfo ?? new EpgContentInfo()).nibbleList;
            return EpgDataContentBrush(nibbleList, EpgSettingIndex, filtered);
        }
        public static Brush EpgDataContentBrush(List<EpgContentData> nibbleList, int EpgSettingIndex = 0, bool filtered = false)
        {
            List<Brush> colorList = filtered ? Settings.BrushCache.Epg[EpgSettingIndex].ContentFilteredColorList : Settings.BrushCache.Epg[EpgSettingIndex].ContentColorList;
            if (nibbleList != null)
            {
                //0x0C,0D(将来用)は、設定UIは無いが色設定データ自体はあるので他と同様に扱う。
                //0x0E00(番組特性コード)と0x0E02～(未定義で将来の割り当ては不明)は除外。
                //0x10以上は存在しないが、コード上は弾いておく。
                EpgContentData info = nibbleList.Find(n => n.content_nibble_level_1 <= 0x0F
                                && (n.content_nibble_level_1 != 0x0E || n.content_nibble_level_2 == 0x01));

                if (info != null)
                {
                    if (info.content_nibble_level_1 == 0x0E)
                    {
                        //CSのコード置き換え。通常は一般のジャンル情報も付いているので、効果は薄いかも。
                        switch (info.user_nibble_1)
                        {
                            case 0x00: return colorList[0x01];//スポーツ(CS)→スポーツ
                            case 0x01: return colorList[0x06];//洋画(CS)→映画
                            case 0x02: return colorList[0x06];//邦画(CS)→映画
                            case 0x03: return colorList[0x0F];//その他(CS)→その他
                            default: return colorList[0x0F];//将来用→その他
                        }
                    }
                    return colorList[info.content_nibble_level_1];
                }
            }
            return colorList[0x10];
        }

        public static Brush ReserveErrBrush(ReserveData ReserveData, bool defTransParent = false)
        {
            int idx = defTransParent ? -1 : 0;
            if (ReserveData != null)
            {
                if (ReserveData.IsEnabled == false)
                {
                    idx = 1;
                }
                else if (ReserveData.OverlapMode == 2)
                {
                    idx = 2;
                }
                else if (ReserveData.OverlapMode == 1)
                {
                    idx = 3;
                }
                else if (ReserveData.IsAutoAddInvalid == true)
                {
                    idx = 4;
                }
                else if (ReserveData.IsMultiple == true)
                {
                    idx = 5;
                }
            }
            return idx < 0 ? null : Settings.BrushCache.ResBackColor[idx];
        }

        public static void SetSpecificChgAppearance(Control obj)
        {
            obj.Background = Brushes.LavenderBlush;
            obj.BorderThickness = new Thickness(2);
            obj.BorderBrush = Brushes.Red;
        }

        //ジャンル絞り込み
        public static bool ContainsContent(EpgEventInfo info, HashSet<uint> ContentHash, bool notContent = false)
        {
            //絞り込み無し
            if (ContentHash.Count == 0) return true;

            //ジャンルデータ'なし'扱い。
            if (info.ContentInfo == null)
            {
                return ContentHash.Contains(0xFFFF0000) == !notContent;
            }
            if (ContentHash.Contains(0xFFFF0000) == true &&
                    info.ContentInfo.nibbleList.Any(data => data.IsAttributeInfo == false) != true)
            {
                return !notContent;
            }

            //不明なジャンル
            if (ContentHash.Contains(0xFEFF0000) == true &&
                    info.ContentInfo.nibbleList.Any(data => CommonManager.ContentKindDictionary.ContainsKey(data.Key) == false))
            {
                return !notContent;
            }

            //検索
            return info.ContentInfo.nibbleList.Any(data => ContentHash.Contains(data.Key)) != notContent;
        }

        public static void AddTimeList(ICollection<DateTime> timeList, DateTime startTime, uint duration)
        {
            AddTimeList(timeList, startTime, startTime.AddSeconds(duration));
        }
        public static void AddTimeList(ICollection<DateTime> timeList, DateTime startTime, DateTime lastTime)
        {
            var chkStartTime = startTime.Date.AddHours(startTime.Hour);
            while (chkStartTime <= lastTime)
            {
                timeList.Add(chkStartTime);
                chkStartTime += TimeSpan.FromHours(1);
            }
        }

        public static void SetItemVerticalPos(List<DateTime> timeList, PanelItem item, DateTime startTime, uint duration, double MinutesHeight, bool NeedTimeOnly)
        {
            item.Height = duration * MinutesHeight / 60;
            var chkStartTime = NeedTimeOnly == false ? timeList[0] : startTime.Date.AddHours(startTime.Hour);
            int offset = NeedTimeOnly == false ? 0 : 60 * timeList.BinarySearch(chkStartTime);
            if (offset >= 0)
            {
                item.TopPos = (offset + (startTime - chkStartTime).TotalMinutes) * MinutesHeight;
            }
        }

        //最低表示高さ
        public const double PanelMinimumHeight = 2;
        public static double CulcLineHeight(double fontHeight) { return 1 + fontHeight * 1.1; }
        public static void ModifierMinimumLine(IEnumerable<PanelItem> list, double minimumLine, double fontHeight, double lineWidth)
        {
            double minimum = Math.Max(CulcLineHeight(fontHeight) * minimumLine + lineWidth * (minimumLine == 0 ? 0 : 1), PanelMinimumHeight);
            double lastLeft = double.MinValue;
            double lastBottom = 0;
            foreach (PanelItem item in list.OrderBy(item => item.LeftPos * 1e6 + item.TopPos))
            {
                if (lastLeft != item.LeftPos)
                {
                    lastLeft = item.LeftPos;
                    lastBottom = double.MinValue;
                }
                if (item.TopPos < lastBottom)
                {
                    item.Height = Math.Max(item.BottomPos - lastBottom, minimum);
                    item.TopPos = lastBottom;
                }
                else
                {
                    item.Height = Math.Max(item.Height, minimum);
                }
                lastBottom = item.BottomPos;
            }
        }

        public static void AdjustTimeList(IEnumerable<PanelItem> list, List<DateTime> timeList, double MinutesHeight)
        {
            if (list.Any() == true && timeList.Count > 0)
            {
                double bottom = list.Max(info => info.BottomPos);
                AddTimeList(timeList, timeList.Last().AddHours(1), timeList.Last().AddHours(1 + bottom / 60 / MinutesHeight - timeList.Count));
            }
        }

        //指定アイテムまでマーキング付で移動する。
        public static int JumpToListItem(object target, ListBox listBox, JumpItemStyle style = JumpItemStyle.None, bool dryrun = false)
        {
            if (target is IGridViewSorterItem)
            {
                return JumpToListItem(((IGridViewSorterItem)target).KeyID, listBox, style, dryrun);
            }
            else
            {
                return ScrollToFindItem(target, listBox, style, dryrun);
            }
        }
        public static int JumpToListItem(ulong gvSorterID, ListBox listBox, JumpItemStyle style = JumpItemStyle.None, bool dryrun = false)
        {
            var target = listBox.Items.OfType<IGridViewSorterItem>().FirstOrDefault(data => data.KeyID == gvSorterID);
            return ScrollToFindItem(target, listBox, style, dryrun);
        }
        public static int ScrollToFindItem(object target, ListBox listBox, JumpItemStyle style = JumpItemStyle.None, bool dryrun = false)
        {
            int selIdx = -1;
            try
            {
                selIdx = listBox.Items.IndexOf(target);
                if (dryrun == false) listBox.SelectedIndex = -1;
                if (dryrun == true || selIdx < 0) return selIdx;

                listBox.SelectedIndex = selIdx;
                listBox.ScrollIntoView(target);

                //パネルビューと比較して、こちらでは最後までゆっくり点滅させる。全表示時間は同じ。
                //ただ、結局スクロールさせる位置がうまく調整できてないので効果は限定的。
                if ((style & ~JumpItemStyle.PanelNoScroll) == JumpItemStyle.JumpTo && target is DataListItemBase)
                {
                    var target_item = target as DataListItemBase;
                    listBox.SelectedItem = null;

                    var notifyTimer = new DispatcherTimer();
                    notifyTimer.Interval = TimeSpan.FromSeconds(0.2);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    notifyTimer.Tick += (sender, e) =>
                    {
                        if (sw.ElapsedMilliseconds > Settings.Instance.DisplayNotifyJumpTime * 1000)
                        {
                            notifyTimer.Stop();
                            target_item.NowJumpingTable = 0;
                            listBox.SelectedItem = target_item;
                        }
                        else
                        {
                            target_item.NowJumpingTable = target_item.NowJumpingTable != 1 ? 1 : 2;
                        }
                        listBox.Items.Refresh();
                    };
                    notifyTimer.Start();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return selIdx;
        }

        //リストボックスの巡回移動
        public static int GetNextIdx(int oldIdx, int nowIdx, int count, int direction)
        {
            if (oldIdx >= 0 && nowIdx == -1 && direction == 1) direction = 0;//表示アイテムが削除された後に移動する場合の調整
            if (oldIdx >= count) oldIdx = count - (direction > 0 ? 1 : 0);
            if (nowIdx >= count) nowIdx = count - (direction > 0 ? 1 : 0);
            oldIdx = (oldIdx == -1 || nowIdx != -1) ? nowIdx : oldIdx;
            return oldIdx == -1 ? (direction >= 0 ? 0 : count - 1) : ((oldIdx + direction) % count + count) % count;
        }

        //パネル系画面の移動用
        public static object MoveNextReserve(ref int itemIdx, PanelViewBase view, IEnumerable<ReserveViewItem> reslist, ref Point jmpPos,
                                        ulong id, int direction, bool move = true, JumpItemStyle style = JumpItemStyle.MoveTo)
        {
            Point pos = jmpPos;
            jmpPos = new Point(-1, -1);
            if (reslist.Any() == false) return null;

            List<ReserveViewItem> list = reslist.OrderBy(d => d.Data.StartTimeActual).ToList();
            ReserveViewItem viewItem = null;
            int idx = id == 0 ? -1 : list.FindIndex(item => item.Data.ReserveID == id);
            if (idx == -1 && pos.X >= 0)
            {
                viewItem = list.GetNearDataList(pos).First();
                idx = list.IndexOf(viewItem);
            }
            else
            {
                idx = ViewUtil.GetNextIdx(itemIdx, idx, list.Count, direction);
                viewItem = list[idx];
            }
            if (move == true) view.ScrollToFindItem(viewItem, style);
            if (move == true) itemIdx = idx;
            return viewItem == null ? null : viewItem.Data;
        }
        public static void OnKeyMoveNextReserve(object sender, KeyEventArgs e, DataItemViewBase view)
        {
            if (e.Handled || Keyboard.Modifiers != ModifierKeys.Control || view == null) return;
            //
            switch (e.Key)
            {
                case Key.Up: view.MoveNextReserve(-1); break;
                case Key.Down: view.MoveNextReserve(1); break;
                default: return;
            }
            e.Handled = true;
        }

        /// <summary> 列の端でダブルクリックしたのと同様の効果。見えてない行まで考慮されないのも同じ。 </summary>
        public static void FitColumnWidth(this ListView box)
        {
            var gridView = box.View as GridView;
            if (gridView != null)
            {
                foreach (var col in gridView.Columns)
                {
                    col.Width = 0;
                    col.Width = double.NaN;
                }
                box.Items.Refresh();
            }
        }

        //無効だけどテキストは選択出来るような感じ
        public static void SetReadOnlyWithEffect(this TextBox obj, bool val) { SetReadOnlyWithEffectObj(obj, val); }
        public static void SetReadOnlyWithEffect(this ComboBox obj, bool val) { SetReadOnlyWithEffectObj(obj, val); }
        private static void SetReadOnlyWithEffectObj(DependencyObject obj, bool val)
        {
            obj.SetValue(Control.IsEnabledProperty, true);
            obj.SetValue(TextBox.IsReadOnlyProperty, val);//ComboBox.IsReadOnlyPropertyと同じもの
            SetDisabledEffect(obj, val);
        }
        public static void SetDisabledEffect(DependencyObject obj, bool val)
        {
            if (val == true)
            {
                //SystemColors.ControlBrushとは少し違うらしい
                obj.SetValue(Control.BackgroundProperty, new SolidColorBrush(ColorDef.FromUInt(0xFFF4F4F4)));
                obj.SetValue(Control.ForegroundProperty, SystemColors.GrayTextBrush);
            }
            else
            {
                obj.ClearValue(Control.BackgroundProperty);
                obj.ClearValue(Control.ForegroundProperty);
            }
        }
        public static void SetIsEnabledChildren(UIElement ele, bool isEnabled)
        {
            ele.IsEnabled = true;
            foreach (var child in LogicalTreeHelper.GetChildren(ele).OfType<UIElement>())
            {
                child.IsEnabled = isEnabled;
            }
        }
        /*/未使用
        public static DependencyObject SearchParentWpfTree(DependencyObject obj, Type t_trg, Type t_cut = null)
        {
            Func<string, DependencyObject> GetParentFromProperty = name =>
            {
                PropertyInfo p = obj.GetType().GetProperty(name);
                return p == null ? null : p.GetValue(obj, null) as DependencyObject;
            };
            while (true)
            {
                if (obj == null || obj.GetType() == t_trg) return obj;
                if (obj.GetType() == t_cut) return null;

                DependencyObject trg = GetParentFromProperty("TemplatedParent");
                if (trg == null) trg = GetParentFromProperty("Parent");//次の行と同じ？
                if (trg == null) trg = LogicalTreeHelper.GetParent(obj);
                obj = trg;
            }
        }
        /*/
        public static string ConvertSearchItemStatus(IEnumerable<SearchItem> list, string itemText = "番組数")
        {
            return string.Format("{0}:{1}", itemText, list.Count()) + ConvertReserveStatus(list, " 予約");
        }
        public static string ConvertReserveStatus(IEnumerable<SearchItem> list, string itemText = "予約数", int reserveMode = 0)
        {
            if (reserveMode == 0 && list.Any() != true) return "";
            return ConvertReserveStatus(list.GetReserveList(), itemText, reserveMode);
        }
        public static string ConvertReserveStatus(List<ReserveData> rlist, string itemText = "予約数", int reserveMode = 0)
        {
            var text = string.Format("{0}:{1}", itemText, rlist.Count);
            List<ReserveData> onlist = rlist.FindAll(data => data.IsEnabled == true);
            if (reserveMode == 0 || (reserveMode != 3 && rlist.Count != onlist.Count))
            {
                text += string.Format(" (有効:{0} 無効:{1})", onlist.Count, rlist.Count - onlist.Count);
            }
            if (reserveMode != 0)
            {
                if (reserveMode <= 2)
                {
                    uint sum = (uint)(onlist.Sum(info => info.DurationActual));
                    text += (reserveMode == 1 ? " 総録画時間:" : " 録画時間:")
                            + CommonManager.ConvertDurationText(sum, false);
                }
                else
                {
                    long errs = onlist.Count(item => item.OverlapMode == 2);
                    long warns = onlist.Count(item => item.OverlapMode == 1);
                    if (Settings.Instance.TunerDisplayOffReserve == true)
                    {
                        long off = rlist.Count - onlist.Count;
                        text += string.Format(" (チューナー不足:{0} 一部録画:{1} 無効予約:{2})", errs, warns, off);
                    }
                    else
                    {
                        text += string.Format(" (チューナー不足:{0} 一部録画:{1})", errs, warns);
                    }
                }
            }
            return text;
        }
        public static string ConvertRecinfoStatus(IEnumerable<RecInfoItem> list, string itemText = "録画結果")
        {
            var format = "{0}:{1} ({2}:{3} {4}:{5})";
            if (Settings.Instance.RecinfoErrCriticalDrops == true)
            {
                return string.Format(format, itemText, list.Count(),
                    "*Drop", list.Sum(item => item.RecInfo.DropsCritical),
                    "*Scramble", list.Sum(item => item.RecInfo.ScramblesCritical));
            }
            else
            {
                return string.Format(format, itemText, list.Count(),
                    "Drop", list.Sum(item => item.RecInfo.Drops),
                    "Scramble", list.Sum(item => item.RecInfo.Scrambles));
            }
        }
        public static string ConvertAutoAddStatus(IEnumerable<AutoAddDataItem> list, string itemText = "自動予約登録数")
        {
            var onRes = new List<uint>();
            var offRes = new List<uint>();
            foreach (var rlist in list.Select(data => data.Data.GetReserveList()))
            {
                onRes.AddRange(rlist.Where(item => item.IsEnabled == true).Select(res => res.ReserveID));
                offRes.AddRange(rlist.Where(item => item.IsEnabled == false).Select(res => res.ReserveID));
            }
            return string.Format("{0}:{1} (有効予約数:{2} 無効予約数:{3})",
                itemText, list.Count(), onRes.Distinct().Count(), offRes.Distinct().Count());
        }
        public static string ConvertInfoSearchItemStatus(IEnumerable<InfoSearchItem> list, string itemText)
        {
            string det = "";
            foreach (var key in InfoSearchItem.ViewTypeNameList())
            {
                int num = list.Count(item => item.ViewItemName == key);
                if (num != 0)
                {
                    det += string.Format("{0}:{1} ", key.Substring(0, 2), num);
                }
            }
            return string.Format("{0}:{1}", itemText, list.Count()) + (det == "" ? "" : " (" + det.TrimEnd() + ")");
        }

        public static Type GetListBoxItemType(ListBox lb)
        {
            if (lb == null) return null;
            //場合分けしないで取得する方法があるはず
            return lb is ListView ? typeof(ListViewItem) : lb is ListBox ? typeof(ListBoxItem) : typeof(object);
        }
        public static void ResetItemContainerStyle(ListBox lb)
        {
            try
            {
                if (lb == null) return;
                if (lb.ItemContainerStyle != null && lb.ItemContainerStyle.IsSealed == false) return;

                //IsSealedが設定されているので、作り直す。
                var newStyle = new Style();

                //baseStyleに元のItemContainerStyle放り込むと一見簡単だが、ここはちゃんと内容を移す。
                if (lb.ItemContainerStyle != null)
                {
                    newStyle.TargetType = lb.ItemContainerStyle.TargetType;//余り意味は無いはずだが一応
                    newStyle.BasedOn = lb.ItemContainerStyle.BasedOn;//nullならそのままnullにしておく
                    newStyle.Resources = lb.ItemContainerStyle.Resources;
                    foreach (var item in lb.ItemContainerStyle.Setters) newStyle.Setters.Add(item);
                    foreach (var item in lb.ItemContainerStyle.Triggers) newStyle.Triggers.Add(item);
                }
                else
                {
                    newStyle.TargetType = GetListBoxItemType(lb);
                    //現在のContainerTypeを引っ張る。ただし、アプリケーションリソースでListViewItemが定義されている前提。
                    try { newStyle.BasedOn = (Style)lb.FindResource(newStyle.TargetType); }
                    catch { }
                }
                lb.ItemContainerStyle = newStyle;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        public static string WindowTitleText(string contentTitle, string baseTitle)
        {
            return (string.IsNullOrEmpty(contentTitle) == true ? "" : contentTitle + " - ") + baseTitle;
        }

        public static SelectionChangedEventHandler ListBox_TextBoxSyncSelectionChanged(ListBox lstBox, TextBox txtBox)
        {
            return new SelectionChangedEventHandler((sender, e) =>
            {
                if (lstBox == null || lstBox.SelectedItem == null || txtBox == null) return;
                //
                txtBox.Text = lstBox.SelectedItem.ToString();
            });
        }

        public static RoutedEventHandler OpenFolderNameDialog(TextBox box, string Description = "", bool checkNWPath = false, string defaultPath = "", bool recFile = false)
        {
            return (sender, e) => CommonManager.GetFolderNameByDialog(box, Description, checkNWPath, defaultPath, recFile);
        }
        public static RoutedEventHandler OpenFileNameDialog(TextBox box, bool isNameOnly, string Title = "", string DefaultExt = "", bool checkNWPath = false, string defaultPath = "", bool checkExist = true, bool recFile = false)
        {
            return (sender, e) => CommonManager.GetFileNameByDialog(box, isNameOnly, Title, DefaultExt, checkNWPath, defaultPath, checkExist, recFile);
        }

        public static RoutedEventHandler ListBox_TextCheckAdd(ListBox lstBox, TextBox txtBox, StringComparison type = StringComparison.OrdinalIgnoreCase)
        {
            return new RoutedEventHandler((sender, e) => ListBox_TextCheckAdd(lstBox, txtBox == null ? null : txtBox.Text, type));
        }
        public static bool ListBox_TextCheckAdd(ListBox lstBox, string text, StringComparison type = StringComparison.OrdinalIgnoreCase)
        {
            if (lstBox == null || string.IsNullOrEmpty(text) == true) return false;
            //
            var isAdd = lstBox.Items.OfType<object>().All(s => text.Equals(s.ToString(), type) == false);
            if (isAdd == true) lstBox.ScrollIntoViewLast(text);
            return isAdd;
        }

        public static void KeyDown_Escape_Close(object sender, KeyEventArgs e)
        {
            if (e.Handled == false && e.Key == Key.Escape && e.IsRepeat == false)
            {
                e.Handled = true;
                var win = CommonUtil.GetTopWindow(sender as Visual);
                if (win != null) win.Close();
            }
        }

        public static KeyEventHandler KeyDown_Enter(Button btn)
        {
            return new KeyEventHandler((sender, e) =>
            {
                if (e.Handled == false && Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Enter && e.IsRepeat == false)
                {
                    e.Handled = true;
                    if (btn != null) btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            });
        }

        public static void Set_ComboBox_LostFocus_SelectItemUInt(Panel panel)
        {
            Set_ComboBox_LostFocus_SelectItemUInt(panel.Children.OfType<ComboBox>().ToArray());
        }
        public static void Set_ComboBox_LostFocus_SelectItemUInt(params ComboBox[] radioButtonList)
        {
            foreach (var cmb in radioButtonList)
            {
                cmb.LostFocus += ComboBox_LostFocus_SelectUIntItem;
                cmb.LostKeyboardFocus += ComboBox_LostFocus_SelectUIntItem;
            }
        }
        private static void ComboBox_LostFocus_SelectUIntItem(object sender, RoutedEventArgs e)
        {
            var box = sender as ComboBox;
            uint val;
            uint.TryParse(box.Text, out val);
            box.Text = val.ToString();
            if (box.SelectedItem == null) box.SelectedIndex = 0;
        }

        ///<summary>同じアイテムがあってもスクロールするようにしたもの(ItemSource使用時無効)</summary>
        //ScrollIntoView()は同じアイテムが複数あると上手く動作しないので、ダミーを使って無理矢理移動させる。
        //同じ理由でSelectedItemも正しく動作しないので、スクロール位置はindexで取るようにする。
        public static void ScrollIntoViewIndex(this ListBox box, int index)
        {
            try
            {
                if (box == null || box.Items.Count == 0) return;

                index = Math.Min(Math.Max(0, index), box.Items.Count - 1);
                object item = box.Items[index];

                //リストに追加・削除をするので、ItemsSourceなどあるときは動作しない
                if (box.ItemsSource == null)
                {
                    if (box.Items.IndexOf(item) != index)
                    {
                        item = new ListBoxItem { Visibility = Visibility.Collapsed };
                        box.Items.Insert(index == 0 ? 0 : index + 1, item);

                        //ScrollIntoView()は遅延して実行されるので、実行後にダミーを削除する。
                        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => box.Items.Remove(item)), DispatcherPriority.Loaded);
                    }
                }

                box.ScrollIntoView(item);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }
        ///<summary>最後のアイテムを選択してスクロールさせる。addItemがあればリストに追加する。</summary>
        public static void ScrollIntoViewLast(this ListBox box, object addItem = null)
        {
            box.ScrollIntoViewLast(addItem.IntoList());
        }
        ///<summary>addItemsを追加し、最後のアイテムを選択してスクロールさせる</summary>
        public static void ScrollIntoViewLast(this ListBox box, IEnumerable<object> addItems)
        {
            addItems = (addItems ?? new List<object>()).Where(item => item != null).ToList();
            var boxItems = box.ItemsSource as IList ?? box.Items;
            foreach (var item in addItems) boxItems.Add(item);
            if (addItems.Any() == true && box.ItemsSource is IList) box.Items.Refresh();
            box.SelectedIndex = box.Items.Count - 1;
            box.ScrollIntoView(box.SelectedItem);
            box.SelectedItemsAdd(addItems);
        }

        public static DependencyObject GetPlacementItem(this ItemsControl lb, Point? pt = null)
        {
            if (lb == null) return null;
            var element = lb.InputHitTest((Point)(pt ?? Mouse.GetPosition(lb))) as DependencyObject;
            return element == null ? null : lb.ContainerFromElement(element);
        }

        public static int SingleWindowCheck(Type t, bool closeWindow = false)
        {
            var wList = Application.Current.Windows.OfType<Window>().Where(w => w.GetType() == t);
            foreach (var w in wList)
            {
                if (closeWindow == true)
                {
                    w.Close();
                }
                else
                {
                    if (w.WindowState == WindowState.Minimized)
                    {
                        w.WindowState = WindowState.Normal;
                    }
                    w.Visibility = Visibility.Visible;
                    w.Activate();
                }
            }
            return wList.Count();
        }

        public static TextBlock GetTooltipBlockStandard(string text)
        {
            var block = new TextBlock();
            block.Text = text;
            block.MaxWidth = Settings.Instance.ToolTipWidth;
            block.TextWrapping = TextWrapping.Wrap;
            return block;
        }

        public static void RenameHeader(this IEnumerable<GridViewColumn> list, string uid, object title, string tag = null)
        {
            foreach (var item in list)
            {
                var header = item.Header as GridViewColumnHeader;
                if (header != null && header.Uid == uid)
                {
                    header.Content = title;
                    if (tag != null) header.Tag = tag;
                }
            }
        }

        public static void AdjustWindowPosition(Window win)
        {
            foreach (var sc in System.Windows.Forms.Screen.AllScreens)
            {
                if (sc.WorkingArea.Contains((int)(win.Left + Math.Min(win.Width - 5, 50)), (int)(win.Top + Math.Min(win.Height - 5, 10))) == true)
                {
                    if (sc.WorkingArea.Contains((int)(win.Left + 100), (int)(win.Top + 100)) == true)
                    {
                        return;
                    }
                    break;
                }
            }
            win.Left = double.NaN;
            win.Top = double.NaN;
        }

        public static List<string> GetFolderList(ListBox box)
        {
            return box.Items.OfType<string>().Select(s => SettingPath.CheckFolder(s)).Where(s => s != "").ToList();
        }

        public static TextBlock GetPanelTextBlock(string s = null)
        {
            return new TextBlock
            {
                Text = s,
                TextAlignment = TextAlignment.Center,
                FontSize = 12,
            };
        }

        public static StackPanel ServiceHeaderToToolTip(StackPanel panel)
        {
            var tip = new StackPanel();
            foreach (TextBlock tb in panel.Children)
            {
                tip.Children.Add(GetPanelTextBlock(tb.Text));
            }
            return tip;
        }

        public static void TabControlHeaderCopy(TabControl src, TabControl trg)
        {
            trg.Items.Clear();
            foreach (var tb in src.Items.OfType<TabItem>()) trg.Items.Add(new TabItem { Header = tb.Header as string ?? tb.Tag as string });
        }
    }
}
