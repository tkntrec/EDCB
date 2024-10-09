// Only ASCII characters can be used here.

namespace EpgTimer
{
    public partial class App : System.Windows.Application
    {
        internal const string VERSION_TEXT = (VERSION_TAG != "" ? " " + VERSION_TAG : "") + (VERSION_EXTRA != "" ? " " + VERSION_EXTRA : "");
        const string VERSION_TAG = "tkntrec-241010";
        const string VERSION_EXTRA = "";
    }
}
