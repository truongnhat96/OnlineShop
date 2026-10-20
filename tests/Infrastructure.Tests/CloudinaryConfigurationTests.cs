using Infrastructure.AIChat;
using Infrastructure.Configuration;
using Infrastructure.EditorImages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests;

public sealed class CloudinaryConfigurationTests
{
    [Fact]
    public void RootEnvCompletesBlankCloudinaryFieldsWithoutExportingOtherSecrets()
    {
        WithEnv("Cloudinary__ApiKey=TEST_KEY\nCloudinary__ApiSecret='TEST_SECRET' # comment\nGEMINI_API_KEY=TEST_GEMINI\nSA_PASSWORD=DO_NOT_LOAD", (root, content) =>
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Cloudinary:CloudName"] = "configured-cloud", ["Cloudinary:ApiKey"] = "", ["Cloudinary:ApiSecret"] = ""
            }).Build();
            using var services = new ServiceCollection().AddEditorImages(config, content).BuildServiceProvider();
            var settings = services.GetRequiredService<IOptions<EditorImageOptions>>().Value;
            Assert.True(settings.IsConfigured);
            Assert.Equal("configured-cloud", settings.CloudName);
            Assert.Equal("TEST_KEY", settings.ApiKey);
            Assert.Equal("TEST_SECRET", settings.ApiSecret);
            Assert.Equal("TEST_GEMINI", LocalChatSettings.ReadToken(content));
            Assert.Equal(2, LocalCloudinarySettings.Read(content).Count);
            Assert.Null(config["SA_PASSWORD"]);
            Assert.Equal("motoshop/editor", settings.Folder);
        });
    }

    [Fact]
    public void ExplicitConfigurationWinsPerFieldAndProjectEnvWinsOverRootEnv()
    {
        WithEnv("Cloudinary__CloudName=root-cloud\nCloudinary__ApiKey=ROOT_KEY\nCloudinary__ApiSecret=ROOT_SECRET", (root, content) =>
        {
            File.WriteAllText(Path.Combine(content, ".env"), "export Cloudinary__ApiSecret=\"PROJECT_SECRET\" # comment");
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Cloudinary:ApiKey"] = "EXPLICIT_KEY" }).Build();
            using var services = new ServiceCollection().AddEditorImages(config, content).BuildServiceProvider();
            var settings = services.GetRequiredService<IOptions<EditorImageOptions>>().Value;
            Assert.Equal("root-cloud", settings.CloudName);
            Assert.Equal("EXPLICIT_KEY", settings.ApiKey);
            Assert.Equal("PROJECT_SECRET", settings.ApiSecret);
            Assert.True(settings.IsConfigured);
        });
    }

    [Fact]
    public void NoContentRootDoesNotLoadRealCheckoutSecretsAndMissingValuesRemainOptional()
    {
        using var services = new ServiceCollection().AddEditorImages(new ConfigurationBuilder().Build()).BuildServiceProvider();
        Assert.False(services.GetRequiredService<IOptions<EditorImageOptions>>().Value.IsConfigured);
    }

    [Theory]
    [InlineData("Cloudinary__ApiKey='unterminated")]
    [InlineData("Cloudinary__ApiKey='value' garbage")]
    [InlineData("Cloudinary__ApiKey= # comment")]
    public void InvalidOrEmptyEnvValuesAreIgnored(string content)
        => WithEnv(content, (_, path) => Assert.Empty(LocalCloudinarySettings.Read(path)));

    private static void WithEnv(string env, Action<string, string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "motoshop-cloudinary-config-" + Guid.NewGuid());
        var content = Path.Combine(root, "Infrastructure");
        Directory.CreateDirectory(content);
        try { File.WriteAllText(Path.Combine(root, "OnlineShop.sln"), "test marker"); File.WriteAllText(Path.Combine(root, ".env"), env); test(root, content); }
        finally { Directory.Delete(root, true); }
    }
}
