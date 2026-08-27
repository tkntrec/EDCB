using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace EpgTimer
{
    public class RecInfoItem : DataListItemBase
    {
        public RecInfoItem() { }
        public RecInfoItem(RecFileInfo info, Dictionary<char, List<KeyValuePair<string, string>>> replaceRecFilePathDictionary = null)
        {
            RecInfo = info;
            ReplaceRecFilePathDictionary = replaceRecFilePathDictionary ?? new Dictionary<char, List<KeyValuePair<string, string>>>();
        }

        public RecFileInfo RecInfo { get; private set; }
        public Dictionary<char, List<KeyValuePair<string, string>>> ReplaceRecFilePathDictionary { get; private set; }
        public override ulong KeyID { get { return RecInfo == null ? 0 : RecInfo.ID; } }
        public override object DataObj { get { return RecInfo; } }

        public bool IsProtect
        {
            set { EpgCmds.ChgOnOffCheck.Execute(this, null); }
            get { return RecInfo.ProtectFlag != 0; }
        }
        public string EventName
        {
            get { return RecInfo.Title; }
        }
        public string EventNameValue
        {
            get { return Settings.Instance.TrimSortTitle == true ? MenuUtil.TrimKeyword(EventName) : EventName; }
        }
        public string ServiceName
        {
            get { return RecInfo.ServiceName; }
        }
        public string Duration
        {
            get { return CommonManager.ConvertDurationText(RecInfo.DurationSecond, Settings.Instance.RecInfoNoDurSecond); }
        }
        public uint DurationValue
        {
            get { return RecInfo.DurationSecond; }
        }
        public string StartTime
        {
            get { return CommonManager.ConvertTimeText(RecInfo.StartTime, RecInfo.DurationSecond, Settings.Instance.RecInfoNoYear, Settings.Instance.RecInfoNoSecond, isNoEnd: Settings.Instance.RecInfoNoEnd); }
        }
        public long StartTimeValue
        {
            get { return RecInfo.StartTime.Ticks; }
        }
        public long Drops
        {
            get { return RecInfo.Drops; }
        }
        public long DropsSerious
        {
            get { return RecInfo.DropsCritical; }
        }
        public long Scrambles
        {
            get { return RecInfo.Scrambles; }
        }
        public long ScramblesSerious
        {
            get { return RecInfo.ScramblesCritical; }
        }
        public string Result
        {
            get { return RecInfo.Comment; }
        }
        public string NetworkName
        {
            get { return CommonManager.ConvertNetworkNameText(RecInfo.OriginalNetworkID); }
        }
        public string RecFilePath
        {
            get { return RecInfo.RecFilePath; }
        }
        public string ReplacedRecFilePath
        {
            get { return CommonManager.ReplaceText(RecInfo.RecFilePath, ReplaceRecFilePathDictionary); }
        }
        public override Brush BackColor
        {
            get { return NowJumpingTable != 0 ? base.BackColor : BackColorBrush(); }
        }
        public override Brush BackColor2
        {
            get { return BackColorBrush(true); }
        }
        private Brush BackColorBrush(bool defTransParent = false)
        {
            //通常表示
            long drops = Settings.Instance.RecinfoErrCriticalDrops == false ? RecInfo.Drops : RecInfo.DropsCritical;
            long scrambles = Settings.Instance.RecinfoErrCriticalDrops == false ? RecInfo.Scrambles : RecInfo.ScramblesCritical;

            int idx = defTransParent ? -1 : 0;
            if (Settings.Instance.RecInfoDropErrIgnore >= 0 && drops > Settings.Instance.RecInfoDropErrIgnore
                || RecInfo.RecStatusBasic == RecEndStatusBasic.ERR)
            {
                idx = 1;
            }
            else if (Settings.Instance.RecInfoDropWrnIgnore >= 0 && drops > Settings.Instance.RecInfoDropWrnIgnore
                || Settings.Instance.RecInfoScrambleIgnore >= 0 && scrambles > Settings.Instance.RecInfoScrambleIgnore
                || RecInfo.RecStatusBasic == RecEndStatusBasic.WARN)
            {
                idx = 2;
            }
            return idx < 0 ? null : Settings.BrushCache.RecEndBackColor[idx];
        }
        public override string ConvertInfoText(object param = null)
        {
            var mode = param is int ? (int)param : Settings.Instance.RecInfoToolTipMode;
            if (mode == 1) return RecInfo.ProgramInfo;

            string view = CommonManager.ConvertTimeText(RecInfo.StartTime, RecInfo.DurationSecond, false, false, false) + "\r\n";
            view += ServiceName + "(" + NetworkName + ")" + "\r\n";
            view += EventName + "\r\n\r\n";

            view += "結果 : " + Result + "\r\n";
            view += "録画ファイル : " + RecFilePath + "\r\n";
            view += ConvertDropText() + "\r\n";
            view += ConvertScrambleText() + "\r\n\r\n";

            view += CommonManager.Convert64PGKeyString(RecInfo.Create64PgKey()) + "\r\n\r\n";

            view += "録画情報ID : " + string.Format("{0} (0x{0:X})", DisplayID);
            return view;
        }
        public string DropInfoText
        {
            get { return ConvertDropText("D:") + " " + ConvertScrambleText("S:"); }
        }
        private string ConvertDropText(string title = "Drop : ")
        {
            if (Settings.Instance.RecinfoErrCriticalDrops == true)
            {
                return "*" + title + RecInfo.DropsCritical.ToString();
            }
            else
            {
                return title + RecInfo.Drops.ToString();
            }
        }
        private string ConvertScrambleText(string title = "Scramble : ")
        {
            if (Settings.Instance.RecinfoErrCriticalDrops == true)
            {
                return "*" + title + RecInfo.ScramblesCritical.ToString();
            }
            else
            {
                return title + RecInfo.Scrambles.ToString();
            }
        }
    }

    public static class RecInfoItemEx
    {
        public static List<RecFileInfo> RecInfoList(this ICollection<RecInfoItem> itemlist)
        {
            return itemlist.Where(item => item != null).Select(item => item.RecInfo).ToList();
        }
    }
}
