using System.Text.Json.Nodes;

namespace Dnui.TestApps;

/// <summary>A XAML form (quickstart §3). With DNUI_TEST_AUTOFILL set, it submits that name on open.</summary>
public partial class NameFormWindow : DnuiWindow
{
    public NameFormWindow()
    {
        InitializeComponent();
        SubmitButton.Click += (_, _) => Resolve(new JsonObject { ["name"] = NameBox.Text });
        if (Environment.GetEnvironmentVariable("DNUI_TEST_AUTOFILL") is { } name)
        {
            Opened += (_, _) =>
            {
                NameBox.Text = name;
                Resolve(new JsonObject { ["name"] = NameBox.Text });
            };
        }
    }
}
