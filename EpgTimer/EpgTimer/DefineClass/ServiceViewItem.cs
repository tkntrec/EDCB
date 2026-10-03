using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace EpgTimer
{
    public class ServiceViewItem : SelectableItemNWMode
    {
        public ServiceViewItem(EpgServiceInfo info) { ServiceInfo = info; }
        public ServiceViewItem(ulong key) { ServiceInfo = EpgServiceInfo.FromKey(key); }
        public readonly EpgServiceInfo ServiceInfo;
        public ulong Key
        { 
            get { return ServiceInfo.Key; }
        }
        public string NetworkName
        {
            get { return CommonManager.ConvertNetworkNameText(ServiceInfo.ONID, ServiceInfo.HasSPKey); }
        }
        public string ServiceName
        { 
            get { return ServiceInfo.service_name; }
        }
        public string ServiceType
        {
            get { return CommonManager.ServiceTypeList[ServiceInfo.service_type]; }
        }
        public string IsVideo
        {
            get { return ServiceInfo.IsVideo == true ? "○" : ""; }
        }
        public string IsPartial
        {
            get { return ServiceInfo.PartialFlag == true ? "○" : ""; }
        }
        public string CombineMark { get; set; }
        public TextBlock ToolTipView
        {
            get
            {
                if (Settings.Instance.NoToolTip == true) return null;
                //
                return ViewUtil.GetTooltipBlockStandard(ConvertInfoText());
            }
        }
        public string ConvertInfoText()
        {
            string ret = "ServiceName : " + ServiceName + "\r\n";
            if (ServiceInfo.HasSPKey)
                return ret + "※検索による絞り込みでは使用出来ません。\r\n"
                        + "　過去番組表で終了サービスを表示するには\r\n"
                        + "　設定画面[基本設定][EPG取得]にある\r\n"
                        + "　[EPG取得サービスのみ表示する]の\r\n"
                        + "　チェックをオフにしてください。";
            return ret + "ServiceType : " + ServiceType + " (0x" + ServiceInfo.service_type.ToString("X2") + ")" + "\r\n" +
                CommonManager.Convert64KeyString(Key) + "\r\n" +
                "PartialReception : " + (ServiceInfo.PartialFlag == true ? "ワンセグ" : "-") + " (0x" + (ServiceInfo.PartialFlag ? 1 : 0).ToString("X2") + ")";
        }
        public override string ToString() { return ServiceName; }

        //BoxExchangeEditor用。Equalsをoverrideすると他に影響あるのでIEqualityComparerを使用する。
        public static readonly ServiceViewItemComparer Comparator = new ServiceViewItemComparer();
        public class ServiceViewItemComparer : EqualityComparer<object>
        {
            public override bool Equals(object x, object y)
            {
                return x is ServiceViewItem && y is ServiceViewItem && (x as ServiceViewItem).Key == (y as ServiceViewItem).Key;
            }
            public override int GetHashCode(object obj)
            {
                return obj is ServiceViewItem == false ? 0 : (obj as ServiceViewItem).Key.GetHashCode();
            }
        }
    }
}
