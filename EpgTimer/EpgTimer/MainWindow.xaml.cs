using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Threading; //紅
using Microsoft.Win32;

namespace EpgTimer
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, ITaskTrayClickHandler
    {
        private Dictionary<string, Button> buttonList = new Dictionary<string, Button>();
        private MenuBinds mBinds = new MenuBinds();
        private DispatcherTimer chkTimer = null;

        //MainWindowにIDisposableを実装するべき？
        private Mutex mutex;
        private string mutexName;
        private NWConnect nwConnect = new NWConnect();
        private PipeServer pipeServer = null;
        private bool closeFlag = false;
        private bool? minimizedStarting = false;

        public MainWindow()
        {
#if NETCOREAPP
            //Encoding.GetEncoding()でCP932を使うため
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            string appName = Path.GetFileNameWithoutExtension(SettingPath.ModuleName);
#if DEBUG
            appName += "(debug)";
#endif
            CommonManager.Instance.NWMode = appName.StartsWith("EpgTimerNW", StringComparison.OrdinalIgnoreCase)
                            || File.Exists(Path.Combine(SettingPath.ModulePath, "EpgTimerSrv.exe")) == false;

            Settings.LoadFromXmlFile(CommonManager.Instance.NWMode);
            CommonManager.Instance.NWMode |= Settings.Instance.ForceNWMode;
            IniFileHandler.ReadOnly = CommonManager.Instance.NWMode;

            CommonManager.Instance.MM.ReloadWorkData();
            Settings.Instance.LoadIniOptions();

            if (CheckCmdLine() && Settings.Instance.ExitAfterProcessingArgs)
            {
                Environment.Exit(0);
            }
            mutexName = (CommonManager.Instance.NWMode ? "NW" : "2") +
                        (appName.StartsWith("EpgTimer", StringComparison.OrdinalIgnoreCase) ? appName.Substring(8).ToUpperInvariant() : "");
            mutex = new Mutex(false, "Global\\EpgTimer_Bon" + mutexName);
            if (!mutex.WaitOne(0, false))
            {
                mutex.Close();
                Environment.Exit(0);
            }

            if (Settings.AppResourceDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Add(Settings.AppResourceDictionary);
            }

            // レイアウト用のスタイルをロード
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Style/UiLayoutStyles.xaml") });

            //ツールチップの表示時間を30秒、Disableでも表示するように変更
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(true));
            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(30000));

            //オリジナルのmutex名をもつEpgTimerか
            if (mutexName == "2")
            {
                try
                {
                    using (Mutex.OpenExisting("Global\\EpgTimer_Bon_Service")) { }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    //二重起動抑止Mutexが存在しないのでEpgTimerSrvがあれば起動する
                    string exePath = Path.Combine(SettingPath.ModulePath, "EpgTimerSrv.exe");
                    if (File.Exists(exePath))
                    {
                        try
                        {
                            using (Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = false })) { }
                        }
                        catch
                        {
                            MessageBox.Show("EpgTimerSrv.exeの起動ができませんでした");
                            mutex.ReleaseMutex();
                            mutex.Close();
                            Environment.Exit(0);
                        }
                        //EpgTimerSrvを自分で起動させた場合、後でUpdateNotifyItem.EpgDataが来るので、初期フラグをリセットする。
                        CommonManager.Instance.WaitingSrvReady = true;
                        CommonManager.Instance.DB.ResetUpdateNotify(UpdateNotifyItem.EpgData);
                    }
                }
                catch { }
            }

            InitializeComponent();

            Title = appName + (CommonManager.Instance.NWMode == true ? " - NW Mode" : "");

            try
            {
                //ウインドウ位置の復元
                Settings.Instance.WndSettings.SetSizeToWindow(this);
                ViewUtil.AdjustWindowPosition(this);

                if (Settings.Instance.WakeMin == true)
                {
                    minimizedStarting = true;
                    this.WindowState = WindowState.Minimized;
                    if (Settings.Instance.ShowTray && Settings.Instance.MinHide && Settings.Instance.WakeMinTraySilent)
                    {
                        this.Visibility = Visibility.Hidden;
                    }
                }

                //ステータスバーの登録
                StatusManager.RegisterStatusbar(this.statusBar, this);

                //上のボタン
                Action<string, Action> ButtonGen = (key, handler) =>
                {
                    Button btn = new Button();
                    btn.MinWidth = 75;
                    btn.Margin = new Thickness(2, 2, 2, 5);
                    btn.Click += (sender, e) => handler();
                    btn.Content = key;
                    buttonList.Add(key, btn);
                };
                ButtonGen("設定", () => OpenSettingDialog());
                ButtonGen("再接続", OpenConnectDialog);
                ButtonGen("再接続(前回)", () => ConnectCmd());
                ButtonGen("検索", OpenSearchDialog);
                ButtonGen("予約情報検索", OpenInfoSearchDialog);
                ButtonGen("スタンバイ", () => SuspendCmd(1));
                ButtonGen("休止", () => SuspendCmd(2));
                ButtonGen("終了", CloseCmd);
                ButtonGen("EPG取得", EpgCapCmd);
                ButtonGen("EPG再読み込み", EpgReloadCmd);
                ButtonGen("NetworkTV終了", NwTVEndCmd);
                ButtonGen("情報通知ログ", OpenNotifyLogDialog);
                ButtonGen("カスタム１", () => CustumCmd(1));
                ButtonGen("カスタム２", () => CustumCmd(2));
                ButtonGen("カスタム３", () => CustumCmd(3));

                //登録したボタン名の保存
                Settings.ResisterViewButtonIDs(buttonList.Keys);

                //検索ボタンは他と共通でショートカット割り振られているので、その部分はコマンド側で処理する。
                this.CommandBindings.Add(new CommandBinding(EpgCmds.Search, (sender, e) => CommonButtons_Click("検索")));
                this.CommandBindings.Add(new CommandBinding(EpgCmds.InfoSearch, (sender, e) => CommonButtons_Click("予約情報検索")));
                mBinds.AddInputCommand(EpgCmds.Search);
                mBinds.AddInputCommand(EpgCmds.InfoSearch);
                SetSearchButtonTooltip(buttonList["検索"]);
                SetInfoSearchButtonTooltip(buttonList["予約情報検索"]);

                //EpgDataは遅延実行される場合があるので、データ取得後の処理を登録
                CommonManager.Instance.DB.DBChanged[UpdateNotifyItem.EpgData] = () => MainProc(MainProcItem.EpgDataLoaded);
                CommonManager.Instance.DB.DBChanged[UpdateNotifyItem.EpgDataAdd] = () => MainProc(MainProcItem.EpgDataAddLoaded);

                //ロゴは読み込みがゆっくりなので更新用の設定。番組表表示に時間がかかっているときなどは間引かれる。
                ChSet5.LogoChanged += () => MainProc(MainProcItem.ChSet5Logo, () => epgView.UpdateLog());

                if (CommonManager.Instance.NWMode == false)
                {
                    int pid;
                    using (var process = Process.GetCurrentProcess())
                    {
                        pid = process.Id;
                    }
                    //コールバックは別スレッドかもしれないので設定は予めキャプチャする
                    uint execBat = Settings.Instance.ExecBat;
                    pipeServer = new PipeServer("Global\\EpgTimerGUI_Ctrl_BonConnect_" + pid,
                                                "EpgTimerGUI_Ctrl_BonPipe_" + pid,
                                                (c, r) => OutsideCmdCallback(c, r, false, execBat));

                    for (int i = 0; i < 150 && CommonManager.CreateSrvCtrl().SendRegistGUI((uint)pid) != ErrCode.CMD_SUCCESS; i++)
                    {
                        Thread.Sleep(100);
                    }

                    //TSIDの変更チェック
                    WakeCheckService();

                    //予約一覧の表示に使用したりするのであらかじめ読込んでおく(暫定処置)
                    CommonManager.Instance.DB.ReloadReserveInfo(true);
                    CommonManager.Instance.DB.ReloadEpgAutoAddInfo(true);
                    CommonManager.Instance.DB.ReloadManualAutoAddInfo(true);
                    CommonManager.Instance.DB.ReloadEpgDatabaseInfo(true);
                    if (Settings.Instance.NgAutoEpgLoadNW == false)
                    {
                        CommonManager.Instance.DB.ReloadEpgData(false, true);
                        Dispatcher.BeginInvoke(new Action(() => epgView.UpdateInfo()), DispatcherPriority.Loaded);
                    }
                    Dispatcher.BeginInvoke(new Action(() => UpdateReserveTab()), DispatcherPriority.Loaded);
                }

                ResetMainView();

                //番組表タブをタブ切り替えではなく非表示で切り替え、表示用の番組表ストックを維持する
                this.tabControl_main.SelectionChanged += (sender, e) =>
                {
                    epgView.Visibility = tabItem_epg.IsSelected == true ? Visibility.Visible : Visibility.Hidden;

                    //キー操作などによるタブ風ボタンエリアへの進入防止
                    if (tabControl_main.SelectedItem == null || (tabControl_main.SelectedItem as TabItem).Tag as string != specific) return;
                    (e.RemovedItems[0] as TabItem == tabItem_reserve ? tabItem_epg : tabItem_reserve).IsSelected = true;
                };

                //自動登録の個別タブに番組表設定画面を出すコンテキストメニューを表示する
                tabItem_AutoAdd.MouseRightButtonUp += autoAddView.TabContextMenuOpen;

                //番組表タブに番組表設定画面を出すコンテキストメニューを表示する
                tabItem_epg.MouseRightButtonUp += epgView.EpgTabContextMenuOpen;

                //その他のタブに設定画面を出すコンテキストメニューを表示する
                tabControl_main.MouseRightButtonUp += (sender, e) =>
                {
                    var tab = tabControl_main.GetPlacementItem() as TabItem;
                    if (tab == null) return;

                    e.Handled = true;
                    var mode = tab == tabItem_reserve ? SettingWindow.SettingMode.ReserveSetting :
                    tab == tabItem_tunerReserve ? SettingWindow.SettingMode.TunerSetting :
                    tab == tabItem_recinfo ? SettingWindow.SettingMode.RecInfoSetting :
                    tab == tabItem_AutoAdd ? SettingWindow.SettingMode.ReserveSetting :
                    //tab == tabItem_epg ? SettingWindow.SettingMode.EpgSetting :
                    SettingWindow.SettingMode.Default;

                    var menuSet = new MenuItem { Header = (tab.Header as string ?? tab.Tag as string) + "の画面設定(_O)..." };
                    menuSet.Click += (s2, e2) => OpenSettingDialog(mode);
                    var ctxm = new ContextMenuEx { IsOpen = true };
                    ctxm.Items.Add(menuSet);

                    //チューナー不足時の追加メニュー
                    if (tab == tabItem_reserve || tab == tabItem_tunerReserve)
                    {
                        var menuSearch = new MenuItem { Header = "チューナー不足予約一覧(_L)..." };
                        menuSearch.Click += (s2, e2) => new InfoSearchWindow(new InfoSearchSettingData()
                        { SearchWord = "エラー状況 : *チューナー不足(", ReserveInfo = true }, true).Show();
                        menuSearch.IsEnabled = res_icon_Error.Visibility == Visibility.Visible || res_icon_Warning.Visibility == Visibility.Visible;
                        ctxm.Items.Add(new Separator() { IsEnabled = menuSearch.IsEnabled });
                        ctxm.Items.Add(menuSearch);
                    }
                };

                //初期タブ選択
                switch (Settings.Instance.StartTab)
                {
                    //case CtxmCode.ReserveView:
                    //    this.tabItem_reserve.IsSelected = true;
                    //    break;
                    case CtxmCode.TunerReserveView:
                        this.tabItem_tunerReserve.IsSelected = true;
                        break;
                    case CtxmCode.RecInfoView:
                        this.tabItem_recinfo.IsSelected = true;
                        break;
                    case CtxmCode.EpgAutoAddView:
                        this.tabItem_AutoAdd.IsSelected = true;
                    //    this.autoAddView.tabItem_epgAutoAdd.IsSelected = true;
                        break;
                    case CtxmCode.ManualAutoAddView:
                        this.tabItem_AutoAdd.IsSelected = true;
                        this.autoAddView.tabItem_manualAutoAdd.IsSelected = true;
                        break;
                    case CtxmCode.EpgView:
                        this.tabItem_epg.IsSelected = true;
                        break;
                }

                //自動接続ならWindowLoadedしない場合でも接続させる
                if (Settings.Instance.WakeReconnectNW == true)
                {
                    Dispatcher.BeginInvoke(new Action(() => ConnectCmd()), DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        private static bool CheckCmdLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length <= 1)
            {
                //引数なし
                return false;
            }
            var cmd = new CtrlCmdUtil();
            if (CommonManager.Instance.NWMode)
            {
                cmd.SetSendMode(true);
                bool connected = false;
                try
                {
                    //IPv4の名前解決を優先する
                    foreach (var address in System.Net.Dns.GetHostAddresses(Settings.Instance.NWServerIP).OrderBy(a => a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
                    {
                        cmd.SetNWSetting(address, Settings.Instance.NWServerPort);
                        byte[] binData;
                        if (cmd.SendFileCopy("ChSet5.txt", out binData) == ErrCode.CMD_SUCCESS)
                        {
                            connected = ChSet5.LoadWithStreamReader(new MemoryStream(binData));
                            break;
                        }
                    }
                }
                catch { }
                if (connected == false)
                {
                    MessageBox.Show("EpgTimerSrvとの接続に失敗しました。EpgTimerNWの接続設定を確認してください。");
                    return true;
                }
            }
            else
            {
                byte[] binData;
                if (cmd.SendFileCopy("ChSet5.txt", out binData) != ErrCode.CMD_SUCCESS ||
                    ChSet5.LoadWithStreamReader(new MemoryStream(binData)) == false)
                {
                    MessageBox.Show("EpgTimerSrvとの接続に失敗しました。");
                    return true;
                }
            }
            SendAddReserveFromArgs(cmd, args.Skip(1));
            return true;
        }
        private void ResetMainView()
        {
            RefreshMenu();          //右クリックメニューの更新
            ResetButtonView();      //上部ボタンの更新
            ResetViewButtonColumn();//一覧部分ボタン表示の更新
            StatusbarReset();       //ステータスバーリセット
            ResetTaskMenu();        //タスクバーのリセット
            ChkTimerWork();         //タスクツールチップ、接続維持用タイマーリセット

            //その他更新
            tunerReserveView.RefreshView();
        }

        private void ResetViewButtonColumn()
        {
            var ToVisibility = new Func<bool, Visibility>(v => v == true ? Visibility.Visible : Visibility.Collapsed);
            Dock dock = Settings.Instance.MainViewButtonsDock;
            bool IsVertical = dock == Dock.Right || dock == Dock.Left;
            var panel_margin = new Dictionary<Dock, Thickness> {
                    { Dock.Top, new Thickness(0, 0, 0, 6) },{ Dock.Bottom, new Thickness(12, 6, 0, 0) },
                    { Dock.Left, new Thickness(0, 12, 6, 0) },{ Dock.Right, new Thickness(6, 12, 0, 0) }}[dock];

            var SetButtonsPanel = new Action<StackPanel, bool>((panel, pnlHide) =>
            {
                DockPanel.SetDock(panel, dock);
                panel.Visibility = ToVisibility(!pnlHide);
                panel.Orientation = IsVertical ? Orientation.Vertical : Orientation.Horizontal;
                panel.Margin = panel_margin;
                foreach (var btn in panel.Children.OfType<Button>())
                {
                    btn.MinWidth = 75;
                    btn.Margin = IsVertical ? new Thickness(0, 0, 0, 10) : new Thickness(0, 0, 12, 0);
                }
            });
            SetButtonsPanel(reserveView.stackPanel_button, Settings.Instance.ResHideButton);
            SetButtonsPanel(recInfoView.stackPanel_button, Settings.Instance.RecInfoHideButton);
            SetButtonsPanel(autoAddView.epgAutoAddView.stackPanel_button, Settings.Instance.AutoAddEpgHideButton);
            SetButtonsPanel(autoAddView.manualAutoAddView.stackPanel_button, Settings.Instance.AutoAddManualHideButton);

            //面倒なのでここで処理
            var SetDragMover = new Action<ListBoxDragMoverView>(dm =>
            {
                dm.Margin = IsVertical ? new Thickness(0, 12, 0, 0) : dock == Dock.Top ? new Thickness(6, -2, 0, -5) : new Thickness(6, -6, 0, -1);
                dm.groupOrder.Header = IsVertical ? dm.textBox_Header2.Text : null;
                dm.stackPanel_Order.Orientation = IsVertical ? Orientation.Vertical : Orientation.Horizontal;
                dm.textBox_Header2.Visibility = ToVisibility(IsVertical == false);
                dm.textBox_Header2.Margin = new Thickness(4, 6, 8, 0);
                foreach (var btn in dm.stackPanel_Order.Children.OfType<Button>())
                {
                    btn.MinWidth = 40;
                    btn.Margin = IsVertical ? new Thickness(0, 10, 0, 0) : new Thickness(0, 3, 8, -3);
                }
                dm.textBox_Status.TextWrapping = IsVertical ? TextWrapping.Wrap : TextWrapping.NoWrap;
                dm.textBox_Status.MinWidth = IsVertical ? 40 : 80;
                dm.textBox_Status.Margin = IsVertical ? new Thickness(10, 10, 0, 10) : new Thickness(0, 6, 8, 0);
            });
            SetDragMover(autoAddView.epgAutoAddView.dragMover);
            SetDragMover(autoAddView.manualAutoAddView.dragMover);
        }

        private void ResetTaskMenu()
        {
            //ウィンドウ状態の設定。タスクアイコン状態からの設定変更で操作不能になるのも防止する。
            if (this.WindowState == WindowState.Minimized)
            {
                var vis = Settings.Instance.ShowTray && Settings.Instance.MinHide ? Visibility.Hidden : Visibility.Visible;
                Dispatcher.BeginInvoke(new Action(() => this.Visibility = vis), DispatcherPriority.Loaded);
            }

            if (Settings.Instance.ShowTray == false)
            {
                TrayManager.Tray.Dispose();
                TrayManager.Tray.IconUri = null;
                TrayManager.Tray.Text = "";
                return;
            }
            TrayManager.UpdateInfo();
            TrayManager.Tray.ForceHideBalloonTipSec = Settings.Instance.ForceHideBalloonTipSec;
            TrayManager.Tray.BalloonTipRealtime = Settings.Instance.BalloonTipRealtime;
            TrayManager.Tray.Visible = true;
        }
        public void TaskTrayLeftClick()
        {
            Show();
            WindowState = Settings.Instance.WndSettings[this].LastWindowState;
            Activate();
        }
        public void TaskTrayRightClick()
        {
            if (Settings.Instance.TaskMenuList.Count == 0) return;

            var menu = new ContextMenuEx();
            foreach (string info in Settings.Instance.TaskMenuList)
            {
                if(buttonList.ContainsKey(info))
                {
                    //Contentに置き換えるのはカスタムボタン対応
                    string id = info;//CS4対応のキャプチャ
                    var item = new MenuItem();
                    item.Header = MenuUtil.DeleteAccessKey(buttonList[id].Content as string, true);
                    item.Click += (sender, e) => CommonButtons_Click(id);
                    menu.Items.Add(item);
                }
                else
                {
                    menu.Items.Add(new Separator());
                }
            }

            menu.IsOpen = true;
            var ps = PresentationSource.FromVisual(menu);
            if (ps != null)
            {
                //Activate()したいがContextMenuからWindowを取得できないので仕方なく
                CommonUtil.SetForegroundWindow(((System.Windows.Interop.HwndSource)ps).Handle);
                menu.Focus();
            }
        }

        const string specific = "PushLike";
        private void ResetButtonView()
        {
            //カスタムボタンの更新
            buttonList["カスタム１"].Content = Settings.Instance.Cust1BtnName;
            buttonList["カスタム２"].Content = Settings.Instance.Cust2BtnName;
            buttonList["カスタム３"].Content = Settings.Instance.Cust3BtnName;

            var delTabs = tabControl_main.Items.OfType<TabItem>().Where(ti => (string)ti.Tag == specific).ToList();
            delTabs.ForEach(ti => tabControl_main.Items.Remove(ti));
            stackPanel_button.Children.Clear();

            if (Settings.Instance.ViewButtonShowAsTab == true)
            {
                Settings.Instance.ViewButtonList.ForEach(id => TabButtonAdd(id));
            }
            else
            {
                foreach (string info in Settings.Instance.ViewButtonList)
                {
                    if (string.Compare(info, Settings.ViewButtonSpacer) == 0)
                    {
                        stackPanel_button.Children.Add(new Label { Width = 15 });
                    }
                    else
                    {
                        Button btn;
                        if (buttonList.TryGetValue(info, out btn) == true)
                        {
                            stackPanel_button.Children.Add(btn);
                        }
                    }
                }
            }
            EmphasizeButton(SearchWindow.HasHideWindow, "検索");
            EmphasizeButton(InfoSearchWindow.HasHideWindow, "予約情報検索");

            //EPGビューの高さ調整用
            ViewUtil.TabControlHeaderCopy(tabControl_main, tabEpgDummy);
        }

        TabItem TabButtonAdd(string id)
        {
            Button btn;
            if (buttonList.TryGetValue(id, out btn) == false) return null;

            //ボタン風のタブを追加する
            var ti = new TabItem();
            ti.Header = MenuUtil.DeleteAccessKey(btn.Content as string);
            ti.ToolTip = btn.ToolTip;
            ti.Tag = specific;
            ti.Uid = id;
            ti.Background = null;
            ti.BorderBrush = null;

            //タブ移動をキャンセルしつつ擬似的に対応するボタンを押す
            ti.PreviewMouseDown += (sender, e) => e.Handled = true;
            ti.MouseLeftButtonUp += (sender, e) => CommonButtons_Click(((TabItem)sender).Uid);

            //検索ボタン用のツールチップ設定。
            if (id == "検索") SetSearchButtonTooltip(ti);
            if (id == "予約情報検索") SetInfoSearchButtonTooltip(ti);

            tabControl_main.Items.Add(ti);
            return ti;
        }
        void SetSearchButtonTooltip(FrameworkElement fe)
        {
            SetButtonTooltip(fe, EpgCmds.Search, new Func<string>(() => SearchWindow.HasHideWindow ? "最後に番組表などへジャンプしたダイアログを復帰します。" : ""));
        }
        void SetInfoSearchButtonTooltip(FrameworkElement fe)
        {
            SetButtonTooltip(fe, EpgCmds.InfoSearch, new Func<string>(() => InfoSearchWindow.HasHideWindow ? "最後に番組表などへジャンプしたダイアログを復帰します。" : ""));
        }
        void SetButtonTooltip(FrameworkElement fe, ICommand cmd, Func<string> addText = null)
        {
            fe.ToolTip = "";
            fe.ToolTipOpening += (sender, e) =>
            {
                var keytip = MenuBinds.GetInputGestureText(cmd);
                var addtip = addText == null ? "" : addText();
                fe.ToolTip = ((string.IsNullOrEmpty(keytip) == true ? "" : keytip + "\r\n") + addtip).TrimEnd();
            };
        }

        void CommonButtons_Click(string tag)
        {
            Button btn;
            if (string.IsNullOrEmpty(tag) == true || buttonList.TryGetValue(tag, out btn) == false) return;
            btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        DispatcherTimer connectTimer = null;
        void OpenConnectDialog()
        {
            if (CommonManager.Instance.NWMode == false) return;

            //複数ダイアログの禁止(タスクアイコンからの起動対策)
            if (ViewUtil.SingleWindowCheck(typeof(ConnectWindow)) != 0) return;

            if (connectTimer != null) return;

            var dlg = new ConnectWindow();
            dlg.Owner = CommonUtil.GetTopWindow(this);
            if (dlg.ShowDialog() == true)
            {
                ConnectCmd(true);
            }
        }
        void ConnectCmd(bool showDialog = false)
        {
            if (CommonManager.Instance.NWMode == false) return;

            //ダイアログが残っているようなら閉じる(タスクアイコンからの起動対策)
            ViewUtil.SingleWindowCheck(typeof(ConnectWindow), true);

            var interval = TimeSpan.FromSeconds(Settings.Instance.WoLWaitSecond + 60);
            var CheckIsConnected = new Action(() =>
            {
                if (connectTimer != null)
                {
                    connectTimer.Stop();
                    connectTimer = null;
                }
                if (CommonManager.Instance.IsConnected == false)
                {
                    if (showDialog == true)
                    {
                        MessageBox.Show("サーバーへの接続に失敗しました");
                    }
                    StatusManager.StatusNotifyAppend("接続に失敗 < ");
                }
            });

            if (Settings.Instance.WoLWaitRecconect == true)
            {
                int dummy;
                try { NWConnect.SendMagicPacket(ConnectWindow.ConvertTextMacAddress(Settings.Instance.NWMacAdd), out dummy, out dummy); }
                catch { }

                connectTimer = new DispatcherTimer();
                connectTimer.Interval = TimeSpan.FromSeconds(Math.Max(Settings.Instance.WoLWaitSecond, 1));
                connectTimer.Tick += (sender, e) =>
                {
                    connectTimer.Stop();
                    StatusManager.StatusNotifyAppend("EpgTimerSrvへ接続中... < ", interval);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ConnectSrv();
                        CheckIsConnected();
                    }), DispatcherPriority.Render);
                };
                connectTimer.Start();
            }

            StatusManager.StatusNotifySet("EpgTimerSrvへ接続中...", interval);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ConnectSrv() == false && Settings.Instance.WoLWaitRecconect == true)
                {
                    var msg = string.Format("{0}再接続待機中({1}秒間)...", showDialog == true ? "" : "起動時自動", Settings.Instance.WoLWaitSecond);
                    StatusManager.StatusNotifySet(msg, interval);
                    return;
                }
                CheckIsConnected();
            }), DispatcherPriority.Render);
        }
        bool connectingSrv = false;
        bool ConnectSrv()
        {
            if (connectingSrv == false)
            {
                connectingSrv = true;
                try { return ConnectSrvMain(); } catch { } finally { connectingSrv = false; }
            }
            return false;
        }
        bool ConnectSrvMain()
        {
            var connected = false;
            try
            {
                //IPv4の名前解決を優先する
                foreach (var address in System.Net.Dns.GetHostAddresses(Settings.Instance.NWServerIP).OrderBy(a => a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
                {
                    //コールバックは別スレッドかもしれないので設定は予めキャプチャする
                    uint execBat = Settings.Instance.ExecBat;
                    CommonManager.Instance.NWConnectedIP = null;
                    if (nwConnect.ConnectServer(address, Settings.Instance.NWServerPort, Settings.Instance.NWWaitPort, (c, r) => OutsideCmdCallback(c, r, true, execBat)))
                    {
                        CommonManager.Instance.NWConnectedIP = address;
                        CommonManager.Instance.NWConnectedPort = Settings.Instance.NWServerPort;
                        connected = true;
                        break;
                    }
                }
            }
            catch { }

            if (connected == false)
            {
                TrayManager.SrvLosted();
                return false;
            }

            StatusManager.StatusNotifySet("EpgTimerSrvへ接続完了");

            IniFileHandler.UpdateSrvProfileIni();

            WakeCheckService();

            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.RecInfo);
            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.PlugInFile);
            CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.EpgData);
            CommonManager.Instance.DB.ReloadEpgDatabaseInfo(true);
            CommonManager.Instance.DB.ReloadReserveInfo(true);
            CommonManager.Instance.DB.ClearRecFileAppend();
            CommonManager.Instance.DB.ReloadEpgAutoAddInfo(true);
            CommonManager.Instance.DB.ReloadManualAutoAddInfo(true);
            if (Settings.Instance.NgAutoEpgLoadNW == false)
            {
                CommonManager.Instance.DB.ReloadEpgData(false, true);
            }
            epgView.UpdateInfo(!Settings.Instance.NgAutoEpgLoadNW);
            reserveView.UpdateInfo();
            UpdateReserveTab();
            tunerReserveView.UpdateInfo();
            autoAddView.UpdateInfo();
            recInfoView.UpdateInfo();
            SearchWindow.UpdatesInfo();
            InfoSearchWindow.UpdatesInfo();
            AddReserveEpgWindow.UpdatesInfo();
            ChgReserveWindow.UpdatesInfo();
            RecInfoDescWindow.UpdatesInfo();
            NotifyLogWindow.UpdatesInfo();
            SettingWindow.UpdatesInfo("再接続に伴う設定更新");
            return true;
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                TrayManager.SrvLosted();
                ChkTimerWork();
            }
        }

        private void WakeCheckService()
        {
            if (Settings.Instance.WakeCheckService == true)
            {
                bool ret = SettingWindow.CheckServiceSettings(Settings.Instance, !Settings.Instance.WakeCheckServiceDialog);
                if (ret) SettingWindow.UpdatesInfo("TSID情報の更新");
            }
        }

        //視聴予約開始チェック用
        private List<uint> watchReserveOnRec = new List<uint>();

        public void ChkTimerWork()
        {
            //オプション状態などが変っている場合もあるので、いったん破棄する。
            if (chkTimer != null)
            {
                chkTimer.Stop();
                chkTimer = null;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }

            bool chkSrvRegistTCP = CommonManager.Instance.NWMode == true && Settings.Instance.ChkSrvRegistTCP == true;
            bool updateTaskText = Settings.Instance.UpdateTaskText == true;

            if (chkSrvRegistTCP == true || updateTaskText == true)
            {
                chkTimer = new DispatcherTimer();
                chkTimer.Interval = TimeSpan.FromSeconds(30);//初回はすぐ実行
                chkTimer.Tick += (sender, e) => chkTimer.Interval = TimeSpan.FromMinutes(Math.Max(Settings.Instance.ChkSrvRegistInterval, 1));

                if (chkSrvRegistTCP == true)
                {
                    SystemEvents.PowerModeChanged += OnPowerModeChanged;
                    chkTimer.Tick += (sender, e) =>
                    {
                        if (CommonManager.Instance.IsConnected == true)
                        {
                            var status = new NotifySrvInfo();
                            var waitPort = Settings.Instance.NWWaitPort;
                            bool registered = true;
                            if (waitPort == 0 && CommonManager.CreateSrvCtrl().SendGetNotifySrvStatus(ref status) == ErrCode.CMD_SUCCESS ||
                                waitPort != 0 && CommonManager.CreateSrvCtrl().SendIsRegistTCP(waitPort, ref registered) == ErrCode.CMD_SUCCESS)
                            {
                                if (TrayManager.IsSrvLost == true ||　//EpgTimerNW側の休止復帰も含む
                                        waitPort != 0 && registered == false) //Srv側再起動の場合。待ち受けポート無しの場合は勝手に復帰する。
                                {
                                    if (ConnectSrv() == true)
                                    {
                                        StatusManager.StatusNotifyAppend("自動再接続 - ");
                                    }
                                    else
                                    {
                                        StatusManager.StatusNotifySet("自動再接続 - EpgTimerSrvへの再接続に失敗");
                                    }
                                }
                                return;
                            }
                        }
                        TrayManager.SrvLosted(updateTaskText == false);
                    };
                }
                if (updateTaskText == true)
                {
                    chkTimer.Tick += (sender, e) => ChkWatchStart();
                    chkTimer.Tick += (sender, e) => TrayManager.UpdateInfo();
                }
                chkTimer.Start();
            }
        }

        private void ChkWatchStart(bool chkOnly = false)
        {
            //視聴予約開始チェック
            var list = CommonManager.Instance.DB.ReserveList.Values
                                .Where(r => r.IsWatchMode && r.IsOnRec()).Select(r => r.ReserveID).ToList();
            if (watchReserveOnRec.Count != list.Count || watchReserveOnRec.Except(list).Any())
            {
                if (chkOnly != true) RefreshAllViewsReserveInfo(UpdateViewMode.ReserveInfoNoAutoAdd);
                watchReserveOnRec = list;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.minimizedStarting == false)
            {
                if (Settings.Instance.WakeReconnectNW == false && CommonManager.Instance.IsConnected == false)
                {
                    Dispatcher.BeginInvoke(new Action(() => OpenConnectDialog()));
                }
            }
            AttendantWindow.UpdatesPinned();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Settings.Instance.CloseMin == true && closeFlag == false)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
            }
            else
            {
                AttendantWindow.CloseWindows();
                SaveData();

                if (CommonManager.Instance.NWMode == false)
                {
                    var cmd = CommonManager.CreateSrvCtrl();
                    cmd.SetConnectTimeOut(3000);
                    using (var process = Process.GetCurrentProcess())
                    {
                        cmd.SendUnRegistGUI((uint)process.Id);
                    }
                    //オリジナルのmutex名をもつEpgTimerか
                    if (mutexName == "2")
                    {
                        //実際にEpgTimerSrvを終了するかどうかは(現在は)EpgTimerSrvの判断で決まる
                        //このフラグはEpgTimerと原作のサービスモードのEpgTimerSrvを混用するなど特殊な状況を想定したもの
                        if (Settings.Instance.NoSendClose == 0)
                        {
                            cmd.SendClose();
                        }
                    }
                    pipeServer.Dispose();
                }
                else if (Settings.Instance.NWWaitPort != 0 && CommonManager.Instance.IsConnected == true && TrayManager.IsSrvLost == false)
                {
                    CommonManager.CreateSrvCtrl().SendUnRegistTCP(Settings.Instance.NWWaitPort);
                }
                mutex.ReleaseMutex();
                mutex.Close();
                TrayManager.Tray.Dispose();
                nwConnect.Dispose();
            }
        }

        public void SaveData(bool manualSave = false)
        {
            reserveView.SaveViewData();
            recInfoView.SaveViewData();
            autoAddView.SaveViewData();
            epgView.SaveViewData();

            Settings.Instance.WndSettings.GetSizeFromWindow(this);
            Settings.SaveToXmlFile(manualSave);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.minimizedStarting == null) return;
            if (this.WindowState == WindowState.Minimized)
            {
                if (Settings.Instance.ShowTray && Settings.Instance.MinHide)
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        win.Visibility = Visibility.Hidden;
                    }
                }
            }
            else
            {
                if (this.minimizedStarting == true)
                {
                    minimizedStarting = null;
                    if (Settings.Instance.WndSettings[this].LastWindowState != WindowState.Minimized)
                    {
                        this.WindowState = Settings.Instance.WndSettings[this].LastWindowState;
                    }
                    minimizedStarting = false;
                    if (Settings.Instance.WakeReconnectNW == false && CommonManager.Instance.IsConnected == false)
                    {
                        Dispatcher.BeginInvoke(new Action(() => OpenConnectDialog()));
                    }
                }
                foreach (Window win in Application.Current.Windows)
                {
                    win.Visibility = Visibility.Visible;
                }
                AttendantWindow.UpdatesPinned();
                Settings.Instance.WndSettings[this].LastWindowState = this.WindowState;
            }
        }

        private void Window_PreviewDragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            string[] filePath = e.Data.GetData(DataFormats.FileDrop, true) as string[];
            if (filePath != null) SendAddReserveFromArgs(CommonManager.CreateSrvCtrl(), filePath);
        }

        private static void SendAddReserveFromArgs(CtrlCmdUtil cmd, IEnumerable<string> args)
        {
            var addList = new List<ReserveData>();
            foreach (string arg in args)
            {
                ReserveData info = null;
                if (arg.EndsWith(".tvpid", StringComparison.OrdinalIgnoreCase) || arg.EndsWith(".tvpio", StringComparison.OrdinalIgnoreCase))
                {
                    //iEPG追加
                    info = IEPGFileClass.TryLoadTVPID(arg, ChSet5.ChList);
                    if (info == null)
                    {
                        MessageBox.Show("解析に失敗しました。デジタル用Version 2のiEPGの必要があります。");
                        return;
                    }
                }
                else if (arg.EndsWith(".tvpi", StringComparison.OrdinalIgnoreCase))
                {
                    //iEPG追加
                    info = IEPGFileClass.TryLoadTVPI(arg, ChSet5.ChList, Settings.Instance.IEpgStationList);
                    if (info == null)
                    {
                        MessageBox.Show("解析に失敗しました。放送局名がサービスに関連づけされていない可能性があります。");
                        return;
                    }
                }
                if (info != null)
                {
                    ulong pgID = CommonManager.Create64PgKey(info.OriginalNetworkID, info.TransportStreamID, info.ServiceID, info.EventID);
                    var pgInfo = new EpgEventInfo();
                    if (info.EventID != 0xFFFF && cmd.SendGetPgInfo(pgID, ref pgInfo) == ErrCode.CMD_SUCCESS)
                    {
                        //番組情報が見つかったので更新しておく
                        if (pgInfo.ShortInfo != null)
                        {
                            info.Title = pgInfo.ShortInfo.event_name;
                        }
                        if (pgInfo.StartTimeFlag != 0)
                        {
                            info.StartTime = pgInfo.start_time;
                            info.StartTimeEpg = pgInfo.start_time;
                        }
                        if (pgInfo.DurationFlag != 0)
                        {
                            info.DurationSecond = pgInfo.durationSec;
                        }
                    }
                    info.RecSetting = Settings.Instance.RecPresetList[0].Data.DeepClone();
                    addList.Add(info);
                }
            }
            if (addList.Count > 0)
            {
                var list = new List<ReserveData>();
                if (cmd.SendEnumReserve(ref list) == ErrCode.CMD_SUCCESS)
                {
                    //重複除去
                    addList = addList.Where(a => list.All(b =>
                        a.OriginalNetworkID != b.OriginalNetworkID ||
                        a.TransportStreamID != b.TransportStreamID ||
                        a.ServiceID != b.ServiceID ||
                        a.EventID != b.EventID ||
                        a.EventID == 0xFFFF && (a.StartTime != b.StartTime || a.DurationSecond != b.DurationSecond))).ToList();
                    if (addList.Count == 0 || cmd.SendAddReserve(addList) == ErrCode.CMD_SUCCESS)
                    {
                        return;
                    }
                }
                MessageBox.Show("予約追加に失敗しました。");
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled && e.IsRepeat) return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.D1:
                        tabItem_reserve.IsSelected = true;
                        break;
                    case Key.D2:
                        tabItem_tunerReserve.IsSelected = true;
                        break;
                    case Key.D3:
                        tabItem_recinfo.IsSelected = true;
                        break;
                    case Key.D4:
                        tabItem_AutoAdd.IsSelected = true;
                        break;
                    case Key.D5:
                        tabItem_epg.IsSelected = true;
                        break;
                    default:
                        return;
                }
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F5)
            {
                RefreshReserveInfo();
                e.Handled = true;
            }
        }

        public void OpenSettingDialog(SettingWindow.SettingMode mode = SettingWindow.SettingMode.Default, object param = null)
        {
            //複数ダイアログの禁止
            if (ViewUtil.SingleWindowCheck(typeof(SettingWindow)) != 0)
            {
                Application.Current.Windows.OfType<SettingWindow>().First().SetMode(mode, param);
            }
            else
            {
                new SettingWindow(mode, param).Show();
            }
        }
        public void RefreshSetting(SettingWindow setting)
        {
            if (CommonManager.Instance.NWMode == true)
            {
                if (setting.setBasicView.IsChangeSettingPath == true)
                {
                    IniFileHandler.UpdateSrvProfileIni();
                }
            }
            else
            {
                CommonManager.CreateSrvCtrl().SendReloadSetting();
                CommonManager.CreateSrvCtrl().SendNotifyProfileUpdate(mutexName);
            }

            ChSet5.ReLoadLogo();

            if (setting.setEpgView.IsChangeRecInfoDropExcept == true)
            {
                CommonManager.Instance.DB.ResetRecFileErrInfo();
            }
            if (Settings.Instance.NgAutoEpgLoadNW == false)
            {
                CommonManager.Instance.DB.ReloadEpgData(false, true);
            }

            ResetMainView();
            StatusManager.StatusNotifySet("設定変更に伴う画面再構築を実行");

            epgView.UpdateSetting();
            reserveView.UpdateInfo();
            tunerReserveView.UpdateInfo();
            recInfoView.UpdateInfo();
            autoAddView.UpdateInfo();
            SearchWindow.UpdatesInfo(false);
            InfoSearchWindow.UpdatesInfo();
            AddReserveEpgWindow.UpdatesInfo();
            ChgReserveWindow.UpdatesInfo();
            RecInfoDescWindow.UpdatesInfo();
            NotifyLogWindow.UpdatesInfo();
        }

