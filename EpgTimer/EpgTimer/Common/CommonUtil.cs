using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Linq;
using System.Linq.Expressions;
using System.Windows.Media;
using System.IO;
using System.Reflection;

namespace EpgTimer
{
    public static class CommonUtil
    {
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        static extern uint GetTickCount();

        // Struct we'll need to pass to the function
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
        
        public static int GetIdleTimeSec()
        {
            // The number of ticks that passed since last input
            uint IdleTicks = 0;

            // Set the struct
            LASTINPUTINFO LastInputInfo = new LASTINPUTINFO();
            LastInputInfo.cbSize = (uint)Marshal.SizeOf(LastInputInfo);
            LastInputInfo.dwTime = 0;

            // If we have a value from the function
            if (GetLastInputInfo(ref LastInputInfo))
            {
                // Number of idle ticks = system uptime ticks - number of ticks at last input
                IdleTicks = unchecked(GetTickCount() - LastInputInfo.dwTime);
            }
            return (int)(IdleTicks / 1000);
        }

        public static T Max<T>(params T[] args) { return args.Max(); }
        public static T Min<T>(params T[] args) { return args.Min(); }

        public static int NumBits(long bits)
        {
            bits = (bits & 0x55555555) + (bits >> 1 & 0x55555555);
            bits = (bits & 0x33333333) + (bits >> 2 & 0x33333333);
            bits = (bits & 0x0f0f0f0f) + (bits >> 4 & 0x0f0f0f0f);
            bits = (bits & 0x00ff00ff) + (bits >> 8 & 0x00ff00ff);
            return (int)((bits & 0x0000ffff) + (bits >> 16 & 0x0000ffff));
        }

        /// <summary>ショートカットの作成</summary>
        /// <remarks>WSHを使用して、ショートカット(lnkファイル)を作成します。(遅延バインディング)</remarks>
        /// <param name="path">出力先のファイル名(*.lnk)</param>
        /// <param name="targetPath">対象のアセンブリ(*.exe)</param>
        /// <param name="description">説明</param>
        public static void CreateShortCut(String path, String targetPath, String description)
        {
            // WSHオブジェクトを作成し、CreateShortcutメソッドを実行する
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortCut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });

            Type shortcutType = shell.GetType();
            // TargetPathプロパティをセットする
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortCut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortCut, new object[] { Path.GetDirectoryName(targetPath) });
            // Descriptionプロパティをセットする
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortCut, new object[] { description });
            // Saveメソッドを実行する
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortCut, null);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32")]
        static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        delegate void Setting(IntPtr parentWnd);

        public static bool ShowPlugInSetting(string dllFilePath, IntPtr parentWnd)
        {
            IntPtr module = LoadLibrary(dllFilePath);
            if (module != IntPtr.Zero)
            {
                try
                {
                    IntPtr func = GetProcAddress(module, "Setting");
                    if (func != IntPtr.Zero)
                    {
                        Setting settingDelegate = (Setting)Marshal.GetDelegateForFunctionPointer(func, typeof(Setting));
                        settingDelegate(parentWnd);
                        return true;
                    }
                }
                finally
                {
                    FreeLibrary(module);
                }
            }
            return false;
        }

        /// <summary>メンバ名を返す。</summary>
        public static string NameOf<T>(Expression<Func<T>> e)
        {
            var member = (MemberExpression)e.Body;
            return member.Member.Name;
        }

        /// <summary>リストに入れて返す。(return new List&lt;T&gt; { item })</summary>
        public static List<T> IntoList<T>(this T item)
        {
            return new List<T> { item };
        }

        /// <summary>非同期のメッセージボックスを表示</summary>
        public static void DispatcherMsgBoxShow(string message, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => MessageBox.Show(message, caption, button, icon)));
        }

        /// <summary>ウィンドウがあれば取得する</summary>
        public static Window GetTopWindow(Visual obj)
        {
            if (obj == null) return null;
            var topWindow = PresentationSource.FromVisual(obj);
            return topWindow == null ? null : topWindow.RootVisual as Window;
        }

        /// <summary>文字数を制限し、超える場合は省略記号を付与する。pos:省略記号の位置(負数指定で中央)</summary>
        public static string LimitLenString(string s, int max_len, int pos = int.MaxValue, string tag = "...")
        {
            if (string.IsNullOrEmpty(s) == false && s.Length > max_len)
            {
                max_len = Math.Max(max_len, 0);
                tag = tag ?? "";
                tag = tag.Substring(0, Math.Min(max_len, tag.Length));
                int sel_len = Math.Max(0, max_len - tag.Length);
                pos = pos < 0 ? sel_len / 2 : pos > sel_len ? sel_len : pos;
                s = s.Substring(0, pos) + tag + s.Substring(s.Length - (sel_len - pos));
            }
            return s;
        }
    }
}
