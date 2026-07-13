﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;

namespace EpgTimer
{
    class IniFileHandler
    {
        public static bool ReadOnly { get; set; }

        /// <summary>書き込み値はbool型のみtrue:"1",false:"0"、他はvalue.ToString()。ただし空のstringはnull。</summary>
        public static bool WritePrivateProfileString(string appName, string keyName, object val, string fileName)
        {
            if (ReadOnly == true) return false;
            string valstr = val == null ? null : !(val is bool) ? val.ToString() : (bool)val ? "1" : "0";
            return NativeMethods.WritePrivateProfileString(appName, keyName, valstr == "" ? null : valstr, fileName);
        }
        /// <summary>デフォルト値の時キーを削除する。書き込み値はbool型のみtrue:"1",false:"0"、他はvalue.ToString()</summary>
        public static bool WritePrivateProfileString<T>(string appName, string keyName, T val, T defval, string fileName)
        {
            //引数をTからobjectにすると、valueがdoubleなどのときEqualsで失敗する
            return WritePrivateProfileString(appName, keyName, object.Equals(val, defval) == true ? null : (object)val, fileName);
        }

        public static int GetPrivateProfileInt(string appName, string keyName, int nDefault, string fileName)
        {
            return NativeMethods.GetPrivateProfileInt(appName, keyName, nDefault, fileName);
        }
        public static string GetPrivateProfileString(string appName, string keyName, string def, string fileName)
        {
            StringBuilder buff = null;
            for (uint n = 512; n <= 1024 * 1024; n *= 2)
            {
                //セクション名取得などのNUL文字分割された結果は先頭要素のみ格納される
                buff = new StringBuilder((int)n);
                if (NativeMethods.GetPrivateProfileString(appName, keyName, def, buff, n, fileName) < n - 2)
                {
                    break;
                }
            }
            return buff.ToString();
        }
        /// <summary>"0"をfalse、それ以外をtrueで読み込む。</summary>
        public static bool GetPrivateProfileBool(string appName, string keyName, bool defval, string fileName)
        {
            return GetPrivateProfileString(appName, keyName, defval == false ? "0" : "1", fileName) != "0";
        }
        public static double GetPrivateProfileDouble(string appName, string keyName, double defval, string fileName)
        {
            string s = GetPrivateProfileString(appName, keyName, defval.ToString(), fileName);
            double.TryParse(s, out defval);
            return defval;
        }
        public static string GetPrivateProfileFolder(string appName, string keyName, string fileName)
        {
            var path = GetPrivateProfileString(appName ?? "", keyName ?? "", "", fileName ?? "");
            return SettingPath.CheckFolder(path);
        }

        /// <summary>INIファイルから指定セクションのキー一覧を取得する。lpAppNameにnullを指定すると全セクションの一覧を取得する。</summary>
        public static string[] GetPrivateProfileKeys(string appName, string fileName)
        {
            byte[] buff = null;
            uint resultSize = 0;
            for (uint n = 1024; n <= 1024 * 1024; n *= 2)
            {
                buff = new byte[n];
                resultSize = NativeMethods.GetPrivateProfileStringByByteArray(appName, null, null, buff, n, fileName);
                if (resultSize < n - 2)
                {
                    break;
                }
            }
            return Encoding.GetEncoding(932).GetString(buff, 0, (int)resultSize).TrimEnd('\0').Split('\0');
        }

        /// <summary>INIファイルから連番のキー/セクションを削除する。lpAppNameにnullを指定すると連番セクションを削除する。</summary>
        public static void DeletePrivateProfileNumberKeys(string appName, string fileName, string BaseFront = "", string BaseRear = "", bool deleteBaseName = false)
        {
            if (ReadOnly == true) return;
            string numExp = string.IsNullOrEmpty(BaseFront + BaseRear) == false && deleteBaseName == true ? "*" : "+";
            foreach (string key in GetPrivateProfileKeys(appName, fileName))
            {
                if (Regex.Match(key, "^" + BaseFront + "\\d" + numExp + BaseRear + "$").Success == true)
                {
                    if (appName != null)
                    {
                        NativeMethods.WritePrivateProfileString(appName, key, null, fileName);
                    }
                    else
                    {
                        NativeMethods.WritePrivateProfileString(key, null, null, fileName);
                    }
                }
            }
        }

        public static ErrCode UpdateSrvProfileIni(List<string> iniList = null)
        {
            var err = ErrCode.CMD_SUCCESS;
            if (CommonManager.Instance.NWMode == true)
            {
                if (CommonManager.Instance.IsConnected == false) return err;
                err = ReloadSettingFilesNW(iniList);
            }

            ChSet5.Clear();
            Settings.Instance.LoadIniOptions();
            if (Settings.Instance.WakeUpHdd == false) CommonManager.WakeUpHDDLogClear();
            return err;
        }

        public static ErrCode ReloadSettingFilesNW(List<string> iniList = null)
        {
            if (iniList == null)
            {
                iniList = new List<string> {
                    "EpgTimerSrv.ini"
                    ,"Common.ini"
                    ,"EpgDataCap_Bon.ini"
                    ,"BonCtrl.ini"
                    ,"ViewApp.ini" 
                    ,"Bitrate.ini"
                    ,"ChSet5.txt"
                };
            }

            var datalist = new List<FileData>();
            ErrCode err = CommonManager.CreateSrvCtrl().SendFileCopy2(iniList, ref datalist);
            try
            {
                if (err == ErrCode.CMD_SUCCESS)
                {
                    Directory.CreateDirectory(SettingPath.SettingFolderPath);
                    foreach (var data in datalist)
                    {
                        try
                        {
                            string path = Path.Combine(SettingPath.SettingFolderPath, data.Name);
                            if (data.Data == null)
                            {
                                File.Delete(path);
                                continue;
                            }
                            using (var w = new BinaryWriter(File.Create(path)))
                            {
                                w.Write(data.Data);
                            }
                        }
                        catch { err = ErrCode.CMD_ERR; }
                    }
                }
            }
            catch { err = ErrCode.CMD_ERR; }
            return err;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            public static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault,
                                                              StringBuilder lpReturnedString, uint nSize, string lpFileName);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            public static extern int GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            public static extern bool WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName);

