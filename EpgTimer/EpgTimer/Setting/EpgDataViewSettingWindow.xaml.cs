using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EpgTimer
{
    using BoxExchangeEdit;

    /// <summary>EpgDataViewSettingWindow.xaml の相互作用ロジック</summary>
    public partial class EpgDataViewSettingWindow : Window
    {
        private CustomEpgTabInfo info { get { return DataContext as CustomEpgTabInfo; } }
        private EpgSearchKeyInfo searchKey = new EpgSearchKeyInfo();

        public EpgDataViewSettingWindow(CustomEpgTabInfo setInfo = null, List<EpgSetting> setList = null)
        {
            InitializeComponent();

            DataContext = new CustomEpgTabInfo();

            button_ok.Click += new RoutedEventHandler((sender, e) => DialogResult = true);
            button_cancel.Click += new RoutedEventHandler((sender, e) => DialogResult = false);
            checkBox_tryEpgSetting.Visibility = Visibility.Hidden;

            comboBox_timeH_week.ItemsSource = Enumerable.Range(0, 24);

            foreach (var item in ChSet5.ChListSelected.Select(item => new ServiceViewItem(item)))
            {
                if (item.ServiceInfo.IsDttv) listBox_serviceDttv.Items.Add(item);
                if (item.ServiceInfo.IsBS) listBox_serviceBS.Items.Add(item);
                if (item.ServiceInfo.IsCS) listBox_serviceCS.Items.Add(item);
                if (item.ServiceInfo.IsSPHD) listBox_serviceSP.Items.Add(item);
                if (item.ServiceInfo.IsOther) listBox_serviceOther.Items.Add(item);
                listBox_serviceAll.Items.Add(item);
            }

            int i = 0;
            var spItems = Enum.GetValues(typeof(EpgServiceInfo.SpecialViewServices)).Cast<ulong>().Select(id => new ServiceViewItem(id)).ToList();
            var spItemsOther = spItems.ToList();
            foreach (TabItem tab in tab_ServiceList.Items.OfType<TabItem>().Take(4))
            {
                var listbox = (ListView)tab.Content;
                if (listbox.Items.Count != 0)
                {
                    listbox.Items.Insert(0, spItems[i]);
                    spItemsOther.Remove(spItems[i]);
                }
                tab.Visibility = listbox.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                i++;
            }
            listBox_serviceOther.Items.InsertItems(0, spItemsOther);

            listBox_jyanru.ItemsSource = CommonManager.ContentKindList;

            RadioButtonTagConverter.SetBindingButtons(CommonUtil.NameOf(() => new CustomEpgTabInfo().ViewMode), PanelDisplaySet);

            listBox_Button_Set();
            listBox_serviceView_ContextMenu_Set();

            SetSetting(setInfo ?? new CustomEpgTabInfo(), setList);
        }
        /// <summary>デフォルト表示の設定値</summary>
        public void SetSetting(CustomEpgTabInfo setInfo, List<EpgSetting> setList = null)
        {
            DataContext = setInfo.DeepClone();
            searchKey = setInfo.SearchKey.DeepClone();

            textBox_tabName.Text = setInfo.TabName;
            checkBox_isVisible.IsChecked = setInfo.IsVisible;

            SetRecSettingCmbo(setInfo.RecSetting);

            setList = setList ?? Settings.Instance.EpgSettingList;
            cmb_design.SelectedValuePath = CommonUtil.NameOf(() => setList[0].ID);
            cmb_design.DisplayMemberPath = CommonUtil.NameOf(() => setList[0].Name);
            cmb_design.ItemsSource = setList;
            cmb_design.SelectedIndex = setList.FindIndex(set => set.ID == setInfo.EpgSettingID);
            if (cmb_design.SelectedIndex < 0) cmb_design.SelectedIndex = 0;

            listBox_serviceView.Items.AddItems(setInfo.ViewServiceList.Select(id => new ServiceViewItem(id)));
            listBox_jyanruView.Items.AddItems(setInfo.ViewContentList.Select(data => CommonManager.ContentKindInfoForDisplay(data)));
            UpdateServiceViewItemMarks();
        }
        /// <summary>設定値の取得</summary>
        public CustomEpgTabInfo GetSetting()
        {
            info.TabName = textBox_tabName.IsEnabled == true ? textBox_tabName.Text : info.TabName;
            info.IsVisible = checkBox_isVisible.IsEnabled == true ? checkBox_isVisible.IsChecked == true : info.IsVisible;
            if (cmb_design.SelectedIndex >= 0)
            {
                info.EpgSettingIndex = cmb_design.SelectedIndex;
                info.EpgSettingID = (int)cmb_design.SelectedValue;
            }

            info.RecSetting = ((RecPresetItem)cmb_recSetting.SelectedItem).Data;
            if (info.RecSetting.EqualsSettingTo(Settings.Instance.RecPresetList[0].Data))
            {
                info.RecSetting = null;
            }

            info.SearchKey = searchKey.DeepClone();
            info.SearchKey.serviceList.Clear();//不要なので削除

            info.ViewServiceList = listBox_serviceView.Items.OfType<ServiceViewItem>().Select(item => item.Key).ToList();
            info.ViewContentList = listBox_jyanruView.Items.OfType<ContentKindInfo>().Select(item => item.Data).DeepClone();

            return info.DeepClone();
        }
        public void SetTryMode(bool tryOnly = false)
        {
            checkBox_tryEpgSetting.Visibility = Visibility.Visible;
            checkBox_tryEpgSetting.IsChecked = tryOnly == true || Settings.Instance.TryEpgSetting == true;
            if (tryOnly == true)
            {
                checkBox_tryEpgSetting.IsEnabled = false;
                checkBox_tryEpgSetting.ToolTip = "デフォルト表示では一時的な変更のみ可能で設定は保存されません。";
            }
            checkBox_isVisible.IsEnabled = checkBox_tryEpgSetting.IsChecked != true;
        }
        private void checkBox_tryEpgSetting_Click(object sender, RoutedEventArgs e)
        {
            //これはダイアログの設定なので即座に反映
            Settings.Instance.TryEpgSetting = (checkBox_tryEpgSetting.IsChecked == true);
            checkBox_isVisible.IsEnabled = checkBox_tryEpgSetting.IsChecked != true;
        }

        //サービス選択関係は他でも使用するので
        private BoxExchangeEditor bxs;

        private void listBox_Button_Set()
        {
            bxs = new BoxExchangeEditor(null, this.listBox_serviceView, true, true, true, true);
            bxs.ItemComparer = ServiceViewItem.Comparator;
            bxs.ItemsChanged += UpdateServiceViewItemMarks;

            chkBox_CombineProgram.Checked += (sender, e) => UpdateServiceViewItemMarks();
            chkBox_CombineProgram.Unchecked += (sender, e) => UpdateServiceViewItemMarks();

            //サービス選択関係はソースの ListView が複数あるので、全ての ListViewItem にイベントを追加する。
            foreach (TabItem tab in tab_ServiceList.Items)
            {
                var box = tab.Content as ListView;
                bxs.sourceBoxAllowCancelAction(box);
                bxs.sourceBoxAllowDragDrop(box);
                bxs.sourceBoxAllowKeyAction(box);
                bxs.sourceBoxAllowDoubleClick(box);
            }
            //ソースのリストボックスは複数あるので、リストボックスが選択されたときに SourceBox の登録を行う
            tab_ServiceList.SelectionChanged += (sender, e) =>
            {
                try { bxs.SourceBox = ((sender as TabControl).SelectedItem as TabItem).Content as ListView; }
                catch { bxs.SourceBox = null; }
            };
            button_service_addAll.Click += (sender, e) => button_AddAll();
            button_service_add.Click += new RoutedEventHandler(bxs.button_Add_Click);
            button_service_ins.Click += new RoutedEventHandler(bxs.button_Insert_Click);
            button_service_del.Click += new RoutedEventHandler(bxs.button_Delete_Click);
            button_service_delAll.Click += new RoutedEventHandler(bxs.button_DeleteAll_Click);
            button_service_top.Click += new RoutedEventHandler(bxs.button_Top_Click);
            button_service_up.Click += new RoutedEventHandler(bxs.button_Up_Click);
            button_service_down.Click += new RoutedEventHandler(bxs.button_Down_Click);
            button_service_bottom.Click += new RoutedEventHandler(bxs.button_Bottom_Click);

            //ジャンル選択関係
            var bxj = new BoxExchangeEditor(this.listBox_jyanru, this.listBox_jyanruView, true, true, true, true);
            button_jyanru_addAll.Click += new RoutedEventHandler(bxj.button_AddAll_Click);
            button_jyanru_add.Click += new RoutedEventHandler(bxj.button_Add_Click);
            button_jyanru_ins.Click += new RoutedEventHandler(bxj.button_Insert_Click);
            button_jyanru_del.Click += new RoutedEventHandler(bxj.button_Delete_Click);
            button_jyanru_delAll.Click += new RoutedEventHandler(bxj.button_DeleteAll_Click);
            button_jyanru_top.Click += new RoutedEventHandler(bxj.button_Top_Click);
            button_jyanru_up.Click += new RoutedEventHandler(bxj.button_Up_Click);
            button_jyanru_down.Click += new RoutedEventHandler(bxj.button_Down_Click);
            button_jyanru_bottom.Click += new RoutedEventHandler(bxj.button_Bottom_Click);
        }

        //特殊アイテムを全追加から外す
        private void button_AddAll()
        {
            bxs.SourceBox.SelectAll();
            bxs.bxAddItems(bxs.SourceBox.SelectedItemsList().Where(item => ((ServiceViewItem)item).ServiceInfo.HasSPKey == false), bxs.TargetBox);
        }

        List<Tuple<int, int>> sortList;
        private void listBox_serviceView_ContextMenu_Set()
        {
            // 右クリックメニューにSIDのソートを登録
            var cm = new ContextMenuEx();
            var menuItemAsc = new MenuItem();
            menuItemAsc.Header = "サブチャンネルの結合表示を解除";
            menuItemAsc.ToolTip = "同一TSIDのサービスの結合表示が解除されるようServiceIDを昇順に並び替えます";
            menuItemAsc.Tag = 0;
            cm.Items.Insert(0, menuItemAsc);
            var menuItemDesc = new MenuItem();
            menuItemDesc.Header = "サブチャンネルを番組表で結合表示";
            menuItemDesc.ToolTip = "同一TSIDのサービスをServiceIDが逆順になるよう並べると番組表で結合表示される機能を使い、\r\nサブチャンネルを含めて1サービスの幅で表示します";
            menuItemDesc.Tag = 1;
            cm.Items.Insert(0, menuItemDesc);
            foreach (MenuItem item in cm.Items)
            {
                item.Click += listBox_serviceView_SidSort;
            }
            listBox_serviceView.ContextMenu = cm;
            listBox_serviceView.ContextMenuOpening += listBox_serviceView_ContextMenu_Opening;
            listBox_serviceView.ContextMenuClosing += (s, e) => sortList = null;
        }

        private void listBox_serviceView_ContextMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            var grpDic = new Dictionary<int, Tuple<int, int>>();
            var grpDicAdd = new Action<int, int>((first, end) =>
            {
                for (int i = first; i <= end; i++) grpDic.Add(i, new Tuple<int, int>(first, end));
            });
            var grpDicRemove = new Action<int, int>((first, end) =>
            {
                for (int i = first; i <= end; i++) grpDic.Remove(i);
            });

            //並べ替え可能なグループを抽出
            int itemIndex = 0, firstTsidIndex = 0;
            for (; itemIndex < listBox_serviceView.Items.Count - 1; itemIndex++)
            {
                // 同一TSIDが連続する部分を選択中の中から探す(散らばっているTSIDはまとめない)
                var a = listBox_serviceView.Items[itemIndex] as ServiceViewItem;
                var b = listBox_serviceView.Items[itemIndex + 1] as ServiceViewItem;
                if (a.ServiceInfo.ONID == b.ServiceInfo.ONID && a.ServiceInfo.TSID == b.ServiceInfo.TSID) continue;

                // 見つかった場合 firstTsidIndex < itemIndex になる
                if (itemIndex != firstTsidIndex) grpDicAdd(firstTsidIndex, itemIndex);
                firstTsidIndex = itemIndex + 1;
            }
            if (itemIndex != firstTsidIndex) grpDicAdd(firstTsidIndex, itemIndex);

            // 対象があるときのみソート可能になるようにする
            sortList = new List<Tuple<int, int>>();
            foreach (var item in listBox_serviceView.SelectedItems)
            {
                Tuple<int, int> data;
                if (grpDic.TryGetValue(listBox_serviceView.Items.IndexOf(item), out data) == true)
                {
                    sortList.Add(data);
                    grpDicRemove(data.Item1, data.Item2);
                }
            }

            bool isSortable = sortList.Count != 0;
            ((MenuItem)listBox_serviceView.ContextMenu.Items[0]).IsEnabled = isSortable;
            ((MenuItem)listBox_serviceView.ContextMenu.Items[1]).IsEnabled = isSortable;

            if (isSortable == false) return;

            //全選択時以外は選択状態を変更する
            if (listBox_serviceView.Items.Count == listBox_serviceView.SelectedItems.Count) return;

            listBox_serviceView.UnselectAll();
            foreach (var data in sortList)
            {
                for (int i = data.Item1; i <= data.Item2; i++)
                {
                    listBox_serviceView.SelectedItems.Add(listBox_serviceView.Items[i]);
                }
            }
        }

        //残りの追加イベント
        /// <summary>映像のみ全追加</summary>
        private void button_service_addVideo_Click(object sender, RoutedEventArgs e)
        {
            ListBox listBox = bxs.SourceBox;
            if (listBox == null) return;

            listBox.UnselectAll();
            listBox.SelectedItemsAdd(listBox.Items.OfType<ServiceViewItem>().Where(info => info.ServiceInfo.IsVideo == true));
            button_service_add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private void listBox_serviceView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Display_ServiceView(listBox_serviceView, textBox_serviceView1);
        }

        private void listBox_service_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Display_ServiceView(bxs.SourceBox, textBox_serviceView2);
        }

        private void listBox_serviceView_SidSort(object sender, RoutedEventArgs e)
        {
            // 昇順・逆順用コンパレーター
            var comparerator = ((sender as MenuItem).Tag as int?) == 0 ?
                new Func<ushort, ushort, bool>((a, b) => a <= b) : new Func<ushort, ushort, bool>((a, b) => a >= b);

            // 実質、高々3つの並べ替えなので Bubble Sort で十分
            var sort = new Action<int, int>((start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    for (int j = i + 1; j <= end; j++)
                    {
                        if (comparerator((listBox_serviceView.Items[j] as ServiceViewItem).ServiceInfo.SID, (listBox_serviceView.Items[i] as ServiceViewItem).ServiceInfo.SID))
                        {
                            var tmp = listBox_serviceView.Items[i];
                            listBox_serviceView.Items[i] = listBox_serviceView.Items[j];
                            listBox_serviceView.Items[j] = tmp;
                        }
                    }
                }
            });

            //選択状態を保存
            var listKeeper = new ListViewSelectedKeeper(listBox_serviceView, true, item => (item as ServiceViewItem).Key);

            //ソート
            sortList.ForEach(data => sort(data.Item1, data.Item2));

            //選択状態を復元
            listKeeper.RestoreListViewSelected();

            //連結表示を更新
            UpdateServiceViewItemMarks();
        }

        private void UpdateServiceViewItemMarks()
        {
            foreach (ServiceViewItem item in listBox_serviceView.Items) item.CombineMark = "";

            if (info.CombineProgramByReverseSID)
            {
                var pMark = false;
                for (int idx = 0; idx < listBox_serviceView.Items.Count - 1; idx++)
                {
                    // 同一TSIDが連続する部分を選択中の中から探す
                    var a = listBox_serviceView.Items[idx] as ServiceViewItem;
                    var b = listBox_serviceView.Items[idx + 1] as ServiceViewItem;
                    var isMark = a.ServiceInfo.ONID == b.ServiceInfo.ONID && a.ServiceInfo.TSID == b.ServiceInfo.TSID && a.ServiceInfo.SID > b.ServiceInfo.SID;
                    if (isMark)
                    {
                        a.CombineMark = pMark ? "│" : "┌";
                        b.CombineMark = "└";
                    }
                    pMark = isMark;
                }
            }

            listBox_serviceView.Items.Refresh();
        }

        private void Display_ServiceView(ListBox srclistBox, TextBox targetBox)
        {
            if (srclistBox == null || targetBox == null) return;

            targetBox.Text = "";
            if (srclistBox.SelectedItem == null) return;

            var info = (ServiceViewItem)srclistBox.SelectedItems[srclistBox.SelectedItems.Count - 1];
            targetBox.Text = info.ConvertInfoText();
        }

        private void SetRecSettingCmbo(RecSettingData set = null)
        {
            cmb_recSetting.Items.Clear();
            cmb_recSetting.Items.AddItems(Settings.Instance.RecPresetList.DeepClone());

            var item = (set ?? Settings.Instance.RecPresetList[0].Data).LookUpPreset(cmb_recSetting.Items.OfType<RecPresetItem>());
            if (item == null)
            {
                item = new RecPresetItem("登録時", RecPresetItem.CustomID, set.DeepClone());
                cmb_recSetting.Items.Add(item);
            }
            cmb_recSetting.SelectedItem = item;
        }
        private void button_recSetting_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetRecPresetWindow(this);
            dlg.SetSettingMode("予約操作時の録画設定");
            dlg.DataView.SetDefSetting(GetSetting().RecSetting);
            if (dlg.ShowDialog() == true)
            {
                SetRecSettingCmbo(dlg.DataView.GetRecSetting());
            }
        }

        private void button_searchKey_Click(object sender, RoutedEventArgs e)
        {
            CustomEpgTabInfo tabInfo = GetSetting();

            var dlg = new SetSearchPresetWindow(this);
            dlg.SetSettingMode("検索条件");
            dlg.DataView.SetSearchKey(tabInfo.GetSearchKeyReloadEpg());
            if (dlg.ShowDialog() == true)
            {
                searchKey = dlg.DataView.GetSearchKey();

                //サービスリストは表示順を保持する
                var oldList = listBox_serviceView.Items.OfType<object>().ToList();
                var newList = searchKey.serviceList.Select(id => new ServiceViewItem((ulong)id)).ToList();
                listBox_serviceView.UnselectAll();
                listBox_serviceView.Items.RemoveItems(oldList.Where(sv => newList.Contains(sv, ServiceViewItem.Comparator) == false));
                listBox_serviceView.Items.AddItems(newList.Where(sv => oldList.Contains(sv, ServiceViewItem.Comparator) == false));

                //ジャンルリストの同期はオプションによる
                if (tabInfo.SearchGenreNoSyncView == false)
                {
                    var items = searchKey.contentList.Select(data => CommonManager.ContentKindInfoForDisplay(data));
                    listBox_jyanruView.Items.Clear();
                    listBox_jyanruView.Items.AddItems(items);
                    checkBox_notContent.IsChecked = searchKey.notContetFlag != 0;
                }
            }
        }
    }
}
