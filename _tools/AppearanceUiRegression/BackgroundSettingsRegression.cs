using System.Xml.Linq;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

internal static class BackgroundSettingsRegression
{
    public static async Task RunAsync(Panel host)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace toolkit = "using:CommunityToolkit.WinUI.Controls";
        var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "GeneralSettingsControl.xaml"));
        var expanderSource = source.Descendants(toolkit + "SettingsExpander")
            .Single(element => ((string?)element.Attribute("Header"))?.Contains("WindowBackgroundTitle", StringComparison.Ordinal) == true);
        var markup = new XElement(xaml + "Grid",
            new XAttribute("xmlns", xaml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "controls", toolkit.NamespaceName),
            new XElement(expanderSource));
        if (expanderSource.ElementsAfterSelf().FirstOrDefault() is { } next && next.Name == xaml + "InfoBar")
            markup.Add(new XElement(next));

        // Use the shipping control hierarchy and real Toolkit templates. This test
        // exercises item realization; data/commands outside that boundary are inert.
        foreach (var element in markup.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().ToArray())
            {
                if (!attribute.Value.StartsWith("{x:Bind", StringComparison.Ordinal)) continue;
                if (attribute.Value.Contains("GetString(", StringComparison.Ordinal)) attribute.Value = "Background setting";
                else if (attribute.Name.LocalName == "Visibility") attribute.Value = "Collapsed";
                else if (attribute.Name.LocalName == "IsOpen") attribute.Value = "False";
                else if (attribute.Name.LocalName == "Value") attribute.Value = "20";
                else attribute.Remove();
            }
        }

        var grid = (Grid)XamlReader.Load(markup.ToString());
        // A StackPanel models the page's vertical flow without changing item templates.
        var stack = new StackPanel();
        while (grid.Children.Count != 0)
        {
            var child = grid.Children[0];
            grid.Children.RemoveAt(0);
            stack.Children.Add(child);
        }
        var expander = (SettingsExpander)stack.Children[0];
        host.Children.Add(stack);
        try
        {
            await Task.Delay(50);
            expander.IsExpanded = true;
            stack.UpdateLayout();
            await Task.Delay(50);
            if (!expander.IsExpanded) throw new Exception("Background settings did not expand");

            var warning = stack.Children.OfType<InfoBar>().Single();
            warning.Message = "Background image cannot be loaded";
            warning.Visibility = Visibility.Visible;
            warning.IsOpen = true;
            stack.UpdateLayout();
            if (warning.ActualHeight <= 0) throw new Exception("Background warning did not become visible");
            expander.IsExpanded = false;
            await Task.Delay(50);
            expander.IsExpanded = true;
            stack.UpdateLayout();
            await Task.Delay(50);
            stack.Measure(new Windows.Foundation.Size(700, double.PositiveInfinity));
            double heightWithWarning = stack.DesiredSize.Height;
            warning.IsOpen = false;
            warning.Visibility = Visibility.Collapsed;
            // Collapsed elements may retain their last ActualHeight in WinUI;
            // measure the containing flow to verify the warning takes no space.
            stack.Measure(new Windows.Foundation.Size(700, double.PositiveInfinity));
            if (stack.DesiredSize.Height >= heightWithWarning)
                throw new Exception("Closed warning retained layout height");
        }
        finally { host.Children.Remove(stack); }
    }
}