            [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileStringA")]
            public static extern uint GetPrivateProfileStringByByteArray(string lpAppName, string lpKeyName, string lpDefault, byte[] lpReturnedString, uint nSize, string lpFileName);
        }
    }

    class SettingPath
    {
        //Path.Combine()はドライブ区切り':'の扱いがアヤシイのでここでは使用しないことにする。
        //他のEpgTimerコード中で使っているところはModelePathに対してなので大丈夫。(ModelePathは'C:'のようなパスを返さない)
        public static string CheckFolder(string path)
        {
            path = (path ?? "").Trim(' ');
            if (path != "\\") path = path.TrimEnd('\\');
            return path + (path.EndsWith(Path.VolumeSeparatorChar.ToString(), StringComparison.Ordinal) == true ? "\\" : "");
        }
        public static string CheckTSExtension(string ext)
        {
            //5文字以下の英数字拡張子に限る
            if (ext.Length < 2 || ext.Length > 6 || ext[0] != '.'
                || ext.Substring(1).Any(c => !('0' <= c && c <= '9' || 'A' <= c && c <= 'Z' || 'a' <= c && c <= 'z')))
            { return ".ts"; }
            return ext;
        }
        private static string IniPath
        {
            get { return (CommonManager.Instance.NWMode == false ? ModulePath : SettingFolderPath); }
        }
        public static string BonCtrlIniPath
        {
            get { return IniPath.TrimEnd('\\') + "\\BonCtrl.ini"; }
        }
        public static string CommonIniPath
        {
            get { return IniPath.TrimEnd('\\') + "\\Common.ini"; }
        }
        public static string BitrateIniPath
        {
            get { return IniPath.TrimEnd('\\') + "\\Bitrate.ini"; }
        }
        public static string TimerSrvIniPath
        {
            get { return IniPath.TrimEnd('\\') + "\\EpgTimerSrv.ini"; }
        }
        public static string ViewAppIniPath
        {
            get { return IniPath.TrimEnd('\\') + "\\ViewApp.ini"; }
        }
        public static string DefHttpPublicPath
        {
            get { return GetModulePath(true).TrimEnd('\\') + "\\HttpPublic"; }
        }
        public static string DefEdcbExePath
        {
            get { return GetModulePath(true).TrimEnd('\\') + "\\EpgDataCap_Bon.exe"; }
        }
        public static string EdcbExePath
        {
            get { return IniFileHandler.GetPrivateProfileString("SET", "RecExePath", DefEdcbExePath, CommonIniPath); }
            set { IniFileHandler.WritePrivateProfileString("SET", "RecExePath", value == "" || value.Equals(DefEdcbExePath, StringComparison.OrdinalIgnoreCase) == true ? null : value, SettingPath.CommonIniPath); }
        }
        public static string EdcbIniPath
        {
            get
            {
                if (CommonManager.Instance.NWMode == false)
                {
                    return EdcbExePath.TrimEnd("exe".ToArray()) + "ini";
                }
                else
                {
                    return IniPath.TrimEnd('\\') + "\\" + Path.GetFileNameWithoutExtension(EdcbExePath) + ".ini";
                }
            }
        }
        private static string GetDefSettingFolderPath(bool isSrv = false)
        {
            return GetModulePath(isSrv).TrimEnd('\\') + "\\Setting" + (isSrv == true || CommonManager.Instance.NWMode == false ? "" : "\\EpgTimerNW");
        }
        public static string GetSettingFolderPath(bool isSrv = false)
        {
            string path;
            if (isSrv == true || CommonManager.Instance.NWMode == false)
            {
                path = IniFileHandler.GetPrivateProfileFolder("SET", "DataSavePath", CommonIniPath);
            }
            else
            {
                path = CheckFolder(Settings.Instance.SettingFolderPathNW);
            }
            path = string.IsNullOrEmpty(path) == true ? GetDefSettingFolderPath(isSrv) : path;
            return (Path.IsPathRooted(path) ? "" : GetModulePath(isSrv).TrimEnd('\\') + "\\") + path;
        }
        public static string SettingFolderPath
        {
            get { return GetSettingFolderPath(); }
            set
            {
                string path = CheckFolder(value);
                bool isDefaultPath = path == "" || path.TrimEnd('\\').Equals(GetDefSettingFolderPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) == true;
                if (CommonManager.Instance.NWMode == false)
                {
                    IniFileHandler.WritePrivateProfileString("SET", "DataSavePath", isDefaultPath == true ? null : path, SettingPath.CommonIniPath);
                }
                else
                {
                    Settings.Instance.SettingFolderPathNW = isDefaultPath == true ? "" : path;
                }
            }
        }
        private static string GetModulePath(bool isSrv = false)
        {
            if (isSrv == true && CommonManager.Instance.NWMode == true)
            {
                return IniFileHandler.GetPrivateProfileString("SET", "ModulePath", "(サーバ側アプリフォルダ)", CommonIniPath);
            }
            else
            {
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            }
        }
        public static string ModulePath
        {
            get { return GetModulePath(); }
        }
        public static string ModuleName
        {
            get { return Path.GetFileName(Assembly.GetEntryAssembly().Location); }
        }
    }

    public class EpgSetting : IDeepCloneObj
    {
        public int ID { get; set; }
        private string name = "";
        public string Name
        {
            get { return string.IsNullOrWhiteSpace(name) == false ? name : ("パターン" + (1 + ID).ToString()); }
            set { name = value; }
        }

        public string FontNameTitle { get; set; }
        public double FontSizeTitle { get; set; }
        public bool FontBoldTitle { get; set; }
        public string FontName { get; set; }
        public double FontSize { get; set; }
        public string TitleColor1 { get; set; }
        public string TitleColor2 { get; set; }
        public uint TitleCustColor1 { get; set; }
        public uint TitleCustColor2 { get; set; }
        public bool ReserveRectShowMarker { get; set; }
        public double ScrollSize { get; set; }
        public bool MouseScrollAuto { get; set; }
        public double HorizontalScrollSize { get; set; }
        public bool MouseHorizontalScrollAuto { get; set; }
        public double ServiceWidth { get; set; }
        public bool EpgServiceNameTooltip { get; set; }
        public double MinHeight { get; set; }
        public double DragScroll { get; set; }
        public double MinimumHeight { get; set; }
        public double EpgBorderLeftSize { get; set; }
        public double EpgBorderTopSize { get; set; }
        public bool EpgPopup { get; set; }
        public int EpgPopupMode { get; set; }
        public double EpgPopupWidth { get; set; }
        public bool EpgToolTip { get; set; }
        public bool EpgToolTipNoViewOnly { get; set; }
        public int EpgToolTipViewWait { get; set; }
        public bool EpgExtInfoTable { get; set; }
        public bool EpgExtInfoPopup { get; set; }
        public bool EpgExtInfoTooltip { get; set; }
        public bool EpgInfoSingleClick { get; set; }
        public int EpgInfoOpenMode { get; set; }
        public bool EpgTitleIndent { get; set; }
        public bool DisplayNotifyEpgChange { get; set; }
        public bool EpgGradation { get; set; }
        public bool EpgGradationHeader { get; set; }
        public int EpgArcDefaultDays { get; set; }
        public double EpgArcTabWeeks { get; set; }
        public bool EpgArcStartSunday { get; set; }
        public string EpgReplacePatternTitle { get; set; }
        public string EpgReplacePattern { get; set; }
        public bool EpgReplacePatternTitleDef { get; set; }
        public bool EpgReplacePatternDef { get; set; }
        public bool ApplyReplacePatternTuner { get; set; }
        public bool ShareEpgReplacePatternTitle { get; set; }
        public bool EpgChangeBorderOnRecWeekOnly { get; set; }
        public uint EpgChangeBorderMode { get; set; }
        public List<string> ContentColorList { get; set; }
        public List<uint> ContentCustColorList { get; set; }
        public List<string> EpgResColorList { get; set; }
        public List<uint> EpgResCustColorList { get; set; }
        public List<string> EpgEtcColors { get; set; }
        public List<uint> EpgEtcCustColors { get; set; }
        public int ReserveRectFillOpacity { get; set; }
        public bool ReserveRectFillWithShadow { get; set; }

        public static EpgSetting DefSetting { get { var ret = new EpgSetting(); ret.Reset(); return ret; } }

        public EpgSetting() { ID = 0; Init(); }
        public void Reset() { Init(); Settings.SetColorSettingEpg(this); }
        public void Init()
        {
            FontNameTitle = Settings.DefaultFontName;
            FontSizeTitle = 12;
            FontBoldTitle = true;
            FontName = Settings.DefaultFontName;
            FontSize = 12;
            TitleColor1 = "Black";
            TitleColor2 = "Black";
            TitleCustColor1 = 0xFFFFFFFF;
            TitleCustColor2 = 0xFFFFFFFF;
            ReserveRectShowMarker = true;
            ScrollSize = 240;
            MouseScrollAuto = false;
            HorizontalScrollSize = 150;
            MouseHorizontalScrollAuto = false;
            ServiceWidth = 150;
            EpgServiceNameTooltip = false;
            MinHeight = 2;
            DragScroll = 1.5;
            MinimumHeight = 0.6;
            EpgBorderLeftSize = 1;
            EpgBorderTopSize = 0.5;
            EpgPopup = true;
            EpgPopupMode = 0;
            EpgPopupWidth = 1;
            EpgToolTip = false;
            EpgToolTipNoViewOnly = true;
            EpgToolTipViewWait = 1500;
            EpgExtInfoTable = false;
            EpgExtInfoPopup = false;
            EpgExtInfoTooltip = true;
            EpgInfoSingleClick = false;
            EpgInfoOpenMode = 0;
            EpgTitleIndent = true;
            DisplayNotifyEpgChange = false;
            EpgGradation = true;
            EpgGradationHeader = true;
            EpgArcDefaultDays = 1;
            EpgArcTabWeeks = 1;
            EpgArcStartSunday = false;
            EpgReplacePatternTitle = "";
            EpgReplacePattern = "";
            EpgReplacePatternTitleDef = false;
            EpgReplacePatternDef = false;
            ApplyReplacePatternTuner = false;
            ShareEpgReplacePatternTitle = false;
            EpgChangeBorderOnRecWeekOnly = true;
            EpgChangeBorderMode = 1;
            ContentColorList = new List<string>();
            ContentCustColorList = new List<uint>();
            EpgResColorList = new List<string>();
            EpgResCustColorList = new List<uint>();
            EpgEtcColors = new List<string>();
            EpgEtcCustColors = new List<uint>();
            ReserveRectFillOpacity = 0;
            ReserveRectFillWithShadow = true;
        }
        public object DeepCloneObj()
        {
            var other = (EpgSetting)MemberwiseClone();
            other.ContentColorList = ContentColorList.ToList();
            other.ContentCustColorList = ContentCustColorList.ToList();
            other.EpgResColorList = EpgResColorList.ToList();
            other.EpgResCustColorList = EpgResCustColorList.ToList();
            other.EpgEtcColors = EpgEtcColors.ToList();
            other.EpgEtcCustColors = EpgEtcCustColors.ToList();
            return other;
        }
    }
    public class Settings
    {
        //ver履歴 20200723、20200721、20200528、20200411、20200320、20190520、20190321、20190217、20170717、20170512
        private int verSaved = 0;
        public int SettingFileVer { get { return 20200723; } set { verSaved = value; } }

        public bool UseCustomEpgView { get; set; }
        public List<CustomEpgTabInfo> CustomEpgTabList { get; set; }
        public IndexSafeList<EpgSetting> EpgSettingList { get; set; }
        public bool SynchronizeEpgScroll { get; set; }
        public bool ToggleEpgModeOnHeaderLeftClick { get; set; }
        public bool NoToolTip { get; set; }
        public double ToolTipWidth { get; set; }
        public bool NoBallonTips { get; set; }
        public bool BalloonTipRealtime { get; set; }
        public int ForceHideBalloonTipSec { get; set; }
        public bool PlayDClick { get; set; }
        public bool ConfirmDelRecInfoFileDelete { get; set; }
        public bool ShowEpgCapServiceOnly { get; set; }
        public bool SortServiceList { get; set; }
        public bool ExitAfterProcessingArgs { get; set; }
        public bool RecinfoErrCriticalDrops { get; set; }
        public int ReserveToolTipMode { get; set; }
        public int ReserveEpgInfoOpenMode { get; set; }
        public string TunerFontNameService { get; set; }
        public double TunerFontSizeService { get; set; }
        public bool TunerFontBoldService { get; set; }
        public string TunerFontName { get; set; }
        public double TunerFontSize { get; set; }
        public List<string> TunerServiceColors { get; set; }
        public List<uint> TunerServiceCustColors { get; set; }
        public double TunerMinHeight { get; set; }
        public double TunerMinimumLine { get; set; }
        public double TunerDragScroll { get; set; }
        public double TunerScrollSize { get; set; }
        public bool TunerMouseScrollAuto { get; set; }
        public double TunerHorizontalScrollSize { get; set; }
        public bool TunerMouseHorizontalScrollAuto { get; set; }
        public double TunerWidth { get; set; }
        public bool TunerNameTooltip { get; set; }
        public bool TunerServiceNoWrap { get; set; }
        public double TunerBorderLeftSize { get; set; }
        public double TunerBorderTopSize { get; set; }
        public bool TunerTitleIndent { get; set; }
        public bool TunerToolTip { get; set; }
        public int TunerToolTipViewWait { get; set; }
        public bool TunerPopup { get; set; }
        public int TunerPopupMode { get; set; }
        public bool TunerPopupRecinfo { get; set; }
        public double TunerPopupWidth { get; set; }
        public uint TunerChangeBorderMode { get; set; }
        public bool TunerInfoSingleClick { get; set; }
        public bool TunerColorModeUse { get; set; }
        public bool TunerDisplayOffReserve { get; set; }
        public int TunerToolTipMode { get; set; }
        public int TunerEpgInfoOpenMode { get; set; }
        public string FontReplacePatternEdit { get; set; }
        public double FontSizeReplacePattern { get; set; }
        public bool FontBoldReplacePattern { get; set; }
        public bool ReplacePatternEditFontShare { get; set; }
        public bool EpgNameTabEnabled { get; set; }
        public bool EpgViewModeTabEnabled { get; set; }
        public bool EpgTabMoveCheckEnabled { get; set; }
        public SerializableDictionary<ulong, byte> RemoconIDList { get; set; }
        public string ResColumnHead { get; set; }
        public ListSortDirection ResSortDirection { get; set; }
        public WindowSettingData WndSettings { get; set; }
        public double SearchWndTabsHeight { get; set; }
        public double SearchWndJunreHeight { get; set; }
        public bool CloseMin { get; set; }
        public bool WakeMin { get; set; }
        public bool WakeMinTraySilent { get; set; }
        public bool ViewButtonShowAsTab { get; set; }
        public List<string> ViewButtonList { get; set; }
        public List<string> TaskMenuList { get; set; }
        public string Cust1BtnName { get; set; }
        public string Cust1BtnCmd { get; set; }
        public string Cust1BtnCmdOpt { get; set; }
        public string Cust2BtnName { get; set; }
        public string Cust2BtnCmd { get; set; }
        public string Cust2BtnCmdOpt { get; set; }
        public string Cust3BtnName { get; set; }
        public string Cust3BtnCmd { get; set; }
        public string Cust3BtnCmdOpt { get; set; }
        public List<string> AndKeyList { get; set; }
        public List<string> NotKeyList { get; set; }
        public EpgSearchKeyInfo DefSearchKey { get; set; }
        public bool UseLastSearchKey { get; set; }
        public List<SearchPresetItem> SearchPresetList { get; set; }
        public bool SetWithoutSearchKeyWord { get; set; }
        public bool SetWithoutRecTag { get; set; }
        public int RecInfoToolTipMode { get; set; }
        public string RecInfoColumnHead { get; set; }
        public ListSortDirection RecInfoSortDirection { get; set; }
        public long RecInfoDropErrIgnore { get; set; }
        public long RecInfoDropWrnIgnore { get; set; }
        public long RecInfoScrambleIgnore { get; set; }
        public List<string> RecInfoDropExcept { get; set; }
        public static readonly List<string> RecInfoDropExceptDefault = new List<string> { "EIT", "NIT", "CAT", "SDT/BAT", "SDTT", "TDT/TOT", "ECM", "EMM" };
        public bool RecInfoNoYear { get; set; }
        public bool RecInfoNoSecond { get; set; }
        public bool RecInfoNoDurSecond { get; set; }
        public bool RecInfoNoEnd { get; set; }
        public bool ResInfoNoYear { get; set; }
        public bool ResInfoNoSecond { get; set; }
        public bool ResInfoNoDurSecond { get; set; }
        public bool ResInfoNoEnd { get; set; }
        public string TvTestExe { get; set; }
        public string TvTestCmd { get; set; }
        public bool NwTvMode { get; set; }
        public bool NwTvModeUDP { get; set; }
        public bool NwTvModeTCP { get; set; }
        public bool NwTvModePipe { get; set; }
        public bool FilePlay { get; set; }
        public string FilePlayExe { get; set; }
        public string FilePlayCmd { get; set; }
        public bool FilePlayOnAirWithExe { get; set; }
        public bool FilePathReplaceRecFile { get; set; }
        public string FilePathReplacePattern { get; set; }
        public string FilePathReplaceTestPath { get; set; }
        public bool OpenFolderWithFileDialog { get; set; }
        public List<IEPGStationInfo> IEpgStationList { get; set; }
        public MenuSettingData MenuSet { get; set; }
        public string NWServerIP { get; set; }
        public uint NWServerPort { get; set; }
        public uint NWWaitPort { get; set; }
        public string NWMacAdd { get; set; }
        public List<NWPresetItem> NWPreset { get; set; }
        public bool WakeReconnectNW { get; set; }
        public bool WakeCheckService { get; set; }
        public bool WakeCheckServiceDialog { get; set; }
        public bool WoLWaitRecconect { get; set; }
        public double WoLWaitSecond { get; set; }
        public bool SuspendCloseNW { get; set; }
        public bool NgAutoEpgLoadNW { get; set; }
        public bool NoReserveEventList { get; set; }
        public bool NoEpgAutoAddAppend { get; set; }
        public bool PrebuildEpg { get; set; }
        public bool PrebuildEpgAll { get; set; }
        public bool ChkSrvRegistTCP { get; set; }
        public double ChkSrvRegistInterval { get; set; }
        public int TvTestOpenWait { get; set; }
        public int TvTestChgBonWait { get; set; }
        public string FontNameListView { get; set; }
        public double FontSizeListView { get; set; }
        public bool FontBoldListView { get; set; }
        public List<string> RecEndColors { get; set; }
        public List<uint> RecEndCustColors { get; set; }
        public string ListDefColor { get; set; }
        public uint ListDefCustColor { get; set; }
        public string ListRuledLineColor { get; set; }
        public uint ListRuledLineCustColor { get; set; }
        public bool ListRuledLine { get; set; }
        public bool ListRuledLineContent { get; set; }
        public List<string> RecModeFontColors { get; set; }
        public List<uint> RecModeFontCustColors { get; set; }
        public List<string> ResBackColors { get; set; }
        public List<uint> ResBackCustColors { get; set; }
        public List<string> StatColors { get; set; }
        public List<uint> StatCustColors { get; set; }
        public uint ExecBat { get; set; }
        public uint SuspendChk { get; set; }
        public uint SuspendChkTime { get; set; }
        public List<ListColumnInfo> ReserveListColumn { get; set; }
        public List<ListColumnInfo> RecInfoListColumn { get; set; }
        public List<ListColumnInfo> AutoAddEpgColumn { get; set; }
        public List<ListColumnInfo> AutoAddManualColumn { get; set; }
        public List<ListColumnInfo> EpgListColumn { get; set; }
        public string EpgListColumnHead { get; set; }
        public ListSortDirection EpgListSortDirection { get; set; }
        public List<ListColumnInfo> SearchWndColumn { get; set; }
        public string SearchColumnHead { get; set; }
        public ListSortDirection SearchSortDirection { get; set; }
        public int SearchEpgInfoOpenMode { get; set; }
        public bool SaveSearchKeyword { get; set; }
        public bool ShowLogo { get; set; }
        public List<ListColumnInfo> InfoSearchWndColumn { get; set; }
        public string InfoSearchColumnHead { get; set; }
        public ListSortDirection InfoSearchSortDirection { get; set; }
        public bool InfoSearchItemTooltip { get; set; }
        public InfoSearchSettingData InfoSearchData { get; set; }
        public short AutoSaveNotifyLog { get; set; }
        public int NotifyLogMax { get; set; }
        public bool NotifyLogEpgTimer { get; set; }
        public bool ShowTray { get; set; }
        public bool MinHide { get; set; }
        public int NoStyle { get; set; }
        public bool ApplyContextMenuStyle { get; set; }
        public int NoSendClose { get; set; }
        public bool CautionManyChange { get; set; }
        public int CautionManyNum { get; set; }
        public bool CautionOnRecChange { get; set; }
        public int CautionOnRecMarginMin { get; set; }
        public bool SyncResAutoAddChange { get; set; }
        public bool SyncResAutoAddChgNewRes { get; set; }
        public bool SyncResAutoAddChgKeepRecTag { get; set; }
        public bool SyncResAutoAddDelete { get; set; }
        public double DisplayNotifyJumpTime { get; set; }
        public bool DisplayReserveAutoAddMissing { get; set; }
        public bool DisplayReserveMultiple { get; set; }
        public bool TryEpgSetting { get; set; }
        public bool LaterTimeUse { get; set; }
        public int LaterTimeHour { get; set; }
        public bool DisplayPresetOnSearch { get; set; }
        public bool ForceNWMode { get; set; }
        public string SettingFolderPathNW { get; set; }
        public bool UpdateTaskText { get; set; }
        public bool DisplayStatus { get; set; }
        public bool DisplayStatusNotify { get; set; }
        public bool ResHideButton { get; set; }
        public bool RecInfoHideButton { get; set; }
        public bool AutoAddEpgHideButton { get; set; }
        public bool AutoAddManualHideButton { get; set; }
        public Dock MainViewButtonsDock { get; set; }
        public CtxmCode StartTab { get; set; }
        public bool TrimSortTitle { get; set; }
        public bool KeepReserveWindow { get; set; }
        public PicUpTitle PicUpTitleWork { get; set; }
        public bool ArcSearch { get; set; }//暫定

        [XmlIgnore]
        public bool SeparateFixedTuners { get; set; }

        //WakeUpHDD関係
        [XmlIgnore]
        public int RecAppWakeTime { get; set; }
        [XmlIgnore]
        public bool WakeUpHdd { get; set; }
        [XmlIgnore]
        public int NoWakeUpHddMin { get; set; }
        [XmlIgnore]
        public int WakeUpHddOverlapNum { get; set; }
        
        //録画設定関係(デフォルトマージン等)
        [XmlIgnore]
        public int DefStartMargin { get; set; }
        [XmlIgnore]
        public int DefEndMargin { get; set; }
        [XmlIgnore]
        public bool DefServiceCaption { get; set; }
        [XmlIgnore]
        public bool DefServiceData { get; set; }
        [XmlIgnore]
        public int DefRecEndMode { get; set; }
        [XmlIgnore]
        public byte DefRebootFlg { get; set; }

        private List<RecPresetItem> recPresetList = null;
        [XmlIgnore]
        public List<RecPresetItem> RecPresetList
        {
            get
            {
                if (recPresetList == null) recPresetList = RecPresetItem.LoadPresetList();
                return recPresetList;
            }
            set { recPresetList = value; }
        }

        private List<string> defRecFolders = null;
        [XmlIgnore]
        public List<string> DefRecFolders
        {
            get
            {
                if (defRecFolders == null)
                {
                    defRecFolders = new List<string>();
                    int num = IniFileHandler.GetPrivateProfileInt("SET", "RecFolderNum", 0, SettingPath.CommonIniPath);

                    for (int i = 0; i < num; i++)
                    {
                        defRecFolders.Add(IniFileHandler.GetPrivateProfileFolder("SET", "RecFolderPath" + i.ToString(), SettingPath.CommonIniPath));
                    }

                    defRecFolders = defRecFolders.Except(new[] { "" }).ToList();
                    if (defRecFolders.Count == 0) defRecFolders.Add(SettingPath.GetSettingFolderPath(true));
                }
                return defRecFolders;
            }
            set { defRecFolders = value; }
        }
        public void SaveDefRecFolders()
        {
            if (defRecFolders == null) return;

            int recFolderCount = defRecFolders.Count == 1 && defRecFolders[0].Equals(SettingPath.GetSettingFolderPath(true), StringComparison.OrdinalIgnoreCase) == true ? 0 : defRecFolders.Count;
            IniFileHandler.WritePrivateProfileString("SET", "RecFolderNum", recFolderCount, SettingPath.CommonIniPath);
            IniFileHandler.DeletePrivateProfileNumberKeys("SET", SettingPath.CommonIniPath, "RecFolderPath");

            for (int i = 0; i < recFolderCount; i++)
            {
                IniFileHandler.WritePrivateProfileString("SET", "RecFolderPath" + i.ToString(), defRecFolders[i], SettingPath.CommonIniPath);
            }
        }

        public void LoadIniOptions()
        {
            SeparateFixedTuners = IniFileHandler.GetPrivateProfileBool("SET", "SeparateFixedTuners", false, SettingPath.TimerSrvIniPath);
            DefStartMargin = IniFileHandler.GetPrivateProfileInt("SET", "StartMargin", 5, SettingPath.TimerSrvIniPath);
            DefEndMargin = IniFileHandler.GetPrivateProfileInt("SET", "EndMargin", 2, SettingPath.TimerSrvIniPath);
            DefServiceCaption = IniFileHandler.GetPrivateProfileBool("SET", "Caption", true, SettingPath.TimerSrvIniPath);
            DefServiceData = IniFileHandler.GetPrivateProfileBool("SET", "Data", false, SettingPath.TimerSrvIniPath);
            DefRecEndMode = IniFileHandler.GetPrivateProfileInt("SET", "RecEndMode", 2, SettingPath.TimerSrvIniPath);
            DefRebootFlg = (byte)IniFileHandler.GetPrivateProfileInt("SET", "Reboot", 0, SettingPath.TimerSrvIniPath);
            RecAppWakeTime = IniFileHandler.GetPrivateProfileInt("SET", "RecAppWakeTime", 2, SettingPath.TimerSrvIniPath);
            WakeUpHdd = IniFileHandler.GetPrivateProfileBool("SET", "WakeUpHdd", false, SettingPath.TimerSrvIniPath);
            NoWakeUpHddMin = IniFileHandler.GetPrivateProfileInt("SET", "NoWakeUpHddMin", 30, SettingPath.TimerSrvIniPath);
            WakeUpHddOverlapNum = IniFileHandler.GetPrivateProfileInt("SET", "WakeUpHddOverlapNum", 0, SettingPath.TimerSrvIniPath);
            recPresetList = null;
            defRecFolders = null;
        }
        public void SaveIniOptions()
        {
            IniFileHandler.WritePrivateProfileString("SET", "SeparateFixedTuners", SeparateFixedTuners, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "StartMargin", DefStartMargin, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "EndMargin", DefEndMargin, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecEndMode", DefRecEndMode, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "Reboot", DefRebootFlg, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "Caption", DefServiceCaption, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "Data", DefServiceData, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "RecAppWakeTime", RecAppWakeTime, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "WakeUpHdd", WakeUpHdd, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "NoWakeUpHddMin", NoWakeUpHddMin, SettingPath.TimerSrvIniPath);
            IniFileHandler.WritePrivateProfileString("SET", "WakeUpHddOverlapNum", WakeUpHddOverlapNum, SettingPath.TimerSrvIniPath);
            RecPresetItem.SavePresetList(recPresetList);
            SaveDefRecFolders();
        }
        private void DeepCopyXmlIgnoreSettingsTo(Settings other)
        {
            other.SeparateFixedTuners = SeparateFixedTuners;
            other.DefStartMargin = DefStartMargin;
            other.DefEndMargin = DefEndMargin;
            other.DefServiceCaption = DefServiceCaption;
            other.DefServiceData = DefServiceData;
            other.DefRecEndMode = DefRecEndMode;
            other.DefRebootFlg = DefRebootFlg;
            other.RecAppWakeTime = RecAppWakeTime;
            other.WakeUpHdd = WakeUpHdd;
            other.NoWakeUpHddMin = NoWakeUpHddMin;
            other.WakeUpHddOverlapNum = WakeUpHddOverlapNum;
            other.recPresetList = recPresetList.DeepClone();
            other.defRecFolders = defRecFolders == null ? null : defRecFolders.ToList();
        }

        public Settings()
        {
            //カラー関係
            TunerServiceColors = new List<string>();
            TunerServiceCustColors = new List<uint>();
            RecEndColors = new List<string>();
            RecEndCustColors = new List<uint>();
            ListDefColor = "カスタム";
            ListDefCustColor = 0xFF042271;
            ListRuledLineColor = "LightGray";
            ListRuledLineCustColor = 0xFFFFFFFF;
            ListRuledLine = true;
            ListRuledLineContent = true;
            RecModeFontColors = new List<string>();
            RecModeFontCustColors = new List<uint>();
            ResBackColors = new List<string>();
            ResBackCustColors = new List<uint>();
            StatColors = new List<string>();
            StatCustColors = new List<uint>();

            UseCustomEpgView = false;
            CustomEpgTabList = new List<CustomEpgTabInfo>();
            EpgSettingList = new IndexSafeList<EpgSetting>();
            SynchronizeEpgScroll = false;
            ToggleEpgModeOnHeaderLeftClick = false;
            NoToolTip = false;
            ToolTipWidth = 400;
            NoBallonTips = false;
            BalloonTipRealtime = false;
            ForceHideBalloonTipSec = 0;
            PlayDClick = false;
            ConfirmDelRecInfoFileDelete = true;
            ShowEpgCapServiceOnly = false;
            SortServiceList = true;
            ExitAfterProcessingArgs = false;
            RecinfoErrCriticalDrops = false;
            ReserveToolTipMode = 0;
            ReserveEpgInfoOpenMode = 0;
            TunerFontNameService = DefaultFontName;
            TunerFontSizeService = 12;
            TunerFontBoldService = true;
            TunerFontName = DefaultFontName;
            TunerFontSize = 12;
            TunerMinHeight = 2;
            TunerMinimumLine = 0.6;
            TunerDragScroll = 1.5;
            TunerScrollSize = 240;
            TunerMouseScrollAuto = false;
            TunerHorizontalScrollSize = 150;
            TunerMouseHorizontalScrollAuto = false;
            TunerWidth = 150;
            TunerNameTooltip = false;
            TunerServiceNoWrap = true;
            TunerBorderLeftSize = 1;
            TunerBorderTopSize = 1;
            TunerTitleIndent = true;
            TunerToolTip = false;
            TunerToolTipViewWait = 1500;
            TunerPopup = false;
            TunerPopupMode = 0;
            TunerPopupRecinfo = false;
            TunerInfoSingleClick = false;
            TunerPopupWidth = 1;
            TunerChangeBorderMode = 1;
            TunerColorModeUse = false;
            TunerDisplayOffReserve = true;
            TunerToolTipMode = 0;
            TunerEpgInfoOpenMode = 0;
            FontReplacePatternEdit = "";
            FontSizeReplacePattern = 12;
            FontBoldReplacePattern = false;
            ReplacePatternEditFontShare = false;
            EpgNameTabEnabled = true;
            EpgViewModeTabEnabled = false;
            EpgTabMoveCheckEnabled = true;
            RemoconIDList = new SerializableDictionary<ulong, byte>();
            ResColumnHead = "";
            ResSortDirection = ListSortDirection.Ascending;
            WndSettings = new WindowSettingData();
            SearchWndTabsHeight = 0;
            SearchWndJunreHeight = -1;
            CloseMin = false;
            WakeMin = false;
            WakeMinTraySilent = true;
            ViewButtonShowAsTab = false;
            ViewButtonList = new List<string>();
            TaskMenuList = new List<string>();
            Cust1BtnName = "";
            Cust1BtnCmd = "";
            Cust1BtnCmdOpt = "";
            Cust2BtnName = "";
            Cust2BtnCmd = "";
            Cust2BtnCmdOpt = "";
            Cust3BtnName = "";
            Cust3BtnCmd = "";
            Cust3BtnCmdOpt = "";
            AndKeyList = new List<string>();
            NotKeyList = new List<string>();
            DefSearchKey = new EpgSearchKeyInfo();
            UseLastSearchKey = false;
            SearchPresetList = new List<SearchPresetItem>();
            SetWithoutSearchKeyWord = false;
            SetWithoutRecTag = false;
            RecInfoToolTipMode = 0;
            RecInfoColumnHead = "";
            RecInfoSortDirection = ListSortDirection.Ascending;
            RecInfoDropErrIgnore = 0;
            RecInfoDropWrnIgnore = 0;
            RecInfoScrambleIgnore = 0;
            RecInfoDropExcept = new List<string>();
            RecInfoNoYear = false;
            RecInfoNoSecond = false;
            RecInfoNoDurSecond = false;
            RecInfoNoEnd = false;
            ResInfoNoYear = false;
            ResInfoNoSecond = false;
            ResInfoNoDurSecond = false;
            ResInfoNoEnd = false;
            TvTestExe = "";
            TvTestCmd = "";
            NwTvMode = false;
            NwTvModeUDP = false;
            NwTvModeTCP = false;
            NwTvModePipe = false;
            FilePlay = true;
            FilePlayExe = "";
            FilePlayCmd = "";
            FilePlayOnAirWithExe = true;
            FilePathReplaceRecFile = false;
            FilePathReplacePattern = "";
            FilePathReplaceTestPath = "C:\\Test\\ファイル";
            OpenFolderWithFileDialog = false;
            IEpgStationList = new List<IEPGStationInfo>();
            MenuSet = new MenuSettingData();
            NWServerIP = "";
            NWServerPort = 4510;
            NWWaitPort = 0;
            NWMacAdd = "";
            NWPreset = new List<NWPresetItem>();
            WakeReconnectNW = false;
            WakeCheckService = false;
            WakeCheckServiceDialog = true;
            WoLWaitRecconect = false;
            WoLWaitSecond= 30;
            SuspendCloseNW = false;
            NgAutoEpgLoadNW = false;
            NoReserveEventList = false;
            NoEpgAutoAddAppend = false;
            PrebuildEpg = false;
            PrebuildEpgAll = false;
            ChkSrvRegistTCP = false;
            ChkSrvRegistInterval = 5;
            TvTestOpenWait = 2000;
            TvTestChgBonWait = 2000;
            FontNameListView = DefaultFontName;
            FontSizeListView = 12;
            FontBoldListView = false;
            ExecBat = 0;
            SuspendChk = 0;
            SuspendChkTime = 15;
            ReserveListColumn = new List<ListColumnInfo>();
            RecInfoListColumn = new List<ListColumnInfo>();
            AutoAddEpgColumn = new List<ListColumnInfo>();
            AutoAddManualColumn = new List<ListColumnInfo>();
            EpgListColumn = new List<ListColumnInfo>();
            EpgListColumnHead = "";
            EpgListSortDirection = ListSortDirection.Ascending;
            SearchWndColumn = new List<ListColumnInfo>();
            SearchColumnHead = "";
            SearchSortDirection = ListSortDirection.Ascending;
            SearchEpgInfoOpenMode = 0;
            SaveSearchKeyword = true;
            ShowLogo = false;
            InfoSearchWndColumn = new List<ListColumnInfo>();
            InfoSearchColumnHead = "";
            InfoSearchSortDirection = ListSortDirection.Ascending;
            InfoSearchItemTooltip = true;
            InfoSearchData = new InfoSearchSettingData(true);
            AutoSaveNotifyLog = 0;
            NotifyLogMax = 100;
            NotifyLogEpgTimer = false;
            ShowTray = false;
            MinHide = true;
            NoStyle = 1;
            ApplyContextMenuStyle = false;
            NoSendClose = 0;
            CautionManyChange = true;
            CautionManyNum = 10;
            CautionOnRecChange = true;
            CautionOnRecMarginMin = 5;
            SyncResAutoAddChange = false;
            SyncResAutoAddChgNewRes = false;
            SyncResAutoAddChgKeepRecTag = false;
            SyncResAutoAddDelete = false;
            DisplayNotifyJumpTime = 3;
            DisplayReserveAutoAddMissing = false;
            DisplayReserveMultiple = true;
            TryEpgSetting = false;
            LaterTimeUse = false;
            LaterTimeHour = 28 - 24;
            DisplayPresetOnSearch = false;
            ForceNWMode = false;
            SettingFolderPathNW = "";
            UpdateTaskText = false;
            DisplayStatus = false;
            DisplayStatusNotify = false;
            ResHideButton = false;
            RecInfoHideButton = false;
            AutoAddEpgHideButton = false;
            AutoAddManualHideButton = false;
            MainViewButtonsDock = Dock.Right;
            StartTab = CtxmCode.ReserveView;
            TrimSortTitle = false;
            KeepReserveWindow = false;
            PicUpTitleWork = new PicUpTitle();
            ArcSearch = false;
        }

        public Settings DeepCloneStaticSettings()
        {
            var xs = new XmlSerializer(typeof(Settings));
            var ms = new MemoryStream();
            xs.Serialize(ms, this);
            ms.Seek(0, SeekOrigin.Begin);
            var other = (Settings)xs.Deserialize(ms);
            DeepCopyXmlIgnoreSettingsTo(other);
            return other;
        }
        public void ShallowCopyDynamicSettingsTo(Settings dest)
        {
            //設定画面と関係なくその場で動的に更新されるプロパティ
            dest.ResColumnHead = ResColumnHead;
            dest.ResSortDirection = ResSortDirection;
            dest.WndSettings = WndSettings;
            dest.SearchWndTabsHeight = SearchWndTabsHeight;
            dest.SearchWndJunreHeight = SearchWndJunreHeight;
            dest.SetWithoutSearchKeyWord = SetWithoutSearchKeyWord;
            dest.SetWithoutRecTag = SetWithoutRecTag;
            dest.RecInfoColumnHead = RecInfoColumnHead;
            dest.RecInfoSortDirection = RecInfoSortDirection;
            dest.OpenFolderWithFileDialog = OpenFolderWithFileDialog;
            dest.NWServerIP = NWServerIP;
            dest.NWServerPort = NWServerPort;
            dest.NWWaitPort = NWWaitPort;
            dest.NWMacAdd = NWMacAdd;
            dest.NWPreset = NWPreset;
            dest.ReserveListColumn = ReserveListColumn;
            dest.RecInfoListColumn = RecInfoListColumn;
            dest.AutoAddEpgColumn = AutoAddEpgColumn;
            dest.AutoAddManualColumn = AutoAddManualColumn;
            dest.EpgListColumn = EpgListColumn;
            dest.EpgListColumnHead = EpgListColumnHead;
            dest.EpgListSortDirection = EpgListSortDirection;
            dest.SearchWndColumn = SearchWndColumn;
            dest.SearchColumnHead = SearchColumnHead;
            dest.SearchSortDirection = SearchSortDirection;
            dest.InfoSearchWndColumn = InfoSearchWndColumn;
            dest.InfoSearchColumnHead = InfoSearchColumnHead;
            dest.InfoSearchSortDirection = InfoSearchSortDirection;
            dest.InfoSearchItemTooltip = InfoSearchItemTooltip;
            dest.InfoSearchData = InfoSearchData;
            dest.NotifyLogMax = NotifyLogMax;
            dest.NotifyLogEpgTimer = NotifyLogEpgTimer;
            dest.TryEpgSetting = TryEpgSetting;
            dest.SettingFolderPathNW = SettingFolderPathNW;
            dest.AndKeyList = AndKeyList;
            dest.NotKeyList = NotKeyList;
            dest.ArcSearch = ArcSearch;
        }

        private static Settings _instance;
        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Settings();
                    //色設定関係
                    _instance.SetColorSetting();
                }
                return _instance;
            }
            set
            {
                _instance = value;
                _brushCache = null;
            }
        }

        private static SettingsBrushCache _brushCache;
        public static SettingsBrushCache BrushCache
        {
            get
            {
                if (_brushCache == null)
                {
                    _brushCache = new SettingsBrushCache();
                }
                return _brushCache;
            }
        }

        private static bool appResourceDictionaryInitialized;
        private static ResourceDictionary _appResourceDictionary;
        public static ResourceDictionary AppResourceDictionary
        {
            get
            {
                if (appResourceDictionaryInitialized == false)
                {
                    appResourceDictionaryInitialized = true;
                    if (Instance.NoStyle == 0)
                    {
                        try
                        {
                            string path = Path.Combine(SettingPath.ModulePath, SettingPath.ModuleName + ".rd.xaml");
                            if (File.Exists(path))
                            {
                                //ResourceDictionaryを定義したファイルがあるので本体にマージする
                                _appResourceDictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(path));
                            }
                            else
                            {
                                //既定のテーマ(Aero)をマージする
                                _appResourceDictionary = (ResourceDictionary)Application.LoadComponent(
                                    new Uri("/PresentationFramework.Aero, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35;component/themes/aero.normalcolor.xaml", UriKind.Relative));
                            }
                        }
                        catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                    }
                }
                return _appResourceDictionary;
            }
        }

        private static bool contextMenuResourceDictionaryInitialized;
        private static ResourceDictionary _contextMenuResourceDictionary;
        public static ResourceDictionary ContextMenuResourceDictionary
        {
            get
            {
                if (contextMenuResourceDictionaryInitialized == false)
                {
                    contextMenuResourceDictionaryInitialized = true;
                    if (Instance.ApplyContextMenuStyle)
                    {
                        try
                        {
                            string path = Path.Combine(SettingPath.ModulePath, SettingPath.ModuleName + ".rdcm.xaml");
                            if (File.Exists(path))
                            {
                                //ResourceDictionaryを定義したファイルがあるので本体にマージする
                                _contextMenuResourceDictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(path));
                            }
                            else
                            {
                                //既定のテーマ(Aero)をマージする
                                _contextMenuResourceDictionary = (ResourceDictionary)Application.LoadComponent(
                                    new Uri("/PresentationFramework.Aero, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35;component/themes/aero.normalcolor.xaml", UriKind.Relative));
                            }
                        }
                        catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                    }
                }
                return _contextMenuResourceDictionary;
            }
        }
        
        /// <summary>設定ファイルロード関数</summary>
        public static void LoadFromXmlFile(bool nwMode = false)
        {
            string path = GetSettingPath();
            for (int retry = 0; ;)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        //読み込んで逆シリアル化する
                        Instance = (Settings)(new XmlSerializer(typeof(Settings)).Deserialize(fs));
                    }
                    break;
                }
                catch (IOException)
                {
                    //FileNotFoundExceptionを含むので注意(File.Replace()の内部でNotFoundになる瞬間がある)
                    if (++retry > 4)
                    {
                        break;
                    }
                    Thread.Sleep(170);
                }
                catch (Exception ex)
                {
                    //内容が異常など
                    MessageBox.Show(ex.ToString());
                    MessageBox.Show("現在の設定ファイルは次の名前で保存されます。\r\n" + path + ".err",
                        "設定ファイル読込みエラー", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    try
                    {
                        try { File.Replace(path, path + ".err", null); }
                        catch (FileNotFoundException) { File.Move(path, path + ".err"); }
                    }
                    catch (Exception ex2) { MessageBox.Show(ex2.ToString()); }
                    break;
                }
            }

            nwMode |= Instance.ForceNWMode;

            // タイミング合わせにくいので、メニュー系のデータチェックは
            // MenuManager側のワークデータ作成時に実行する。

            Instance.SetCustomEpgTabInfoID(true);
            Instance.SearchPresetList.FixUp();

            //互換チェック
            CompatibilityCheck();

            //色設定関係
            Instance.SetColorSetting();

            if (Instance.ViewButtonList.Count == 0)
            {
                Instance.ViewButtonList.AddRange(GetViewButtonDefIDs(nwMode));
            }
            if (Instance.TaskMenuList.Count == 0)
            {
                Instance.TaskMenuList.AddRange(GetTaskMenuDefIDs(nwMode));
            }
            if (Instance.ReserveListColumn.Count == 0)
            {
                var obj = new ReserveItem();
                Instance.ReserveListColumn.AddRange(GetDefaultColumn(typeof(ReserveView)));
                Instance.ResColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                Instance.ResSortDirection = ListSortDirection.Ascending;
            }
            if (Instance.RecInfoListColumn.Count == 0)
            {
                var obj = new RecInfoItem();
                Instance.RecInfoListColumn.AddRange(GetDefaultColumn(typeof(RecInfoView)));
                Instance.RecInfoColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                Instance.RecInfoSortDirection = ListSortDirection.Descending;
            }
            if (Instance.AutoAddEpgColumn.Count == 0)
            {
                Instance.AutoAddEpgColumn.AddRange(GetDefaultColumn(typeof(EpgAutoAddView)));
            }
            if (Instance.AutoAddManualColumn.Count == 0)
            {
                Instance.AutoAddManualColumn.AddRange(GetDefaultColumn(typeof(ManualAutoAddView)));
            }
            if (Instance.EpgListColumn.Count == 0)
            {
                var obj = new SearchItem();
                Instance.EpgListColumn.AddRange(GetDefaultColumn(typeof(EpgListMainView)));
                Instance.EpgListColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                Instance.EpgListSortDirection = ListSortDirection.Ascending;
            }
            if (Instance.SearchWndColumn.Count == 0)
            {
                var obj = new SearchItem();
                Instance.SearchWndColumn.AddRange(GetDefaultColumn(typeof(SearchWindow)));
                Instance.SearchColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                Instance.SearchSortDirection = ListSortDirection.Ascending;
            }
            if (Instance.InfoSearchWndColumn.Count == 0)
            {
                var obj = new InfoSearchItem();
                Instance.InfoSearchWndColumn.AddRange(GetDefaultColumn(typeof(InfoSearchWindow)));
                Instance.InfoSearchColumnHead = CommonUtil.NameOf(() => obj.StartTime);
                Instance.InfoSearchSortDirection = ListSortDirection.Ascending;
            }
            if (Instance.RecInfoDropExcept.Count == 0)
            {
                Instance.RecInfoDropExcept = RecInfoDropExceptDefault.ToList();
            }
        }

        private static void ReplaceColumTag(string oldTag, string newTag, params List<ListColumnInfo>[] lists)
        {
            foreach(var list in lists) list.ForEach(c => { if (c.Tag == oldTag) c.Tag = newTag; });
        }
        private static void CompatibilityCheck()
        {
            //最新
            if (Instance.verSaved >= 20200723) return;

            //互換用コード。カラム名の変更追従。
            ReplaceColumTag("StartTimeNoDuration", CommonUtil.NameOf(() => new ReserveItem().StartTime), Instance.ReserveListColumn, Instance.RecInfoListColumn);

            if (Instance.verSaved >= 20200721) return;

            //録画無効モード導入の調整。
            Instance.CustomEpgTabList.ForEach(tab =>
            {
                if (tab.RecSetting != null)
                {
                    tab.RecSetting.IsEnable = tab.RecSetting.RecMode / 5 % 2 == 0;
                    tab.RecSetting.RecMode = (byte)((tab.RecSetting.RecMode + tab.RecSetting.RecMode / 5 % 2) % 5); ;
                }
            });
            //互換用コード。カラム名の変更追従。
            ReplaceColumTag("RecEnabled", CommonUtil.NameOf(() => new SearchItem().IsEnabled), Instance.ReserveListColumn);

            if (Instance.verSaved >= 20200528) return;

            //フォーク元との擦り合せ。ショートカットキーの変更に伴い、該当部分を初期化。
            var chgCmds = new[] { EpgCmds.AddInDialog, EpgCmds.ChangeInDialog, EpgCmds.DeleteInDialog, EpgCmds.ReSearch, EpgCmds.ReSearch2 };
            Instance.MenuSet.EasyMenuItems.ForEach(item =>
            {
                if(chgCmds.Contains(item.GetCommand())) item.ShortCuts.Clear();
            });

            if (Instance.verSaved >= 20200411) return;

            //互換用コード。ボタン列非表示関連追従。
            System.Xml.Linq.XElement xdr = null;
            try { xdr = System.Xml.Linq.XDocument.Load(GetSettingPath()).Root; } catch { }
            if (xdr != null)
            {
                var val = new[] { "IsVisibleReserveView", "IsVisibleRecInfoView", "IsVisibleAutoAddView" }
                            .Select(s => xdr.Element(s)).Select(xe => xe == null ? (bool?)null : xe.Value != "true").ToArray();
                if (val[0] != null) Instance.ResHideButton = (bool)val[0];
                if (val[1] != null) Instance.RecInfoHideButton = (bool)val[1];
                if (val[2] != null) Instance.AutoAddEpgHideButton = (bool)val[2];
                if (val[2] != null) Instance.AutoAddManualHideButton = (bool)val[2];
            }

            if (Instance.verSaved >= 20200320) return;

            //各画面「録画タグ」のカラム名のフォーク元追従。
            ReplaceColumTag("RecTag", CommonUtil.NameOf(() => new RecSettingItem().BatFileTag)
                , Instance.ReserveListColumn, Instance.AutoAddEpgColumn, Instance.AutoAddManualColumn
                , Instance.EpgListColumn, Instance.SearchWndColumn, Instance.InfoSearchWndColumn);

            if (Instance.verSaved >= 20190520) return;

            //各画面「長さ」のカラム名のフォーク元追従。
            ReplaceColumTag("ProgramDuration", CommonUtil.NameOf(() => new SearchItem().Duration), Instance.ReserveListColumn, Instance.EpgListColumn, Instance.SearchWndColumn);
            ReplaceColumTag("ProgramDuration", CommonUtil.NameOf(() => new RecInfoItem().Duration), Instance.RecInfoListColumn);
            ReplaceColumTag("ProgramDuration", CommonUtil.NameOf(() => new ManualAutoAddDataItem().Duration), Instance.AutoAddManualColumn);
            ReplaceColumTag("ProgramDuration", CommonUtil.NameOf(() => new InfoSearchItem().Duration), Instance.InfoSearchWndColumn);

            if (Instance.verSaved >= 20190321) return;

            //番組表ごとのデザイン対応
            if (xdr != null)
            {
                Instance.EpgSettingList.Clear();//一応クリア
                for (int i = 0; i < 3; i++)
                {
                    var xe = i == 0 ? xdr : xdr.Element("EpgSetting" + i.ToString());
                    if (xe == null) continue;
                    try
                    {
                        Instance.EpgSettingList.Add((EpgSetting)(new XmlSerializer(typeof(EpgSetting), new XmlRootAttribute(xe.Name.LocalName)).Deserialize(xe.CreateReader())));
                        Instance.EpgSettingList.Last().ID = i;
                    }
                    catch { }
                }
            }

            if (Instance.verSaved >= 20190217) return;

            //録画結果へジャンプ対応
            //全体メニューのみ番組表へジャンプと同じ設定で追加する。個別設定はさわらない。
            MenuSettingData.CmdSaveData data = Instance.MenuSet.EasyMenuItems.FirstOrDefault(item => item.Name == EpgCmds.JumpReserve.Name);
            if (data != null)
            {
                data = data.DeepClone();
                data.Name = EpgCmds.JumpRecInfo.Name;
                Instance.MenuSet.EasyMenuItems.Add(data);
            }

            if (Instance.verSaved >= 20170717) return;

            //検索プリセット対応。DefSearchKeyの吸収があるので旧CS仮対応コードより前。
            Instance.SearchPresetList[0].Data = Instance.DefSearchKey;

            if (Instance.verSaved >= 20170512) return;

            //旧CS仮対応コード(+0x70)も変換する。
            foreach (var info in Instance.CustomEpgTabList)
            {
                info.ViewContentList.AddRange(info.ViewContentKindList.Select(id_old => new EpgContentData((uint)(id_old << 16))));
                EpgContentData.FixNibble(info.ViewContentList);
                EpgContentData.FixNibble(info.SearchKey.contentList);
                info.ViewContentKindList = null;
            }
            EpgContentData.FixNibble(Instance.SearchPresetList[0].Data.contentList);

            //互換用コード。カラム名の変更追従。
            var objk = new EpgAutoDataItem();
            var objm = new ManualAutoAddDataItem();
            ReplaceColumTag("AndKey", CommonUtil.NameOf(() => objk.EventName), Instance.AutoAddEpgColumn);
            ReplaceColumTag("NetworkKey", CommonUtil.NameOf(() => objk.NetworkName), Instance.AutoAddEpgColumn);
            ReplaceColumTag("ServiceKey", CommonUtil.NameOf(() => objk.ServiceName), Instance.AutoAddEpgColumn);
            ReplaceColumTag("Title", CommonUtil.NameOf(() => objm.EventName), Instance.AutoAddManualColumn);
            ReplaceColumTag("Time", CommonUtil.NameOf(() => objm.StartTime), Instance.AutoAddManualColumn);
            ReplaceColumTag("StationName", CommonUtil.NameOf(() => objm.ServiceName), Instance.AutoAddManualColumn);
        }

        /// <summary>設定ファイルセーブ関数</summary>
        public static void SaveToXmlFile(bool notifyException = true)
        {
            try
            {
                string path = GetSettingPath();

                try
                {
                    //所有者などが特殊なときFile.Replace()に失敗することがあるため
                    File.Delete(path + ".back");
                }
                catch { }

                using (var fs = new FileStream(path + ".back", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    //シリアル化して書き込む
                    new XmlSerializer(typeof(Settings)).Serialize(fs, Instance);
                }
                for (int retry = 0; ;)
                {
                    try
                    {
                        File.Replace(path + ".back", path, null);
                        break;
                    }
                    catch (FileNotFoundException)
                    {
                        File.Move(path + ".back", path);
                        break;
                    }
                    catch (IOException)
                    {
                        if (++retry > 4)
                        {
                            throw;
                        }
                        Thread.Sleep(200 * retry);
                    }
                }
            }
            catch (Exception ex) { if (notifyException) MessageBox.Show(ex.ToString()); }
        }

        private static string GetSettingPath()
        {
            return Path.Combine(SettingPath.ModulePath, SettingPath.ModuleName + ".xml");
        }

        public void SetSettings(string propertyName, object value)
        {
            if (propertyName == null) return;
            var info = typeof(Settings).GetProperty(propertyName);
            if (info != null) info.SetValue(this, value, null);
        }

        public object GetSettings(string propertyName)
        {
            if (propertyName == null) return null;
            var info = typeof(Settings).GetProperty(propertyName);
            return (info == null ? null : info.GetValue(this, null));
        }

        public static string DefaultFontName
        {
            get
            {
                string fName = null;
                SystemFonts.MenuFontFamily.FamilyNames.TryGetValue(System.Windows.Markup.XmlLanguage.GetLanguage("ja-JP"), out fName);
                return fName ?? SystemFonts.MenuFontFamily.Source;//別に日本語名無視でもいいかも‥。
            }
        }

        public RecPresetItem RecPreset(int presetID)
        {
            return Instance.RecPresetList[Math.Max(0, Math.Min(presetID, Instance.RecPresetList.Count - 1))];
        }

        //起動中はIDを再利用しない
        private static int EpgTabCounter = 0;
        public void SetCustomEpgTabInfoID(bool reset = false)
        {
            //無いと思うが一応
            reset |= CustomEpgTabList.Count > int.MaxValue - EpgTabCounter;
            if (reset) EpgTabCounter = 0;

            CustomEpgTabList.ForEach(info =>
            {
                if (reset || info.ID < 0) info.ID = EpgTabCounter++;
            });
        }

        private static List<string> viewButtonIDs = new List<string>();
        public const string ViewButtonSpacer = "（空白）";
        public const string TaskMenuSeparator = "（セパレータ）";
        public static void ResisterViewButtonIDs(IEnumerable<string> list)
        {
            viewButtonIDs = list == null ? new List<string>() : list.ToList();
        } 
        public static List<string> GetViewButtonAllIDs()
        {
            return new List<string> { ViewButtonSpacer }.Concat(viewButtonIDs).ToList();
        }
        public static List<string> GetTaskMenuAllIDs()
        {
            return new List<string> { TaskMenuSeparator }.Concat(viewButtonIDs).ToList();
        }
        public static List<string> GetViewButtonDefIDs(bool nwMode)
        {
            if (nwMode == false)
            {
                return new List<string>
                {
                    "設定",
                    ViewButtonSpacer,
                    "検索",
                    ViewButtonSpacer,
                    "スタンバイ",
                    "休止",
                    ViewButtonSpacer,
                    "EPG取得",
                    ViewButtonSpacer,
                    "EPG再読み込み",
                    ViewButtonSpacer,
                    "終了",
                };
            }
            else
            {
                return new List<string>
                {
                    "設定",
                    ViewButtonSpacer,
                    "再接続",
                    ViewButtonSpacer,
                    "検索",
                    ViewButtonSpacer,
                    "EPG取得",
                    ViewButtonSpacer,
                    "EPG再読み込み",
                    ViewButtonSpacer,
                    "終了",
                };
            }
        }
        public static List<string> GetTaskMenuDefIDs(bool nwMode)
        {
            if (nwMode == false)
            {
                return new List<string>
                {
                    "設定",
                    TaskMenuSeparator,
                    "スタンバイ",
                    "休止",
                    TaskMenuSeparator,
                    "終了",
                };
            }
            else
            {
                return new List<string>
                {
                    "設定",
                    TaskMenuSeparator,
                    "再接続",
                    TaskMenuSeparator,
                    "終了",
                };
            }
        }

        public static List<ListColumnInfo> GetDefaultColumn(Type t)
        {
            if (t == typeof(ReserveView))
            {
                var obj = new ReserveItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Duration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecMode) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Priority) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Tuijyu) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Comment) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecFileName) },
                };
            }
            else if (t == typeof(RecInfoView))
            {
                var obj = new RecInfoItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.IsProtect) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Duration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Drops) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Scrambles) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Result) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecFilePath) },
                };
            }
            else if (t == typeof(EpgAutoAddView))
            {
                var obj = new EpgAutoDataItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NotKey) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RegExp) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecMode) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Priority) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Tuijyu) },
                };
            }
            else if (t == typeof(ManualAutoAddView))
            {
                var obj = new ManualAutoAddDataItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.DayOfWeek) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Duration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.RecMode) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Priority) },
                };
            }
            else if (t == typeof(EpgListMainView))
            {
                var obj = new SearchItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Status) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Duration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                };
            }
            else if (t == typeof(SearchWindow))
            {
                var obj = new SearchItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Status) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Duration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ProgramContent) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.JyanruKey) },
                };
            }
            else if (t == typeof(InfoSearchWindow))
            {
                var obj = new InfoSearchItem();
                return new List<ListColumnInfo>
                {
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ViewItemName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Status) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.StartTime) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.Duration) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.NetworkName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ServiceName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.EventName) },
                    new ListColumnInfo { Tag = CommonUtil.NameOf(() => obj.ProgramContent) },
                };
            }
            return null;
        }

        //色リストの初期化用
        private static void _FillList<T>(List<T> list, T val, int num)
        {
            _FillList(list, Enumerable.Repeat(val, num).ToArray());
        }
        private static void _FillList<T>(List<T> list, T[] val)
        {
            if (list.Count < val.Length) list.AddRange(val.Skip(list.Count));
            if (list.Count > val.Length) list.RemoveRange(val.Length, list.Count - val.Length);
        }
        public static void SetColorSettingEpg(EpgSetting set)
        {
            //番組表の背景色17色。過去に16色時代があった。
            _FillList(set.ContentColorList, new[]{
                    "LightYellow"
                    ,"Lavender"
                    ,"LavenderBlush"
                    ,"MistyRose"
                    ,"Honeydew"
                    ,"LightCyan"
                    ,"PapayaWhip"
                    ,"Pink"
                    ,"LightYellow"
                    ,"PapayaWhip"
                    ,"AliceBlue"
                    ,"AliceBlue"
                    ,"White"
                    ,"White"
                    ,"White"
                    ,"WhiteSmoke"
                    ,"White"
                });
            _FillList(set.ContentCustColorList, 0xFFFFFFFF, set.ContentColorList.Count);

            //番組表の予約枠色
            _FillList(set.EpgResColorList, new[]{
                    "Lime"              //通常(EPG予約)
                    ,"Lime"             //通常(プログラム予約)
                    ,"Black"            //無効
                    ,"Red"              //チューナ不足
                    ,"Yellow"           //一部実行
                    ,"Blue"             //自動予約登録不明
                    ,"Purple"           //重複EPG予約
                    ,"Lime"             //録画中(EPG予約)
                    ,"Lime"             //録画中(プログラム予約)
                    ,"Green"            //録画済み
                });
            _FillList(set.EpgResCustColorList, 0xFFFFFFFF, set.EpgResColorList.Count);

            //番組表の時間軸のデフォルトの背景色、その他色
            _FillList(set.EpgEtcColors, new[]{
                    "MediumPurple"      //00-05時
                    ,"LightSeaGreen"    //06-11時
                    ,"LightSalmon"      //12-17時
                    ,"CornflowerBlue"   //18-23時
                    ,"LightSlateGray"   //サービス背景色
                    ,"DarkGray"         //番組表背景色
                    ,"DarkGray"         //番組枠色
                    ,"White"            //サービス文字
                    ,"Silver"           //サービス枠色
                    ,"White"            //時間文字色
                    ,"White"            //時間枠色
                    ,"LightSlateGray"   //一週間モード日付枠色
                });
            _FillList(set.EpgEtcCustColors, 0xFFFFFFFF, set.EpgEtcColors.Count);
        }
        public void SetColorSetting()
        {
            if (EpgSettingList.Count == 0) EpgSettingList.Add(new EpgSetting());
            EpgSettingList.ForEach(set => SetColorSettingEpg(set));

            //チューナー画面各色、保存名はServiceColorsだが他も含む。
            _FillList(TunerServiceColors, Enumerable.Repeat("Black", 7)//固定色2 + 優先度色5 + 追加の画面色
                    .Concat(new[]{
                        "DarkGray"          //チューナ画面背景色
                        ,"LightGray"        //予約枠色(EPG予約)
                        ,"Black"            //時間文字色
                        ,"AliceBlue"        //時間背景色
                        ,"LightSlateGray"   //時間枠色
                        ,"Black"            //チューナー名文字色
                        ,"AliceBlue"        //チューナー名背景色
                        ,"LightSlateGray"   //チューナー名枠色
                        ,"LightGray"        //予約枠色(プログラム予約)
                        ,"Black"            //予約枠色(無効)
                        ,"OrangeRed"        //予約枠色(録画中)(EPG予約)
                        ,"OrangeRed"        //予約枠色(録画中)(プログラム予約)
                }).ToArray());
            _FillList(TunerServiceCustColors, 0xFFFFFFFF, TunerServiceColors.Count);

            //録画済み一覧背景色
            _FillList(RecEndColors, new[]{
                        "White"         //デフォルト
                        ,"LightPink"    //エラー
                        ,"LightYellow"  //ワーニング
                    });
            _FillList(RecEndCustColors, 0xFFFFFFFF, RecEndColors.Count);

            //録画モード別予約文字色
            _FillList(RecModeFontColors, ListDefColor, 6);//そのときのデフォルト色をあてる
            _FillList(RecModeFontCustColors, ListDefCustColor, RecModeFontColors.Count);

            //状態別予約背景色
            _FillList(ResBackColors, new[]{
                        "White"         //通常
                        ,"LightGray"    //無効
                        ,"LightPink"    //チューナー不足
                        ,"LightYellow"  //一部実行
                        ,"LightBlue"    //自動予約登録不明
                        ,"Thistle"      //重複EPG予約
                    });
            _FillList(ResBackCustColors, 0xFFFFFFFF, ResBackColors.Count);

            //予約状態列文字色
            _FillList(StatColors, new[]{
                        "Blue"      //予約中
                        ,"OrangeRed"//録画中
                        ,"LimeGreen"//放送中
                        ,"OrangeRed"//視聴中
                        ,"Green"    //録画済み
                    });
            _FillList(StatCustColors, 0xFFFFFFFF, StatColors.Count);
        }

        public class EpgColorList
        {
            public List<Brush> ContentColorList { get; private set; }
            public List<Brush> ContentFilteredColorList { get; private set; }
            public List<Brush> ResColorList { get; private set; }
            public List<Brush> ResFillColorList { get; private set; }
            public List<Brush> TimeColorList { get; private set; }
            public Brush TitleColor { get; private set; }
            public Brush NormalColor { get; private set; }
            public Brush TitleFilteredColor { get; private set; }
            public Brush NormalFilteredColor { get; private set; }
            public Brush ServiceBackColor { get; private set; }
            public Brush BackColor { get; private set; }
            public Brush BorderColor { get; private set; }
            public Brush ServiceFontColor { get; private set; }
            public Brush ServiceBorderColor { get; private set; }
            public Brush TimeFontColor { get; private set; }
            public Brush TimeBorderColor { get; private set; }
            public Brush WeekdayBorderColor { get; private set; }

            public EpgColorList(int idx)
            {
                EpgSetting set = Instance.EpgSettingList[idx];

                ContentColorList = new List<Brush>();
                ContentFilteredColorList = new List<Brush>();
                ResColorList = new List<Brush>();
                ResFillColorList = new List<Brush>();
                TimeColorList = new List<Brush>();

                SolidColorBrush brush;
                for (int i = 0; i < set.ContentColorList.Count; i++)
                {
                    brush = BrushCache.CustColorBrush(set.ContentColorList[i], set.ContentCustColorList[i]);
                    ContentColorList.Add(set.EpgGradation ? (Brush)BrushCache.CustGradientBrush(brush.Color) : brush);
                    brush = BrushCache.CustColorBrush(set.ContentColorList[i], set.ContentCustColorList[i], 255, 50);
                    ContentFilteredColorList.Add(set.EpgGradation ? (Brush)BrushCache.CustGradientBrush(brush.Color) : brush);
                }

                //0→50で塗りつぶしの不透明度が上がる
                int fillOpacity = Math.Min(set.ReserveRectFillOpacity, 50) * 2;
                //50→100で枠の不透明度が下がる
                int strokeOpacity = Math.Min(100 - set.ReserveRectFillOpacity, 50) * 2;

                for (int i = 0; i < set.EpgResColorList.Count; i++)
                {
                    ResColorList.Add(BrushCache.CustColorBrush(set.EpgResColorList[i], set.EpgResCustColorList[i], 0xA0, strokeOpacity));
                    ResFillColorList.Add(BrushCache.CustColorBrush(set.EpgResColorList[i], set.EpgResCustColorList[i], 0xA0, fillOpacity));
                }
                for (int i = 0; i < set.EpgEtcColors.Count; i++)
                {
                    brush = BrushCache.CustColorBrush(set.EpgEtcColors[i], set.EpgEtcCustColors[i]);
                    TimeColorList.Add(set.EpgGradationHeader ? (Brush)BrushCache.CustGradientBrush(brush.Color) : brush);
                }

                TitleColor = BrushCache.CustColorBrush(set.TitleColor1, set.TitleCustColor1);
                NormalColor = BrushCache.CustColorBrush(set.TitleColor2, set.TitleCustColor2);
                TitleFilteredColor = BrushCache.CustColorBrush(set.TitleColor1, set.TitleCustColor1, 255, 70);
                NormalFilteredColor = BrushCache.CustColorBrush(set.TitleColor2, set.TitleCustColor2, 255, 70);
                brush = BrushCache.CustColorBrush(set.EpgEtcColors[4], set.EpgEtcCustColors[4]);
                ServiceBackColor = set.EpgGradationHeader ? (Brush)ColorDef.GradientBrush(brush.Color, 1.0, 2.0) : brush;
                BackColor = BrushCache.CustColorBrush(set.EpgEtcColors[5], set.EpgEtcCustColors[5]);
                BorderColor = BrushCache.CustColorBrush(set.EpgEtcColors[6], set.EpgEtcCustColors[6]);
                ServiceFontColor = BrushCache.CustColorBrush(set.EpgEtcColors[7], set.EpgEtcCustColors[7]);
                ServiceBorderColor = BrushCache.CustColorBrush(set.EpgEtcColors[8], set.EpgEtcCustColors[8]);
                TimeFontColor = BrushCache.CustColorBrush(set.EpgEtcColors[9], set.EpgEtcCustColors[9]);
                TimeBorderColor = BrushCache.CustColorBrush(set.EpgEtcColors[10], set.EpgEtcCustColors[10]);
                WeekdayBorderColor = BrushCache.CustColorBrush(set.EpgEtcColors[11], set.EpgEtcCustColors[11]);
            }
        }
        public class SettingsBrushCache
        {
            public class EpgColorCache
            {
                private Dictionary<int, EpgColorList> Data = new Dictionary<int, EpgColorList>();
                public EpgColorList this[int idx]
                {
                    get
                    {
                        EpgColorList ret;
                        if (Data.TryGetValue(idx, out ret) == false)
                        {
                            ret = new EpgColorList(idx);
                            Data.Add(idx, ret);
                        }
                        return ret;
                    }
                }
            }

            //主に色関係の設定
            public EpgColorCache Epg { get; private set; }
            public DropShadowEffect EpgBlurEffect { get; private set; }
            public DoubleCollection EpgDashArray { get; private set; }

            public List<Brush> CustTunerServiceColor { get; private set; }
            public Brush CustTunerTextColor { get; private set; }
            public Brush TunerBackColor { get; private set; }
            public Brush TunerTimeFontColor { get; private set; }
            public Brush TunerTimeBackColor { get; private set; }
            public Brush TunerTimeBorderColor { get; private set; }
            public Brush TunerNameFontColor { get; private set; }
            public Brush TunerNameBackColor { get; private set; }
            public Brush TunerNameBorderColor { get; private set; }
            public List<Brush> TunerResBorderColor { get; private set; }
            public DashStyle TunerDashStyle { get; private set; }
            public List<Brush> ResBackColor { get; private set; }
            public Brush ListDefForeColor { get; private set; }
            public Brush ListRuledLineColor { get; private set; }
            public List<Brush> RecModeForeColor { get; private set; }
            public List<Brush> ResStatusColor { get; private set; }
            public List<Brush> RecEndBackColor { get; private set; }

            //キャッシュ
            private Dictionary<uint, SolidColorBrush> SolidBrushCache = new Dictionary<uint, SolidColorBrush>();
            private Dictionary<uint, LinearGradientBrush> GradientBrushCache = new Dictionary<uint, LinearGradientBrush>();

            //SettingsBrushCache()用のコンバートメソッド
            public SolidColorBrush CustColorBrush(string name, uint cust = 0, byte a = 0xFF, int opacity = 100)
            {
                Color c = (name == "カスタム" ? ColorDef.FromUInt(cust) : ColorDef.ColorFromName(name));
                a = name == "カスタム" ? c.A : a;
                c = c.A != 0 && (a != 0xFF || opacity != 100) ? Color.FromArgb((byte)(a * opacity / 100), c.R, c.G, c.B) : c;

                SolidColorBrush brush;
                if (SolidBrushCache.TryGetValue(ColorDef.ToUInt(c), out brush) == false)
                {
                    brush = new SolidColorBrush(c);
                    brush.Freeze();
                    SolidBrushCache.Add(ColorDef.ToUInt(c), brush);
                }
                return brush;
            }
            public LinearGradientBrush CustGradientBrush(Color c)
            {
                LinearGradientBrush brush;
                if (GradientBrushCache.TryGetValue(ColorDef.ToUInt(c), out brush) == false)
                {
                    brush = ColorDef.GradientBrush(c);
                    brush.Freeze();
                    GradientBrushCache.Add(ColorDef.ToUInt(c), brush);
                }
                return brush;
            }
            private void SimpleColorSet(List<Brush> listBrush, List<string> listName, List<uint> listCust, int start = 0, int end = 0)
            {
                if (end <= 0) end = listName.Count;
                for (int i = start; i < end; i++) listBrush.Add(CustColorBrush(listName[i], listCust[i]));
            }
            public SettingsBrushCache()
            {
                Epg = new EpgColorCache();

                EpgBlurEffect = new DropShadowEffect() { BlurRadius = 10 };
                EpgBlurEffect.Freeze();
                EpgDashArray = new DoubleCollection() { 2.5, 2 };
                EpgDashArray.Freeze();

                CustTunerServiceColor = new List<Brush>();
                TunerResBorderColor = new List<Brush>();
                TunerDashStyle = new DashStyle(new double[] { 1, 4.5 }, 0);
                TunerDashStyle.Freeze();
                ResBackColor = new List<Brush>();
                RecModeForeColor = new List<Brush>();
                ResStatusColor = new List<Brush>();
                RecEndBackColor = new List<Brush>();

                CustTunerTextColor = CustColorBrush(Instance.TunerServiceColors[1], Instance.TunerServiceCustColors[1]);
                CustTunerServiceColor.Add(CustColorBrush(Instance.TunerServiceColors[0], Instance.TunerServiceCustColors[0]));
                SimpleColorSet(CustTunerServiceColor, Instance.TunerServiceColors, Instance.TunerServiceCustColors, 2, 2 + 5);
                TunerBackColor = CustColorBrush(Instance.TunerServiceColors[7 + 0], Instance.TunerServiceCustColors[7 + 0]);
                TunerTimeFontColor = CustColorBrush(Instance.TunerServiceColors[7 + 2], Instance.TunerServiceCustColors[7 + 2]);
                TunerTimeBackColor = CustColorBrush(Instance.TunerServiceColors[7 + 3], Instance.TunerServiceCustColors[7 + 3]);
                TunerTimeBorderColor = CustColorBrush(Instance.TunerServiceColors[7 + 4], Instance.TunerServiceCustColors[7 + 4]);
                TunerNameFontColor = CustColorBrush(Instance.TunerServiceColors[7 + 5], Instance.TunerServiceCustColors[7 + 5]);
                TunerNameBackColor = CustColorBrush(Instance.TunerServiceColors[7 + 6], Instance.TunerServiceCustColors[7 + 6]);
                TunerNameBorderColor = CustColorBrush(Instance.TunerServiceColors[7 + 7], Instance.TunerServiceCustColors[7 + 7]);
                TunerResBorderColor.Add(CustColorBrush(Instance.TunerServiceColors[7 + 1], Instance.TunerServiceCustColors[7 + 1]));
                SimpleColorSet(TunerResBorderColor, Instance.TunerServiceColors, Instance.TunerServiceCustColors, 7 + 8, 7 + 8 + 4);

                ListDefForeColor = CustColorBrush(Instance.ListDefColor, Instance.ListDefCustColor);
                ListRuledLineColor = Instance.ListRuledLine == false ? null : CustColorBrush(Instance.ListRuledLineColor, Instance.ListRuledLineCustColor);

                SimpleColorSet(RecModeForeColor, Instance.RecModeFontColors, Instance.RecModeFontCustColors);
                SimpleColorSet(ResBackColor, Instance.ResBackColors, Instance.ResBackCustColors);
                SimpleColorSet(ResStatusColor, Instance.StatColors, Instance.StatCustColors);
                SimpleColorSet(RecEndBackColor, Instance.RecEndColors, Instance.RecEndCustColors);
            }
        }
    }

    public interface IEpgSettingAccess
    {
        int EpgSettingIndex { get; }
    }
    public static class EpgCacheAccess
    {
        public static EpgSetting EpgStyle(this IEpgSettingAccess item)
        {
            return Settings.Instance.EpgSettingList[item.EpgSettingIndex];
        }
        public static Settings.EpgColorList EpgBrushCache(this IEpgSettingAccess item)
        {
            return Settings.BrushCache.Epg[item.EpgSettingIndex];
        }
    }
}
