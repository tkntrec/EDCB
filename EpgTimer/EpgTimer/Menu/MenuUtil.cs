using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EpgTimer
{
    public static class MenuUtil
    {
        public static string TrimEpgKeyword(string KeyWord, bool NotToggle = false)//NotToggleはショートカット用
        {
            return TrimKeywordCheckToggled(KeyWord, Settings.Instance.MenuSet.Keyword_Trim, NotToggle);
        }

        public static void CopyTitle2Clipboard(string Title, bool NotToggle = false)
        {
            Title = TrimKeywordCheckToggled(Title, Settings.Instance.MenuSet.CopyTitle_Trim, NotToggle);
            ToClipBoard(Title);
        }

        public static void CopyContent2Clipboard(EpgEventInfo eventInfo, bool NotToggle = false)
        {
            string text = CommonManager.ConvertProgramText(eventInfo, EventInfoTextMode.BasicInfo);
            if (CheckShiftToggled(Settings.Instance.MenuSet.CopyContentBasic, NotToggle) == false)
            {
                text += CommonManager.ConvertProgramText(eventInfo, EventInfoTextMode.BasicText)
                    + CommonManager.TrimHyphenSpace(CommonManager.ConvertProgramText(eventInfo, EventInfoTextMode.ExtendedText))
                    + CommonManager.ConvertProgramText(eventInfo, EventInfoTextMode.PropertyInfo);
            }
            ToClipBoard(text.TrimEnd() + "\r\n");
        }

        public static void CopyContent2Clipboard(ReserveData resInfo, bool NotToggle = false)
        {
            EpgEventInfo info = resInfo == null ? null : resInfo.GetPgInfo();
            CopyContent2Clipboard(info, NotToggle);
        }

        public static void CopyContent2Clipboard(RecFileInfo recInfo, bool NotToggle = false)
        {
            string text = "";
            if (recInfo != null)
            {
                recInfo.ProgramInfoSet();

                if (CheckShiftToggled(Settings.Instance.MenuSet.CopyContentBasic, NotToggle) == true)
                {
                    text = string.Join("\r\n", recInfo.ProgramInfo.Replace("\r\n", "\n").Split('\n').Take(3));
                }
                else
                {
                    string[] parts = recInfo.GetProgramInfoParts();
                    text = parts[0] + CommonManager.TrimHyphenSpace(parts[1]) + parts[2];
                }
            }
            ToClipBoard(text.TrimEnd() + "\r\n");
        }

        private static void ToClipBoard(string text)
        {
            try { Clipboard.SetDataObject(text, true); } catch { }
        }

        public static void SearchTextWeb(string KeyWord, bool? NotToggle = null)
        {
            try
            {
                if (NotToggle != null)
                {
                    KeyWord = TrimKeywordCheckToggled(KeyWord, Settings.Instance.MenuSet.SearchTitle_Trim, (bool)NotToggle);
                }
                string txtURI = Settings.Instance.MenuSet.SearchURI + UrlEncode(KeyWord, System.Text.Encoding.UTF8);
                using (System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(txtURI) { UseShellExecute = true })) { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                MessageBox.Show("'検索のURI'の設定を確認してください。");
            }
        }

        private static string TrimKeywordCheckToggled(string s, bool setting, bool NotToggle = false)
        {
            return CheckShiftToggled(setting, NotToggle) == true ? TrimKeyword(s) : s;
        }
        private static bool CheckShiftToggled(bool setting, bool NotToggle = false)
        {
            return (Keyboard.Modifiers == ModifierKeys.Shift && NotToggle == false) ? !setting : setting;
        }

        private static bool TrimKeywordFatalErr = false;
        public static string TrimKeyword(string val)
        {
            try
            {
                if (TrimKeywordFatalErr == true) return val;
                return Settings.Instance.PicUpTitleWork.PicUp(val);
            }
            catch (Exception ex)
            {
                string msg = "\r\n\r\nエラーメッセージ :\r\n" + ex.ToString();
                if (Settings.Instance.PicUpTitleWork.UseCustom == true)
                {
                    msg = "記号類の除去でエラーが発生しました。\r\n"
                            + "カスタム設定の使用を中止し、内部デフォルト設定で実行します。" + msg;
                    Settings.Instance.PicUpTitleWork.UseCustom = false;
                }
                else
                {
                    msg = "記号類の除去で致命的なエラーが発生しました。\r\n"
                            + "記号類の除去を停止します。" + msg;
                    TrimKeywordFatalErr = true;
                }
                string ret = TrimKeyword(val);//メッセージボックスの順序を入れ替える
                CommonUtil.DispatcherMsgBoxShow(msg, "記号類除去のエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return ret;
            }
        }

        //
        // HttpUtility を使わないUrlEncodeの実装
        // From http://d.hatena.ne.jp/kazuv3/20080605/1212656674
        //
        public static string UrlEncode(string s, System.Text.Encoding enc)
        {
            var rt = new System.Text.StringBuilder();
            foreach (byte i in enc.GetBytes(s))
                if (i == 0x20)
                    rt.Append('+');
                else if (i >= 0x30 && i <= 0x39 || i >= 0x41 && i <= 0x5a || i >= 0x61 && i <= 0x7a)
                    rt.Append((char)i);
                else
                    rt.Append("%" + i.ToString("X2"));
            return rt.ToString();
        }

        /// <summary>
        /// アクセスキーを削除した文字列を返す。displayFormをtrueにすると、アクセスキーが有効な
        /// コントロール上での実際の表示文字列、falseにすると、アクセスキーが有効なコントロールで
        /// アクセスキーのみ削除され、それ以外は処理前と同じ表示結果となる文字列を返す。
        /// </summary>
        /// <param name="displayForm">falseにすると、引き続き'_'をエスケープした文字列を返す。</param>
        public static string DeleteAccessKey(string s, bool displayForm = false)
        {
            if (s == null) return s;
            for (int i = 0; i < s.Length - 1; i++)
            {
                if (s[i] == '_')
                {
                    if (s[i + 1] != '_')
                    {
                        s = s.Remove(i, 1);
                        break;//アクセスキーは最初の1つだけ
                    }
                    i++;
                }
            }
            s = ToDisplayForm(s);
            return displayForm == true ? s : ToAccessKeyForm(s);
        }
        public static string ToDisplayForm(string s) { return s.Replace("__", "_"); }
        public static string ToAccessKeyForm(string s) { return s.Replace("_", "__"); }

        /// <summary>
        /// 変換エラーの場合、デフォルト値を返し、テキストボックスの内容をデフォルト値に置き換える。
        /// </summary>
        public static T MyToNumerical<T>(TextBox box, Func<string, T> converter, T defValue = default(T), bool setdef = true)
        {
            try
            {
                return converter(box.Text.ToString());
            }
            catch
            {
                if (setdef == true) box.Text = defValue.ToString();
                return defValue;
            }
        }
        public static T MyToNumerical<T>(TextBox box, Func<string, T> converter, T max, T min, T defValue = default(T), bool setdef = true) where T : IComparable
        {
            try
            {
                T val = MyToNumerical(box, converter, defValue, setdef);
                if (val.CompareTo(min) < 0)
                {
                    box.Text = min.ToString();
                    return min;
                }
                if (val.CompareTo(max) > 0)
                {
                    box.Text = max.ToString();
                    return max;
                }
                return val;
            }
            catch
            {
                if (setdef == true) box.Text = defValue.ToString();
                return defValue;
            }
        }

        public static bool ReserveAdd(List<EpgEventInfo> itemlist, RecSettingData setInfo, int presetID = 0, bool cautionMany = true)
        {
            itemlist = CheckReservable(itemlist);
            if (itemlist == null) return false;

            //番組表やキーワードダイアログではsetInfoが指定されている
            setInfo = setInfo ?? Settings.Instance.RecPreset(presetID).Data;

            var list = itemlist.Select(item => item.ToReserveData()).ToList();
            list.ForEach(resInfo => resInfo.RecSetting = setInfo);//setInfoはコピーしなくても大丈夫。

            return ReserveAdd(list, cautionMany);
        }
        public static List<EpgEventInfo> CheckReservable(List<EpgEventInfo> list, bool fixlist = true)
        {
            if (list.Count == 0) return list;

            //開始未定と終了番組を除外
            list = list.FindAll(item => item.StartTimeFlag != 0);
            if (list.Count == 0)
            {
                MessageBox.Show("開始時間未定のため予約できません");
                return null;
            }
            list = list.FindAll(item => item.IsOver() == false);
            if (list.Count == 0)
            {
                MessageBox.Show("放映終了しているため予約できません");
                return null;
            }
            return list;
        }
        public static bool ReserveAdd(List<ReserveData> list, bool cautionMany = true)
        {
            if (list.Count == 0) return true;

            //録画時間過ぎているものを除外
            list = list.FindAll(item => item.IsOver() == false);
            if (list.Count == 0)
            {
                MessageBox.Show("録画時間が既に終了しています。\r\n(番組が放映中の場合は録画マージンも確認してください。)");
                return false;
            }
            return ReserveCmdSend(list, CommonManager.CreateSrvCtrl().SendAddReserve, "予約追加", cautionMany, "エラーが発生しました。\r\n終了時間がすでに過ぎている可能性があります。");
        }

        public static bool ReserveChangeOnOff(List<ReserveData> itemlist, bool cautionMany = true)
        {
            itemlist.ForEach(item => item.RecSetting.IsEnable = !item.RecSetting.IsEnable);
            return ReserveChange(itemlist, cautionMany);
        }

        public static void ChangeMargin(List<RecSettingData> infoList, bool isDefault, int? start = null, int? end = null, bool isOffset = false)
        {
            infoList.ForEach(info => info.SetMargin(isDefault, start, end, isOffset));
        }
        public static bool ChangeMarginValue(List<RecSettingData> infoList, bool start, UIElement owner = null, bool PresetResCompare = false)
        {
            try
            {
                infoList[0].SetMargin(false);

                var dlg = new SetRecPresetWindow(owner);
                dlg.SetSettingMode(start == true ? "開始マージン設定" : "終了マージン設定", start == true ? 0 : 1);
                dlg.DataView.PresetResCompare = PresetResCompare;
                dlg.DataView.SetDefSetting(infoList[0]);

                if (dlg.ShowDialog() == false) return false;

                RecSettingData setData = dlg.DataView.GetRecSetting();
                ChangeMargin(infoList, false, start ? (int?)setData.StartMargine : null, start ? null : (int?)setData.EndMargine, false);
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return false;
        }

        public static bool ChangeBulkSet(IEnumerable<IRecSetttingData> dataList, UIElement owner = null, bool pgAll = false, bool PresetResCompare = false)
        {
            try
            {
                var dlg = new SetRecPresetWindow(owner);
                dlg.SetSettingMode("まとめて録画設定を変更");
                dlg.DataView.PresetResCompare = PresetResCompare;
                dlg.DataView.SetViewMode(pgAll != true);
                dlg.DataView.SetDefSetting(dataList.First().RecSettingInfo);

                if (dlg.ShowDialog() == false) return false;

                ChangeRecSet(dataList, dlg.DataView.GetRecSetting());
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return false;
        }
        public static void ChangeRecSet(IEnumerable<IRecSetttingData> dataList, RecSettingData setData)
        {
            foreach (var data in dataList)
            {
                RecSettingData orgData = data.RecSettingInfo;
                data.RecSettingInfo = setData.DeepClone();
                if (Settings.Instance.SetWithoutRecTag) data.RecSettingInfo.RecTag = orgData.RecTag;
            }
        }

        public static bool ChgGenre(List<EpgSearchKeyInfo> infoList, UIElement owner = null)
        {
            try
            {
                var dlg = new SetSearchPresetWindow(owner);
                dlg.SetSettingMode("まとめてジャンル設定を変更", 0);
                dlg.DataView.SetSearchKey(infoList[0]);

                if (dlg.ShowDialog() == false) return false;

                EpgSearchKeyInfo setData = dlg.DataView.GetSearchKey();
                infoList.ForEach(info => info.contentList = setData.contentList.DeepClone());
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return false;
        }

        public static bool ReserveChangeResMode(List<ReserveData> itemlist, uint resMode)
        {
            List<ReserveData> list;
            if (resMode == 0)//EPG予約へ変更
            {
                list = itemlist.Where(item => item.ReserveMode == ReserveMode.KeywordAuto ||
                    item.IsManual && item.GetPgInfo().ToReserveData(ref item) == true).ToList();
            }
            else if (resMode == 1)//プログラム予約へ変更
            {
                list = itemlist.Where(item => item.ReserveMode != ReserveMode.Program).ToList();
                list.ForEach(item => { item.EventID = 0xFFFF; });
            }
            else
            {
                return true;
            }

            list.ForEach(item => item.ReleaseAutoAdd());
            return ReserveChange(list);
        }

        public static bool ReserveChangeResModeAutoAdded(List<ReserveData> itemList, AutoAddData autoAdd)
        {
            if (ReserveDelete(itemList, false) == false) return false;
            return AutoAddChange(autoAdd.IntoList(), false, false, true, true);
        }

        public static bool ReserveChange(List<ReserveData> itemlist, bool cautionMany = true, bool noHistory = false, bool noChkOnRec = false)
        {
            if (noChkOnRec == false && CheckReserveOnRec(itemlist, "変更") == false) return false;

            List<ReserveData> rhist = null;
            if (Settings.Instance.MenuSet.RestoreNoUse == false && noHistory == false)
            {
                //変更時は一応Send前に元データを確保
                rhist = itemlist.Where(item => CommonManager.Instance.DB.ReserveList.ContainsKey(item.ReserveID)).Select(item => CommonManager.Instance.DB.ReserveList[item.ReserveID]).DeepClone();
            }

            bool ret = ReserveCmdSend(itemlist, CommonManager.CreateSrvCtrl().SendChgReserve, "予約変更", cautionMany);

            if (rhist != null && ret == true)
            {
                CmdHistorys.Add(EpgCmdsEx.ChgMenu, rhist);
            }
            return ret;
        }

        public static bool ReserveDelete(List<ReserveData> itemlist, bool cautionMany = true, bool noHistory = false, bool noChkOnRec = false)
        {
            if (noChkOnRec == false && CheckReserveOnRec(itemlist, "削除") == false) return false;
            List<uint> list = itemlist.Select(item => item.ReserveID).ToList();
            bool ret = ReserveCmdSend(list, CommonManager.CreateSrvCtrl().SendDelReserve, "予約削除", cautionMany);

            if (Settings.Instance.MenuSet.RestoreNoUse == false && noHistory == false && ret == true)
            {
                CmdHistorys.Add(EpgCmds.Delete, itemlist.DeepClone());
            }
            return ret;
        }

        public static bool CheckReserveOnRec(List<ReserveData> itemlist, string description)
        {
            if (Settings.Instance.CautionOnRecChange == false) return true;
            int cMin = Settings.Instance.CautionOnRecMarginMin;

            List<string> list = itemlist.Select(item => CommonManager.Instance.DB.ReserveList.ContainsKey(item.ReserveID) == false ? item : CommonManager.Instance.DB.ReserveList[item.ReserveID])
                .Where(item => item.IsEnabled == true && item.OnTime(CommonUtil.EdcbNowEpg.AddMinutes(cMin)) >= 0)
                .Select(item => new ReserveItem(item).StartTime + "　" + item.Title).ToList();

            if (list.Count == 0) return true;

            string text = string.Format("録画中または{0}分以内に録画開始される予約が含まれています。\r\n"
                + "処理を続けますか?\r\n\r\n"
                + "[該当予約数: {1}]\r\n\r\n", cMin, list.Count)
                + CmdExeUtil.FormatTitleListForDialog(list);

            return MessageBox.Show(text, "[予約" + description + "]の確認", MessageBoxButton.OKCancel,
                                MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK;
        }

        public static bool ReserveChangeRecTag(List<ReserveData> itemlist, string value, bool cautionMany = true)
        {
            foreach (var item in itemlist) item.RecSettingInfo.RecTag = value;
            return ReserveChange(itemlist, cautionMany);
        }
        public static bool AutoAddChangeRecTag(IEnumerable<AutoAddData> itemlist, string value, bool cautionMany = true)
        {
            foreach (var item in itemlist) item.RecSettingInfo.RecTag = value;
            return AutoAddChange(itemlist, cautionMany);
        }
        public static bool AutoAddChangeKeyEnabled(IEnumerable<AutoAddData> itemlist, bool? value = null)
        {
            if (AutoAddChangeKeyEnabledCautionMany(itemlist) == false) return false;

            foreach (var item in itemlist) item.IsEnabled = value ?? !item.IsEnabled;
            return AutoAddChange(itemlist, false);
        }
        public static bool AutoAddChangeKeyEnabledCautionMany(IEnumerable<AutoAddData> itemlist)
        {
            if (Settings.Instance.CautionManyChange == true)
            {
                long addReserveNum = itemlist.Where(item => item.IsEnabled == false)
                    .Sum(item => item.SearchCount - item.ReserveCount);
                if (itemlist.Count() >= Settings.Instance.CautionManyNum
                    || addReserveNum >= Settings.Instance.CautionManyNum)
                {
                    if (MessageBox.Show("多数の項目を処理しようとしています。\r\n"
                        + "または多数の予約が追加されます。\r\n"
                        + "よろしいですか?\r\n\r\n"
                        + "[項目数 : " + itemlist.Count() + "]\r\n"
                        + "[追加される予約数 : " + addReserveNum + "]\r\n"
                        , "自動予約登録の変更", MessageBoxButton.OKCancel,
                        MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Cancel)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public static bool EpgAutoAddChangeNotKey(List<EpgAutoAddData> itemlist)
        {
            itemlist.ForEach(item => item.searchInfo.notKey = Clipboard.GetText());
            return AutoAddChange(itemlist);
        }
        public static bool EpgAutoAddChangeNote(List<EpgAutoAddData> itemlist)
        {
            itemlist.ForEach(item => item.searchInfo.note = Clipboard.GetText());
            return AutoAddChange(itemlist);
        }
        public static bool AutoAddAdd(IEnumerable<AutoAddData> itemlist, bool cautionMany = true)
        {
            return AutoAddCmdSend(itemlist, 0, cautionMany: cautionMany);
        }
        public static bool AutoAddChange(IEnumerable<AutoAddData> itemlist, bool cautionMany = true)
        {
            return AutoAddChange(itemlist, Settings.Instance.SyncResAutoAddChange, cautionMany);
        }
        public static bool AutoAddChange(IEnumerable<AutoAddData> itemlist, bool SyncChange, bool cautionMany = true, bool isViewOrder = true, bool noHistory = false)
        {
            if (SyncChange == true)
            {
                //操作前にリストを作成する
                List<ReserveData> deleteList = Settings.Instance.SyncResAutoAddChgNewRes == false ? null : new List<ReserveData>();
                List<ReserveData> syncList = AutoAddSyncChangeList(itemlist, false, deleteList);
                return AutoAddCmdSend(itemlist, 1, deleteList, syncList, cautionMany, isViewOrder, noHistory);
            }
            else
            {
                return AutoAddCmdSend(itemlist, 1, null, null, cautionMany, isViewOrder, noHistory);
            }
        }
        public static bool AutoAddChangeSyncReserve(IEnumerable<AutoAddData> itemlist)
        {
            return ReserveChange(AutoAddSyncChangeList(itemlist, true), false, true);
        }
        private static List<ReserveData> AutoAddSyncChangeList(IEnumerable<AutoAddData> itemlist, bool SyncAll, List<ReserveData> deleteList = null)
        {
            var syncDict = new Dictionary<uint, ReserveData>();

            foreach (AutoAddData data in itemlist)
            {
                //変更前のTunerIDを参照する。
                //プログラム自動登録では重複予約されないが、EpgTimer側の扱いは同じにしておく。
                uint TunerID = (AutoAddData.AutoAddList(data.GetType(), (uint)data.DataID) ?? data).RecSettingInfo.TunerID;
                IEnumerable<ReserveData> list = SyncAll == true ?
                    data.GetReserveList() : data.GetReserveList().Where(info => info.IsAutoAdded == true &&
                    (Settings.Instance.SeparateFixedTuners == false || info.RecSetting.TunerID == TunerID));
                foreach (ReserveData resinfo in list)
                {
                    if (syncDict.ContainsKey(resinfo.ReserveID) == false)
                    {
                        ReserveData rdata = resinfo.DeepClone();//変更かけるのでコピーする
                        rdata.RecSetting = data.RecSettingInfo.DeepClone();
                        //無効は保持する
                        if (resinfo.RecSetting.IsEnable == false)
                        {
                            rdata.RecSetting.IsEnable = false;
                        }
                        //プログラム予約の場合は名前も追従させる。
                        if (data.IsManual == true && resinfo.IsManual == true)
                        {
                            rdata.Title = data.DataTitle;
                        }
                        //録画タグの保持。
                        if (SyncAll == false && Settings.Instance.SyncResAutoAddChgNewRes == false
                                && Settings.Instance.SyncResAutoAddChgKeepRecTag == true)
                        {
                            rdata.RecSetting.RecTag = resinfo.RecSetting.RecTag;
                        }
                        syncDict.Add(resinfo.ReserveID, rdata);
                    }
                }
            }

            List<ReserveData> syncList = syncDict.Values.ToList();

            if (deleteList != null)
            {
                List<ReserveData> modList = (SyncAll == true ? syncList : AutoAddSyncModifyReserveList(syncList, itemlist));

                int cMin = Settings.Instance.CautionOnRecChange == true ? Settings.Instance.CautionOnRecMarginMin : 1;
                deleteList.AddRange(modList.FindAll(data => data.IsEnabled == true && data.OnTime(CommonUtil.EdcbNowEpg.AddMinutes(cMin)) < 0));
                syncList = syncList.Except(deleteList).ToList();
            }

            //無効になっている自動登録からの連動変更で、他の有効な自動登録の予約が変更されないようにする
            if (SyncAll == false)
            {
                //syncListのReserveDataはコピーなのでIDで処理する
                var extList1 = new List<uint>();//処理対象のうち無効の自動登録の予約一覧
                var extList2 = new List<uint>();//処理対象のうち有効の自動登録の予約一覧
                foreach (AutoAddData data in itemlist)
                {
                    (data.IsEnabled == false ? extList1 : extList2).AddRange(data.GetReserveList().Where(info => info.IsAutoAdded == true).Select(info => info.ReserveID));
                }
                var extHash = new HashSet<uint>(extList1.Except(extList2));
                syncList = syncList.Where(resinfo => extHash.Contains(resinfo.ReserveID) == false).ToList();
            }

            return syncList;
        }
        public static bool AutoAddDelete(IEnumerable<AutoAddData> itemlist, bool cautionMany = true)
        {
            return AutoAddDelete(itemlist, Settings.Instance.SyncResAutoAddDelete, false, cautionMany);
        }
        public static bool AutoAddDelete(IEnumerable<AutoAddData> itemlist, bool SyncDelete, bool SyncAll, bool cautionMany = true)
        {
            //操作前にリストを作成する
            return AutoAddCmdSend(itemlist, 2, SyncDelete == false ? null : AutoAddSyncDeleteList(itemlist, SyncAll), null, cautionMany);
        }
        private static List<ReserveData> AutoAddSyncDeleteList(IEnumerable<AutoAddData> itemlist, bool SyncAll)
        {
            var list = itemlist.GetReserveList();
            return SyncAll == true ? list : AutoAddSyncModifyReserveList(list, itemlist);
        }

        private static List<ReserveData> AutoAddSyncModifyReserveList(List<ReserveData> reslist, IEnumerable<AutoAddData> itemlist)
        {
            //変更前TunerID参照
            List<ReserveData> reslist_org = reslist.Select(info => CommonManager.Instance.DB.ReserveList.ContainsKey(info.ReserveID) == false ? info : CommonManager.Instance.DB.ReserveList[info.ReserveID]).ToList();
            var autoIDList = new Func<ReserveData, IEnumerable<AutoAddData>, List<ulong>>((info, autoList) =>
                autoList.Where(item => Settings.Instance.SeparateFixedTuners == false || item.RecSettingInfo.TunerID == info.RecSettingInfo.TunerID).Select(item => item.DataID).ToList());
            var epgAutoList = reslist_org.ToDictionary(info => info.ReserveID, info => autoIDList(info, info.GetEpgAutoAddList(true)));
            var manualAutoList = reslist_org.ToDictionary(info => info.ReserveID, info => autoIDList(info, info.GetManualAutoAddList(true)));

            foreach (AutoAddData data in itemlist)
            {
                var autoList = data is EpgAutoAddData ? epgAutoList : manualAutoList;
                reslist.ForEach(resinfo => autoList[resinfo.ReserveID].Remove(data.DataID));
            }

            // 1)個別予約を除外
            // 2)処理する自動登録リスト以外の有効な自動登録に含まれている予約を除外
            return reslist.FindAll(info => info.IsAutoAdded == true && (epgAutoList[info.ReserveID].Count + manualAutoList[info.ReserveID].Count) == 0);
        }

        private static bool AutoAddCmdSend(IEnumerable<AutoAddData> itemlist, int mode,
            List<ReserveData> delReserveList = null, List<ReserveData> chgReserveList = null, bool cautionMany = true, bool isViewOrder = true, bool noHistory = false)
        {
            if (itemlist.Any() == false) return true;
            return AutoAddCmdSendWork(itemlist, mode, delReserveList, chgReserveList, cautionMany, isViewOrder, noHistory);
        }

        //mode 0:追加、1:変更、2:削除
        private static bool AutoAddCmdSendWork(IEnumerable<AutoAddData> itemlist, int mode,
            List<ReserveData> delReserveList = null, List<ReserveData> chgReserveList = null, bool cautionMany = true, bool isViewOrder = true, bool noHistory = false)
        {
            var message = "自動予約登録の" + (new List<string> { "追加", "変更", "削除" }[(int)mode]);
            if (cautionMany == true && CautionManyMessage(itemlist.Count(), message) == false) return false;

            var epgList = itemlist.OfType<EpgAutoAddData>().ToList();
            var manualList = itemlist.OfType<ManualAutoAddData>().ToList();

            List<EpgAutoAddData> ehist = null;
            List<ManualAutoAddData> mhist = null;
            noHistory |= Settings.Instance.MenuSet.RestoreNoUse;
            if (mode == 1 && noHistory == false)
            {
                //変更時は、一応Send・並び順変更前に元データを確保。
                ehist = epgList.Where(item => CommonManager.Instance.DB.EpgAutoAddList.ContainsKey(item.dataID)).Select(item => CommonManager.Instance.DB.EpgAutoAddList[item.dataID]).DeepClone();
                mhist = manualList.Where(item => CommonManager.Instance.DB.ManualAutoAddList.ContainsKey(item.dataID)).Select(item => CommonManager.Instance.DB.ManualAutoAddList[item.dataID]).DeepClone();
            }

            if (isViewOrder == true)
            {
                //自動予約登録データ変更の前に、並び順を自動保存する。
                if ((AutoAddOrderAutoSave(ref epgList, mode != 0) && AutoAddOrderAutoSave(ref manualList, mode != 0)) == false)
                {
                    MessageBox.Show("自動登録の並べ替え保存中に問題が発生しました。\r\n処理を中止します。", message, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }
            }

            if (delReserveList != null || chgReserveList != null)
            {
                //予約の連動操作がある場合は、直近の予約の有無を先に確認する
                var chkResList = (delReserveList ?? new List<ReserveData>()).Concat((chgReserveList ?? new List<ReserveData>())).ToList();
                if (CheckReserveOnRec(chkResList, "連動処理") == false) return false;
            }

            var cmd = CommonManager.CreateSrvCtrl();//これは単なる表記の省略
            bool ret = false, retE = false, retM = false;
            switch (mode)
            {
                case 0:
                    return ReserveCmdSend(epgList, cmd.SendAddEpgAutoAdd, "キーワード予約の追加", false)
                        && ReserveCmdSend(manualList, cmd.SendAddManualAdd, "プログラム自動予約の追加", false);
                case 1:
                    //ReserveDelete()、ReserveChange()とも自動予約登録の処理後に実施したいが、処理の前後関係の都合上先に実施する。
                    ret = (delReserveList == null ? true : ReserveDelete(delReserveList, false, true, true))
                        && (chgReserveList == null ? true : ReserveChange(chgReserveList, false, true, true))
                        && (retE = ReserveCmdSend(epgList, cmd.SendChgEpgAutoAdd, "キーワード予約の変更", false))
                        && (retM = ReserveCmdSend(manualList, cmd.SendChgManualAdd, "プログラム自動予約の変更", false));
                    break;
                case 2:
                    ret = (retE = ReserveCmdSend(epgList.Select(item => (uint)item.DataID).ToList(), cmd.SendDelEpgAutoAdd, "キーワード予約の削除", false))
                        && (retM = ReserveCmdSend(manualList.Select(item => (uint)item.DataID).ToList(), cmd.SendDelManualAdd, "プログラム自動予約の削除", false))
                        && (delReserveList == null ? true : ReserveDelete(delReserveList, false, true, true));
                    break;
            }
            if (noHistory == false && (retE == true || retM == true))
            {
                var list = new List<AutoAddData>();
                if (retE == true) list.AddRange(ehist ?? epgList.DeepClone());
                if (retM == true) list.AddRange(mhist ?? manualList.DeepClone());
                CmdHistorys.Add(mode == 1 ? EpgCmdsEx.ChgMenu : EpgCmds.Delete, list);
            }
            return ret;
        }

        private static bool AutoAddOrderAutoSave<T>(ref List<T> list, bool changeID) where T : AutoAddData
        {
            //並べ替え不要
            if (list.Count == 0) return true;

            var autoView = CommonManager.MainWindow.autoAddView;
            var view = (list[0] is EpgAutoAddData) ? (AutoAddListView)autoView.epgAutoAddView : autoView.manualAutoAddView;

            if (changeID == true)
            {
                //並べ替えの影響回避(変更以外では生データがそのままくるので。この位置でないとダメ。)
                list = list.DeepClone();
            }

            //並べ替えしなかった
            Dictionary<ulong, ulong> changeIDTable = null;
            if (AutoAddViewOrderCheckAndSave(view, out changeIDTable) == false) return true;

            //並べ替え保存時に何か問題があった
            if (changeIDTable == null) return false;

            if (changeID == true)
            {
                foreach (var item in list)
                {
                    //通常無いはずだが、並べ替えが上手くできない時に継続するのはとても危険なので中止する。
                    if (changeIDTable.ContainsKey(item.DataID) == false) return false;

                    //新しいIDに張り替え
                    item.DataID = changeIDTable[item.DataID];
                }
            }
            return true;
        }

        public static bool? AutoAddViewOrderCheckAndSave(AutoAddListView view, out Dictionary<ulong, ulong> changeIDTable)
        {
            changeIDTable = null;
            try
            {
                if (view == null || view.IsVisible == false || view.dragMover.NotSaved == false) return false;
                //
                var cmdPrm = new EpgCmdParam(null);
                EpgCmds.SaveOrder.Execute(cmdPrm, view);
                changeIDTable = cmdPrm.Data as Dictionary<ulong, ulong>;
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        public static bool RecinfoChgProtect(List<RecFileInfo> itemlist, bool cautionMany = true)
        {
            try
            {
                itemlist.ForEach(item => item.ProtectFlag = (byte)(item.ProtectFlag == 0 ? 1 : 0));
                return ReserveCmdSend(itemlist, CommonManager.CreateSrvCtrl().SendChgProtectRecInfo, "録画情報の変更", cautionMany);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return false;
        }
        public static bool RecinfoDelete(List<RecFileInfo> itemlist, bool cautionMany = true)
        {
            try
            {
                List<uint> list = itemlist.Select(item => item.ID).ToList();
                return ReserveCmdSend(list, CommonManager.CreateSrvCtrl().SendDelRecInfo, "録画情報の削除", cautionMany);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return false;
        }

        public static bool RecWorkMainDataAdd(IEnumerable<IRecWorkMainData> itemlist, bool cautionMany = true)
        {
            var listAutoAdd = itemlist.OfType<AutoAddData>().ToList();
            var listReserve = itemlist.OfType<ReserveData>().ToList();

            if (cautionMany == true && CautionManyMessage(listAutoAdd.Count + listReserve.Count, "項目の追加") == false) return false;

            return AutoAddAdd(listAutoAdd, false) && ReserveAdd(listReserve, false);
        }

        private static bool ReserveCmdSend<T>(List<T> list, Func<List<T>, ErrCode> cmdSend, string description = "", bool cautionMany = true, string msg_other = null)
        {
            try
            {
                if (list.Count == 0) return true;

                if (cautionMany == true && CautionManyMessage(list.Count, description) == false) return false;

                ErrCode err = cmdSend(list);
                return CommonManager.CmdErrMsgTypical(err, description, msg_other);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return false;
        }
        public static bool CautionManyMessage(int Count, string description = "")
        {
            if (Settings.Instance.CautionManyChange == true && Count >= Settings.Instance.CautionManyNum)
            {
                if (MessageBox.Show("多数の項目を処理しようとしています。\r\nよろしいですか?\r\n"
                    + "　項目数: " + Count + "\r\n"
                    , description, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Cancel)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool? OpenEpgReserveDialog(EpgEventInfo Data, int epgInfoOpenMode = 0, RecSettingData setInfo = null)
        {
            try
            {
                if (AddReserveEpgWindow.ChangeDataLastUsedWindow(Data) != null) return true;

                //番組表でのダブルクリック時のフォーカス対策
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => new AddReserveEpgWindow(Data, epgInfoOpenMode, setInfo).Show()));
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        public static bool? OpenChangeReserveDialog(uint id, int epgInfoOpenMode = 0)
        {
            ReserveData data;
            if (CommonManager.Instance.DB.ReserveList.TryGetValue(id, out data) == false) return false;
            return OpenChangeReserveDialog(data, epgInfoOpenMode);
        }
        public static bool? OpenChangeReserveDialog(ReserveData Data, int epgInfoOpenMode = 0)
        {
            if (ChgReserveWindow.ChangeDataLastUsedWindow(Data) != null) return true;
            return OpenChgReserveDialog(Data, epgInfoOpenMode);
        }
        public static bool? OpenManualReserveDialog(RecSettingData setInfo = null)
        {
            return OpenChgReserveDialog(null, 0, setInfo);
        }
        public static bool? OpenChgReserveDialog(ReserveData Data, int epgInfoOpenMode = 0, RecSettingData setInfo = null)
        {
            try
            {
                //番組表でのダブルクリック時のフォーカス対策
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => new ChgReserveWindow(Data, epgInfoOpenMode, setInfo).Show()));
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        public static bool? OpenSearchEpgDialog(string searchWord = null)
        {
            return OpenEpgAutoAddDialog(null, AutoAddMode.Find, searchWord);
        }
        public static bool? OpenAddEpgAutoAddDialog(string searchWord = null)
        {
            return OpenEpgAutoAddDialog(null, AutoAddMode.NewAdd, searchWord);
        }
        public static bool? OpenChangeEpgAutoAddDialog(EpgAutoAddData Data)
        {
            if (SearchWindow.ChangeDataLastUsedWindow(Data) != null) return true;
            return OpenEpgAutoAddDialog(Data, AutoAddMode.Change);
        }
        private static bool? OpenEpgAutoAddDialog(EpgAutoAddData Data, AutoAddMode mode, string searchWord = null)
        {
            try
            {
                new SearchWindow(Data, mode, searchWord).Show();
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }
        public static void SendAutoAdd(IBasicPgInfo item, bool NotToggle = false, EpgSearchKeyInfo key = null)
        {
            try
            {
                if (item == null) return;

                var dlg = new SearchWindow(mode: AutoAddMode.NewAdd);
                dlg.SetSearchKey(key ?? SendAutoAddKey(item, NotToggle));

                var item_r = item as IRecSetttingData;
                if (item_r != null)
                {
                    RecPresetItem recPreSet = item_r.RecSettingInfo.LookUpPreset(item_r.IsManual, true);
                    RecSettingData recSet = recPreSet.Data;
                    if (recPreSet.IsCustom == true && recSet.IsEnable == false)
                    {
                        recSet.IsEnable = true;
                    }
                    dlg.SetRecSetting(recSet);
                }

                dlg.Show();
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }
        public static EpgSearchKeyInfo SendAutoAddKey(IBasicPgInfo item, bool NotToggle = false, EpgSearchKeyInfo refkey = null)
        {
            var key = (refkey ?? Settings.Instance.SearchPresetList[0].Data).DeepClone();
            key.regExpFlag = 0;
            if (refkey == null)
            {
                key.notContetFlag = 0;
                key.contentList.Clear();
            }
            if (item == null) return key;

            key.andKey = TrimEpgKeyword(item.DataTitle, NotToggle);
            key.serviceList = ((long)item.Create64Key()).IntoList();

            var eventInfo = item as EpgEventInfo;
            if (eventInfo != null && Settings.Instance.MenuSet.SetJunreToAutoAdd == true)
            {
                key.notContetFlag = 0;
                key.contentList.Clear();
                if (eventInfo.ContentInfo != null)
                {
                    var kindList = eventInfo.ContentInfo.nibbleList.Where(info => info.IsAttributeInfo == false);
                    if (Settings.Instance.MenuSet.SetJunreContentToAutoAdd == true)
                    {
                        kindList = kindList.GroupBy(info => info.CategoryKey).Select(gr => new EpgContentData(gr.Key));
                    }
                    key.contentList = kindList.DeepClone();
                }
            }
            return key;
        }

        public static bool? OpenAddManualAutoAddDialog()
        {
            return OpenManualAutoAddDialog(null, AutoAddMode.NewAdd);
        }
        public static bool? OpenChangeManualAutoAddDialog(ManualAutoAddData Data)
        {
            if (AddManualAutoAddWindow.ChangeDataLastUsedWindow(Data) != null) return true;
            return OpenManualAutoAddDialog(Data, AutoAddMode.Change);
        }
        public static bool? OpenManualAutoAddDialog(ManualAutoAddData Data, AutoAddMode mode)
        {
            try
            {
                new AddManualAutoAddWindow(Data, mode).Show();
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        public static bool? OpenChangeAutoAddDialog(Type t, uint id)
        {
            AutoAddData autoAdd = AutoAddData.AutoAddList(t, id);
            if (t == typeof(EpgAutoAddData))
            {
                return OpenChangeEpgAutoAddDialog(autoAdd as EpgAutoAddData);
            }
            else if (t == typeof(ManualAutoAddData))
            {
                return OpenChangeManualAutoAddDialog(autoAdd as ManualAutoAddData);
            }
            return null;
        }

        public static bool? OpenRecInfoDialog(RecFileInfo info)
        {
            try
            {
                if (RecInfoDescWindow.ChangeDataLastUsedWindow(info) != null) return true;

                //番組表でのダブルクリック時のフォーカス対策
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => new RecInfoDescWindow(info).Show()));

                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        public static bool? OpenInfoSearchDialog(string word = null, bool? NotToggle = null)
        {
            try
            {
                if (NotToggle != null)
                {
                    word = TrimKeywordCheckToggled(word, Settings.Instance.MenuSet.InfoSearchTitle_Trim, (bool)NotToggle);
                }
                new InfoSearchWindow(word).Show();
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        public static EpgEventInfo GetPgInfoUid(ulong uid, Dictionary<ulong, EpgEventInfo> currentList = null)
        {
            EpgEventInfo data;
            (currentList ?? CommonManager.Instance.DB.EventUIDList).TryGetValue(uid, out data);
            return data;
        }
        public static EpgEventInfo GetPgInfoUidAll(ulong uid)
        {
            //EPGが読み込まれているときなど
            EpgEventInfo hit = GetPgInfoUid(uid);
            if (hit != null) return hit;

            //検索番組表の情報も探す
            foreach (EpgView.EpgViewData viewData in CommonManager.MainWindow.epgView.GetAllEpgEventList())
            {
                if (viewData.IsEpgLoaded == false || viewData.EpgTabInfo.SearchMode == false) continue;
                if (viewData.EventUIDList.TryGetValue(uid, out hit)) break;
            }
            return hit;
        }

        public static EpgEventInfo GetPgInfoLikeThat(IAutoAddTargetData trg, IEnumerable<EpgServiceEventInfo> currentList = null, IEnumerable<EpgEventInfo> currentEventList = null)
        {
            var eventList = new List<EpgEventInfo>();
            ulong key = trg.Create64Key();
            if (currentEventList != null)
            {
                eventList = currentEventList.Where(info => info.Create64Key() == key).ToList();
            }
            else if (currentList != null)
            {
                EpgServiceEventInfo sInfo = currentList.FirstOrDefault(info => info.serviceInfo.Key == key);
                if (sInfo != null)
                {
                    var sAllInfo = sInfo as EpgServiceAllEventInfo;
                    eventList = sAllInfo != null ? sAllInfo.eventMergeList.ToList() : sInfo.eventList;
                }
            }
            else
            {
                EpgServiceAllEventInfo sInfo;
                CommonManager.Instance.DB.ServiceEventList.TryGetValue(key, out sInfo);
                if (sInfo != null) eventList = sInfo.eventMergeList.ToList();
            }

            EpgEventInfo hit = null;
            
            //イベントベースで見つかるならそれを返す
            if ((ushort)trg.Create64PgKey() != 0xFFFF)
            {
                ulong PgUID = trg.CurrentPgUID();
                hit = eventList.Find(pg => pg.CurrentPgUID() == PgUID);
                if (hit != null) return hit;
            }

            double dist = double.MaxValue;
            foreach (EpgEventInfo eventChkInfo in eventList)
            {
                //itemが調べている番組に完全に含まれているならそれを選択する
                double overlapLength = CulcOverlapLength(trg.PgStartTime, trg.PgDurationSecond,
                                                        eventChkInfo.start_time, eventChkInfo.durationSec);
                if (overlapLength > 0 && overlapLength == trg.PgDurationSecond)
                {
                    hit = eventChkInfo;
                    break;
                }

                //開始時間が最も近いものを選ぶ。同じ差なら時間が前のものを選ぶ
                double dist1 = Math.Abs((trg.PgStartTime - eventChkInfo.start_time).TotalSeconds);
                if (overlapLength >= 0 && (dist > dist1 ||
                    dist == dist1 && (hit == null || trg.PgStartTime > eventChkInfo.start_time)))
                {
                    dist = dist1;
                    hit = eventChkInfo;
                    if (dist == 0) break;
                }
            }
            return hit;
        }
        public static EpgEventInfo GetPgInfoLikeThatAll(IAutoAddTargetData trg)
        {
            //EPGが読み込まれているときなど
            EpgEventInfo hit = GetPgInfoLikeThat(trg);
            if (hit != null) return hit;

            //検索番組表の情報も探す
            foreach (EpgView.EpgViewData viewData in CommonManager.MainWindow.epgView.GetAllEpgEventList())
            {
                if (viewData.IsEpgLoaded == false || viewData.EpgTabInfo.SearchMode == false) continue;
                if (viewData.EventUIDList.TryGetValue(trg.CurrentPgUID(), out hit)) break;
                hit = GetPgInfoLikeThat(trg, viewData.ServiceEventList);
                if (hit != null) break;
            }
            return hit;
        }

        /// <summary>重複してない場合は負数が返る。</summary>
        public static double CulcOverlapLength(DateTime s1, uint d1, DateTime s2, uint d2)
        {
            TimeSpan ts1 = s1 + TimeSpan.FromSeconds(d1) - s2;
            TimeSpan ts2 = s2 + TimeSpan.FromSeconds(d2) - s1;
            return Math.Min(Math.Min(ts1.TotalSeconds, ts2.TotalSeconds), Math.Min(d1, d2));
        }

        public static List<EpgAutoAddData> FazySearchEpgAutoAddData(string title, bool? IsEnabled = null)
        {
            Func<string, string> _regulate_str = s => CommonManager.AdjustSearchText(TrimKeyword(s));

            string title_key = _regulate_str(title);

            List<EpgAutoAddData> list = CommonManager.Instance.DB.EpgAutoAddList.Values
                .Where(data => data.DataTitle != "" && title_key.Contains(_regulate_str(data.DataTitle)) == true).ToList();

            foreach (ReserveData info in CommonManager.Instance.DB.ReserveList.Values
                .Where(data => data.DataTitle != "" && title_key == _regulate_str(data.DataTitle)))
            {
                list.AddRange(info.GetEpgAutoAddList());
            }

            list = list.Distinct().ToList();
            return IsEnabled == null ? list : list.FindAll(data => data.IsEnabled == IsEnabled);
        }

        public static void JumpTab(object target, CtxmCode trg_code)
        {
            if (target == null) return;
            BlackoutWindow.SelectedData = target;
            CommonManager.MainWindow.moveTo_tabItem(trg_code);
        }
        public static bool CheckJumpTab(SearchItem target, bool switchTab = false)
        {
            return CommonManager.MainWindow.epgView.SearchJumpTargetProgram(target, !switchTab);
        }
    }
}
