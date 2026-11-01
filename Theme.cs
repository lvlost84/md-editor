using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

static class Theme
{
    static readonly string[] Keys = { "Bg", "Paper", "Ink", "InkSoft", "Accent", "AccentSoft", "Border", "BorderStrong", "Amber" };
    static readonly string[] Light = { "#f4f4f0", "#ffffff", "#182233", "#5b6472", "#2b5f8c", "#e7eff6", "#d9dad3", "#b9bcb2", "#b6742b" };
    static readonly string[] Dark = { "#0f2a44", "#123a5e", "#eaf3fb", "#9fbdd6", "#7ecbe8", "#1d456b", "#2c5480", "#3e6c9c", "#e0a458" };
    public static bool IsDark;

    const string StylesXaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style x:Key='TbBtn' TargetType='Button'>
 <Setter Property='Background' Value='{DynamicResource PaperBrush}'/>
 <Setter Property='Foreground' Value='{DynamicResource InkBrush}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource BorderStrongBrush}'/>
 <Setter Property='BorderThickness' Value='1'/>
 <Setter Property='Padding' Value='9,4'/>
 <Setter Property='Margin' Value='0,0,4,0'/>
 <Setter Property='Cursor' Value='Hand'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
  <Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='3' Padding='{TemplateBinding Padding}'>
   <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource AccentSoftBrush}'/><Setter Property='Foreground' Value='{DynamicResource InkBrush}'/></Trigger>
  </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='PrimaryBtn' TargetType='Button' BasedOn='{StaticResource TbBtn}'>
 <Setter Property='Background' Value='{DynamicResource AccentBrush}'/>
 <Setter Property='Foreground' Value='{DynamicResource BgBrush}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource AccentBrush}'/>
 <Setter Property='FontWeight' Value='SemiBold'/>
</Style>
</ResourceDictionary>";

    public static void Install()
    {
        var res = Application.Current.Resources;
        if (res.Contains("TbBtn")) return;
        var d = (ResourceDictionary)XamlReader.Parse(StylesXaml);
        res.MergedDictionaries.Add(d);
        Apply(IsDark);
    }

    public static void Apply(bool dark)
    {
        IsDark = dark;
        var res = Application.Current.Resources;
        var set = dark ? Dark : Light;
        for (int i = 0; i < Keys.Length; i++)
            res[Keys[i] + "Brush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(set[i]));
    }

    public static void Bind(FrameworkElement e, DependencyProperty p, string key) { e.SetResourceReference(p, key + "Brush"); }
}

class Settings
{
    public bool Dark;
    public double Width = 1440, Height = 900;

    public static string PathName { get { return Path.Combine(Log.Dir, "settings.json"); } }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(PathName))
            {
                var s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(PathName, Encoding.UTF8));
                if (s != null) return s;
            }
        }
        catch (Exception ex) { Log.Error("settings load", ex); }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Log.Dir);
            string tmp = PathName + ".tmp";
            File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
            if (File.Exists(PathName)) File.Replace(tmp, PathName, null); else File.Move(tmp, PathName);
        }
        catch (Exception ex) { Log.Error("settings save", ex); }
    }
}
