using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

// 색 토큰 + 공통 스타일. 라이트=따뜻한 회백 / 다크=차분한 먹색, 포인트색은 하나(파랑)만 쓴다.
static class Theme
{
    static readonly string[] Keys  = { "Bg", "Paper", "Ink", "InkSoft", "Accent", "AccentSoft", "Border", "BorderStrong", "Amber", "Hover", "Icon" };
    static readonly string[] Light = { "#f5f5f3", "#ffffff", "#1c1d20", "#6b6f76", "#2563eb", "#e8effd", "#e6e6e3", "#d3d3cf", "#b45309", "#ebebe8", "#4a505c" };
    static readonly string[] Dark  = { "#16171a", "#1e2024", "#e6e8ec", "#9aa1ad", "#7aa7ff", "#26324f", "#2b2e34", "#3a3e46", "#e0a458", "#2a2d33", "#c3c9d4" };
    public static bool IsDark;

    const string StylesXaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

<!-- 글자 버튼 (대화상자 등) -->
<Style x:Key='TbBtn' TargetType='Button'>
 <Setter Property='Background' Value='{DynamicResource PaperBrush}'/>
 <Setter Property='Foreground' Value='{DynamicResource InkBrush}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource BorderStrongBrush}'/>
 <Setter Property='BorderThickness' Value='1'/>
 <Setter Property='Padding' Value='12,5'/>
 <Setter Property='Margin' Value='0,0,6,0'/>
 <Setter Property='Cursor' Value='Hand'/>
 <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
  <Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
   <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource HoverBrush}'/></Trigger>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource AccentBrush}'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
  </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='PrimaryBtn' TargetType='Button' BasedOn='{StaticResource TbBtn}'>
 <Setter Property='Background' Value='{DynamicResource AccentBrush}'/>
 <Setter Property='Foreground' Value='{DynamicResource PaperBrush}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource AccentBrush}'/>
 <Setter Property='FontWeight' Value='SemiBold'/>
</Style>

<!-- 아이콘 버튼: 테두리 없이 평소엔 조용하고, 올리면 옅은 배경이 켜진다 -->
<Style x:Key='IconBtn' TargetType='Button'>
 <Setter Property='Width' Value='32'/><Setter Property='Height' Value='32'/>
 <Setter Property='Margin' Value='1,0'/>
 <Setter Property='Background' Value='Transparent'/>
 <Setter Property='Foreground' Value='{DynamicResource IconBrush}'/>
 <Setter Property='Cursor' Value='Hand'/>
 <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
  <Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='Transparent' BorderThickness='1' CornerRadius='7'>
   <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource HoverBrush}'/></Trigger>
   <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource AccentSoftBrush}'/></Trigger>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource AccentBrush}'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
  </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<!-- 글자 아이콘 버튼 (H1, H2) -->
<Style x:Key='GlyphBtn' TargetType='Button' BasedOn='{StaticResource IconBtn}'>
 <Setter Property='FontSize' Value='12.5'/><Setter Property='FontWeight' Value='SemiBold'/>
 <Setter Property='Width' Value='32'/>
</Style>

<!-- 얇은 스크롤바 -->
<Style x:Key='SbPage' TargetType='RepeatButton'>
 <Setter Property='Focusable' Value='False'/><Setter Property='IsTabStop' Value='False'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></Setter.Value></Setter>
</Style>
<Style x:Key='SbThumb' TargetType='Thumb'>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Thumb'>
  <Border x:Name='t' Background='{DynamicResource BorderStrongBrush}' CornerRadius='4' Margin='2'/>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='t' Property='Background' Value='{DynamicResource InkSoftBrush}'/></Trigger>
   <Trigger Property='IsDragging' Value='True'><Setter TargetName='t' Property='Background' Value='{DynamicResource AccentBrush}'/></Trigger>
  </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>
<ControlTemplate x:Key='SbV' TargetType='ScrollBar'>
 <Grid Background='Transparent'>
  <Track x:Name='PART_Track' IsDirectionReversed='True'>
   <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Style='{StaticResource SbPage}'/></Track.DecreaseRepeatButton>
   <Track.Thumb><Thumb Style='{StaticResource SbThumb}'/></Track.Thumb>
   <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Style='{StaticResource SbPage}'/></Track.IncreaseRepeatButton>
  </Track>
 </Grid>
</ControlTemplate>
<ControlTemplate x:Key='SbH' TargetType='ScrollBar'>
 <Grid Background='Transparent'>
  <Track x:Name='PART_Track'>
   <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageLeftCommand' Style='{StaticResource SbPage}'/></Track.DecreaseRepeatButton>
   <Track.Thumb><Thumb Style='{StaticResource SbThumb}'/></Track.Thumb>
   <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageRightCommand' Style='{StaticResource SbPage}'/></Track.IncreaseRepeatButton>
  </Track>
 </Grid>
</ControlTemplate>
<Style TargetType='ScrollBar'>
 <Setter Property='Background' Value='Transparent'/>
 <Setter Property='Width' Value='12'/><Setter Property='Height' Value='Auto'/>
 <Setter Property='Template' Value='{StaticResource SbV}'/>
 <Style.Triggers>
  <Trigger Property='Orientation' Value='Horizontal'>
   <Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='12'/>
   <Setter Property='Template' Value='{StaticResource SbH}'/>
  </Trigger>
 </Style.Triggers>
</Style>

<!-- 목록 항목 (글꼴 선택 목록 등) -->
<Style x:Key='OptListItem' TargetType='ListBoxItem'>
 <Setter Property='Foreground' Value='{DynamicResource InkBrush}'/>
 <Setter Property='Cursor' Value='Hand'/>
 <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
 <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'>
  <Border x:Name='b' Background='Transparent' CornerRadius='5' Padding='10,5' Margin='2,0'>
   <ContentPresenter TextElement.Foreground='{TemplateBinding Foreground}'/></Border>
  <ControlTemplate.Triggers>
   <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource HoverBrush}'/></Trigger>
   <Trigger Property='IsSelected' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource AccentSoftBrush}'/><Setter Property='Foreground' Value='{DynamicResource AccentBrush}'/></Trigger>
  </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
</Style>

<Style TargetType='ToolTip'>
 <Setter Property='Background' Value='{DynamicResource PaperBrush}'/>
 <Setter Property='Foreground' Value='{DynamicResource InkBrush}'/>
 <Setter Property='BorderBrush' Value='{DynamicResource BorderStrongBrush}'/>
 <Setter Property='Padding' Value='8,5'/>
 <Setter Property='FontSize' Value='12'/>
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
