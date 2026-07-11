using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace EpgTimer.EpgView
{
    public class EpgMainViewBase : EpgViewBase
    {
        protected class StateMainBase : StateBase
        {
            public DateTime? scrollTime = null;
            public bool? isJumpDate = null;

            public StateMainBase() { }
            public StateMainBase(EpgMainViewBase view) : base(view) { scrollTime = view.GetScrollTime(); }
        }
        public override EpgViewState GetViewState() { return new StateMainBase(this); }
        protected new StateMainBase RestoreState { get { return restoreState as StateMainBase ?? new StateMainBase(); } }

        protected Dictionary<ulong, ProgramViewItem> programList = new Dictionary<ulong, ProgramViewItem>();
        protected List<ReserveViewItem> reserveList = new List<ReserveViewItem>();
        protected List<ReserveViewItem> recinfoList = new List<ReserveViewItem>();
        protected IEnumerable<ReserveViewItem> dataItemList { get { return recinfoList.Concat(reserveList); } }
        protected List<DateTime> timeList = new List<DateTime>();
        protected DispatcherTimer nowViewTimer;
        protected Point clickPos;

        private ProgramView programView = null;
        private TimeView timeView = null;
        private ScrollViewer horizontalViewScroll = null;

        protected ContextMenu cmdMenu = new ContextMenuEx();
        protected ContextMenu cmdMenuView = new ContextMenuEx();

        protected override void InitCommand()
        {
            base.InitCommand();

            //コマンド集の初期化の続き
            mc.SetFuncGetDataList(isAll => isAll == true ? dataItemList.GetDataList() : programView.GetReserveViewData(clickPos).GetDataList());
            mc.SetFuncGetEpgEventList(() => 
            {
                ProgramViewItem hitItem = programView.GetProgramViewData(clickPos);
                return hitItem != null && hitItem.Data != null ? new List<EpgEventInfo> { hitItem.Data } : new List<EpgEventInfo>();
            });

            //コマンド集からコマンドを登録
            mc.ResetCommandBindings(this, cmdMenu, cmdMenuView);

            //現在ラインの描画を追加
            nowViewTimer = new DispatcherTimer(DispatcherPriority.Normal);
            nowViewTimer.Tick += (sender, e) => ReDrawNowLine();
            this.Unloaded += (sender, e) => nowViewTimer.Stop();//アンロード時にReDrawNowLine()しないパスがある。
            this.IsVisibleChanged += (sender, e) => ReDrawNowLine();
        }
        protected override void RefreshMenuInfo()
        {
            base.RefreshMenuInfo();
            mBinds.ResetInputBindings(this);
            cmdMenu.Tag = viewMode;
            cmdMenuView.Tag = viewMode;
            mm.CtxmGenerateContextMenu(cmdMenu, CtxmCode.EpgView, false);
            mm.CtxmGenerateContextMenuEpgView(cmdMenuView);
        }

        public override void SetViewData(EpgViewData data)
        {
            base.SetViewData(data);
            programView.SetViewData(viewData);
            timeView.SetViewData(viewData);
        }
        public void SetControls(ProgramView pv, TimeView tv, ScrollViewer hv)
        {
            programView = pv;
            timeView = tv;
            horizontalViewScroll = hv;

            programView.ScrollChanged += epgProgramView_ScrollChanged;
            programView.LeftDoubleClick += (sender, cursorPos) => EpgCmds.ShowDialog.Execute(null, cmdMenu);
            programView.MouseClick += (sender, cursorPos) => clickPos = cursorPos;
            programView.RightClick += epgProgramView_RightClick;
        }

        protected override void UpdateStatusData(int mode = 0)
        {
            this.status[1] = string.Format("番組数:{0}", programList.Count)
                + ViewUtil.ConvertReserveStatus(reserveList.GetDataList(), "　予約");
        }

        protected virtual DateTime GetViewTime(DateTime time) { return time; }
        protected virtual DateTime LimitedStart(IBasicPgInfo info) { return info.PgStartTime; }
        protected virtual uint LimitedDuration(IBasicPgInfo info) { return info.PgDurationSecond; }

        /// <summary>番組の縦表示位置設定</summary>
        protected virtual void SetProgramViewItemVertical()
        {
            //時間リストを構築
            if (viewCustNeedTimeOnly == true)
            {
                var timeSet = new HashSet<DateTime>();
                foreach (ProgramViewItem item in programList.Values)
                {
                    ViewUtil.AddTimeList(timeSet, GetViewTime(LimitedStart(item.Data)), LimitedDuration(item.Data));
                }
                timeList.AddRange(timeSet.OrderBy(time => time));
            }

            //縦位置を設定
            foreach (ProgramViewItem item in programList.Values)
            {
                ViewUtil.SetItemVerticalPos(timeList, item, GetViewTime(LimitedStart(item.Data)), LimitedDuration(item.Data), this.EpgStyle().MinHeight, viewCustNeedTimeOnly);
            }

            //最低表示行数を適用。また、最低表示高さを確保して、位置も調整する。
            ViewUtil.ModifierMinimumLine(programList.Values, this.EpgStyle().MinimumHeight, this.EpgStyle().FontSizeTitle, this.EpgStyle().EpgBorderTopSize);

            //必要時間リストの修正。番組長の関係や、最低表示行数の適用で下に溢れた分を追加する。
            ViewUtil.AdjustTimeList(programList.Values, timeList, this.EpgStyle().MinHeight);
        }

        protected virtual ReserveViewItem AddReserveViewItem(ReserveData resInfo, ref ProgramViewItem refPgItem, bool SearchEvent = false)
        {
            //LimitedStart()の関係で判定出来ないものを除外
            if (timeList.Any() == false || resInfo.IsManual && resInfo.IsOver(timeList[0]) && resInfo.OnTimeBaseOnAir(timeList[0]) > 0) return null;

            //マージン適用前
            DateTime startTime = GetViewTime(LimitedStart(resInfo));
            DateTime chkStartTime = startTime.Date.AddHours(startTime.Hour);

            //離れた時間のプログラム予約など、番組表が無いので表示不可
            int index = timeList.BinarySearch(chkStartTime);
            if (index < 0) return null;

            var resItem = new ReserveViewItem(resInfo) { EpgSettingIndex = viewInfo.EpgSettingIndex, ViewMode = viewMode };
            (resInfo is ReserveDataEnd ? recinfoList : reserveList).Add(resItem);

            //予約情報から番組情報を特定し、枠表示位置を再設定する
            refPgItem = null;
            programList.TryGetValue(resInfo.CurrentPgUID(), out refPgItem);
            if (refPgItem == null && SearchEvent == true)
            {
                EpgEventInfo epgInfo;
                if (viewData.EventUIDList.TryGetValue(resInfo.CurrentPgUID(), out epgInfo))
                {
                    EpgEventInfo epgRefInfo = epgInfo.GetGroupMainEvent(viewData.EventUIDList);
                    if (epgRefInfo != null)
                    {
                        programList.TryGetValue(epgRefInfo.CurrentPgUID(), out refPgItem);
                    }
                }
            }

            //EPG予約の場合は番組の外側に予約枠が飛び出さないようなマージンを作成。
            double StartMargin = resInfo.IsEpgReserve == false ? resInfo.StartMarginResActual : Math.Min(0, resInfo.StartMarginResActual);
            double EndMargin = resInfo.IsEpgReserve == false ? resInfo.EndMarginResActual : Math.Min(0, resInfo.EndMarginResActual);

            //duationがマイナスになる場合は後で処理される
            double duration = resInfo.DurationSecond + StartMargin + EndMargin;

            if (resInfo.IsEpgReserve && resInfo.DurationSecond != 0 && refPgItem != null)
            {
                resItem.Height = Math.Max(refPgItem.Height * duration / resInfo.DurationSecond, ViewUtil.PanelMinimumHeight);
                resItem.TopPos = refPgItem.TopPos + Math.Min(refPgItem.Height - resItem.Height, refPgItem.Height * (-StartMargin) / resInfo.DurationSecond);
            }
            else
            {
                //週間番組表のプログラム録画の予約前マージン対応があるので、マージンの反映はGetViewTime()の後
                startTime = GetViewTime(resInfo.PgStartTime).AddSeconds(-StartMargin);
                resItem.TopPos = this.EpgStyle().MinHeight * (index * 60 + (startTime - chkStartTime).TotalMinutes);
                resItem.Height = Math.Max(duration * this.EpgStyle().MinHeight / 60 + Math.Min(resItem.TopPos, 0), ViewUtil.PanelMinimumHeight);
                resItem.TopPos = Math.Max(resItem.TopPos, 0);
            }
            return resItem;
        }

        protected IEnumerable<ReserveData> CombinedReserveList()
        {
            Func<IAutoAddTargetData, bool> InDic = r => viewData.EventUIDList.ContainsKey(r.CurrentPgUID()) || (ushort)r.CurrentPgUID() == 0xFFFF;
            return CommonManager.Instance.DB.RecFileInfo.Values.Where(r => InDic(r)).Select(r => r.ToReserveData())
                    .Concat(CommonManager.Instance.DB.ReserveList.Values.Where(r => InDic(r)));
        }

        /// <summary>表示スクロールイベント呼び出し</summary>
        protected virtual void epgProgramView_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            programView.view_ScrollChanged(programView.scrollViewer, timeView.scrollViewer, horizontalViewScroll);
        }

        /// <summary>右クリック表示メニューの作成</summary>
        protected void epgProgramView_RightClick(object sender, Point cursorPos)
        {
            clickPos = cursorPos;
            mc.SupportContextMenuLoading(cmdMenu, null);
        }
        /// <summary>右クリック表示メニューの作成(番組表エリア外)</summary>
        protected void button_erea_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            clickPos = Mouse.GetPosition(programView);//範囲外にしておく
            mc.SupportContextMenuLoading(cmdMenuView, null);
        }
        protected override object GetJumpTabItemNear()
        {
            double voffset = programView.scrollViewer.VerticalOffset;
            if (clickPos.X >= 0 && clickPos.Y < 0)
            {
                clickPos.X += programView.scrollViewer.HorizontalOffset;
                //サービス名付近から実行しているときはX位置を調整(サービス結合関連)
                clickPos.X -= clickPos.X % viewData.EpgStyle().ServiceWidth;
                clickPos.Y = Math.Max(0, clickPos.Y) + voffset;
            }
            ProgramViewItem hitItem = programView.GetProgramViewDataNear(clickPos, voffset, voffset + timeView.ActualHeight);
            return hitItem != null && hitItem.Data != null ? new SearchItem(hitItem.Data) : null;
        }

        public override int MoveToItem(ulong id, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            ProgramViewItem target_item;
            programList.TryGetValue(id, out target_item);
            if (dryrun == false) programView.ScrollToFindItem(target_item, style);
            return target_item == null ? -1 : 0;
        }

        public override object MoveNextItem(int direction, ulong id = 0, bool move = true, JumpItemStyle style = JumpItemStyle.MoveTo)
        {
            if (programList.Count == 0) return null;

            var list = programList.Values.OrderBy(item => (int)(item.LeftPos / this.EpgStyle().ServiceWidth) * 1e6 + item.TopPos + item.Width / this.EpgStyle().ServiceWidth / 100).ToList();
            int idx = list.FindIndex(item => item.Data.CurrentPgUID() == id);
            idx = ViewUtil.GetNextIdx(ItemIdx, idx, list.Count, direction);
            if (move == true) programView.ScrollToFindItem(list[idx], style);
            if (move == true) ItemIdx = idx;
            return list[idx] == null ? null : list[idx].Data;
        }

        public override int MoveToReserveItem(ReserveData target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            if (target == null) return -1;
            int idx = reserveList.FindIndex(item => item.Data.ReserveID == target.ReserveID);
            if (idx != -1 && dryrun == false) programView.ScrollToFindItem(reserveList[idx], style);
            if (dryrun == false) ItemIdx = idx;
            return idx;
        }
        public override int MoveToProgramItem(EpgEventInfo target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            target = target == null ? null : target.GetGroupMainEvent(viewData.EventUIDList);
            return MoveToItem(target == null ? 0 : target.CurrentPgUID(), style, dryrun);
        }
        public override int MoveToRecInfoItem(RecFileInfo target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            if (target == null) return -1;
            ulong id = target.CurrentPgUID();
            int idx = recinfoList.FindIndex(item => item.Data.CurrentPgUID() == id);
            if (idx != -1 && dryrun == false) programView.ScrollToFindItem(recinfoList[idx], style);
            if (dryrun == false) ItemIdx = idx;
            return idx;
        }

        protected int resIdx = -1;
        public override object MoveNextReserve(int direction, ulong id = 0, bool move = true, JumpItemStyle style = JumpItemStyle.MoveTo)
        {
            return ViewUtil.MoveNextReserve(ref resIdx, programView, reserveList, ref clickPos, id, direction, move, style);
        }

        protected int recIdx = -1;
        public override object MoveNextRecinfo(int direction, ulong id = 0, bool move = true, JumpItemStyle style = JumpItemStyle.MoveTo)
        {
            return (ViewUtil.MoveNextReserve(ref recIdx, programView, recinfoList, ref clickPos, id, direction, move, style) as ReserveDataEnd).GetRecinfoFromPgUID();
        }

        public void SyncScroll(EpgMainViewBase refView)
        {
            if (this == refView || refView == null) return;

            var time = refView.GetScrollTime();
            if (ViewPeriod.Contains(time))
            {
                MoveTime(time);
            }
            else
            {
                restoreState = new StateMainBase { scrollTime = time, isJumpDate = true };
                JumpDate(refView.ViewPeriod);
            }
        }
        /// <summary>表示位置を現在の時刻にスクロールする</summary>
        protected override void MoveNowTime()
        {
            DateTime current = RestoreState.scrollTime ?? GetViewTime(CommonUtil.EdcbNow);
            //再描画のときは再描画前の時間かその近くに飛ぶが、過去番組絡みで移動する時は同じ曜日の同時刻に飛ぶ
            if (RestoreState.isJumpDate == true && timeList.Any())
            {
                //timeListは歯抜けの場合もあるので、範囲だけ合わせて、突き合わせ表示はMoveTime()に任せる
                //最初から範囲内にいるときは、そのまま表示する。
                if (current < timeList.First() || timeList.Last() < current)
                {
                    int direction = current < timeList.First() ? 1 : -1;
                    Func<int, bool> chkTime = d => d > 0 ? current < timeList.First() : timeList.Last() < current;
                    while (chkTime(direction)) current += TimeSpan.FromDays(7 * direction);
                    if (chkTime(-direction))//表示期間が1週間無い場合
                    {
                        current -= TimeSpan.FromDays(7 * direction);
                        while (chkTime(direction)) current += TimeSpan.FromDays(direction);
                        if (chkTime(-direction)) current -= TimeSpan.FromDays(direction);//表示期間が1日無い場合
                    }
                }
            }
            MoveTime(current, RestoreState.scrollTime == null ? -120 : 0);
        }
        protected void MoveTime(DateTime time, int offset = 0)
        {
            int idx = timeList.BinarySearch(time.AddSeconds(1));
            double pos = Math.Max(0, ((idx < 0 ? ~idx : idx) - 1) * 60 * this.EpgStyle().MinHeight + offset);
            double back = programView.scrollViewer.VerticalOffset;
            programView.scrollViewer.ScrollToVerticalOffset(pos);
            if (pos == back) epgProgramView_ScrollChanged(null, null);//ScrollChangedEventArgsがCreate出来ない(RaiseEvent出来ない)ので
        }
        protected DateTime GetScrollTime()
        {
            if (timeList.Any() == false) return DateTime.MinValue;
            var idx = (int)(programView.scrollViewer.VerticalOffset / 60 / this.EpgStyle().MinHeight);
            return timeList[Math.Max(0, Math.Min(idx, timeList.Count - 1))];
        }
        protected override void SetJumpState() { restoreState = new StateMainBase { scrollTime = GetScrollTime(), isJumpDate = true }; }

        /// <summary>現在ライン表示</summary>
        protected virtual void ReDrawNowLine()
        {
            nowViewTimer.Stop();
            programView.nowLine.Visibility = Visibility.Hidden;
            var now = CommonUtil.EdcbNow;
            if (this.IsVisible == false || timeList.Any() == false || now >= ViewPeriod.End.AddDays(1)) return;

            //今は表示されない場合でも、そのうち表示されるかもしれない
            if (ViewPeriod.StartLoad <= now)
            {
                int idx = timeList.BinarySearch(GetViewTime(now).Date.AddHours(now.Hour));
                double posY = (idx < 0 ? ~idx * 60 : (idx * 60 + now.Minute)) * this.EpgStyle().MinHeight;

                programView.nowLine.X1 = 0;
                programView.nowLine.Y1 = posY;
                programView.nowLine.X2 = programView.epgViewPanel.Width;
                programView.nowLine.Y2 = posY;
                programView.nowLine.Visibility = Visibility.Visible;
            }
            nowViewTimer.Interval = TimeSpan.FromSeconds(60 - now.Second);
            nowViewTimer.Start();
        }
    }
}
