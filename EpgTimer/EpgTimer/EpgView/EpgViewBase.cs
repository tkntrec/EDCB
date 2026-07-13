using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EpgTimer.EpgView
{
    public interface IEpgViewDataSet
    {
        void SetViewData(EpgViewData data);
    }
    public class EpgViewData : IEpgSettingAccess
    {
        //表示形式間で番組表定義と番組リストを共有する
        //EpgTimerNWで検索絞り込みを使用時に多少効果があるくらいだが‥
        public EpgViewData()
        {
            EpgTabInfo = new CustomEpgTabInfo();
            viewFunc = new EpgDataView.EpgDataViewInterface(null);
            ClearEventList();
        }
        public void ClearEventList()
        {
            ServiceEventList = new List<EpgServiceEventInfo>();
            EventUIDList = new Dictionary<ulong, EpgEventInfo>();
            IsEpgLoaded = false;
        }
        public EpgDataView.EpgDataViewInterface viewFunc { get; set; }
        private CustomEpgTabInfo epgTabInfo;
        public CustomEpgTabInfo EpgTabInfo
        {
            get { return epgTabInfo; }
            set
            {
                epgTabInfo = value;
                EpgSettingIndex = epgTabInfo.EpgSettingIndex;
                ReplaceDictionaryNormal = CommonManager.GetReplaceDictionaryNormal(this.EpgStyle());
                ReplaceDictionaryTitle = CommonManager.GetReplaceDictionaryTitle(this.EpgStyle());
                DefPeriod = new EpgViewPeriodDef(this.EpgStyle());
                Period = DefPeriod.DefPeriod;
            }
        }
        public bool HasKey(ulong key) { return KeyList.Contains(key); }
        public IEnumerable<ulong> KeyList { get { return IsEpgLoaded ? ServiceEventList.Select(info => info.serviceInfo.Key) : CommonManager.Instance.DB.ExpandSpecialKey(EpgTabInfo.ViewServiceList); } }
        public bool IsEpgLoaded { get; private set; }
        public List<EpgServiceEventInfo> ServiceEventList { get; private set; }
        public Dictionary<ulong, EpgEventInfo> EventUIDList { get; private set; }
        public HashSet<ulong> EventFilteredHash { get; private set; }

        public int EpgSettingIndex { get; private set; }
        public Dictionary<char, List<KeyValuePair<string, string>>> ReplaceDictionaryNormal { get; private set; }
        public Dictionary<char, List<KeyValuePair<string, string>>> ReplaceDictionaryTitle { get; private set; }

        public EpgViewPeriodDef DefPeriod;
        public EpgViewPeriod Period;
        public bool IsDefPeriod = true;

        public bool ReloadEpgData(EpgViewPeriod newPeriod = null, bool noMsg = false)
        {
            try
            {
                newPeriod = newPeriod ?? DefPeriod.DefPeriod;
                if (Period.Equals(newPeriod) == true && IsEpgLoaded == true) return true;

                if (CommonManager.Instance.WaitingSrvReady == true)
                {
                    StatusManager.StatusNotifySet("EpgTimerSrv準備完了待ち");
                    return false;
                }
                if (CommonManager.Instance.IsConnected == false) return false;

                ErrCode err;
                var serviceDic = new Dictionary<ulong, EpgServiceAllEventInfo>();
                if (EpgTabInfo.SearchMode == false)
                {
                    err = CommonManager.Instance.DB.LoadEpgData(ref serviceDic, newPeriod, EpgTabInfo.ViewServiceList);
                }
                else
                {
                    //番組情報の検索
                    err = CommonManager.Instance.DB.SearchPgLists(EpgTabInfo.GetSearchKeyReloadEpg().IntoList(), ref serviceDic, newPeriod);
                }
                if ((noMsg && err != ErrCode.CMD_SUCCESS) 
                    || CommonManager.CmdErrMsgTypical(err, "EPGデータの取得", err == ErrCode.CMD_ERR_BUSY ?
                    "EPGデータの読み込みを行える状態ではありません。\r\n(EPGデータ読み込み中など)" :
                    "エラーが発生しました。\r\nEPGデータが読み込まれていない可能性があります。") == false) return false;

                //並び順はViewServiceListによる。eventListはこの後すぐ作り直すのでとりあえずそのままもらう。
                ServiceEventList = CommonManager.Instance.DB.ExpandSpecialKey(EpgTabInfo.ViewServiceList, serviceDic.Values.Select(info => info.serviceInfo))
                    .Where(id => serviceDic.ContainsKey(id)).Select(id => serviceDic[id])
                    .Select(info => new EpgServiceEventInfo { serviceInfo = info.serviceInfo, eventList = info.eventMergeList.ToList() }).ToList();

                EventUIDList = new Dictionary<ulong, EpgEventInfo>();
                EventFilteredHash = new HashSet<ulong>();
                var viewContentMatchingHash = new HashSet<uint>(EpgTabInfo.ViewContentList.Select(d => d.MatchingKeyList).SelectMany(x => x));
                foreach (EpgServiceEventInfo item in ServiceEventList)
                {
                    item.eventList = item.eventList.FindAll(eventInfo =>
                    {
                        //開始時間未定を除外
                        bool ret = (eventInfo.StartTimeFlag != 0)

                        //自動登録されたりするので、サービス別番組表では表示させる
                        //&& (eventInfo.IsGroupMainEvent == true)

                        //表示抑制
                        && (eventInfo.IsOver(newPeriod.Start) == false && eventInfo.PgStartTime < newPeriod.End);

                        if (ret == false) return false;

                        //ジャンル絞り込み
                        bool filtered = !ViewUtil.ContainsContent(eventInfo, viewContentMatchingHash, EpgTabInfo.ViewNotContentFlag);
                        if (EpgTabInfo.HighlightContentKind && filtered)
                        {
                            EventFilteredHash.Add(eventInfo.CurrentPgUID());
                        }

                        return EpgTabInfo.HighlightContentKind || !filtered;
                    });
                    item.eventList.ForEach(data => EventUIDList[data.CurrentPgUID()] = data);
                }

                IsEpgLoaded = true;
                Period = newPeriod.DeepClone();
                IsDefPeriod = Period.Equals(DefPeriod.DefPeriod);
                return true;
            }
            catch (Exception ex) { CommonUtil.DispatcherMsgBoxShow(ex.ToString()); }
            return false;
        }
    }

    public class EpgViewState { public int viewMode; }

    public class EpgViewBase : DataItemViewBase, IEpgSettingAccess, IEpgViewDataSet
    {
        public static event ViewUpdatedHandler ViewReserveUpdated = null;

        protected CmdExeReserve mc; //予約系コマンド集
        protected bool ReloadLogoFlg = true;
        protected bool ReloadReserveInfoFlg = true;
        protected bool RefreshMenuFlg = true;

        protected EpgViewState restoreState = null;
        protected class StateBase : EpgViewState
        {
            public EpgViewPeriod period = null;
            public bool? isDefPeriod = null;

            public StateBase() { }
            public StateBase(EpgViewBase view)
            {
                viewMode = view.viewMode;
                period = view.ViewPeriod.DeepClone();
                isDefPeriod = view.IsDataDefPeriod;
            }
        }
        public virtual void SetViewState(EpgViewState data) { restoreState = data; }
        public virtual EpgViewState GetViewState() { return new StateBase(this); }
        protected StateBase RestoreState { get { return restoreState as StateBase ?? new StateBase(); } }

        //表示形式間で番組表定義と番組リストを共有する
        //EpgTimerNWで検索絞り込みを使用時に多少効果があるくらいだが‥
        protected EpgViewData viewData = new EpgViewData();
        protected EpgDataView.EpgDataViewInterface viewFunc { get { return viewData.viewFunc; } }
        protected int viewMode = 0;//最初に設定した後は固定するコード。
        public void SetViewData(EpgViewData data, int mode)
        {
            viewMode = mode;
            SetViewData(data);
        }
        public virtual void SetViewData(EpgViewData data)
        {
            viewData = data;
            ViewPeriod = DataPeriod.DeepClone(); ;
        }
        protected CustomEpgTabInfo viewInfo { get { return viewData.EpgTabInfo; } }
        protected virtual bool viewCustNeedTimeOnly { get { return viewInfo.NeedTimeOnlyBasic; } }
        public int EpgSettingIndex { get { return viewData.EpgSettingIndex; } }
        protected bool IsDataDefPeriod { get { return viewData.IsDefPeriod; } set { viewData.IsDefPeriod = value; } }
        protected EpgViewPeriodDef DefPeriod { get { return viewData.DefPeriod; } }
        protected EpgViewPeriod ViewPeriod = new EpgViewPeriod();
        protected EpgViewPeriod DataPeriod { get { return viewData.Period; } }
        protected bool IsJumpPanelOpened { get { return viewFunc.IsJumpPanelOpened; } set { viewFunc.IsJumpPanelOpened = value; } }
        protected List<EpgServiceEventInfo> serviceEventList { get { return viewData.ServiceEventList; } }
        protected List<EpgServiceInfo> serviceListOrderAdjust
        {
            get
            {
                var grpList = new SortedList<ulong, EpgServiceInfo>();
                var ordered = new List<EpgServiceInfo>();
                var back = new EpgServiceInfo();
                foreach (EpgServiceInfo info in serviceEventList.Select(item => item.serviceInfo))
                {
                    if (info.ONID != back.ONID || info.TSID != back.TSID || info.SID > back.SID)
                    {
                        ordered.AddRange(grpList.Values);
                        grpList.Clear();
                        back = info;
                    }
                    grpList[info.SID] = info;
                }
                ordered.AddRange(grpList.Values);
                return ordered;
            }
        }

        protected virtual void InitCommand()
        {
            base.updateInvisible = true;

            //ビューコードの登録
            mBinds.View = CtxmCode.EpgView;

            //コマンド集の初期化
            mc = new CmdExeReserve(this);
            mc.SetFuncGetRecSetting(() => viewInfo.RecSetting.DeepClone());
            mc.SetFuncGetSearchKey(() => viewInfo.SearchMode ? viewInfo.SearchKey.DeepClone() : null);

            //コマンド集にないものを登録
            mc.AddReplaceCommand(EpgCmds.ViewChgSet, (sender, e) => viewFunc.ViewSetting(-1));
            mc.AddReplaceCommand(EpgCmds.ViewChgReSet, (sender, e) => viewFunc.ViewSetting(-2));
            mc.AddReplaceCommand(EpgCmds.ViewChgMode, mc_ViewChgMode);

            //コマンド集を振り替えるもの
            mc.AddReplaceCommand(EpgCmds.JumpTable, mc_JumpTable);

            //過去番組関係
            ViewPeriod = DefPeriod.DefPeriod;
        }

        //表示設定関係
        protected void mc_ViewChgMode(object sender, ExecutedRoutedEventArgs e)
        {
            var param = e.Parameter as EpgCmdParam;
            if (param == null || param.ID == viewMode) return;
            mc_ViewChgMode(param.ID);
        }
        protected void mc_ViewChgMode(int mode)
        {
            try
            {
                //BlackWindowに状態を登録。
                //mc.GetJumpTabItem()はコマンド集の機能による各ビューの共用メソッド。
                BlackoutWindow.SelectedData = mc.GetJumpTabItem() ?? GetJumpTabItemNear() ?? (mode <= 1 ? GetJumpTabService() : null);

                viewFunc.ViewSetting(mode);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }
        protected virtual object GetJumpTabItemNear() { return null; }
        protected virtual object GetJumpTabService() { return null; }
        protected void mc_JumpTable(object sender, ExecutedRoutedEventArgs e)
        {
            var param = e.Parameter as EpgCmdParam;
            if (param == null) return;

            param.ID = 0;//実際は設定するまでもなく、初期値0。
            BlackoutWindow.NowJumpTable = true;
            mc_ViewChgMode(sender, e);

            //EPG画面でのフォーカス対策。若干ウィンドウの表示タイミングが微妙だが、とりあえずこれで解決する。
            //切替え自体は上のmc_ViewChgMode()が行っており、このコードはスプラッシュを表示するだけ。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                new BlackoutWindow(CommonManager.MainWindow).showWindow(CommonManager.MainWindow.tabItem_epg.Header.ToString());
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void UpdateMenu(bool refesh = true)
        {
            RefreshMenuFlg |= refesh;
            RefreshMenu();
        }
        protected void RefreshMenu()
        {
            if (RefreshMenuFlg == true && this.IsVisible == true)
            {
                RefreshMenuInfo();
                RefreshMenuFlg = false;
            }
        }
        protected virtual void RefreshMenuInfo()
        {
            mc.EpgInfoOpenMode = this.EpgStyle().EpgInfoOpenMode;
        }

        /// 保存関係
        public virtual void SaveViewData() { }

        //表示期間変更関係
        //mode: -1=戻る、+1=進む
        protected TimeJumpView jumpPanel = null;
        protected TimeMoveView movePanel = null;
        protected Button buttonNow = null;
        protected void SetControlsPeriod(TimeJumpView tj, TimeMoveView tm, Button bn)
        {
            jumpPanel = tj;
            movePanel = tm;
            buttonNow = bn;

            jumpPanel.JumpDateClick += period => JumpDate(period, true);
            jumpPanel.SetViewData(viewData);
            movePanel.OpenToggleClick += MovePanel_OpenToggleClick;
            movePanel.MoveButtonClick += direction => JumpDate(MoveTimeTarget(direction), true);
            movePanel.MoveButtonToolTipOpen += MovePanel_MoveButtonTooltip;
            buttonNow.Click += (sender, e) => NowTimeClick(true);
        }

        private void MovePanel_OpenToggleClick(bool isOpen)
        {
            IsJumpPanelOpened = isOpen;
            RefreshMoveButtonStatus();
        }
        protected virtual void SetJumpState() { }
        protected EpgViewPeriod MoveTimeTarget(int direction)
        {
            var start = ViewPeriod.Start;
            if (this.EpgStyle().EpgArcStartSunday && start.DayOfWeek != DayOfWeek.Sunday)
            {
                start += TimeSpan.FromDays(direction * (direction < 0 ? DefPeriod.InitDays : ViewPeriod.Days));
                var offset = (int)start.DayOfWeek;
                if (offset != 0)
                {
                    start += TimeSpan.FromDays(-offset + (direction < 0 ? 7 : 0));
                }
            }
            else
            {
                start += TimeSpan.FromDays(direction * (direction < 0 ? DefPeriod.InitMoveDays : ViewPeriod.MoveDays));
            }
            return start >= DefPeriod.InitStart ? DefPeriod.DefPeriod : new EpgViewPeriod(start, DefPeriod.InitDays);
        }
        public void MovePanel_MoveButtonTooltip(Button btn, ToolTipEventArgs e, int mode)
        {
            e.Handled = !btn.IsEnabled;
            btn.ToolTip = MoveTimeTarget(mode).ConvertText(DefPeriod.DefPeriod.End);
        }
        public void JumpDate(EpgViewPeriod period = null, bool IsSetJumpState = false)
        {
            if(IsSetJumpState) SetJumpState();
            period = period ?? DefPeriod.DefPeriod;
            if (period.Equals(ViewPeriod)) return;
            ViewPeriod = period.DeepClone();
            IsDataDefPeriod = false;
            UpdateInfo(true);
        }
        protected virtual void RefreshMoveButtonStatus()
        {
            buttonNow.ToolTip = (IsDataDefPeriod ? null : "番組表を元に戻して、") + "現在時刻へスクロール";
            buttonNow.Tag = IsDataDefPeriod ? null : "jump";

            movePanel.SetButtonEnabled(
                ViewPeriod.Start > CommonManager.Instance.DB.EventTimeMin,
                ViewPeriod.End < (IsDataDefPeriod ? ViewPeriod.End : DefPeriod.DefPeriod.End),
                IsJumpPanelOpened);

            jumpPanel.Visibility = IsJumpPanelOpened ? Visibility.Visible : Visibility.Collapsed;
            jumpPanel.SetDate(ViewPeriod, CommonManager.Instance.DB.EventTimeMin);
        }
        protected virtual void NowTimeClick(bool buttonAction)
        {
            if (buttonAction && buttonNow.Tag != null)
            {
                JumpDate();
                return;
            }
            MoveNowTime();
        }
        protected virtual void MoveNowTime() { }

        /// <summary>
        /// ロゴ更新通知
        /// </summary>
        public void UpdateLogo(bool immediately = true)
        {
            ReloadLogoFlg = true;
            if (immediately == true) ReloadLogo();
        }
        protected void ReloadLogo()
        {
            if (ReloadLogoFlg == true)
            {
                ReloadServiceLogo();
                ReloadLogoFlg = false;
            }
        }
        protected virtual void ReloadServiceLogo() { }

        /// <summary>
        /// 予約情報更新通知
        /// </summary>
        public void UpdateReserveInfo(bool immediately = true)
        {
            ReloadReserveInfoFlg = true;
            if (immediately == true) ReloadReserveInfo();
        }
        protected void ReloadReserveInfo()
        {
            if (ReloadReserveInfoFlg == true)
            {
                ReloadReserveInfoFlg = !ReloadReserveInfoData();
                if (ViewReserveUpdated != null) ViewReserveUpdated(this, true);
                UpdateStatus();
            }
        }
        protected bool ReloadReserveInfoData()
        {
            CommonManager.Instance.DB.ReloadRecFileInfo();//起動直後用
            ReloadReserveViewItem();
            return true;
        }
        protected virtual void ReloadReserveViewItem() { }

        /// <summary>EPGデータ更新</summary>
        protected override bool ReloadInfoData()
        {
            EpgViewPeriod newPeriod = RestoreState.isDefPeriod == true ? DefPeriod.DefPeriod : RestoreState.period ?? (IsDataDefPeriod ? DefPeriod.DefPeriod : ViewPeriod);
            if (!viewData.ReloadEpgData(newPeriod, !this.IsVisible)) return false;
            ViewPeriod = DataPeriod.DeepClone();
            RefreshMoveButtonStatus();

            ReloadReserveInfoFlg = true;
            ReloadProgramViewItem();
            if (ReloadReserveInfoFlg == true) ReloadReserveInfoFlg = !ReloadReserveInfoData();
            restoreState = null;

            if (viewData.EpgTabInfo.SearchMode && Settings.Instance.NgAutoEpgLoadNW && Settings.Instance.PrebuildEpg == false
                && ViewPeriod.End > CommonUtil.EdcbNowEpg && CommonManager.Instance.DB.ReserveList.Values.Any(r => r.IsManual))
            {
                CommonManager.MainWindow.MainProc(MainProcItem.EpgDataSearch);
            }
            return true;
        }
        protected virtual void ReloadProgramViewItem() { }

        protected override void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible == false) return;

            RefreshMenu();
            JumpDate(BlackoutWindow.HasItemData ? SearchJumpPeriod(BlackoutWindow.ItemData.PgStartTime) : DataPeriod);
            ReloadInfo();//JumpDate()が実行された場合は、何もしない
            ReloadLogo();
            ReloadReserveInfo();//JumpDate() or ReloadInfo()が実行された場合は、何もしない

            if (BlackoutWindow.HasItemData)
            {
                //「番組表へジャンプ」の場合、またはオプションで指定のある場合に強調表示する。
                var isMarking = (BlackoutWindow.NowJumpTable || this.EpgStyle().DisplayNotifyEpgChange) ? JumpItemStyle.JumpTo : JumpItemStyle.None;
                bool mgs = MoveToItem(BlackoutWindow.SelectedItem, isMarking) == false && BlackoutWindow.NowJumpTable == true;
                StatusManager.StatusNotifySet(mgs == false ? "" : "アイテムが見つかりませんでした < 番組表へジャンプ");
            }
            BlackoutWindow.Clear();

            RefreshMoveButtonStatus();
            RefreshStatus();
        }
        protected EpgViewPeriod SearchJumpPeriod(DateTime time)
        {
            if (DataPeriod.Contains(time) == true) return DataPeriod;
            if (DefPeriod.DefPeriod.Contains(time) == true) return DefPeriod.DefPeriod;

            //見つからない場合はそのまま
            if (CommonManager.Instance.DB.IsEventTimePossible(time) == false) return DataPeriod;

            return new EpgViewPeriod(time - TimeSpan.FromDays(this.EpgStyle().EpgArcStartSunday ? (int)time.DayOfWeek : 0), DefPeriod.InitDays);
        }
        public bool IsEnabledJumpTab(SearchItem target)
        {
            return MoveToItem(target, JumpItemStyle.None, true);
        }
        public bool MoveToItem(SearchItem target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            if (target == null) return false;
            if (target.ReserveInfo != null)
            {
                ReloadReserveInfo();
                if(target.ReserveInfo is ReserveDataEnd)
                {
                    return MoveToRecInfoItem(target.ReserveInfo.GetRecinfoFromPgUID(), style, dryrun) >= 0;
                }
                else
                {
                    return MoveToReserveItem(target.ReserveInfo, style, dryrun) >= 0;
                }
            }
            return MoveToProgramItem(target.EventInfo, style, dryrun) >= 0;
        }
    }
}
