using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.IO;
using System.Reflection;

namespace EpgTimer.Setting
{
    using BoxExchangeEdit;

    /// <summary>
    /// SetAppView.xaml の相互作用ロジック
    /// </summary>
    public partial class SetAppView : UserControl
    {
        private Settings settings { get { return (Settings)DataContext; } }

        BoxExchangeEditor bxb;
        BoxExchangeEditor bxt;
        private List<string> buttonItem = Settings.GetViewButtonAllIDs();
        private List<string> taskItem = Settings.GetTaskMenuAllIDs();

        private RadioBtnSelect delReserveModeRadioBtns;

        public SetAppView()
        {
            InitializeComponent();

            if (CommonManager.Instance.NWMode == true)
            {
                tabItem1.Foreground = SystemColors.GrayTextBrush;
                grid_AppRecEnd.IsEnabled = false;
                grid_AppRec.IsEnabled = false;
                ViewUtil.SetIsEnabledChildren(grid_AppCancelMain, false);
                ViewUtil.SetIsEnabledChildren(grid_AppCancelMainInput, false);
                textBox_process.SetReadOnlyWithEffect(true);

                ViewUtil.SetIsEnabledChildren(grid_AppReserve1, false);
                ViewUtil.SetIsEnabledChildren(grid_AppReserve2, false);
                ViewUtil.SetIsEnabledChildren(grid_AppReserveIgnore, false);
                text_RecInfo2RegExp.SetReadOnlyWithEffect(true);
                checkBox_autoDel.IsEnabled = false;
                ViewUtil.SetIsEnabledChildren(grid_App2DelMain, false);
                listBox_ext.IsEnabled = true;
                textBox_ext.SetReadOnlyWithEffect(true);
                grid_App2DelChkFolderText.IsEnabled = true;
                listBox_chk_folder.IsEnabled = true;
                textBox_chk_folder.SetReadOnlyWithEffect(true);
                button_chk_open.IsEnabled = true;

                grid_recname.IsEnabled = false;
                checkBox_noChkYen.IsEnabled = false;
                grid_delReserve.IsEnabled = false;

                checkBox_wakeReconnect.IsEnabled = true;
                stackPanel_WoLWait.IsEnabled = true;
                checkBox_suspendClose.IsEnabled = true;
                checkBox_keepTCPConnect.IsEnabled = true;
                grid_srvResident.IsEnabled = false;
                label_shortCutSrv.IsEnabled = false;
                button_shortCutSrv.IsEnabled = false;
                checkBox_srvSaveNotifyLog.IsEnabled = false;
                checkBox_srvSaveDebugLog.IsEnabled = false;
                grid_tsExt.IsEnabled = false;
            }

            //0 全般
            var SetScButton = new Action<Button, string, string>((btn, baseName, scLinkPath) =>
            {
                string scPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), baseName + ".lnk");
                btn.Content = File.Exists(scPath) ? "削除" : "作成";
                btn.Click += (sender, e) =>
                {
                    try
                    {
                        if (File.Exists(scPath))
                        {
                            File.Delete(scPath);
                        }
                        else
                        {
                            CommonUtil.CreateShortCut(scPath, scLinkPath, "");
                        }
                        btn.Content = File.Exists(scPath) ? "削除" : "作成";
                    }
                    catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                };
            });
            SetScButton(button_shortCut, Path.GetFileNameWithoutExtension(SettingPath.ModuleName), Path.Combine(SettingPath.ModulePath, SettingPath.ModuleName));
            SetScButton(button_shortCutSrv, "EpgTimerSrv", Path.Combine(SettingPath.ModulePath, "EpgTimerSrv.exe"));

            //1 録画動作
            RadioButtonTagConverter.SetBindingButtons(CommonUtil.NameOf(() => settings.DefRecEndMode), panel_recEndMode);
            button_process_open.Click += ViewUtil.OpenFileNameDialog(textBox_process, true, "", ".exe");
            comboBox_process.Items.AddItems(new[] { "リアルタイム", "高", "通常以上", "通常", "通常以下", "低" });

            var bx = new BoxExchangeEditor(null, listBox_process, true);
            listBox_process.SelectionChanged += ViewUtil.ListBox_TextBoxSyncSelectionChanged(listBox_process, textBox_process);
            if (CommonManager.Instance.NWMode == false)
            {
                bx.AllowKeyAction();
                bx.AllowDragDrop();
                button_process_del.Click += bx.button_Delete_Click;
                button_process_add.Click += ViewUtil.ListBox_TextCheckAdd(listBox_process, textBox_process);
                textBox_process.KeyDown += ViewUtil.KeyDown_Enter(button_process_add);
            }

