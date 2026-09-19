using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace EpgTimer
{
    using EpgView;

    /// <summary>
    /// RecInfoDescWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class RecInfoDescWindow : RecInfoDescWindowBase
    {
        protected override ulong DataID { get { return recInfo.ID; } }
        protected override IEnumerable<KeyValuePair<ulong, object>> DataRefList { get { return CommonManager.Instance.DB.RecFileInfo.Select(d => new KeyValuePair<ulong, object>(d.Key, d.Value)); } }
        protected override DataItemViewBase DataView { get { return base.DataView ?? mainWindow.recInfoView; } }

        private RecFileInfo recInfo = new RecFileInfo();
        private CmdExeRecinfo mc;

        public RecInfoDescWindow(RecFileInfo info = null)
        {
            InitializeComponent();

            if (CommonManager.Instance.NWMode == true)
            {
                label_recFilePath.IsEnabled = false;
                textBox_recFilePath.SetReadOnlyWithEffect(true);
                button_rename.ToolTip = "EpgTimerNWでは使用不可";
            }

            try
            {
                base.SetParam(false, checkBox_windowPinned, checkBox_dataReplace);

                //最初にコマンド集の初期化
                mc = new CmdExeRecinfo(this);
                mc.SetFuncGetDataList(isAll => recInfo.IntoList());

                //コマンド集に無いもの,変更するもの
                mc.AddReplaceCommand(EpgCmds.Play, (sender, e) => CommonManager.Instance.FilePlay(recInfo), (sender, e) => e.CanExecute = recInfo.ID != 0);
                mc.AddReplaceCommand(EpgCmds.Cancel, (sender, e) => this.Close());
                mc.AddReplaceCommand(EpgCmds.BackItem, (sender, e) => MoveViewNextItem(-1));
                mc.AddReplaceCommand(EpgCmds.NextItem, (sender, e) => MoveViewNextItem(1));
                mc.AddReplaceCommand(EpgCmds.Search, (sender, e) => MoveViewRecinfoTarget(), (sender, e) => e.CanExecute = DataView is EpgViewBase);
                mc.AddReplaceCommand(EpgCmds.SaveTextInDialog, (sender, e) => CommonManager.Save_ProgramText(recInfo.ProgramInfo, recInfo.RecFilePath), (sender, e) => e.CanExecute = !string.IsNullOrEmpty(recInfo.ProgramInfo));
                mc.AddReplaceCommand(EpgCmds.DeleteInDialog, info_del, (sender, e) => e.CanExecute = recInfo.ID != 0 && recInfo.ProtectFlag == 0);
                mc.AddReplaceCommand(EpgCmds.ChgOnOffCheck, (sender, e) => EpgCmds.ProtectChange.Execute(null, this));

                //コマンド集からコマンドを登録
                mc.ResetCommandBindings(this);

                //ボタンの設定
                mBinds.View = CtxmCode.RecInfoView;
                mBinds.SetCommandToButton(button_play, EpgCmds.Play);
                mBinds.SetCommandToButton(button_cancel, EpgCmds.Cancel);
                mBinds.SetCommandToButton(button_up, EpgCmds.BackItem);
                mBinds.SetCommandToButton(button_down, EpgCmds.NextItem);
                mBinds.SetCommandToButton(button_chk, EpgCmds.Search);
                mBinds.SetCommandToButton(button_save_program, EpgCmds.SaveTextInDialog);
                mBinds.SetCommandToButton(button_del, EpgCmds.DeleteInDialog);
                mBinds.AddInputCommand(EpgCmds.ProtectChange);//ショートカット登録
                RefreshMenu();

                button_del.ToolTipOpening += (sender, e) => button_del.ToolTip = (button_del.ToolTip as string +
                        (IniFileHandler.GetPrivateProfileBool("SET", "RecInfoDelFile", false, SettingPath.CommonIniPath) ?
                        "\r\n録画ファイルが存在する場合は一緒に削除されます。" : "")).Trim();

                grid_protect.ToolTipOpening += (sender, e) => grid_protect.ToolTip =
                        ("" + MenuBinds.GetInputGestureTextView(EpgCmds.ProtectChange, mBinds.View) + "\r\nプロテクト設定/解除").Trim();

                button_rename_open.Click += ViewUtil.OpenFileNameDialog(textBox_recFilePath, false, "", "", true, "", false, true);
                if (CommonManager.Instance.NWMode == false)
                {
                    textBox_recFilePath.TextChanged += textBox_recFilePath_TextChanged;
                    button_rename.Click += button_rename_Click;
                }

                //ステータスバーの設定
                this.statusBar.Status.Visibility = Visibility.Collapsed;
                StatusManager.RegisterStatusbar(this.statusBar, this);

                ChangeData(info);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        protected override bool ReloadInfoData()
        {
            if (recInfo.ID == 0) return false;

            RecFileInfo info;
            CommonManager.Instance.DB.RecFileInfo.TryGetValue(recInfo.ID, out info);
            if (info == null) recInfo.ID = 0;
            ChangeData(info ?? recInfo);
            return true;
        }

        public override void ChangeData(object data)
        {
            var info = data as RecFileInfo ?? new RecFileInfo();//nullデータを受け付ける
            DataContext = new RecInfoItem(info);

            //Appendデータが無くなる場合を考慮し、テキストはrecInfoと連動させない
            if (recInfo != data)
            {
                recInfo = info;
                recInfo.ProgramInfoSet();
                this.Title = ViewUtil.WindowTitleText(recInfo.Title, "録画情報");

                // 詳細情報を分離して表示に送る
                string[] parts = recInfo.GetProgramInfoParts();
                textBox_pgInfo.Document = CommonManager.ConvertDisplayText(parts[0], parts[1], parts[2]);
                textBox_errLog.Text = recInfo.ErrInfo;
                textBox_recFilePath.Text = recInfo.RecFilePath;
                button_rename.IsEnabled = false;
            }
            UpdateViewSelection(0);
        }

        private void info_del(object sender, ExecutedRoutedEventArgs e)
        {
            EpgCmds.Delete.Execute(e.Parameter, this);
            if (mc.IsCommandExecuted == true) MoveViewNextItem(1);
        }

        protected override void UpdateViewSelection(int mode = 0)
        {
            //番組表では「前へ」「次へ」の移動の時だけ追従させる。mode=2はアクティブ時の自動追尾
            var style = JumpItemStyle.MoveTo | (mode < 2 ? JumpItemStyle.PanelNoScroll : JumpItemStyle.None);
            if (DataView is RecInfoView)
            {
                if (mode != 0) DataView.MoveToItem(DataID, style);
            }
            else if (DataView is EpgMainViewBase)
            {
                if (mode != 2) ((EpgMainViewBase)DataView).MoveToRecInfoItem(recInfo, style);
            }
            else if (DataView is EpgListMainView)
            {
                if (mode != 0 && mode != 2) DataView.MoveToRecInfoItem(recInfo, style);
            }
            else if (DataView is SearchWindow.AutoAddWinListView)
            {
                if (mode != 0) DataView.MoveToRecInfoItem(recInfo, style);
            }
        }
        private void MoveViewRecinfoTarget()
        {
            //一覧以外では「前へ」「次へ」の移動の時に追従させる
            if (DataView is EpgViewBase)
            {
                //BeginInvokeはフォーカス対応
                MenuUtil.CheckJumpTab(new ReserveItem(recInfo.ToReserveData()), true);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    DataView.MoveToRecInfoItem(recInfo);
                }), DispatcherPriority.Loaded);
            }
            else
            {
                UpdateViewSelection(3);
            }
        }
        protected override void MoveViewNextItem(int direction, bool toRefData = false)
        {
            object NewData = null;
            if (DataView is EpgViewBase || DataView is SearchWindow.AutoAddWinListView)
            {
                NewData = DataView.MoveNextRecinfo(direction, recInfo.CurrentPgUID(), true, JumpItemStyle.MoveTo);
                if (NewData is RecFileInfo)
                {
                    ChangeData(NewData);
                    return;
                }
                toRefData = true;
            }
            base.MoveViewNextItem(direction, toRefData);
        }
        private void textBox_recFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            button_rename.IsEnabled = recInfo != null &&
                                      recInfo.RecFilePath.Length > 0 &&
                                      textBox_recFilePath.Text.Length > 0 &&
                                      recInfo.RecFilePath != textBox_recFilePath.Text;
        }
        private void button_rename_Click(object sender, RoutedEventArgs e)
        {
            if (recInfo != null && recInfo.RecFilePath.Length > 0)
            {
                string destPath = null;
                try
                {
                    // 絶対パスであること
                    string path = textBox_recFilePath.Text;
                    if (Path.GetFullPath(path).Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        // 拡張子は変更できない
                        if (Path.GetExtension(path).Equals(Path.GetExtension(recInfo.RecFilePath), StringComparison.OrdinalIgnoreCase))
                        {
                            // 移動先のディレクトリは存在しなければならない
                            if (Directory.Exists(Path.GetDirectoryName(path)))
                            {
                                destPath = path;
                            }
                        }
                    }
                }
                catch { }

                if (destPath == null)
                {
                    MessageBox.Show("拡張子または移動先が不正です。", "", MessageBoxButton.OK, MessageBoxImage.Error);
                    textBox_recFilePath.Text = recInfo.RecFilePath;
                }
                else
                {
                    // データベースを変更
                    ErrCode err = ErrCode.CMD_ERR;
                    string originalPath = recInfo.RecFilePath;
                    recInfo.RecFilePath = destPath;
                    try
                    {
                        err = CommonManager.CreateSrvCtrl().SendChgPathRecInfo(new List<RecFileInfo>() { recInfo });
                        StatusManager.StatusNotifySet(err == ErrCode.CMD_SUCCESS, "録画ファイル名を変更");
                        if (err != ErrCode.CMD_SUCCESS)
                        {
                            MessageBox.Show(CommonManager.GetErrCodeText(err) ?? "録画ファイル名の変更に失敗しました。", "録画ファイル名の変更", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                    if (err != ErrCode.CMD_SUCCESS)
                    {
                        textBox_recFilePath.Text = recInfo.RecFilePath = originalPath;
                    }
                    else
                    {
                        // ファイルが存在すれば移動する
                        var errFileList = new List<string>();
                        try
                        {
                            File.Move(originalPath, destPath);
                        }
                        catch (FileNotFoundException) { }
                        catch
                        {
                            errFileList.Add(originalPath);
                        }
                        try
                        {
                            // 拡張子が付加されたファイルも移動する
                            foreach (string path in Directory.GetFiles(Path.GetDirectoryName(originalPath), Path.GetFileName(originalPath) + ".*"))
                            {
                                if (path.Length > originalPath.Length &&
                                    string.Compare(path, 0, originalPath, 0, originalPath.Length, StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    try
                                    {
                                        File.Move(path, destPath + path.Substring(originalPath.Length));
                                    }
                                    catch
                                    {
                                        errFileList.Add(path);
                                    }
                                }
                            }
                        }
                        catch { }

                        // 録画情報保存フォルダのファイルも移動する
                        string recInfoFolder = IniFileHandler.GetPrivateProfileFolder("SET", "RecInfoFolder", SettingPath.CommonIniPath);
                        if (recInfoFolder.Length > 0)
                        {
                            foreach (string suffix in new string[] { ".err", ".program.txt" })
                            {
                                string path = "";
                                try
                                {
                                    path = Path.Combine(recInfoFolder, Path.GetFileName(originalPath) + suffix);
                                    File.Move(path, Path.Combine(recInfoFolder, Path.GetFileName(destPath) + suffix));
                                }
                                catch (FileNotFoundException) { }
                                catch
                                {
                                    errFileList.Add(path);
                                }
                            }
                        }

                        if (errFileList.Any())
                        {
                            StatusManager.StatusNotifyAppend("リネームに失敗 < ");
                            MessageBox.Show("録画済み一覧の情報は更新されましたが、リネームまたは移動に失敗したファイルがあります。\r\n\r\n" + string.Join("\r\n", errFileList), "録画ファイル名の変更", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            button_rename.IsEnabled = false;
        }
    }
    public class RecInfoDescWindowBase : ReserveWindowBase<RecInfoDescWindow> { }
}
