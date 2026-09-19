using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
    
namespace EpgTimer
{
    public class CmdExeReserve : CmdExe<ReserveData>
    {
        protected Func<bool, List<SearchItem>> _getSearchList = null;
        protected Func<List<EpgEventInfo>> _getEpgEventList = null;
        protected Func<bool, SearchItem> _selectSingleSearchData = null;

        public void SetFuncGetSearchList(Func<bool, List<SearchItem>> f) { _getSearchList = f; }
        public void SetFuncGetEpgEventList(Func<List<EpgEventInfo>> f) { _getEpgEventList = f; }
        public void SetFuncSelectSingleSearchData(Func<bool, SearchItem> f) { _selectSingleSearchData = f; }

        protected Func<RecSettingData> GetRecSetting = new Func<RecSettingData>(() => null);
        protected Func<EpgSearchKeyInfo> GetSearchKey = new Func<EpgSearchKeyInfo>(() => null);

        public void SetFuncGetRecSetting(Func<RecSettingData> f) { if (f != null) GetRecSetting = f; }
        public void SetFuncGetSearchKey(Func<EpgSearchKeyInfo> f) { if (f != null) GetSearchKey = f; }

        protected override int ItemCount { get { return dataList.Count  + eventListEx.Count + recinfoList.Count; } }
        protected bool HasList { get { return _getSearchList != null; } }
        protected bool IsMultiReserve { get { return eventList.Count != 0 && eventListEx.Count == 0; } }
        protected IAutoAddTargetData headData = null;//メニューオープン時に使用
        protected IAutoAddTargetData headDataEv = null;//番組情報優先先頭データ。headDataは予約情報優先。
        protected List<EpgEventInfo> eventList = new List<EpgEventInfo>();
        protected List<EpgEventInfo> eventListEx = new List<EpgEventInfo>();//reserveData(dataList)とかぶらないもの
        protected List<EpgEventInfo> eventListAdd { get { return IsMultiReserve == true ? eventList : eventListEx; } }
        protected RecFileInfo headDataRec = null;//録画済みデータの場合
        protected List<RecFileInfo> recinfoList = new List<RecFileInfo>();

