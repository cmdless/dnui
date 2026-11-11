namespace Dnui.Tests.Api;

/// <summary>FR-002 / constitution I: window commands offer no app override and no tray.</summary>
public class WindowSurfaceTests
{
    [Fact]
    public void WindowCommands_HaveNoAppOrTrayMembers()
    {
        var methods = typeof(IDnuiWindowCommands).GetMethods().Concat(typeof(IDnuiDialogs).GetMethods());
        foreach (var method in methods)
        {
            Assert.DoesNotContain("Tray", method.Name);
            var types = method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType)
                .Concat(method.IsGenericMethodDefinition ? method.GetGenericArguments().SelectMany(g => g.GetGenericParameterConstraints()) : []);
            Assert.DoesNotContain(types, t => typeof(DnuiApp).IsAssignableFrom(t) || t == typeof(DnuiTray));
            Assert.DoesNotContain(method.GetParameters(), p => p.Name!.StartsWith("app", StringComparison.OrdinalIgnoreCase));
        }
    }
}
