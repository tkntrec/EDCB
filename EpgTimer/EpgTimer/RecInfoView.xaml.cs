using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EpgTimer
{
    /// <summary>
    /// RecInfoView.xaml の相互作用ロジック
    /// </summary>
    public partial class RecInfoView : DataItemViewBase
    {
        private ListViewController<RecInfoItem> lstCtrl;
        private CmdExeRecinfo mc;
        protected override ListBox DataListBox { get { return listView_recinfo; } }

        public RecInfoView()
        {
            InitializeComponent();

            try
            {
                //リストビュー関連の設定
                lstCtrl = new ListViewController<RecInfoItem>(this);
                lstCtrl.SetSavePath(CommonUtil.NameOf(() => Settings.Instance.RecInfoListColumn)
                    , CommonUtil.NameOf(() => Settings.Instance.RecInfoColumnHead)
                    , CommonUtil.NameOf(() => Settings.Instance.RecInfoSortDirection));
                lstCtrl.SetViewSetting(listView_recinfo, gridView_recinfo, true, true);
                lstCtrl.SetSelectedItemDoubleClick((sender, e) =>
                {
                    var cmd = Settings.Instance.PlayDClick == true ? EpgCmds.Play : EpgCmds.ShowDialog;
                    cmd.Execute(sender, listView_recinfo);
                });

                //ステータス変更の設定
                lstCtrl.SetSelectionChangedEventHandler((sender, e) => this.UpdateStatus(1));

                //最初にコマンド集の初期化
                mc = new CmdExeRecinfo(this);
                mc.SetFuncGetDataList(isAll => (isAll == true ? lstCtrl.dataList : lstCtrl.GetSelectedItemsList()).RecInfoList());
                mc.SetFuncSelectSingleData((noChange) =>
                {
                    var item = lstCtrl.SelectSingleItem(noChange);
                    return item == null ? null : item.RecInfo;
                });
                mc.SetFuncReleaseSelectedData(() => listView_recinfo.UnselectAll());

                //コマンド集に無いもの
                mc.AddReplaceCommand(EpgCmds.ChgOnOffCheck, (sender, e) => lstCtrl.ChgOnOffFromCheckbox(e.Parameter, EpgCmds.ProtectChange));

                //コマンド集からコマンドを登録
                mc.ResetCommandBindings(this, listView_recinfo.ContextMenu);

                //コンテキストメニューを開く時の設定
                listView_recinfo.ContextMenu.Opened += new RoutedEventHandler(mc.SupportContextMenuLoading);

                //ボタンの設定
                mBinds.View = CtxmCode.RecInfoView;
                mBinds.SetCommandToButton(button_Delete, EpgCmds.Delete);
                mBinds.SetCommandToButton(button_DeleteAll, EpgCmds.DeleteAll);
                mBinds.SetCommandToButton(button_Play, EpgCmds.Play);
                mBinds.SetCommandToButton(button_ToAutoadd, EpgCmds.ToAutoadd);
                mBinds.SetCommandToButton(button_OpenFolder, EpgCmds.OpenFolder);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }
        public void RefreshMenu()
        {
            mBinds.ResetInputBindings(this, listView_recinfo);
            mm.CtxmGenerateContextMenu(listView_recinfo.ContextMenu, CtxmCode.RecInfoView, true);
        }
        public void SaveViewData()
        {
            lstCtrl.SaveViewDataToSettings();
        }
        protected override bool ReloadInfoData()
        {
            return lstCtrl.ReloadInfoData(dataList =>
            {
                ErrCode err = CommonManager.Instance.DB.ReloadRecFileInfo();
                if (CommonManager.CmdErrMsgTypical(err, "録画情報の取得") == false) return false;

                dataList.AddRange(CommonManager.Instance.DB.RecFileInfo.Values.Select(info => new RecInfoItem(info)));

                //ツールチップに番組情報を表示する場合は先に一括で詳細情報を読込んでおく
                if (Settings.Instance.NoToolTip == false && Settings.Instance.RecInfoToolTipMode == 1)
                {
                    CommonManager.Instance.DB.ReadRecFileAppend();
                }

                return true;
            });
        }
        protected override void UpdateStatusData(int mode = 0)
        {
            if (mode == 0) this.status[1] = ViewUtil.ConvertRecinfoStatus(lstCtrl.dataList, "録画結果");
            List<RecInfoItem> sList = lstCtrl.GetSelectedItemsList();
            this.status[2] = sList.Count == 0 ? "" : ViewUtil.ConvertRecinfoStatus(sList, "　選択中");
        }
    }
}
