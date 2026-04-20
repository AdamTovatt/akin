using Akin.Core.Models;
using Akin.Core.Services;

namespace Akin.Tests
{
    public class ChunkerSelectorTests
    {
        private readonly ChunkerSelector _selector = new ChunkerSelector();

        [Theory]
        [InlineData("README.md", "markdown")]
        [InlineData("docs/intro.markdown", "markdown")]
        [InlineData("src/Foo.cs", "csharp")]
        [InlineData("app.js", "javascript")]
        [InlineData("app.jsx", "javascript")]
        [InlineData("app.ts", "javascript")]
        [InlineData("app.tsx", "javascript")]
        [InlineData("app.mjs", "javascript")]
        [InlineData("app.cjs", "javascript")]
        [InlineData("page.html", "html")]
        [InlineData("page.htm", "html")]
        [InlineData("styles.css", "css")]
        [InlineData("styles.scss", "css")]
        [InlineData("main.py", "python")]
        [InlineData("types.pyi", "python")]
        public void SelectFor_KnownExtension_ReturnsExpectedFormat(string relativePath, string expectedFormat)
        {
            ChunkerConfig? config = _selector.SelectFor(relativePath);
            Assert.NotNull(config);
            Assert.Equal(expectedFormat, config.FormatName);
        }

        [Theory]
        [InlineData("data.json")]
        [InlineData("config.yaml")]
        [InlineData("notes.txt")]
        [InlineData("script.sh")]
        [InlineData("main.go")]
        [InlineData("lib.rs")]
        [InlineData("project.csproj")]
        public void SelectFor_KnownTextExtension_ReturnsPlainText(string relativePath)
        {
            ChunkerConfig? config = _selector.SelectFor(relativePath);
            Assert.NotNull(config);
            Assert.Equal("plaintext", config.FormatName);
        }

        [Theory]
        [InlineData("Dockerfile")]
        [InlineData("Makefile")]
        [InlineData("LICENSE")]
        [InlineData("README")]
        [InlineData("Gemfile")]
        public void SelectFor_WellKnownExtensionlessFilename_ReturnsPlainText(string relativePath)
        {
            ChunkerConfig? config = _selector.SelectFor(relativePath);
            Assert.NotNull(config);
            Assert.Equal("plaintext", config.FormatName);
        }

        [Theory]
        [InlineData("logo.ai")]
        [InlineData("diagram.pdf")]
        [InlineData("image.png")]
        [InlineData("favicon.ico")]
        [InlineData("archive.zip")]
        [InlineData("binary.bin")]
        [InlineData("random_file_no_extension")]
        [InlineData("data.csv")]
        [InlineData("output.log")]
        public void SelectFor_UnknownOrKnownBinaryExtension_ReturnsNull(string relativePath)
        {
            ChunkerConfig? config = _selector.SelectFor(relativePath);
            Assert.Null(config);
        }

        [Theory]
        [InlineData("Art/Logo.ai")]
        [InlineData("icons/home.svg")]
        [InlineData("favicon.ico")]
        [InlineData("public/hero.png")]
        [InlineData("img/photo.jpeg")]
        [InlineData("docs/whitepaper.pdf")]
        [InlineData("fonts/Inter.woff2")]
        public void ShouldIndexByFilename_AssetExtensions_ReturnsTrue(string relativePath)
        {
            Assert.True(_selector.ShouldIndexByFilename(relativePath));
        }

        [Theory]
        [InlineData("main.cs")]
        [InlineData("README.md")]
        [InlineData("notes.txt")]
        [InlineData("data.csv")]
        [InlineData("Dockerfile")]
        public void ShouldIndexByFilename_TextFiles_ReturnsFalse(string relativePath)
        {
            Assert.False(_selector.ShouldIndexByFilename(relativePath));
        }

        [Fact]
        public void SelectFor_IsCaseInsensitiveOnExtension()
        {
            ChunkerConfig? upper = _selector.SelectFor("File.CS");
            ChunkerConfig? lower = _selector.SelectFor("file.cs");
            Assert.NotNull(upper);
            Assert.NotNull(lower);
            Assert.Equal(upper.FormatName, lower.FormatName);
        }

        [Fact]
        public void SelectFor_IsCaseInsensitiveOnFilename()
        {
            ChunkerConfig? upper = _selector.SelectFor("DOCKERFILE");
            ChunkerConfig? mixed = _selector.SelectFor("Dockerfile");
            Assert.NotNull(upper);
            Assert.NotNull(mixed);
            Assert.Equal(upper.FormatName, mixed.FormatName);
        }

        [Fact]
        public void Fingerprint_IsStableAcrossInstances()
        {
            string a = new ChunkerSelector().Fingerprint;
            string b = new ChunkerSelector().Fingerprint;
            Assert.Equal(a, b);
            Assert.False(string.IsNullOrEmpty(a));
        }

        [Theory]
        [InlineData("src/Foo.cs", FileKind.Code)]
        [InlineData("app.ts", FileKind.Code)]
        [InlineData("page.html", FileKind.Code)]
        [InlineData("main.py", FileKind.Code)]
        [InlineData("scripts/deploy.sh", FileKind.Code)]
        [InlineData("server.go", FileKind.Code)]
        [InlineData("lib.rs", FileKind.Code)]
        [InlineData("README.md", FileKind.Docs)]
        [InlineData("docs/intro.markdown", FileKind.Docs)]
        [InlineData("notes.txt", FileKind.Docs)]
        [InlineData("LICENSE", FileKind.Docs)]
        [InlineData("CHANGELOG", FileKind.Docs)]
        [InlineData("config.json", FileKind.Config)]
        [InlineData("pipeline.yaml", FileKind.Config)]
        [InlineData("Akin.Core.csproj", FileKind.Config)]
        [InlineData("Dockerfile", FileKind.Config)]
        [InlineData("Makefile", FileKind.Config)]
        [InlineData("assets/logo.png", FileKind.Config)]
        public void GetFileKind_Classifies(string relativePath, FileKind expected)
        {
            Assert.Equal(expected, _selector.GetFileKind(relativePath));
        }

        [Theory]
        [InlineData("code", FileKind.Code)]
        [InlineData("Code", FileKind.Code)]
        [InlineData("docs", FileKind.Docs)]
        [InlineData("doc", FileKind.Docs)]
        [InlineData("DOCS", FileKind.Docs)]
        [InlineData("config", FileKind.Config)]
        [InlineData("cfg", FileKind.Config)]
        public void FileKinds_TryParse_KnownValues(string input, FileKind expected)
        {
            Assert.True(FileKinds.TryParse(input, out FileKind kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData("source")]
        [InlineData("")]
        [InlineData("markdown")]
        public void FileKinds_TryParse_UnknownValue_ReturnsFalse(string input)
        {
            Assert.False(FileKinds.TryParse(input, out _));
        }
    }
}
