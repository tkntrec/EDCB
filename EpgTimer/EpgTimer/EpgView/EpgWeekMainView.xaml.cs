using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EpgTimer
{
    using EpgView;
    using ComboItem = KeyValuePair<ulong, string>;

    /// <summary>
    /// EpgWeekMainView.xaml の相互作用ロジック
    /// </summary>
    public partial class EpgWeekMainView : EpgMainViewBase
    {
        protected override bool viewCustNeedTimeOnly { get { return viewInfo.NeedTimeOnlyWeek; } }
        private List<DateTime> dayList = new List<DateTime>();

        protected class StateWeekMain : StateMainBase
        {
            public ulong? selectID = null;
            public StateWeekMain() { }
            public StateWeekMain(EpgWeekMainView view) : base(view) { selectID = view.GetSelectID(); }
        }
        public override EpgViewState GetViewState() { return new StateWeekMain(this); }
        protected new StateWeekMain RestoreState { get { return restoreState as StateWeekMain ?? new StateWeekMain(); } }

        public EpgWeekMainView()
        {
            InitializeComponent();
            SetControls(epgProgramView, timeView, weekDayView.scrollViewer);
            SetControlsPeriod(timeJumpView, timeMoveView, button_now);

            base.InitCommand();

            //時間関係の設定の続き
            nowViewTimer.Tick += (sender, e) => weekDayView.SetTodayMark();
            weekDayView.PreviewMouseLeftButtonDown += (sender, e) => button_erea_MouseLeftClick();

            //コマンド集の初期化の続き、ボタンの設定
            mBinds.SetCommandToButton(button_go_Main, EpgCmds.ViewChgMode, 0);
        }
        public override void SetViewData(EpgViewData data)
        {
            base.SetViewData(data);
            weekDayView.SetViewData(viewData);
        }

        //週間番組表での時刻表現用のメソッド。
        protected override DateTime GetViewTime(DateTime time)
        {
            return new DateTime(2001, 1, time.Hour >= viewInfo.StartTimeWeek ? 1 : 2).Add(time.TimeOfDay);
        }
        private DateTime GetViewDay(DateTime time)
        {
            return time.AddHours(-viewInfo.StartTimeWeek).Date;
        }

        /// <summary>サービスロゴの再描画</summary>
        protected override void ReloadServiceLogo()
        {
            int idx = comboBox_service.SelectedIndex;
            if (idx < 0) return;
            if(Settings.Instance.ShowLogo)
            {
                image_Logo.Source = serviceListOrderAdjust[idx].Logo;
                image_Logo.Width = 64;
            }
            else
            {
                image_Logo.Source = null;
                image_Logo.Width = 0; //隙間調整もあるので非表示にはしない。
            }
        }
        /// <summary>予約情報の再描画</summary>
        protected override void ReloadReserveViewItem()
        {
            try
            {
                reserveList.Clear();
                recinfoList.Clear();

                ulong selectID = GetSelectID(true);
                foreach (ReserveData info in CombinedReserveList())
                {
                    if (selectID == info.Create64Key())
                    {
                        //離れたプログラム予約など範囲外は除外。
                        int dayPos = dayList.BinarySearch(GetViewDay(info.StartTime));
                        if (dayPos < 0) continue;

                        ProgramViewItem dummy = null;
                        ReserveViewItem resItem = AddReserveViewItem(info, ref dummy);
                        if (resItem != null)
                        {
                            //横位置の設定
                            resItem.Width = this.EpgStyle().ServiceWidth;
                            resItem.LeftPos = resItem.Width * dayPos;
                        }
                    }
                }

                epgProgramView.SetReserveList(dataItemList);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        /// <summary>番組情報の再描画</summary>
        protected override void ReloadProgramViewItem()
        {
            serviceChanging = true;
            try
            {
                //表示していたサービスがあれば維持
                comboBox_service.ItemsSource = serviceListOrderAdjust.Select(info => new ComboItem(info.Key, info.service_name));
                comboBox_service.SelectedValue = RestoreState.selectID ?? GetSelectID();
                if (comboBox_service.SelectedIndex < 0) comboBox_service.SelectedIndex = 0;

                UpdateProgramView();
                MoveNowTime();
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            serviceChanging = false;
        }
        private void UpdateProgramView()
        {
            try
            {
                timeView.ClearInfo();
                weekDayView.ClearInfo();
                epgProgramView.ClearInfo();
                timeList.Clear();
                programList.Clear();
                ReDrawNowLine();
                dayList.Clear();

                ulong selectID = GetSelectID(true);
                if (selectID == 0) return;

                //リストの作成
                int idx = serviceEventList.FindIndex(item => item.serviceInfo.Key == selectID);
                if (idx < 0) return;

                serviceEventList[idx].eventList.ForEach(eventInfo =>
                {
                    //無いはずだが、ToDictionary()にせず、一応保険。
                    programList[eventInfo.CurrentPgUID()] = new ProgramViewItem(eventInfo) { EpgSettingIndex = viewInfo.EpgSettingIndex, Filtered = viewData.EventFilteredHash.Contains(eventInfo.CurrentPgUID()) };
                });

                //日付リスト構築
                dayList.AddRange(programList.Values.Select(d => GetViewDay(d.Data.start_time)).Distinct().OrderBy(day => day));

                //横位置の設定
                foreach (ProgramViewItem item in programList.Values)
                {
                    item.Width = this.EpgStyle().ServiceWidth;
                    item.LeftPos = item.Width * dayList.BinarySearch(GetViewDay(item.Data.start_time));
                }

                //縦位置の設定
                if (viewCustNeedTimeOnly == false)
                {
                    ViewUtil.AddTimeList(timeList, new DateTime(2001, 1, 1, viewInfo.StartTimeWeek, 0, 0), 86400);
                }
                SetProgramViewItemVertical();

                epgProgramView.SetProgramList(programList.Values.ToList(),
                    dayList.Count * this.EpgStyle().ServiceWidth,
                    timeList.Count * 60 * this.EpgStyle().MinHeight);

                timeView.SetTime(timeList, true);
                weekDayView.SetDay(dayList);

                ReDrawNowLine();
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        bool serviceChanging = false;
        private void comboBox_service_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (serviceChanging == true) return;
            ReloadServiceLogo();
            UpdateProgramView();
            ReloadReserveViewItem();
            UpdateStatus();
        }
        private ulong GetSelectID(bool alternativeSelect = false)
        {
            var idx = comboBox_service.SelectedIndex;
            if (idx < 0)
            {
                if (alternativeSelect == false || comboBox_service.Items.Count == 0) return 0;
                idx = 0;
            }
            return (ulong)comboBox_service.SelectedValue;
        }

        //実際には切り替えないと分からない
        public override int MoveToItem(ulong id, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            ulong key = CommonManager.Reverse64Key(id);
            if (dryrun == true) return viewData.HasKey(key) ? 1 : -1;
            if (key != 0) ChangeViewService(key);
            return base.MoveToItem(id, style, dryrun);
        }
        public override int MoveToReserveItem(ReserveData target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            if (dryrun == true) return target == null ? -1 : viewData.HasKey(target.Create64Key()) ? 1 : -1;
            if (target != null) ChangeViewService(target.Create64Key());
            return base.MoveToReserveItem(target, style);
        }
        public override int MoveToProgramItem(EpgEventInfo target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            if (dryrun == true) return target == null ? -1 : viewData.HasKey(target.Create64Key()) ? 1 : -1;
            if (target != null) ChangeViewService(target.Create64Key());
            return base.MoveToProgramItem(target, style);
        }
        public override int MoveToRecInfoItem(RecFileInfo target, JumpItemStyle style = JumpItemStyle.MoveTo, bool dryrun = false)
        {
            if (dryrun == true) return target == null ? -1 : viewData.HasKey(target.Create64Key()) ? 1 : -1;
            if (target != null) ChangeViewService(target.Create64Key());
            return base.MoveToRecInfoItem(target, style);
        }
        protected void ChangeViewService(ulong id)
        {
            var target = comboBox_service.Items.OfType<ComboItem>().FirstOrDefault(item => item.Key == id);
            if (target.Key != default(ulong)) comboBox_service.SelectedItem = target;
        }
    }
}
