using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Dnui.Runtime.ContractTests.Support;

namespace Dnui.Runtime.ContractTests;

/// <summary>Constitution V: an app built against a different Dnui version is rejected (exit 13).</summary>
public class VersionMismatchContractTests
{
    [Fact]
    public async Task AppReferencingOtherDnuiVersion_Exits13()
    {
        using var dir = new TempDir();
        var dll = Path.Combine(dir.Path, "OtherVersionApp.dll");
        EmitAssemblyReferencingDnui(dll, new Version(9, 9, 9, 9));

        await using var runtime = RuntimeProcess.Start("--app-assembly", dll, "--app-type", "OtherVersionApp.App");
        Assert.Equal(13, await runtime.WaitForExitAsync());
        Assert.Contains("\"code\":\"DnuiVersionMismatch\"", Assert.Single(runtime.AllStdOut));
    }

    /// <summary>Emits a minimal library whose only notable content is an AssemblyRef to Dnui at <paramref name="dnuiVersion"/>.</summary>
    internal static void Emit(string path, Version dnuiVersion) => EmitAssemblyReferencingDnui(path, dnuiVersion);

    private static void EmitAssemblyReferencingDnui(string path, Version dnuiVersion)
    {
        var metadata = new MetadataBuilder();
        var name = metadata.GetOrAddString("OtherVersionApp");
        metadata.AddAssembly(name, new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.Sha1);
        metadata.AddModule(0, name, metadata.GetOrAddGuid(Guid.NewGuid()), default, default);

        var coreLib = typeof(object).Assembly.GetName();
        var coreRef = metadata.AddAssemblyReference(metadata.GetOrAddString(coreLib.Name!), coreLib.Version!, default, metadata.GetOrAddBlob(coreLib.GetPublicKeyToken()!), default, default);
        metadata.AddAssemblyReference(metadata.GetOrAddString("Dnui"), dnuiVersion, default, default, default, default);

        var objectRef = metadata.AddTypeReference(coreRef, metadata.GetOrAddString("System"), metadata.GetOrAddString("Object"));
        metadata.AddTypeDefinition(default, default, metadata.GetOrAddString("<Module>"), default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Class,
            metadata.GetOrAddString("OtherVersionApp"),
            metadata.GetOrAddString("App"),
            objectRef,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        var peBuilder = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder());
        var blob = new BlobBuilder();
        peBuilder.Serialize(blob);
        using var file = File.Create(path);
        blob.WriteContentTo(file);
    }
}