        public CmdExeReserve(UIElement owner) : base(owner) { }
        protected override void SetData(bool IsAllData = false)
        {
            base.SetData(IsAllData);
            if (HasList == true)//SearchItemリストがある場合
            {
                List<SearchItem> searchList = _getSearchList(IsAllData);
                searchList = searchList == null ? new List<SearchItem>() : searchList.OfType<SearchItem>().ToList();//無くても大丈夫なはずだが一応
                OrderAdjust(searchList);
                dataList = searchList.GetReserveList();
                eventList = searchList.GetEventList();
                eventListEx = searchList.GetNoReserveList();
                headData = searchList.Count == 0 ? null : searchList[0].IsReserved == true ? searchList[0].ReserveInfo as IAutoAddTargetData : searchList[0].EventInfo;
                headDataEv = searchList.Count == 0 ? null : searchList[0].EventInfo;
                recinfoList = eventList.SelectMany(data => data.GetRecListFromPgUID()).ToList();
                headDataRec = eventList.FirstOrDefault().GetRecinfoFromPgUID();
            }
            else
            {
                //終了済み録画データの処理
                recinfoList = dataList.OfType<ReserveDataEnd>().SelectMany(data => data.GetRecListFromPgUID()).ToList();
                headDataRec = recinfoList.FirstOrDefault();
                dataList.RemoveAll(data => data is ReserveDataEnd);

                eventList = _getEpgEventList == null ? null : _getEpgEventList();
                eventList = eventList == null ? new List<EpgEventInfo>() : eventList.OfType<EpgEventInfo>().ToList();
                eventListEx = new List<EpgEventInfo>();
                eventList.ForEach(epg => 
                {
                    if (dataList.All(res => epg.CurrentPgUID() != res.CurrentPgUID()))
                    {
                        eventListEx.Add(epg);
                    }
                });
                headData = dataList.Count != 0 ? dataList[0] as IAutoAddTargetData : eventList.Count != 0 ? eventList[0] : null;
                headDataEv = eventList.Count != 0 ? eventList[0] as IAutoAddTargetData : dataList.Count != 0 ? dataList[0] : null;
            }
            eventList = eventList.Distinct().ToList();
            eventListEx = eventListEx.Distinct().ToList();
            recinfoList = recinfoList.Distinct().ToList();
        }
        protected override void ClearData()
        {
            base.ClearData();
            headData = null;
            headDataRec = null;
            headDataEv = null;
            eventList.Clear();
            eventListEx.Clear();
            recinfoList.Clear();
        }
        protected override object SelectSingleData(bool noChange = false)
        {
            return _selectSingleSearchData == null ? base.SelectSingleData(noChange) : _selectSingleSearchData(noChange);
        }
        //以下個別コマンド対応
        protected override void mc_Add(object sender, ExecutedRoutedEventArgs e)
        {
            IsCommandExecuted = MenuUtil.ReserveAdd(eventListAdd, GetRecSetting(), 0);
        }
        protected override void mc_AddOnPreset(object sender, ExecutedRoutedEventArgs e)
        {
            int presetID = CmdExeUtil.ReadIdData(e, 0, 0xFE);
            IsCommandExecuted = MenuUtil.ReserveAdd(eventListAdd, null, presetID);
        }
        protected override void mc_ShowDialog(object sender, ExecutedRoutedEventArgs e)
        {
            if (dataList.Count != 0 && CmdExeUtil.ReadIdData(e) != 1)
            {
                IsCommandExecuted = true == MenuUtil.OpenChangeReserveDialog(dataList[0], EpgInfoOpenMode);
            }
            else if (headDataRec != null && (eventListAdd.Count == 0 || eventListAdd[0].IsOver()))
            {
                IsCommandExecuted = true == MenuUtil.OpenRecInfoDialog(headDataRec);
            }
            else if (eventListAdd.Count != 0)
            {
                IsCommandExecuted = true == MenuUtil.OpenEpgReserveDialog(eventListAdd[0], EpgInfoOpenMode, GetRecSetting());
            }
        }
        protected override void mc_ShowAddDialog(object sender, ExecutedRoutedEventArgs e)
        {
            IsCommandExecuted = true == MenuUtil.OpenManualReserveDialog(GetRecSetting());//今のところこの呼び出し時にGetRecSetting()が有効な場合はない。
        }
        protected override void mc_ChangeOnOff(object sender, ExecutedRoutedEventArgs e)
        {
            //多数アイテム処理の警告。合計数に対して出すので、結構扱いづらい。
            if (MenuUtil.CautionManyMessage(dataList.Count + eventListEx.Count, "簡易予約/有効←→無効") == false) return;

            bool ret1 = MenuUtil.ReserveChangeOnOff(dataList, false);
            var eList = dataList.Count == 0 ? eventListEx :
                HasList == true ? eventListEx.FindAll(data => data.IsReservable == true) : new List<EpgEventInfo>();
            bool ret2 = MenuUtil.ReserveAdd(eList, GetRecSetting(), 0, false);
            IsCommandExecuted = !(ret1 == false && ret2 == false || dataList.Count == 0 && ret2 == false || eventListEx.Count == 0 && ret1 == false);
        }
        protected override void mc_ChangeRecSetting(object sender, ExecutedRoutedEventArgs e)
        {
            if (mcc_chgRecSetting(e, true) == false) return;
            IsCommandExecuted = MenuUtil.ReserveChange(dataList);
        }
        protected override void mc_ChgResMode(object sender, ExecutedRoutedEventArgs e)
        {
            if (dataList.Count == 0) return;

            var data = CmdExeUtil.ReadObjData(e) as Type;
            uint id = (uint)CmdExeUtil.ReadIdData(e);

            if (data == null)
            {
                //通常の変更
                IsCommandExecuted = MenuUtil.ReserveChangeResMode(dataList, id);
            }

            if (dataList.Count != 1) return;//通常はここに引っかかることは無いはず

            AutoAddData autoAdd = AutoAddData.AutoAddList(data, id);
            if (autoAdd != null)
            {
                IsCommandExecuted = MenuUtil.ReserveChangeResModeAutoAdded(dataList, autoAdd);
            }
        }
        protected override void mc_ChgBulkRecSet(object sender, ExecutedRoutedEventArgs e)
        {
            if (dataList.Count == 0) return;
            var mList = dataList.FindAll(info => info.IsEpgReserve == false);
            if (MenuUtil.ChangeBulkSet(dataList, this.Owner, mList.Count == dataList.Count, true) == false) return;
            IsCommandExecuted = MenuUtil.ReserveChange(dataList);
        }
        protected override void mcs_CopyItem(object sender, ExecutedRoutedEventArgs e)
        {
            if (dataList.Count == 0) return;
            var list = dataList.DeepClone();//コピーが必要なのは予約だけ。(キーワード予約などはコピー不要なので)
            list.ForEach(rs => rs.Comment = "");
            IsCommandExecuted = MenuUtil.ReserveAdd(list);
        }
        protected override void mcs_CopyItemDialog(object sender, ExecutedRoutedEventArgs e)
        {
            if (dataList.Count == 0) return;
            dataList[0].ReserveID = 0;
            mc_ShowDialog(sender, e);
        }
        protected override void mc_Delete(object sender, ExecutedRoutedEventArgs e)
        {
            List<ReserveData> resList = dataList.ToList();
            if (e.Command == EpgCmds.DeleteAll) recinfoList.Clear();
            recinfoList = recinfoList.GetNoProtectedList();
            dataList.AddRange(recinfoList.Select(info => new ReserveDataEnd { Title = "[録画済み] " + info.Title }));

            if (mcs_DeleteCheck(e) == false || CmdExeRecinfo.mcs_DeleteCheckDelFile(recinfoList) == false) return;
            IsCommandExecuted = MenuUtil.ReserveDelete(resList) && MenuUtil.RecinfoDelete(recinfoList);
        }
        protected override void mc_JumpRecInfo(object sender, ExecutedRoutedEventArgs e)
        {
            //同じショートカットのフォロー
            mcs_JumpTab(dataList.Count != 0 ? CtxmCode.ReserveView : CtxmCode.RecInfoView);
        }
        protected override SearchItem mcs_GetSearchItem()
        {
            if (dataList.Count != 0)//予約情報優先
            {
                return new ReserveItem(dataList[0]);
            }
            else if (recinfoList.Count != 0)
            {
                return new ReserveItem(recinfoList[0].ToReserveData());
            }
            else if (eventList.Count != 0)
            {
                return new SearchItem(eventList[0]);
            }
            return null;
        }
        protected override ReserveData mcs_GetNextReserve()
        {
            return headData as ReserveData;
        }
        protected override RecFileInfo mcs_GetRecInfoItem()
        {
            return headDataRec;
        }
        protected override void mc_ToAutoadd(object sender, ExecutedRoutedEventArgs e)
        {
            ReserveData resData = dataList.Count != 0 ? dataList[0] : null;
            IBasicPgInfo eventRefData = eventList.Count != 0 ? eventList[0] : null;
            if (resData != null)
            {
                eventRefData = eventRefData ?? new ReserveItem(resData).EventInfo ?? (IBasicPgInfo)resData;
            }
            else if (eventRefData != null)
            {
                resData = ((EpgEventInfo)eventRefData).ToReserveData();
                resData.RecSetting = (GetRecSetting() ?? Settings.Instance.RecPresetList[0].Data).DeepClone();
            }
            else if (recinfoList.Count != 0)
            {
                eventRefData = recinfoList[0];
            }

            var key = MenuUtil.SendAutoAddKey(eventRefData, CmdExeUtil.IsKeyGesture(e));
            MenuUtil.SendAutoAdd(resData ?? eventRefData, CmdExeUtil.IsKeyGesture(e), key);
            IsCommandExecuted = true;
        }
        protected override void mc_Play(object sender, ExecutedRoutedEventArgs e)
        {
            if (CmdExeUtil.ReadIdData(e) == 0)
            {
                if (dataList.Count == 0) return;
                CommonManager.Instance.FilePlay(dataList[0]);
            }
            else
            {
                if (headDataRec == null) return;
                CommonManager.Instance.FilePlay(headDataRec);
            }
            IsCommandExecuted = true;
        }
        protected override void mc_CopyTitle(object sender, ExecutedRoutedEventArgs e)
        {
            //番組情報優先
            MenuUtil.CopyTitle2Clipboard((headDataEv ?? headDataRec).DataTitle, CmdExeUtil.IsKeyGesture(e));
            IsCommandExecuted = true; 
        }
        protected override void mc_CopyContent(object sender, ExecutedRoutedEventArgs e)
        {
            if (eventList.Count != 0)//番組情報優先
            {
                MenuUtil.CopyContent2Clipboard(eventList[0], CmdExeUtil.IsKeyGesture(e));
            }
            else if (dataList.Count != 0)
            {
                MenuUtil.CopyContent2Clipboard(dataList[0], CmdExeUtil.IsKeyGesture(e));
            }
            else if (recinfoList.Count != 0)
            {
                MenuUtil.CopyContent2Clipboard(recinfoList[0], CmdExeUtil.IsKeyGesture(e));
            }
            IsCommandExecuted = true;
        }
        protected override void mc_InfoSearchTitle(object sender, ExecutedRoutedEventArgs e)
        {
            //番組情報優先
            IsCommandExecuted = true == MenuUtil.OpenInfoSearchDialog((headDataEv ?? headDataRec).DataTitle, CmdExeUtil.IsKeyGesture(e));
        }
        protected override void mc_OpenFolder(object sender, ExecutedRoutedEventArgs e)
        {
            if (headDataRec == null)
            {
                base.mc_OpenFolder(sender, e);
                return;
            }
            CommonManager.OpenRecFolder(headDataRec.RecFilePath);
            IsCommandExecuted = true;
        }
        protected override void mc_SearchTitle(object sender, ExecutedRoutedEventArgs e)
        {
            //番組情報優先
            MenuUtil.SearchTextWeb((headDataEv ?? headDataRec).DataTitle, CmdExeUtil.IsKeyGesture(e));
            IsCommandExecuted = true;
        }
        protected override void mc_SetRecTag(object sender, ExecutedRoutedEventArgs e)
        {
            if (CmdExeUtil.CheckSetFromClipBoardCancel(e, dataList, "録画タグ") == true) return;
            IsCommandExecuted = MenuUtil.ReserveChangeRecTag(dataList, Clipboard.GetText());
        }
        protected override void mcs_ctxmLoading_switch(ContextMenu ctxm, MenuItem menu)
        {
            var view = (menu.CommandParameter as EpgCmdParam).Code;

            //有効無効制御の追加分。予約データが無ければ無効
            new List<ICommand> { EpgCmdsEx.ChgMenu, EpgCmds.CopyItem, EpgCmds.DeleteAll, EpgCmds.Play, EpgCmds.SetRecTag }.ForEach(icmd =>
            {
                if (menu.Tag == icmd) menu.IsEnabled = dataList.Count != 0;
            });

            var CheckReservableEpg = new Func<MenuItem, List<EpgEventInfo>, bool>((mi, list) =>
            {
                if (list.Count != 0 && list.Count(data => data.IsReservable == true) == 0)
                {
                    mi.IsEnabled = false;
                    mi.ToolTip = "放映終了";
                }
                return mi.IsEnabled;
            });

            //switch使えないのでifで回す。
            if (menu.Tag == EpgCmds.ChgOnOff)
            {
                menu.IsEnabled = dataList.Count + eventListEx.Count != 0;
                menu.Header = view == CtxmCode.ReserveView || dataList.Count != 0 ? "予約←→無効" : "簡易予約";
                //予約データの有無で切り替える。
                if (dataList.Count == 0)
                {
                    if (CheckReservableEpg(menu, eventListEx) == true)
                    {
                        RecSettingData setInfo = GetRecSetting();
                        if (setInfo != null)
                        {
                            RecPresetItem preset = setInfo.LookUpPreset();
                            string text = preset.IsCustom == true ? "カスタム設定" : string.Format("プリセット'{0}'", preset.DisplayName);
                            menu.ToolTip = string.Format("現在の録画設定({0})で予約する", text);
                        }
                        else
                        {
                            menu.ToolTip = "プリセット'デフォルト'で予約する";
                        }
                    }
                }
            }
            else if (menu.Tag == EpgCmdsEx.AddMenu)
            {
                if (CheckReservableEpg(menu, eventListAdd) == true)
                {
                    menu.IsEnabled = eventListAdd.Count != 0;//未予約アイテムがあれば有効
                    mm.CtxmGenerateAddOnPresetItems(menu);
                    mcs_SetSingleMenuEnabled(menu, HasList == false || IsMultiReserve == true || headData is EpgEventInfo);
                }
                var s = menu.Header as string;
                menu.Header = (IsMultiReserve == true ? "重複予約追加" : "予約追加") + s.Substring(s.Length - 4);
            }
            else if (menu.Tag == EpgCmdsEx.ChgMenu)
            {
                mcs_chgMenuOpening(menu, true);
                mcs_SetSingleMenuEnabled(menu, headData is ReserveData);
            }
            else if (menu.Tag == EpgCmds.CopyItem)
            {
                menu.IsEnabled &= !Settings.Instance.MenuSet.ShowCopyDialog || headData is ReserveData;
            }
            else if (menu.Tag == EpgCmds.Delete)
            {
                menu.IsEnabled = dataList.Any() || recinfoList.GetNoProtectedList().Any();
            }
            else if (menu.Tag == EpgCmds.JumpReserve)
            {
                mcs_ctxmLoading_jumpTabRes(menu);
                menu.Visibility = mcs_GetNextReserve() != null || headDataEv == null || headDataEv.IsOver() == false ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (menu.Tag == EpgCmds.JumpRecInfo)
            {
                //予約状況によってはJumpReserveと両方表示する場合もある
                menu.IsEnabled = headDataRec != null;
                menu.Visibility = menu.IsEnabled || headDataEv != null && headDataEv.IsOver() == true ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (menu.Tag == EpgCmds.JumpTuner)
            {
                mcs_ctxmLoading_jumpTabRes(menu);
            }
            else if (menu.Tag == EpgCmds.JumpTable)
            {
                if (view != CtxmCode.EpgView)
                {
                    mcs_ctxmLoading_jumpTabEpg(menu);
                    return;
                }

                //標準モードでは非表示。
                if ((int)ctxm.Tag == 0)
                {
                    menu.Visibility = Visibility.Collapsed;
                }
            }
            else if (menu.Tag == EpgCmdsEx.ShowAutoAddDialogMenu)
            {
                menu.IsEnabled = mm.CtxmGenerateChgAutoAdd(menu, headData);
            }
            else if (menu.Tag == EpgCmds.Play)
            {
                //予約状況によっては予約用と録画済み用の両方表示する場合もある
                if ((menu.CommandParameter as EpgCmdParam).ID == 0)
                {
                    menu.IsEnabled = false;
                    var info = headData as ReserveData;
                    if (info != null && info.IsEnabled == true)
                    {
                        if (info.IsOnRec() == true)
                        {
                            menu.IsEnabled = true;
                        }
                        else
                        {
                            menu.ToolTip = "まだ録画が開始されていません。";
                        }
                    }
                    menu.Visibility = menu.IsEnabled || (headDataEv is EpgEventInfo && !headDataEv.IsOver()) ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    menu.IsEnabled = headDataRec != null;
                    menu.Visibility = menu.IsEnabled || (headDataEv is EpgEventInfo && headDataEv.IsOver()) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            else if (menu.Tag == EpgCmdsEx.OpenFolderMenu)
            {
                mm.CtxmGenerateOpenFolderItems(menu, headData is ReserveData ? dataList[0].RecSetting : null, headDataRec != null ? headDataRec.RecFilePath : null, !(ctxm.Tag is int && (int)(ctxm.Tag) != 2));
            }
            else if (menu.Tag == EpgCmdsEx.ViewMenu)
            {
                foreach (var item in menu.Items.OfType<MenuItem>().Where(item => item.Tag == EpgCmds.ViewChgMode))
                {
                    item.IsChecked = ((item.CommandParameter as EpgCmdParam).ID == (int)ctxm.Tag);
                }
            }
        }
        protected override string GetCmdMessage(ICommand icmd)
        {
            if (icmd == EpgCmds.CopyItem && Settings.Instance.MenuSet.ShowCopyDialog) return null;

            string cmdMsg = null;
            cmdMessage.TryGetValue(icmd, out cmdMsg);
            if (icmd == EpgCmds.ChgOnOff)
            {
                cmdMsg = eventListEx.Count == 0 ? "有効・無効切替を実行" : dataList.Count == 0 ? "簡易予約を実行" : cmdMsg;
            }
            //DeleteはRecInfo分含めてdataList.Countに入っている。
            int procCount = GetCmdParam(icmd).ExeType == cmdExeType.SingleItem ? 1 :
                            icmd == EpgCmds.AddReserve || icmd == EpgCmds.AddOnPreset ? eventListAdd.Count :
                                dataList.Count + (icmd == EpgCmds.ChgOnOff ? eventListEx.Count : 0);

            return procCount == 0 ? null : GetCmdMessageFormat(cmdMsg, procCount);
        }
    }
}