            //2 予約管理情報
            button_chk_open.Click += ViewUtil.OpenFolderNameDialog(textBox_chk_folder, "自動削除対象フォルダの選択", true);

            var bxe = new BoxExchangeEditor(null, listBox_ext, true);
            var bxc = new BoxExchangeEditor(null, listBox_chk_folder, true);
            listBox_ext.SelectionChanged += ViewUtil.ListBox_TextBoxSyncSelectionChanged(listBox_ext, textBox_ext);
            bxc.TargetBox.SelectionChanged += ViewUtil.ListBox_TextBoxSyncSelectionChanged(bxc.TargetBox, textBox_chk_folder);
            bxc.TargetBox.KeyDown += ViewUtil.KeyDown_Enter(button_chk_open);
            bxc.targetBoxAllowDoubleClick(bxc.TargetBox, (sender, e) => button_chk_open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            if (CommonManager.Instance.NWMode == false)
            {
                bxe.AllowKeyAction();
                bxe.AllowDragDrop();
                button_ext_del.Click += bxe.button_Delete_Click;
                button_ext_add.Click += ViewUtil.ListBox_TextCheckAdd(listBox_ext, textBox_ext);
                bxc.AllowKeyAction();
                bxc.AllowDragDrop();
                button_chk_del.Click += bxc.button_Delete_Click;
                button_chk_add.Click += (sender, e) => textBox_chk_folder.Text = SettingPath.CheckFolder(textBox_chk_folder.Text);
                button_chk_add.Click += ViewUtil.ListBox_TextCheckAdd(listBox_chk_folder, textBox_chk_folder);

                textBox_ext.KeyDown += ViewUtil.KeyDown_Enter(button_ext_add);
                textBox_chk_folder.KeyDown += ViewUtil.KeyDown_Enter(button_chk_add);
            }

            //3 ボタン表示 ボタン表示画面の上下ボタンのみ他と同じものを使用する。
            bxb = new BoxExchangeEditor(this.listBox_itemBtn, this.listBox_viewBtn, true);
            bxt = new BoxExchangeEditor(this.listBox_itemTask, this.listBox_viewTask, true);
            textblockTimer.Text = CommonManager.Instance.NWMode == true ?
                "EpgTimerNW側の設定です。" :
                "録画終了時にスタンバイ、休止する場合は必ず表示されます(ただし、サービス未使用時はこの設定は使用されず15秒固定)。";

            //上部表示ボタン関係
            bxb.AllowDuplication(StringItem.Items(Settings.ViewButtonSpacer), StringItem.Cloner, StringItem.Comparator);
            button_btnUp.Click += bxb.button_Up_Click;
            button_btnDown.Click += bxb.button_Down_Click;
            button_btnAdd.Click += (sender, e) => button_Add(bxb, buttonItem);
            button_btnIns.Click += (sender, e) => button_Add(bxb, buttonItem, true);
            button_btnDel.Click += (sender, e) => button_Dell(bxb, bxt, buttonItem);
            bxb.sourceBoxAllowKeyAction(listBox_itemBtn, (sender, e) => button_btnAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.targetBoxAllowKeyAction(listBox_viewBtn, (sender, e) => button_btnDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.sourceBoxAllowDoubleClick(listBox_itemBtn, (sender, e) => button_btnAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.targetBoxAllowDoubleClick(listBox_viewBtn, (sender, e) => button_btnDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.sourceBoxAllowDragDrop(listBox_itemBtn, (sender, e) => button_btnDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxb.targetBoxAllowDragDrop(listBox_viewBtn, (sender, e) => drag_drop(sender, e, button_btnAdd, button_btnIns));

            //タスクアイコン関係
            bxt.AllowDuplication(StringItem.Items(Settings.TaskMenuSeparator), StringItem.Cloner, StringItem.Comparator);
            button_taskUp.Click += bxt.button_Up_Click;
            button_taskDown.Click += bxt.button_Down_Click;
            button_taskAdd.Click += (sender, e) => button_Add(bxt, taskItem);
            button_taskIns.Click += (sender, e) => button_Add(bxt, taskItem, true);
            button_taskDel.Click += (sender, e) => button_Dell(bxt, bxb, taskItem);
            bxt.sourceBoxAllowKeyAction(listBox_itemTask, (sender, e) => button_taskAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.targetBoxAllowKeyAction(listBox_viewTask, (sender, e) => button_taskDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.sourceBoxAllowDoubleClick(listBox_itemTask, (sender, e) => button_taskAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.targetBoxAllowDoubleClick(listBox_viewTask, (sender, e) => button_taskDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.sourceBoxAllowDragDrop(listBox_itemTask, (sender, e) => button_taskDel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxt.targetBoxAllowDragDrop(listBox_viewTask, (sender, e) => drag_drop(sender, e, button_taskAdd, button_taskIns));

            //4 カスタムボタン
            button_exe1.Click += ViewUtil.OpenFileNameDialog(textBox_exe1, false, "", ".exe");
            button_exe2.Click += ViewUtil.OpenFileNameDialog(textBox_exe2, false, "", ".exe");
            button_exe3.Click += ViewUtil.OpenFileNameDialog(textBox_exe3, false, "", ".exe");

            //5 iEpg キャンセルアクションだけは付けておく
            new BoxExchangeEditor(null, this.listBox_service, true);
            var bxi = new BoxExchangeEditor(null, this.listBox_iEPG, true);
            bxi.targetBoxAllowKeyAction(this.listBox_iEPG, (sender, e) => button_del.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            bxi.TargetBox.SelectionChanged += ViewUtil.ListBox_TextBoxSyncSelectionChanged(bxi.TargetBox, textBox_station);
            textBox_station.KeyDown += ViewUtil.KeyDown_Enter(button_add);
        }

        public void LoadSetting()
        {
            //0 全般
            int residentMode = IniFileHandler.GetPrivateProfileInt("SET", "ResidentMode", 2, SettingPath.TimerSrvIniPath);
            checkBox_srvResident.IsChecked = residentMode >= 1;
            checkBox_srvShowTray.IsChecked = residentMode >= 2;
            checkBox_NotifyTipStyle.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "NotifyTipStyle", false, SettingPath.TimerSrvIniPath);
            checkBox_blinkPreRec.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "BlinkPreRec", false, SettingPath.TimerSrvIniPath);
            int NoBalloonTip = IniFileHandler.GetPrivateProfileInt("SET", "NoBalloonTip", 0, SettingPath.TimerSrvIniPath);
            checkBox_srvBalloonTip.IsChecked = NoBalloonTip != 1;
            checkBox_srvBalloonTipRealtime.IsChecked = NoBalloonTip == 2;

            checkBox_srvSaveNotifyLog.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "SaveNotifyLog", false, SettingPath.TimerSrvIniPath);
            checkBox_srvSaveDebugLog.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "SaveDebugLog", false, SettingPath.TimerSrvIniPath);
            textBox_tsExt.Text = SettingPath.CheckTSExtension(IniFileHandler.GetPrivateProfileString("SET", "TSExt", ".ts", SettingPath.TimerSrvIniPath));

            //1 録画動作
            textBox_pcWakeTime.Text = IniFileHandler.GetPrivateProfileInt("SET", "WakeTime", 5, SettingPath.TimerSrvIniPath).ToString();

            listBox_process.Items.Clear();
            int ngCount = IniFileHandler.GetPrivateProfileInt("NO_SUSPEND", "Count", int.MaxValue, SettingPath.TimerSrvIniPath);
            if (ngCount == int.MaxValue)
            {
                listBox_process.Items.Add("EpgDataCap_Bon");
            }
            else
            {
                for (int i = 0; i < ngCount; i++)
                {
                    listBox_process.Items.Add(IniFileHandler.GetPrivateProfileString("NO_SUSPEND", i.ToString(), "", SettingPath.TimerSrvIniPath));
                }
            }
            textBox_ng_min.Text = IniFileHandler.GetPrivateProfileString("NO_SUSPEND", "NoStandbyTime", "10", SettingPath.TimerSrvIniPath);
            checkBox_ng_usePC.IsChecked = IniFileHandler.GetPrivateProfileBool("NO_SUSPEND", "NoUsePC", false, SettingPath.TimerSrvIniPath);
            textBox_ng_usePC_min.Text = IniFileHandler.GetPrivateProfileString("NO_SUSPEND", "NoUsePCTime", "3", SettingPath.TimerSrvIniPath);
            checkBox_ng_fileStreaming.IsChecked = IniFileHandler.GetPrivateProfileBool("NO_SUSPEND", "NoFileStreaming", false, SettingPath.TimerSrvIniPath);
            checkBox_ng_shareFile.IsChecked = IniFileHandler.GetPrivateProfileBool("NO_SUSPEND", "NoShareFile", false, SettingPath.TimerSrvIniPath);

            checkBox_appMin.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "RecMinWake", true, SettingPath.TimerSrvIniPath);
            int recView= IniFileHandler.GetPrivateProfileInt("SET", "RecView", 1, SettingPath.TimerSrvIniPath);
            checkBox_appOpenViewing.IsChecked = (recView & 1) != 0;
            checkBox_appOpenRec.IsChecked = (recView & 2) != 0;
            checkBox_appOpenAlways.IsChecked = (recView & 4) != 0;
            checkBox_appDrop.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "DropLog", true, SettingPath.TimerSrvIniPath);
            checkBox_addPgInfo.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "PgInfoLog", true, SettingPath.TimerSrvIniPath);
            checkBox_PgInfoLogAsUtf8.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "PgInfoLogAsUtf8", false, SettingPath.TimerSrvIniPath);
            checkBox_appNW.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "RecNW", false, SettingPath.TimerSrvIniPath);
            checkBox_appKeepDisk.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "KeepDisk", true, SettingPath.TimerSrvIniPath);
            checkBox_appOverWrite.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "RecOverWrite", false, SettingPath.TimerSrvIniPath);
            comboBox_process.SelectedIndex = IniFileHandler.GetPrivateProfileInt("SET", "ProcessPriority", 3, SettingPath.TimerSrvIniPath);
            
            //2 予約管理情報
            checkBox_back_priority.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "BackPriority", true, SettingPath.TimerSrvIniPath);
            checkBox_fixedTunerPriority.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "FixedTunerPriority", true, SettingPath.TimerSrvIniPath);
            text_RecInfo2RegExp.Text = IniFileHandler.GetPrivateProfileString("SET", "RecInfo2RegExp", "", SettingPath.TimerSrvIniPath);
            checkBox_RetryOtherTuners.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "RetryOtherTuners", false, SettingPath.TimerSrvIniPath);
            checkBox_CommentAutoAdd.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "CommentAutoAdd", false, SettingPath.TimerSrvIniPath);
            checkBox_FixNoRecToServiceOnly.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "FixNoRecToServiceOnly", false, SettingPath.TimerSrvIniPath);
            checkBox_recInfoFolderOnly.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "RecInfoFolderOnly", true, SettingPath.TimerSrvIniPath);
            checkBox_autoDelRecInfo.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "AutoDelRecInfo", false, SettingPath.TimerSrvIniPath);
            textBox_autoDelRecInfo.Text = IniFileHandler.GetPrivateProfileInt("SET", "AutoDelRecInfoNum", 100, SettingPath.TimerSrvIniPath).ToString();
            textBox_RecInfo2Max.Text = IniFileHandler.GetPrivateProfileInt("SET", "RecInfo2Max", 1000, SettingPath.TimerSrvIniPath).ToString();
            textBox_RecInfo2DropChk.Text = IniFileHandler.GetPrivateProfileInt("SET", "RecInfo2DropChk", 2, SettingPath.TimerSrvIniPath).ToString();
            checkBox_recInfoDelFile.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "RecInfoDelFile", false, SettingPath.CommonIniPath);
            checkBox_applyExtTo.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "ApplyExtToRecInfoDel", false, SettingPath.TimerSrvIniPath);
            checkBox_autoDel.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "AutoDel", false, SettingPath.TimerSrvIniPath);

            listBox_ext.Items.Clear();
            int count;
            count = IniFileHandler.GetPrivateProfileInt("DEL_EXT", "Count", int.MaxValue, SettingPath.TimerSrvIniPath);
            if (count == int.MaxValue)
            {
                button_ext_def_Click(null, null);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    listBox_ext.Items.Add(IniFileHandler.GetPrivateProfileString("DEL_EXT", i.ToString(), "", SettingPath.TimerSrvIniPath));
                }
            }
            listBox_chk_folder.Items.Clear();
            count = IniFileHandler.GetPrivateProfileInt("DEL_CHK", "Count", 0, SettingPath.TimerSrvIniPath);
            for (int i = 0; i < count; i++)
            {
                listBox_chk_folder.Items.Add(IniFileHandler.GetPrivateProfileFolder("DEL_CHK", i.ToString(), SettingPath.TimerSrvIniPath));
            }

            checkBox_recname.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "RecNamePlugIn", false, SettingPath.TimerSrvIniPath);
            if (CommonManager.Instance.IsConnected == true)
            {
                CommonManager.Instance.DB.ReloadPlugInFile();
            }
            comboBox_recname.ItemsSource = CommonManager.Instance.DB.RecNamePlugInList;
            comboBox_recname.SelectedItem = IniFileHandler.GetPrivateProfileString("SET", "RecNamePlugInFile", "", SettingPath.TimerSrvIniPath);
            if (comboBox_recname.SelectedIndex < 0) comboBox_recname.SelectedIndex = 0;

            checkBox_noChkYen.IsChecked = IniFileHandler.GetPrivateProfileBool("SET", "NoChkYen", false, SettingPath.TimerSrvIniPath);
            delReserveModeRadioBtns = new RadioBtnSelect(grid_delReserve);
            delReserveModeRadioBtns.Value = IniFileHandler.GetPrivateProfileInt("SET", "DelReserveMode", 2, SettingPath.TimerSrvIniPath);

            checkBox_autoDel_Click(null, null);

            //3 ボタン表示
            listBox_viewBtn.Items.Clear();
            listBox_viewBtn.Items.AddItems(StringItem.Items(settings.ViewButtonList.Where(item => buttonItem.Contains(item) == true)));
            reLoadButtonItem(bxb, buttonItem);

            listBox_viewTask.Items.Clear();
            listBox_viewTask.Items.AddItems(StringItem.Items(settings.TaskMenuList.Where(item => taskItem.Contains(item) == true)));
            reLoadButtonItem(bxt, taskItem);

            //5 iEpg
            listBox_service.ItemsSource = ChSet5.ChListSelected.Select(info => new ServiceViewItem(info));
        }

        public void SaveSetting()
        {
            //0 全般
            IniFileHandler.WritePrivateProfileString("SET", "ResidentMode",
                checkBox_srvResident.IsChecked == false ? 0 : checkBox_srvShowTray.IsChecked == false ? 1 : 2, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "NotifyTipStyle", checkBox_NotifyTipStyle.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "BlinkPreRec", checkBox_blinkPreRec.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "NoBalloonTip", checkBox_srvBalloonTip.IsChecked != true ?
                1 : checkBox_srvBalloonTipRealtime.IsChecked == true ? 2 : 0, SettingPath.TimerSrvIniPath);

            IniFileHandler.WritePrivateProfileString("SET", "SaveNotifyLog", checkBox_srvSaveNotifyLog.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "SaveDebugLog", checkBox_srvSaveDebugLog.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "TSExt", SettingPath.CheckTSExtension(textBox_tsExt.Text), SettingPath.TimerSrvIniPath);

            //1 録画動作
            IniFileHandler.WritePrivateProfileString("SET", "WakeTime", textBox_pcWakeTime.Text, SettingPath.TimerSrvIniPath);

            List<string> ngProcessList = listBox_process.Items.OfType<string>().ToList();
            IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "Count", ngProcessList.Count, SettingPath.TimerSrvIniPath);
            IniFileHandler.DeletePrivateProfileNumberKeys("NO_SUSPEND", SettingPath.TimerSrvIniPath);
            for (int i = 0; i < ngProcessList.Count; i++)
            {
                IniFileHandler.WritePrivateProfileString("NO_SUSPEND", i.ToString(), ngProcessList[i], SettingPath.TimerSrvIniPath);
            }

            IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoStandbyTime", textBox_ng_min.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoUsePC", checkBox_ng_usePC.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoUsePCTime", textBox_ng_usePC_min.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoFileStreaming", checkBox_ng_fileStreaming.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("NO_SUSPEND", "NoShareFile", checkBox_ng_shareFile.IsChecked, SettingPath.TimerSrvIniPath);

            IniFileHandler.WritePrivateProfileString("SET", "RecMinWake", checkBox_appMin.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecView"
                , Convert.ToInt32(checkBox_appOpenViewing.IsChecked)
                + Convert.ToInt32(checkBox_appOpenRec.IsChecked) * 2
                + Convert.ToInt32(checkBox_appOpenAlways.IsChecked) * 4, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "DropLog", checkBox_appDrop.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "PgInfoLog", checkBox_addPgInfo.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "PgInfoLogAsUtf8", checkBox_PgInfoLogAsUtf8.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecNW", checkBox_appNW.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "KeepDisk", checkBox_appKeepDisk.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecOverWrite", checkBox_appOverWrite.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "ProcessPriority", comboBox_process.SelectedIndex, SettingPath.TimerSrvIniPath);

            //2 予約管理情報
            IniFileHandler.WritePrivateProfileString("SET", "BackPriority", checkBox_back_priority.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "FixedTunerPriority", checkBox_fixedTunerPriority.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RetryOtherTuners", checkBox_RetryOtherTuners.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "CommentAutoAdd", checkBox_CommentAutoAdd.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "FixNoRecToServiceOnly", checkBox_FixNoRecToServiceOnly.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecInfoFolderOnly", checkBox_recInfoFolderOnly.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecInfo2RegExp", text_RecInfo2RegExp.Text, "", SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "AutoDelRecInfo", checkBox_autoDelRecInfo.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "AutoDelRecInfoNum", textBox_autoDelRecInfo.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecInfo2Max", textBox_RecInfo2Max.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecInfo2DropChk", textBox_RecInfo2DropChk.Text, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecInfoDelFile", checkBox_recInfoDelFile.IsChecked, false, SettingPath.CommonIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "ApplyExtToRecInfoDel", checkBox_applyExtTo.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "AutoDel", checkBox_autoDel.IsChecked, SettingPath.TimerSrvIniPath);

            List<string> extList = listBox_ext.Items.OfType<string>().ToList();
            List<string> delChkFolderList = ViewUtil.GetFolderList(listBox_chk_folder);
            IniFileHandler.WritePrivateProfileString("DEL_EXT", "Count", extList.Count, SettingPath.TimerSrvIniPath);
            IniFileHandler.DeletePrivateProfileNumberKeys("DEL_EXT", SettingPath.TimerSrvIniPath);
            for (int i = 0; i < extList.Count; i++)
            {
                IniFileHandler.WritePrivateProfileString("DEL_EXT", i.ToString(), extList[i], SettingPath.TimerSrvIniPath);
            }
            IniFileHandler.WritePrivateProfileString("DEL_CHK", "Count", delChkFolderList.Count, SettingPath.TimerSrvIniPath);
            IniFileHandler.DeletePrivateProfileNumberKeys("DEL_CHK", SettingPath.TimerSrvIniPath);
            for (int i = 0; i < delChkFolderList.Count; i++)
            {
                IniFileHandler.WritePrivateProfileString("DEL_CHK", i.ToString(), delChkFolderList[i], SettingPath.TimerSrvIniPath);
            }

            IniFileHandler.WritePrivateProfileString("SET", "RecNamePlugIn", checkBox_recname.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecNamePlugInFile", comboBox_recname.SelectedItem, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "NoChkYen", checkBox_noChkYen.IsChecked, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "DelReserveMode", delReserveModeRadioBtns.Value, -1, SettingPath.TimerSrvIniPath);

            //3 ボタン表示
            settings.ViewButtonList = listBox_viewBtn.Items.OfType<StringItem>().ValueList();
            settings.TaskMenuList = listBox_viewTask.Items.OfType<StringItem>().ValueList();
        }

        private void button_ext_def_Click(object sender, RoutedEventArgs e)
        {
            ViewUtil.ListBox_TextCheckAdd(listBox_ext, ".ts.err");
            ViewUtil.ListBox_TextCheckAdd(listBox_ext, ".ts.program.txt");
        }

        private void button_recname_Click(object sender, RoutedEventArgs e)
        {
            CommonManager.ShowPlugInSetting(comboBox_recname.SelectedItem as string, "RecName", this);
        }

        private void drag_drop(object sender, DragEventArgs e, Button add, Button ins)
        {
            var handler = (BoxExchangeEditor.GetDragHitItem(sender, e) == null ? add : ins);
            handler.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private void button_Add(BoxExchangeEditor bx, List<string> src, bool isInsert = false)
        {
            int pos = bx.SourceBox.SelectedIndex - bx.SourceBox.SelectedItems.Count;
            bx.bxAddItems(bx.SourceBox, bx.TargetBox, isInsert);
            reLoadButtonItem(bx, src);
            if (bx.SourceBox.Items.Count != 0)
            {
                pos = Math.Max(0, Math.Min(pos, bx.SourceBox.Items.Count - 1));
                bx.SourceBox.SelectedIndex = pos;//順序がヘンだが、ENTERの場合はこの後に+1処理が入る模様
            }
        }
        private void button_Dell(BoxExchangeEditor bx, BoxExchangeEditor bx_other, List<string> src)
        {
            if (bx.TargetBox.SelectedItem == null) return;
            //
            var item1 = bx.TargetBox.SelectedItems.OfType<StringItem>().FirstOrDefault(item => item.Value == "設定");
            var item2 = bx_other.TargetBox.Items.OfType<StringItem>().FirstOrDefault(item => item.Value == "設定");
            if (item1 != null && item2 == null)
            {
                MessageBox.Show("設定は上部表示ボタンか右クリック表示項目のどちらかに必要です");
                return;
            }

            bx.bxDeleteItems(bx.TargetBox);
            reLoadButtonItem(bx, src);
        }
        private void button_btnIni_Click(object sender, RoutedEventArgs e)
        {
            listBox_viewBtn.Items.Clear();
            listBox_viewBtn.Items.AddItems(StringItem.Items(Settings.GetViewButtonDefIDs(CommonManager.Instance.NWMode)));
            reLoadButtonItem(bxb, buttonItem);
        }
        private void button_taskIni_Click(object sender, RoutedEventArgs e)
        {
            listBox_viewTask.Items.Clear();
            listBox_viewTask.Items.AddItems(StringItem.Items(Settings.GetTaskMenuDefIDs(CommonManager.Instance.NWMode)));
            reLoadButtonItem(bxt, taskItem);
        }
        private void reLoadButtonItem(BoxExchangeEditor bx, List<string> src)
        {
            var viewlist = bx.TargetBox.Items.OfType<StringItem>().Values();
            var diflist = src.Except(viewlist).ToList();
            diflist.Insert(0, (bx.DuplicationSpecific.First() as StringItem).Value);

            bx.SourceBox.ItemsSource = StringItem.Items(diflist.Distinct());
        }

        private void button_recDef_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetRecPresetWindow(this, settings.RecPresetList);
            if (dlg.ShowDialog() == true)
            {
                settings.RecPresetList = dlg.GetPresetList();
            }
        }

        private void button_searchDef_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SetSearchPresetWindow(this, settings.SearchPresetList);
            if (dlg.ShowDialog() == true)
            {
                settings.SearchPresetList = dlg.GetPresetList();
            }
        }

        private void ReLoadStation()
        {
            listBox_iEPG.Items.Clear();
            if (listBox_service.SelectedItem == null) return;
            //
            var key = (listBox_service.SelectedItem as ServiceViewItem).Key;
            listBox_iEPG.Items.AddItems(settings.IEpgStationList.Where(item => item.Key == key));
        }

        private void button_add_iepg_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_service.SelectedItem == null) return;
            //
            if (settings.IEpgStationList.Any(info => info.StationName == textBox_station.Text) == true)
            {
                MessageBox.Show("すでに追加されています");
                return;
            }
            var key = (listBox_service.SelectedItem as ServiceViewItem).Key;
            settings.IEpgStationList.Add(new IEPGStationInfo { StationName = textBox_station.Text, Key = key });
            ReLoadStation();
            listBox_iEPG.ScrollIntoViewLast();
        }

        private void button_del_iepg_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_service.SelectedItem == null) return;
            //
            listBox_iEPG.SelectedItemsList().ForEach(item => settings.IEpgStationList.Remove(item as IEPGStationInfo));
            ReLoadStation();
        }

        private void listBox_service_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReLoadStation();
        }

        private void checkBox_autoDel_Click(object sender, RoutedEventArgs e)
        {
            bool chkEnabled = (bool)checkBox_autoDel.IsChecked;
            bool extEnabled = chkEnabled || (bool)checkBox_recInfoDelFile.IsChecked && (bool)checkBox_applyExtTo.IsChecked;
            textBox_ext.SetReadOnlyWithEffect(!extEnabled);
            button_ext_def.IsEnabled = extEnabled;
            button_ext_del.IsEnabled = extEnabled;
            button_ext_add.IsEnabled = extEnabled;
            textBox_chk_folder.SetReadOnlyWithEffect(!chkEnabled);
            button_chk_del.IsEnabled = chkEnabled;
            button_chk_add.IsEnabled = chkEnabled;
        }
    }
}
