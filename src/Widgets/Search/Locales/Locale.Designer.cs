namespace Search.Locales {
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Search.Locales.Locale", typeof(Locale).Assembly);
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
        
        public static string Search => ResourceManager.GetString("Search", resourceCulture) ?? "Search";
        public static string Search_Title => ResourceManager.GetString("Search_Title", resourceCulture) ?? "Search";
        public static string Search_Subtitle => ResourceManager.GetString("Search_Subtitle", resourceCulture) ?? "Quick spotlight and customizable search bar";
        public static string Search_Placeholder => ResourceManager.GetString("Search_Placeholder", resourceCulture) ?? "Search anything...";
        public static string Search_Button => ResourceManager.GetString("Search_Button", resourceCulture) ?? "Search";
        public static string Search_Clear => ResourceManager.GetString("Search_Clear", resourceCulture) ?? "Clear";
        public static string Search_Engine => ResourceManager.GetString("Search_Engine", resourceCulture) ?? "Search Engine";
        public static string Search_Settings => ResourceManager.GetString("Search_Settings", resourceCulture) ?? "Search Settings";
        public static string Search_Manage_Engines => ResourceManager.GetString("Search_Manage_Engines", resourceCulture) ?? "Manage Search Engines";
        public static string Search_Add_Engine => ResourceManager.GetString("Search_Add_Engine", resourceCulture) ?? "Add Engine";
        public static string Search_Edit_Engine => ResourceManager.GetString("Search_Edit_Engine", resourceCulture) ?? "Edit Engine";
        public static string Search_Engine_Name => ResourceManager.GetString("Search_Engine_Name", resourceCulture) ?? "Name";
        public static string Search_Engine_Url => ResourceManager.GetString("Search_Engine_Url", resourceCulture) ?? "URL Template ({q} for keyword)";
        public static string Search_Engine_Category => ResourceManager.GetString("Search_Engine_Category", resourceCulture) ?? "Category";
        public static string Search_Default => ResourceManager.GetString("Search_Default", resourceCulture) ?? "Default";
        public static string Search_Restore_Defaults => ResourceManager.GetString("Search_Restore_Defaults", resourceCulture) ?? "Restore Presets";
        public static string Search_Clear_On_Search => ResourceManager.GetString("Search_Clear_On_Search", resourceCulture) ?? "Clear input after search";
        public static string Search_Save_History => ResourceManager.GetString("Search_Save_History", resourceCulture) ?? "Record recent search history";
        public static string Search_Clear_History => ResourceManager.GetString("Search_Clear_History", resourceCulture) ?? "Clear History";
        public static string Search_History => ResourceManager.GetString("Search_History", resourceCulture) ?? "Recent Searches";
        public static string Search_Quick_Engines => ResourceManager.GetString("Search_Quick_Engines", resourceCulture) ?? "Quick Engines";
        public static string Search_Category_All => ResourceManager.GetString("Search_Category_All", resourceCulture) ?? "All";
        public static string Search_Category_AI => ResourceManager.GetString("Search_Category_AI", resourceCulture) ?? "AI";
        public static string Search_Category_Dev => ResourceManager.GetString("Search_Category_Dev", resourceCulture) ?? "Dev";
        public static string Search_Category_Media => ResourceManager.GetString("Search_Category_Media", resourceCulture) ?? "Media";
        public static string Search_Category_Community => ResourceManager.GetString("Search_Category_Community", resourceCulture) ?? "Community";
    }
}
