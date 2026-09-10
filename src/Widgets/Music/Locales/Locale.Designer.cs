namespace Music.Locales;

[global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
public class Locale
{
    private static global::System.Resources.ResourceManager resourceMan;
    private static global::System.Globalization.CultureInfo resourceCulture;

    internal Locale() { }

    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (object.ReferenceEquals(resourceMan, null))
            {
                global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Music.Locales.Locale", typeof(Locale).Assembly);
                resourceMan = temp;
            }
            return resourceMan;
        }
    }

    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static global::System.Globalization.CultureInfo Culture
    {
        get => resourceCulture;
        set => resourceCulture = value;
    }

    public static string Music => ResourceManager.GetString("Music", resourceCulture);
    public static string Music_Title => ResourceManager.GetString("Music_Title", resourceCulture);
    public static string Music_Subtitle => ResourceManager.GetString("Music_Subtitle", resourceCulture);
    public static string Music_Play => ResourceManager.GetString("Music_Play", resourceCulture);
    public static string Music_Pause => ResourceManager.GetString("Music_Pause", resourceCulture);
    public static string Music_Next => ResourceManager.GetString("Music_Next", resourceCulture);
    public static string Music_Previous => ResourceManager.GetString("Music_Previous", resourceCulture);
    public static string Music_NotPlaying => ResourceManager.GetString("Music_NotPlaying", resourceCulture);
    public static string Music_Prompt => ResourceManager.GetString("Music_Prompt", resourceCulture);
}
