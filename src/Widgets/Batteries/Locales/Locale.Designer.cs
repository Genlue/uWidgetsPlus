namespace Batteries.Locales {
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Batteries.Locales.Locale", typeof(Locale).Assembly);
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
        
        public static string Batteries => ResourceManager.GetString("Batteries", resourceCulture) ?? "电池";
        public static string Batteries_Title => ResourceManager.GetString("Batteries_Title", resourceCulture) ?? "电池与外设";
        public static string Batteries_Subtitle => ResourceManager.GetString("Batteries_Subtitle", resourceCulture) ?? "macOS 风格的主机电池与蓝牙外设电量监控看板";
        public static string Batteries_MainDevice => ResourceManager.GetString("Batteries_MainDevice", resourceCulture) ?? "电脑电池";
        public static string Batteries_AcPower => ResourceManager.GetString("Batteries_AcPower", resourceCulture) ?? "电源适配器供电";
        public static string Batteries_Charging => ResourceManager.GetString("Batteries_Charging", resourceCulture) ?? "正在充电";
        public static string Batteries_ShowPercentage => ResourceManager.GetString("Batteries_ShowPercentage", resourceCulture) ?? "显示百分比数字";
        public static string Batteries_ShowPeripherals => ResourceManager.GetString("Batteries_ShowPeripherals", resourceCulture) ?? "显示蓝牙外设电量";
        public static string Batteries_ShowRemainingTime => ResourceManager.GetString("Batteries_ShowRemainingTime", resourceCulture) ?? "显示预计可用时间";
        public static string Batteries_Mouse => ResourceManager.GetString("Batteries_Mouse", resourceCulture) ?? "鼠标";
        public static string Batteries_Keyboard => ResourceManager.GetString("Batteries_Keyboard", resourceCulture) ?? "键盘";
        public static string Batteries_Headphones => ResourceManager.GetString("Batteries_Headphones", resourceCulture) ?? "耳机";
    }
}
