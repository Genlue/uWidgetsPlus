namespace Map.Locales {
    using System;
    
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Locale {
        
        private static global::System.Resources.ResourceManager resourceMan;
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Locale() {
        }
        
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Map.Locales.Locale", typeof(Locale).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        public static string Map => ResourceManager.GetString("Map", resourceCulture) ?? "地图";
        public static string Map_Title => ResourceManager.GetString("Map_Title", resourceCulture) ?? "地图";
        public static string Map_Subtitle => ResourceManager.GetString("Map_Subtitle", resourceCulture) ?? "macOS / iPad 风格多图源桌面地图小组件";
        public static string Map_Provider => ResourceManager.GetString("Map_Provider", resourceCulture) ?? "地图图源";
        public static string Map_PresetLocations => ResourceManager.GetString("Map_PresetLocations", resourceCulture) ?? "预设城市";
        public static string Map_LocationName => ResourceManager.GetString("Map_LocationName", resourceCulture) ?? "地点名称";
        public static string Map_LocationDescription => ResourceManager.GetString("Map_LocationDescription", resourceCulture) ?? "详细地址 / 描述";
        public static string Map_Latitude => ResourceManager.GetString("Map_Latitude", resourceCulture) ?? "纬度";
        public static string Map_Longitude => ResourceManager.GetString("Map_Longitude", resourceCulture) ?? "经度";
        public static string Map_Zoom => ResourceManager.GetString("Map_Zoom", resourceCulture) ?? "默认缩放级别";
        public static string Map_ShowPin => ResourceManager.GetString("Map_ShowPin", resourceCulture) ?? "显示中心大头针";
        public static string Map_ShowInfoPill => ResourceManager.GetString("Map_ShowInfoPill", resourceCulture) ?? "显示浮动信息卡";
        public static string Map_ShowControls => ResourceManager.GetString("Map_ShowControls", resourceCulture) ?? "显示悬浮控制按钮";
        public static string Map_ShowScaleBar => ResourceManager.GetString("Map_ShowScaleBar", resourceCulture) ?? "显示比例尺";
        public static string Map_AllowDrag => ResourceManager.GetString("Map_AllowDrag", resourceCulture) ?? "允许鼠标拖拽平移";
        public static string Map_BaiduKey => ResourceManager.GetString("Map_BaiduKey", resourceCulture) ?? "百度地图 AK (可选)";
        public static string Map_RecenterTooltip => ResourceManager.GetString("Map_RecenterTooltip", resourceCulture) ?? "复位至预设中心";
        public static string Map_ZoomInTooltip => ResourceManager.GetString("Map_ZoomInTooltip", resourceCulture) ?? "放大";
        public static string Map_ZoomOutTooltip => ResourceManager.GetString("Map_ZoomOutTooltip", resourceCulture) ?? "缩小";
        public static string Map_LayerTooltip => ResourceManager.GetString("Map_LayerTooltip", resourceCulture) ?? "切换图源";
    }
}
