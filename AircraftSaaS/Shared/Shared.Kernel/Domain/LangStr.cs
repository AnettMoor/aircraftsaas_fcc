namespace Shared.Kernel.Domain;

public class LangStr : Dictionary<string, string>
{
    
    // look at appsettings.json LangStrDefaultCulture value
    public static string DefaultCulture { get; set; } = "en";

    // s["en"] = "foo";
    // var bar = s["en"];
    public new string this[string key]
    {
        get => base[key];
        set => base[key] = value;
    }

    public LangStr()
    {
    }

    public LangStr(string value) : this(value,
        string.IsNullOrEmpty(Thread.CurrentThread.CurrentUICulture.Name)
            ? DefaultCulture
            : Thread.CurrentThread.CurrentUICulture.Name)
    {
    }

    public LangStr(string value, string culture)
    {
        if (string.IsNullOrEmpty(culture)) culture = DefaultCulture;

        var neutralCulture = culture.Split('-')[0];
        this[neutralCulture] = value;
        
        // check for default culture also. if not set - do so
        if (!ContainsKey(DefaultCulture))
        {
            this[DefaultCulture] = value;
        }
    }
    
    // returns the value for currentculture
    public string? Translate(string? culture = null)
    {
        if (Count == 0) return null;
        culture = culture?.Trim() ?? Thread.CurrentThread.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(culture)) culture = DefaultCulture;

        if (ContainsKey(culture))
        {
            return this[culture];
        }

        var neutralCulture = culture.Split('-')[0];
        if (ContainsKey(neutralCulture))
        {
            return this[neutralCulture];
        }

        if (ContainsKey(DefaultCulture))
        {
            return this[DefaultCulture];
        }

        return null;
    }

    public void SetTranslation(string value, string? culture = null)
    {
        // call currenthread and find current culture
        culture = culture?.Trim() ?? Thread.CurrentThread.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(culture)) culture = DefaultCulture;
        var neutralCulture = culture.Split('-')[0];
        //look up the culture in dict and return the translation
        this[neutralCulture] = value;
    }

    // calls translate
    public override string ToString()
    {
        return Translate() ?? "????";
    }

    // string xxx = new LangStr("foo","et-EE"); xxx == "foo";
    public static implicit operator string(LangStr? langStr) => langStr?.ToString() ?? "null";

    // LangStr xxx = "foobar";
    public static implicit operator LangStr(string value) => new LangStr(value);
}
