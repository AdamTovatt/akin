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
            ChunkerConfig config = _selector.SelectFor(relativePath);
            Assert.Equal(expectedFormat, config.FormatName);
        }

        [Theory]
        [InlineData("something.unknown")]
        [InlineData("justafile")]
        [InlineData("data.json")]
        public void SelectFor_UnknownOrNoExtension_ReturnsPlainTextFallback(string relativePath)
        {
            ChunkerConfig config = _selector.SelectFor(relativePath);
            Assert.Equal("plaintext", config.FormatName);
        }

        [Fact]
        public void SelectFor_IsCaseInsensitive()
        {
            ChunkerConfig upper = _selector.SelectFor("File.CS");
            ChunkerConfig lower = _selector.SelectFor("file.cs");
            Assert.Equal(upper.FormatName, lower.FormatName);
        }

        [Fact]
        public void Fingerprint_IsStableAcrossInstances()
        {
            string a = new ChunkerSelector().Fingerprint;
            string b = new ChunkerSelector().Fingerprint;
            Assert.Equal(a, b);
            Assert.False(string.IsNullOrEmpty(a));
        }
    }
}
