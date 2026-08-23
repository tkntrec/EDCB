using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace EpgTimer
{
    class CommonManager
    {
        public static MainWindow MainWindow { get { return (MainWindow)Application.Current.MainWindow; } }

        public DBManager DB { get; private set; }
        public TVTestCtrlClass TVTestCtrl { get; private set; }
        public List<NotifySrvInfo> NotifyLogList { get; private set; }
        public bool NWMode { get; set; }
        public System.Net.IPAddress NWConnectedIP { get; set; }
        public uint NWConnectedPort { get; set; }
        public bool IsConnected { get { return NWMode == false || Instance.NWConnectedIP != null; } }
        public bool WaitingSrvReady { get; set; }

        MenuManager _mm;
        public MenuManager MM
        {
            get
            {
                //初期化に他のオブジェクトを使うので遅延させる
                if (_mm == null) _mm = new MenuManager();
                //
                return _mm;
            }
            set { _mm = value; }
        }

        private static CommonManager _instance;
        public static CommonManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new CommonManager();
                return _instance;
            }
            set { _instance = value; }
        }

        public CommonManager()
        {
            DB = new DBManager();
            TVTestCtrl = new TVTestCtrlClass();
            NWMode = false;
            NotifyLogList = new List<NotifySrvInfo>();
            WaitingSrvReady = false;
        }

        public static readonly string[] DayOfWeekArray = Enumerable.Range(0, 7).Select(i => (new DateTime(2000, 1, 2 + i)).ToString("ddd")).ToArray();
        public static readonly string[] RecModeList = new string[] { "全サービス", "指定サービス", "全サービス(デコード処理なし)", "指定サービス(デコード処理なし)", "視聴" };
        public static readonly string[] RecEndModeList = new string[] { "何もしない", "スタンバイ", "休止", "シャットダウン" };
        public static readonly string[] IsEnableList = new string[] { "無効", "有効" };
        public static readonly string[] YesNoList = new string[] { "しない", "する" };
        public static readonly string[] PriorityList = new string[] { "1 (低)", "2", "3", "4", "5 (高)" };
        public static readonly Dictionary<char, List<KeyValuePair<string, string>>> ReplaceUrlDictionary = CreateReplaceDictionary(",０,0,１,1,２,2,３,3,４,4,５,5,６,6,７,7,８,8,９,9" +
                ",Ａ,A,Ｂ,B,Ｃ,C,Ｄ,D,Ｅ,E,Ｆ,F,Ｇ,G,Ｈ,H,Ｉ,I,Ｊ,J,Ｋ,K,Ｌ,L,Ｍ,M,Ｎ,N,Ｏ,O,Ｐ,P,Ｑ,Q,Ｒ,R,Ｓ,S,Ｔ,T,Ｕ,U,Ｖ,V,Ｗ,W,Ｘ,X,Ｙ,Y,Ｚ,Z" +
                ",ａ,a,ｂ,b,ｃ,c,ｄ,d,ｅ,e,ｆ,f,ｇ,g,ｈ,h,ｉ,i,ｊ,j,ｋ,k,ｌ,l,ｍ,m,ｎ,n,ｏ,o,ｐ,p,ｑ,q,ｒ,r,ｓ,s,ｔ,t,ｕ,u,ｖ,v,ｗ,w,ｘ,x,ｙ,y,ｚ,z" +
                ",！,!,＃,#,＄,$,％,%,＆,&,’,',（,(,）,),～,~,￣,~,＝,=,＠,@,；,;,：,:,？,?,＿,_,＋,+,－,-,＊,*,／,/,．,.,　, ");
        public static readonly ContentKindInfo[] ContentKindList = new[]
        {
            new ContentKindInfo(0x00FF0000, "ニュース／報道", ""),
            new ContentKindInfo(0x00000000, "ニュース／報道", "定時・総合"),
            new ContentKindInfo(0x00010000, "ニュース／報道", "天気"),
            new ContentKindInfo(0x00020000, "ニュース／報道", "特集・ドキュメント"),
            new ContentKindInfo(0x00030000, "ニュース／報道", "政治・国会"),
            new ContentKindInfo(0x00040000, "ニュース／報道", "経済・市況"),
            new ContentKindInfo(0x00050000, "ニュース／報道", "海外・国際"),
            new ContentKindInfo(0x00060000, "ニュース／報道", "解説"),
            new ContentKindInfo(0x00070000, "ニュース／報道", "討論・会談"),
            new ContentKindInfo(0x00080000, "ニュース／報道", "報道特番"),
            new ContentKindInfo(0x00090000, "ニュース／報道", "ローカル・地域"),
            new ContentKindInfo(0x000A0000, "ニュース／報道", "交通"),
            new ContentKindInfo(0x000F0000, "ニュース／報道", "その他"),

            new ContentKindInfo(0x01FF0000, "スポーツ", ""),
            new ContentKindInfo(0x01000000, "スポーツ", "スポーツニュース"),
            new ContentKindInfo(0x01010000, "スポーツ", "野球"),
            new ContentKindInfo(0x01020000, "スポーツ", "サッカー"),
            new ContentKindInfo(0x01030000, "スポーツ", "ゴルフ"),
            new ContentKindInfo(0x01040000, "スポーツ", "その他の球技"),
            new ContentKindInfo(0x01050000, "スポーツ", "相撲・格闘技"),
            new ContentKindInfo(0x01060000, "スポーツ", "オリンピック・国際大会"),
            new ContentKindInfo(0x01070000, "スポーツ", "マラソン・陸上・水泳"),
            new ContentKindInfo(0x01080000, "スポーツ", "モータースポーツ"),
            new ContentKindInfo(0x01090000, "スポーツ", "マリン・ウィンタースポーツ"),
            new ContentKindInfo(0x010A0000, "スポーツ", "競馬・公営競技"),
            new ContentKindInfo(0x010F0000, "スポーツ", "その他"),

            new ContentKindInfo(0x02FF0000, "情報／ワイドショー", ""),
            new ContentKindInfo(0x02000000, "情報／ワイドショー", "芸能・ワイドショー"),
            new ContentKindInfo(0x02010000, "情報／ワイドショー", "ファッション"),
            new ContentKindInfo(0x02020000, "情報／ワイドショー", "暮らし・住まい"),
            new ContentKindInfo(0x02030000, "情報／ワイドショー", "健康・医療"),
            new ContentKindInfo(0x02040000, "情報／ワイドショー", "ショッピング・通販"),
            new ContentKindInfo(0x02050000, "情報／ワイドショー", "グルメ・料理"),
            new ContentKindInfo(0x02060000, "情報／ワイドショー", "イベント"),
            new ContentKindInfo(0x02070000, "情報／ワイドショー", "番組紹介・お知らせ"),
            new ContentKindInfo(0x020F0000, "情報／ワイドショー", "その他"),

            new ContentKindInfo(0x03FF0000, "ドラマ", ""),
            new ContentKindInfo(0x03000000, "ドラマ", "国内ドラマ"),
            new ContentKindInfo(0x03010000, "ドラマ", "海外ドラマ"),
            new ContentKindInfo(0x03020000, "ドラマ", "時代劇"),
            new ContentKindInfo(0x030F0000, "ドラマ", "その他"),

            new ContentKindInfo(0x04FF0000, "音楽", ""),
            new ContentKindInfo(0x04000000, "音楽", "国内ロック・ポップス"),
            new ContentKindInfo(0x04010000, "音楽", "海外ロック・ポップス"),
            new ContentKindInfo(0x04020000, "音楽", "クラシック・オペラ"),
            new ContentKindInfo(0x04030000, "音楽", "ジャズ・フュージョン"),
            new ContentKindInfo(0x04040000, "音楽", "歌謡曲・演歌"),
            new ContentKindInfo(0x04050000, "音楽", "ライブ・コンサート"),
            new ContentKindInfo(0x04060000, "音楽", "ランキング・リクエスト"),
            new ContentKindInfo(0x04070000, "音楽", "カラオケ・のど自慢"),
            new ContentKindInfo(0x04080000, "音楽", "民謡・邦楽"),
            new ContentKindInfo(0x04090000, "音楽", "童謡・キッズ"),
            new ContentKindInfo(0x040A0000, "音楽", "民族音楽・ワールドミュージック"),
            new ContentKindInfo(0x040F0000, "音楽", "その他"),

            new ContentKindInfo(0x05FF0000, "バラエティ", ""),
            new ContentKindInfo(0x05000000, "バラエティ", "クイズ"),
            new ContentKindInfo(0x05010000, "バラエティ", "ゲーム"),
            new ContentKindInfo(0x05020000, "バラエティ", "トークバラエティ"),
            new ContentKindInfo(0x05030000, "バラエティ", "お笑い・コメディ"),
            new ContentKindInfo(0x05040000, "バラエティ", "音楽バラエティ"),
            new ContentKindInfo(0x05050000, "バラエティ", "旅バラエティ"),
            new ContentKindInfo(0x05060000, "バラエティ", "料理バラエティ"),
            new ContentKindInfo(0x050F0000, "バラエティ", "その他"),

            new ContentKindInfo(0x06FF0000, "映画", ""),
            new ContentKindInfo(0x06000000, "映画", "洋画"),
            new ContentKindInfo(0x06010000, "映画", "邦画"),
            new ContentKindInfo(0x06020000, "映画", "アニメ"),
            new ContentKindInfo(0x060F0000, "映画", "その他"),

            new ContentKindInfo(0x07FF0000, "アニメ／特撮", ""),
            new ContentKindInfo(0x07000000, "アニメ／特撮", "国内アニメ"),
            new ContentKindInfo(0x07010000, "アニメ／特撮", "海外アニメ"),
            new ContentKindInfo(0x07020000, "アニメ／特撮", "特撮"),
            new ContentKindInfo(0x070F0000, "アニメ／特撮", "その他"),

            new ContentKindInfo(0x08FF0000, "ドキュメンタリー／教養", ""),
            new ContentKindInfo(0x08000000, "ドキュメンタリー／教養", "社会・時事"),
            new ContentKindInfo(0x08010000, "ドキュメンタリー／教養", "歴史・紀行"),
            new ContentKindInfo(0x08020000, "ドキュメンタリー／教養", "自然・動物・環境"),
            new ContentKindInfo(0x08030000, "ドキュメンタリー／教養", "宇宙・科学・医学"),
            new ContentKindInfo(0x08040000, "ドキュメンタリー／教養", "カルチャー・伝統文化"),
            new ContentKindInfo(0x08050000, "ドキュメンタリー／教養", "文学・文芸"),
            new ContentKindInfo(0x08060000, "ドキュメンタリー／教養", "スポーツ"),
            new ContentKindInfo(0x08070000, "ドキュメンタリー／教養", "ドキュメンタリー全般"),
            new ContentKindInfo(0x08080000, "ドキュメンタリー／教養", "インタビュー・討論"),
            new ContentKindInfo(0x080F0000, "ドキュメンタリー／教養", "その他"),

            new ContentKindInfo(0x09FF0000, "劇場／公演", ""),
            new ContentKindInfo(0x09000000, "劇場／公演", "現代劇・新劇"),
            new ContentKindInfo(0x09010000, "劇場／公演", "ミュージカル"),
            new ContentKindInfo(0x09020000, "劇場／公演", "ダンス・バレエ"),
            new ContentKindInfo(0x09030000, "劇場／公演", "落語・演芸"),
            new ContentKindInfo(0x09040000, "劇場／公演", "歌舞伎・古典"),
            new ContentKindInfo(0x090F0000, "劇場／公演", "その他"),

            new ContentKindInfo(0x0AFF0000, "趣味／教育", ""),
            new ContentKindInfo(0x0A000000, "趣味／教育", "旅・釣り・アウトドア"),
            new ContentKindInfo(0x0A010000, "趣味／教育", "園芸・ペット・手芸"),
            new ContentKindInfo(0x0A020000, "趣味／教育", "音楽・美術・工芸"),
            new ContentKindInfo(0x0A030000, "趣味／教育", "囲碁・将棋"),
            new ContentKindInfo(0x0A040000, "趣味／教育", "麻雀・パチンコ"),
            new ContentKindInfo(0x0A050000, "趣味／教育", "車・オートバイ"),
            new ContentKindInfo(0x0A060000, "趣味／教育", "コンピュータ・ＴＶゲーム"),
            new ContentKindInfo(0x0A070000, "趣味／教育", "会話・語学"),
            new ContentKindInfo(0x0A080000, "趣味／教育", "幼児・小学生"),
            new ContentKindInfo(0x0A090000, "趣味／教育", "中学生・高校生"),
            new ContentKindInfo(0x0A0A0000, "趣味／教育", "大学生・受験"),
            new ContentKindInfo(0x0A0B0000, "趣味／教育", "生涯教育・資格"),
            new ContentKindInfo(0x0A0C0000, "趣味／教育", "教育問題"),
            new ContentKindInfo(0x0A0F0000, "趣味／教育", "その他"),

            new ContentKindInfo(0x0BFF0000, "福祉", ""),
            new ContentKindInfo(0x0B000000, "福祉", "高齢者"),
            new ContentKindInfo(0x0B010000, "福祉", "障害者"),
            new ContentKindInfo(0x0B020000, "福祉", "社会福祉"),
            new ContentKindInfo(0x0B030000, "福祉", "ボランティア"),
            new ContentKindInfo(0x0B040000, "福祉", "手話"),
            new ContentKindInfo(0x0B050000, "福祉", "文字(字幕)"),
            new ContentKindInfo(0x0B060000, "福祉", "音声解説"),
            new ContentKindInfo(0x0B0F0000, "福祉", "その他"),

            new ContentKindInfo(0x0E0100FF, "スポーツ(CS)", ""),
            new ContentKindInfo(0x0E010000, "スポーツ(CS)", "テニス"),
            new ContentKindInfo(0x0E010001, "スポーツ(CS)", "バスケットボール"),
            new ContentKindInfo(0x0E010002, "スポーツ(CS)", "ラグビー"),
            new ContentKindInfo(0x0E010003, "スポーツ(CS)", "アメリカンフットボール"),
            new ContentKindInfo(0x0E010004, "スポーツ(CS)", "ボクシング"),
            new ContentKindInfo(0x0E010005, "スポーツ(CS)", "プロレス"),
            new ContentKindInfo(0x0E01000F, "スポーツ(CS)", "その他"),

            new ContentKindInfo(0x0E0101FF, "洋画(CS)", ""),
            new ContentKindInfo(0x0E010100, "洋画(CS)", "アクション"),
            new ContentKindInfo(0x0E010101, "洋画(CS)", "SF／ファンタジー"),
            new ContentKindInfo(0x0E010102, "洋画(CS)", "コメディー"),
            new ContentKindInfo(0x0E010103, "洋画(CS)", "サスペンス／ミステリー"),
            new ContentKindInfo(0x0E010104, "洋画(CS)", "恋愛／ロマンス"),
            new ContentKindInfo(0x0E010105, "洋画(CS)", "ホラー／スリラー"),
            new ContentKindInfo(0x0E010106, "洋画(CS)", "ウエスタン"),
            new ContentKindInfo(0x0E010107, "洋画(CS)", "ドラマ／社会派ドラマ"),
            new ContentKindInfo(0x0E010108, "洋画(CS)", "アニメーション"),
            new ContentKindInfo(0x0E010109, "洋画(CS)", "ドキュメンタリー"),
            new ContentKindInfo(0x0E01010A, "洋画(CS)", "アドベンチャー／冒険"),
            new ContentKindInfo(0x0E01010B, "洋画(CS)", "ミュージカル／音楽映画"),
            new ContentKindInfo(0x0E01010C, "洋画(CS)", "ホームドラマ"),
            new ContentKindInfo(0x0E01010F, "洋画(CS)", "その他"),

            new ContentKindInfo(0x0E0102FF, "邦画(CS)", ""),
            new ContentKindInfo(0x0E010200, "邦画(CS)", "アクション"),
            new ContentKindInfo(0x0E010201, "邦画(CS)", "SF／ファンタジー"),
            new ContentKindInfo(0x0E010202, "邦画(CS)", "お笑い／コメディー"),
            new ContentKindInfo(0x0E010203, "邦画(CS)", "サスペンス／ミステリー"),
            new ContentKindInfo(0x0E010204, "邦画(CS)", "恋愛／ロマンス"),
            new ContentKindInfo(0x0E010205, "邦画(CS)", "ホラー／スリラー"),
            new ContentKindInfo(0x0E010206, "邦画(CS)", "青春／学園／アイドル"),
            new ContentKindInfo(0x0E010207, "邦画(CS)", "任侠／時代劇"),
            new ContentKindInfo(0x0E010208, "邦画(CS)", "アニメーション"),
            new ContentKindInfo(0x0E010209, "邦画(CS)", "ドキュメンタリー"),
            new ContentKindInfo(0x0E01020A, "邦画(CS)", "アドベンチャー／冒険"),
            new ContentKindInfo(0x0E01020B, "邦画(CS)", "ミュージカル／音楽映画"),
            new ContentKindInfo(0x0E01020C, "邦画(CS)", "ホームドラマ"),
            new ContentKindInfo(0x0E01020F, "邦画(CS)", "その他"),

            new ContentKindInfo(0x0E0103FF, "その他(CS)", ""),
            new ContentKindInfo(0x0E010300, "その他(CS)", "アダルト"),
            new ContentKindInfo(0x0E010301, "その他(CS)", "海外放送"),
            new ContentKindInfo(0x0E01030F, "その他(CS)", "その他"),

            new ContentKindInfo(0x0FFF0000, "その他", ""),
            new ContentKindInfo(0x0F0F0000, "その他", "その他"),

            new ContentKindInfo(0xFFFF0000, "ジャンル情報なし", ""),

            new ContentKindInfo(0x0E0000FF, "編成情報", "編成に関する情報あり"),
            new ContentKindInfo(0x0E000000, "編成情報", "中止の可能性あり"),
            new ContentKindInfo(0x0E000001, "編成情報", "延長の可能性あり"),
            new ContentKindInfo(0x0E000002, "編成情報", "中断の可能性あり"),
            new ContentKindInfo(0x0E000003, "編成情報", "別話数放送の可能性あり"),
            new ContentKindInfo(0x0E000004, "編成情報", "編成未定枠"),
            new ContentKindInfo(0x0E000005, "編成情報", "繰り上げの可能性あり"),
            new ContentKindInfo(0x0E00000F, "編成情報", "その他の編成情報"),

            new ContentKindInfo(0x0E0001FF, "中断情報", "中断に関する情報あり"),
            new ContentKindInfo(0x0E000100, "中断情報", "中断ニュースあり"),
            new ContentKindInfo(0x0E000101, "中断情報", "臨時サービスあり"),
            new ContentKindInfo(0x0E00010F, "中断情報", "その他の中断情報"),

            new ContentKindInfo(0x0E0002FF, "3D映像", "3D映像に関する情報あり"),
            new ContentKindInfo(0x0E000200, "3D映像", "3D映像あり"),
            new ContentKindInfo(0x0E00020F, "3D映像", "その他の3D映像情報"),

            new ContentKindInfo(0x0E000FFF, "その他付属情報", "その他付属情報あり"),
            new ContentKindInfo(0x0E000F0F, "その他付属情報", "その他"),

            new ContentKindInfo(0xFEFF0000, "不明な情報(番組表でのみ有効)", ""),
        }; 
        public static readonly Dictionary<uint, ContentKindInfo> ContentKindDictionary = ContentKindList.ToDictionary(info => info.Data.Key, info => info);
        public static readonly Dictionary<ushort, string> ComponentKindDictionary = new Dictionary<ushort, string>()
        {
            { 0x0101, "480i(525i)、アスペクト比4:3" },
            { 0x0102, "480i(525i)、アスペクト比16:9 パンベクトルあり" },
            { 0x0103, "480i(525i)、アスペクト比16:9 パンベクトルなし" },
            { 0x0104, "480i(525i)、アスペクト比 > 16:9" },
            { 0x0191, "2160p、アスペクト比4:3" },
            { 0x0192, "2160p、アスペクト比16:9 パンベクトルあり" },
            { 0x0193, "2160p、アスペクト比16:9 パンベクトルなし" },
            { 0x0194, "2160p、アスペクト比 > 16:9" },
            { 0x01A1, "480p(525p)、アスペクト比4:3" },
            { 0x01A2, "480p(525p)、アスペクト比16:9 パンベクトルあり" },
            { 0x01A3, "480p(525p)、アスペクト比16:9 パンベクトルなし" },
            { 0x01A4, "480p(525p)、アスペクト比 > 16:9" },
            { 0x01B1, "1080i(1125i)、アスペクト比4:3" },
            { 0x01B2, "1080i(1125i)、アスペクト比16:9 パンベクトルあり" },
            { 0x01B3, "1080i(1125i)、アスペクト比16:9 パンベクトルなし" },
            { 0x01B4, "1080i(1125i)、アスペクト比 > 16:9" },
            { 0x01C1, "720p(750p)、アスペクト比4:3" },
            { 0x01C2, "720p(750p)、アスペクト比16:9 パンベクトルあり" },
            { 0x01C3, "720p(750p)、アスペクト比16:9 パンベクトルなし" },
            { 0x01C4, "720p(750p)、アスペクト比 > 16:9" },
            { 0x01D1, "240p アスペクト比4:3" },
            { 0x01D2, "240p アスペクト比16:9 パンベクトルあり" },
            { 0x01D3, "240p アスペクト比16:9 パンベクトルなし" },
            { 0x01D4, "240p アスペクト比 > 16:9" },
            { 0x01E1, "1080p(1125p)、アスペクト比4:3" },
            { 0x01E2, "1080p(1125p)、アスペクト比16:9 パンベクトルあり" },
            { 0x01E3, "1080p(1125p)、アスペクト比16:9 パンベクトルなし" },
            { 0x01E4, "1080p(1125p)、アスペクト比 > 16:9" },
            { 0x0201, "1/0モード（シングルモノ）" },
            { 0x0202, "1/0＋1/0モード（デュアルモノ）" },
            { 0x0203, "2/0モード（ステレオ）" },
            { 0x0204, "2/1モード" },
            { 0x0205, "3/0モード" },
            { 0x0206, "2/2モード" },
            { 0x0207, "3/1モード" },
            { 0x0208, "3/2モード" },
            { 0x0209, "3/2＋LFEモード（3/2.1モード）" },
            { 0x020A, "3/3.1モード" },
            { 0x020B, "2/0/0-2/0/2-0.1モード" },
            { 0x020C, "5/2.1モード" },
            { 0x020D, "3/2/2.1モード" },
            { 0x020E, "2/0/0-3/0/2-0.1モード" },
            { 0x020F, "0/2/0-3/0/2-0.1モード" },
            { 0x0210, "2/0/0-3/2/3-0.2モード" },
            { 0x0211, "3/3/3-5/2/3-3/0/0.2モード" },
            { 0x0240, "視覚障害者用音声解説" },
            { 0x0241, "聴覚障害者用音声" },
            { 0x0501, "H.264|MPEG-4 AVC、480i(525i)、アスペクト比4:3" },
            { 0x0502, "H.264|MPEG-4 AVC、480i(525i)、アスペクト比16:9 パンベクトルあり" },
            { 0x0503, "H.264|MPEG-4 AVC、480i(525i)、アスペクト比16:9 パンベクトルなし" },
            { 0x0504, "H.264|MPEG-4 AVC、480i(525i)、アスペクト比 > 16:9" },
            { 0x0591, "H.264|MPEG-4 AVC、2160p、アスペクト比4:3" },
            { 0x0592, "H.264|MPEG-4 AVC、2160p、アスペクト比16:9 パンベクトルあり" },
            { 0x0593, "H.264|MPEG-4 AVC、2160p、アスペクト比16:9 パンベクトルなし" },
            { 0x0594, "H.264|MPEG-4 AVC、2160p、アスペクト比 > 16:9" },
            { 0x05A1, "H.264|MPEG-4 AVC、480p(525p)、アスペクト比4:3" },
            { 0x05A2, "H.264|MPEG-4 AVC、480p(525p)、アスペクト比16:9 パンベクトルあり" },
            { 0x05A3, "H.264|MPEG-4 AVC、480p(525p)、アスペクト比16:9 パンベクトルなし" },
            { 0x05A4, "H.264|MPEG-4 AVC、480p(525p)、アスペクト比 > 16:9" },
            { 0x05B1, "H.264|MPEG-4 AVC、1080i(1125i)、アスペクト比4:3" },
            { 0x05B2, "H.264|MPEG-4 AVC、1080i(1125i)、アスペクト比16:9 パンベクトルあり" },
            { 0x05B3, "H.264|MPEG-4 AVC、1080i(1125i)、アスペクト比16:9 パンベクトルなし" },
            { 0x05B4, "H.264|MPEG-4 AVC、1080i(1125i)、アスペクト比 > 16:9" },
            { 0x05C1, "H.264|MPEG-4 AVC、720p(750p)、アスペクト比4:3" },
            { 0x05C2, "H.264|MPEG-4 AVC、720p(750p)、アスペクト比16:9 パンベクトルあり" },
            { 0x05C3, "H.264|MPEG-4 AVC、720p(750p)、アスペクト比16:9 パンベクトルなし" },
            { 0x05C4, "H.264|MPEG-4 AVC、720p(750p)、アスペクト比 > 16:9" },
            { 0x05D1, "H.264|MPEG-4 AVC、240p アスペクト比4:3" },
            { 0x05D2, "H.264|MPEG-4 AVC、240p アスペクト比16:9 パンベクトルあり" },
            { 0x05D3, "H.264|MPEG-4 AVC、240p アスペクト比16:9 パンベクトルなし" },
            { 0x05D4, "H.264|MPEG-4 AVC、240p アスペクト比 > 16:9" },
            { 0x05E1, "H.264|MPEG-4 AVC、1080p(1125p)、アスペクト比4:3" },
            { 0x05E2, "H.264|MPEG-4 AVC、1080p(1125p)、アスペクト比16:9 パンベクトルあり" },
            { 0x05E3, "H.264|MPEG-4 AVC、1080p(1125p)、アスペクト比16:9 パンベクトルなし" },
            { 0x05E4, "H.264|MPEG-4 AVC、1080p(1125p)、アスペクト比 > 16:9" }
        };
        public static readonly string[] ServiceTypeList = Enumerable.Repeat("不明", 256).ToArray();

        static CommonManager()
        {
            ServiceTypeList[0x00] = "";
            ServiceTypeList[0x01] = "デジタルTVサービス";
            ServiceTypeList[0x02] = "デジタル音声サービス";
            ServiceTypeList[0xA1] = "臨時映像サービス";
            ServiceTypeList[0xA2] = "臨時音声サービス";
            ServiceTypeList[0xA3] = "臨時データサービス";
            ServiceTypeList[0xA4] = "エンジニアリングサービス";
            ServiceTypeList[0xA5] = "プロモーション映像サービス";
            ServiceTypeList[0xA6] = "プロモーション音声サービス";
            ServiceTypeList[0xA7] = "プロモーションデータサービス";
            ServiceTypeList[0xA8] = "事前蓄積用データサービス";
            ServiceTypeList[0xA9] = "蓄積専用データサービス";
            ServiceTypeList[0xAA] = "ブックマーク一覧データサービス";
            ServiceTypeList[0xAB] = "サーバー型サイマルサービス";
            ServiceTypeList[0xAC] = "独立ファイルサービス";
            ServiceTypeList[0xAD] = "超高精細度4K専用TVサービス";
            ServiceTypeList[0xC0] = "データサービス";
            ServiceTypeList[0xC1] = "TLVを用いた蓄積型サービス";
            ServiceTypeList[0xC2] = "マルチメディアサービス";
            ServiceTypeList[0xFF] = "無効";
        }

        public static IEnumerable<int> CustomHourList
        {
            get { return Enumerable.Range(0, Settings.Instance.LaterTimeUse == false ? 24 : 37); }
        }

        public static CtrlCmdUtil CreateSrvCtrl()
        {
            var cmd = new CtrlCmdUtil();
            if (Instance.NWMode)
            {
                cmd.SetSendMode(true);
                cmd.SetNWSetting(Instance.NWConnectedIP, Instance.NWConnectedPort);
            }
            return cmd;
        }

        //ChKeyを16ビットに圧縮し、EpgTimerの起動中保持し続ける。
        private static Dictionary<ulong, ushort> chKey64to16Dic = new Dictionary<ulong, ushort>();
        public static ushort Create16Key(ulong key64)
        {
            ushort Key16;
            if (chKey64to16Dic.TryGetValue(key64, out Key16) == false)
            {
                Key16 = (ushort)chKey64to16Dic.Count;
                chKey64to16Dic[key64] = Key16;
            }
            return Key16;
        }
        public static ulong Reverse64Key(ulong key64)
        {
            ushort Key16 = (ushort)(key64 >> 16);
            return chKey64to16Dic.FirstOrDefault(item => item.Value == Key16).Key;
        }
        public static ulong Create64Key(ushort ONID, ushort TSID, ushort SID)
        {
            return ((ulong)ONID) << 32 | ((ulong)TSID) << 16 | (ulong)SID;
        }
        public static ulong Create64PgKey(ushort ONID, ushort TSID, ushort SID, ushort EventID)
        {
            return ((ulong)ONID) << 48 | ((ulong)TSID) << 32 | ((ulong)SID) << 16 | (ulong)EventID;
        }
        public static ulong CurrentPgUID(ulong key64Pg, DateTime startTime)
        {
            return (ulong)(startTime.Ticks) & 0xFFFFFF0000000000 //分解能約1日
                | ((uint)Create16Key(key64Pg >> 16)) << 16 | (ushort)key64Pg;
        }

        public static string Convert64PGKeyString(ulong Key, string separator = "\r\n", bool chDisplay = true)
        {
            return Convert64KeyString(Key >> 16, separator, chDisplay) + separator
                + ConvertEpgIDString("EventID", Key);
        }
        public static string Convert64KeyString(ulong Key, string separator = "\r\n", bool chDisplay = true)
        {
            int chnum = chDisplay ? ChSet5.ChNumber(Key) : 0;
            return string.Join(separator, ConvertEpgIDString("OriginalNetworkID", Key >> 32),
                            ConvertEpgIDString("TransportStreamID", Key >> 16),
                            ConvertEpgIDString("ServiceID", Key) + (chnum == 0 ? "" : " [" + chnum + "ch]"));
        }
        private static string ConvertEpgIDString(string Title, ulong id)
        {
            return string.Format("{0} : {1} (0x{1:X4})", Title, (ushort)id);
        }

        public static Dictionary<char, List<KeyValuePair<string, string>>> GetReplaceDictionaryNormal(EpgSetting set = null)
        {
            set = set ?? Settings.Instance.EpgSettingList[0];
            return set.ShareEpgReplacePatternTitle == true ? GetReplaceDictionaryTitle(set) :
                                     CreateReplaceDictionary(set.EpgReplacePattern, set.EpgReplacePatternDef);
        }
        public static Dictionary<char, List<KeyValuePair<string, string>>> GetReplaceDictionaryTitle(EpgSetting set = null)
        {
            set = set ?? Settings.Instance.EpgSettingList[0];
            return CreateReplaceDictionary(set.EpgReplacePatternTitle, set.EpgReplacePatternTitleDef);
        }
        private static Dictionary<char, List<KeyValuePair<string, string>>> CreateReplaceDictionary(string pattern, bool useDefDic)
        {
            var ret = CreateReplaceDictionary(pattern);
            if (useDefDic == true) MergeReplaceDictionary(ret, ReplaceUrlDictionary);
            return ret.Count != 0 ? ret : null;
        }
        private static void MergeReplaceDictionary(Dictionary<char, List<KeyValuePair<string, string>>> primary, Dictionary<char, List<KeyValuePair<string, string>>> secondary)
        {
            foreach (var item in secondary)
            {
                List<KeyValuePair<string, string>> bucket;
                primary.TryGetValue(item.Key, out bucket);
                primary[item.Key] = bucket == null ? item.Value.ToList() :
                                    bucket.Concat(item.Value).GroupBy(kvp => kvp.Key) //同一キーはprimary優先
                                          .Select(gr => gr.First()).OrderByDescending(kvp => kvp.Key.Length).ToList();
            }
        }

        public static Dictionary<char, List<KeyValuePair<string, string>>> CreateReplaceDictionary(string pattern)
        {
            var ret = new Dictionary<char, List<KeyValuePair<string, string>>>();
            if (pattern.Length > 0)
            {
                string[] arr = pattern.Substring(1).Split(pattern[0]);
                for (int i = 0; i + 1 < arr.Length; i += 2)
                {
                    //先頭文字で仕分けする
                    if (arr[i].Length > 0)
                    {
                        List<KeyValuePair<string, string>> bucket;
                        if (ret.TryGetValue(arr[i][0], out bucket) == false)
                        {
                            ret[arr[i][0]] = bucket = new List<KeyValuePair<string, string>>();
                        }
                        bucket.Add(new KeyValuePair<string, string>(arr[i], arr[i + 1]));
                    }
                }
                foreach (var bucket in ret)
                {
                    //最長一致のため
                    bucket.Value.Sort((a, b) => b.Key.Length - a.Key.Length);
                }
            }
            return ret;
        }

        public static string ReplaceText(string text, Dictionary<char, List<KeyValuePair<string, string>>> replaceDictionary)
        {
            if (replaceDictionary == null || string.IsNullOrEmpty(text) == true) return text;

            StringBuilder ret = null;
            if (replaceDictionary.Count > 0)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    List<KeyValuePair<string, string>> bucket;
                    if (replaceDictionary.TryGetValue(text[i], out bucket))
                    {
                        int j = bucket.FindIndex(p => string.CompareOrdinal(text, i, p.Key, 0, p.Key.Length) == 0);
                        if (j >= 0)
                        {
                            if (ret == null)
                            {
                                ret = new StringBuilder(text, 0, i, text.Length);
                            }
                            ret.Append(bucket[j].Value);
                            i += bucket[j].Key.Length - 1;
                            continue;
                        }
                    }
                    if (ret != null)
                    {
                        ret.Append(text[i]);
                    }
                }
            }
            return ret != null ? ret.ToString() : text;
        }

        public static string AdjustSearchText(string s)
        {
            return ReplaceUrl(s).Replace("\r\n", "").ToLower();
        }
        public static string ReplaceUrl(string s)
        {
            return ReplaceText(s, ReplaceUrlDictionary);
        }

        /// <summary>良くある通信エラー(CMD_ERR_CONNECT,CMD_ERR_TIMEOUT)をMessageBoxで表示する。</summary>
        public static bool CmdErrMsgTypical(ErrCode err, string caption = "通信エラー", string msg_other = null)
        {
            if (err == ErrCode.CMD_SUCCESS) return true;
            CommonUtil.DispatcherMsgBoxShow(GetErrCodeText(err) ?? msg_other ?? "EpgTimerSrvとの通信中にエラーが発生しました。", caption);
            return false;
        }

        public static string GetErrCodeText(ErrCode err)
        {
            switch (err)
            {
                case ErrCode.CMD_NON_SUPPORT:
                    return "EpgTimerSrvがサポートしていないコマンドです。";
                case ErrCode.CMD_ERR_CONNECT:
                    return "EpgTimerSrvに接続できませんでした。";
                case ErrCode.CMD_ERR_DISCONNECT:
                    return "EpgTimerSrvとの接続がリセットされた可能性があります。";
                case ErrCode.CMD_ERR_TIMEOUT:
                    return "EpgTimerSrvとの接続にタイムアウトしました。";
                case ErrCode.CMD_ERR_BUSY:
                    //このエラーはコマンドによって解釈が異なる
                    return null;
                default:
                    return null;
            }
        }

        public static string ConvertTimeText(EpgEventInfo info)
        {
            if (info.StartTimeFlag == 0) return "未定 ～ 未定";
            //
            string reftxt = ConvertTimeText(info.start_time, info.PgDurationSecond, false, false, false, false);
            return info.DurationFlag != 0 ? reftxt : reftxt.Split(new char[] { '～' })[0] + "～ 未定";
        }
        public static string ConvertTimeText(EpgSearchDateInfo info)
        {
            //超手抜き。書式が変ったら、巻き込まれて死ぬ。
            var start = new DateTime(2000, 1, 2 + info.startDayOfWeek, info.startHour, info.startMin, 0);
            var end = new DateTime(2000, 1, 2 + info.endDayOfWeek, info.endHour, info.endMin, 0);
            if (end < start) end = end.AddDays(7);
            string reftxt = ConvertTimeText(start, (uint)(end - start).TotalSeconds, true, true, false, false);
            string[] src = reftxt.Split(new char[] { ' ', '～' });
            return src[0].Substring(6, 1) + " " + src[1] + " ～ " + src[2].Substring(6, 1) + " " + src[3];
        }
        public static string ConvertTimeText(DateTime start, uint duration, bool isNoYear, bool isNoSecond, bool isNoEndDay = true, bool isNoStartDay = false, bool isNoEnd = false)
        {
            if (isNoEnd) return ConvertTimeText(start, isNoYear, isNoSecond, isNoStartDay);

            DateTime end = start + TimeSpan.FromSeconds(duration);

            if (Settings.Instance.LaterTimeUse == true)
            {
                bool over1Day = duration >= 24 * 60 * 60;
                bool? isStartLate = (isNoEndDay == false && over1Day == true) ? (bool?)false : null;
                bool isEndLate = (isNoEndDay == false || isNoStartDay == true && over1Day == false
                    ? over1Day == false && DateTime28.JudgeLateHour(end, start)
                    : DateTime28.JudgeLateHour(end, start, 99));
                DateTime28 ref_start = (isEndLate == true && isNoEndDay == true && isNoStartDay == false) ? new DateTime28(start) : null;

                return ConvertTimeText(start, isNoYear, isNoSecond, isNoStartDay, isStartLate)
                    + (isNoSecond == true ? "～" : " ～ ")
                    + ConvertTimeText(end, isNoYear, isNoSecond, isNoEndDay, isEndLate, ref_start);
            }
            else
            {
                return ConvertTimeText(start, isNoYear, isNoSecond, isNoStartDay)
                + (isNoSecond == true ? "～" : " ～ ")
                + ConvertTimeText(end, isNoYear, isNoSecond, isNoEndDay);
            }
        }
        public static string ConvertTimeText(DateTime time, bool isNoYear, bool isNoSecond, bool isNoDay = false, bool? isUse28 = null, DateTime28 ref_start = null)
        {
            if (Settings.Instance.LaterTimeUse == true)
            {
                var time28 = new DateTime28(time, isUse28, ref_start);
                return (isNoDay == true ? "" : time28.DateTimeMod.ToString((isNoYear == true ? "MM/dd(ddd) " : "yyyy/MM/dd(ddd) ")))
                + time28.HourMod.ToString("00:") + time.ToString(isNoSecond == true ? "mm" : "mm:ss");
            }
            else
            {
                return time.ToString((isNoDay == true ? "" :
                (isNoYear == true ? "MM/dd(ddd) " : "yyyy/MM/dd(ddd) ")) + (isNoSecond == true ? "HH:mm" : "HH:mm:ss"));
            }
        }
        public static string ConvertTimeTextForProgramText(bool timeFlag, DateTime time, bool durationFlag, double durationSec)
        {
            DateTime _time = timeFlag ? time : DateTime.MinValue;
            double _durationSec = durationFlag ? durationSec : double.MinValue;
            if (_time == DateTime.MinValue)
            {
                return "未定";
            }
            string endText = double.IsNaN(_durationSec) ? "" : _durationSec == double.MinValue ? " ～ 未定" : null;
            if (endText == null)
            {
                DateTime endTime = _time.AddSeconds(_durationSec);
                //秒が0でない場合は秒を付加
                endText = endTime.ToString("～" + "HH\\:mm" + (endTime.Second != 0 ? "\\:ss" : ""), CultureInfo.InvariantCulture);
            }
            return _time.ToString("yyyy\\/MM\\/dd(" + "日月火水木金土"[(int)_time.DayOfWeek] + ") HH\\:mm" + (_time.Second != 0 ? "\\:ss" : ""),
                                  CultureInfo.InvariantCulture) + endText;
        }
        public static string ConvertDurationText(uint duration, bool isNoSecond)
        {
            return (duration / 3600).ToString() 
                + ((duration % 3600) / 60).ToString(":00") 
                + (isNoSecond == true ? "" : (duration % 60).ToString(":00"));
        }

        public static string ConvertResModeText(ReserveMode? mode)
        {
            switch (mode)
            {
                case ReserveMode.KeywordAuto: return "キーワード予約";
                case ReserveMode.ManualAuto : return "プログラム自動予約";
                case ReserveMode.EPG        : return "個別予約(EPG)";
                case ReserveMode.Program    : return "個別予約(プログラム)";
                default                     : return "";
            }
        }

        public static void Save_ProgramText(EpgEventInfo info, string filename = "")
        {
            Save_ProgramText(ConvertProgramText(info, EventInfoTextMode.AllForProgramText), string.IsNullOrEmpty(filename) ? info.DataTitle : filename);
        }
        private static string saveProgramTextDirectory;
        public static void Save_ProgramText(string text, string filename = "")
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();

            var filebase = Path.GetFileName(filename);
            if (string.IsNullOrWhiteSpace(filebase)) filebase = "a";
            filename = filebase + ".program.txt";

            if (saveProgramTextDirectory != null)
            {
                dlg.InitialDirectory = saveProgramTextDirectory;
                for (int i = 1; File.Exists(Path.Combine(saveProgramTextDirectory, filename)); i++)
                {
                    filename = filebase + "(" + i + ").program.txt";
                }
            }
            dlg.DefaultExt = ".txt";
            dlg.FileName = filename;
            dlg.Filter = "txt Files|*.txt|all Files|*.*";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    using (var file = new StreamWriter(dlg.FileName, false, Encoding.UTF8))
                    {
                        file.Write(text);
                    }
                    saveProgramTextDirectory = Path.GetDirectoryName(dlg.FileName);
                }
                catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            }
        }

        public static string ConvertProgramText(EpgEventInfo eventInfo, EventInfoTextMode textMode = EventInfoTextMode.All)
        {
            if (eventInfo == null) return "";

            var ConvertChInfoText = new Func<EpgEventInfo, string>(info =>
            {
                if (string.IsNullOrEmpty(info.ServiceName) == false)
                {
                    return info.ServiceName + "(" + info.NetworkName + ")";
                }
                else
                {
                    return Convert64KeyString(eventInfo.Create64Key(), " ", false);
                }
            });

            string retText = "";

            //基本情報
            if (textMode == EventInfoTextMode.BasicInfo || textMode == EventInfoTextMode.All)
            {
                retText += ConvertChInfoText(eventInfo) + "\r\n";
                retText += ConvertTimeText(eventInfo) + "\r\n";
                retText += eventInfo.ShortInfo != null ? eventInfo.ShortInfo.event_name : "";
                retText = retText.TrimEnd() + "\r\n\r\n";
            }
            //説明(番組表のツールチップなど)
            if (textMode == EventInfoTextMode.BasicText || textMode == EventInfoTextMode.All)
            {
                if (eventInfo.ShortInfo != null && !string.IsNullOrEmpty(eventInfo.ShortInfo.text_char))
                {
                    retText += eventInfo.ShortInfo.text_char.TrimEnd() + "\r\n\r\n";
                }
            }
            //詳細情報
            if (textMode == EventInfoTextMode.ExtendedText || textMode == EventInfoTextMode.All)
            {
                if(eventInfo.ExtInfo != null && !string.IsNullOrEmpty(eventInfo.ExtInfo.text_char))
                {
                    retText += eventInfo.ExtInfo.text_char.TrimEnd() + "\r\n\r\n\r\n";
                }
            }
            //EpgTimerSrvの番組情報と同じ形式、基本情報～詳細情報まで
            if (textMode == EventInfoTextMode.AllForProgramText)
            {
                //EpgTimerSrvの番組情報の形式では先頭行が時刻になる
                retText += ConvertTimeTextForProgramText(eventInfo.StartTimeFlag != 0, eventInfo.start_time,
                                            eventInfo.DurationFlag != 0, eventInfo.durationSec) + "\r\n";
                retText += eventInfo.ServiceName + "\r\n";

                string eventName = (eventInfo.ShortInfo != null ? eventInfo.ShortInfo.event_name : "") + "\r\n\r\n";
                //空行を防ぐ
                retText += Regex.Replace(Regex.Replace(eventName, @"^(?:\r\n)+", "") + "\r\n", @"(?:\r\n){2,}", "\r\n") + "\r\n";
                
                var shortInfo = (eventInfo.ShortInfo != null ? eventInfo.ShortInfo.text_char : "") + "\r\n\r\n";
                //空行を防ぐ
                retText += Regex.Replace(Regex.Replace(shortInfo, @"^(?:\r\n)+", "") + "\r\n", @"(?:\r\n){2,}", "\r\n") + "\r\n";

                if (eventInfo.ExtInfo != null)
                {
                    var extTxt = eventInfo.ExtInfo.text_char + "\r\n\r\n";
                    //空行2行を防ぐ
                    retText += "詳細情報\r\n" + Regex.Replace(Regex.Replace(extTxt, @"^(?:\r\n)+", "") + "\r\n\r\n", @"(?:\r\n){3,}", "\r\n\r\n") + "\r\n";
                }
            }
            if (textMode == EventInfoTextMode.PropertyInfo || textMode == EventInfoTextMode.All || textMode == EventInfoTextMode.AllForProgramText)
            {
                var extInfo = "";
                var contentList = new List<ContentKindInfo>();
                if (eventInfo.ContentInfo != null)
                {
                    extInfo += "ジャンル : \r\n";
                    contentList = eventInfo.ContentInfo.nibbleList.Select(data => ContentKindInfoForDisplay(data)).ToList();
                    foreach (ContentKindInfo info in contentList.Where(info => info.Data.IsAttributeInfo == false))
                    {
                        extInfo += info.ListBoxView + "\r\n";
                    }
                    extInfo += "\r\n";
                }

                if (eventInfo.ComponentInfo != null)
                {
                    extInfo += "映像 : ";
                    int streamContent = eventInfo.ComponentInfo.stream_content;
                    int componentType = eventInfo.ComponentInfo.component_type;
                    string name;
                    extInfo += ComponentKindDictionary.TryGetValue((ushort)(streamContent << 8 | componentType), out name) ? name :
                                   "(0x" + streamContent.ToString("X2") + ",0x" + componentType.ToString("X2") + ")";
                    extInfo += "\r\n";
                    //空行を防ぐ
                    string textChar = Regex.Replace(Regex.Replace(eventInfo.ComponentInfo.text_char, @"^(?:\r\n)+", "") + "\r\n", @"(?:\r\n){2,}", "\r\n");
                    if (textChar.Length > 2)
                    {
                        extInfo += textChar;
                    }
                }

                if (eventInfo.AudioInfo != null && eventInfo.AudioInfo.componentList.Count > 0)
                {
                    extInfo += "音声 : ";
                    foreach (EpgAudioComponentInfoData info in eventInfo.AudioInfo.componentList)
                    {
                        int streamContent = info.stream_content;
                        int componentType = info.component_type;
                        string name;
                        extInfo += ComponentKindDictionary.TryGetValue((ushort)(streamContent << 8 | componentType), out name) ? name :
                                       "(0x" + streamContent.ToString("X2") + ",0x" + componentType.ToString("X2") + ")";
                        extInfo += "\r\n";
                        //空行を防ぐ
                        string textChar = Regex.Replace(Regex.Replace(info.text_char, @"^(?:\r\n)+", "") + "\r\n", @"(?:\r\n){2,}", "\r\n");
                        if (textChar.Length > 2)
                        {
                            extInfo += textChar;
                        }
                        extInfo += "サンプリングレート : ";
                        switch (info.sampling_rate)
                        {
                            case 1:
                                extInfo += "16kHz";
                                break;
                            case 2:
                                extInfo += "22.05kHz";
                                break;
                            case 3:
                                extInfo += "24kHz";
                                break;
                            case 5:
                                extInfo += "32kHz";
                                break;
                            case 6:
                                extInfo += "44.1kHz";
                                break;
                            case 7:
                                extInfo += "48kHz";
                                break;
                            default:
                                extInfo += "(0x" + info.sampling_rate.ToString("X2") + ")";
                                break;
                        }
                        extInfo += "\r\n";
                    }
                }
                extInfo += "\r\n";

                //スクランブル
                if (!ChSet5.IsDttv(eventInfo.original_network_id))
                {
                    extInfo += (eventInfo.FreeCAFlag == 0 ? "無料放送" : "有料放送") + "\r\n\r\n";
                }

                //イベントリレー
                if (eventInfo.EventRelayInfo != null && eventInfo.EventRelayInfo.eventDataList.Count > 0)
                {
                    extInfo += "イベントリレーあり : " + (textMode != EventInfoTextMode.AllForProgramText ? "\r\n" : "");
                    foreach (EpgEventData info in eventInfo.EventRelayInfo.eventDataList)
                    {
                        if (textMode != EventInfoTextMode.AllForProgramText)
                        {
                            //Epgデータが無いときや過去番組は探せない場合がある
                            ulong key = CurrentPgUID(info.Create64PgKey(), eventInfo.PgStartTime == DateTime.MaxValue ? DateTime.MaxValue : eventInfo.PgStartTime.AddSeconds(eventInfo.PgDurationSecond));
                            var relayInfo = MenuUtil.GetPgInfoUidAll(key) ?? new EpgEventInfo { original_network_id = info.original_network_id, transport_stream_id = info.transport_stream_id, service_id = info.service_id };
                            extInfo += "→ " + ConvertChInfoText(relayInfo) + ConvertEpgIDString("  EventID", info.event_id) + " " + relayInfo.DataTitle;
                        }
                        else
                        {
                            extInfo += "ID:" + info.original_network_id + "(0x" + info.original_network_id.ToString("X4") + ")-" +
                                        info.transport_stream_id + "(0x" + info.transport_stream_id.ToString("X4") + ")-" +
                                        info.service_id + "(0x" + info.service_id.ToString("X4") + ")-" +
                                        info.event_id + "(0x" + info.event_id.ToString("X4") + ")";
                            EpgServiceInfo sInfo;
                            if (ChSet5.ChList.TryGetValue(info.Create64Key(), out sInfo))
                            {
                                extInfo += " " + sInfo.service_name;
                            }
                        }
                        extInfo += "\r\n";
                    }
                    extInfo += "\r\n";
                }

                if (textMode != EventInfoTextMode.AllForProgramText)
                {
                    //その他情報(番組特性コード関係)
                    var attStr = string.Join("\r\n", contentList.Where(info => info.Data.IsAttributeInfo == true).Select(info => info.SubName));
                    if (attStr != "") extInfo += attStr + "\r\n\r\n";

                    extInfo += Convert64PGKeyString(eventInfo.Create64PgKey()) + "\r\n\r\n";

                    List<ReserveData> list = eventInfo.GetReserveListFromPgUID();
                    if (list.Any()) extInfo += "予約中/ID : " + string.Join(", ", list.Select(info => string.Format("{0} (0x{0:X4})", info.ReserveID)));

                    retText += extInfo.TrimEnd() + "\r\n";
                }
                else
                {
                    //コロン(:)の前後などにスペースが入らない
                    extInfo += "OriginalNetworkID:" + eventInfo.original_network_id + "(0x" + eventInfo.original_network_id.ToString("X4") + ")\r\n" +
                               "TransportStreamID:" + eventInfo.transport_stream_id + "(0x" + eventInfo.transport_stream_id.ToString("X4") + ")\r\n" +
                               "ServiceID:" + eventInfo.service_id + "(0x" + eventInfo.service_id.ToString("X4") + ")\r\n" +
                               "EventID:" + eventInfo.event_id + "(0x" + eventInfo.event_id.ToString("X4") + ")\r\n";
                    retText += extInfo;
                }
            }
            return retText;
        }

        //主にジャンル項目の設定リスト表示用
        public static ContentKindInfo ContentKindInfoForDisplay(EpgContentData data)
        {
            ContentKindInfo info;
            if (ContentKindDictionary.TryGetValue(data.Key, out info) == true)
            { return info; }

            //不明なカテゴリの表示用ContentKindInfo作成
            string idString = data.IsUserNibble ? string.Format("(0x{0:X8})", data.Key) : string.Format("(0x{0:X4})", data.Key >> 16);
            info = new ContentKindInfo(data.Key, "不明" + (data.IsCategory ? idString : ""), "不明" + (data.IsCategory ? "" : idString));

            if (ContentKindDictionary.ContainsKey(data.CategoryKey) == true)
            {
                info.ContentName = ContentKindDictionary[data.CategoryKey].ContentName;
            }
            return info;
        }

        //主にリストビューの表示用
        public static string ConvertJyanruText(EpgEventInfo eventInfo)
        {
            if (eventInfo == null || eventInfo.ContentInfo == null) return "";
            //
            return ConvertJyanruText(eventInfo.ContentInfo.nibbleList, true);
        }
        public static string ConvertJyanruText(CustomEpgTabInfo info)
        {
            if (info == null) return "";
            //
            string retText = ConvertJyanruText(info.ViewContentList);
            return ((retText != "" && info.ViewNotContentFlag == true) ? "NOT " : "") + retText;
        }
        public static string ConvertJyanruText(EpgSearchKeyInfo searchKeyInfo)
        {
            if (searchKeyInfo == null) return "";
            //
            string retText = ConvertJyanruText(searchKeyInfo.contentList);
            return ((retText != "" && searchKeyInfo.notContetFlag == 1) ? "NOT " : "") + retText;
        }
        public static string ConvertJyanruText(IEnumerable<EpgContentData> nibbleList, bool noAttribute = false)
        {
            if (nibbleList == null) return "";
            //
            var retText = new List<string>();

            var infoList = nibbleList.Where(info => noAttribute == false || info.IsAttributeInfo == false).ToList();
            var knownList = infoList.Where(info => ContentKindDictionary.ContainsKey(info.Key) == true).ToList();
            foreach (var gr in knownList.Select(info => ContentKindDictionary[info.Key]).GroupBy(info => info.Data.CategoryKey))
            {
                var smallCategory1 = string.Join(",", gr.Where(info => info.Data.IsCategory == false)
                                                            .Select(info => info.SubName).Distinct());
                retText.Add("[" + gr.First().ContentName + (smallCategory1 == "" ? "" : (" - " + smallCategory1)) + "]");
            }
            if (infoList.Count != knownList.Count) retText.Add("[不明]");

            return string.Join(",", retText);
        }

        //主にリストビューの表示用
        public static string ConvertAttribText(EpgEventInfo eventInfo)
        {
            if (eventInfo == null || eventInfo.ContentInfo == null) return "";
            //
            var retText = new List<string>();

            if (eventInfo.EventRelayInfo != null && eventInfo.EventRelayInfo.eventDataList.Count != 0)
            {
                retText.Add("[イベントリレー]");
            }

            var infoList = eventInfo.ContentInfo.nibbleList.Where(info => info.IsAttributeInfo == true).ToList();
            var knownList = infoList.Where(info => ContentKindDictionary.ContainsKey(info.Key) == true).ToList();
            foreach (var gr in knownList.GroupBy(info => info.CategoryKey))
            {
                retText.Add("[" + ContentKindDictionary[gr.Key].ContentName + "]");
            }
            if (infoList.Count != knownList.Count) retText.Add("[不明な情報]");

            return string.Join(",", retText);
        }

        public static string ConvertNetworkNameText(ushort originalNetworkID, bool IsSimple = false)
        {
            if (ChSet5.IsDttv(originalNetworkID) == true)
            {
                return "地デジ";
            }
            else if (ChSet5.IsBS(originalNetworkID) == true)
            {
                return "BS";
            }
            else if (ChSet5.IsCS1(originalNetworkID) == true)
            {
                return IsSimple == true ? "CS" : "CS1";
            }
            else if (ChSet5.IsCS2(originalNetworkID) == true)
            {
                return IsSimple == true ? "CS" : "CS2";
            }
            else if (ChSet5.IsSPHD(originalNetworkID) == true)
            {
                return "スカパー";
            }
            else
            {
                return "その他";
            }
        }

        static string ConvertValueText(int val, string[] textList, string errText = "不明")
        {
            return 0 <= val && val < textList.Length ? textList[val] : errText;
        }

        public static string ConvertRecModeText(int val)
        {
            return ConvertValueText(val, RecModeList);
        }

        public static string ConvertRecEndModeText(int val)
        {
            return ConvertValueText(val, RecEndModeList);
        }

        public static string ConvertIsEnableText(bool val)
        {
            return ConvertValueText(val ? 1 : 0, IsEnableList);
        }

        public static string ConvertYesNoText(int val)
        {
            return ConvertValueText(val, YesNoList);
        }

        public static string ConvertPriorityText(int val)
        {
            return ConvertValueText(val - 1, PriorityList);
        }

        public static string ConvertTunerText(uint tunerID)
        {
            string tunerName = "";
            TunerReserveInfo info;
            if (Instance.DB.TunerReserveList.TryGetValue(tunerID, out info))
            {
                tunerName = info.tunerName;
            }
            else if (tunerID != 0)
            {
                tunerName = "不明なチューナー";
            }
            return new TunerSelectInfo(tunerName, tunerID).ToString();
        }

        public static string ConvertViewModeText(int viewMode)
        {
            return ConvertValueText(viewMode, new string[] { "標準モード", "1週間モード", "リスト表示モード" }, "");
        }

        public static string TrimHyphenSpace(string text)
        {
            //行ごとに処理
            for (int lineStart = 0, lineEnd; lineStart < text.Length; lineStart = lineEnd)
            {
                lineEnd = text.IndexOf('\n', lineStart);
                lineEnd = lineEnd < 0 ? text.Length : lineEnd + 1;
                if (lineEnd - lineStart >= 2 && text[lineStart] == '-')
                {
                    if (text[lineStart + 1] == ' ')
                    {
                        //"- "を取り除く
                        text = text.Remove(lineStart, 2);
                        lineEnd -= 2;
                    }
                    else
                    {
                        //"-- "などで始まるものは"-"を1つだけ減らす
                        for (lineStart++; lineStart < lineEnd && text[lineStart] == '-'; lineStart++) { }
                        if (lineStart < lineEnd && text[lineStart] == ' ')
                        {
                            text = text.Remove(lineStart - 1, 1);
                            lineEnd--;
                        }
                    }
                }
            }
            return text;
        }

        public static FlowDocument ConvertDisplayText(EpgEventInfo eventInfo)
        {
            string basicInfo = ConvertProgramText(eventInfo, EventInfoTextMode.BasicInfo) + ConvertProgramText(eventInfo, EventInfoTextMode.BasicText);
            string extText = ConvertProgramText(eventInfo, EventInfoTextMode.ExtendedText);
            string propertyInfo = ConvertProgramText(eventInfo, EventInfoTextMode.PropertyInfo);
            if (string.IsNullOrWhiteSpace(basicInfo)) basicInfo = "番組情報がありません。\r\n" + "またはEPGデータが読み込まれていません。\r\n";

            return ConvertDisplayText(basicInfo, extText, propertyInfo);
        }
        public static FlowDocument ConvertDisplayText(string basicInfo, string extText, string propertyInfo)
        {
            int plainStart = 0;
            var para = new Paragraph();
            string text = basicInfo + extText + propertyInfo;
            string rtext = ReplaceText(text, ReplaceUrlDictionary);
            if (rtext.Length == text.Length)
            {
                var reEscapedHyphenSpace = new Regex(@"\G--+ ");
                var reHyperlink = new Regex(@"(?:https?://|(?<![0-9A-Za-z%_/.-])[0-9A-Za-z%_.-]+\.(?:com|jp|tv)/)[0-9A-Za-z!#$%&'()~=@;:?_+*/.-]+");

                //行ごとに処理
                for (int lineStart = 0, lineEnd; lineStart < text.Length; lineStart = lineEnd)
                {
                    lineEnd = text.IndexOf('\n', lineStart);
                    lineEnd = lineEnd < 0 ? text.Length : lineEnd + 1;
                    //extTextの各行について"- "で始まるものは装飾する
                    if (lineStart >= basicInfo.Length &&
                        lineStart < basicInfo.Length + extText.Length &&
                        lineEnd - lineStart >= 2 && text[lineStart] == '-' && text[lineStart + 1] == ' ')
                    {
                        //非装飾
                        para.Inlines.Add(text.Substring(plainStart, lineStart - plainStart));
                        //非表示
                        var run = new Run("- ");
                        run.FontSize = 1;
                        run.Foreground = Brushes.Transparent;
                        para.Inlines.Add(run);
                        lineStart += 2;
                        //太字
                        para.Inlines.Add(new Bold(new Run(text.Substring(lineStart, lineEnd - lineStart))));
                        plainStart = lineEnd;
                    }
                    else
                    {
                        //extTextの各行について"-- "などで始まるものは"-"を1つだけ減らす
                        if (lineStart >= basicInfo.Length &&
                            lineStart < basicInfo.Length + extText.Length &&
                            reEscapedHyphenSpace.IsMatch(text, lineStart))
                        {
                            para.Inlines.Add(text.Substring(plainStart, lineStart - plainStart));
                            var run = new Run("-");
                            run.FontSize = 1;
                            run.Foreground = Brushes.Transparent;
                            para.Inlines.Add(run);
                            plainStart = ++lineStart;
                        }
                        //http(s)スキームで始まるか特定のTLDっぽい文字列を含むものを探す
                        for (Match m = reHyperlink.Match(rtext, lineStart, lineEnd - lineStart); m.Success; m = m.NextMatch())
                        {
                            para.Inlines.Add(text.Substring(plainStart, m.Index - plainStart));
                            plainStart = m.Index;
                            var h = new Hyperlink(new Run(text.Substring(m.Index, m.Length)));
                            h.MouseLeftButtonDown += (sender, e) =>
                            {
                                try
                                {
                                    using (Process.Start(new ProcessStartInfo(((Hyperlink)sender).NavigateUri.ToString()) { UseShellExecute = true })) { }
                                }
                                catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                            };
                            h.Foreground = SystemColors.HotTrackBrush;
                            h.Cursor = Cursors.Hand;
                            try
                            {
                                h.NavigateUri = new Uri((Regex.IsMatch(m.Value, "^https?://") ? "" : "https://") + m.Value);
                                para.Inlines.Add(h);
                                plainStart = m.Index + m.Length;
                            }
                            catch { }
                        }
                    }
                }
            }
            para.Inlines.Add(text.Substring(plainStart));
            return new FlowDocument(para);
        }

        //デフォルト番組表の情報作成
        public static List<CustomEpgTabInfo> CreateDefaultTabInfo()
        {
            //再表示の際の認識用に、負の仮番号を与えておく。
            List<ulong>[] spKeyList = EpgServiceInfo.SPKeyList.Select(key => key.IntoList()).ToArray();
            var setInfo = new[]
            {
                new CustomEpgTabInfo(){ID = -1, TabName = "地デジ", ViewServiceList = spKeyList[0]},
                new CustomEpgTabInfo(){ID = -2, TabName = "BS", ViewServiceList = spKeyList[1]},
                new CustomEpgTabInfo(){ID = -3, TabName = "CS", ViewServiceList = spKeyList[2]},
                new CustomEpgTabInfo(){ID = -4, TabName = "スカパー", ViewServiceList = spKeyList[3]},
                new CustomEpgTabInfo(){ID = -5, TabName = "その他", ViewServiceList = spKeyList[4]},
            };
            var check = new HashSet<int>(ChSet5.ChList.Values.Select(info => info.IsDttv ? -1 : info.IsBS ? -2 : info.IsCS ? -3 : info.IsSPHD ? -4 : -5));
            return setInfo.Where(info => check.Contains(info.ID)).ToList();
        }

        public static void GetFolderNameByDialog(TextBox txtBox, string Description = "", bool checkNWPath = false, string defaultPath = "", bool recFile = false)
        {
            GetPathByDialog(txtBox, checkNWPath, path => GetFolderNameByDialog(path, Description), defaultPath, recFile);
        }
        public static string GetFolderNameByDialog(string InitialPath = "", string Description = "")
        {
            try
            {
                if (Settings.Instance.OpenFolderWithFileDialog == true)
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog();
                    dlg.Title = Description;
                    dlg.CheckFileExists = false;
                    dlg.DereferenceLinks = false;
                    dlg.FileName = "(任意ファイル名)";
                    dlg.InitialDirectory = GetDirectoryName2(InitialPath);
                    if (dlg.ShowDialog() == true)
                    {
                        return GetDirectoryName2(dlg.FileName);
                    }
                }
                else
                {
                    var dlg = new System.Windows.Forms.FolderBrowserDialog();
                    dlg.Description = Description;
                    dlg.SelectedPath = GetDirectoryName2(InitialPath);
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        return dlg.SelectedPath;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        public static void GetFileNameByDialog(TextBox txtBox, bool isNameOnly, string Title = "", string DefaultExt = "", bool checkNWPath = false, string defaultPath = "", bool checkExist = true, bool recFile = false)
        {
            GetPathByDialog(txtBox, checkNWPath, path => GetFileNameByDialog(path, isNameOnly, Title, DefaultExt, checkExist), defaultPath, recFile);
        }
        public static string GetFileNameByDialog(string InitialPath = "", bool isNameOnly = false, string Title = "", string DefaultExt = "", bool checkExist = true)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.Title = Title;
                dlg.FileName = Path.GetFileName(InitialPath);
                dlg.InitialDirectory = GetDirectoryName2(InitialPath);
                dlg.CheckFileExists = checkExist;
                dlg.CheckPathExists = checkExist;
                switch (DefaultExt)
                {
                    case ".exe":
                        dlg.DefaultExt = ".exe";
                        dlg.Filter = "exe Files|*.exe|all Files|*.*";
                        break;
                    case ".bat":
                        dlg.DefaultExt = ".bat";
                        dlg.Filter = "batch Files(*.bat;*.ps1;*.lua)|*.bat;*.ps1;*.lua|all Files(*.*)|*.*";
                        break;
                    default:
                        dlg.Filter = "all Files|*.*";
                        break;
                }
                if (dlg.ShowDialog() == true)
                {
                    return isNameOnly == true ? dlg.SafeFileName : dlg.FileName;
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
            return null;
        }

        private static string GetDirectoryName2(string folder_path)
        {
            string path = SettingPath.CheckFolder(folder_path);
            while (path != "")
            {
                if (Directory.Exists(path)) break;
                path = Path.GetDirectoryName(path) ?? "";
            }
            return path;
        }

        //ネットワークパス対応のパス設定
        private static void GetPathByDialog(TextBox tbox, bool checkNWPath, Func<string, string> funcGetPathDialog, string defaultPath = "", bool recFile = false)
        {
            string path = SettingPath.CheckFolder(string.IsNullOrEmpty(tbox.Text) ? defaultPath : tbox.Text);

            string base_src = "";
            string base_nw = "";
            if (checkNWPath)
            {
                //NWモードのとき、可能ならサーバ側のローカルパスをUNCとして扱う。
                string path_src = path.TrimEnd('\\');
                string path_nw = GetRecPath(path_src, recFile).TrimEnd('\\');

                if (path_nw != "" && path_nw != path_src)
                {
                    IEnumerable<string> r_src = path_src.Split('\\').Reverse();
                    IEnumerable<string> r = path_nw.Split('\\').Reverse();
                    int length_match = -1;
                    foreach (var item in r.Zip(r_src, (p, ps) => new { nw = p, src = ps }))
                    {
                        if (item.nw != item.src) break;
                        length_match += item.nw.Length + 1;
                    }
                    length_match = Math.Max(0, length_match);
                    base_src = path_src.Substring(0, path_src.Length - length_match).TrimEnd('\\');
                    base_nw = path_nw.Substring(0, path_nw.Length - length_match).TrimEnd('\\');

                    if (base_nw != "")
                    {
                        path = path_nw;
                    }
                }
            }

            path = funcGetPathDialog(path);
            if (path != null && tbox.IsEnabled == true && tbox.IsReadOnly == false)
            {
                //他のドライブに変ったりしたときは何もしない
                //また、複数の共有フォルダ使ってる場合はとりあえず諦める。(サーバ側で要逆変換)
                if (base_nw != "" && path.StartsWith(base_nw, StringComparison.Ordinal) == true)
                {
                    path = path.Replace(base_nw, base_src);
                }
                tbox.Text = SettingPath.CheckFolder(path);
            }
        }

        public static string GetRecPath(string path, bool recFile)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (Settings.Instance.FilePathReplaceRecFile && recFile)
                {
                    path = ReplaceText(path, CreateReplaceDictionary(Settings.Instance.FilePathReplacePattern));
                }
                else if (Instance.NWMode && !path.StartsWith("\\\\", StringComparison.Ordinal))
                {
                    try { CreateSrvCtrl().SendGetRecFileNetworkPath(path, ref path); }
                    catch (Exception ex) { MessageBox.Show(ex.ToString()); }
                }
            }
            return path;
        }

    public static void OpenRecFolder(string folderPath, bool recFile = true)
        {
            try
            {
                string path1 = GetRecPath(folderPath, recFile);
                bool isFile = File.Exists(path1) == true;//録画結果から開く場合
                string path = isFile == true ? path1 : GetDirectoryName2(path1);//録画フォルダ未作成への対応
                bool noParent = path.TrimEnd('\\') != path1.TrimEnd('\\');//フォルダを遡った場合の特例

                if (string.IsNullOrWhiteSpace(path) == true)
                {
                    MessageBox.Show("パスが見つかりません。\r\n\r\n" + folderPath, "録画フォルダを開く", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    //オプションに応じて一つ上のフォルダから対象フォルダを選択した状態で開く。
                    string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                    string cmd = isFile == true || noParent == false && Settings.Instance.MenuSet.OpenParentFolder == true ? "/select," : "";
                    using (Process.Start(new ProcessStartInfo(explorer, cmd + "\"" + path + "\"") { UseShellExecute = false })) { }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        public void FilePlay(ReserveData data)
        {
            if (data == null || data.RecSetting == null || data.IsEnabled == false) return;
            if (data.IsOnRec() == false)
            {
                MessageBox.Show("まだ録画が開始されていません。", "追っかけ再生", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (data.RecSetting.RecMode == 4)//視聴モード
            {
                TVTestCtrl.SetLiveCh(data.OriginalNetworkID, data.TransportStreamID, data.ServiceID);
                return;
            }

            if (Settings.Instance.FilePlay && Settings.Instance.FilePlayOnAirWithExe)
            {
                //ファイルパスを取得するため開いてすぐ閉じる
                var info = new NWPlayTimeShiftInfo();
                try
                {
                    if (CreateSrvCtrl().SendNwTimeShiftOpen(data.ReserveID, ref info) == ErrCode.CMD_SUCCESS)
                    {
                        CreateSrvCtrl().SendNwPlayClose(info.ctrlID);
                        if (info.filePath != "")
                        {
                            FilePlay(info.filePath, false);
                            return;
                        }
                    }
                }
                catch { }
                MessageBox.Show("録画ファイルの場所がわかりませんでした。", "追っかけ再生", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TVTestCtrl.StartStreamingPlay(null, data.ReserveID);
            }
        }
        public void FilePlay(string filePath, bool recFile = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) == true) return;

                if (Settings.Instance.FilePlay == false)
                {
                    TVTestCtrl.StartStreamingPlay(filePath, 0);
                }
                else
                {
                    //録画フォルダと保存・共有フォルダが異なる場合($FileNameExt$運用など)で、
                    //コマンドラインの一部になるときは、ファイルの確認を未チェックとする。
                    string path = GetRecPath(filePath, recFile);
                    string playExe = Settings.Instance.FilePlayExe;
                    string cmdLine = string.IsNullOrWhiteSpace(Settings.Instance.FilePlayCmd) == true ? "$FilePath$" : Settings.Instance.FilePlayCmd;

                    if (File.Exists(path) == false)
                    {
                        if (cmdLine.Contains("$FilePath$") == true && cmdLine.Contains("$FileNameExt$") == false)
                        {
                            MessageBox.Show("録画ファイルが見つかりません。\r\n\r\n" + path, "録画ファイルの再生", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        path = filePath;
                    }

                    //再生アプリ指定でコマンドラインとなる場合に、ファイル名のみのときは""を補う
                    string mark = cmdLine.Trim() == "$FilePath$" ? "\"" : "";
                    //'$'->'\t'は再帰的な展開を防ぐため
                    cmdLine = cmdLine.Replace("$FileNameExt$", Path.GetFileName(path).Replace('$', '\t'));
                    cmdLine = cmdLine.Replace("$FilePath$", path).Replace('\t', '$');

                    if (string.IsNullOrWhiteSpace(playExe) == true)
                    {
                        cmdLine = cmdLine.Replace("\"", "");//必要無いのに両端に""が付与されている時は削除する
                        if (File.Exists(cmdLine) == false)
                        {
                            MessageBox.Show("録画ファイルが見つかりません。\r\n\r\n" + cmdLine, "録画ファイルの再生", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        using (Process.Start(new ProcessStartInfo(cmdLine) { UseShellExecute = true })) { }
                    }
                    else
                    {
                        if (File.Exists(playExe) == false)
                        {
                            MessageBox.Show("再生アプリが見つかりません。\r\n設定を確認してください。\r\n\r\n" + playExe, "録画ファイルの再生", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        using (Process.Start(new ProcessStartInfo(playExe, mark + cmdLine + mark) { UseShellExecute = false })) { }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        const int NotifyLogMaxLocal = 8192 * 2;
        public static void AddNotifyLog(NotifySrvInfo notifyInfo)
        {
            if (Instance.NotifyLogList.Count >= NotifyLogMaxLocal)
            {
                Instance.NotifyLogList.RemoveRange(0, NotifyLogMaxLocal / 2);
            }
            Instance.NotifyLogList.Add(notifyInfo);
            if (Settings.Instance.AutoSaveNotifyLog == 1)
            {
                string filePath = SettingPath.ModulePath + "\\Log";
                Directory.CreateDirectory(filePath);
                filePath += "\\EpgTimerNotify_" + CommonUtil.EdcbNow.ToString("yyyyMMdd") + ".log";
                using (var file = new StreamWriter(filePath, true, Encoding.Unicode))
                {
                    file.WriteLine(new NotifySrvInfoItem(notifyInfo));
                }
            }
        }

        public static void ShowPlugInSetting(string pName, string pFolder, Visual vis)
        {
            string dllPath = Path.Combine(SettingPath.ModulePath, pFolder ?? "", pName ?? "");
            CommonUtil.ShowPlugInSetting(dllPath, ((HwndSource)PresentationSource.FromVisual(vis)).Handle);
        }

        public static List<string> GetBonFileList()
        {
            var list = new List<string>();

            try
            {
                if (Instance.NWMode == false)
                {
                    foreach (string info in Directory.GetFiles(SettingPath.SettingFolderPath, "*.ChSet4.txt"))
                    {
                        list.Add(GetBonFileName(Path.GetFileName(info)) + ".dll");
                    }
                }
                else
                {
                    //EpgTimerが作成したEpgTimerSrv.iniからBonDriverセクションを拾い出す
                    //将来にわたって確実なリストアップではないし、本来ならSendEnumPlugIn()あたりを変更して取得すべきだが、
                    //参考表示なので構わない
                    using (var reader = (new StreamReader(SettingPath.TimerSrvIniPath, Encoding.GetEncoding(932))))
                    {
                        while (reader.Peek() >= 0)
                        {
                            string buff = reader.ReadLine();
                            int start = buff.IndexOf('[');
                            int end = buff.LastIndexOf(".dll]", StringComparison.OrdinalIgnoreCase);
                            if (start >= 0 && end >= start + 2)
                            {
                                int comment = buff.IndexOf(';');
                                if (comment >= 0 && comment < end) continue;
                                list.Add(buff.Substring(start + 1, end + 3 - start));
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        private static string GetBonFileName(string src)
        {
            int pos = src.LastIndexOf(')');
            if (pos < 1)
            {
                return src;
            }

            int count = 1;
            for (int i = pos - 1; i >= 0; i--)
            {
                if (src[i] == '(')
                {
                    count--;
                }
                else if (src[i] == ')')
                {
                    count++;
                }
                if (count == 0)
                {
                    return src.Substring(0, i);
                }
            }
            return src;
        }

        private static Process SrvSettingProcess = null;

        public static void OpenSrvSetting()
        {
            if (Instance.NWMode == true) return;

            try
            {
                if (SrvSettingProcess == null || SrvSettingProcess.HasExited)
                {
                    SrvSettingProcess = Process.Start(new ProcessStartInfo(Path.Combine(SettingPath.ModulePath, "EpgTimerSrv.exe"), "/setting") { UseShellExecute = false });
                }
                else
                {
                    CommonUtil.SetForegroundWindow(SrvSettingProcess.MainWindowHandle);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        //2回呼び出されるが、2回目の呼び出しはキャッシュヒットで落ちる。
        private static Dictionary<string, DateTime> wakeLog = null;
        public static void WakeUpHDDLogClear() { wakeLog = null; }
        public static void WakeUpHDDWork()
        {
            if (CommonManager.Instance.NWMode == true || Settings.Instance.WakeUpHdd == false)
            { return; }

            wakeLog = wakeLog ?? new Dictionary<string, DateTime>();

            //録画中の予約と、録画が始まる予約を抽出
            var now = CommonUtil.EdcbNowEpg;
            var start = now.AddMinutes(1 + Settings.Instance.RecAppWakeTime);
            var past = start.AddMinutes(-Settings.Instance.NoWakeUpHddMin);
            List<ReserveData> reslist = Instance.DB.ReserveList.Values.Where(info => info.RecSetting.IsEnable && info.RecSetting.RecMode < 4).ToList();
            List<ReserveData> onlist = reslist.Where(info => info.IsOnRec(now)).ToList();
            List<ReserveData> stlist = reslist.Where(info => info.OnTime(start) >= 0).Except(onlist).ToList();

            //録画フォルダの抽出メソッド
            string def1 = Settings.Instance.DefRecFolders[0];
            var cnv = new PathConverter(SettingPath.EdcbExePath);//今のところ同じフォルダにある前提だが、設定があるので変換しておく
            Func<List<ReserveData>, IEnumerable<IGrouping<string, string>>> RecFolders = list =>
            {
                var fs = new List<string>();
                foreach (RecSettingData s in list.Select(data => data.RecSetting))
                {
                    fs.AddRange(s.RecFolderList.Select(f => f.RecFolder));
                    if (s.PartialRecFlag != 0) fs.AddRange(s.PartialRecFolder.Select(f => f.RecFolder));
                    if (s.RecFolderList.Count == 0 || s.PartialRecFlag != 0 && s.PartialRecFolder.Count == 0) fs.Add(def1);
                }
                return fs.Select(f => cnv.Convert(f == "!Default" ? def1 : f))
                        .Except(new string[] { null }).GroupBy(f => f.ToLower());//日本語でもこれでいい
            };

            //保存情報の更新・追加
            wakeLog.Where(info => info.Value < past).ToList().ForEach(item => wakeLog.Remove(item.Key));
            foreach (var f in RecFolders(onlist)) wakeLog[f.Key] = now;//録画中のフォルダは常に回避
            List<IGrouping<string, string>> stFolders = RecFolders(stlist).Where(f => wakeLog.ContainsKey(f.Key) == false).ToList();

            //対象があるかどうかと複数書き出しのチェック
            if (stFolders.Count <= Settings.Instance.WakeUpHddOverlapNum) return;

            stFolders.ForEach(f =>
            {
                wakeLog[f.Key] = now;//見込みではなく確定情報で更新
                AddNotifyLog(new NotifySrvInfo { notifyID = (uint)UpdateNotifyItem.PreRecStart, time = now, param4 = string.Format("EpgTimer > Access to \"{0}\"", f.First()) });
            });

            //Writeを発生。バッチで投げちゃった方がいいかも？
            var folder = "\\EpgTimer_WakeUpHDD_" + now.ToString("yyyyMMddHHmmssfff");
            var flist = stFolders.Select(f => f.First().TrimEnd('\\') + folder).ToList();
            Task.Factory.StartNew(() => flist.ForEach(f => { try { Directory.Delete(Directory.CreateDirectory(f).FullName); } catch { } }));
        }
        class PathConverter
        {
            string refPath = null;
            string n(string s) { return string.IsNullOrEmpty(s) == true ? null : s; }
            string d(string s) { return s.TrimEnd('\\') + "\\"; }
            public PathConverter(string refFile = null)
            {
                try { refPath = Path.GetDirectoryName(Path.GetFullPath(n(refFile))); }
                catch { }
                refPath = d(refPath ?? SettingPath.ModulePath);
            }
            public string Convert(string folder)
            {
                folder = d(n(folder) ?? ".");
                try { return d(Path.GetFullPath(Path.IsPathRooted(folder) == true ? folder : refPath + Path.GetDirectoryName(folder))); }
                catch { return null; }
            }
        }
    }
}
