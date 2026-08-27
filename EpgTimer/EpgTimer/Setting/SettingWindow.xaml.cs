using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EpgTimer
{
    using Setting;

    /// <summary>
    /// SettingWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingWindow : AttendantWindow
    {
        private HashSet<string> msgSet = new HashSet<string>();

        public static void UpdatesInfo(string msg = null)
        {
            foreach (var win in Application.Current.Windows.OfType<SettingWindow>())
            {
                win.SetReload(true, msg);
            }
        }
        private void SetReload(bool reload, string msg = null)
        {
            if (string.IsNullOrWhiteSpace(msg) == false) msgSet.Add(msg);
            if (reload == false) msgSet.Clear();
            button_Reload.Content = "再読込" + (reload == false ? "" : "*");
            button_Reload.ToolTip = reload == false ? null :
                ("他の操作により設定が変更されています" + (msgSet.Count == 0 ? null : "\r\n *" + string.Join("\r\n *", msgSet)));
        }

        public enum SettingMode { Default, ReserveSetting, TunerSetting, RecInfoSetting, EpgSetting, EpgTabSetting }
        public SettingMode Mode { get; private set; }

        public SettingWindow(SettingMode mode = SettingMode.Default, object param = null)
        {
            InitializeComponent();

            //設定ウィンドウについては最低サイズを決めておく。
            if (Height < 650) Height = 650;
            if (Width < 780) Width = 780;

            base.SetParam(false, new CheckBox());
            this.Pinned = true;

            if (CommonManager.Instance.NWMode)
            {
                button_srvSetting.IsEnabled = false;
            }
            button_srvSetting.Click += (sender, e) => CommonManager.OpenSrvSetting();

            button_Reload.Click += (sender, e) => LoadSetting();
            button_Apply.Click += (sender, e) => { Apply(); LoadSetting(); };
            button_OK.Click += (sender, e) => { this.Close(); Apply(); };
            button_cancel.Click += (sender, e) => this.Close();

            LoadSetting(CommonManager.Instance.IsConnected);
            SetMode(mode, param);
        }

        public void LoadSetting(bool init = false)
        {
            try
            {
                DataContext = Settings.Instance.DeepCloneStaticSettings();
                if (init == true) CheckServiceSettings((Settings)DataContext, false, true);
                setBasicView.LoadSetting();
                setAppView.LoadSetting();
                setEpgView.LoadSetting();
                setOtherAppView.LoadSetting();
                SetReload(false);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        public void SetMode(SettingMode mode, object param)
        {
            Mode = mode;
            switch (mode)
            {
                case SettingMode.ReserveSetting:
                    tabItem_epgView.IsSelected = true;
                    setEpgView.tabReserve.IsSelected = true;
                    setEpgView.tabReserveBasic.IsSelected = true;
                    break;
                case SettingMode.TunerSetting:
                    tabItem_epgView.IsSelected = true;
                    setEpgView.tabTuner.IsSelected = true;
                    setEpgView.tabTunerBasic.IsSelected = true;
                    break;
                case SettingMode.RecInfoSetting:
                    tabItem_epgView.IsSelected = true;
                    setEpgView.tabRecInfo.IsSelected = true;
                    break;
                case SettingMode.EpgSetting:
                    tabItem_epgView.IsSelected = true;
                    setEpgView.tabEpg.IsSelected = true;
                    setEpgView.tabEpgBasic.IsSelected = true;
                    break;
                case SettingMode.EpgTabSetting:
                    tabItem_epgView.IsSelected = true;
                    setEpgView.tabEpg.IsSelected = true;
                    setEpgView.tabEpgTab.IsSelected = true;
                    setEpgView.listBox_tab.SelectedItem = setEpgView.listBox_tab.Items.OfType<CustomEpgTabInfoView>().FirstOrDefault(item => item.Info.Uid == param as string);
                    break;
            }
        }

        private void Apply()
        {
            try
            {
                setBasicView.SaveSetting();
                setAppView.SaveSetting();
                setEpgView.SaveSetting();
                setOtherAppView.SaveSetting();
                ((Settings)DataContext).SaveIniOptions();

                Settings.Instance.ShallowCopyDynamicSettingsTo((Settings)DataContext);
                Settings.Instance = (Settings)DataContext;
                SettingWindow.UpdatesInfo("別画面/PCでの設定更新");//基本的に一つしか使わないが一応通知

                if (CommonManager.Instance.NWMode == false)
                {
                    ChSet5.SaveFile();
                }
                if (Settings.Instance.WakeUpHdd == false) CommonManager.WakeUpHDDLogClear();
                ItemFontCache.Clear();

                CommonManager.MainWindow.SaveData(true);
                CommonManager.MainWindow.RefreshSetting(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                MessageBox.Show("不正な入力値によるエラーのため、一部設定のみ更新されました。");
            }
        }

        public static bool CheckServiceSettings(Settings settings, bool apply, bool onSettingWindow = false)
        {
            //サービス一覧に見つからず、TSIDを無視すれば見つかるサービスがないか調べる
            bool isDetected = false;
            string searchDefault = "";
            foreach(SearchPresetItem info in settings.SearchPresetList)
            {
                for (int i = 0; i < info.Data.serviceList.Count; i++)
                {
                    isDetected |= CheckService(apply, (ulong)info.Data.serviceList[i], newId => info.Data.serviceList[i] = (long)newId, ref searchDefault);
                }
            }

            string viewService = "";
            string searchKey = "";
            foreach (CustomEpgTabInfo info in settings.CustomEpgTabList)
            {
                for (int i = 0; i < info.ViewServiceList.Count; i++)
                {
                    isDetected |= CheckService(apply, info.ViewServiceList[i], newId => info.ViewServiceList[i] = newId, ref viewService);
                }
                for (int i = 0; i < info.SearchKey.serviceList.Count; i++)
                {
                    isDetected |= CheckService(apply, (ulong)info.SearchKey.serviceList[i], newId => info.SearchKey.serviceList[i] = (long)newId, ref searchKey);
                }
            }

            string iepgStation = "";
            foreach (IEPGStationInfo info in settings.IEpgStationList)
            {
                isDetected |= CheckService(apply, info.Key, newId => info.Key = newId, ref iepgStation);
            }

            if (!apply && isDetected)
            {
                var msg1 = onSettingWindow ? "変更を設定ウィンドウに適用しますか？\r\n（「適用」ボタンを押すか、「OK」ボタンにより設定ウィンドウを閉じるまで変更は保存されません）" : "変更を適用しますか？";
                var msg2 = onSettingWindow ? "設定" : "EpgTimer";
                if (MessageBox.Show("TransportStreamIDの変更を検出しました。\r\n\r\n" +
                                    (searchDefault != "" ? "【検索プリセットのサービス絞り込み】\r\n" : "") + searchDefault +
                                    (viewService != "" ? "【番組表の表示条件の表示サービス】\r\n" : "") + viewService +
                                    (searchKey != "" ? "【番組表の表示条件の検索条件のサービス絞り込み】\r\n" : "") + searchKey +
                                    (iepgStation != "" ? "【iEPG Ver.1の放送局リスト】\r\n" : "") + iepgStation +
                                    "\r\n" + msg1, msg2 + " - サービス情報変更の検出"
                                    , MessageBoxButton.OKCancel, MessageBoxImage.Information, MessageBoxResult.Cancel) == MessageBoxResult.OK)
                {
                    return CheckServiceSettings(settings, true, onSettingWindow);
                }
            }
            return apply && isDetected;
        }
        private static bool CheckService(bool apply, ulong id, Action<ulong> SetNewId, ref string log)
        {
            EpgServiceInfo item = ChSet5.ChItem(id, true, true);
            if (item.Key != id)
            {
                if (apply)
                {
                    SetNewId(item.Key);
                }
                else if (log.Count(c => c == '\n') < 5)
                {
                    log += "  ID=0x" + id.ToString("X12") + " -> 0x" + item.Key.ToString("X12") + " (" + item.service_name + ")\r\n";
                }
                else if (log.EndsWith(".\r\n", StringComparison.Ordinal) == false)
                {
                    log += "  ...\r\n";
                }
                return true;
            }
            return false;
        }
    }
}
