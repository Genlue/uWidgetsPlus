namespace Pomodoro.Locales {
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Pomodoro.Locales.Locale", typeof(Locale).Assembly);
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
        
        public static string Pomodoro => ResourceManager.GetString("Pomodoro", resourceCulture) ?? "番茄钟";
        public static string Pomodoro_Title => ResourceManager.GetString("Pomodoro_Title", resourceCulture) ?? "番茄钟";
        public static string Pomodoro_Subtitle => ResourceManager.GetString("Pomodoro_Subtitle", resourceCulture) ?? "极简高效的番茄工作法计时器与专注看板";
        public static string Pomodoro_Phase_Focus => ResourceManager.GetString("Pomodoro_Phase_Focus", resourceCulture) ?? "专注";
        public static string Pomodoro_Phase_ShortBreak => ResourceManager.GetString("Pomodoro_Phase_ShortBreak", resourceCulture) ?? "短休息";
        public static string Pomodoro_Phase_LongBreak => ResourceManager.GetString("Pomodoro_Phase_LongBreak", resourceCulture) ?? "长休息";
        public static string Pomodoro_Start => ResourceManager.GetString("Pomodoro_Start", resourceCulture) ?? "开始";
        public static string Pomodoro_Pause => ResourceManager.GetString("Pomodoro_Pause", resourceCulture) ?? "暂停";
        public static string Pomodoro_Reset => ResourceManager.GetString("Pomodoro_Reset", resourceCulture) ?? "重置";
        public static string Pomodoro_Skip => ResourceManager.GetString("Pomodoro_Skip", resourceCulture) ?? "跳过";
        public static string Pomodoro_FocusMinutes => ResourceManager.GetString("Pomodoro_FocusMinutes", resourceCulture) ?? "专注时长 (分钟)";
        public static string Pomodoro_ShortBreakMinutes => ResourceManager.GetString("Pomodoro_ShortBreakMinutes", resourceCulture) ?? "短休息时长 (分钟)";
        public static string Pomodoro_LongBreakMinutes => ResourceManager.GetString("Pomodoro_LongBreakMinutes", resourceCulture) ?? "长休息时长 (分钟)";
        public static string Pomodoro_LongBreakInterval => ResourceManager.GetString("Pomodoro_LongBreakInterval", resourceCulture) ?? "长休息间隔 (周期)";
        public static string Pomodoro_AutoStartBreaks => ResourceManager.GetString("Pomodoro_AutoStartBreaks", resourceCulture) ?? "自动开始休息";
        public static string Pomodoro_AutoStartFocus => ResourceManager.GetString("Pomodoro_AutoStartFocus", resourceCulture) ?? "自动开始下一个专注";
    }
}