#if PER_MONITOR_DPI
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            //この段階では周辺オブジェクトの再配置が終わっていないため物理位置が変わるかもしれない
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                StatusManager.StatusNotifySet("表示DPI変更に伴う画面再構築を実行");
                epgView.UpdateSetting();
                tunerReserveView.UpdateInfo();
            }));
        }
#endif

        public void RefreshMenu()
        {
            CommonManager.Instance.MM.ReloadWorkData();
            if (Settings.Instance.MenuSet.RestoreNoUse == true) CmdHistorys.Clear();
            reserveView.RefreshMenu();
            tunerReserveView.RefreshMenu();
            recInfoView.RefreshMenu();
            autoAddView.RefreshMenu();
            epgView.RefreshMenu();
            AttendantWindow.RefreshMenus();

            //メインウィンドウの検索ボタン用。
            mBinds.ResetInputBindings(this);
        }
        public enum UpdateViewMode { ReserveInfo, ReserveInfoNoTuner, ReserveInfoNoAutoAdd }
        public void RefreshAllViewsReserveInfo(UpdateViewMode mode = UpdateViewMode.ReserveInfo)
        {
            reserveView.UpdateInfo();
            if (mode != UpdateViewMode.ReserveInfoNoTuner) tunerReserveView.UpdateInfo();
            if (mode != UpdateViewMode.ReserveInfoNoAutoAdd) autoAddView.UpdateInfo();
            epgView.UpdateReserveInfo();
            SearchWindow.UpdatesInfo(false);
            InfoSearchWindow.UpdatesInfo();
            if (mode != UpdateViewMode.ReserveInfoNoAutoAdd) AddReserveEpgWindow.UpdatesInfo(false);
            ChgReserveWindow.UpdatesInfo();
        }
        void StatusbarReset()
        {
            statusBar.ClearText();//一応
            statusBar.Visibility = Settings.Instance.DisplayStatus == true ? Visibility.Visible : Visibility.Collapsed;
        }

        void OpenSearchDialog()
        {
            // 最小化したSearchWindowを復帰
            if (SearchWindow.HasHideWindow == true)
            {
                SearchWindow.RestoreHideWindow();
            }
            else
            {
                MenuUtil.OpenSearchEpgDialog();
            }
        }

        void OpenInfoSearchDialog()
        {
            if (InfoSearchWindow.HasHideWindow == true)
            {
                InfoSearchWindow.RestoreHideWindow();
            }
            else
            {
                MenuUtil.OpenInfoSearchDialog();
            }
        }

        public void RestoreMinimizedWindow()
        {
            if (this.IsVisible == false || this.WindowState == WindowState.Minimized)
            {
                this.Visibility = Visibility.Visible;
                this.WindowState = Settings.Instance.WndSettings[this].LastWindowState;
            }
        }

        void CloseCmd()
        {
            closeFlag = true;
            Close();
        }

        void EpgCapCmd()
        {
            if (CommonManager.CreateSrvCtrl().SendEpgCapNow() != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show("EPG取得を行える状態ではありません。\r\n（もうすぐ予約が始まる。EPGデータ読み込み中。など）");
            }
        }

        private bool epgReloadCmdRun = false;
        void EpgReloadCmd()
        {
            if (CommonManager.CreateSrvCtrl().SendReloadEpg() != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show("EPG再読み込みを行える状態ではありません。\r\n（EPGデータ読み込み中。など）");
                return;
            }
            epgReloadCmdRun = true;
            StatusManager.StatusNotifySet("EPG再読み込みを実行");
        }

        void SuspendCmd(byte suspendMode)
        {
            //既にダイアログが出ている場合は閉じる。(タスクアイコンからの起動対策)
            ViewUtil.SingleWindowCheck(typeof(SuspendCheckWindow), true);

            suspendMode = suspendMode == 1 ? suspendMode : (byte)2;
            ErrCode err = TrayManager.IsSrvLost == true ? ErrCode.CMD_ERR_CONNECT : CommonManager.CreateSrvCtrl().SendChkSuspend();
            if (err != ErrCode.CMD_SUCCESS)
            {
                MessageBox.Show(CommonManager.GetErrCodeText(err) ?? (suspendMode == 1 ? "スタンバイ" : "休止") 
                    + "に移行できる状態ではありません。\r\n（もうすぐ予約が始まる。または抑制条件のexeが起動している。など）");
                return;
            }

            if (Settings.Instance.SuspendChk == 1)
            {
                if (new SuspendCheckWindow(suspendMode).ShowDialog() != true) return;
            }

            if (CommonManager.Instance.NWMode == true && Settings.Instance.SuspendCloseNW == true)
            {
                CloseCmd();
            }
            else
            {
                SaveData();
            }
            //※Window_Closing()でnwConnectは破棄されるが、CommonManager.NWConnectedIP/NWConnectedPortは残っているので問題無い
            CommonManager.CreateSrvCtrl().SendSuspend((ushort)(0xFF00 | suspendMode));
        }

        void CustumCmd(int id)
        {
            try
            {
                switch (id)
                {
                    case 1:
                        using (Process.Start(new ProcessStartInfo(Settings.Instance.Cust1BtnCmd, Settings.Instance.Cust1BtnCmdOpt) { UseShellExecute = true })) { }
                        break;
                    case 2:
                        using (Process.Start(new ProcessStartInfo(Settings.Instance.Cust2BtnCmd, Settings.Instance.Cust2BtnCmdOpt) { UseShellExecute = true })) { }
                        break;
                    case 3:
                        using (Process.Start(new ProcessStartInfo(Settings.Instance.Cust3BtnCmd, Settings.Instance.Cust3BtnCmdOpt) { UseShellExecute = true })) { }
                        break;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        void NwTVEndCmd()
        {
            CommonManager.Instance.TVTestCtrl.CloseTVTest();
        }

        void OpenNotifyLogDialog()
        {
            //複数ダイアログの禁止
            if (ViewUtil.SingleWindowCheck(typeof(NotifyLogWindow)) != 0) return;

            new NotifyLogWindow().Show();
        }

        private Tuple<ErrCode, byte[], uint> OutsideCmdCallback(uint cmdParam, byte[] cmdData, bool networkFlag, uint execBat)
        {
            Trace.WriteLine((CtrlCmd)cmdParam);
            var res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_NON_SUPPORT, null, 0);

            switch ((CtrlCmd)cmdParam)
            {
                case CtrlCmd.CMD_TIMER_GUI_SHOW_DLG:
                    if (networkFlag == false)
                    {
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, 0);
                        Dispatcher.BeginInvoke(new Action(() => Visibility = Visibility.Visible));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_VIEW_EXECUTE:
                    if (networkFlag == false)
                    {
                        //原作では成否にかかわらずCMD_SUCCESSだったが、サーバ側の仕様と若干矛盾するので変更した
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_ERR, null, 0);
                        string exeCmd = "";
                        new CtrlCmdReader(new MemoryStream(cmdData, false)).Read(ref exeCmd);
                        if (exeCmd.Length > 0 && exeCmd[0] == '"')
                        {
                            //形式は("FileName")か("FileName" Arguments..)のどちらか。ほかは拒否してよい
                            int i = exeCmd.IndexOf('"', 1);
                            if (i >= 2 && (exeCmd.Length == i + 1 || exeCmd[i + 1] == ' '))
                            {
                                var startInfo = new ProcessStartInfo(exeCmd.Substring(1, i - 1));
                                startInfo.UseShellExecute = true;
                                if (exeCmd.Length > i + 2)
                                {
                                    startInfo.Arguments = exeCmd.Substring(i + 2);
                                }
                                if (startInfo.FileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (execBat == 0)
                                    {
                                        startInfo.WindowStyle = ProcessWindowStyle.Minimized;
                                    }
                                    else if (execBat == 1)
                                    {
                                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    }
                                }
                                //FileNameは実行ファイルか.batのフルパス。チェックはしない(安全性云々はここで考えることではない)
                                try
                                {
                                    //ShellExecute相当なので.batなどもそのまま与える
                                    using (var process = Process.Start(startInfo))
                                    {
                                        if (process != null)
                                        {
                                            try
                                            {
                                                //"EpgTimer Service"のサービスセキュリティ識別子(Service-specific SID)に対するアクセス許可を追加する
                                                var trustee = new System.Security.Principal.NTAccount("NT Service\\EpgTimer Service");
                                                var trusteeSid = trustee.Translate(typeof(System.Security.Principal.SecurityIdentifier));
                                                var sec = new KernelObjectSecurity(process.Handle);
                                                //SYNCHRONIZE | PROCESS_TERMINATE | PROCESS_SET_INFORMATION
                                                sec.AddAccessRule(new KernelObjectAccessRule(trusteeSid, 0x100000 | 0x01 | 0x0200,
                                                                                             System.Security.AccessControl.AccessControlType.Allow));
                                                sec.Persist(process.Handle);
                                            }
                                            catch { }
                                            var w = new CtrlCmdWriter(new MemoryStream());
                                            w.Write(process.Id);
                                            w.Stream.Close();
                                            res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, w.Stream.ToArray(), 0);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_QUERY_SUSPEND:
                    if (networkFlag == false)
                    {
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, 0);

                        ushort param = 0;
                        new CtrlCmdReader(new MemoryStream(cmdData, false)).Read(ref param);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePC", 0, SettingPath.TimerSrvIniPath) == 1)
                            {
                                int ngUsePCTime = IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "NoUsePCTime", 3, SettingPath.TimerSrvIniPath);
                                if (ngUsePCTime == 0 || CommonUtil.GetIdleTimeSec() < ngUsePCTime * 60)
                                {
                                    return;
                                }
                            }

                            if (new SuspendCheckWindow(param & 0x00FFu).ShowDialog() == true)
                            {
                                SaveData();
                                CommonManager.CreateSrvCtrl().SendSuspend(param);
                            }
                        }));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_QUERY_REBOOT:
                    if (networkFlag == false)
                    {
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, 0);

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (new SuspendCheckWindow(0).ShowDialog() == true)
                            {
                                SaveData();
                                CommonManager.CreateSrvCtrl().SendReboot();
                            }
                        }));
                    }
                    break;
                case CtrlCmd.CMD_TIMER_GUI_SRV_STATUS_NOTIFY2:
                    {
                        NotifySrvInfo status = new NotifySrvInfo();
                        var r = new CtrlCmdReader(new MemoryStream(cmdData, false));
                        ushort version = 0;
                        r.Read(ref version);
                        r.Version = version;
                        r.Read(ref status);
                        //通知の巡回カウンタをItem3で返す
                        res = new Tuple<ErrCode, byte[], uint>(ErrCode.CMD_SUCCESS, null, status.param3);
                        Dispatcher.BeginInvoke(new Action(() => NotifyStatus(status)));
                    }
                    break;
            }
            return res;
        }

        void NotifyStatus(NotifySrvInfo status)
        {
            bool notifyLogWindowUpdate = false;//WakeUpHDDWork()などのメッセージを一応フォローしておく
            var NotifyWork = new Action(() =>
            {
                if (Settings.Instance.NoBallonTips == false)
                {
                    var item = new NotifySrvInfoItem(status, false);
                    TrayManager.Tray.ShowBalloonTip(item.Title, item.LogText);
                }
                CommonManager.AddNotifyLog(status);
                notifyLogWindowUpdate = true;
            });

            Trace.WriteLine((UpdateNotifyItem)status.notifyID);

            var err = ErrCode.CMD_SUCCESS;

            switch ((UpdateNotifyItem)status.notifyID)
            {
                case UpdateNotifyItem.SrvStatus:
                    TrayManager.UpdateInfo(status.param1);
                    if (status.param1 == 1) ChkWatchStart();
                    break;
                case UpdateNotifyItem.PreRecStart:
                    NotifyWork();
                    TrayManager.UpdateInfo();
                    CommonManager.WakeUpHDDWork();
                    break;
                case UpdateNotifyItem.RecStart:
                    NotifyWork();
                    RefreshAllViewsReserveInfo(UpdateViewMode.ReserveInfoNoAutoAdd);
                    break;
                case UpdateNotifyItem.RecEnd:
                    NotifyWork();
                    ChkWatchStart(true);
                    break;
                case UpdateNotifyItem.RecTuijyu:
                case UpdateNotifyItem.ChgTuijyu:
                case UpdateNotifyItem.PreEpgCapStart:
                case UpdateNotifyItem.EpgCapStart:
                case UpdateNotifyItem.EpgCapEnd:
                    NotifyWork();
                    break;
                case UpdateNotifyItem.EpgData:
                    {
                        bool epgReload = tabItem_epg.IsSelected == true || Settings.Instance.NgAutoEpgLoadNW == false || epgReloadCmdRun == true;
                        epgReloadCmdRun = false;

                        //録画予定ファイル名が変化しているかもしれない。先に実行
                        err = CommonManager.Instance.DB.ReloadReserveRecFileNameList(true);

                        //EpgDataは遅延実行される場合があるので、処理内容には注意する。
                        CommonManager.Instance.WaitingSrvReady = false;
                        CommonManager.Instance.DB.SetUpdateNotify(UpdateNotifyItem.EpgData);
                        CommonManager.Instance.DB.ReloadEpgDatabaseInfo(true);
                        if (epgReload == true)
                        {
                            var err2 = CommonManager.Instance.DB.ReloadEpgData(false, true);
                            if (err == ErrCode.CMD_SUCCESS) err = err2;
                        }

                        epgView.UpdateInfo(epgReload);
                        reserveView.UpdateInfo();
                        tunerReserveView.UpdateInfo();
                        autoAddView.epgAutoAddView.UpdateInfo();
                        SearchWindow.UpdatesInfo();
                        InfoSearchWindow.UpdatesInfo();
                        if (epgReload == true) AddReserveEpgWindow.UpdatesInfo();
                        ChgReserveWindow.UpdatesInfo();
                        TrayManager.UpdateInfo();
                        StatusManager.StatusNotifyAppend((epgReload == true ? "EPG" : "予約名") + "データ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.ReserveInfo:
                    //頻繁に来るときがあるので間引く
                    MainProc(MainProcItem.ReserveInfo, () =>
                    {
                        err = CommonManager.Instance.DB.ReloadReserveInfo(true);
                        RefreshAllViewsReserveInfo();
                        UpdateReserveTab();
                        TrayManager.UpdateInfo();
                        StatusManager.StatusNotifyAppend("予約データ更新 < ");
                    });
                    break;
                case UpdateNotifyItem.RecInfo:
                    {
                        CommonManager.Instance.DB.ReloadRecFileInfo(true);
                        recInfoView.UpdateInfo();
                        epgView.UpdateReserveInfo();
                        SearchWindow.UpdatesRecinfo();
                        AddReserveEpgWindow.UpdatesInfo(false);
                        InfoSearchWindow.UpdatesInfo();
                        RecInfoDescWindow.UpdatesInfo();
                        StatusManager.StatusNotifyAppend("録画済みデータ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.AutoAddEpgInfo:
                    {
                        err = CommonManager.Instance.DB.ReloadEpgAutoAddInfo(true);
                        autoAddView.epgAutoAddView.UpdateInfo();

                        if (Settings.Instance.DisplayReserveAutoAddMissing == true)
                        {
                            RefreshAllViewsReserveInfo(UpdateViewMode.ReserveInfoNoAutoAdd);
                        }
                        StatusManager.StatusNotifyAppend("キーワード予約データ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.AutoAddManualInfo:
                    {
                        err = CommonManager.Instance.DB.ReloadManualAutoAddInfo(true);
                        autoAddView.manualAutoAddView.UpdateInfo();

                        if (Settings.Instance.DisplayReserveAutoAddMissing == true)
                        {
                            RefreshAllViewsReserveInfo(UpdateViewMode.ReserveInfoNoAutoAdd);
                        }
                        StatusManager.StatusNotifyAppend("プログラム自動予約データ更新 < ");
                    }
                    break;
                case UpdateNotifyItem.IniFile:
                    {
                        //EpgtimerNWはSendNotifyProfileUpdate()しないので当面これでいい
                        if (status.param4 != mutexName)
                        {
                            err = IniFileHandler.UpdateSrvProfileIni();
                            RefreshAllViewsReserveInfo();
                            notifyLogWindowUpdate = true;
                            SetRecPresetWindow.UpdatesInfo();
                            SettingWindow.UpdatesInfo("別画面/PCでの設定更新");
                            TrayManager.UpdateInfo();
                            StatusManager.StatusNotifyAppend("別画面/PCでの設定更新に伴う画面更新 < ");
                        }
                    }
                    break;
            }

            if (err != ErrCode.CMD_SUCCESS) StatusManager.StatusNotifyAppend("情報更新中にエラー発生 < ");
            if (notifyLogWindowUpdate == true) NotifyLogWindow.UpdatesInfo();
        }

        private Dictionary<MainProcItem, Action> mainProc = new Dictionary<MainProcItem, Action>();
        public void MainProc(MainProcItem notifyID, Action work = null)
        {
            mainProc[notifyID] = work;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var proc in mainProc)
                {
                    switch (proc.Key)
                    {
                        case MainProcItem.EpgDataLoaded:
                            reserveView.UpdateInfo();
                            SearchWindow.UpdatesInfo(false);
                            InfoSearchWindow.UpdatesInfo();
                            AddReserveEpgWindow.UpdatesInfo();
                            ChgReserveWindow.UpdatesInfo();
                            break;
                        case MainProcItem.EpgDataSearch:
                            if (mainProc.ContainsKey(MainProcItem.ReserveInfo)) continue;
                            if (mainProc.ContainsKey(MainProcItem.EpgDataLoaded)) continue;
                            reserveView.UpdateInfo();
                            InfoSearchWindow.UpdatesInfo();
                            ChgReserveWindow.UpdatesInfo();
                            break;
                        case MainProcItem.EpgDataAddLoaded:
                            if (mainProc.ContainsKey(MainProcItem.ReserveInfo)) continue;
                            if (mainProc.ContainsKey(MainProcItem.EpgDataLoaded)) continue;
                            if (mainProc.ContainsKey(MainProcItem.EpgDataSearch)) continue;
                            ChgReserveWindow.UpdatesInfo();
                            break;
                    }
                    if (proc.Value != null) proc.Value();
                }
                mainProc.Clear();
            }));
        }

        void RefreshReserveInfo()
        {
            try
            {
                new BlackoutWindow(this).showWindow("情報の強制更新");
                DBManager DB = CommonManager.Instance.DB;

                //録画済み一覧のクリア。InfoSearchWindowは予約情報の更新部分でUpdateInfo()が実行される。
                DB.ClearRecFileAppend();
                DB.SetUpdateNotify(UpdateNotifyItem.RecInfo);
                recInfoView.UpdateInfo();
                RecInfoDescWindow.UpdatesInfo();

                //誤って変更しないよう、一度Srv側のリストを読み直す
                if (DB.ReloadEpgAutoAddInfo(true) == ErrCode.CMD_SUCCESS && DB.EpgAutoAddList.Count != 0)
                {
                    CommonManager.CreateSrvCtrl().SendChgEpgAutoAdd(DB.EpgAutoAddList.Values.ToList());
                }
                //追加データもクリアしておく。
                DB.ClearEpgAutoAddDataAppend();

                //EPG自動登録とは独立
                if (DB.ReloadManualAutoAddInfo(true) == ErrCode.CMD_SUCCESS && DB.ManualAutoAddList.Count != 0)
                {
                    CommonManager.CreateSrvCtrl().SendChgManualAdd(DB.ManualAutoAddList.Values.ToList());
                }

                //上の二つが空リストでなくても、予約情報の更新がされない場合もある
                if (DB.ReloadReserveInfo(true) == ErrCode.CMD_SUCCESS && DB.ReserveList.Count != 0)
                {
                    //予約一覧は一つでも更新をかければ、再構築される。
                    CommonManager.CreateSrvCtrl().SendChgReserve(DB.ReserveList.Values.First().IntoList());
                }
                else
                {
                    //更新しない場合でも、再描画だけはかけておく
                    RefreshAllViewsReserveInfo();
                    UpdateReserveTab();
                }
                StatusManager.StatusNotifySet("情報の強制更新を実行(F5)");
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }

        }

        public void moveTo_tabItem(CtxmCode code)
        {
            TabItem tab;
            switch (code)
            {
                case CtxmCode.ReserveView:
                    tab = this.tabItem_reserve;
                    break;
                case CtxmCode.TunerReserveView:
                    tab = this.tabItem_tunerReserve;
                    break;
                case CtxmCode.RecInfoView:
                    tab = this.tabItem_recinfo;
                    break;
                case CtxmCode.EpgAutoAddView:
                    tab = this.tabItem_AutoAdd;
                    this.autoAddView.tabItem_epgAutoAdd.IsSelected = true;
                    break;
                case CtxmCode.ManualAutoAddView:
                    tab = this.tabItem_AutoAdd;
                    this.autoAddView.tabItem_manualAutoAdd.IsSelected = true;
                    break;
                case CtxmCode.EpgView:
                    tab = this.tabItem_epg;
                    break;
                default:
                    return;
            }
            BlackoutWindow.NowJumpTable = true;
            new BlackoutWindow(this).showWindow(tab.Header as string ?? tab.Tag as string);
            this.Focus();//チューナー画面やEPG画面でのフォーカス対策。とりあえずこれで解決する。
            tab.IsSelected = false;//必ずOnVisibleChanged()を発生させるため。
            tab.IsSelected = true;
        }

        public void EmphasizeButton(bool emphasize, string buttonID)
        {
            Button button1 = buttonList[buttonID];

            //検索ボタンを点滅させる
            if (emphasize && Settings.Instance.ViewButtonShowAsTab == false)
            {
                if (stackPanel_button.Children.Contains(button1) == false)
                {
                    stackPanel_button.Children.Add(button1);
                }
                button1.Effect = new DropShadowEffect();
                var animation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.7,
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true
                };
                button1.BeginAnimation(Button.OpacityProperty, animation);
            }
            else
            {
                //ストックのボタンは削除されないので、一応このコードは毎回実行させることにする。
                button1.BeginAnimation(Button.OpacityProperty, null);
                button1.Opacity = 1;
                button1.Effect = null;
                if (Settings.Instance.ViewButtonList.Contains(buttonID) == false)
                {
                    stackPanel_button.Children.Remove(button1);
                }
            }

            //もしあればタブとして表示のタブも点滅させる
            if (Settings.Instance.ViewButtonShowAsTab == true)
            {
                var ti = tabControl_main.Items.OfType<TabItem>().FirstOrDefault(item => item.Uid == buttonID);
                if (emphasize)
                {
                    if (ti == null) ti = TabButtonAdd(buttonID);
                    var animation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.1,
                        RepeatBehavior = RepeatBehavior.Forever,
                        AutoReverse = true
                    };
                    ti.BeginAnimation(TabItem.OpacityProperty, animation);
                }
                else if (ti != null)
                {
                    if (Settings.Instance.ViewButtonList.Contains(buttonID) == false)
                    {
                        tabControl_main.Items.Remove(ti);
                    }
                    else
                    {
                        ti.BeginAnimation(TabItem.OpacityProperty, null);
                        ti.Opacity = 1;
                    }
                }
                ViewUtil.TabControlHeaderCopy(tabControl_main, tabEpgDummy);
            }
        }

        /// <summary>
        /// 予約情報の更新通知
        /// </summary>
        public void UpdateReserveTab()
        {
            bool ReserveError = CommonManager.Instance.DB.ReserveList.Values.Any(info => info.OverlapMode == 2);
            bool ReserveWarning = CommonManager.Instance.DB.ReserveList.Values.Any(info => info.OverlapMode == 1);
            res_icon_Error.Visibility = ReserveError == true ? Visibility.Visible : Visibility.Collapsed;
            res_icon_Warning.Visibility = (ReserveError == false && ReserveWarning == true) ? Visibility.Visible : Visibility.Collapsed;

            //予約一覧のタブにツールチップを追加
            string tooltip = ((ReserveError == true ? "チューナー不足(録画不可)あり" : "") + "\r\n" +
                                    (ReserveWarning == true ? "チューナー不足(一部録画)あり" : "")).Trim();
            grid_res_Header.ToolTip = tooltip != "" ? tooltip : null;
        }

        public void ListFoucsOnVisibleChanged()
        {
            if (this.reserveView.listView_reserve.IsVisible == true)
            {
                this.reserveView.listView_reserve.Focus();
            }
            else if (this.recInfoView.listView_recinfo.IsVisible == true)
            {
                this.recInfoView.listView_recinfo.Focus();
            }
            else if (this.autoAddView.epgAutoAddView.listView_key.IsVisible == true)
            {
                this.autoAddView.epgAutoAddView.listView_key.Focus();
            }
            else if (this.autoAddView.manualAutoAddView.listView_key.IsVisible == true)
            {
                this.autoAddView.manualAutoAddView.listView_key.Focus();
            }
        }
    }

    /// <summary>アプリケーション全体に適用する拡張コンテキストメニュー</summary>
    public class ContextMenuEx : ContextMenu
    {
        public ContextMenuEx()
        {
            if (Settings.ContextMenuResourceDictionary != null)
            {
                Resources.MergedDictionaries.Add(Settings.ContextMenuResourceDictionary);
            }
        }
    }
}
