﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EpgTimer
{
    /// <summary>
    /// SearchWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SearchWindow : Window
    {
        private CtrlCmdUtil cmd = CommonManager.Instance.CtrlCmd;
        private MenuUtil mutil = CommonManager.Instance.MUtil;
        private ViewUtil vutil = CommonManager.Instance.VUtil;
        private MenuManager mm = CommonManager.Instance.MM;

        private CmdExeReserve mc; //予約系コマンド集
        private MenuBinds mBinds = new MenuBinds();

        private ListViewController<SearchItem> lstCtrl;

        public enum SearchMode { Find, NewAdd, Change }
        private SearchMode winMode = SearchMode.Find;
        public bool IsThisSubWindow { get { return this.Owner is SearchWindow; } }
        private static string subWindowString = "(サブウィンドウ)";
        
        private UInt32 autoAddID = 0;

        MainWindow mainWindow = null;
        EpgAutoAddView autoAddView = null;
        private bool ReloadInfo = false;
        private bool ReloadReserveInfo = false;

        public SearchWindow()
        {
            InitializeComponent();

            try
            {
                //比較その他でよく使う
                mainWindow = (MainWindow)Application.Current.MainWindow;
                autoAddView = mainWindow.autoAddView.epgAutoAddView;

                //リストビュー関連の設定
                lstCtrl = new ListViewController<SearchItem>(this);
                lstCtrl.SetInitialSortKey("StartTime");
                lstCtrl.SetViewSetting(listView_result, gridView_result, true);

                //最初にコマンド集の初期化
                mc = new CmdExeReserve(this);
                mc.SetFuncGetSearchList(isAll => (isAll == true ? lstCtrl.dataList.ToList() : lstCtrl.GetSelectedItemsList()));
                mc.SetFuncSelectSingleSearchData(lstCtrl.SelectSingleItem);
                mc.SetFuncReleaseSelectedData(() => listView_result.UnselectAll());
                mc.recSettingView = this.recSettingView;
                
                //コマンド集に無いもの
                mc.AddReplaceCommand(EpgCmds.ReSearch, mc_Research);
                mc.AddReplaceCommand(EpgCmds.ReSearch2, mc_Research);
                mc.AddReplaceCommand(EpgCmds.Search, (sender, e) => SearchPg());
                mc.AddReplaceCommand(EpgCmds.AddInDialog, button_add_epgAutoAdd_Click);
                mc.AddReplaceCommand(EpgCmds.ChangeInDialog, button_chg_epgAutoAdd_Click, (sender, e) => e.CanExecute = winMode == SearchMode.Change);
                mc.AddReplaceCommand(EpgCmds.DeleteInDialog, button_del_epgAutoAdd_Click, (sender, e) => e.CanExecute = winMode == SearchMode.Change);
                mc.AddReplaceCommand(EpgCmds.UpItem, (sender, e) => button_up_down_Click(-1), button_up_down_CanExecute);
                mc.AddReplaceCommand(EpgCmds.DownItem, (sender, e) => button_up_down_Click(1), button_up_down_CanExecute);
                mc.AddReplaceCommand(EpgCmds.Cancel, (sender, e) => this.Close());

                //コマンド集を振り替えるもの
                mc.AddReplaceCommand(EpgCmds.JumpReserve, (sender, e) => mc_JumpTab(CtxmCode.ReserveView, true));
                mc.AddReplaceCommand(EpgCmds.JumpTuner, (sender, e) => mc_JumpTab(CtxmCode.TunerReserveView, true, Settings.Instance.TunerDisplayOffReserve == false));
                mc.AddReplaceCommand(EpgCmds.JumpTable, (sender, e) => mc_JumpTab(CtxmCode.EpgView));

                //コマンド集からコマンドを登録。
                mc.ResetCommandBindings(this, listView_result.ContextMenu);

                //コンテキストメニューを開く時の設定
                lstCtrl.SetCtxmTargetSave(listView_result.ContextMenu);//こっちが先
                listView_result.ContextMenu.Opened += new RoutedEventHandler(mc.SupportContextMenuLoading);

                //ボタンの設定
                mBinds.View = CtxmCode.SearchWindow;
                mBinds.SetCommandToButton(button_search, EpgCmds.Search);
                mBinds.SetCommandToButton(button_add_reserve, EpgCmds.Add);
                mBinds.SetCommandToButton(button_delall_reserve, EpgCmds.DeleteAll);
                mBinds.SetCommandToButton(button_add_epgAutoAdd, EpgCmds.AddInDialog);
                mBinds.SetCommandToButton(button_chg_epgAutoAdd, EpgCmds.ChangeInDialog);
                mBinds.SetCommandToButton(button_del_epgAutoAdd, EpgCmds.DeleteInDialog);
                mBinds.SetCommandToButton(button_up_epgAutoAdd, EpgCmds.UpItem);
                mBinds.SetCommandToButton(button_down_epgAutoAdd, EpgCmds.DownItem);
                mBinds.AddInputCommand(EpgCmds.Cancel);//ショートカット登録

                //メニューの作成、ショートカットの登録
                RefreshMenu();

                //その他のショートカット(検索ダイアログ固有の設定)
                searchKeyView.InputBindings.Add(new InputBinding(EpgCmds.Search, new KeyGesture(Key.Enter)));

                //ウインドウ位置の復元
                if (Settings.Instance.SearchWndTop != 0)
                {
                    this.Top = Settings.Instance.SearchWndTop;
                }
                if (Settings.Instance.SearchWndLeft != 0)
                {
                    this.Left = Settings.Instance.SearchWndLeft;
                }
                if (Settings.Instance.SearchWndWidth != 0)
                {
                    this.Width = Settings.Instance.SearchWndWidth;
                }
                if (Settings.Instance.SearchWndHeight != 0)
                {
                    this.Height = Settings.Instance.SearchWndHeight;
                }

                SetSearchKey(Settings.Instance.DefSearchKey);

                //notify残ってれば更新。通常残ってないはず。
                vutil.ReloadReserveData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }
        public static void RefreshMenu(Window owner_win)
        {
            foreach (SearchWindow win in owner_win.OwnedWindows.OfType<SearchWindow>())
            {
                SearchWindow.RefreshMenu(win);
                win.RefreshMenu();
            }
        }
        public void RefreshMenu()
        {
            mBinds.ResetInputBindings(this, listView_result);
            mm.CtxmGenerateContextMenu(listView_result.ContextMenu, CtxmCode.SearchWindow, true);
        }

        public EpgSearchKeyInfo GetSearchKey()
        {
            return searchKeyView.GetSearchKey();
        }

        public void SetSearchKey(EpgSearchKeyInfo key)
        {
            searchKeyView.SetSearchKey(key);
        }

        public RecSettingData GetRecSetting()
        {
            return recSettingView.GetRecSetting();
        }

        public void SetRecSetting(RecSettingData set)
        {
            recSettingView.SetDefSetting(set);
        }

        public EpgAutoAddData GetAutoAddData()
        {
            var data = new EpgAutoAddData();
            data.dataID = autoAddID;
            data.searchInfo = GetSearchKey();
            data.recSetting = GetRecSetting();
            return data;
        }

        public void SetAutoAddData(EpgAutoAddData data)
        {
            autoAddID = data.dataID;
            searchKeyView.SetSearchKey(data.searchInfo);
            recSettingView.SetDefSetting(data.recSetting);
        }

        private void ChangeAutoAddData(EpgAutoAddData data, bool refresh = true)
        {
            this.SetViewMode(SearchMode.Change);
            this.SetAutoAddData(data);
            this.UpdateEpgAutoAddViewSelection();
            if (refresh == true) UpdateInfo();
        }

        public void SetViewMode(SearchMode md)
        {
            winMode = md;
            if (winMode == SearchMode.Find)
            {
                Title = "検索";
                button_chg_epgAutoAdd.Visibility = Visibility.Hidden;
                button_del_epgAutoAdd.Visibility = Visibility.Hidden;
                button_up_epgAutoAdd.Visibility = Visibility.Hidden;
                button_down_epgAutoAdd.Visibility = Visibility.Hidden;
            }
            else if (winMode == SearchMode.NewAdd)
            {
                Title = "キーワード自動予約登録";
                button_chg_epgAutoAdd.Visibility = Visibility.Hidden;
                button_del_epgAutoAdd.Visibility = Visibility.Hidden;
                button_up_epgAutoAdd.Visibility = Visibility.Visible;
                button_down_epgAutoAdd.Visibility = Visibility.Visible;
            }
            else if (winMode == SearchMode.Change)
            {
                Title = "キーワード自動予約登録";
                button_chg_epgAutoAdd.Visibility = Visibility.Visible;
                button_del_epgAutoAdd.Visibility = Visibility.Visible;
                button_up_epgAutoAdd.Visibility = Visibility.Visible;
                button_down_epgAutoAdd.Visibility = Visibility.Visible;
            }
            if (IsThisSubWindow == true)
            {
                Title += (Title.EndsWith(subWindowString) == true ? "" : subWindowString);
            }
        }

        private void SearchPg()
        {
            lstCtrl.ReloadInfoData(dataList =>
            {
                EpgSearchKeyInfo key = GetSearchKey();
                key.keyDisabledFlag = 0; //無効解除
                var list = new List<EpgEventInfo>();

                cmd.SendSearchPg(CommonUtil.ToList(key), ref list);

                lstCtrl.dataList.AddFromEventList(list, false, true);

                searchKeyView.AddSearchLog();
                return true;
            });

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            text_result.Text = string.Format("検索数:{0}  ", lstCtrl.dataList.Count);
            List<ReserveData> rlist = lstCtrl.dataList.GetReserveList();
            if (rlist.Count != 0)
            {
                int OnCount = rlist.Count(data => data.RecSetting.RecMode != 5);
                int OffCount = rlist.Count - OnCount;
                text_result.Text += string.Format("予約数:{0} ( 有効 {1} / 無効 {2} )", rlist.Count, OnCount, OffCount);
            }
        }

        private void ReloadReserveData()
        {
            mutil.SetSearchItemReserved(lstCtrl.dataList);
            this.listView_result.Items.Refresh();
            RefreshStatus();
        }

        private void button_add_epgAutoAdd_Click(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (CheckAutoAddChange(e, 0) == false) return;

                //一覧画面非表示の状態から実施する場合のためのコード
                if (autoAddView.IsVisible == false && CommonManager.Instance.DB.EpgAutoAddList.Count == 0)
                {
                    CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddEpgInfo);
                    CommonManager.Instance.DB.ReloadEpgAutoAddInfo();
                }

                List<uint> oldlist = CommonManager.Instance.DB.EpgAutoAddList.Keys.ToList();

                if (mutil.EpgAutoAddAdd(CommonUtil.ToList(this.GetAutoAddData())) == true)
                {
                    //以降の処理をEpgTimerSrvからの更新通知後に実行すればReload減らせるが、トラブル増えそうなのでこのまま。
                    CommonManager.Instance.DB.SetUpdateNotify((UInt32)UpdateNotifyItem.AutoAddEpgInfo);
                    CommonManager.Instance.DB.ReloadEpgAutoAddInfo();
                    
                    List<uint> newlist = CommonManager.Instance.DB.EpgAutoAddList.Keys.ToList();
                    List<uint> diflist = newlist.Except(oldlist).ToList();

                    if (diflist.Count == 1)
                    {
                        ChangeAutoAddData(CommonManager.Instance.DB.EpgAutoAddList[diflist[0]], false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button_chg_epgAutoAdd_Click(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (CheckAutoAddChange(e, 1) == false) return;

                mutil.EpgAutoAddChange(CommonUtil.ToList(this.GetAutoAddData()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void button_del_epgAutoAdd_Click(object sender, ExecutedRoutedEventArgs e)
        {
            if (CheckAutoAddChange(e, 2) == false) return;

            if (mutil.EpgAutoAddDelete(CommonUtil.ToList(autoAddID)) == true)
            {
                SetViewMode(SearchMode.NewAdd);
                this.autoAddID = 0;
            }
        }

        //proc 0:追加、1:変更、2:削除
        private bool CheckAutoAddChange(ExecutedRoutedEventArgs e, int proc)
        {
            if (CmdExeUtil.IsDisplayKgMessage(e) == true)
            {
                var strMode = new string[] { "追加", "変更", "削除" }[proc];
                if (MessageBox.Show("自動予約登録を" + strMode + "します。\r\nよろしいですか？", strMode + "の確認", MessageBoxButton.OKCancel) != MessageBoxResult.OK)
                { return false; }
            }

            //データの更新
            SearchPg();

            if (proc != 0)
            {
                if (CommonManager.Instance.DB.EpgAutoAddList.ContainsKey(this.autoAddID) == false)
                {
                    MessageBox.Show("項目がありません。\r\n" + "既に削除されています。\r\n" + "(別のEpgtimerによる操作など)");
                    SetViewMode(SearchMode.NewAdd);
                    this.autoAddID = 0;
                    return false;
                }
            }

            if (proc != 2 && Settings.Instance.CautionManyChange == true && searchKeyView.searchKeyDescView.checkBox_keyDisabled.IsChecked != true)
            {
                if (mutil.CautionManyMessage(lstCtrl.dataList.GetNoReserveList().Count, "予約追加の確認") == false)
                { return false; }
            }

            return true;
        }

        private void button_up_down_Click(int direction)
        {
            ListView list = autoAddView.listView_key;

            if (list.Items.Count == 0) return;//ここには引っかからないはずだけど一応チェック入れておく。

            this.UpdateEpgAutoAddViewSelection();
            if (list.SelectedIndex == -1)
            {
                list.SelectedIndex = direction >= 0 ? 0 : list.Items.Count - 1;
            }
            else
            {
                list.SelectedIndex = ((list.SelectedIndex + direction) % list.Items.Count + list.Items.Count) % list.Items.Count;
            }

            ChangeAutoAddData((list.SelectedItem as EpgAutoDataItem).EpgAutoAddInfo);
        }

        private void button_up_down_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = winMode != SearchMode.Find //1行目、ボタンは非表示だがショートカットキーを抑制する。
                            && autoAddView.IsVisible == true
                            && autoAddView.listView_key.Items.Count != 0;
        }

        private void listView_result_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EpgCmds.ShowDialog.Execute(sender, null);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                if (this.Visibility == Visibility.Visible && this.Width > 0 && this.Height > 0)
                {
                    Settings.Instance.SearchWndWidth = this.Width;
                    Settings.Instance.SearchWndHeight = this.Height;
                }
            }
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                if (this.Visibility == Visibility.Visible && this.Top > 0 && this.Left > 0)
                {
                    Settings.Instance.SearchWndTop = this.Top;
                    Settings.Instance.SearchWndLeft = this.Left;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if ((winMode == SearchMode.Find || winMode == SearchMode.NewAdd) && string.IsNullOrEmpty(searchKeyView.ComboBox_andKey.Text))
            {
                this.searchKeyView.ComboBox_andKey.Focus();
            }
            else
            {
                this.SearchPg();
            }
        }

        private void mc_JumpTab(CtxmCode code, bool reserveOnly = false, bool onReserveOnly = false)
        {
            if (IsThisSubWindow == false && listView_result.SelectedItem != null)
            {
                SearchItem item = lstCtrl.SelectSingleItem();

                if (reserveOnly && item.IsReserved == false) return;
                if (onReserveOnly && item.ReserveInfo.RecSetting.RecMode == 5) return;

                BlackoutWindow.SelectedItem = item;

                this.Hide();
                mainWindow.SetHideSearchWindow(this);
                mainWindow.moveTo_tabItem(code);
                mainWindow.Hide(); // UserControl_IsVisibleChangedイベントを発生させる
                mainWindow.Show();
            }
        }

        private void mc_Research(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (listView_result.SelectedItem != null && !(e.Command == EpgCmds.ReSearch2 && IsThisSubWindow == true))
                {
                    SearchItem item = lstCtrl.SelectSingleItem();

                    EpgSearchKeyInfo defKey = GetSearchKey();
                    defKey.andKey = mutil.TrimEpgKeyword(item.EventName, CmdExeUtil.IsKeyGesture(e));
                    defKey.regExpFlag = 0;
                    defKey.serviceList.Clear();
                    UInt64 sidKey = item.EventInfo.Create64Key();
                    defKey.serviceList.Add((Int64)sidKey);

                    if (e.Command == EpgCmds.ReSearch)
                    {
                        SetSearchKey(defKey);
                        SearchPg();
                    }
                    else
                    {
                        //「番組名で再検索」と比べてどうなのという感じだが、元の検索を残したまま作業できる
                        //新番組チェックなんかには向いてるかもしれないが、機能としては微妙なところ。

                        var dlg = new SearchWindow();
                        //SearchWindowからの呼び出しを記録する。表示制御などでも使う。
                        dlg.Owner = this;
                        dlg.SetViewMode(winMode == SearchMode.Change ? SearchMode.NewAdd : winMode);
                        if (Settings.Instance.MenuSet.CancelAutoAddOff == true)
                        {
                            defKey.keyDisabledFlag = 0;
                        }
                        dlg.SetSearchKey(defKey);
                        dlg.SetRecSetting(this.GetRecSetting());
                        dlg.Left += 50;
                        dlg.Top += 25;
                        dlg.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            Settings.SaveToXmlFile();//検索ワードリストの保存
            if (IsThisSubWindow == false)
            {
                mainWindow.ListFoucsOnVisibleChanged();
            }
        }

        public static void UpdateInfo(Window owner_win, bool refreshOnly = false)
        {
            foreach (SearchWindow win in owner_win.OwnedWindows.OfType<SearchWindow>())
            {
                SearchWindow.UpdateInfo(win, refreshOnly);
                win.UpdateInfo(refreshOnly);
            }
        }
        public void UpdateInfo(bool refreshOnly = false)
        {
            if (refreshOnly == false)
            {
                ReloadInfo = true;
            }
            ReloadReserveInfo = true;
            ReloadInfoData();
        }
        private void Window_Activated(object sender, EventArgs e)
        {
            UpdateEpgAutoAddViewSelection();
            ReloadInfoData();
        }
        private void ReloadInfoData()
        {
            //再検索はCtrlCmdを使うので、アクティブウィンドウでだけ実行させる。
            if (this.IsActive == true)
            {
                if (ReloadInfo == true)
                {
                    SearchPg();
                    ReloadInfo = false;
                    ReloadReserveInfo = false;
                }
            }
            //表示の更新は見えてれば実行する。
            if (this.IsVisible == true)
            {
                if (ReloadReserveInfo == true)
                {
                    ReloadReserveData();
                    ReloadReserveInfo = false;
                }
            }
        }
        public static bool UpdateEpgAutoAddViewSelection(Window owner_win)
        {
            foreach (SearchWindow win in owner_win.OwnedWindows.OfType<SearchWindow>())
            {
                if (SearchWindow.UpdateEpgAutoAddViewSelection(win) == true) return true;
                if (win.IsActive == true)
                {
                    win.UpdateEpgAutoAddViewSelection();
                    return true;
                }
            }
            return false;
        }
        private void UpdateEpgAutoAddViewSelection()
        {
            autoAddView.UpdateListViewSelection(this.autoAddID);
        }

    }
}
